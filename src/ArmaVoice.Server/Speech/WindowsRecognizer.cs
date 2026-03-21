using System.Globalization;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace ArmaVoice.Server.Speech;

/// <summary>
/// STT using Windows built-in speech recognition (System.Speech).
/// Only works on Windows. Uses NAudio for mic capture, feeds audio to System.Speech.
/// </summary>
public class WindowsRecognizer : ISpeechRecognizer
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _audioBuffer;
    private bool _recording;
    private readonly object _lock = new();
    private readonly string _language;

    public WindowsRecognizer(string language = "en-US")
    {
        _language = language;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Windows speech recognition is only available on Windows.");

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _audioBuffer = new MemoryStream();

        Log.Info("WindowsSTT", $"Initialized with language: {_language}");
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
        Log.Info("WindowsSTT", "Recording started.");
    }

    public void StopRecording()
    {
        if (!_recording || _waveIn == null) return;

        _waveIn.StopRecording();
        _recording = false;
        Log.Info("WindowsSTT", "Recording stopped.");
    }

    public Task<string> TranscribeAsync()
    {
        byte[] rawPcm;
        lock (_lock)
        {
            if (_audioBuffer == null || _audioBuffer.Length == 0)
                return Task.FromResult("");

            rawPcm = _audioBuffer.ToArray();
            _audioBuffer.SetLength(0);
            _audioBuffer.Seek(0, SeekOrigin.Begin);
        }

        Log.Info("WindowsSTT", $"Transcribing {rawPcm.Length / 2} samples ({rawPcm.Length / 2 / 16000.0:F1}s)...");

        try
        {
            var result = RecognizeWithSystemSpeech(rawPcm, _language);
            Log.Info("WindowsSTT", $"Transcription: \"{result}\"");
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            Log.Error("WindowsSTT", $"Recognition failed: {ex}");
            return Task.FromResult("");
        }
    }

    private static string RecognizeWithSystemSpeech(byte[] pcmData, string language)
    {
        // Build a WAV in memory from raw PCM
        using var wavStream = new MemoryStream();
        using (var writer = new BinaryWriter(wavStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            int sampleRate = 16000, bitsPerSample = 16, channels = 1;
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
        }
        wavStream.Position = 0;

        // Use System.Speech via reflection to avoid compile-time dependency
        // (System.Speech is Windows-only and not available as a NuGet package for .NET 8)
        var culture = new CultureInfo(language);

        var speechAssembly = System.Reflection.Assembly.Load("System.Speech");
        var engineType = speechAssembly.GetType("System.Speech.Recognition.SpeechRecognitionEngine")!;
        var grammarType = speechAssembly.GetType("System.Speech.Recognition.DictationGrammar")!;

        using var engine = (IDisposable)Activator.CreateInstance(engineType, culture)!;

        // engine.LoadGrammar(new DictationGrammar())
        var grammar = Activator.CreateInstance(grammarType)!;
        engineType.GetMethod("LoadGrammar")!.Invoke(engine, [grammar]);

        // engine.SetInputToWaveStream(wavStream)
        engineType.GetMethod("SetInputToWaveStream")!.Invoke(engine, [wavStream]);

        // var result = engine.Recognize()
        var result = engineType.GetMethod("Recognize", Type.EmptyTypes)!.Invoke(engine, null);
        if (result == null) return "";

        // return result.Text
        var text = result.GetType().GetProperty("Text")!.GetValue(result) as string;
        return text ?? "";
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
    }
}
