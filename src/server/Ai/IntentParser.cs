using System.Text.Json;

namespace ZdoArmaVoice.Server.Ai;

public class ParsedCommand
{
    public string Command { get; set; } = "";
    public JsonElement Args { get; set; }
}

/// <summary>
/// Parsed intent: who + what.
/// </summary>
public class ParsedIntent
{
    public List<string> Units { get; set; } = [];
    public List<ParsedCommand> Commands { get; set; } = [];
}

public class IntentPromptResult
{
    public string SystemInstructions { get; set; } = "";
    public string Message { get; set; } = "";
    public float[] LookAtPosition { get; set; } = [0, 0, 0];
    public bool IsRadio { get; set; } = true;
}

/// <summary>
/// Calls SQF for the intent prompt, sends to LLM, parses response.
/// C# is agnostic to command args — just validates JSON and passes through.
/// </summary>
public class IntentParser
{
    private readonly ILlmClient _llm;
    private readonly Game.RpcClient _rpc;

    public IntentParser(ILlmClient llm, Game.RpcClient rpc)
    {
        _llm = llm;
        _rpc = rpc;
    }

    public async Task<(ParsedIntent Intent, float[] LookAtPosition, bool IsRadio)?> ParseAsync(
        string speechText, bool isRadio = true, Dictionary<string, bool>? extraContext = null)
    {
        try
        {
            var promptResult = await GetIntentPromptAsync(speechText, isRadio, extraContext);
            if (promptResult == null) return null;

            var messages = new List<LlmMessage> { new("user", promptResult.Message) };
            var textResponse = await _llm.CompleteAsync(
                promptResult.SystemInstructions, messages, temperature: 0.1f, maxTokens: 500);

            if (string.IsNullOrEmpty(textResponse))
            {
                Log.Warn("IntentParser", "LLM returned empty response.");
                return null;
            }

            Log.Info("IntentParser", $"LLM response: {textResponse}");

            var json = StripMarkdownFences(textResponse);

            var intent = TryParseIntent(json);
            if (intent == null)
            {
                Log.Warn("IntentParser", "Bad JSON from LLM, retrying with fix prompt...");
                intent = await RetryFixAsync(json);
            }

            if (intent != null)
            {
                Log.Info("IntentParser", $"Parsed: units=[{string.Join(",", intent.Units)}]");
                foreach (var cmd in intent.Commands)
                    Log.Info("IntentParser", $"  command={cmd.Command} args={cmd.Args}");
            }

            return intent != null ? (intent, promptResult.LookAtPosition, promptResult.IsRadio) : null;
        }
        catch (Exception ex)
        {
            Log.Error("IntentParser", $"Error: {ex.Message}");
            return null;
        }
    }

    private async Task<IntentPromptResult?> GetIntentPromptAsync(
        string speechText, bool isRadio, Dictionary<string, bool>? extraContext = null)
    {
        try
        {
            var escaped = speechText.Replace("\"", "\\\"");
            var radioStr = isRadio ? "true" : "false";

            // Build context hashmap for SQF: createHashMapFromArray [["key",true],...]
            var contextSqf = "createHashMap";
            if (extraContext != null && extraContext.Count > 0)
            {
                var pairs = string.Join(",", extraContext.Select(kv =>
                    $"[\"{kv.Key}\",{(kv.Value ? "true" : "false")}]"));
                contextSqf = $"createHashMapFromArray [{pairs}]";
            }

            var sqfCall = $"[\"{escaped}\", {radioStr}, {contextSqf}] call zdoArmaVoice_fnc_coreIntentPrompt";
            Log.Info("SQF", $"Call: {sqfCall}");

            var result = await _rpc.CallAsync(sqfCall);
            Log.Info("SQF", $"Result: {result[..Math.Min(200, result.Length)]}{(result.Length > 200 ? "..." : "")}");

            using var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;

            var system = root.GetProperty("systemInstructions").GetString() ?? "";
            var message = root.GetProperty("message").GetString() ?? "";

            var lookAt = new float[] { 0, 0, 0 };
            if (root.TryGetProperty("lookAtPosition", out var posEl) && posEl.ValueKind == JsonValueKind.Array)
            {
                var arr = posEl.EnumerateArray().ToArray();
                if (arr.Length >= 2)
                {
                    lookAt[0] = arr[0].GetSingle();
                    lookAt[1] = arr[1].GetSingle();
                    if (arr.Length >= 3) lookAt[2] = arr[2].GetSingle();
                }
            }

            var resultIsRadio = !root.TryGetProperty("isRadio", out var radioProp) || radioProp.GetBoolean();

            return new IntentPromptResult
            {
                SystemInstructions = system,
                Message = message,
                LookAtPosition = lookAt,
                IsRadio = resultIsRadio
            };
        }
        catch (Exception ex)
        {
            Log.Error("IntentParser", $"Failed to get intent prompt from SQF: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parse LLM response: {units: [...], commands: [{command, args}]}
    /// </summary>
    private static ParsedIntent? TryParseIntent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object) return null;

            var intent = new ParsedIntent();

            // Parse units — strings ("netId", "all", "red") or numbers (squad index)
            if (root.TryGetProperty("units", out var unitsProp) && unitsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var u in unitsProp.EnumerateArray())
                {
                    if (u.ValueKind == JsonValueKind.String)
                        intent.Units.Add(u.GetString() ?? "");
                    else if (u.ValueKind == JsonValueKind.Number)
                        intent.Units.Add(u.GetInt32().ToString());
                }
            }

            // Parse commands
            if (root.TryGetProperty("commands", out var cmdsProp) && cmdsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in cmdsProp.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;
                    if (!el.TryGetProperty("command", out var cmdProp)) continue;

                    var cmd = new ParsedCommand
                    {
                        Command = cmdProp.GetString() ?? "",
                        Args = el.TryGetProperty("args", out var argsProp)
                            ? argsProp.Clone()
                            : default
                    };
                    if (!string.IsNullOrEmpty(cmd.Command))
                        intent.Commands.Add(cmd);
                }
            }

            return intent.Commands.Count > 0 ? intent : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<ParsedIntent?> RetryFixAsync(string brokenJson)
    {
        var fixPrompt = "The following JSON is malformed. Fix it and return ONLY valid JSON.\n"
            + "Expected format: {\"units\":[...], \"commands\":[{\"command\":\"...\", \"args\":{...}}]}\n\n"
            + "Broken JSON:\n" + brokenJson;

        try
        {
            var messages = new List<LlmMessage> { new("user", fixPrompt) };
            var fixedResponse = await _llm.CompleteAsync(
                "You fix broken JSON. Return ONLY the fixed JSON, nothing else.",
                messages, temperature: 0f, maxTokens: 500);

            if (string.IsNullOrEmpty(fixedResponse)) return null;

            var result = TryParseIntent(StripMarkdownFences(fixedResponse));
            if (result != null)
                Log.Info("IntentParser", "Retry succeeded.");
            else
                Log.Warn("IntentParser", "Retry also failed.");

            return result;
        }
        catch (Exception ex)
        {
            Log.Error("IntentParser", $"Retry error: {ex.Message}");
            return null;
        }
    }

    private static string StripMarkdownFences(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            text = text[7..];
        else if (text.StartsWith("```"))
            text = text[3..];
        if (text.EndsWith("```"))
            text = text[..^3];
        return text.Trim();
    }
}
