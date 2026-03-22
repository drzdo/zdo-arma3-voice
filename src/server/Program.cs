using ZdoArmaVoice.Server.Ai;
using ZdoArmaVoice.Server.Audio;
using ZdoArmaVoice.Server.Game;
using ZdoArmaVoice.Server.Net;
using ZdoArmaVoice.Server.Speech;

namespace ZdoArmaVoice.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine($"=== ZdoArmaVoice Server {BuildInfo.Version} ({BuildInfo.CommitHash}) ===");

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
            Log.Error("Server", "Usage: ZdoArmaVoice.Server --config <path>");
            return;
        }

        var config = AppConfig.Load(configPath);

        Log.Info("Server", $"Listen: {config.Server.Host}:{config.Server.Port}");
        Log.Info("Server", $"STT: {config.Stt.System}");
        Log.Info("Server", $"TTS: {config.Tts.System}");
        Log.Info("Server", $"LLM intent: {config.Llm.Intent.System}");
        Log.Info("Server", $"LLM dialog: {config.Llm.Dialog.System}");

        // Load data files (SQF) relative to exe location
        var dataLoader = new DataLoader();
        var exeDir = AppContext.BaseDirectory;
        dataLoader.LoadData(Path.Combine(exeDir, "server-data"));

        // Core infrastructure
        var bridge = new TcpBridge(config.Server.Host, config.Server.Port);
        var rpcClient = new RpcClient(bridge);
        var gameState = new GameState();
        var unitRegistry = new UnitRegistry(rpcClient);

        // STT
        var micMode = config.Stt.MicMode.ToLowerInvariant();
        var micDevice = config.Stt.MicDevice;
        MicHelper.ListDevices(micMode);

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
                    config.Stt.Deepgram.SampleRate,
                    micDevice, micMode),
                #pragma warning disable CA1416
                "windows" => new WindowsRecognizer(config.Stt.Windows.Language),
                #pragma warning restore CA1416
                "google" => new GoogleRecognizer(config.Stt.Google.ApiKey, config.Stt.Google.Language, micDevice, micMode),
                "azure" => new AzureRecognizer(config.Stt.Azure.SubscriptionKey, config.Stt.Azure.Region, config.Stt.Azure.Language, micDevice, micMode),
                _ => new WhisperRecognizer(config.Stt.Whisper.ModelPath, config.Stt.Whisper.Language, micDevice, micMode),
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
        var rc = config.Audio.Radio;
        var radioEffect = new RadioEffect(rc.LowCutHz, rc.HighCutHz, rc.Distortion, rc.NoiseLevel, rc.SquelchDuration, rc.UseBiquad);

        // LLM clients
        static ILlmClient CreateLlmClient(LlmInstanceConfig cfg) => cfg.System.ToLowerInvariant() switch
        {
            "gemini" => new GeminiLlmClient(cfg.Gemini.ApiKey, cfg.Gemini.Model, cfg.Gemini.ThinkingBudget),
            "claude" => new ClaudeLlmClient(cfg.Claude.ApiKey, cfg.Claude.Model),
            _ => throw new InvalidOperationException($"Unknown LLM system: {cfg.System}")
        };

        var intentLlm = CreateLlmClient(config.Llm.Intent);
        var intentParser = new IntentParser(intentLlm, rpcClient);
        Log.Info("Server", "IntentParser ready.");

        // Dialog (optional)
        DialogManager? dialogManager = null;
        if (config.Llm.Dialog.System.ToLowerInvariant() != "none")
        {
            var dialogLlm = CreateLlmClient(config.Llm.Dialog);
            var npcDialog = new NpcDialog(dialogLlm);
            dialogManager = new DialogManager(
                npcDialog, tts, audioPlayer, radioEffect, gameState, unitRegistry, config.Audio.RadioPan);
            Log.Info("Server", "Dialog ready.");
        }
        else
        {
            Log.Info("Server", "Dialog disabled.");
        }

        // Command executor
        var commandExecutor = new CommandExecutor(rpcClient, dialogManager, config.Audio.AckChance);

        // Wire up TcpBridge events
        bridge.OnHeadReceived = headJson =>
        {
            gameState.UpdateHead(headJson);
        };

        bridge.OnStateReceived = stateJson =>
        {
            gameState.UpdateState(stateJson);
            unitRegistry.UpdateFromState(gameState.NearbyUnits);
            unitRegistry.EvictStale(maxAge: 300);
        };

        bridge.OnRpcResponse = (id, result) =>
        {
            rpcClient.HandleResponse(id, result);
        };

        bridge.OnPttEvent = (direction) =>
        {
            var isDown = direction is "down" or "down_direct";
            var isUp = direction is "up" or "up_direct";
            var isRadio = direction is "down" or "up";

            if (isDown)
            {
                speechRecognizer?.StartRecording();
                Log.Info("Pipeline", $"Recording started ({(isRadio ? "radio" : "direct")})");
            }
            else if (isUp)
            {
                if (speechRecognizer == null)
                {
                    Log.Warn("Pipeline", "Speech recognizer not available.");
                    return;
                }

                speechRecognizer.StopRecording();
                Log.Info("Pipeline", "Recording stopped, transcribing...");
                var capturedIsRadio = isRadio;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var transcript = await speechRecognizer.TranscribeAsync();
                        sw.Stop();

                        if (string.IsNullOrWhiteSpace(transcript))
                        {
                            Log.Info("STT", $"No speech detected ({sw.ElapsedMilliseconds}ms)");
                            return;
                        }

                        Log.Info("STT", $"Recognized ({sw.ElapsedMilliseconds}ms): \"{transcript}\"");

                        rpcClient.Fire($"[\"{transcript.Replace("\"", "\"\"")}\"] call zdoArmaVoice_fnc_coreOnPlayerSay");

                        Dictionary<string, bool>? extraContext = null;
                        const int maxIterations = 2;

                        for (int iteration = 0; iteration < maxIterations; iteration++)
                        {
                            var result = await intentParser.ParseAsync(transcript, capturedIsRadio, extraContext);
                            if (result == null)
                            {
                                Log.Warn("Pipeline", "Could not parse intent.");
                                break;
                            }

                            var (intent, lookAtPosition, resultIsRadio) = result.Value;
                            var retryContext = await commandExecutor.ExecuteAsync(intent, lookAtPosition, resultIsRadio);

                            if (retryContext == null || retryContext.Count == 0)
                                break;

                            if (iteration + 1 >= maxIterations)
                            {
                                Log.Warn("Pipeline", "Max retry iterations reached.");
                                break;
                            }

                            Log.Info("Pipeline", $"Retry with context: [{string.Join(",", retryContext.Keys)}]");
                            extraContext = retryContext;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Pipeline", $"Error: {ex.Message}");
                    }
                });
            }
        };

        bridge.OnClientConnected = () =>
        {
            Log.Info("Server", "Client connected — sending data files...");
            dataLoader.RegisterAll(rpcClient);

            _ = Task.Run(async () =>
            {
                // Wait until core functions are compiled in SQF
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(1000);
                    try
                    {
                        var test = await rpcClient.CallAsync("!isNil 'zdoArmaVoice_fnc_coreCallCommand'");
                        if (test == "true") break;
                        Log.Info("Server", "Waiting for SQF data files to load...");
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

        // Start dialog manager in background
        var dialogTask = dialogManager?.RunAsync(cts.Token);

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

        if (dialogTask != null)
        {
            try { await dialogTask; }
            catch (OperationCanceledException) { }
        }

        Log.Info("Server", "Shut down.");
    }
}
