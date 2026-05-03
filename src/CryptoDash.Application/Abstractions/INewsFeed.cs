namespace CryptoDash.Application.Abstractions;

public record NewsItem(
    string ExternalId,
    string Title,
    string Url,
    string Source,
    DateTimeOffset PublishedAt,
    IReadOnlyList<string> CoinsMentioned,
    string? Sentiment,
    string? RawPayload);

public interface INewsFeed
{
    Task<IReadOnlyList<NewsItem>> GetLatestAsync(IEnumerable<string> currencies, CancellationToken ct = default);
}

public interface ILlmDigestGenerator
{
    Task<DigestGeneration> GenerateAsync(IReadOnlyList<string> coins, IReadOnlyList<NewsItemForPrompt> news, CancellationToken ct = default);
}

public record NewsItemForPrompt(string Title, string Url, string Source, IReadOnlyList<string> Coins);
public record DigestGeneration(string Markdown, string Model, int TokensUsed);
