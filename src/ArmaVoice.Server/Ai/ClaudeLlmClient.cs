using System.Text.Json;

namespace ArmaVoice.Server.Ai;

public class ClaudeLlmClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;

    public ClaudeLlmClient(string apiKey, string model = "claude-sonnet-4-20250514")
    {
        _apiKey = apiKey;
        _model = model;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<string?> CompleteAsync(string systemPrompt, List<LlmMessage> messages, float temperature = 0.1f, int maxTokens = 300)
    {
        var requestBody = new
        {
            model = _model,
            max_tokens = maxTokens,
            system = systemPrompt,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray()
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Claude] API error ({response.StatusCode}): {body[..Math.Min(200, body.Length)]}");
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var contentArray = doc.RootElement.GetProperty("content");
            if (contentArray.GetArrayLength() == 0) return null;

            return contentArray[0].GetProperty("text").GetString()?.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Claude] Error: {ex.Message}");
            return null;
        }
    }
}
