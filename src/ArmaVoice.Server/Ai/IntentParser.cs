using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArmaVoice.Server.Ai;

// ── LLM output contract ─────────────────────────────────

public class IntentParsed
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    /// <summary>
    /// Unit netIds, names, team colors ("red","green","blue","yellow"), or "all".
    /// LLM is given the full unit registry and should return netIds when possible.
    /// </summary>
    [JsonPropertyName("units")]
    public List<string> Units { get; set; } = [];

    [JsonPropertyName("location")]
    public LocationParsed? Location { get; set; }

    /// <summary>Attack target or dialogue target — netId or name.</summary>
    [JsonPropertyName("target")]
    public string? Target { get; set; }

    /// <summary>Dialogue: what the player said to the NPC.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>SQF formation constant: COLUMN, LINE, WEDGE, VEE, STAG COLUMN, DIAMOND, FILE, ECH LEFT, ECH RIGHT</summary>
    [JsonPropertyName("formation")]
    public string? Formation { get; set; }

    /// <summary>SQF stance: DOWN (prone), MIDDLE (crouch), UP (standing), AUTO</summary>
    [JsonPropertyName("stance")]
    public string? Stance { get; set; }

    /// <summary>SQF speed mode: FULL (sprint), NORMAL (run), LIMITED (walk)</summary>
    [JsonPropertyName("speed")]
    public string? Speed { get; set; }
}

public class LocationParsed
{
    /// <summary>"look_target", "relative", "azimuth"</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "look_target";

    [JsonPropertyName("distance")]
    public float? Distance { get; set; }

    /// <summary>For relative: "forward","back","left","right","north","south","east","west"</summary>
    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    /// <summary>For azimuth: degrees 0-360</summary>
    [JsonPropertyName("azimuth")]
    public float? Azimuth { get; set; }
}

// ── Context passed to LLM ───────────────────────────────

public class UnitSummary
{
    public string NetId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Side { get; set; } = "";
    public bool SameGroup { get; set; }
    public string UnitType { get; set; } = "";
}

// ── Parser ───────────────────────────────────────────────

public class IntentParser
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public IntentParser(string geminiApiKey)
    {
        _apiKey = geminiApiKey;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<IntentParsed?> ParseAsync(string speechText, List<UnitSummary> knownUnits)
    {
        var unitContext = string.Join("\n", knownUnits.Select(u =>
            $"  - netId=\"{u.NetId}\" name=\"{u.Name}\" side={u.Side} type={u.UnitType} sameGroup={u.SameGroup}"));

        var systemPrompt = $"""
            You are a military voice command parser for Arma 3.
            Parse the player's speech into a structured JSON command.
            The player may speak in any language (English, Russian, etc).

            IMPORTANT: The LLM does ALL parsing. Return final values ready for execution.
            Do NOT return raw text — return structured, normalized values.

            Known units:
            {unitContext}

            === JSON SCHEMA ===

            action (required, string): one of "move","attack","hold","regroup","formation","dialogue"

            units (required, array of strings): WHO should execute.
              - Return netIds from the known units list above when you can identify the unit.
              - "all" = entire squad.
              - Team color names: "red","green","blue","yellow" = Arma team colors.
              - If the player says "second" or "unit 2", find the 2nd unit in the list where sameGroup=true and return its netId.
              - If the player says a name like "Miller", find the matching unit and return its netId.
              - If you cannot match to a known unit, return the name as-is (the server will fuzzy-match).
              - Default to ["all"] if not specified.

            location (optional, object): WHERE. Only for "move" action.
              type="look_target" — player said "there","that position","here". No other fields needed.
              type="relative" + distance (meters) + direction ("forward","back","left","right","north","south","east","west")
                Example: "100 meters ahead" -> type="relative", distance=100, direction="forward"
                "forward"/"front"/"ahead" = same direction the player is facing.
              type="azimuth" + distance (meters) + azimuth (degrees 0-360)
                Example: "200 meters azimuth 320" -> type="azimuth", distance=200, azimuth=320

            target (optional, string): for "attack" = target unit netId or name. For "dialogue" = NPC netId or name.

            text (optional, string): for "dialogue" only — what the player said to the NPC.

            formation (optional, string): SQF formation constant, one of:
              "COLUMN","LINE","WEDGE","VEE","STAG COLUMN","DIAMOND","FILE","ECH LEFT","ECH RIGHT"

            stance (optional, string): SQF unit pos, one of: "DOWN" (prone/crawl), "MIDDLE" (crouch), "UP" (standing), "AUTO"
              Can accompany any action. E.g. "move there prone" -> stance="DOWN"

            speed (optional, string): SQF speed mode, one of: "FULL" (sprint/fast), "NORMAL" (run), "LIMITED" (walk/slow)
              Can accompany any action. E.g. "run to that building" -> speed="FULL"

            === RULES ===
            - Return ONLY valid JSON. No markdown, no explanation.
            - Omit null/absent fields.
            - Use SQF constants for formation/stance/speed (uppercase as shown above).
            - Prefer returning netIds over names for units and targets.
            """ +
            """

            === EXAMPLES ===
            {"action":"move","units":["2:3","2:7"],"location":{"type":"look_target"},"stance":"DOWN","speed":"FULL"}
            {"action":"move","units":["2:3"],"location":{"type":"relative","distance":100,"direction":"forward"},"stance":"MIDDLE"}
            {"action":"move","units":["all"],"location":{"type":"azimuth","distance":200,"azimuth":320}}
            {"action":"attack","units":["all"],"target":"2:10"}
            {"action":"regroup","units":["all"]}
            {"action":"hold","units":["2:3","2:7"],"stance":"DOWN"}
            {"action":"formation","units":["all"],"formation":"WEDGE"}
            {"action":"dialogue","target":"2:3","text":"what's the situation ahead?"}
            """;

        var requestBody = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { parts = new[] { new { text = speechText } } } },
            generationConfig = new { temperature = 0.1, maxOutputTokens = 300 }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}";

        try
        {
            var json = JsonSerializer.Serialize(requestBody, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[IntentParser] Gemini error ({response.StatusCode}): {responseBody[..Math.Min(200, responseBody.Length)]}");
                return null;
            }

            using var doc = JsonDocument.Parse(responseBody);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0) return null;

            var textResponse = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "";

            // Strip markdown fences
            textResponse = textResponse.Trim();
            if (textResponse.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                textResponse = textResponse[7..];
            else if (textResponse.StartsWith("```"))
                textResponse = textResponse[3..];
            if (textResponse.EndsWith("```"))
                textResponse = textResponse[..^3];
            textResponse = textResponse.Trim();

            var intent = JsonSerializer.Deserialize<IntentParsed>(textResponse, JsonOptions);
            Console.WriteLine($"[IntentParser] action={intent?.Action} units=[{string.Join(",", intent?.Units ?? [])}] location={intent?.Location?.Type} target={intent?.Target} stance={intent?.Stance} speed={intent?.Speed}");
            return intent;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IntentParser] Error: {ex.Message}");
            return null;
        }
    }
}
