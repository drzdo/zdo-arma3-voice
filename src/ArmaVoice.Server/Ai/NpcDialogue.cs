using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArmaVoice.Server.Ai;

/// <summary>
/// Uses Claude API for NPC dialogue responses. Maintains per-NPC conversation history
/// and generates in-character responses based on NPC role, side, and nearby context.
/// </summary>
public class NpcDialogue
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly Dictionary<string, List<(string Role, string Text)>> _history = new();

    private const int MaxHistoryPerNpc = 10;
    private const int HistoryContextCount = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public NpcDialogue(string claudeApiKey)
    {
        _apiKey = claudeApiKey;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Generate an NPC dialogue response using Claude API.
    /// Maintains conversation history per NPC (by netId).
    /// </summary>
    public async Task<string> GenerateResponseAsync(
        string npcName,
        string npcRole,
        string npcSide,
        string playerText,
        List<UnitSummary> nearbyUnits,
        string npcNetId)
    {
        var nearbyContext = string.Join("\n", nearbyUnits.Select(u =>
            $"- {u.Name} ({u.UnitType}, {u.Side}{(u.SameGroup ? ", same group" : "")})"));

        var systemPrompt = $"""
            You are {npcName}, a military NPC in Arma 3. Stay in character at all times.

            Your details:
            - Name: {npcName}
            - Role/Class: {npcRole}
            - Side: {npcSide}

            Nearby units:
            {nearbyContext}

            Guidelines:
            - Respond naturally as this character would in a military setting.
            - Keep responses concise (1-3 sentences). Soldiers are brief on comms.
            - Use appropriate military terminology for your side and role.
            - Reference nearby units and the tactical situation when relevant.
            - Do not break character or reference game mechanics.
            - Do not use quotation marks around your own speech.
            """;

        // Build messages array with conversation history
        var messages = new List<object>();

        // Add recent history for this NPC
        if (_history.TryGetValue(npcNetId, out var history))
        {
            var recentHistory = history.Count > HistoryContextCount
                ? history[^HistoryContextCount..]
                : history;

            foreach (var (role, text) in recentHistory)
            {
                messages.Add(new { role, content = text });
            }
        }

        // Add the current player message
        messages.Add(new { role = "user", content = playerText });

        var requestBody = new
        {
            model = "claude-sonnet-4-20250514",
            max_tokens = 256,
            system = systemPrompt,
            messages
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody, JsonOptions);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _http.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[NpcDialogue] Claude API error ({response.StatusCode}): {responseBody[..Math.Min(200, responseBody.Length)]}");
                return $"*{npcName} does not respond*";
            }

            // Parse Claude response: { content: [{ type: "text", text: "..." }] }
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var contentArray = root.GetProperty("content");
            if (contentArray.GetArrayLength() == 0)
            {
                Console.WriteLine("[NpcDialogue] Claude returned empty content.");
                return $"*{npcName} does not respond*";
            }

            var npcResponse = contentArray[0].GetProperty("text").GetString() ?? "";
            npcResponse = npcResponse.Trim();

            // Update conversation history
            if (!_history.ContainsKey(npcNetId))
            {
                _history[npcNetId] = [];
            }

            _history[npcNetId].Add(("user", playerText));
            _history[npcNetId].Add(("assistant", npcResponse));

            // Cap history at max entries
            if (_history[npcNetId].Count > MaxHistoryPerNpc)
            {
                _history[npcNetId] = _history[npcNetId][^MaxHistoryPerNpc..];
            }

            Console.WriteLine($"[NpcDialogue] {npcName}: \"{npcResponse[..Math.Min(80, npcResponse.Length)]}\"");
            return npcResponse;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[NpcDialogue] Claude API request failed: {ex.Message}");
            return $"*{npcName} does not respond*";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NpcDialogue] Unexpected error: {ex.Message}");
            return $"*{npcName} does not respond*";
        }
    }
}
