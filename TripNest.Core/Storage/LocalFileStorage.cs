using Microsoft.AspNetCore.Http;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Storage;

/// <summary>
/// Stores uploads on the local disk under <c>wwwroot/uploads/&lt;folder&gt;</c> and returns a web path
/// (e.g. <c>/uploads/properties/abc.jpg</c>) served by the static-file middleware. Single-instance /
/// development only — files live on one instance's ephemeral disk, so use Blob storage for scale-out.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IWebHostEnvironment env)
        => _root = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

    public async Task<string> SaveAsync(string folder, IFormFile file, UploadKind kind, CancellationToken cancellationToken = default)
    {
        var ext = UploadValidation.Validate(file, kind);

        var dir = Path.Combine(_root, "uploads", folder);
        Directory.CreateDirectory(dir);

        var name = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, name);
        await using (var stream = new FileStream(fullPath, FileMode.Create))
            await file.CopyToAsync(stream, cancellationToken);

        // Web path (forward slashes), served by UseStaticFiles.
        return $"/uploads/{folder.Replace('\\', '/')}/{name}";
    }

    public Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return Task.CompletedTask;

        var relative = storedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_root, relative));

        // Guard against path traversal — only delete within the web root.
        if (fullPath.StartsWith(Path.GetFullPath(_root), StringComparison.Ordinal) && File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }
}
