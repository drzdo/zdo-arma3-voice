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

// ── Source-generated JSON context (trim/AOT safe) ────────

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(IntentParsed))]
internal partial class IntentJsonContext : JsonSerializerContext;

// ── Parser ───────────────────────────────────────────────

public class IntentParser
{
    private readonly ILlmClient _llm;
    private readonly string _commandPromptSection;

    public IntentParser(ILlmClient llm, string commandPromptSection)
    {
        _llm = llm;
        _commandPromptSection = commandPromptSection;
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

            === AVAILABLE COMMANDS (action field) ===
            {_commandPromptSection}

            === JSON SCHEMA ===

            action (required, string): command id from the list above.

            units (required, array of strings): WHO should execute.
              - Return netIds from the known units list above when you can identify the unit.
              - "all" = entire squad.
              - Team color names: "red","green","blue","yellow" = Arma team colors.
              - Team numbers map to colors: group/team 1 = "red", 2 = "green", 3 = "blue", 4 = "yellow".
              - If the player says "second"/"третий"/ordinal, find the Nth unit (sameGroup=true) and return its netId.
              - If the player says a name, find it and return its netId.
              - Default to ["all"] if not specified.

            location (optional, object): WHERE.
              type="look_target" — "there","that position","here".
              type="relative" + distance (meters) + direction ("forward","back","left","right","north","south","east","west")
              type="azimuth" + distance (meters) + azimuth (degrees 0-360)

            target (optional, string): target unit netId or name.
            text (optional, string): for dialogue — what the player said.
            formation (optional, string): COLUMN, LINE, WEDGE, VEE, STAG COLUMN, DIAMOND, FILE, ECH LEFT, ECH RIGHT
            stance (optional, string): DOWN (prone), MIDDLE (crouch), UP (standing), AUTO
            speed (optional, string): FULL (sprint), NORMAL (run), LIMITED (walk)

            === RULES ===
            - Return ONLY valid JSON. No markdown, no explanation. Omit null fields.
            - Prefer returning netIds over names.
            - "dialogue" is ONLY for conversation/questions, NEVER for giving orders.
            - Ignore filler words ("слышь","ну","а ну-ка","давай","hey").
            """;

        try
        {
            var messages = new List<LlmMessage> { new("user", speechText) };
            var textResponse = await _llm.CompleteAsync(systemPrompt, messages, temperature: 0.1f, maxTokens: 300);
            if (string.IsNullOrEmpty(textResponse)) return null;

            // Strip markdown fences
            if (textResponse.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                textResponse = textResponse[7..];
            else if (textResponse.StartsWith("```"))
                textResponse = textResponse[3..];
            if (textResponse.EndsWith("```"))
                textResponse = textResponse[..^3];
            textResponse = textResponse.Trim();

            var intent = JsonSerializer.Deserialize(textResponse, IntentJsonContext.Default.IntentParsed);
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
