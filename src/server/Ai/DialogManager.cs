using System.Threading.Channels;
using NAudio.Wave;
using ZdoArmaVoice.Server.Audio;
using ZdoArmaVoice.Server.Game;
using ZdoArmaVoice.Server.Speech;

namespace ZdoArmaVoice.Server.Ai;

public record DialogRequest(string TargetNetId, string SystemInstructions, string Message, bool IsRadio);

/// <summary>
/// Manages the full NPC dialog lifecycle. One at a time, queued.
/// - Radio mode: radio effect applied, full volume
/// - Direct mode: spatial audio only, distance-attenuated
/// Spatial positioning updates in real-time during playback.
/// </summary>
public class DialogManager
{
    private readonly NpcDialog _npcDialog;
    private readonly ISpeechSynthesizer _tts;
    private readonly AudioPlayer _audioPlayer;
    private readonly RadioEffect _radioEffect;
    private readonly GameState _gameState;
    private readonly UnitRegistry _unitRegistry;
    private readonly float _radioPan;
    private readonly Channel<DialogRequest> _queue;

    public DialogManager(
        NpcDialog npcDialog,
        ISpeechSynthesizer tts,
        AudioPlayer audioPlayer,
        RadioEffect radioEffect,
        GameState gameState,
        UnitRegistry unitRegistry,
        float radioPan = 0f)
    {
        _npcDialog = npcDialog;
        _tts = tts;
        _audioPlayer = audioPlayer;
        _radioEffect = radioEffect;
        _gameState = gameState;
        _radioPan = radioPan;
        _unitRegistry = unitRegistry;

        _queue = Channel.CreateUnbounded<DialogRequest>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
    }

    public void Enqueue(string targetNetId, string systemInstructions, string message, bool isRadio = true)
    {
        _queue.Writer.TryWrite(new DialogRequest(targetNetId, systemInstructions, message, isRadio));
        Log.Info("DialogManager", $"Enqueued dialog with {targetNetId} ({(isRadio ? "radio" : "direct")})");
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Info("DialogManager", "Started.");
        await foreach (var request in _queue.Reader.ReadAllAsync(ct))
        {
            try { await ProcessDialogAsync(request, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { Log.Error("DialogManager", $"Error: {ex.Message}"); }
        }
    }

    private async Task ProcessDialogAsync(DialogRequest request, CancellationToken ct)
    {
        var unit = _unitRegistry.GetUnit(request.TargetNetId);
        var npcName = unit != null && !string.IsNullOrEmpty(unit.Name) ? unit.Name : "Soldier";
        var npcSide = unit != null && !string.IsNullOrEmpty(unit.Side) ? unit.Side : "UNKNOWN";

        Log.Info("DialogManager", $"Generating response from {npcName}...");

        // 1. LLM response
        var responseText = await _npcDialog.GenerateResponseAsync(
            request.SystemInstructions, request.Message, request.TargetNetId);
        ct.ThrowIfCancellationRequested();

        // 2. TTS
        Log.Info("DialogManager", $"TTS: \"{responseText[..Math.Min(60, responseText.Length)]}\"");
        var voiceContext = new SpeechContext(UnitName: npcName, Side: npcSide);
        var wavBytes = await _tts.SynthesizeAsync(responseText, voiceContext);
        if (wavBytes.Length == 0) { Log.Warn("DialogManager", "TTS returned empty."); return; }
        ct.ThrowIfCancellationRequested();

        // 3. Extract mono PCM
        var (samples, sampleRate) = ExtractPcm(wavBytes);
        if (samples.Length == 0) { Log.Warn("DialogManager", "PCM extraction failed."); return; }

        // 4. Apply radio effect if radio mode
        if (request.IsRadio)
        {
            Log.Info("DialogManager", "Radio effect");
            samples = _radioEffect.ApplyRadioEffect(samples, sampleRate);
        }
        else
        {
            Log.Info("DialogManager", "Direct/spatial audio");
        }

        // 5. Play through spatial provider
        var spatialProvider = new SpatialSampleProvider(
            samples, sampleRate, _gameState, request.TargetNetId, _unitRegistry, request.IsRadio, _radioPan);

        _audioPlayer.Play(spatialProvider);

        Log.Info("DialogManager", $"Playing {npcName}'s response...");

        while (_audioPlayer.IsPlaying && !ct.IsCancellationRequested)
            await Task.Delay(100, ct);

        Log.Info("DialogManager", $"Finished {npcName}.");
    }

    private static (float[] Samples, int SampleRate) ExtractPcm(byte[] audioData)
    {
        if (audioData.Length < 4) return ([], 0);

        try
        {
            using var ms = new MemoryStream(audioData);
            WaveStream reader;

            if (audioData[0] == 'R' && audioData[1] == 'I' && audioData[2] == 'F' && audioData[3] == 'F')
                reader = new WaveFileReader(ms);
            else if (audioData[0] == 0xFF || (audioData[0] == 0x49 && audioData[1] == 0x44 && audioData[2] == 0x33))
                reader = new Mp3FileReader(ms);
            else
            {
                Log.Warn("DialogManager", $"Unknown audio format: 0x{audioData[0]:X2}{audioData[1]:X2}");
                return ([], 0);
            }

            using (reader)
            {
                var sampleProvider = reader.ToSampleProvider();

                if (sampleProvider.WaveFormat.Channels > 1)
                    sampleProvider = sampleProvider.ToMono();

                var sampleRate = sampleProvider.WaveFormat.SampleRate;
                var samples = new List<float>();
                var buffer = new float[4096];
                int read;
                while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                        samples.Add(buffer[i]);
                }

                Log.Info("DialogManager", $"Extracted {samples.Count} samples at {sampleRate}Hz from {audioData.Length} bytes");
                return (samples.ToArray(), sampleRate);
            }
        }
        catch (Exception ex)
        {
            Log.Error("DialogManager", $"PCM extraction failed: {ex.Message}");
            return ([], 0);
        }
    }
}
