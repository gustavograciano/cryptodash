using CryptoDash.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace CryptoDash.Infrastructure.External.CoinGecko;

public class CoinGeckoMarketDataFeed(ICoinGeckoApi api, ILogger<CoinGeckoMarketDataFeed> logger) : IMarketDataFeed
{
    public async Task<IReadOnlyList<OhlcvPoint>> GetMarketChartAsync(string coingeckoId, int days, CancellationToken ct = default)
    {
        try
        {
            var resp = await api.GetMarketChartAsync(coingeckoId, "brl", days, ct);
            if (resp.Prices.Count == 0) return Array.Empty<OhlcvPoint>();

            var byDate = new Dictionary<DateOnly, (decimal open, decimal high, decimal low, decimal close, decimal volume)>();
            foreach (var p in resp.Prices)
            {
                if (p.Count < 2) continue;
                var ts = DateTimeOffset.FromUnixTimeMilliseconds((long)p[0]).UtcDateTime;
                var date = DateOnly.FromDateTime(ts);
                var price = p[1];
                if (!byDate.TryGetValue(date, out var v))
                {
                    byDate[date] = (price, price, price, price, 0m);
                }
                else
                {
                    byDate[date] = (v.open, Math.Max(v.high, price), Math.Min(v.low, price), price, v.volume);
                }
            }
            foreach (var v in resp.Total_volumes)
            {
                if (v.Count < 2) continue;
                var ts = DateTimeOffset.FromUnixTimeMilliseconds((long)v[0]).UtcDateTime;
                var date = DateOnly.FromDateTime(ts);
                if (byDate.TryGetValue(date, out var current))
                    byDate[date] = (current.open, current.high, current.low, current.close, current.volume + v[1]);
            }

            return byDate.OrderBy(kv => kv.Key)
                .Select(kv => new OhlcvPoint(kv.Key,
                    Math.Round(kv.Value.open, 2),
                    Math.Round(kv.Value.high, 2),
                    Math.Round(kv.Value.low, 2),
                    Math.Round(kv.Value.close, 2),
                    Math.Round(kv.Value.volume, 2)))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CoinGecko market_chart {Id} falhou", coingeckoId);
            return Array.Empty<OhlcvPoint>();
        }
    }

    public async Task<GlobalMarketSnapshot?> GetGlobalAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await api.GetGlobalAsync(ct);
            var btcDom = r.Data?.Market_cap_percentage?.GetValueOrDefault("btc") ?? 0m;
            var totalUsd = r.Data?.Total_market_cap?.GetValueOrDefault("usd") ?? 0m;
            return new GlobalMarketSnapshot(Math.Round(btcDom, 2), Math.Round(totalUsd, 2));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CoinGecko /global falhou");
            return null;
        }
    }
}
