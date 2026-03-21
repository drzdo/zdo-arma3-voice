using System.Globalization;
using System.Speech.Recognition;

namespace ArmaVoice.Server.Speech;

/// <summary>
/// STT using Windows built-in speech recognition (System.Speech).
/// Uses the default audio device directly — no NAudio needed.
/// </summary>
public class WindowsRecognizer : ISpeechRecognizer
{
    private readonly SpeechRecognitionEngine _engine;
    private string _lastResult = "";
    private readonly ManualResetEventSlim _done = new(false);
    private bool _recording;

    public WindowsRecognizer(string language = "en-US")
    {
        _engine = new SpeechRecognitionEngine(new CultureInfo(language));
        _engine.LoadGrammar(new DictationGrammar());
        _engine.SetInputToDefaultAudioDevice();

        _engine.SpeechRecognized += (_, e) =>
        {
            _lastResult = e.Result.Text;
            Log.Info("WindowsSTT", $"Recognized: \"{_lastResult}\"");
        };

        _engine.RecognizeCompleted += (_, _) =>
        {
            _done.Set();
        };

        Log.Info("WindowsSTT", $"Initialized with language: {language}");
    }

    public void StartRecording()
    {
        if (_recording) return;
        _lastResult = "";
        _done.Reset();
        _engine.RecognizeAsync(RecognizeMode.Multiple);
        _recording = true;
        Log.Info("WindowsSTT", "Recording started.");
    }

    public void StopRecording()
    {
        if (!_recording) return;
        _engine.RecognizeAsyncStop();
        _recording = false;
        Log.Info("WindowsSTT", "Recording stopped.");
    }

    public Task<string> TranscribeAsync()
    {
        // Wait briefly for any pending recognition to complete
        _done.Wait(TimeSpan.FromSeconds(2));
        var result = _lastResult;
        Log.Info("WindowsSTT", $"Transcription: \"{result}\"");
        return Task.FromResult(result);
    }

    public void Dispose()
    {
        if (_recording)
            _engine.RecognizeAsyncStop();
        _engine.Dispose();
        _done.Dispose();
    }
}
