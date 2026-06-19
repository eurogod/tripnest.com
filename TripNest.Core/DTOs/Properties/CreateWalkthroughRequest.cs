using Microsoft.AspNetCore.Http;

namespace TripNest.Core.DTOs.Properties;

public class CreateWalkthroughRequest
{
    public string PropertyId { get; set; } = string.Empty;
    public required string Title { get; set; }

    /// <summary>The video file to upload (mp4, mov, avi, webm — max 500 MB)</summary>
    public required IFormFile VideoFile { get; set; }
}
