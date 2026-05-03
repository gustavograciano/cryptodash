using CryptoDash.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CryptoDash.Infrastructure.External.CoinGecko;

public class CoinGeckoPriceFeed(ICoinGeckoApi api, IMemoryCache cache, ILogger<CoinGeckoPriceFeed> logger) : IPriceFeed
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<PriceQuote>> GetSimplePricesAsync(IEnumerable<string> coingeckoIds, CancellationToken ct = default)
    {
        var ids = coingeckoIds.Distinct().OrderBy(x => x).ToList();
        if (ids.Count == 0) return Array.Empty<PriceQuote>();
        var cacheKey = "cg:simple:" + string.Join(",", ids);
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<PriceQuote>? cached) && cached is not null)
            return cached;

        try
        {
            var response = await api.GetSimplePriceAsync(
                string.Join(",", ids),
                "brl,usd",
                include24hChange: true,
                include24hVol: true,
                includeMarketCap: true,
                ct: ct);

            var quotes = response.Select(kv => new PriceQuote(
                kv.Key,
                kv.Value.Brl ?? 0m,
                kv.Value.Usd ?? 0m,
                kv.Value.Brl_24h_change ?? 0m,
                kv.Value.Brl_24h_vol ?? 0m,
                kv.Value.Brl_market_cap ?? 0m
            )).ToList();

            cache.Set(cacheKey, (IReadOnlyList<PriceQuote>)quotes, CacheTtl);
            return quotes;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CoinGecko simple/price falhou");
            return Array.Empty<PriceQuote>();
        }
    }
}
