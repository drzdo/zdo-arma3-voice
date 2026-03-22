using System.Globalization;
using System.Text.Json;
using ZdoArmaVoice.Server.Game;

namespace ZdoArmaVoice.Server.Ai;

/// <summary>
/// Executes parsed intent by calling SQF coreCallCommand for each command.
/// Units are resolved once (top-level) and passed to every command.
/// C# is agnostic to command args — just proxies them to SQF.
/// Returns retry context if a command requests re-parsing with extra info.
/// </summary>
public class CommandExecutor
{
    private readonly RpcClient _rpc;
    private readonly DialogManager? _dialogManager;
    private readonly float _ackChance;
    private readonly Random _rng = new();

    public CommandExecutor(RpcClient rpc, DialogManager? dialogManager, float ackChance = 0f)
    {
        _rpc = rpc;
        _dialogManager = dialogManager;
        _ackChance = Math.Clamp(ackChance, 0f, 1f);
    }

    /// <summary>
    /// Execute all commands. Returns retry context if any command requests it, null otherwise.
    /// </summary>
    public async Task<Dictionary<string, bool>?> ExecuteAsync(ParsedIntent intent, float[] lookAtPosition, bool isRadio = true)
    {
        var unitsSqf = "[" + string.Join(",", intent.Units.Select(u =>
            int.TryParse(u, out _) ? u : $"\"{u}\"")) + "]";
        var posStr = FmtPos(lookAtPosition);
        Dictionary<string, bool>? retryContext = null;

        foreach (var cmd in intent.Commands)
        {
            try
            {
                var argsJson = cmd.Args.ValueKind != JsonValueKind.Undefined
                    ? cmd.Args.GetRawText()
                    : "{}";

                var sqfArgs = argsJson.Replace("\"", "\"\"");

                var sqf = $"[\"{cmd.Command}\", fromJSON \"{sqfArgs}\", {posStr}, {unitsSqf}] call zdoArmaVoice_fnc_coreCallCommand";
                Log.Info("SQF", $"Call: {sqf}");

                var resultStr = await _rpc.CallAsync(sqf);
                Log.Info("SQF", $"Result: {resultStr}");

                var cmdRetry = HandleCommandResult(cmd.Command, resultStr, isRadio);
                if (cmdRetry != null)
                {
                    retryContext ??= new();
                    foreach (var kv in cmdRetry)
                        retryContext[kv.Key] = kv.Value;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Cmd", $"Error executing {cmd.Command}: {ex.Message}");
            }
        }

        return retryContext;
    }

    /// <summary>
    /// Handle command result. Returns retry context if command requests it, null otherwise.
    /// </summary>
    private Dictionary<string, bool>? HandleCommandResult(string commandId, string resultStr, bool isRadio)
    {
        if (string.IsNullOrEmpty(resultStr) || resultStr == "null" || resultStr == "nil")
            return null;

        try
        {
            using var doc = JsonDocument.Parse(resultStr);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return null;

            // Handle retry request
            if (root.TryGetProperty("retryWithContext", out var retryProp) && retryProp.ValueKind == JsonValueKind.Object)
            {
                var ctx = new Dictionary<string, bool>();
                foreach (var prop in retryProp.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.True)
                        ctx[prop.Name] = true;
                    else if (prop.Value.ValueKind == JsonValueKind.False)
                        ctx[prop.Name] = false;
                }
                Log.Info("Cmd", $"{commandId}: requested retry with context [{string.Join(",", ctx.Keys)}]");
                return ctx;
            }

            // Handle dialog response
            if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "dialog")
            {
                if (_dialogManager == null) { Log.Info("Cmd", $"{commandId}: dialog disabled"); return null; }

                var targetNetId = root.TryGetProperty("targetNetId", out var tn) ? tn.GetString() ?? "" : "";
                var systemInstructions = root.TryGetProperty("systemInstructions", out var si) ? si.GetString() ?? "" : "";
                var message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "";

                if (!string.IsNullOrEmpty(systemInstructions))
                {
                    _dialogManager.Enqueue(targetNetId, systemInstructions, message, isRadio);
                    Log.Info("Cmd", $"{commandId}: dialog queued -> {targetNetId} ({(isRadio ? "radio" : "direct")})");
                }
                return null;
            }

            // Handle ack response
            if (root.TryGetProperty("ackSystemInstructions", out var ackSi) &&
                root.TryGetProperty("ackMessage", out var ackMsg))
            {
                if (_dialogManager == null || _ackChance <= 0) return null;
                if (_rng.NextSingle() >= _ackChance) return null;

                var ackSystemInstructions = ackSi.GetString() ?? "";
                var ackMessage = ackMsg.GetString() ?? "";
                var ackTarget = root.TryGetProperty("targetNetId", out var at) ? at.GetString() ?? "" : "";

                if (!string.IsNullOrEmpty(ackSystemInstructions))
                {
                    _dialogManager.Enqueue(ackTarget, ackSystemInstructions, ackMessage, isRadio);
                    Log.Info("Cmd", $"{commandId}: ack queued ({(isRadio ? "radio" : "direct")})");
                }
            }
        }
        catch
        {
            // Result was not JSON — that's fine, many commands return simple values
        }

        return null;
    }

    private static string FmtPos(float[] p) => p.Length >= 3
        ? string.Format(CultureInfo.InvariantCulture, "[{0:F1},{1:F1},{2:F1}]", p[0], p[1], p[2])
        : "[0,0,0]";
}
