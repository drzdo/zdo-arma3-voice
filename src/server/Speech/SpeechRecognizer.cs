using Whisper.net;
using NAudio.Wave;

namespace ZdoArmaVoice.Server.Speech;

/// <summary>
/// Uses Whisper.net for speech-to-text. Records from microphone using NAudio,
/// transcribes with Whisper. Expected audio format: 16kHz, 16-bit, mono.
/// </summary>
public class WhisperRecognizer : ISpeechRecognizer
{
    private WhisperProcessor? _processor;
    private IWaveIn? _waveIn;
    private MemoryStream? _audioBuffer;
    private bool _recording;
    private readonly object _lock = new();

    public WhisperRecognizer(string modelPath = "ggml-base.en.bin", string language = "en", int micDevice = -1, string micMode = "wasapi")
    {
        var factory = WhisperFactory.FromPath(modelPath);
        _processor = factory.CreateBuilder()
            .WithLanguage(language)
            .Build();

        _waveIn = MicHelper.CreateWaveIn(micDevice, micMode);
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

    /// <summary>
    /// Start capturing mic audio into a MemoryStream buffer.
    /// </summary>
    public void StartRecording()
    {
        if (_recording || _waveIn == null)
            return;

        lock (_lock)
        {
            _audioBuffer?.SetLength(0);
            _audioBuffer?.Seek(0, SeekOrigin.Begin);
        }

        _waveIn.StartRecording();
        _recording = true;
        Log.Info("SpeechRecognizer", "Recording started.");
    }

    /// <summary>
    /// Stop capturing mic audio.
    /// </summary>
    public void StopRecording()
    {
        if (!_recording || _waveIn == null)
            return;

        _waveIn.StopRecording();
        _recording = false;
        Log.Info("SpeechRecognizer", "Recording stopped.");
    }

    /// <summary>
    /// Take the recorded buffer, convert to float samples, run whisper, return transcribed text.
    /// Resets the buffer after transcription.
    /// </summary>
    public async Task<string> TranscribeAsync()
    {
        if (_processor == null)
            return "";

        byte[] rawPcm;
        lock (_lock)
        {
            if (_audioBuffer == null || _audioBuffer.Length == 0)
                return "";

            rawPcm = _audioBuffer.ToArray();
            _audioBuffer.SetLength(0);
            _audioBuffer.Seek(0, SeekOrigin.Begin);
        }

        // Convert 16-bit PCM bytes to float samples (Whisper expects float32 in [-1, 1])
        var sampleCount = rawPcm.Length / 2;
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = (short)(rawPcm[i * 2] | (rawPcm[i * 2 + 1] << 8));
            samples[i] = sample / 32768f;
        }

        Log.Info("SpeechRecognizer", $"Transcribing {sampleCount} samples ({sampleCount / 16000.0:F1}s)...");

        var result = new System.Text.StringBuilder();
        await foreach (var segment in _processor.ProcessAsync(samples))
        {
            result.Append(segment.Text);
        }

        var text = result.ToString().Trim();
        Log.Info("SpeechRecognizer", $"Transcription: \"{text}\"");
        return text;
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

        _processor?.Dispose();
        _processor = null;

        lock (_lock)
        {
            _audioBuffer?.Dispose();
            _audioBuffer = null;
        }
    }
}
