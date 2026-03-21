using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArmaVoice.Server.Ai;

/// <summary>
/// Structured intent parsed from player speech by the LLM.
/// </summary>
public class IntentParsed
{
    /// <summary>WHAT: move, attack, hold, regroup, formation, dialogue</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    /// <summary>TO WHOM: unit references — names, "all", "team_red", "unit_2", etc.</summary>
    [JsonPropertyName("units")]
    public List<string> Units { get; set; } = [];

    /// <summary>WHERE: "look_target", "100m_forward", "200m_north", explicit coords</summary>
    [JsonPropertyName("location")]
    public string? Location { get; set; }

    /// <summary>Attack target or dialogue target NPC name</summary>
    [JsonPropertyName("target")]
    public string? Target { get; set; }

    /// <summary>Dialogue text (what the player said to the NPC)</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Formation type: column, line, wedge, etc.</summary>
    [JsonPropertyName("formation")]
    public string? Formation { get; set; }

    /// <summary>HOW (stance): "prone", "crouch", "standing", "auto"</summary>
    [JsonPropertyName("stance")]
    public string? Stance { get; set; }

    /// <summary>HOW (speed): "sprint", "run", "walk"</summary>
    [JsonPropertyName("speed")]
    public string? Speed { get; set; }
}

/// <summary>
/// Summary of a known unit, provided as context to the LLM for intent parsing.
/// </summary>
public class UnitSummary
{
    public string NetId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Side { get; set; } = "";
    public bool SameGroup { get; set; }
    public string UnitType { get; set; } = "";
}

/// <summary>
/// Uses Gemini Flash 2.0 to parse player speech into structured intents.
/// Calls the Gemini API with a system prompt that explains available actions
/// and known unit context, then parses the JSON response.
/// </summary>
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
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    /// <summary>
    /// Parse player speech text into a structured intent using Gemini Flash 2.0.
    /// Returns null if parsing fails.
    /// </summary>
    public async Task<IntentParsed?> ParseAsync(string speechText, List<UnitSummary> knownUnits)
    {
        var unitContext = string.Join("\n", knownUnits.Select(u =>
            $"- NetId: {u.NetId}, Name: \"{u.Name}\", Side: {u.Side}, Type: {u.UnitType}, SameGroup: {u.SameGroup}"));

        var systemPrompt = $"""
            You are a military voice command parser for Arma 3. Parse the player's speech into a structured JSON command.
            The player may speak in any language (English, Russian, etc). Parse intent regardless of language.

            Each command has up to 4 dimensions:

            TO WHOM ("units" field) — who should execute:
            - "all" — entire squad
            - "team_red", "team_green", "team_blue", "team_yellow" — Arma team colors
            - Unit name: "Miller", "the medic", "unit 2" (index in squad), "unit 2 and 3"
            - If not specified, default to ["all"]

            WHAT ("action" field) — the action:
            - "move" — move to a location. Needs "location".
            - "attack" — engage a target. Needs "target" (enemy name/reference).
            - "hold" — stop and hold current position.
            - "regroup" — regroup on the player / fall back to player.
            - "formation" — change formation. Needs "formation" (column, line, wedge, vee, staggered column, diamond, file, echelon left, echelon right).
            - "dialogue" — player is talking to an NPC conversationally. Needs "target" (NPC name) and "text" (what player said).

            HOW (optional modifiers, can accompany any action):
            - "stance": "prone"/"crouch"/"standing"/"auto" — body posture while executing.
              E.g. "move there prone", "hold position crouched", "crawl" = prone.
            - "speed": "sprint"/"run"/"walk" — movement speed.
              E.g. "run to that building", "move slowly" = walk.

            WHERE ("location" field):
            - "look_target" — where the player's crosshair/map cursor is pointing. Use for "there", "that building", "that position", "here", etc.
            - "Xm_forward", "Xm_back", "Xm_left", "Xm_right", "Xm_north", "Xm_south", "Xm_east", "Xm_west" — relative distance. E.g. "100 meters ahead" = "100m_forward", "50m to the left" = "50m_left".
            - If not specified and not needed, omit.

            Known units in the area:
            {unitContext}

            Return ONLY valid JSON, no markdown, no explanation. Omit null fields.
            """ +
            """
            Example: {"action":"move","units":["team_red","team_green"],"location":"look_target","stance":"prone","speed":"run"}
            Example: {"action":"regroup","units":["all"]}
            Example: {"action":"move","units":["Miller"],"location":"100m_forward","stance":"crouch"}
            Example: {"action":"dialogue","target":"Miller","text":"what's the situation ahead?"}
            """;

        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new { text = systemPrompt }
                }
            },
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = speechText }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 256
            }
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
                Console.WriteLine($"[IntentParser] Gemini API error ({response.StatusCode}): {responseBody[..Math.Min(200, responseBody.Length)]}");
                return null;
            }

            // Parse Gemini response: { candidates: [{ content: { parts: [{ text: "..." }] } }] }
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var candidates = root.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0)
            {
                Console.WriteLine("[IntentParser] Gemini returned no candidates.");
                return null;
            }

            var textResponse = candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "";

            // Strip markdown code fences if present
            textResponse = textResponse.Trim();
            if (textResponse.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                textResponse = textResponse[7..];
            }
            else if (textResponse.StartsWith("```"))
            {
                textResponse = textResponse[3..];
            }

            if (textResponse.EndsWith("```"))
            {
                textResponse = textResponse[..^3];
            }

            textResponse = textResponse.Trim();

            var intent = JsonSerializer.Deserialize<IntentParsed>(textResponse, JsonOptions);
            Console.WriteLine($"[IntentParser] Parsed: action={intent?.Action} units=[{string.Join(", ", intent?.Units ?? [])}] location={intent?.Location} target={intent?.Target} stance={intent?.Stance} speed={intent?.Speed}");
            return intent;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[IntentParser] JSON parse error: {ex.Message}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[IntentParser] Gemini API request failed: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IntentParser] Unexpected error: {ex.Message}");
            return null;
        }
    }
}
