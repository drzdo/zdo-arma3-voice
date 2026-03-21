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
    private readonly string _sessionContext;

    public IntentParser(ILlmClient llm, string commandPromptSection, string sessionContext = "")
    {
        _llm = llm;
        _commandPromptSection = commandPromptSection;
        _sessionContext = sessionContext;
    }

    public async Task<IntentParsed?> ParseAsync(string speechText, List<UnitSummary> knownUnits)
    {
        var squadMembers = knownUnits.Where(u => u.SameGroup).ToList();
        var others = knownUnits.Where(u => !u.SameGroup).ToList();

        var squadContext = string.Join("\n", squadMembers.Select((u, i) =>
            $"  #{i + 1} netId=\"{u.NetId}\" name=\"{u.Name}\" type={u.UnitType}"));

        var othersContext = others.Count > 0
            ? "\n  Other units nearby:\n" + string.Join("\n", others.Select(u =>
                $"  - netId=\"{u.NetId}\" name=\"{u.Name}\" side={u.Side} type={u.UnitType}"))
            : "";

        var unitContext = $"  Player's squad (in order):\n{squadContext}{othersContext}";

        var sessionBlock = string.IsNullOrWhiteSpace(_sessionContext)
            ? ""
            : $"\n            Mission context: {_sessionContext}\n";

        var systemPrompt = $"""
            You are a military voice command parser for Arma 3.
            Parse the player's speech into a structured JSON command.
            The player may speak in any language (English, Russian, etc).
            {sessionBlock}

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
              - If the player says "second"/"второй"/"третий"/ordinal, use the squad # number above. "Second"/"второй" = #2, "third"/"третий" = #3, etc. Return that unit's netId.
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

            var json = StripMarkdownFences(textResponse);
            var intent = TryParseIntent(json);

            if (intent == null)
            {
                Log.Warn("IntentParser", "Bad JSON from LLM, retrying with fix prompt...");
                intent = await RetryWithFixPromptAsync(json);
            }

            if (intent != null)
                Log.Info("IntentParser", $"action={intent.Action} units=[{string.Join(",", intent.Units)}] location={intent.Location?.Type} target={intent.Target} stance={intent.Stance} speed={intent.Speed}");

            return intent;
        }
        catch (Exception ex)
        {
            Log.Error("IntentParser", $"Error: {ex.Message}");
            return null;
        }
    }

    private async Task<IntentParsed?> RetryWithFixPromptAsync(string brokenJson)
    {
        var fixPrompt = """
            The following JSON is malformed or doesn't match the expected schema. Fix it and return ONLY valid JSON.

            Expected schema:
            {"action":"...","units":["..."],"location":{"type":"...","distance":0,"direction":"...","azimuth":0},"target":"...","text":"...","formation":"...","stance":"...","speed":"..."}
            All fields except "action" and "units" are optional — omit if not needed.

            Broken JSON:
            """ + brokenJson;

        try
        {
            var messages = new List<LlmMessage> { new("user", fixPrompt) };
            var fixedResponse = await _llm.CompleteAsync(
                "You fix broken JSON. Return ONLY the fixed JSON, nothing else.", messages, temperature: 0f, maxTokens: 300);

            if (string.IsNullOrEmpty(fixedResponse)) return null;

            var json = StripMarkdownFences(fixedResponse);
            var intent = TryParseIntent(json);

            if (intent != null)
                Log.Info("IntentParser", "Retry succeeded.");
            else
                Log.Warn("IntentParser", $"Retry also failed: {json[..Math.Min(80, json.Length)]}");

            return intent;
        }
        catch (Exception ex)
        {
            Log.Error("IntentParser", $"Retry error: {ex.Message}");
            return null;
        }
    }

    private static IntentParsed? TryParseIntent(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, IntentJsonContext.Default.IntentParsed);
        }
        catch
        {
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
