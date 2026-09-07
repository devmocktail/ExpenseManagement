using ExpenseManagement.Application.Common.Interfaces;
using ExpenseManagement.Domain.Entities;
using ExpenseManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseManagement.Application.Features.Receipts;

/// <inheritdoc cref="IReceiptService"/>
public sealed class ReceiptService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IFileStorage fileStorage,
    IFileValidator fileValidator,
    IAuditService audit,
    ILogger<ReceiptService> logger) : IReceiptService
{
    /// <summary>
    /// The same 10 MB ceiling as CK_Receipts_FileSizeBytes_Range in the schema
    /// and the API's multipart body limit. Three layers enforce it and all three
    /// have to agree: a file that clears one and fails the next is either a
    /// confusing 500 or an orphaned blob.
    /// </summary>
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Room for a long till roll photographed in pieces, while stopping one
    /// transaction from being used as free image hosting.
    /// </summary>
    public const int MaxReceiptsPerTransaction = 5;

    /// <summary>Storage partition. The blob layout below it is opaque to this layer.</summary>
    private const string StorageScope = "receipts";

    /// <summary>Matches the nvarchar(260) column the display name is stored in.</summary>
    private const int MaxFileNameLength = 260;

    public async Task<ReceiptDto> UploadAsync(
        Guid transactionId,
        Stream content,
        string fileName,
        string? declaredContentType,
        long length,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        // Ownership is settled before anything expensive happens. Sniffing bytes
        // and writing a blob on behalf of a caller who turns out not to own the
        // transaction is work an attacker gets for free, and the cleanup path for
        // a stored-then-rejected upload is one more thing to get wrong.
        var transactionExists = await db.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.UserId == userId && transaction.Id == transactionId)
            .AnyAsync(cancellationToken);

        if (!transactionExists)
        {
            throw new NotFoundException("Transaction", transactionId);
        }

        // COUNT in SQL; the rows themselves are of no interest here.
        var attachedCount = await db.Receipts
            .AsNoTracking()
            .Where(receipt => receipt.UserId == userId && receipt.TransactionId == transactionId)
            .CountAsync(cancellationToken);

        // Two simultaneous uploads can both read four and both insert, landing a
        // transaction on six attachments. Left as is deliberately: the remedy is
        // serialising every upload behind a lock, and one extra receipt from a
        // double-tapped button is a far smaller problem than that.
        if (attachedCount >= MaxReceiptsPerTransaction)
        {
            throw new BusinessRuleException(
                $"You can attach up to {MaxReceiptsPerTransaction} receipts to one transaction. Remove one before adding another.",
                "receipt_limit_reached");
        }

        EnsureWithinSizeLimit(length);

        var validation = await fileValidator.ValidateImageAsync(
            content,
            declaredContentType,
            fileName,
            MaxFileSizeBytes,
            cancellationToken);

        // A valid result with no sniffed type would mean a broken validator, and
        // storing an unknown content type means later serving bytes the browser
        // has to guess at — the very sniffing the allow-list exists to avoid.
        if (!validation.IsValid || validation.DetectedContentType is null)
        {
            throw new BusinessRuleException(
                validation.Error ?? "That file is not a supported image.",
                "invalid_file");
        }

        var extension = validation.SafeExtension ?? string.Empty;

        // The extension follows the sniffed type, not the client's claim: storage
        // uses this argument only to pick one, and a PNG parked on disk as ".jpg"
        // misleads every tool that later looks at the blob store directly.
        var storageKey = await fileStorage.SaveAsync(
            content,
            $"receipt{extension}",
            validation.DetectedContentType,
            StorageScope,
            cancellationToken);

        var receipt = new Receipt
        {
            UserId = userId,
            TransactionId = transactionId,
            FileName = ResolveDisplayName(fileName, extension),
            StorageKey = storageKey,
            ContentType = validation.DetectedContentType,

            // What was actually written, not what the client announced.
            FileSizeBytes = content.CanSeek ? content.Length : length,
        };

        db.Receipts.Add(receipt);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The bytes are on disk but no row references them, and nothing sweeps
            // unreferenced files — an orphan here leaks until someone audits the
            // disk by hand. CancellationToken.None because a cancelled request is
            // one of the ways we arrive here, and the cleanup still has to run.
            await SafeDeleteBlobAsync(storageKey, CancellationToken.None);
            throw;
        }

        await audit.LogAsync(
            "receipt.uploaded",
            userId,
            nameof(Receipt),
            receipt.Id,
            metadata: new { receipt.TransactionId, receipt.ContentType, receipt.FileSizeBytes },
            cancellationToken: cancellationToken);

        return ReceiptMappings.ToDtoInMemory(receipt);
    }

    public async Task<ReceiptDto> GetByIdAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        // The context's ownership filter would scope this anyway; the explicit
        // predicate states the intent and still holds if an IgnoreQueryFilters
        // call is ever added upstream.
        return await db.Receipts
            .AsNoTracking()
            .Where(receipt => receipt.UserId == userId && receipt.Id == receiptId)
            .Select(ReceiptMappings.ToDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Receipt", receiptId);
    }

    public async Task<ReceiptContent> GetContentAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        // Projected to the four columns the response needs rather than to
        // ReceiptDto: the storage key must not leave this method, and a DTO
        // carrying a Stream is not something SQL can return.
        var stored = await db.Receipts
            .AsNoTracking()
            .Where(receipt => receipt.UserId == userId && receipt.Id == receiptId)
            .Select(receipt => new
            {
                receipt.StorageKey,
                receipt.ContentType,
                receipt.FileName,
                receipt.FileSizeBytes,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Receipt", receiptId);

        var stream = await fileStorage.OpenReadAsync(stored.StorageKey, cancellationToken);

        if (stream is null)
        {
            // A live row whose blob has vanished is our bug, not bad input, so it
            // is logged loudly — but it is still answered as a plain not-found,
            // because a distinct response here would tell an id-guessing client
            // which receipts exist.
            logger.LogError(
                "Receipt {ReceiptId} references storage key {StorageKey}, which no longer resolves",
                receiptId,
                stored.StorageKey);

            throw new NotFoundException("Receipt", receiptId);
        }

        return new ReceiptContent(stream, stored.ContentType, stored.FileName, stored.FileSizeBytes);
    }

    public async Task DeleteAsync(Guid receiptId, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.RequireUserId();

        var receipt = await db.Receipts
            .Where(entity => entity.UserId == userId && entity.Id == receiptId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Receipt", receiptId);

        var storageKey = receipt.StorageKey;
        var transactionId = receipt.TransactionId;

        // Row first, blob second. This order can only leave an unreferenced file,
        // which is invisible and collectable; the reverse can leave a row pointing
        // at nothing, which the user sees as a permanently broken image they have
        // no way to fix. SaveChanges turns this Remove into a tombstone, so the
        // row keeps reserving its storage key against the unique index until
        // retention purges it.
        db.Receipts.Remove(receipt);
        await db.SaveChangesAsync(cancellationToken);

        // Best effort and uncancellable: the metadata is already gone, so failing
        // the request now would report a failure for work that succeeded.
        await SafeDeleteBlobAsync(storageKey, CancellationToken.None);

        await audit.LogAsync(
            "receipt.deleted",
            userId,
            nameof(Receipt),
            receiptId,
            metadata: new { TransactionId = transactionId },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// The validator only compares against the ceiling when the stream can report
    /// its own length, which a multipart section read straight off the socket
    /// cannot. Checking the transport's figure first means an oversized upload is
    /// refused before a byte is written, and it stops a zero-length file reaching
    /// the database and failing the CHECK constraint as a 500.
    /// </summary>
    private static void EnsureWithinSizeLimit(long length)
    {
        if (length <= 0)
        {
            throw new BusinessRuleException("That file is empty.", "invalid_file");
        }

        if (length > MaxFileSizeBytes)
        {
            var limitMb = MaxFileSizeBytes / (1024 * 1024);
            throw new BusinessRuleException(
                $"Receipts must be smaller than {limitMb} MB.",
                "file_too_large");
        }
    }

    /// <summary>
    /// A name to show the user. Display only — it never reaches the filesystem,
    /// because storage generates its own key — so this is about readability and
    /// about fitting the column, not about traversal.
    /// </summary>
    private static string ResolveDisplayName(string? fileName, string extension)
    {
        var candidate = (fileName ?? string.Empty).Trim();

        // Clients send everything from "IMG_0421.HEIC" to a full device path; only
        // the last segment means anything to a person reading it.
        var separator = candidate.LastIndexOfAny(['/', '\\']);
        if (separator >= 0)
        {
            candidate = candidate[(separator + 1)..].Trim();
        }

        if (candidate.Length == 0)
        {
            return $"receipt{extension}";
        }

        // Truncated rather than rejected: an over-long name is cosmetic, and
        // failing the insert here would fail an upload whose bytes are already
        // stored, for a reason the user cannot act on.
        return candidate.Length > MaxFileNameLength
            ? candidate[..MaxFileNameLength]
            : candidate;
    }

    /// <summary>
    /// Removes stored bytes without letting a storage failure become the caller's
    /// problem. Both call sites have already reached the state the user asked for
    /// (or are unwinding one), and an unreferenced blob costs disk, not
    /// correctness.
    /// </summary>
    private async Task SafeDeleteBlobAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await fileStorage.DeleteAsync(storageKey, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Stored file {StorageKey} could not be removed and is now unreferenced",
                storageKey);
        }
    }
}
