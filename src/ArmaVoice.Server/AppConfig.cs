using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArmaVoice.Server;

public class AppConfig
{
    public ServerConfig Server { get; set; } = new();
    public SttConfig Stt { get; set; } = new();
    public TtsConfig Tts { get; set; } = new();
    public GeminiConfig Gemini { get; set; } = new();
    public ClaudeConfig Claude { get; set; } = new();

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
        Console.WriteLine($"[Config] Loaded from {path}");
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
                break;
            default:
                errors.Add($"Unknown stt.system: \"{Stt.System}\". Must be \"whisper\" or \"deepgram\".");
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

        // AI validation
        RequireField(errors, Gemini.ApiKey, "gemini.api_key");

        if (errors.Count > 0)
        {
            var msg = "Config validation failed:\n  - " + string.Join("\n  - ", errors);
            throw new InvalidOperationException(msg);
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
    public int Port { get; set; } = 9500;
}

// ── STT ──────────────────────────────────────────────────

public class SttConfig
{
    public string System { get; set; } = "whisper";
    public WhisperConfig Whisper { get; set; } = new();
    public DeepgramConfig Deepgram { get; set; } = new();
}

public class WhisperConfig
{
    public string ModelPath { get; set; } = "";
}

public class DeepgramConfig
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string Language { get; set; } = "";
}

// ── TTS ──────────────────────────────────────────────────

public class TtsConfig
{
    public string System { get; set; } = "piper";
    public PiperConfig Piper { get; set; } = new();
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
    /// <summary>
    /// Voice mapping. Keys: "default", "blufor", "opfor", "indfor", "civilian", or a unit name.
    /// Values: ElevenLabs voice IDs.
    /// </summary>
    public Dictionary<string, string> Voices { get; set; } = new();
}

// ── AI ───────────────────────────────────────────────────

public class GeminiConfig
{
    public string ApiKey { get; set; } = "";
}

public class ClaudeConfig
{
    public string ApiKey { get; set; } = "";
}
