namespace ArmaVoice.Server.Speech;

public interface ISpeechRecognizer : IDisposable
{
    void StartRecording();
    void StopRecording();
    Task<string> TranscribeAsync();
}
