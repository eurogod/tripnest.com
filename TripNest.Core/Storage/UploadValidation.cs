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
    private static readonly string[] AudioExtensions = { ".mp3", ".m4a", ".aac", ".ogg", ".oga", ".wav", ".webm" };
    private static readonly string[] DocumentExtensions = { ".pdf", ".doc", ".docx", ".txt" };
    private const long ImageMaxBytes = 10L * 1024 * 1024;   // 10 MB
    private const long VideoMaxBytes = 100L * 1024 * 1024;  // 100 MB (matches the request body limit)
    private const long AudioMaxBytes = 25L * 1024 * 1024;   // 25 MB — plenty for a chat voice note
    private const long DocumentMaxBytes = 25L * 1024 * 1024;

    public static string Validate(IFormFile file, UploadKind kind)
    {
        if (file is null || file.Length == 0)
            throw new ValidationException("No file was provided");

        var (allowed, max, label) = kind switch
        {
            UploadKind.Image => (ImageExtensions, ImageMaxBytes, "image"),
            UploadKind.Video => (VideoExtensions, VideoMaxBytes, "video"),
            UploadKind.Audio => (AudioExtensions, AudioMaxBytes, "audio"),
            _ => (DocumentExtensions, DocumentMaxBytes, "document"),
        };

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

        return kind switch
        {
            UploadKind.Image => IsKnownImage(header),
            UploadKind.Video => IsKnownVideo(header),
            UploadKind.Audio => IsKnownAudio(header),
            // Documents: a .pdf must actually be a PDF (the common, spoofable case). Office/text
            // formats (.doc/.docx/.txt) vary too much to fingerprint reliably, so for those the
            // extension allowlist + size cap are the guard (none can be a script/executable).
            _ => !IsPdfExtension(file.FileName) || Ascii(header, 0, "%PDF"),
        };
    }

    private static bool IsPdfExtension(string fileName) =>
        Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private static bool IsKnownImage(ReadOnlySpan<byte> h) =>
        StartsWith(h, 0xFF, 0xD8, 0xFF) ||                               // JPEG
        StartsWith(h, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A) || // PNG
        StartsWith(h, 0x47, 0x49, 0x46, 0x38) ||                         // GIF (GIF87a/GIF89a)
        (Ascii(h, 0, "RIFF") && Ascii(h, 8, "WEBP"));                    // WEBP

    private static bool IsKnownVideo(ReadOnlySpan<byte> h) =>
        Ascii(h, 4, "ftyp") ||                                           // MP4 / MOV (ISO base media)
        StartsWith(h, 0x1A, 0x45, 0xDF, 0xA3) ||                         // WEBM / MKV (EBML)
        (Ascii(h, 0, "RIFF") && Ascii(h, 8, "AVI "));                    // AVI

    private static bool IsKnownAudio(ReadOnlySpan<byte> h) =>
        Ascii(h, 0, "ID3") ||                                            // MP3 with ID3 tag
        StartsWith(h, 0xFF, 0xFB) || StartsWith(h, 0xFF, 0xF3) || StartsWith(h, 0xFF, 0xF2) || // MP3 frame sync
        Ascii(h, 4, "ftyp") ||                                           // M4A / AAC (ISO base media)
        Ascii(h, 0, "OggS") ||                                           // OGG / OGA
        (Ascii(h, 0, "RIFF") && Ascii(h, 8, "WAVE")) ||                  // WAV
        StartsWith(h, 0x1A, 0x45, 0xDF, 0xA3);                           // WEBM/Opus (EBML) — browser voice notes

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
