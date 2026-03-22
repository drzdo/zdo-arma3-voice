using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using NAudio.Wave;

namespace ZdoArmaVoice.Server.Speech;

/// <summary>
/// STT via Google Cloud Speech-to-Text REST API (v1).
/// Records mic locally, sends audio to Google for transcription.
/// </summary>
public class GoogleRecognizer : ISpeechRecognizer
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _language;
    private WaveInEvent? _waveIn;
    private MemoryStream? _audioBuffer;
    private bool _recording;
    private readonly object _lock = new();

    public GoogleRecognizer(string apiKey, string language = "ru-RU", int micDevice = -1)
    {
        _apiKey = apiKey;
        _language = language;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

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
        Log.Info("GoogleSTT", "Recording started.");
    }

    public void StopRecording()
    {
        if (!_recording || _waveIn == null) return;
        _waveIn.StopRecording();
        _recording = false;
        Log.Info("GoogleSTT", "Recording stopped.");
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

        Log.Info("GoogleSTT", $"Sending {rawPcm.Length / 2} samples ({rawPcm.Length / 2 / 16000.0:F1}s)...");

        var audioBase64 = Convert.ToBase64String(rawPcm);

        var requestBody = new JsonObject
        {
            ["config"] = new JsonObject
            {
                ["encoding"] = "LINEAR16",
                ["sampleRateHertz"] = 16000,
                ["languageCode"] = _language,
                ["model"] = "command_and_search"
            },
            ["audio"] = new JsonObject
            {
                ["content"] = audioBase64
            }
        };

        var url = $"https://speech.googleapis.com/v1/speech:recognize?key={_apiKey}";

        try
        {
            var response = await _http.PostAsync(url,
                new StringContent(requestBody.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("GoogleSTT", $"API error ({response.StatusCode}): {body[..Math.Min(200, body.Length)]}");
                return "";
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return "";

            var transcript = results[0]
                .GetProperty("alternatives")[0]
                .GetProperty("transcript")
                .GetString() ?? "";

            Log.Info("GoogleSTT", $"Transcription: \"{transcript}\"");
            return transcript;
        }
        catch (Exception ex)
        {
            Log.Error("GoogleSTT", $"Error: {ex.Message}");
            return "";
        }
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
