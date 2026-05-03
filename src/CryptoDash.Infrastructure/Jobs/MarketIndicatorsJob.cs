using CryptoDash.Application.Abstractions;
using CryptoDash.Domain.Entities;
using CryptoDash.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CryptoDash.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class MarketIndicatorsJob(
    AppDbContext db,
    IFearGreedFeed fng,
    IMarketDataFeed market,
    ILogger<MarketIndicatorsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var fear = await fng.GetCurrentAsync(ct);
        var global = await market.GetGlobalAsync(ct);

        var date = fear?.Date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var existing = await db.MarketIndicators.FirstOrDefaultAsync(m => m.Date == date, ct);
        if (existing is null)
        {
            db.MarketIndicators.Add(new MarketIndicator
            {
                Date = date,
                FearGreedIndex = fear?.Value ?? 50,
                FearGreedLabel = fear?.Label ?? "Neutral",
                BtcDominancePct = global?.BtcDominancePct ?? 0,
                TotalMarketCapUsd = global?.TotalMarketCapUsd ?? 0
            });
        }
        else
        {
            if (fear is not null)
            {
                existing.FearGreedIndex = fear.Value;
                existing.FearGreedLabel = fear.Label;
            }
            if (global is not null)
            {
                existing.BtcDominancePct = global.BtcDominancePct;
                existing.TotalMarketCapUsd = global.TotalMarketCapUsd;
            }
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("MarketIndicatorsJob: salvo {Date} (FNG={Value})", date, fear?.Value);
    }
}
