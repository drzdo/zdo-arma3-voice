namespace ArmaVoice.Server.Ai;

/// <summary>
/// Generates NPC dialogue responses via ILlmClient.
/// Maintains per-NPC conversation history.
/// </summary>
public class NpcDialogue
{
    private readonly ILlmClient _llm;
    private readonly string _sessionContext;
    private readonly float _useRankChance;
    private readonly Random _rng = new();
    private readonly Dictionary<string, List<LlmMessage>> _history = new();

    private const int MaxHistoryPerNpc = 10;
    private const int HistoryContextCount = 5;

    public NpcDialogue(ILlmClient llm, string sessionContext = "", float useRankChance = 0.5f)
    {
        _llm = llm;
        _sessionContext = sessionContext;
        _useRankChance = Math.Clamp(useRankChance, 0f, 1f);
    }

    public async Task<string> GenerateResponseAsync(
        string npcName,
        string npcRole,
        string npcSide,
        string playerText,
        List<UnitSummary> nearbyUnits,
        string npcNetId,
        string playerName = "",
        string playerRank = "")
    {
        var nearbyContext = string.Join("\n", nearbyUnits.Select(u =>
            $"- {u.Name} ({u.UnitType}, {u.Side}{(u.SameGroup ? ", same group" : "")})"));

        var systemPrompt = $"""
            You are {npcName}, a military NPC in Arma 3. Stay in character at all times.
            The player commanding you is {playerRank} {playerName}.{(_rng.NextSingle() < _useRankChance ? " Address them by rank (e.g. \"сержант\", \"командир\")." : "")}

            Your details:
            - Name: {npcName}
            - Role/Class: {npcRole}
            - Side: {npcSide}

            Nearby units:
            {nearbyContext}
            {(string.IsNullOrWhiteSpace(_sessionContext) ? "" : $"\n            Mission context: {_sessionContext}\n")}
            Guidelines:
            - Respond naturally as this character would in a military setting.
            - Keep responses concise (1-3 sentences). Soldiers are brief on comms.
            - Use appropriate military terminology for your side and role.
            - ONLY reference units and things that are in the nearby units list above. Do NOT invent or imagine enemies, civilians, vehicles, or anything not listed. If you don't know, say you don't know.
            - Do not break character or reference game mechanics.
            - Do not use quotation marks around your own speech.
            """;

        // Build messages with history
        var messages = new List<LlmMessage>();

        if (_history.TryGetValue(npcNetId, out var history))
        {
            var recent = history.Count > HistoryContextCount * 2
                ? history[^(HistoryContextCount * 2)..]
                : history;
            messages.AddRange(recent);
        }

        messages.Add(new LlmMessage("user", playerText));

        var response = await _llm.CompleteAsync(systemPrompt, messages, temperature: 0.7f, maxTokens: 256);
        var npcResponse = response?.Trim() ?? $"*{npcName} does not respond*";

        // Update history
        if (!_history.ContainsKey(npcNetId))
            _history[npcNetId] = [];

        _history[npcNetId].Add(new LlmMessage("user", playerText));
        _history[npcNetId].Add(new LlmMessage("assistant", npcResponse));

        if (_history[npcNetId].Count > MaxHistoryPerNpc * 2)
            _history[npcNetId] = _history[npcNetId][^(MaxHistoryPerNpc * 2)..];

        Log.Info("NpcDialogue", $"{npcName}: \"{npcResponse[..Math.Min(80, npcResponse.Length)]}\"");
        return npcResponse;
    }
}
