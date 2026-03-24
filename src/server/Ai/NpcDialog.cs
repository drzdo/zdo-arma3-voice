using System.Diagnostics;
using System.Text.Json;

namespace ZdoArmaVoice.Server.Ai;

public class NpcDialog
{
    private readonly ILlmClient _llm;
    private readonly Dictionary<string, List<LlmMessage>> _history = new();

    private const int MaxHistoryPerNpc = 10;
    private const int HistoryContextCount = 5;

    public NpcDialog(ILlmClient llm)
    {
        _llm = llm;
    }

    public async Task<string> GenerateResponseAsync(
        string systemInstructions,
        string message,
        string npcNetId,
        LlmImage? image = null,
        string? playerName = null,
        string? npcName = null)
    {
        var messages = new List<LlmMessage>();

        if (_history.TryGetValue(npcNetId, out var history))
        {
            var recent = history.Count > HistoryContextCount * 2
                ? history[^(HistoryContextCount * 2)..]
                : history;
            messages.AddRange(recent);
        }

        if (image != null)
        {
            var visionPrompt = systemInstructions
                + "\n\nThe player is showing you something (screenshot attached). "
                + "You must respond with JSON containing two fields:\n"
                + "1. \"image_description\": A thorough, detailed description of everything visible in the image — "
                + "layout, labels, markers, terrain, routes, positions, colors, text, symbols, and any other relevant details. "
                + "Be exhaustive so someone who cannot see the image can fully understand it.\n"
                + "2. \"response\": Your in-character response to the player's message, informed by what you see in the image.\n\n"
                + "Return ONLY valid JSON, no markdown, no explanation. Example:\n"
                + "{\"image_description\": \"...\", \"response\": \"...\"}";

            messages.Add(new LlmMessage("user", message, image));

            var sw = Stopwatch.StartNew();
            var rawResponse = await _llm.CompleteAsync(visionPrompt, messages, temperature: 0.7f, maxTokens: 1024);
            sw.Stop();

            var (description, npcResponse) = ParseVisionResponse(rawResponse);

            Log.Info("LLM", $"Vision dialog ({sw.ElapsedMilliseconds}ms): desc={description.Length}ch, response=\"{npcResponse[..Math.Min(100, npcResponse.Length)]}\"");

            // Store text-only in history: player's message with image description, NPC response
            var pName = playerName ?? "Player";
            var nName = npcName ?? "Soldier";
            var historyUserMsg = $"[{pName} shows {nName} a screenshot: {description}]\n{pName}: {message}";

            if (!_history.ContainsKey(npcNetId))
                _history[npcNetId] = [];

            _history[npcNetId].Add(new LlmMessage("user", historyUserMsg));
            _history[npcNetId].Add(new LlmMessage("assistant", npcResponse));

            if (_history[npcNetId].Count > MaxHistoryPerNpc * 2)
                _history[npcNetId] = _history[npcNetId][^(MaxHistoryPerNpc * 2)..];

            return npcResponse;
        }
        else
        {
            messages.Add(new LlmMessage("user", message));

            var sw = Stopwatch.StartNew();
            var response = await _llm.CompleteAsync(systemInstructions, messages, temperature: 0.7f, maxTokens: 256);
            sw.Stop();

            var npcResponse = response?.Trim() ?? "*does not respond*";

            Log.Info("LLM", $"Dialog result ({sw.ElapsedMilliseconds}ms): \"{npcResponse[..Math.Min(100, npcResponse.Length)]}\"");

            if (!_history.ContainsKey(npcNetId))
                _history[npcNetId] = [];

            _history[npcNetId].Add(new LlmMessage("user", message));
            _history[npcNetId].Add(new LlmMessage("assistant", npcResponse));

            if (_history[npcNetId].Count > MaxHistoryPerNpc * 2)
                _history[npcNetId] = _history[npcNetId][^(MaxHistoryPerNpc * 2)..];

            return npcResponse;
        }
    }

    private static (string Description, string Response) ParseVisionResponse(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return ("(no description)", "*does not respond*");

        // Strip markdown code fences if present
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0) trimmed = trimmed[(firstNewline + 1)..];
            if (trimmed.EndsWith("```")) trimmed = trimmed[..^3];
            trimmed = trimmed.Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            var description = root.TryGetProperty("image_description", out var d)
                ? d.GetString() ?? "(no description)"
                : "(no description)";
            var response = root.TryGetProperty("response", out var r)
                ? r.GetString()?.Trim() ?? "*does not respond*"
                : "*does not respond*";
            return (description, response);
        }
        catch
        {
            // If JSON parsing fails, treat the whole thing as the response
            Log.Warn("NpcDialog", $"Failed to parse vision JSON, using raw response");
            return ("(failed to parse image description)", raw.Trim());
        }
    }
}
