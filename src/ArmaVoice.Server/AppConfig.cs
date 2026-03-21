using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArmaVoice.Server;

public class AppConfig
{
    public ServerConfig Server { get; set; } = new();
    public WhisperConfig Whisper { get; set; } = new();
    public PiperConfig Piper { get; set; } = new();
    public GeminiConfig Gemini { get; set; } = new();
    public ClaudeConfig Claude { get; set; } = new();

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"[Config] {path} not found, using defaults.");
            return new AppConfig();
        }

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<AppConfig>(yaml) ?? new AppConfig();
        Console.WriteLine($"[Config] Loaded from {path}");
        return config;
    }
}

public class ServerConfig
{
    public int Port { get; set; } = 9500;
}

public class WhisperConfig
{
    public string ModelPath { get; set; } = "ggml-base.en.bin";
}

public class PiperConfig
{
    public string Url { get; set; } = "http://localhost:5000";
}

public class GeminiConfig
{
    public string ApiKey { get; set; } = "";
}

public class ClaudeConfig
{
    public string ApiKey { get; set; } = "";
}
