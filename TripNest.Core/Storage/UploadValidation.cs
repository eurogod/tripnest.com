using Microsoft.AspNetCore.Http;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Storage;

/// <summary>
/// Central validation for uploads: enforces a per-kind extension allowlist and size cap, so callers
/// can't store dangerous types (e.g. .html/.svg/.exe) or oversized files. Returns the validated,
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

        return ext;
    }
}
