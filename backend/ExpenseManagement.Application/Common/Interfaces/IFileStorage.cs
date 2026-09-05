namespace ExpenseManagement.Application.Common.Interfaces;

/// <summary>
/// Binary storage for receipts and avatars. Implemented over the local disk in
/// development and swappable for blob storage in production without touching a
/// caller — which is why nothing above this interface ever sees a file path.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Persists a stream and returns an opaque storage key. The implementation
    /// generates the key itself; the caller's filename is advisory only and is
    /// never used to build a path, so "../../web.config" cannot escape the root.
    /// </summary>
    Task<string> SaveAsync(
        Stream content,
        string suggestedFileName,
        string contentType,
        string scope,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a stored object, or null when the key does not resolve.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
}
