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

    public Task<Stream?> OpenReadAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        // Resolve the stored web path to a physical path and confirm it stays within the uploads
        // area. This is what stops a caller passing an arbitrary path (e.g. "/etc/passwd" or
        // "/uploads/../../secrets") from reading files outside the upload directory.
        var uploadsRoot = Path.GetFullPath(Path.Combine(_root, "uploads"));
        var relative = (storedPath ?? string.Empty).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_root, relative));

        if (!fullPath.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new Exceptions.ValidationException("Invalid file path.");

        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }
}
