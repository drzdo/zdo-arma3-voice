using System.Net.Http.Headers;
using System.Text.Json;
using NAudio.Wave;

namespace ArmaVoice.Server.Speech;

/// <summary>
/// Speech-to-text via Deepgram REST API.
/// Records mic locally with NAudio, sends audio to Deepgram for transcription.
/// </summary>
public class DeepgramRecognizer : ISpeechRecognizer
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _language;
    private WaveInEvent? _waveIn;
    private MemoryStream? _audioBuffer;
    private bool _recording;
    private readonly object _lock = new();

    public DeepgramRecognizer(string apiKey, string model = "nova-2", string language = "en")
    {
        _model = model;
        _language = language;

        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _audioBuffer = new MemoryStream();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_lock)
        {
            _audioBuffer?.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    public void StartRecording()
    {
        if (_recording || _waveIn == null) return;

        lock (_lock)
        {
            _audioBuffer?.SetLength(0);
            _audioBuffer?.Seek(0, SeekOrigin.Begin);
        }

        _waveIn.StartRecording();
        _recording = true;
        Console.WriteLine("[DeepgramRecognizer] Recording started.");
    }

    public void StopRecording()
    {
        if (!_recording || _waveIn == null) return;

        _waveIn.StopRecording();
        _recording = false;
        Console.WriteLine("[DeepgramRecognizer] Recording stopped.");
    }

    public async Task<string> TranscribeAsync()
    {
        byte[] rawPcm;
        lock (_lock)
        {
            if (_audioBuffer == null || _audioBuffer.Length == 0)
                return "";

            rawPcm = _audioBuffer.ToArray();
            _audioBuffer.SetLength(0);
            _audioBuffer.Seek(0, SeekOrigin.Begin);
        }

        Console.WriteLine($"[DeepgramRecognizer] Sending {rawPcm.Length / 2} samples ({rawPcm.Length / 2 / 16000.0:F1}s) to Deepgram...");

        // Build WAV in memory (Deepgram accepts raw WAV)
        var wavBytes = BuildWav(rawPcm, 16000, 16, 1);

        var url = $"https://api.deepgram.com/v1/listen?model={_model}&language={_language}&smart_format=true";

        try
        {
            var content = new ByteArrayContent(wavBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

            var response = await _http.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[DeepgramRecognizer] API error ({response.StatusCode}): {body[..Math.Min(200, body.Length)]}");
                return "";
            }

            using var doc = JsonDocument.Parse(body);
            var transcript = doc.RootElement
                .GetProperty("results")
                .GetProperty("channels")[0]
                .GetProperty("alternatives")[0]
                .GetProperty("transcript")
                .GetString() ?? "";

            Console.WriteLine($"[DeepgramRecognizer] Transcription: \"{transcript}\"");
            return transcript;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeepgramRecognizer] Error: {ex.Message}");
            return "";
        }
    }

    private static byte[] BuildWav(byte[] pcmData, int sampleRate, int bitsPerSample, int channels)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        int byteRate = sampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;

        writer.Write("RIFF"u8);
        writer.Write(36 + pcmData.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16); // subchunk size
        writer.Write((short)1); // PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);
        writer.Write("data"u8);
        writer.Write(pcmData.Length);
        writer.Write(pcmData);

        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            if (_recording)
            {
                _waveIn.StopRecording();
                _recording = false;
            }
            _waveIn.Dispose();
            _waveIn = null;
        }

        lock (_lock)
        {
            _audioBuffer?.Dispose();
            _audioBuffer = null;
        }

        _http.Dispose();
    }
}
