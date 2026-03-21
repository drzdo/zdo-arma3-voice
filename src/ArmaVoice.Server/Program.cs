using ArmaVoice.Server.Ai;
using ArmaVoice.Server.Audio;
using ArmaVoice.Server.Game;
using ArmaVoice.Server.Net;
using ArmaVoice.Server.Speech;

namespace ArmaVoice.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== ArmaVoice Server ===");

        // --config is required
        string? configPath = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--config" or "-c")
            {
                configPath = args[i + 1];
                break;
            }
        }

        if (configPath == null)
        {
            Console.Error.WriteLine("Usage: ArmaVoice.Server --config <path>");
            return;
        }

        var config = AppConfig.Load(configPath);

        Console.WriteLine($"[Server] Port: {config.Server.Port}");
        Console.WriteLine($"[Server] STT: {config.Stt.System}");
        Console.WriteLine($"[Server] TTS: {config.Tts.System}");
        Console.WriteLine($"[Server] Gemini API key: {(string.IsNullOrEmpty(config.Gemini.ApiKey) ? "NOT SET" : "***")}");
        Console.WriteLine($"[Server] Claude API key: {(string.IsNullOrEmpty(config.Claude.ApiKey) ? "NOT SET" : "***")}");

        // Core infrastructure
        var bridge = new TcpBridge(config.Server.Port);
        var rpcClient = new RpcClient(bridge);
        var gameState = new GameState();
        var unitRegistry = new UnitRegistry(rpcClient);

        // STT
        ISpeechRecognizer? speechRecognizer = null;
        try
        {
            speechRecognizer = config.Stt.System.ToLowerInvariant() switch
            {
                "deepgram" => new DeepgramRecognizer(
                    config.Stt.Deepgram.ApiKey,
                    config.Stt.Deepgram.Model,
                    config.Stt.Deepgram.Language),
                _ => new WhisperRecognizer(config.Stt.Whisper.ModelPath),
            };
            Console.WriteLine($"[Server] STT ({config.Stt.System}) ready.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server] STT unavailable: {ex.Message}");
        }

        // TTS
        ISpeechSynthesizer tts = config.Tts.System.ToLowerInvariant() switch
        {
            "elevenlabs" => new ElevenLabsSynthesizer(
                config.Tts.ElevenLabs.ApiKey,
                config.Tts.ElevenLabs.ModelId,
                config.Tts.ElevenLabs.Voices),
            _ => new PiperSynthesizer(config.Tts.Piper.Url),
        };
        Console.WriteLine($"[Server] TTS ({config.Tts.System}) ready.");

        // Audio
        var audioPlayer = new AudioPlayer();
        var spatialMixer = new SpatialMixer();
        var radioEffect = new RadioEffect();

        // AI
        IntentParser? intentParser = null;
        if (!string.IsNullOrEmpty(config.Gemini.ApiKey))
        {
            intentParser = new IntentParser(config.Gemini.ApiKey);
            Console.WriteLine("[Server] IntentParser ready.");
        }
        else
        {
            Console.WriteLine("[Server] IntentParser unavailable: no Gemini API key.");
        }

        NpcDialogue? npcDialogue = null;
        if (!string.IsNullOrEmpty(config.Claude.ApiKey))
        {
            npcDialogue = new NpcDialogue(config.Claude.ApiKey);
            Console.WriteLine("[Server] NpcDialogue ready.");
        }
        else
        {
            Console.WriteLine("[Server] NpcDialogue unavailable: no Claude API key.");
        }

        // Dialogue manager (needs NPC dialogue + TTS)
        DialogueManager? dialogueManager = null;
        if (npcDialogue != null)
        {
            dialogueManager = new DialogueManager(
                npcDialogue, tts, audioPlayer, spatialMixer, radioEffect, gameState, unitRegistry);
        }

        // Command executor
        CommandExecutor? commandExecutor = null;
        if (intentParser != null)
        {
            commandExecutor = new CommandExecutor(rpcClient, unitRegistry, gameState, dialogueManager);
        }

        // Wire up TcpBridge events
        bridge.OnStateReceived = payload =>
        {
            gameState.UpdateFromState(payload);
            unitRegistry.UpdateFromState(gameState.NearbyUnits);
            unitRegistry.EvictStale(maxAge: 300);
        };

        bridge.OnRpcResponse = (id, result) =>
        {
            rpcClient.HandleResponse(id, result);
        };

        float[] lastLookTarget = [0, 0, 0];

        bridge.OnPttEvent = (direction, lookPos) =>
        {
            Console.WriteLine($"[PTT] {direction} at [{string.Join(", ", lookPos.Select(v => v.ToString("F1")))}]");

            if (direction == "down")
            {
                lastLookTarget = lookPos;
                speechRecognizer?.StartRecording();
                Console.WriteLine("[PTT] Recording...");
            }
            else if (direction == "up")
            {
                lastLookTarget = lookPos;

                if (speechRecognizer == null || intentParser == null)
                {
                    Console.WriteLine("[PTT] Speech or intent parser not available.");
                    return;
                }

                speechRecognizer.StopRecording();
                var capturedLookTarget = lastLookTarget;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var transcript = await speechRecognizer.TranscribeAsync();
                        if (string.IsNullOrWhiteSpace(transcript))
                        {
                            Console.WriteLine("[PTT] No speech detected.");
                            return;
                        }

                        Console.WriteLine($"[PTT] Transcript: \"{transcript}\"");

                        var units = unitRegistry.GetAllUnits()
                            .Select(u => new UnitSummary
                            {
                                NetId = u.NetId,
                                Name = u.Name,
                                Side = u.Side,
                                SameGroup = u.SameGroup,
                                UnitType = u.UnitType
                            })
                            .ToList();

                        var intent = await intentParser.ParseAsync(transcript, units);
                        if (intent == null)
                        {
                            Console.WriteLine("[PTT] Could not parse intent.");
                            return;
                        }

                        if (commandExecutor != null)
                        {
                            await commandExecutor.ExecuteAsync(intent, capturedLookTarget);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PTT] Error in speech pipeline: {ex.Message}");
                    }
                });
            }
        };

        bridge.OnClientConnected = () =>
        {
            Console.WriteLine("[Server] Client connected — registering SQF functions...");
            SqfFunctions.RegisterAll(rpcClient);

            // Sync squad after a short delay (let functions register first)
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                await unitRegistry.SyncSquadAsync();
            });
        };

        // Graceful shutdown
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[Server] Shutdown requested...");
            cts.Cancel();
        };

        // Start dialogue manager in background
        Task? dialogueTask = dialogueManager?.RunAsync(cts.Token);

        // Periodic squad re-sync (every 30s)
        _ = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(5_000, cts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                if (bridge.IsClientConnected)
                    await unitRegistry.SyncSquadAsync();
            }
        });

        // Start the TCP bridge (blocks until cancellation)
        try
        {
            Console.WriteLine("[Server] Starting...");
            await bridge.StartAsync(cts.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            bridge.Dispose();
            speechRecognizer?.Dispose();
            (tts as IDisposable)?.Dispose();
            audioPlayer.Dispose();
        }

        if (dialogueTask != null)
        {
            try { await dialogueTask; }
            catch (OperationCanceledException) { }
        }

        Console.WriteLine("[Server] Shut down.");
    }
}
