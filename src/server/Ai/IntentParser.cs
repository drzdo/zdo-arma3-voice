using System.Text.Json;

namespace ZdoArmaVoice.Server.Ai;

/// <summary>
/// LLM output: array of commands with args.
/// </summary>
public class ParsedCommand
{
    public string Command { get; set; } = "";
    public JsonElement Args { get; set; }
}

/// <summary>
/// Result from SQF coreIntentPrompt.
/// </summary>
public class IntentPromptResult
{
    public string SystemInstructions { get; set; } = "";
    public string Message { get; set; } = "";
    public float[] LookAtPosition { get; set; } = [0, 0, 0];
}

/// <summary>
/// Calls SQF for the intent prompt, sends to LLM, parses response into commands.
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

    /// <summary>
    /// Get intent prompt from SQF, call LLM, parse response.
    /// </summary>
    public async Task<(List<ParsedCommand> Commands, float[] LookAtPosition)?> ParseAsync(string speechText)
    {
        try
        {
            // 1. Call SQF for prompt + lookAtPosition
            var promptResult = await GetIntentPromptAsync(speechText);
            if (promptResult == null) return null;

            // 2. Send to LLM
            var messages = new List<LlmMessage> { new("user", promptResult.Message) };
            var textResponse = await _llm.CompleteAsync(
                promptResult.SystemInstructions, messages, temperature: 0.1f, maxTokens: 500);

            if (string.IsNullOrEmpty(textResponse)) return null;

            var json = StripMarkdownFences(textResponse);

            // 3. Parse response as command array
            var commands = TryParseCommands(json);
            if (commands == null)
            {
                Log.Warn("IntentParser", "Bad JSON from LLM, retrying with fix prompt...");
                commands = await RetryFixAsync(json);
            }

            if (commands != null)
            {
                foreach (var cmd in commands)
                    Log.Info("IntentParser", $"  command={cmd.Command} args={cmd.Args}");
            }

            return commands != null ? (commands, promptResult.LookAtPosition) : null;
        }
        catch (Exception ex)
        {
            Log.Error("IntentParser", $"Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Validate a specific command's args against its schema (from SQF).
    /// If invalid, retry LLM with the schema.
    /// </summary>
    public async Task<JsonElement?> ValidateAndRetryArgsAsync(string commandId, JsonElement args, string schema)
    {
        // Basic validation: args should be a JSON object
        if (args.ValueKind == JsonValueKind.Object) return args;

        Log.Warn("IntentParser", $"Args for '{commandId}' are not an object, retrying...");
        var fixPrompt = $"Fix the args for command '{commandId}'. Expected schema: {schema}\n\nBroken args: {args}\n\nReturn ONLY the fixed JSON object.";
        var messages = new List<LlmMessage> { new("user", fixPrompt) };
        var fixedResponse = await _llm.CompleteAsync("Fix JSON args. Return ONLY the fixed JSON object.", messages, temperature: 0f, maxTokens: 300);
        if (string.IsNullOrEmpty(fixedResponse)) return null;

        try
        {
            return JsonDocument.Parse(StripMarkdownFences(fixedResponse)).RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private async Task<IntentPromptResult?> GetIntentPromptAsync(string speechText)
    {
        try
        {
            var escaped = speechText.Replace("\"", "\\\"");
            var result = await _rpc.CallAsync($"[\"{escaped}\"] call zdoZdoArmaVoice_fnc_coreIntentPrompt");

            // Result is a JSON hashmap from toJSON
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

            return new IntentPromptResult
            {
                SystemInstructions = system,
                Message = message,
                LookAtPosition = lookAt
            };
        }
        catch (Exception ex)
        {
            Log.Error("IntentParser", $"Failed to get intent prompt from SQF: {ex.Message}");
            return null;
        }
    }

    private static List<ParsedCommand>? TryParseCommands(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Handle both array and single object
            var elements = root.ValueKind == JsonValueKind.Array
                ? root.EnumerateArray().ToList()
                : [root];

            var commands = new List<ParsedCommand>();
            foreach (var el in elements)
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
                    commands.Add(cmd);
            }

            return commands.Count > 0 ? commands : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<ParsedCommand>?> RetryFixAsync(string brokenJson)
    {
        var fixPrompt = "The following JSON is malformed. Fix it and return ONLY valid JSON.\n"
            + "Expected format: [{\"command\":\"...\", \"args\":{...}}]\n\n"
            + "Broken JSON:\n" + brokenJson;

        try
        {
            var messages = new List<LlmMessage> { new("user", fixPrompt) };
            var fixedResponse = await _llm.CompleteAsync(
                "You fix broken JSON. Return ONLY the fixed JSON, nothing else.",
                messages, temperature: 0f, maxTokens: 500);

            if (string.IsNullOrEmpty(fixedResponse)) return null;

            var result = TryParseCommands(StripMarkdownFences(fixedResponse));
            if (result != null)
                Log.Info("IntentParser", "Retry succeeded.");
            else
                Log.Warn("IntentParser", $"Retry also failed.");

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
