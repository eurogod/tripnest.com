using Microsoft.AspNetCore.Http;

namespace TripNest.Core.Interfaces.Services;

/// <summary>The kind of upload, which selects the allowed extensions and size cap.</summary>
public enum UploadKind { Image, Video }

/// <summary>
/// Abstraction over where uploaded media is stored. Implementations validate the file (type + size),
/// store it, and return a path/URL to persist and serve. Backed by Azure Blob Storage when configured
/// (multi-instance-safe) or local disk under <c>wwwroot/uploads</c> otherwise.
/// </summary>
public interface IFileStorage
{
    /// <summary>Validates and stores <paramref name="file"/> under a logical <paramref name="folder"/>;
    /// returns a path/URL to store on the entity and serve to clients.</summary>
    Task<string> SaveAsync(string folder, IFormFile file, UploadKind kind, CancellationToken cancellationToken = default);

    /// <summary>Best-effort removal of a previously stored file.</summary>
    Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default);
}
