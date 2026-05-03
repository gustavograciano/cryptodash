using CryptoDash.Application.Abstractions;
using CryptoDash.Domain.Entities;
using CryptoDash.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CryptoDash.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class PriceHistoryJob(AppDbContext db, IMarketDataFeed feed, ILogger<PriceHistoryJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var coins = await db.Coins.Where(c => c.IsActive).ToListAsync(ct);

        foreach (var coin in coins)
        {
            var lastDate = await db.PriceHistory.Where(p => p.CoinId == coin.Id)
                .OrderByDescending(p => p.Date).Select(p => (DateOnly?)p.Date).FirstOrDefaultAsync(ct);
            var days = lastDate is null ? 365 : Math.Max(2, (DateOnly.FromDateTime(DateTime.UtcNow.Date).DayNumber - lastDate.Value.DayNumber) + 2);
            days = Math.Min(days, 365);

            var points = await feed.GetMarketChartAsync(coin.CoingeckoId, days, ct);
            if (points.Count == 0) continue;

            var existing = await db.PriceHistory
                .Where(p => p.CoinId == coin.Id && p.Date >= points[0].Date)
                .ToDictionaryAsync(p => p.Date, ct);

            foreach (var p in points)
            {
                if (existing.TryGetValue(p.Date, out var row))
                {
                    row.OpenBrl = p.Open;
                    row.HighBrl = p.High;
                    row.LowBrl = p.Low;
                    row.CloseBrl = p.Close;
                    row.VolumeBrl = p.Volume;
                }
                else
                {
                    db.PriceHistory.Add(new PriceHistory
                    {
                        CoinId = coin.Id,
                        Date = p.Date,
                        OpenBrl = p.Open,
                        HighBrl = p.High,
                        LowBrl = p.Low,
                        CloseBrl = p.Close,
                        VolumeBrl = p.Volume
                    });
                }
            }
            await db.SaveChangesAsync(ct);
            logger.LogInformation("PriceHistoryJob: {Symbol} sincronizado ({Count} pontos)", coin.Symbol, points.Count);
        }

        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddYears(-2));
        var stale = await db.PriceHistory.Where(p => p.Date < cutoff).ToListAsync(ct);
        if (stale.Count > 0)
        {
            db.PriceHistory.RemoveRange(stale);
            await db.SaveChangesAsync(ct);
        }
    }
}
