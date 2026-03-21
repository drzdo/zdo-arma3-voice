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
    private readonly ILlmClient _llm;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public IntentParser(ILlmClient llm)
    {
        _llm = llm;
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

            action (required, string): one of "move","attack","stop","hold","drop","regroup","formation","dialogue"

            units (required, array of strings): WHO should execute.
              - Return netIds from the known units list above when you can identify the unit.
              - "all" = entire squad.
              - Team color names: "red","green","blue","yellow" = Arma team colors.
              - Team numbers map to colors: group/team 1 = "red", 2 = "green", 3 = "blue", 4 = "yellow".
                This applies in any language: "группа 1" = "red", "team 2" = "green".
              - If the player says "second"/"третий"/ordinal, find the Nth unit in the list where sameGroup=true and return its netId.
              - If the player says a name like "Miller"/"Петрович", find the matching unit and return its netId.
              - If you cannot match to a known unit, return the name as-is (the server will fuzzy-match).
              - Default to ["all"] if not specified.

            Action descriptions:
            - "move" — move to a location. Needs "location".
            - "attack" — engage a target. Needs "target".
            - "stop" — cancel current action, stay put, remain responsive to new orders. "stop","freeze","halt","стой","замри".
            - "hold" — stop and LOCK position, won't move until new orders. "hold position","держать позицию".
            - "drop" — go prone immediately. "hit the dirt","get down","ложись","на землю".
            - "regroup" — come back to player. "regroup","come to me","ко мне","перегруппировка".
            - "formation" — change formation. Needs "formation".
            - "dialogue" — talk to NPC. Needs "target" and "text".

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
            - "dialogue" is ONLY for asking questions or making conversation (e.g. "Miller, what do you see?").
              Commands like "come to me", "go there", "hold" are NEVER dialogue, even if addressed by name informally.
              "Слышь Петрович иди ко мне" = regroup, NOT dialogue. "Петрович, что видишь?" = dialogue.
            - The player may use informal/colloquial speech, filler words ("слышь","ну","а ну-ка","давай").
              Ignore filler, extract the actual command.
            """ +
            """

            === EXAMPLES ===
            Speech: "second and third, move to that tree" -> {"action":"move","units":["2:3","2:7"],"location":{"type":"look_target"}}
            Speech: "Miller, move 100 meters forward, crouched" -> {"action":"move","units":["2:3"],"location":{"type":"relative","distance":100,"direction":"forward"},"stance":"MIDDLE"}
            Speech: "everyone move 200 meters azimuth 320" -> {"action":"move","units":["all"],"location":{"type":"azimuth","distance":200,"azimuth":320}}
            Speech: "attack that guy" -> {"action":"attack","units":["all"],"target":"2:10"}
            Speech: "regroup" -> {"action":"regroup","units":["all"]}
            Speech: "second and fifth, hold, prone" -> {"action":"hold","units":["2:3","2:7"],"stance":"DOWN"}
            Speech: "wedge formation" -> {"action":"formation","units":["all"],"formation":"WEDGE"}
            Speech: "Miller, what's the situation?" -> {"action":"dialogue","target":"2:3","text":"what's the situation?"}
            Speech: "третий пятый второй иди к тому дереву" -> {"action":"move","units":["2:5","2:9","2:3"],"location":{"type":"look_target"}}
            Speech: "группа 1 иди к этому дому" -> {"action":"move","units":["red"],"location":{"type":"look_target"}}
            Speech: "петрович иди туда" -> {"action":"move","units":["2:4"],"location":{"type":"look_target"}}
            Speech: "слышь петрович а ну ка иди ко мне" -> {"action":"regroup","units":["2:4"]}
            Speech: "Петрович, що бачиш попереду?" -> {"action":"dialogue","target":"2:4","text":"що бачиш попереду?"}
            Speech: "freeze!" -> {"action":"stop","units":["all"]}
            Speech: "стой!" -> {"action":"stop","units":["all"]}
            Speech: "hit the dirt!" -> {"action":"drop","units":["all"]}
            Speech: "ложись!" -> {"action":"drop","units":["all"]}
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
