using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArmaVoice.Server.Game;

public class CommandDefinition
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public string Sqf { get; set; } = "";
    /// <summary>
    /// Optional SQF expression that must return true for this command to be enabled.
    /// Evaluated once on connect. E.g. "isClass (configFile >> 'CfgPatches' >> 'ace_medical')"
    /// </summary>
    public string EnableIf { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Loads commands from YAML files and SQF functions from .sqf files.
/// Registers functions in-game via compileFinal, builds LLM prompt from command descriptions.
/// </summary>
public class CommandRegistry
{
    private readonly Dictionary<string, CommandDefinition> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _functions = new(); // funcName → sqf body

    public IReadOnlyDictionary<string, CommandDefinition> Commands => _commands;
    public IReadOnlyDictionary<string, string> Functions => _functions;

    /// <summary>
    /// Load all command YAML files from a directory.
    /// </summary>
    public void LoadCommands(string commandsDir)
    {
        if (!Directory.Exists(commandsDir))
        {
            Log.Warn("CommandRegistry", $"Commands directory not found: {commandsDir}");
            return;
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        foreach (var file in Directory.GetFiles(commandsDir, "*.yaml"))
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var cmd = deserializer.Deserialize<CommandDefinition>(yaml);
                if (string.IsNullOrEmpty(cmd.Id))
                {
                    Log.Warn("CommandRegistry", $"Skipping {file}: missing 'id'");
                    continue;
                }
                _commands[cmd.Id] = cmd;
                Log.Info("CommandRegistry", $"Loaded command: {cmd.Id}");
            }
            catch (Exception ex)
            {
                Log.Error("CommandRegistry", $"Error loading {file}: {ex.Message}");
            }
        }

        Log.Info("CommandRegistry", $"{_commands.Count} commands loaded.");
    }

    /// <summary>
    /// Load all SQF function files from a directory.
    /// Filename (without .sqf) becomes the function name.
    /// </summary>
    public void LoadFunctions(string functionsDir)
    {
        if (!Directory.Exists(functionsDir))
        {
            Log.Warn("CommandRegistry", $"Functions directory not found: {functionsDir}");
            return;
        }

        foreach (var file in Directory.GetFiles(functionsDir, "*.sqf"))
        {
            var funcName = Path.GetFileNameWithoutExtension(file);
            var body = File.ReadAllText(file).Trim();
            _functions[funcName] = body;
            Log.Info("CommandRegistry", $"Loaded function: {funcName}");
        }

        Log.Info("CommandRegistry", $"{_functions.Count} functions loaded.");
    }

    /// <summary>
    /// Register all loaded functions in-game via compileFinal RPCs (fire-and-forget).
    /// </summary>
    public void RegisterFunctions(RpcClient rpc)
    {
        Log.Info("CommandRegistry", $"Registering {_functions.Count} functions...");
        foreach (var (name, body) in _functions)
        {
            // Wrap body in single quotes for compileFinal
            var sqf = $"{name} = compileFinal '{body}'";
            rpc.Fire(sqf);
            Log.Info("CommandRegistry", $"  {name}");
        }
        Log.Info("CommandRegistry", "Done.");
    }

    /// <summary>
    /// Check enableIf conditions for all commands via SQF RPCs.
    /// Call after functions are registered and SQF is ready.
    /// </summary>
    public async Task CheckEnableConditionsAsync(RpcClient rpc)
    {
        foreach (var cmd in _commands.Values)
        {
            if (string.IsNullOrWhiteSpace(cmd.EnableIf))
            {
                cmd.Enabled = true;
                continue;
            }

            try
            {
                var result = await rpc.CallAsync(cmd.EnableIf);
                cmd.Enabled = result == "true";
                Log.Info("CommandRegistry", $"Command '{cmd.Id}' enableIf='{cmd.EnableIf}' → {(cmd.Enabled ? "enabled" : "disabled")}");
            }
            catch
            {
                cmd.Enabled = false;
                Log.Warn("CommandRegistry", $"Command '{cmd.Id}' enableIf check failed, disabling.");
            }
        }
    }

    /// <summary>
    /// Build the command list section for the LLM prompt. Only includes enabled commands.
    /// </summary>
    public string BuildPromptSection()
    {
        var lines = new List<string>();
        foreach (var cmd in _commands.Values.Where(c => c.Enabled))
        {
            lines.Add($"- \"{cmd.Id}\": {cmd.Description}");
        }
        return string.Join("\n", lines);
    }
}
