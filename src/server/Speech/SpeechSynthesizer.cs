namespace ZdoArmaVoice.Server.Speech;

/// <summary>
/// Text-to-speech via a local Piper TTS server (HTTP).
/// Posts text, receives WAV bytes. Falls back gracefully if Piper is unavailable.
/// </summary>
public class PiperSynthesizer : ISpeechSynthesizer
{
    private readonly HttpClient _http;
    private readonly string _piperUrl;

    public PiperSynthesizer(string piperUrl = "http://localhost:5000")
    {
        _piperUrl = piperUrl.TrimEnd('/');
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Synthesize text to WAV audio bytes using Piper TTS.
    /// Returns empty array and logs a warning if Piper is not available.
    /// </summary>
    public async Task<byte[]> SynthesizeAsync(string text, SpeechContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        try
        {
            var content = new StringContent(text, System.Text.Encoding.UTF8, "text/plain");
            var response = await _http.PostAsync($"{_piperUrl}/api/tts", content);
            response.EnsureSuccessStatusCode();

            var wavBytes = await response.Content.ReadAsByteArrayAsync();
            Log.Info("PiperTTS", $"Synthesized {wavBytes.Length} bytes for: \"{text[..Math.Min(50, text.Length)]}\"");
            return wavBytes;
        }
        catch (HttpRequestException ex)
        {
            Log.Error("PiperTTS", $"Piper TTS unavailable at {_piperUrl}: {ex.Message}");
            return [];
        }
        catch (TaskCanceledException)
        {
            Log.Warn("PiperTTS", $"Piper TTS request timed out for: \"{text[..Math.Min(50, text.Length)]}\"");
            return [];
        }
        catch (Exception ex)
        {
            Log.Error("PiperTTS", $"TTS error: {ex.Message}");
            return [];
        }
    }
}
