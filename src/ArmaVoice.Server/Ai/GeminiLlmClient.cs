using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArmaVoice.Server.Ai;

public class GeminiLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _thinkingBudget;

    public GeminiLlmClient(string apiKey, string model = "gemini-2.0-flash-lite", int thinkingBudget = 0)
    {
        _apiKey = apiKey;
        _model = model;
        _thinkingBudget = thinkingBudget;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<string?> CompleteAsync(string systemPrompt, List<LlmMessage> messages, float temperature = 0.1f, int maxTokens = 300)
    {
        var contents = new JsonArray();
        foreach (var m in messages)
        {
            contents.Add(new JsonObject
            {
                ["role"] = m.Role == "assistant" ? "model" : "user",
                ["parts"] = new JsonArray { new JsonObject { ["text"] = m.Content } }
            });
        }

        var genConfig = new JsonObject
        {
            ["temperature"] = temperature,
            ["maxOutputTokens"] = maxTokens
        };
        if (_thinkingBudget > 0)
            genConfig["thinkingConfig"] = new JsonObject { ["thinkingBudget"] = _thinkingBudget };

        var requestBody = new JsonObject
        {
            ["system_instruction"] = new JsonObject
            {
                ["parts"] = new JsonArray { new JsonObject { ["text"] = systemPrompt } }
            },
            ["contents"] = contents,
            ["generationConfig"] = genConfig
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        try
        {
            var response = await _http.PostAsync(url,
                new StringContent(requestBody.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Gemini] API error ({response.StatusCode}): {body[..Math.Min(200, body.Length)]}");
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0) return null;

            return candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString()?.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Gemini] Error: {ex.Message}");
            return null;
        }
    }
}
