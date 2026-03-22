using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ZdoArmaVoice.Server;

public class AppConfig
{
    public ServerConfig Server { get; set; } = new();
    public AudioConfig Audio { get; set; } = new();
    public SttConfig Stt { get; set; } = new();
    public TtsConfig Tts { get; set; } = new();
    public LlmConfig Llm { get; set; } = new();
    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file not found: {path}");

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<AppConfig>(yaml)
            ?? throw new InvalidOperationException("Failed to parse config file.");

        config.Validate();
        Log.Info("Config", $"Loaded from {path}");
        return config;
    }

    public void Validate()
    {
        var errors = new List<string>();

        // STT validation
        switch (Stt.System.ToLowerInvariant())
        {
            case "whisper":
                RequireField(errors, Stt.Whisper.ModelPath, "stt.whisper.model_path");
                break;
            case "deepgram":
                RequireField(errors, Stt.Deepgram.ApiKey, "stt.deepgram.api_key");
                RequireField(errors, Stt.Deepgram.Model, "stt.deepgram.model");
                RequireField(errors, Stt.Deepgram.Language, "stt.deepgram.language");
                RequireField(errors, Stt.Deepgram.Encoding, "stt.deepgram.encoding");
                if (Stt.Deepgram.SampleRate <= 0)
                    errors.Add("\"stt.deepgram.sample_rate\" is required and must be > 0.");
                break;
            case "windows":
                RequireField(errors, Stt.Windows.Language, "stt.windows.language");
                break;
            case "google":
                RequireField(errors, Stt.Google.ApiKey, "stt.google.api_key");
                RequireField(errors, Stt.Google.Language, "stt.google.language");
                break;
            case "azure":
                RequireField(errors, Stt.Azure.SubscriptionKey, "stt.azure.subscription_key");
                RequireField(errors, Stt.Azure.Region, "stt.azure.region");
                RequireField(errors, Stt.Azure.Language, "stt.azure.language");
                break;
            default:
                errors.Add($"Unknown stt.system: \"{Stt.System}\". Must be \"whisper\", \"deepgram\", \"windows\", \"google\", or \"azure\".");
                break;
        }

        // TTS validation
        switch (Tts.System.ToLowerInvariant())
        {
            case "piper":
                RequireField(errors, Tts.Piper.Url, "tts.piper.url");
                break;
            case "elevenlabs":
                RequireField(errors, Tts.ElevenLabs.ApiKey, "tts.elevenlabs.api_key");
                RequireField(errors, Tts.ElevenLabs.ModelId, "tts.elevenlabs.model_id");
                if (!Tts.ElevenLabs.Voices.ContainsKey("default"))
                    errors.Add("\"tts.elevenlabs.voices.default\" is required.");
                break;
            default:
                errors.Add($"Unknown tts.system: \"{Tts.System}\". Must be \"piper\" or \"elevenlabs\".");
                break;
        }

        // LLM validation
        ValidateLlmInstance(errors, Llm.Intent, "llm.intent");
        if (Llm.Dialog.System.ToLowerInvariant() != "none")
            ValidateLlmInstance(errors, Llm.Dialog, "llm.dialogue");

        if (errors.Count > 0)
        {
            var msg = "Config validation failed:\n  - " + string.Join("\n  - ", errors);
            throw new InvalidOperationException(msg);
        }
    }

    private static void ValidateLlmInstance(List<string> errors, LlmInstanceConfig cfg, string prefix)
    {
        switch (cfg.System.ToLowerInvariant())
        {
            case "gemini":
                RequireField(errors, cfg.Gemini.ApiKey, $"{prefix}.gemini.api_key");
                RequireField(errors, cfg.Gemini.Model, $"{prefix}.gemini.model");
                break;
            case "claude":
                RequireField(errors, cfg.Claude.ApiKey, $"{prefix}.claude.api_key");
                RequireField(errors, cfg.Claude.Model, $"{prefix}.claude.model");
                break;
            default:
                errors.Add($"Unknown {prefix}.system: \"{cfg.System}\". Must be \"gemini\" or \"claude\".");
                break;
        }
    }

    private static void RequireField(List<string> errors, string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"\"{fieldName}\" is required but missing or empty.");
    }
}

public class ServerConfig
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 9500;
}

public class AudioConfig
{
    /// <summary>Radio pan: -1.0 = left ear only, 0.0 = both ears, 1.0 = right ear only.</summary>
    public float RadioPan { get; set; } = 0f;
    /// <summary>Probability (0-1) that a unit will voice-acknowledge a command. 0=never, 1=always.</summary>
    public float AckChance { get; set; } = 0f;
    public RadioConfig Radio { get; set; } = new();
}

public class RadioConfig
{
    /// <summary>Band-pass low cutoff in Hz.</summary>
    public float LowCutHz { get; set; } = 300f;
    /// <summary>Band-pass high cutoff in Hz.</summary>
    public float HighCutHz { get; set; } = 3000f;
    /// <summary>Distortion drive (tanh input multiplier). Higher = more distortion.</summary>
    public float Distortion { get; set; } = 2f;
    /// <summary>White noise level (0-1).</summary>
    public float NoiseLevel { get; set; } = 0.02f;
    /// <summary>Squelch burst duration in seconds.</summary>
    public float SquelchDuration { get; set; } = 0.05f;
    /// <summary>Use biquad filter (better quality) or simple IIR (faster).</summary>
    public bool UseBiquad { get; set; } = true;
}

// ── STT ──────────────────────────────────────────────────

public class SttConfig
{
    public string System { get; set; } = "whisper";
    /// <summary>Microphone device index. -1 = default. Run server to see device list.</summary>
    public int MicDevice { get; set; } = -1;
    /// <summary>"wasapi" (shared mode, default) or "mme" (legacy, compatible with OBS).</summary>
    public string MicMode { get; set; } = "wasapi";
    public WhisperConfig Whisper { get; set; } = new();
    public DeepgramConfig Deepgram { get; set; } = new();
    public WindowsSttConfig Windows { get; set; } = new();
    public GoogleSttConfig Google { get; set; } = new();
    public AzureSttConfig Azure { get; set; } = new();
}

public class GoogleSttConfig
{
    public string ApiKey { get; set; } = "";
    public string Language { get; set; } = "ru-RU";
}

public class AzureSttConfig
{
    public string SubscriptionKey { get; set; } = "";
    public string Region { get; set; } = "";
    public string Language { get; set; } = "ru-RU";
}

public class WindowsSttConfig
{
    public string Language { get; set; } = "en-US";
}

public class WhisperConfig
{
    public string ModelPath { get; set; } = "";
    public string Language { get; set; } = "en";
}

public class DeepgramConfig
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string Language { get; set; } = "";
    public string Encoding { get; set; } = "";
    public int SampleRate { get; set; }
}

// ── TTS ──────────────────────────────────────────────────

public class TtsConfig
{
    public string System { get; set; } = "piper";
    public PiperConfig Piper { get; set; } = new();
    [YamlMember(Alias = "elevenlabs")]
    public ElevenLabsConfig ElevenLabs { get; set; } = new();
}

public class PiperConfig
{
    public string Url { get; set; } = "";
}

public class ElevenLabsConfig
{
    public string ApiKey { get; set; } = "";
    public string ModelId { get; set; } = "";
    public float Stability { get; set; } = 0.75f;
    public float SimilarityBoost { get; set; } = 0.75f;
    public float Style { get; set; } = 0.2f;
    public bool UseSpeakerBoost { get; set; } = false;
    /// <summary>
    /// Voice mapping. Keys: "default", "blufor", "opfor", "indfor", "civilian", or a unit name.
    /// Values: ElevenLabs voice IDs.
    /// </summary>
    public Dictionary<string, string> Voices { get; set; } = new();
}

// ── LLM ──────────────────────────────────────────────────

public class LlmConfig
{
    /// <summary>LLM for intent parsing (voice commands).</summary>
    public LlmInstanceConfig Intent { get; set; } = new();
    /// <summary>LLM for NPC dialogue.</summary>
    public LlmInstanceConfig Dialog { get; set; } = new();
}

public class LlmInstanceConfig
{
    public string System { get; set; } = "";
    public GeminiLlmConfig Gemini { get; set; } = new();
    public ClaudeLlmConfig Claude { get; set; } = new();
}

public class GeminiLlmConfig
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gemini-2.0-flash-lite";
    public int ThinkingBudget { get; set; } = 0;
}

public class ClaudeLlmConfig
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "claude-sonnet-4-20250514";
}
