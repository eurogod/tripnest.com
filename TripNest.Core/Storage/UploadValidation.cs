using Microsoft.AspNetCore.Http;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Storage;

/// <summary>
/// Central validation for uploads: enforces a per-kind extension allowlist, a size cap, and a
/// magic-byte (file-signature) check, so callers can't store dangerous types (e.g. .html/.svg/.exe),
/// oversized files, or a disallowed payload renamed to an allowed extension. Returns the validated,
/// lower-cased extension. Throws <see cref="ValidationException"/> (→ 400) on rejection.
/// </summary>
internal static class UploadValidation
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".avi", ".webm", ".mkv" };
    private const long ImageMaxBytes = 10L * 1024 * 1024;   // 10 MB
    private const long VideoMaxBytes = 100L * 1024 * 1024;  // 100 MB (matches the request body limit)

    public static string Validate(IFormFile file, UploadKind kind)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException("No file was provided");

        var (allowed, max, label) = kind == UploadKind.Image
            ? (ImageExtensions, ImageMaxBytes, "image")
            : (VideoExtensions, VideoMaxBytes, "video");

        if (file.Length > max)
            throw new ValidationException($"The {label} exceeds the {max / (1024 * 1024)} MB limit");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
            throw new ValidationException($"Unsupported {label} format. Allowed: {string.Join(", ", allowed)}");

        // The extension is attacker-controlled, so confirm the actual bytes look like the claimed kind.
        // This blocks e.g. an HTML/script or executable renamed to .jpg from ever being stored/served.
        if (!ContentMatchesKind(file, kind))
            throw new ValidationException($"The file contents are not a valid {label}.");

        return ext;
    }

    /// <summary>
    /// Reads the leading bytes and confirms they match a known signature for the claimed kind.
    /// Uses a fresh read stream (FormFile hands out a new stream per call, so this doesn't disturb
    /// the subsequent store).
    /// </summary>
    private static bool ContentMatchesKind(IFormFile file, UploadKind kind)
    {
        Span<byte> header = stackalloc byte[16];
        int read;
        using (var stream = file.OpenReadStream())
            read = stream.Read(header);
        header = header[..read];

        return kind == UploadKind.Image ? IsKnownImage(header) : IsKnownVideo(header);
    }

    private static bool IsKnownImage(ReadOnlySpan<byte> h) =>
        StartsWith(h, 0xFF, 0xD8, 0xFF) ||                               // JPEG
        StartsWith(h, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A) || // PNG
        StartsWith(h, 0x47, 0x49, 0x46, 0x38) ||                         // GIF (GIF87a/GIF89a)
        (Ascii(h, 0, "RIFF") && Ascii(h, 8, "WEBP"));                    // WEBP

    private static bool IsKnownVideo(ReadOnlySpan<byte> h) =>
        Ascii(h, 4, "ftyp") ||                                           // MP4 / MOV (ISO base media)
        StartsWith(h, 0x1A, 0x45, 0xDF, 0xA3) ||                         // WEBM / MKV (EBML)
        (Ascii(h, 0, "RIFF") && Ascii(h, 8, "AVI "));                    // AVI

    private static bool StartsWith(ReadOnlySpan<byte> h, params byte[] sig)
    {
        if (h.Length < sig.Length) return false;
        for (var i = 0; i < sig.Length; i++)
            if (h[i] != sig[i]) return false;
        return true;
    }

    private static bool Ascii(ReadOnlySpan<byte> h, int offset, string expected)
    {
        if (h.Length < offset + expected.Length) return false;
        for (var i = 0; i < expected.Length; i++)
            if (h[offset + i] != (byte)expected[i]) return false;
        return true;
    }
}
