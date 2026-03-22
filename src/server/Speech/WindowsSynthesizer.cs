#pragma warning disable CA1416 // Windows-only API

using System.Speech.Synthesis;

namespace ZdoArmaVoice.Server.Speech;

/// <summary>
/// Text-to-speech using built-in Windows SAPI voices.
/// Zero setup — uses whatever voices are installed on the system.
/// </summary>
public class WindowsSynthesizer : ISpeechSynthesizer
{
    private readonly SpeechSynthesizer _synth;

    public WindowsSynthesizer(string? voiceName = null, int rate = 0)
    {
        _synth = new SpeechSynthesizer();

        // List available voices
        var voices = _synth.GetInstalledVoices();
        Log.Info("WindowsTTS", $"Available voices ({voices.Count}):");
        foreach (var v in voices)
        {
            var info = v.VoiceInfo;
            Log.Info("WindowsTTS", $"  {info.Name} ({info.Culture.Name}, {info.Gender}, {info.Age})");
        }

        if (!string.IsNullOrEmpty(voiceName))
        {
            try
            {
                _synth.SelectVoice(voiceName);
                Log.Info("WindowsTTS", $"Selected voice: {voiceName}");
            }
            catch
            {
                Log.Warn("WindowsTTS", $"Voice '{voiceName}' not found, using default");
            }
        }

        _synth.Rate = Math.Clamp(rate, -10, 10);

        Log.Info("WindowsTTS", $"Using voice: {_synth.Voice.Name} (rate: {_synth.Rate})");
    }

    public Task<byte[]> SynthesizeAsync(string text, SpeechContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(Array.Empty<byte>());

        try
        {
            using var stream = new MemoryStream();
            _synth.SetOutputToWaveStream(stream);
            _synth.Speak(text);
            _synth.SetOutputToNull();

            var wavBytes = stream.ToArray();
            Log.Info("WindowsTTS", $"Synthesized {wavBytes.Length} bytes for: \"{text[..Math.Min(50, text.Length)]}\"");
            return Task.FromResult(wavBytes);
        }
        catch (Exception ex)
        {
            Log.Error("WindowsTTS", $"TTS error: {ex.Message}");
            return Task.FromResult(Array.Empty<byte>());
        }
    }
}
