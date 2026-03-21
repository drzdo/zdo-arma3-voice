using System.Threading.Channels;
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
    private readonly Channel<DialogueRequest> _queue;

    private const float RadioDistanceThreshold = 10f;

    public DialogueManager(
        NpcDialogue npcDialogue,
        ISpeechSynthesizer tts,
        AudioPlayer audioPlayer,
        RadioEffect radioEffect,
        GameState gameState,
        UnitRegistry unitRegistry)
    {
        _npcDialogue = npcDialogue;
        _tts = tts;
        _audioPlayer = audioPlayer;
        _radioEffect = radioEffect;
        _gameState = gameState;
        _unitRegistry = unitRegistry;

        _queue = Channel.CreateUnbounded<DialogueRequest>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
    }

    public void Enqueue(string targetNetId, string playerText)
    {
        _queue.Writer.TryWrite(new DialogueRequest(targetNetId, playerText));
        Console.WriteLine($"[DialogueManager] Enqueued dialogue with {targetNetId}");
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("[DialogueManager] Started.");
        await foreach (var request in _queue.Reader.ReadAllAsync(ct))
        {
            try { await ProcessDialogueAsync(request, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { Console.WriteLine($"[DialogueManager] Error: {ex.Message}"); }
        }
    }

    private async Task ProcessDialogueAsync(DialogueRequest request, CancellationToken ct)
    {
        var unit = _unitRegistry.GetUnit(request.TargetNetId);
        if (unit == null)
        {
            Console.WriteLine($"[DialogueManager] Unit {request.TargetNetId} not found, skipping.");
            return;
        }

        var npcName = string.IsNullOrEmpty(unit.Name) ? "Soldier" : unit.Name;
        var npcRole = string.IsNullOrEmpty(unit.UnitType) ? "Infantry" : unit.UnitType;
        var npcSide = string.IsNullOrEmpty(unit.Side) ? "UNKNOWN" : unit.Side;

        Console.WriteLine($"[DialogueManager] Generating response from {npcName}...");

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
            npcName, npcRole, npcSide, request.PlayerText, nearbyUnits, request.TargetNetId);
        ct.ThrowIfCancellationRequested();

        // 2. TTS
        Console.WriteLine($"[DialogueManager] TTS: \"{responseText[..Math.Min(60, responseText.Length)]}\"");
        var voiceContext = new SpeechContext(UnitName: npcName, Side: npcSide);
        var wavBytes = await _tts.SynthesizeAsync(responseText, voiceContext);
        if (wavBytes.Length == 0) { Console.WriteLine("[DialogueManager] TTS returned empty."); return; }
        ct.ThrowIfCancellationRequested();

        // 3. Extract mono PCM
        var (samples, sampleRate) = ExtractPcmFromWav(wavBytes);
        if (samples.Length == 0) { Console.WriteLine("[DialogueManager] PCM extraction failed."); return; }

        // 4. Determine distance at start of speech
        var playerPos = _gameState.PlayerPos;
        var npcPos = unit.Position;
        float dx = npcPos[0] - playerPos[0];
        float dy = npcPos[1] - playerPos[1];
        float dz = npcPos.Length > 2 && playerPos.Length > 2 ? npcPos[2] - playerPos[2] : 0f;
        float distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        // 5. If far → apply radio effect to the mono samples first
        if (distance >= RadioDistanceThreshold)
        {
            Console.WriteLine($"[DialogueManager] Radio effect (distance: {distance:F1}m)");
            samples = _radioEffect.ApplyRadioEffect(samples, sampleRate);
        }
        else
        {
            Console.WriteLine($"[DialogueManager] Spatial only (distance: {distance:F1}m)");
        }

        // 6. Always play through real-time spatial provider
        var spatialProvider = new SpatialSampleProvider(
            samples, sampleRate, _gameState, request.TargetNetId, _unitRegistry);

        _audioPlayer.Play(spatialProvider);

        Console.WriteLine($"[DialogueManager] Playing {npcName}'s response...");

        // Wait for playback
        while (_audioPlayer.IsPlaying && !ct.IsCancellationRequested)
            await Task.Delay(100, ct);

        Console.WriteLine($"[DialogueManager] Finished {npcName}.");
    }

    private static (float[] Samples, int SampleRate) ExtractPcmFromWav(byte[] wav)
    {
        if (wav.Length < 44) return ([], 0);

        int channels = BitConverter.ToInt16(wav, 22);
        int sampleRate = BitConverter.ToInt32(wav, 24);
        int bitsPerSample = BitConverter.ToInt16(wav, 34);

        // Find "data" chunk
        int dataOffset = 12;
        int dataSize = 0;
        while (dataOffset + 8 <= wav.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(wav, dataOffset, 4);
            var chunkSize = BitConverter.ToInt32(wav, dataOffset + 4);
            if (chunkId == "data")
            {
                dataOffset += 8;
                dataSize = Math.Min(chunkSize, wav.Length - dataOffset);
                break;
            }
            dataOffset += 8 + chunkSize;
        }

        if (dataSize == 0) return ([], 0);

        float[] mono;
        if (bitsPerSample == 16)
        {
            int frames = dataSize / 2 / channels;
            mono = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                float sum = 0;
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = dataOffset + (i * channels + ch) * 2;
                    if (idx + 1 < wav.Length)
                        sum += BitConverter.ToInt16(wav, idx) / 32768f;
                }
                mono[i] = sum / channels;
            }
        }
        else if (bitsPerSample == 32)
        {
            int frames = dataSize / 4 / channels;
            mono = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                float sum = 0;
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = dataOffset + (i * channels + ch) * 4;
                    if (idx + 3 < wav.Length)
                        sum += BitConverter.ToSingle(wav, idx);
                }
                mono[i] = sum / channels;
            }
        }
        else return ([], 0);

        return (mono, sampleRate);
    }
}
