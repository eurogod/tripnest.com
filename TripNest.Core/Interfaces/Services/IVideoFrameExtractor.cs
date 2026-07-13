namespace TripNest.Core.Interfaces.Services;

/// <summary>
/// Samples still frames from a video for vision analysis (walkthrough authenticity checks).
/// Backed by ffmpeg when it's on the host PATH; when it isn't, <see cref="IsAvailable"/> is false
/// and extraction returns an empty list so callers fall back to photo-only analysis rather than
/// failing. Same graceful-degradation contract as the other optional integrations.
/// </summary>
public interface IVideoFrameExtractor
{
    /// <summary>True when ffmpeg was found at startup and frame extraction can run.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Extracts up to <paramref name="maxFrames"/> evenly-spaced JPEG frames from the video at
    /// the given stored path. Returns an empty list when ffmpeg is unavailable or extraction fails.
    /// </summary>
    Task<IReadOnlyList<AiImage>> ExtractFramesAsync(string storedVideoPath, int maxFrames, CancellationToken cancellationToken = default);
}
