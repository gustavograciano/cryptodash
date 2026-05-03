using Refit;

namespace CryptoDash.Infrastructure.External.Anthropic;

public interface IAnthropicApi
{
    [Post("/v1/messages")]
    Task<AnthropicResponse> CreateMessageAsync(
        [Header("x-api-key")] string apiKey,
        [Header("anthropic-version")] string version,
        [Body] AnthropicRequest request,
        CancellationToken ct = default);
}

public record AnthropicRequest(
    string model,
    int max_tokens,
    string system,
    AnthropicMessage[] messages);

public record AnthropicMessage(string role, string content);

public class AnthropicResponse
{
    public List<AnthropicContent>? Content { get; set; }
    public string? Model { get; set; }
    public AnthropicUsage? Usage { get; set; }
}

public class AnthropicContent { public string? Type { get; set; } public string? Text { get; set; } }
public class AnthropicUsage { public int Input_tokens { get; set; } public int Output_tokens { get; set; } }
