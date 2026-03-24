using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ZdoArmaVoice.Server.Game;

namespace ZdoArmaVoice.Server.Ai;

public class CommandExecutor
{
    private readonly RpcClient _rpc;
    private readonly DialogManager? _dialogManager;
    private readonly IScreenCapture? _screenCapture;
    private readonly float _ackChance;
    private readonly Random _rng = new();

    public CommandExecutor(RpcClient rpc, DialogManager? dialogManager, float ackChance = 0f, IScreenCapture? screenCapture = null)
    {
        _rpc = rpc;
        _dialogManager = dialogManager;
        _screenCapture = screenCapture;
        _ackChance = Math.Clamp(ackChance, 0f, 1f);
    }

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

                Log.Info("SQF", $"coreCallCommand(\"{cmd.Command}\", args={argsJson}, units={unitsSqf})");

                var sw = Stopwatch.StartNew();
                var resultStr = await _rpc.CallAsync(sqf);
                sw.Stop();

                Log.Info("SQF", $"coreCallCommand -> {resultStr[..Math.Min(200, resultStr.Length)]} ({sw.ElapsedMilliseconds}ms)");

                var cmdRetry = await HandleCommandResultAsync(cmd.Command, resultStr, isRadio);
                if (cmdRetry != null)
                {
                    retryContext ??= new();
                    foreach (var kv in cmdRetry)
                        retryContext[kv.Key] = kv.Value;
                }
            }
            catch (Exception ex)
            {
                Log.Error("SQF", $"coreCallCommand(\"{cmd.Command}\") failed: {ex.Message}");
            }
        }

        return retryContext;
    }

    private async Task<Dictionary<string, bool>?> HandleCommandResultAsync(string commandId, string resultStr, bool isRadio)
    {
        if (string.IsNullOrEmpty(resultStr) || resultStr == "null" || resultStr == "nil")
            return null;

        try
        {
            using var doc = JsonDocument.Parse(resultStr);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return null;

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
                Log.Info("Cmd", $"{commandId}: retry requested [{string.Join(",", ctx.Keys)}]");
                return ctx;
            }

            if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "dialog")
            {
                if (_dialogManager == null) return null;

                var targetNetId = root.TryGetProperty("targetNetId", out var tn) ? tn.GetString() ?? "" : "";
                var systemInstructions = root.TryGetProperty("systemInstructions", out var si) ? si.GetString() ?? "" : "";
                var message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "";
                var wantsScreenshot = root.TryGetProperty("includeScreenshot", out var ss) && ss.GetBoolean();

                LlmImage? image = null;
                if (wantsScreenshot && _screenCapture != null)
                {
                    image = await _screenCapture.CaptureAsync();
                    if (image == null) Log.Warn("Cmd", $"{commandId}: screenshot capture failed");
                }

                if (!string.IsNullOrEmpty(systemInstructions))
                {
                    _dialogManager.Enqueue(targetNetId, systemInstructions, message, isRadio, image);
                    Log.Info("Cmd", $"{commandId}: dialog queued -> {targetNetId}{(image != null ? " +screenshot" : "")}");
                }
                return null;
            }

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
                    Log.Info("Cmd", $"{commandId}: ack queued");
                }
            }
        }
        catch { }

        return null;
    }

    private static string FmtPos(float[] p) => p.Length >= 3
        ? string.Format(CultureInfo.InvariantCulture, "[{0:F1},{1:F1},{2:F1}]", p[0], p[1], p[2])
        : "[0,0,0]";
}
