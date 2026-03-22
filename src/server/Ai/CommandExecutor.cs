using System.Globalization;
using System.Text.Json;
using ZdoArmaVoice.Server.Game;

namespace ZdoArmaVoice.Server.Ai;

/// <summary>
/// Executes parsed commands by calling SQF coreCallCommand.
/// Handles dialog and ack responses from commands.
/// C# is agnostic to command args — just proxies them to SQF.
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

    public async Task ExecuteAsync(List<ParsedCommand> commands, float[] lookAtPosition)
    {
        foreach (var cmd in commands)
        {
            try
            {
                Log.Info("Cmd", $"Executing: {cmd.Command}");

                // Build SQF call: [commandId, args (from JSON), lookAtPosition] call coreCallCommand
                var argsJson = cmd.Args.ValueKind != JsonValueKind.Undefined
                    ? cmd.Args.GetRawText()
                    : "{}";

                // Escape double quotes for SQF string embedding
                var sqfArgs = argsJson.Replace("\"", "\"\"");
                var posStr = FmtPos(lookAtPosition);

                var sqf = $"[\"{cmd.Command}\", fromJSON \"{sqfArgs}\", {posStr}] call zdoZdoArmaVoice_fnc_coreCallCommand";

                var resultStr = await _rpc.CallAsync(sqf);

                // Parse result if any
                HandleCommandResult(cmd.Command, resultStr);
            }
            catch (Exception ex)
            {
                Log.Error("Cmd", $"Error executing {cmd.Command}: {ex.Message}");
            }
        }
    }

    private void HandleCommandResult(string commandId, string resultStr)
    {
        if (string.IsNullOrEmpty(resultStr) || resultStr == "null" || resultStr == "nil")
            return;

        try
        {
            using var doc = JsonDocument.Parse(resultStr);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return;

            // Handle dialog response
            if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "dialog")
            {
                if (_dialogManager == null) { Log.Info("Cmd", $"{commandId}: dialog disabled"); return; }

                var targetNetId = root.TryGetProperty("targetNetId", out var tn) ? tn.GetString() ?? "" : "";
                var systemInstructions = root.TryGetProperty("systemInstructions", out var si) ? si.GetString() ?? "" : "";
                var message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "";

                if (!string.IsNullOrEmpty(systemInstructions))
                {
                    _dialogManager.Enqueue(targetNetId, systemInstructions, message);
                    Log.Info("Cmd", $"{commandId}: dialog queued → {targetNetId}");
                }
                return;
            }

            // Handle ack response
            if (root.TryGetProperty("ackSystemInstructions", out var ackSi) &&
                root.TryGetProperty("ackMessage", out var ackMsg))
            {
                if (_dialogManager == null || _ackChance <= 0) return;
                if (_rng.NextSingle() >= _ackChance) return;

                var ackSystemInstructions = ackSi.GetString() ?? "";
                var ackMessage = ackMsg.GetString() ?? "";
                var ackTarget = root.TryGetProperty("targetNetId", out var at) ? at.GetString() ?? "" : "";

                if (!string.IsNullOrEmpty(ackSystemInstructions))
                {
                    _dialogManager.Enqueue(ackTarget, ackSystemInstructions, ackMessage);
                    Log.Info("Cmd", $"{commandId}: ack queued");
                }
            }
        }
        catch
        {
            // Result was not JSON — that's fine, many commands return simple values
        }
    }

    private static string FmtPos(float[] p) => p.Length >= 3
        ? string.Format(CultureInfo.InvariantCulture, "[{0:F1},{1:F1},{2:F1}]", p[0], p[1], p[2])
        : "[0,0,0]";
}
