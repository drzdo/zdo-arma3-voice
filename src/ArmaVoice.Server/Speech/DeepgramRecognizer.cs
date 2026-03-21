using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NAudio.Wave;

namespace ArmaVoice.Server.Speech;

/// <summary>
/// Speech-to-text via Deepgram WebSocket streaming API.
/// Streams audio in real-time while recording — transcript is ready almost instantly on stop.
/// </summary>
public class DeepgramRecognizer : ISpeechRecognizer
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _language;
    private readonly string _encoding;
    private readonly int _sampleRate;

    private WaveInEvent? _waveIn;
    private ClientWebSocket? _ws;
    private string _transcript = "";
    private readonly object _lock = new();
    private bool _recording;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;

    public DeepgramRecognizer(string apiKey, string model, string language, string encoding, int sampleRate)
    {
        _apiKey = apiKey;
        _model = model;
        _language = language;
        _encoding = encoding;
        _sampleRate = sampleRate;

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(sampleRate, 16, 1),
            BufferMilliseconds = 100
        };
        _waveIn.DataAvailable += OnDataAvailable;
    }

    public void StartRecording()
    {
        if (_recording || _waveIn == null) return;

        _transcript = "";
        _cts = new CancellationTokenSource();

        // Connect WebSocket
        _ws = new ClientWebSocket();
        _ws.Options.SetRequestHeader("Authorization", $"Token {_apiKey}");

        var url = $"wss://api.deepgram.com/v1/listen?model={_model}&language={_language}&encoding={_encoding}&sample_rate={_sampleRate}&smart_format=true&interim_results=false";

        try
        {
            _ws.ConnectAsync(new Uri(url), _cts.Token).GetAwaiter().GetResult();
            Log.Info("Deepgram", "WebSocket connected, streaming...");
        }
        catch (Exception ex)
        {
            Log.Error("Deepgram", $"WebSocket connect failed: {ex.Message}");
            _ws.Dispose();
            _ws = null;
            return;
        }

        // Start receiving transcripts in background
        _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token));

        // Start mic
        _waveIn.StartRecording();
        _recording = true;
        Log.Info("Deepgram", "Recording started (streaming).");
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_ws?.State != WebSocketState.Open) return;

        try
        {
            // Send raw PCM audio to Deepgram
            var segment = new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded);
            _ws.SendAsync(segment, WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch { /* connection may have closed */ }
    }

    public void StopRecording()
    {
        if (!_recording || _waveIn == null) return;

        _waveIn.StopRecording();
        _recording = false;
        Log.Info("Deepgram", "Recording stopped, finalizing...");

        // Send close message to Deepgram to flush final transcript
        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                // Deepgram expects a JSON close message
                var closeMsg = Encoding.UTF8.GetBytes("{\"type\":\"CloseStream\"}");
                _ws.SendAsync(new ArraySegment<byte>(closeMsg), WebSocketMessageType.Text, true, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch { }
        }
    }

    public async Task<string> TranscribeAsync()
    {
        // Wait for receive loop to get the final transcript
        if (_receiveTask != null)
        {
            try
            {
                var timeout = Task.Delay(5000);
                await Task.WhenAny(_receiveTask, timeout);
            }
            catch { }
        }

        // Cleanup WebSocket
        if (_ws != null)
        {
            try { _ws.Dispose(); } catch { }
            _ws = null;
        }
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _receiveTask = null;

        var result = _transcript;
        Log.Info("Deepgram", $"Transcription: \"{result}\"");
        return result;
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var buffer = new byte[8192];
        var msgBuffer = new StringBuilder();

        try
        {
            while (_ws?.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    msgBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        var json = msgBuffer.ToString();
                        msgBuffer.Clear();
                        ParseTranscript(json);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { /* connection closed */ }
        catch (Exception ex) { Log.Error("Deepgram", $"Receive error: {ex.Message}"); }
    }

    private void ParseTranscript(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp)) return;
            var type = typeProp.GetString();

            if (type != "Results") return;

            var isFinal = root.GetProperty("is_final").GetBoolean();
            if (!isFinal) return;

            var transcript = root
                .GetProperty("channel")
                .GetProperty("alternatives")[0]
                .GetProperty("transcript")
                .GetString() ?? "";

            if (!string.IsNullOrEmpty(transcript))
            {
                lock (_lock)
                {
                    if (_transcript.Length > 0) _transcript += " ";
                    _transcript += transcript;
                }
                Log.Info("Deepgram", $"Partial: \"{transcript}\"");
            }
        }
        catch { /* ignore malformed messages */ }
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

        _cts?.Cancel();
        try { _ws?.Dispose(); } catch { }
        _ws = null;
        _cts?.Dispose();
        _cts = null;
    }
}
