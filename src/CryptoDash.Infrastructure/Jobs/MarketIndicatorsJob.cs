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

        // Sem nenhum dos dois feeds não há indicador: a AUSÊNCIA da linha é informação. Gravar
        // "Fear & Greed 50 / Neutral" e "BTC Dominance 0,00%" fazia ficção aparecer no painel
        // como leitura de mercado, indistinguível de dado real.
        if (fear is null && global is null)
        {
            logger.LogWarning(
                "MarketIndicatorsJob: nenhum feed respondeu; nada gravado (linha ausente em vez de valor inventado)");
            return;
        }

        var date = fear?.Date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var existing = await db.MarketIndicators.FirstOrDefaultAsync(m => m.Date == date, ct);
        if (existing is null)
        {
            // Só cria a linha se houver Fear & Greed: a data é dele, e sem ele a chave da linha
            // seria um palpite.
            if (fear is null)
            {
                logger.LogWarning(
                    "MarketIndicatorsJob: Fear & Greed indisponível e nao há linha para {Date}; " +
                    "dominancia nao gravada para nao criar linha com metade inventada", date);
                return;
            }

            db.MarketIndicators.Add(new MarketIndicator
            {
                Date = date,
                FearGreedIndex = fear.Value,
                FearGreedLabel = fear.Label,
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
