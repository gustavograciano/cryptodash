using CryptoDash.Application.Abstractions;
using CryptoDash.Application.Common;
using CryptoDash.Application.DTOs;
using CryptoDash.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoDash.Application.Services;

public class NewsService(IAppDbContext db, ILlmDigestGenerator? llm = null)
{
    public async Task<IReadOnlyList<NewsItemDto>> GetFeedAsync(string? coin, int take, CancellationToken ct = default)
    {
        var q = db.NewsCache.AsQueryable();
        if (!string.IsNullOrWhiteSpace(coin))
        {
            var sym = coin.ToUpper();
            q = q.Where(n => n.CoinsMentioned.Contains(sym));
        }
        var rows = await q.ToListAsync(ct);
        return rows.OrderByDescending(n => n.PublishedAt)
            .Take(Math.Clamp(take, 1, 100))
            .Select(n => new NewsItemDto(n.Id, n.Title, n.Url, n.Source, n.PublishedAt, n.CoinsMentioned, n.Sentiment))
            .ToList();
    }

    public async Task<Result<AiDigestDto>> GetDigestAsync(DateOnly? date, CancellationToken ct = default)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var digest = await db.AiDigests.FirstOrDefaultAsync(x => x.Date == d, ct);
        if (digest is null && !date.HasValue)
        {
            digest = await GenerateForTodayAsync(ct);
        }
        if (digest is null) return Result<AiDigestDto>.Fail("Digest não disponível");
        return Result<AiDigestDto>.Ok(new AiDigestDto(digest.Id, digest.Date, digest.SummaryMd, digest.CoinsCovered, digest.Model, digest.CreatedAt));
    }

    public async Task<AiDigest?> GenerateForTodayAsync(CancellationToken ct = default)
    {
        if (llm is null) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var existing = await db.AiDigests.FirstOrDefaultAsync(d => d.Date == today, ct);
        if (existing is not null) return existing;

        var coins = await db.Coins.Where(c => c.IsActive).Select(c => c.Symbol).ToListAsync(ct);
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var raw = await db.NewsCache.ToListAsync(ct);
        var news = raw.Where(n => n.PublishedAt >= since).OrderByDescending(n => n.PublishedAt).Take(15).ToList();
        if (news.Count == 0) return null;

        var prompts = news.Select(n => new NewsItemForPrompt(n.Title, n.Url, n.Source, n.CoinsMentioned)).ToList();
        var result = await llm.GenerateAsync(coins, prompts, ct);

        var digest = new AiDigest
        {
            Date = today,
            SummaryMd = result.Markdown,
            CoinsCovered = coins,
            NewsIdsUsed = news.Select(n => n.Id).ToList(),
            Model = result.Model,
            TokensUsed = result.TokensUsed,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AiDigests.Add(digest);
        await db.SaveChangesAsync(ct);
        return digest;
    }
}
