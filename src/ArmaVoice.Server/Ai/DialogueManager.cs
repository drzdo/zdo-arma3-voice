using System.Threading.Channels;
using ArmaVoice.Server.Audio;
using ArmaVoice.Server.Game;
using ArmaVoice.Server.Speech;

namespace ArmaVoice.Server.Ai;

/// <summary>
/// Request to process an NPC dialogue interaction.
/// </summary>
public record DialogueRequest(string TargetNetId, string PlayerText);

/// <summary>
/// Manages the full NPC dialogue lifecycle. Processes one dialogue at a time,
/// queuing additional requests. Orchestrates NPC dialogue generation, TTS,
/// audio effects (spatial or radio), and playback.
/// </summary>
public class DialogueManager
{
    private readonly NpcDialogue _npcDialogue;
    private readonly ISpeechSynthesizer _tts;
    private readonly AudioPlayer _audioPlayer;
    private readonly SpatialMixer _spatialMixer;
    private readonly RadioEffect _radioEffect;
    private readonly GameState _gameState;
    private readonly UnitRegistry _unitRegistry;
    private readonly Channel<DialogueRequest> _queue;

    private const float RadioDistanceThreshold = 30f;

    public DialogueManager(
        NpcDialogue npcDialogue,
        ISpeechSynthesizer tts,
        AudioPlayer audioPlayer,
        SpatialMixer spatialMixer,
        RadioEffect radioEffect,
        GameState gameState,
        UnitRegistry unitRegistry)
    {
        _npcDialogue = npcDialogue;
        _tts = tts;
        _audioPlayer = audioPlayer;
        _spatialMixer = spatialMixer;
        _radioEffect = radioEffect;
        _gameState = gameState;
        _unitRegistry = unitRegistry;

        _queue = Channel.CreateUnbounded<DialogueRequest>(new UnboundedChannelOptions
        {
            SingleReader = true
        });
    }

    /// <summary>
    /// Enqueue a dialogue request for processing.
    /// </summary>
    public void Enqueue(string targetNetId, string playerText)
    {
        _queue.Writer.TryWrite(new DialogueRequest(targetNetId, playerText));
        Console.WriteLine($"[DialogueManager] Enqueued dialogue with {targetNetId}: \"{playerText[..Math.Min(50, playerText.Length)]}\"");
    }

    /// <summary>
    /// Main processing loop. Reads from the queue and processes one dialogue at a time.
    /// Run this as a background task.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine("[DialogueManager] Dialogue processing loop started.");

        await foreach (var request in _queue.Reader.ReadAllAsync(ct))
        {
            try
            {
                await ProcessDialogueAsync(request, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DialogueManager] Error processing dialogue: {ex.Message}");
            }
        }

        Console.WriteLine("[DialogueManager] Dialogue processing loop stopped.");
    }

    private async Task ProcessDialogueAsync(DialogueRequest request, CancellationToken ct)
    {
        // 1. Look up unit info from registry
        var unit = _unitRegistry.GetUnit(request.TargetNetId);
        if (unit == null)
        {
            Console.WriteLine($"[DialogueManager] Unit {request.TargetNetId} not found in registry, skipping.");
            return;
        }

        var npcName = string.IsNullOrEmpty(unit.Name) ? "Soldier" : unit.Name;
        var npcRole = string.IsNullOrEmpty(unit.UnitType) ? "Infantry" : unit.UnitType;
        var npcSide = string.IsNullOrEmpty(unit.Side) ? "UNKNOWN" : unit.Side;

        Console.WriteLine($"[DialogueManager] Processing dialogue with {npcName} ({request.TargetNetId})...");

        // Build nearby units context
        var nearbyUnits = _unitRegistry.GetAllUnits()
            .Where(u => u.NetId != request.TargetNetId)
            .Select(u => new UnitSummary
            {
                NetId = u.NetId,
                Name = u.Name,
                Side = u.Side,
                SameGroup = u.SameGroup,
                UnitType = u.UnitType
            })
            .ToList();

        // 2. Generate NPC response via Claude
        Console.WriteLine($"[DialogueManager] Generating response from {npcName}...");
        var responseText = await _npcDialogue.GenerateResponseAsync(
            npcName, npcRole, npcSide, request.PlayerText, nearbyUnits, request.TargetNetId);

        ct.ThrowIfCancellationRequested();

        // 3. Synthesize speech via TTS
        Console.WriteLine($"[DialogueManager] Synthesizing speech for: \"{responseText[..Math.Min(60, responseText.Length)]}\"");
        var voiceContext = new SpeechContext(UnitName: npcName, Side: npcSide);
        var wavBytes = await _tts.SynthesizeAsync(responseText, voiceContext);

        if (wavBytes.Length == 0)
        {
            Console.WriteLine($"[DialogueManager] TTS returned empty audio, skipping playback.");
            return;
        }

        ct.ThrowIfCancellationRequested();

        // Extract PCM samples from WAV bytes
        var (samples, sampleRate) = ExtractPcmFromWav(wavBytes);
        if (samples.Length == 0)
        {
            Console.WriteLine($"[DialogueManager] Failed to extract PCM from WAV, skipping playback.");
            return;
        }

        // 4. Determine if spatial or radio based on distance (>30m = radio)
        var playerPos = _gameState.PlayerPos;
        var npcPos = unit.Position;
        float dx = npcPos[0] - playerPos[0];
        float dy = npcPos[1] - playerPos[1];
        float dz = npcPos.Length > 2 && playerPos.Length > 2 ? npcPos[2] - playerPos[2] : 0f;
        float distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        float[] processedSamples;
        int outputChannels;

        if (distance > RadioDistanceThreshold)
        {
            // 5a. Radio effect (distant NPC)
            Console.WriteLine($"[DialogueManager] Applying radio effect (distance: {distance:F1}m).");
            processedSamples = _radioEffect.ApplyRadioEffect(samples, sampleRate);
            outputChannels = 1;
        }
        else
        {
            // 5b. Spatial audio (nearby NPC)
            Console.WriteLine($"[DialogueManager] Applying spatial audio (distance: {distance:F1}m).");
            processedSamples = _spatialMixer.ApplySpatialAudio(
                samples, sampleRate, playerPos, _gameState.PlayerDir, npcPos);
            outputChannels = 2;
        }

        // 6. Play through AudioPlayer
        Console.WriteLine($"[DialogueManager] Playing {npcName}'s response ({processedSamples.Length} samples, {outputChannels}ch).");
        _audioPlayer.PlaySamples(processedSamples, sampleRate, outputChannels);

        // Wait for playback to finish
        while (_audioPlayer.IsPlaying && !ct.IsCancellationRequested)
        {
            await Task.Delay(100, ct);
        }

        Console.WriteLine($"[DialogueManager] Finished dialogue with {npcName}.");
    }

    /// <summary>
    /// Extract mono float PCM samples and sample rate from a WAV byte array.
    /// Handles 16-bit PCM and 32-bit float WAV formats.
    /// </summary>
    private static (float[] Samples, int SampleRate) ExtractPcmFromWav(byte[] wav)
    {
        if (wav.Length < 44)
            return ([], 0);

        // Parse WAV header
        // Bytes 0-3: "RIFF"
        // Bytes 22-23: number of channels
        // Bytes 24-27: sample rate
        // Bytes 34-35: bits per sample
        // Bytes 36-39: "data" subchunk id (may vary with extra chunks)

        int channels = BitConverter.ToInt16(wav, 22);
        int sampleRate = BitConverter.ToInt32(wav, 24);
        int bitsPerSample = BitConverter.ToInt16(wav, 34);

        // Find the "data" chunk
        int dataOffset = 12; // Skip RIFF header
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

        if (dataSize == 0)
            return ([], 0);

        float[] monoSamples;

        if (bitsPerSample == 16)
        {
            int totalSamples = dataSize / 2;
            int frameSamples = totalSamples / channels;
            monoSamples = new float[frameSamples];

            for (int i = 0; i < frameSamples; i++)
            {
                // Take the first channel (or mix down if stereo)
                float sum = 0;
                for (int ch = 0; ch < channels; ch++)
                {
                    int byteIndex = dataOffset + (i * channels + ch) * 2;
                    if (byteIndex + 1 < wav.Length)
                    {
                        short sample = BitConverter.ToInt16(wav, byteIndex);
                        sum += sample / 32768f;
                    }
                }
                monoSamples[i] = sum / channels;
            }
        }
        else if (bitsPerSample == 32)
        {
            int totalSamples = dataSize / 4;
            int frameSamples = totalSamples / channels;
            monoSamples = new float[frameSamples];

            for (int i = 0; i < frameSamples; i++)
            {
                float sum = 0;
                for (int ch = 0; ch < channels; ch++)
                {
                    int byteIndex = dataOffset + (i * channels + ch) * 4;
                    if (byteIndex + 3 < wav.Length)
                    {
                        float sample = BitConverter.ToSingle(wav, byteIndex);
                        sum += sample;
                    }
                }
                monoSamples[i] = sum / channels;
            }
        }
        else
        {
            Console.WriteLine($"[DialogueManager] Unsupported WAV bits per sample: {bitsPerSample}");
            return ([], 0);
        }

        return (monoSamples, sampleRate);
    }
}
