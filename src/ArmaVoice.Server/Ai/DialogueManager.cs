using System.Threading.Channels;
using NAudio.Wave;
using ArmaVoice.Server.Audio;
using ArmaVoice.Server.Game;
using ArmaVoice.Server.Speech;

namespace ArmaVoice.Server.Ai;

public record DialogueRequest(string TargetNetId, string PlayerText);

/// <summary>
/// Manages the full NPC dialogue lifecycle. One at a time, queued.
/// - NPC within 10m at start of speech → spatial only
/// - NPC beyond 10m → radio effect baked onto audio + spatial
/// Spatial positioning updates in real-time during playback.
/// </summary>
public class DialogueManager
{
    private readonly NpcDialogue _npcDialogue;
    private readonly ISpeechSynthesizer _tts;
    private readonly AudioPlayer _audioPlayer;
    private readonly RadioEffect _radioEffect;
    private readonly GameState _gameState;
    private readonly UnitRegistry _unitRegistry;
    private readonly float _radioPan;
    private readonly float _radioDistance;
    private readonly Channel<DialogueRequest> _queue;

    public DialogueManager(
        NpcDialogue npcDialogue,
        ISpeechSynthesizer tts,
        AudioPlayer audioPlayer,
        RadioEffect radioEffect,
        GameState gameState,
        UnitRegistry unitRegistry,
        float radioPan = 0f,
        float radioDistance = 10f)
    {
        _npcDialogue = npcDialogue;
        _tts = tts;
        _audioPlayer = audioPlayer;
        _radioEffect = radioEffect;
        _gameState = gameState;
        _radioPan = radioPan;
        _radioDistance = radioDistance;
        _unitRegistry = unitRegistry;

        _queue = Channel.CreateUnbounded<DialogueRequest>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
    }

    public void Enqueue(string targetNetId, string playerText)
    {
        _queue.Writer.TryWrite(new DialogueRequest(targetNetId, playerText));
        Log.Info("DialogueManager", $"Enqueued dialogue with {targetNetId}");
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Info("DialogueManager", "Started.");
        await foreach (var request in _queue.Reader.ReadAllAsync(ct))
        {
            try { await ProcessDialogueAsync(request, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { Log.Error("DialogueManager", $"Error: {ex.Message}"); }
        }
    }

    private async Task ProcessDialogueAsync(DialogueRequest request, CancellationToken ct)
    {
        var unit = _unitRegistry.GetUnit(request.TargetNetId);
        if (unit == null)
        {
            Log.Warn("DialogueManager", $"Unit {request.TargetNetId} not found, skipping.");
            return;
        }

        var npcName = string.IsNullOrEmpty(unit.Name) ? "Soldier" : unit.Name;
        var npcRole = string.IsNullOrEmpty(unit.UnitType) ? "Infantry" : unit.UnitType;
        var npcSide = string.IsNullOrEmpty(unit.Side) ? "UNKNOWN" : unit.Side;

        Log.Info("DialogueManager", $"Generating response from {npcName}...");

        var nearbyUnits = _unitRegistry.GetAllUnits()
            .Where(u => u.NetId != request.TargetNetId)
            .Select(u => new UnitSummary
            {
                NetId = u.NetId, Name = u.Name, Side = u.Side,
                SameGroup = u.SameGroup, UnitType = u.UnitType
            })
            .ToList();

        // 1. LLM response
        var responseText = await _npcDialogue.GenerateResponseAsync(
            npcName, npcRole, npcSide, request.PlayerText, nearbyUnits, request.TargetNetId,
            _gameState.PlayerName, _gameState.PlayerRank);
        ct.ThrowIfCancellationRequested();

        // 2. TTS
        Log.Info("DialogueManager", $"TTS: \"{responseText[..Math.Min(60, responseText.Length)]}\"");
        var voiceContext = new SpeechContext(UnitName: npcName, Side: npcSide);
        var wavBytes = await _tts.SynthesizeAsync(responseText, voiceContext);
        if (wavBytes.Length == 0) { Log.Warn("DialogueManager", "TTS returned empty."); return; }
        ct.ThrowIfCancellationRequested();

        // 3. Extract mono PCM
        var (samples, sampleRate) = ExtractPcm(wavBytes);
        if (samples.Length == 0) { Log.Warn("DialogueManager", "PCM extraction failed."); return; }

        // 4. Determine distance at start of speech
        var playerPos = _gameState.PlayerPos;
        var npcPos = unit.Position;
        float dx = npcPos[0] - playerPos[0];
        float dy = npcPos[1] - playerPos[1];
        float dz = npcPos.Length > 2 && playerPos.Length > 2 ? npcPos[2] - playerPos[2] : 0f;
        float distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        // 5. If far → apply radio effect (no distance attenuation in spatial)
        var isRadio = distance >= _radioDistance;
        if (isRadio)
        {
            Log.Info("DialogueManager", $"Radio effect (distance: {distance:F1}m)");
            samples = _radioEffect.ApplyRadioEffect(samples, sampleRate);
        }
        else
        {
            Log.Info("DialogueManager", $"Spatial only (distance: {distance:F1}m)");
        }

        // 6. Play through spatial provider (radio=full volume, spatial=distance attenuated)
        var spatialProvider = new SpatialSampleProvider(
            samples, sampleRate, _gameState, request.TargetNetId, _unitRegistry, isRadio, _radioPan);

        _audioPlayer.Play(spatialProvider);

        Log.Info("DialogueManager", $"Playing {npcName}'s response...");

        // Wait for playback
        while (_audioPlayer.IsPlaying && !ct.IsCancellationRequested)
            await Task.Delay(100, ct);

        Log.Info("DialogueManager", $"Finished {npcName}.");
    }

    /// <summary>
    /// Extract mono float PCM from audio bytes. Handles WAV, MP3 via NAudio.
    /// </summary>
    private static (float[] Samples, int SampleRate) ExtractPcm(byte[] audioData)
    {
        if (audioData.Length < 4) return ([], 0);

        try
        {
            using var ms = new MemoryStream(audioData);
            WaveStream reader;

            // Detect format by header
            if (audioData[0] == 'R' && audioData[1] == 'I' && audioData[2] == 'F' && audioData[3] == 'F')
                reader = new WaveFileReader(ms);
            else if (audioData[0] == 0xFF || (audioData[0] == 0x49 && audioData[1] == 0x44 && audioData[2] == 0x33))
                reader = new Mp3FileReader(ms);
            else
            {
                Log.Warn("DialogueManager", $"Unknown audio format: 0x{audioData[0]:X2}{audioData[1]:X2}");
                return ([], 0);
            }

            using (reader)
            {
                var sampleProvider = reader.ToSampleProvider();

                // Mix to mono if stereo
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

                Log.Info("DialogueManager", $"Extracted {samples.Count} samples at {sampleRate}Hz from {audioData.Length} bytes");
                return (samples.ToArray(), sampleRate);
            }
        }
        catch (Exception ex)
        {
            Log.Error("DialogueManager", $"PCM extraction failed: {ex.Message}");
            return ([], 0);
        }
    }
}
