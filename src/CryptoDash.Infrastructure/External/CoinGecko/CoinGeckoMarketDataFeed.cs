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

            var byDate = new Dictionary<DateOnly, (decimal open, decimal high, decimal low, decimal close, decimal volume, int pontos)>();
            foreach (var p in resp.Prices)
            {
                if (p.Count < 2) continue;
                var ts = DateTimeOffset.FromUnixTimeMilliseconds((long)p[0]).UtcDateTime;
                var date = DateOnly.FromDateTime(ts);
                var price = p[1];
                if (!byDate.TryGetValue(date, out var v))
                {
                    byDate[date] = (price, price, price, price, 0m, 1);
                }
                else
                {
                    byDate[date] = (v.open, Math.Max(v.high, price), Math.Min(v.low, price), price, v.volume, v.pontos + 1);
                }
            }
            // `total_volumes` da CoinGecko é volume ROLANTE de 24h, não incremental. Somar as
            // leituras do dia — o que este código fazia — multiplicava o volume por ~24. A
            // última leitura do dia é a melhor aproximação disponível do volume daquele dia.
            var ultimoVolumePorDia = new Dictionary<DateOnly, (long ts, decimal volume)>();
            foreach (var v in resp.Total_volumes)
            {
                if (v.Count < 2) continue;
                var tsMs = (long)v[0];
                var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(tsMs).UtcDateTime);
                if (!ultimoVolumePorDia.TryGetValue(date, out var atual) || tsMs > atual.ts)
                    ultimoVolumePorDia[date] = (tsMs, v[1]);
            }

            foreach (var (date, ultimo) in ultimoVolumePorDia)
            {
                if (byDate.TryGetValue(date, out var current))
                    byDate[date] = (current.open, current.high, current.low, current.close, ultimo.volume, current.pontos);
            }

            return byDate.OrderBy(kv => kv.Key)
                .Select(kv => new OhlcvPoint(kv.Key,
                    Math.Round(kv.Value.open, 10),
                    Math.Round(kv.Value.high, 10),
                    Math.Round(kv.Value.low, 10),
                    Math.Round(kv.Value.close, 10),
                    Math.Round(kv.Value.volume, 2),
                    HasIntraday: kv.Value.pontos > 1))
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
