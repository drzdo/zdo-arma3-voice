namespace ArmaVoice.Server.Ai;

public interface ILlmClient
{
    /// <summary>
    /// Send a completion request with a system prompt and messages.
    /// Returns the assistant's text response.
    /// </summary>
    Task<string?> CompleteAsync(string systemPrompt, List<LlmMessage> messages, float temperature = 0.1f, int maxTokens = 300);
}

public record LlmMessage(string Role, string Content);
