namespace ZdoArmaVoice.Server.Ai;

public interface IScreenCapture
{
    /// <summary>
    /// Capture the primary screen and return a JPEG-compressed image.
    /// Returns null if capture fails or is not supported on this platform.
    /// </summary>
    Task<LlmImage?> CaptureAsync();
}
