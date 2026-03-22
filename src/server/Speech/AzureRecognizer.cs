using System.Net.Http.Headers;
using System.Text.Json;
using NAudio.Wave;

namespace ZdoArmaVoice.Server.Speech;

/// <summary>
/// STT via Azure Cognitive Services Speech-to-Text REST API.
/// Records mic locally, sends WAV to Azure for transcription.
/// </summary>
public class AzureRecognizer : ISpeechRecognizer
{
    private readonly HttpClient _http;
    private readonly string _region;
    private readonly string _language;
    private WaveInEvent? _waveIn;
    private MemoryStream? _audioBuffer;
    private bool _recording;
    private readonly object _lock = new();

    public AzureRecognizer(string subscriptionKey, string region, string language = "ru-RU", int micDevice = -1)
    {
        _region = region;
        _language = language;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

        _waveIn = MicHelper.CreateWaveIn(micDevice);
        _waveIn.DataAvailable += OnDataAvailable;
        _audioBuffer = new MemoryStream();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_lock) { _audioBuffer?.Write(e.Buffer, 0, e.BytesRecorded); }
    }

    public void StartRecording()
    {
        if (_recording || _waveIn == null) return;
        lock (_lock) { _audioBuffer?.SetLength(0); _audioBuffer?.Seek(0, SeekOrigin.Begin); }
        _waveIn.StartRecording();
        _recording = true;
        Log.Info("AzureSTT", "Recording started.");
    }

    public void StopRecording()
    {
        if (!_recording || _waveIn == null) return;
        _waveIn.StopRecording();
        _recording = false;
        Log.Info("AzureSTT", "Recording stopped.");
    }

    public async Task<string> TranscribeAsync()
    {
        byte[] rawPcm;
        lock (_lock)
        {
            if (_audioBuffer == null || _audioBuffer.Length == 0) return "";
            rawPcm = _audioBuffer.ToArray();
            _audioBuffer.SetLength(0);
            _audioBuffer.Seek(0, SeekOrigin.Begin);
        }

        Log.Info("AzureSTT", $"Sending {rawPcm.Length / 2} samples ({rawPcm.Length / 2 / 16000.0:F1}s)...");

        var wavBytes = BuildWav(rawPcm, 16000, 16, 1);
        var url = $"https://{_region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language={_language}&format=detailed";

        try
        {
            var content = new ByteArrayContent(wavBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

            var response = await _http.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("AzureSTT", $"API error ({response.StatusCode}): {body[..Math.Min(200, body.Length)]}");
                return "";
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("DisplayText", out var displayText))
            {
                var transcript = displayText.GetString() ?? "";
                Log.Info("AzureSTT", $"Transcription: \"{transcript}\"");
                return transcript;
            }

            if (root.TryGetProperty("NBest", out var nBest) && nBest.GetArrayLength() > 0)
            {
                var transcript = nBest[0].GetProperty("Display").GetString() ?? "";
                Log.Info("AzureSTT", $"Transcription: \"{transcript}\"");
                return transcript;
            }

            Log.Warn("AzureSTT", "No transcription in response.");
            return "";
        }
        catch (Exception ex)
        {
            Log.Error("AzureSTT", $"Error: {ex.Message}");
            return "";
        }
    }

    private static byte[] BuildWav(byte[] pcmData, int sampleRate, int bitsPerSample, int channels)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmData.Length);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmData.Length);
        writer.Write(pcmData);
        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            if (_recording) { _waveIn.StopRecording(); _recording = false; }
            _waveIn.Dispose();
            _waveIn = null;
        }
        lock (_lock) { _audioBuffer?.Dispose(); _audioBuffer = null; }
        _http.Dispose();
    }
}
