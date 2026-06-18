namespace TripNest.Core.Interfaces.Services;

public interface IFaceMatchClient
{
    /// <summary>
    /// Compares two photos for face matching using DeepFace sidecar service.
    /// </summary>
    /// <param name="selfiePhotoPath">Local file path to selfie photo</param>
    /// <param name="niaPhotoUrl">Public URL to NIA reference photo</param>
    /// <returns>Tuple of (similarity_score 0-100, failure_reason if any)</returns>
    Task<(double Score, string? FailureReason)> CompareFacesAsync(string selfiePhotoPath, string niaPhotoUrl);
}
