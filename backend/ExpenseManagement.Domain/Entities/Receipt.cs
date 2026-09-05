using ExpenseManagement.Domain.Common;

namespace ExpenseManagement.Domain.Entities;

/// <summary>
/// Metadata for a receipt image. The bytes live in blob/disk storage behind
/// <c>IFileStorage</c>; only the key is persisted, keeping the database small
/// and letting storage move to S3/Azure without a schema change.
/// </summary>
public class Receipt : UserOwnedEntity
{
    public ApplicationUser User { get; set; } = null!;

    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;

    /// <summary>Original client filename, retained for display only — never used to build a path.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Server-generated opaque storage key. The only value used to locate the bytes.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Sniffed and allow-listed on upload, not taken from the client header.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int? Width { get; set; }
    public int? Height { get; set; }
}
