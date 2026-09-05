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
        var rejeitados = 0;

        foreach (var coin in coins)
        {
            var quote = quotes.FirstOrDefault(q => q.CoingeckoId == coin.CoingeckoId);
            if (quote is null) continue;

            var snap = existing.FirstOrDefault(s => s.CoinId == coin.Id);

            // Sanidade do dado ANTES de tocar no banco. Sem isto, um campo ausente no JSON da
            // CoinGecko vira `?? 0m` e sobrescreve um preço bom com R$ 0,00 — e o valor da
            // carteira aparece como zero. Um preço 10x errado é pior: vira quantidade 10x
            // errada no aporte, permanentemente.
            if (!PrecoEhPlausivel(quote.PriceBrl, snap?.PriceBrl, out var motivo))
            {
                rejeitados++;
                logger.LogWarning(
                    "PriceSnapshotJob: cotação rejeitada para {Symbol} ({Motivo}). " +
                    "Anterior={Anterior} Recebido={Recebido}. Snapshot preservado.",
                    coin.Symbol, motivo, snap?.PriceBrl, quote.PriceBrl);
                continue;   // UpdatedAt NÃO é tocado: dado velho não pode parecer fresco
            }

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
        logger.LogInformation(
            "PriceSnapshotJob: atualizou {Count} moedas ({Rejeitadas} rejeitadas por sanidade)",
            quotes.Count - rejeitados, rejeitados);
    }

    /// <summary>
    /// Banda de variação aceita entre dois snapshots consecutivos (15 min de intervalo).
    /// 40% é evento extremo em 15 minutos, não rotina — apertar demais geraria falso
    /// positivo em cripto e congelaria o preço justamente num dia de volatilidade real.
    /// </summary>
    internal const decimal BandaMaxima = 0.40m;

    internal static bool PrecoEhPlausivel(decimal recebido, decimal? anterior, out string motivo)
    {
        if (recebido <= 0m)
        {
            // Campo ausente ou nulo no JSON chega aqui como 0 por causa do `?? 0m` do feed.
            motivo = "preço menor ou igual a zero";
            return false;
        }

        // Primeira cotação da moeda: não há base de comparação, só o teste de positividade.
        if (anterior is not { } anteriorValido || anteriorValido <= 0m)
        {
            motivo = string.Empty;
            return true;
        }

        var variacao = Math.Abs(recebido / anteriorValido - 1m);
        if (variacao > BandaMaxima)
        {
            motivo = $"variação de {variacao:P1} excede a banda de {BandaMaxima:P0}";
            return false;
        }

        motivo = string.Empty;
        return true;
    }
}
