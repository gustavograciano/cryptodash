using CryptoDash.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CryptoDash.Infrastructure.External.CryptoPanic;

public class CryptoPanicNewsFeed(ICryptoPanicApi api, IConfiguration config, ILogger<CryptoPanicNewsFeed> logger) : INewsFeed
{
    public async Task<IReadOnlyList<NewsItem>> GetLatestAsync(IEnumerable<string> currencies, CancellationToken ct = default)
    {
        var token = config["CryptoPanic:ApiKey"];
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("CryptoPanic ApiKey ausente; pulando fetch");
            return Array.Empty<NewsItem>();
        }

        try
        {
            var resp = await api.GetPostsAsync(token, string.Join(",", currencies), true, "news", ct);
            return (resp.Results ?? new()).Select(p => new NewsItem(
                p.Id ?? Guid.NewGuid().ToString(),
                p.Title ?? "(sem título)",
                p.Url ?? "",
                p.Source?.Title ?? "unknown",
                p.Published_at ?? DateTimeOffset.UtcNow,
                p.Currencies?.Select(c => c.Code ?? "").Where(c => !string.IsNullOrEmpty(c)).ToList() ?? new(),
                null,
                null
            )).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CryptoPanic fetch falhou");
            return Array.Empty<NewsItem>();
        }
    }
}
