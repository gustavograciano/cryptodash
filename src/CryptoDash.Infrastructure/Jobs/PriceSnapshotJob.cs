using CryptoDash.Application.Abstractions;
using CryptoDash.Domain.Entities;
using CryptoDash.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CryptoDash.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class PriceSnapshotJob(AppDbContext db, IPriceFeed feed, ILogger<PriceSnapshotJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var coins = await db.Coins.Where(c => c.IsActive).ToListAsync(ct);
        if (coins.Count == 0) return;

        var ids = coins.Select(c => c.CoingeckoId).ToList();
        var quotes = await feed.GetSimplePricesAsync(ids, ct);
        if (quotes.Count == 0)
        {
            logger.LogWarning("PriceSnapshotJob: feed retornou vazio");
            return;
        }

        var existing = await db.PriceSnapshots.ToListAsync(ct);
        foreach (var coin in coins)
        {
            var quote = quotes.FirstOrDefault(q => q.CoingeckoId == coin.CoingeckoId);
            if (quote is null) continue;

            var snap = existing.FirstOrDefault(s => s.CoinId == coin.Id);
            if (snap is null)
            {
                db.PriceSnapshots.Add(new PriceSnapshot
                {
                    CoinId = coin.Id,
                    PriceBrl = quote.PriceBrl,
                    PriceUsd = quote.PriceUsd,
                    Change24hPct = quote.Change24hPct,
                    Volume24hBrl = quote.Volume24hBrl,
                    MarketCapBrl = quote.MarketCapBrl,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                snap.PriceBrl = quote.PriceBrl;
                snap.PriceUsd = quote.PriceUsd;
                snap.Change24hPct = quote.Change24hPct;
                snap.Volume24hBrl = quote.Volume24hBrl;
                snap.MarketCapBrl = quote.MarketCapBrl;
                snap.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("PriceSnapshotJob: atualizou {Count} moedas", quotes.Count);
    }
}
