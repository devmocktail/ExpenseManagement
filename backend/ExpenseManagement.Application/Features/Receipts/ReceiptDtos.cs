using System.Linq.Expressions;
using ExpenseManagement.Domain.Entities;

namespace ExpenseManagement.Application.Features.Receipts;

/// <summary>
/// A stored receipt as the client sees it.
///
/// Deliberately not the whole <see cref="Receipt"/> row: the storage key is a
/// server-side secret, and putting it on the wire would invite clients to build
/// their own blob URLs and route around authorisation entirely.
/// </summary>
public sealed record ReceiptDto(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    /// An authorised API path — never a filesystem path, never a public URL, so
    /// the bytes stay behind the bearer token. Computed in the record body
    /// rather than inside the projection because interpolating a Guid in an
    /// expression tree is a provider-translation gamble for a value that costs
    /// nothing to format in memory.
    /// </summary>
    public string Url => ReceiptUrls.Content(Id);
}

/// <summary>
/// The one place the receipt content route is spelled out. Anything that needs
/// to hand a client a receipt link calls this instead of interpolating its own,
/// so moving or versioning the endpoint is a single edit rather than a hunt for
/// string literals that have quietly drifted apart.
/// </summary>
public static class ReceiptUrls
{
    public static string Content(Guid receiptId) => $"/api/v1/receipts/{receiptId}/content";
}

/// <summary>
/// The bytes of a receipt plus what a response needs to describe them.
/// </summary>
/// <param name="Content">
/// An open stream the caller owns and must dispose — ASP.NET Core's
/// <c>FileStreamResult</c> does that once the response is written. It is handed
/// over unread so a 10 MB photo is piped to the socket rather than buffered.
/// </param>
/// <param name="ContentType">
/// The type sniffed from the bytes at upload time, never a header the client
/// sent then or sends now.
/// </param>
public sealed record ReceiptContent(
    Stream Content,
    string ContentType,
    string FileName,
    long FileSizeBytes);

/// <summary>
/// The one projection every receipt read goes through, so a receipt returned by
/// an upload and one returned by a lookup can never describe the same row
/// differently.
/// </summary>
public static class ReceiptMappings
{
    /// <summary>
    /// Held in a get-only property so the tree is built once per process rather
    /// than per request.
    /// </summary>
    public static Expression<Func<Receipt, ReceiptDto>> ToDto { get; } = receipt => new ReceiptDto(
        receipt.Id,
        receipt.FileName,
        receipt.ContentType,
        receipt.FileSizeBytes,
        receipt.CreatedAt);

    /// <summary>
    /// The same projection, compiled once, for a row a write has just saved.
    /// Re-reading five columns that are already in memory would only buy a
    /// second round trip — and compiling the shared expression rather than
    /// hand-writing a twin is what stops the two paths drifting.
    /// </summary>
    public static Func<Receipt, ReceiptDto> ToDtoInMemory { get; } = ToDto.Compile();
}
