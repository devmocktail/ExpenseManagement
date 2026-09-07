using System.Security.Cryptography;
using ExpenseManagement.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseManagement.Infrastructure.Storage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Root directory for uploads. Must sit OUTSIDE the web root — a file under
    /// wwwroot is served directly by the static-file middleware, which would
    /// bypass every authorisation check and make one user's receipts readable by
    /// anyone who can guess a path.
    /// </summary>
    public string RootPath { get; set; } = "storage";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}

/// <summary>
/// Disk-backed implementation of <see cref="IFileStorage"/>.
///
/// Storage keys are generated here and are opaque: a random 128-bit name plus a
/// date shard. Nothing derived from user input ever reaches the filesystem, so
/// path traversal is impossible by construction rather than by sanitising.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;
    private readonly ILogger<LocalFileStorage> _logger;
    private readonly string _rootFullPath;

    public LocalFileStorage(
        IOptions<FileStorageOptions> options,
        ILogger<LocalFileStorage> logger)
    {
        _options = options.Value;
        _logger = logger;

        _rootFullPath = Path.GetFullPath(_options.RootPath);
        Directory.CreateDirectory(_rootFullPath);
    }

    public async Task<string> SaveAsync(
        Stream content,
        string suggestedFileName,
        string contentType,
        string scope,
        CancellationToken cancellationToken = default)
    {
        // The caller's filename is used ONLY to pick an extension, and even that
        // is normalised to a short alphanumeric string. The stored name is
        // random, so two users uploading "receipt.jpg" never collide and neither
        // can address the other's file.
        var extension = NormaliseExtension(Path.GetExtension(suggestedFileName));
        var safeScope = NormaliseScope(scope);

        // Date sharding keeps any single directory small. Filesystems degrade
        // badly once a directory holds hundreds of thousands of entries.
        var now = DateTimeOffset.UtcNow;
        var relativeDirectory = Path.Combine(safeScope, now.ToString("yyyy"), now.ToString("MM"));

        var fileName = $"{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}{extension}";
        var storageKey = $"{relativeDirectory.Replace(Path.DirectorySeparatorChar, '/')}/{fileName}";

        var absoluteDirectory = Path.Combine(_rootFullPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var absolutePath = Path.Combine(absoluteDirectory, fileName);

        await using (var destination = new FileStream(
            absolutePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true))
        {
            if (content.CanSeek) content.Position = 0;
            await content.CopyToAsync(destination, cancellationToken);
        }

        _logger.LogInformation(
            "Stored file {StorageKey} ({ContentType})", storageKey, contentType);

        return storageKey;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(storageKey, out var absolutePath) || !File.Exists(absolutePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(storageKey, out var absolutePath) || !File.Exists(absolutePath))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(absolutePath);
            return Task.FromResult(true);
        }
        catch (IOException ex)
        {
            // A file that cannot be deleted is a storage problem, not a request
            // problem: the metadata row is already gone, so the blob is
            // unreachable either way and a cleanup job will collect it.
            _logger.LogWarning(ex, "Could not delete stored file {StorageKey}", storageKey);
            return Task.FromResult(false);
        }
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(TryResolve(storageKey, out var path) && File.Exists(path));

    /// <summary>
    /// Resolves a key to an absolute path and refuses anything that escapes the
    /// root.
    ///
    /// Keys are generated by this class and should always be safe, but they make
    /// a round trip through the database and the URL, so this is checked again
    /// on the way back in. Comparing fully-resolved paths defeats "..", absolute
    /// paths, and symlink tricks in one step.
    /// </summary>
    private bool TryResolve(string storageKey, out string absolutePath)
    {
        absolutePath = string.Empty;

        if (string.IsNullOrWhiteSpace(storageKey)) return false;

        var candidate = Path.GetFullPath(
            Path.Combine(_rootFullPath, storageKey.Replace('/', Path.DirectorySeparatorChar)));

        var rootWithSeparator = _rootFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootFullPath
            : _rootFullPath + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected storage key that resolved outside the root: {StorageKey}", storageKey);
            return false;
        }

        absolutePath = candidate;
        return true;
    }

    private static string NormaliseExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return string.Empty;

        var trimmed = extension.TrimStart('.');

        // Anything unexpected becomes no extension at all rather than being
        // cleaned up and kept — the file is addressed by key, not by name.
        if (trimmed.Length is 0 or > 5 || !trimmed.All(char.IsLetterOrDigit))
        {
            return string.Empty;
        }

        return "." + trimmed.ToLowerInvariant();
    }

    private static string NormaliseScope(string scope)
    {
        var cleaned = new string(scope.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return string.IsNullOrEmpty(cleaned) ? "misc" : cleaned;
    }
}
