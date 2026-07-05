using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Storage;

/// <summary>
/// Stores uploads in Azure Blob Storage so media is shared across every instance (and survives
/// restarts/scale-out). Returns the blob's absolute URL. The container is created with public-blob
/// read access so the URLs are directly servable — the storage account must permit public blob access
/// (otherwise front it with a CDN or switch to SAS URLs).
/// </summary>
public sealed class BlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;

    public BlobFileStorage(string connectionString, string containerName)
    {
        _container = new BlobServiceClient(connectionString).GetBlobContainerClient(containerName);
        _container.CreateIfNotExists(PublicAccessType.Blob);
    }

    public async Task<string> SaveAsync(string folder, IFormFile file, UploadKind kind, CancellationToken cancellationToken = default)
    {
        var ext = UploadValidation.Validate(file, kind);

        var blobName = $"{folder.Replace('\\', '/')}/{Guid.NewGuid():N}{ext}";
        var blob = _container.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType }
        }, cancellationToken);

        return blob.Uri.ToString();
    }

    public async Task DeleteAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storedPath) || !Uri.TryCreate(storedPath, UriKind.Absolute, out var uri))
            return;

        // The blob name is the path segment after the container name.
        var prefix = $"/{_container.Name}/";
        var idx = uri.AbsolutePath.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0)
            return;

        var blobName = Uri.UnescapeDataString(uri.AbsolutePath[(idx + prefix.Length)..]);
        await _container.DeleteBlobIfExistsAsync(blobName, cancellationToken: cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(string storedPath, CancellationToken cancellationToken = default)
    {
        // Only accept a URL that points into this container — anything else (arbitrary paths/URLs)
        // resolves to no blob, so this can never read files outside the configured storage.
        if (string.IsNullOrWhiteSpace(storedPath) || !Uri.TryCreate(storedPath, UriKind.Absolute, out var uri))
            return null;

        var prefix = $"/{_container.Name}/";
        var idx = uri.AbsolutePath.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0)
            return null;

        var blobName = Uri.UnescapeDataString(uri.AbsolutePath[(idx + prefix.Length)..]);
        var blob = _container.GetBlobClient(blobName);
        if (!await blob.ExistsAsync(cancellationToken))
            return null;

        var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return download.Value.Content;
    }
}
