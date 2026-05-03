using System.Text.Json;
using CryptoDash.Application.Abstractions;
using CryptoDash.Domain.Entities;
using CryptoDash.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CryptoDash.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public class NewsFetchJob(AppDbContext db, INewsFeed feed, ILogger<NewsFetchJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var coins = await db.Coins.Where(c => c.IsActive).Select(c => c.Symbol).ToListAsync(ct);
        var items = await feed.GetLatestAsync(coins, ct);
        if (items.Count == 0) return;

        var existingIds = await db.NewsCache
            .Where(n => items.Select(i => i.ExternalId).Contains(n.ExternalId))
            .Select(n => n.ExternalId)
            .ToListAsync(ct);

        foreach (var i in items.Where(x => !existingIds.Contains(x.ExternalId)))
        {
            db.NewsCache.Add(new NewsCache
            {
                ExternalId = i.ExternalId,
                Title = i.Title,
                Url = i.Url,
                Source = i.Source,
                PublishedAt = i.PublishedAt,
                CoinsMentioned = i.CoinsMentioned.ToList(),
                Sentiment = i.Sentiment,
                RawPayload = i.RawPayload ?? JsonSerializer.Serialize(i),
                FetchedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);

        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var stale = (await db.NewsCache.ToListAsync(ct)).Where(n => n.FetchedAt < cutoff).ToList();
        if (stale.Count > 0)
        {
            db.NewsCache.RemoveRange(stale);
            await db.SaveChangesAsync(ct);
        }
        logger.LogInformation("NewsFetchJob: {New} novas, {Stale} expiradas", items.Count - existingIds.Count, stale.Count);
    }
}
