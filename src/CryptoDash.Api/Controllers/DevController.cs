using CryptoDash.Application.Abstractions;
using CryptoDash.Application.Services;
using CryptoDash.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;

namespace CryptoDash.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dev")]
public class DevController(ISchedulerFactory schedulerFactory, NewsService news, IAppDbContext db) : ControllerBase
{
    [HttpPost("seed-news")]
    public async Task<IActionResult> SeedNews(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var items = new[]
        {
            new NewsCache { ExternalId = "demo-1", Title = "Bitcoin atinge nova maxima em meio a rally institucional", Url = "https://example.com/btc-ath", Source = "CoinDesk", PublishedAt = now.AddHours(-1), CoinsMentioned = new() { "BTC" }, FetchedAt = now },
            new NewsCache { ExternalId = "demo-2", Title = "Solana lanca nova versao do Firedancer; latencia cai 40%", Url = "https://example.com/sol-firedancer", Source = "The Block", PublishedAt = now.AddHours(-3), CoinsMentioned = new() { "SOL" }, FetchedAt = now },
            new NewsCache { ExternalId = "demo-3", Title = "Aave v4 entra em testnet com cross-chain liquidity", Url = "https://example.com/aave-v4", Source = "Decrypt", PublishedAt = now.AddHours(-6), CoinsMentioned = new() { "AAVE" }, FetchedAt = now },
            new NewsCache { ExternalId = "demo-4", Title = "PAX Gold supera 1 milhao de oz em backing fisico", Url = "https://example.com/paxg-supply", Source = "CoinTelegraph", PublishedAt = now.AddHours(-12), CoinsMentioned = new() { "PAXG" }, FetchedAt = now },
        };
        foreach (var i in items)
        {
            if (!db.NewsCache.Any(n => n.ExternalId == i.ExternalId))
                db.NewsCache.Add(i);
        }
        await db.SaveChangesAsync(ct);
        return Ok(new { seeded = items.Length });
    }


    [HttpPost("run-job/{jobName}")]
    public async Task<IActionResult> RunJob(string jobName, CancellationToken ct)
    {
        var allowed = new[] { "PriceSnapshotJob", "PriceHistoryJob", "MarketIndicatorsJob", "NewsFetchJob", "AIDigestJob" };
        if (!allowed.Contains(jobName)) return BadRequest(new { error = "Job inválido" });

        var scheduler = await schedulerFactory.GetScheduler(ct);
        await scheduler.TriggerJob(new JobKey(jobName), ct);
        return Accepted(new { triggered = jobName });
    }

    [HttpPost("generate-digest")]
    public async Task<IActionResult> GenerateDigest(CancellationToken ct)
    {
        var d = await news.GenerateForTodayAsync(ct);
        return d is null ? Ok(new { generated = false, reason = "Sem notícias ou LLM indisponível" })
                         : Ok(new { generated = true, date = d.Date, tokens = d.TokensUsed });
    }
}
