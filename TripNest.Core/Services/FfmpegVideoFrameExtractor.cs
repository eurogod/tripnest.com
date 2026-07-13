using System.Diagnostics;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Extracts frames with the system ffmpeg binary. Availability is probed once at construction
/// (a fast <c>ffmpeg -version</c>); if the binary isn't installed the extractor advertises
/// itself unavailable and every extraction is a no-op, so the walkthrough check silently falls
/// back to listing photos. The video is streamed to a private temp file, sampled, and cleaned up.
/// </summary>
public class FfmpegVideoFrameExtractor : IVideoFrameExtractor
{
    private const long MaxVideoBytes = 500L * 1024 * 1024; // matches the walkthrough upload cap
    private const int MaxFrameBytes = 4_500_000;

    private readonly IFileStorage _fileStorage;
    private readonly ILogger<FfmpegVideoFrameExtractor> _logger;
    private readonly bool _available;

    public bool IsAvailable => _available;

    public FfmpegVideoFrameExtractor(IFileStorage fileStorage, ILogger<FfmpegVideoFrameExtractor> logger)
    {
        _fileStorage = fileStorage;
        _logger = logger;
        _available = ProbeFfmpeg(logger);
    }

    private static bool ProbeFfmpeg(ILogger logger)
    {
        try
        {
            using var probe = Process.Start(new ProcessStartInfo("ffmpeg", "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (probe is null)
                return false;
            probe.WaitForExit(5000);
            var ok = probe.HasExited && probe.ExitCode == 0;
            if (!ok)
                logger.LogInformation("ffmpeg not usable — walkthrough AI check will use listing photos only");
            return ok;
        }
        catch (Exception)
        {
            logger.LogInformation("ffmpeg not found on PATH — walkthrough AI check will use listing photos only");
            return false;
        }
    }

    public async Task<IReadOnlyList<AiImage>> ExtractFramesAsync(
        string storedVideoPath, int maxFrames, CancellationToken cancellationToken = default)
    {
        if (!_available || maxFrames <= 0 || string.IsNullOrWhiteSpace(storedVideoPath))
            return Array.Empty<AiImage>();

        var workDir = Path.Combine(Path.GetTempPath(), $"tripnest-frames-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workDir);

            // Pull the video down through the same storage abstraction (local disk or Azure Blob).
            var videoFile = Path.Combine(workDir, "video");
            await using (var source = await _fileStorage.OpenReadAsync(storedVideoPath, cancellationToken))
            {
                if (source is null)
                    return Array.Empty<AiImage>();
                await using var dest = File.Create(videoFile);
                await source.CopyToAsync(dest, cancellationToken);
                if (dest.Length is 0 or > MaxVideoBytes)
                    return Array.Empty<AiImage>();
            }

            // Sample evenly across the clip: -vf fps=n/duration is fiddly, so take one frame every
            // few seconds and cap the count — "thumbnail" picks representative frames, not black ones.
            var pattern = Path.Combine(workDir, "frame-%03d.jpg");
            var args = $"-i \"{videoFile}\" -vf \"thumbnail,fps=1/3\" -frames:v {maxFrames} -q:v 4 \"{pattern}\"";
            using var proc = Process.Start(new ProcessStartInfo("ffmpeg", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (proc is null)
                return Array.Empty<AiImage>();

            using (cancellationToken.Register(() => { try { if (!proc.HasExited) proc.Kill(true); } catch { } }))
                await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0)
            {
                _logger.LogWarning("ffmpeg frame extraction exited {Code} for {Path}", proc.ExitCode, storedVideoPath);
                return Array.Empty<AiImage>();
            }

            var frames = new List<AiImage>();
            foreach (var file in Directory.EnumerateFiles(workDir, "frame-*.jpg").OrderBy(f => f).Take(maxFrames))
            {
                var bytes = await File.ReadAllBytesAsync(file, cancellationToken);
                if (bytes.Length is > 0 and <= MaxFrameBytes)
                    frames.Add(new AiImage(bytes, "image/jpeg"));
            }
            return frames;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Video frame extraction failed for {Path} — falling back to photos", storedVideoPath);
            return Array.Empty<AiImage>();
        }
        finally
        {
            try { if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true); } catch { }
        }
    }
}
