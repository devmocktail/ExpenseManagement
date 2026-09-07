namespace ExpenseManagement.Application.Features.Receipts;

/// <summary>
/// Stores and serves the image evidence attached to a transaction.
///
/// Every member resolves the owner from the authenticated principal, never from
/// an argument, so there is no overload here that could be handed someone
/// else's id. A receipt that does not exist and a receipt that belongs to
/// another account both raise <c>NotFoundException</c>: answering 403 for the
/// second case would confirm the id is real and turn an incrementing id in a
/// URL into an enumeration oracle.
///
/// The metadata lives in the database and the bytes live behind
/// <c>IFileStorage</c>. Those are two stores that can fail independently, so
/// each write below commits them in the order whose half-done state is the one
/// a user can live with.
/// </summary>
public interface IReceiptService
{
    /// <summary>
    /// Validates an uploaded image, stores the bytes and records the metadata
    /// against the caller's transaction.
    /// </summary>
    /// <param name="length">
    /// Size the transport reported (an <c>IFormFile.Length</c>). Required
    /// because a multipart section streamed straight off the socket cannot
    /// report its own length, and an unbounded upload must be refused before it
    /// reaches the disk rather than after.
    /// </param>
    /// <param name="declaredContentType">
    /// The client's <c>Content-Type</c>. Advisory only — the stored type comes
    /// from sniffing the bytes, because this header is attacker-controlled.
    /// </param>
    /// <exception cref="ExpenseManagement.Domain.Exceptions.NotFoundException">
    /// The transaction does not exist, or is not the caller's.
    /// </exception>
    /// <exception cref="ExpenseManagement.Domain.Exceptions.BusinessRuleException">
    /// The attachment cap is reached, or the file is not an acceptable image.
    /// </exception>
    Task<ReceiptDto> UploadAsync(
        Guid transactionId,
        Stream content,
        string fileName,
        string? declaredContentType,
        long length,
        CancellationToken cancellationToken = default);

    /// <summary>Metadata for one receipt the caller owns.</summary>
    Task<ReceiptDto> GetByIdAsync(Guid receiptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the stored image for streaming back to the caller.
    ///
    /// This is the member that must never let user A read user B's receipt by
    /// editing an id, so it re-derives ownership from the principal instead of
    /// trusting that a route was already authorised upstream.
    /// </summary>
    Task<ReceiptContent> GetContentAsync(Guid receiptId, CancellationToken cancellationToken = default);

    /// <summary>Removes the metadata row, then the stored bytes.</summary>
    Task DeleteAsync(Guid receiptId, CancellationToken cancellationToken = default);
}
