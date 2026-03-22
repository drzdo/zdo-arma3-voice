namespace ZdoArmaVoice.Server.Speech;

public record SpeechContext(string? UnitName = null, string? Side = null);

public interface ISpeechSynthesizer
{
    Task<byte[]> SynthesizeAsync(string text, SpeechContext? context = null);
}
