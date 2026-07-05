namespace TripNest.Core.Interfaces.Services;

public interface IFaceMatchClient
{
    /// <summary>
    /// Compares two photos for face matching using the DeepFace sidecar, and runs a
    /// liveness/anti-spoofing check on the selfie to reject printed-photo or screen-replay attacks.
    /// </summary>
    /// <param name="selfiePhotoPath">Local file path to selfie photo</param>
    /// <param name="niaPhotoUrl">Public URL to NIA reference photo</param>
    /// <returns>Tuple of (similarity_score 0-100, liveness_score 0-100, failure_reason if any)</returns>
    Task<(double Score, double Liveness, string? FailureReason)> CompareFacesAsync(string selfiePhotoPath, string niaPhotoUrl);
}
