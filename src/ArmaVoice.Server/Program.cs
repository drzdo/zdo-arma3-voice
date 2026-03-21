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
            Log.Error("Server", "Usage: ArmaVoice.Server --config <path>");
            return;
        }

        var config = AppConfig.Load(configPath);

        Log.Info("Server", $"Listen: {config.Server.Host}:{config.Server.Port}");
        Log.Info("Server", $"STT: {config.Stt.System}");
        Log.Info("Server", $"TTS: {config.Tts.System}");
        Log.Info("Server", $"LLM intent: {config.Llm.Intent.System}");
        Log.Info("Server", $"LLM dialogue: {config.Llm.Dialogue.System}");

        // Load commands and functions relative to exe location
        var commandRegistry = new CommandRegistry();
        var exeDir = AppContext.BaseDirectory;
        commandRegistry.LoadCommands(Path.Combine(exeDir, "commands"));
        commandRegistry.LoadFunctions(Path.Combine(exeDir, "functions"));

        // Core infrastructure
        var bridge = new TcpBridge(config.Server.Host, config.Server.Port);
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
                    config.Stt.Deepgram.Language,
                    config.Stt.Deepgram.Encoding,
                    config.Stt.Deepgram.SampleRate),
                _ => new WhisperRecognizer(config.Stt.Whisper.ModelPath),
            };
            Log.Info("Server", $"STT ({config.Stt.System}) ready.");
        }
        catch (Exception ex)
        {
            Log.Warn("Server", $"STT unavailable: {ex.Message}");
        }

        // TTS
        ISpeechSynthesizer tts = config.Tts.System.ToLowerInvariant() switch
        {
            "elevenlabs" => new ElevenLabsSynthesizer(
                config.Tts.ElevenLabs.ApiKey,
                config.Tts.ElevenLabs.ModelId,
                config.Tts.ElevenLabs.Stability,
                config.Tts.ElevenLabs.SimilarityBoost,
                config.Tts.ElevenLabs.Style,
                config.Tts.ElevenLabs.UseSpeakerBoost,
                config.Tts.ElevenLabs.Voices),
            _ => new PiperSynthesizer(config.Tts.Piper.Url),
        };
        Log.Info("Server", $"TTS ({config.Tts.System}) ready.");

        // Audio
        var audioPlayer = new AudioPlayer();
        var radioEffect = new RadioEffect();

        // LLM clients
        static ILlmClient CreateLlmClient(LlmInstanceConfig cfg) => cfg.System.ToLowerInvariant() switch
        {
            "gemini" => new GeminiLlmClient(cfg.Gemini.ApiKey, cfg.Gemini.Model, cfg.Gemini.ThinkingBudget),
            "claude" => new ClaudeLlmClient(cfg.Claude.ApiKey, cfg.Claude.Model),
            _ => throw new InvalidOperationException($"Unknown LLM system: {cfg.System}")
        };

        var intentLlm = CreateLlmClient(config.Llm.Intent);
        var dialogueLlm = CreateLlmClient(config.Llm.Dialogue);

        var intentParser = new IntentParser(intentLlm, commandRegistry.BuildPromptSection(), config.Prompt);
        Log.Info("Server", "IntentParser ready.");

        var npcDialogue = new NpcDialogue(dialogueLlm, config.Prompt);
        Log.Info("Server", "NpcDialogue ready.");

        // Dialogue manager
        var dialogueManager = new DialogueManager(
            npcDialogue, tts, audioPlayer, radioEffect, gameState, unitRegistry);

        // Command executor
        var commandExecutor = new CommandExecutor(rpcClient, unitRegistry, gameState, commandRegistry, dialogueManager);

        // Wire up TcpBridge events
        bridge.OnStateReceived = stateJson =>
        {
            gameState.UpdateFromState(stateJson);
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
            Log.Info("PTT", $"{direction} at [{string.Join(", ", lookPos.Select(v => v.ToString("F1")))}]");

            if (direction == "down")
            {
                lastLookTarget = lookPos;
                speechRecognizer?.StartRecording();
                Log.Info("Mic", "Recording started...");
            }
            else if (direction == "up")
            {
                lastLookTarget = lookPos;

                if (speechRecognizer == null)
                {
                    Log.Warn("Mic", "Speech recognizer not available.");
                    return;
                }

                speechRecognizer.StopRecording();
                Log.Info("Mic", "Recording stopped, transcribing...");
                var capturedLookTarget = lastLookTarget;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var transcript = await speechRecognizer.TranscribeAsync();
                        if (string.IsNullOrWhiteSpace(transcript))
                        {
                            Log.Info("Mic", "No speech detected.");
                            return;
                        }

                        Log.Info("Mic", $"Transcript: \"{transcript}\"");

                        // Show transcript in Arma chat + RPT
                        rpcClient.Fire($"systemChat 'Voice: {transcript.Replace("'", "")}'");
                        rpcClient.Fire($"diag_log 'ArmaVoice transcript: {transcript.Replace("'", "")}'");

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
                            Log.Warn("Mic", "Could not parse intent.");
                            return;
                        }

                        await commandExecutor.ExecuteAsync(intent, capturedLookTarget);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Mic", $"Error in speech pipeline: {ex.Message}");
                    }
                });
            }
        };

        bridge.OnClientConnected = () =>
        {
            Log.Info("Server", "Client connected — registering functions...");
            commandRegistry.RegisterFunctions(rpcClient);

            _ = Task.Run(async () =>
            {
                // Wait until functions are compiled in SQF
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(1000);
                    try
                    {
                        var test = await rpcClient.CallAsync("!isNil 'zdoArmaMic_fnc_getSquad'");
                        if (test == "true") break;
                        Log.Info("Server", "Waiting for SQF functions to compile...");
                    }
                    catch { }
                }
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
        var dialogueTask = dialogueManager.RunAsync(cts.Token);

        // Periodic squad re-sync
        _ = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(5_000, cts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                if (bridge.IsClientConnected)
                    await unitRegistry.SyncSquadAsync();
            }
        });

        // Start the TCP bridge
        try
        {
            Log.Info("Server", "Starting...");
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

        try { await dialogueTask; }
        catch (OperationCanceledException) { }

        Log.Info("Server", "Shut down.");
    }
}
