namespace ExpenseManagement.Application.Common.Interfaces;

/// <summary>Outcome of inspecting an uploaded file before it is stored.</summary>
public sealed record FileValidationResult(
    bool IsValid,
    string? Error,
    string? DetectedContentType,
    string? SafeExtension)
{
    public static FileValidationResult Invalid(string error) => new(false, error, null, null);

    public static FileValidationResult Valid(string contentType, string extension) =>
        new(true, null, contentType, extension);
}

/// <summary>
/// Validates uploads by sniffing magic bytes rather than trusting the
/// client-supplied Content-Type or file extension, both of which are attacker
/// controlled. Only formats on the allow-list survive.
/// </summary>
public interface IFileValidator
{
    Task<FileValidationResult> ValidateImageAsync(
        Stream content,
        string? declaredContentType,
        string? fileName,
        long maxSizeBytes,
        CancellationToken cancellationToken = default);
}
