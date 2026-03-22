namespace ZdoArmaVoice.Server.Ai;

/// <summary>
/// Generates NPC dialog responses via ILlmClient.
/// System instructions and message come from SQF commands.
/// Maintains per-NPC conversation history.
/// </summary>
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
        string npcNetId)
    {
        // Build messages with history
        var messages = new List<LlmMessage>();

        if (_history.TryGetValue(npcNetId, out var history))
        {
            var recent = history.Count > HistoryContextCount * 2
                ? history[^(HistoryContextCount * 2)..]
                : history;
            messages.AddRange(recent);
        }

        messages.Add(new LlmMessage("user", message));

        var response = await _llm.CompleteAsync(systemInstructions, messages, temperature: 0.7f, maxTokens: 256);
        var npcResponse = response?.Trim() ?? "*does not respond*";

        // Update history
        if (!_history.ContainsKey(npcNetId))
            _history[npcNetId] = [];

        _history[npcNetId].Add(new LlmMessage("user", message));
        _history[npcNetId].Add(new LlmMessage("assistant", npcResponse));

        if (_history[npcNetId].Count > MaxHistoryPerNpc * 2)
            _history[npcNetId] = _history[npcNetId][^(MaxHistoryPerNpc * 2)..];

        Log.Info("NpcDialog", $"\"{npcResponse[..Math.Min(80, npcResponse.Length)]}\"");
        return npcResponse;
    }
}
