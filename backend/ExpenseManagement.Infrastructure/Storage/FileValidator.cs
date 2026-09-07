using ExpenseManagement.Application.Common.Interfaces;

namespace ExpenseManagement.Infrastructure.Storage;

/// <summary>
/// Validates uploaded images by inspecting the bytes.
///
/// The client-supplied Content-Type and filename are both attacker-controlled,
/// so neither is trusted for anything but a display label. A file is accepted
/// only if its leading bytes match a format on the allow-list — which is what
/// stops "invoice.jpg" that is actually an HTML page or a polyglot payload from
/// being stored and later served back to a browser.
/// </summary>
public class FileValidator : IFileValidator
{
    /// <summary>
    /// Formats we are willing to store. JPEG, PNG, WebP and HEIC cover every
    /// camera and gallery source on iOS and Android.
    ///
    /// SVG is deliberately absent: it is a document format that can carry
    /// script, and serving one from our own origin would be stored XSS.
    /// </summary>
    private static readonly ImageSignature[] AllowedSignatures =
    [
        new("image/jpeg", ".jpg", [0xFF, 0xD8, 0xFF]),
        new("image/png", ".png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        // RIFF....WEBP — the format marker sits at offset 8, after the size field.
        new("image/webp", ".webp", [0x52, 0x49, 0x46, 0x46], SecondaryOffset: 8, Secondary: [0x57, 0x45, 0x42, 0x50]),
        // ISO base media file with an 'ftyp' box at offset 4; brand identifies HEIC.
        new("image/heic", ".heic", [], SecondaryOffset: 4, Secondary: [0x66, 0x74, 0x79, 0x70]),
    ];

    /// <summary>Enough to cover the longest signature plus its offset.</summary>
    private const int HeaderBytes = 16;

    public async Task<FileValidationResult> ValidateImageAsync(
        Stream content,
        string? declaredContentType,
        string? fileName,
        long maxSizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (!content.CanRead)
        {
            return FileValidationResult.Invalid("The uploaded file could not be read.");
        }

        // A seekable stream lets us check the size before reading anything, so a
        // huge upload is rejected without being buffered.
        if (content.CanSeek)
        {
            if (content.Length == 0)
            {
                return FileValidationResult.Invalid("The file is empty.");
            }

            if (content.Length > maxSizeBytes)
            {
                var limitMb = Math.Round(maxSizeBytes / (1024d * 1024d), 1);
                return FileValidationResult.Invalid($"Images must be smaller than {limitMb} MB.");
            }

            content.Position = 0;
        }

        var header = new byte[HeaderBytes];
        var read = await ReadAtLeastAsync(content, header, cancellationToken);

        if (content.CanSeek) content.Position = 0;

        if (read < 4)
        {
            return FileValidationResult.Invalid("The file is too small to be an image.");
        }

        var match = AllowedSignatures.FirstOrDefault(sig => sig.Matches(header, read));

        if (match is null)
        {
            // Deliberately vague about *why*: enumerating exactly which bytes we
            // look for would help someone craft a polyglot that satisfies the
            // check. The user only needs to know which formats work.
            return FileValidationResult.Invalid(
                "That file is not a supported image. Use a JPEG, PNG, WebP or HEIC photo.");
        }

        // The declared type is only cross-checked, never trusted. A mismatch is
        // usually a confused client rather than an attack, so the sniffed type
        // wins and the upload proceeds.
        return FileValidationResult.Valid(match.ContentType, match.Extension);
    }

    /// <summary>
    /// Fills the buffer across however many reads the stream needs.
    /// A single ReadAsync is allowed to return fewer bytes than requested, and
    /// on a network-backed stream it usually does.
    /// </summary>
    private static async Task<int> ReadAtLeastAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;

        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0) break;
            total += read;
        }

        return total;
    }

    private sealed record ImageSignature(
        string ContentType,
        string Extension,
        byte[] Prefix,
        int SecondaryOffset = 0,
        byte[]? Secondary = null)
    {
        public bool Matches(byte[] header, int length)
        {
            if (Prefix.Length > 0)
            {
                if (length < Prefix.Length) return false;
                if (!header.AsSpan(0, Prefix.Length).SequenceEqual(Prefix)) return false;
            }

            if (Secondary is null || Secondary.Length == 0) return true;

            var end = SecondaryOffset + Secondary.Length;
            if (length < end) return false;

            return header.AsSpan(SecondaryOffset, Secondary.Length).SequenceEqual(Secondary);
        }
    }
}
