using CryptoDash.Application.Abstractions;
using CryptoDash.Application.Common;
using CryptoDash.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CryptoDash.Application.Services;

/// <param name="CoinId">Moeda divergente.</param>
/// <param name="Symbol">Símbolo, para quem lê o alerta.</param>
/// <param name="LedgerQuantity">Σ compras − Σ vendas, direto do razão.</param>
/// <param name="HoldingQuantity">Quantidade persistida na posição.</param>
/// <param name="Difference">Posição menos razão. Positivo = a posição afirma ter MAIS cripto
/// do que o razão comprova, que é a direção perigosa.</param>
public record ReconciliationMismatch(
    int CoinId,
    string Symbol,
    decimal LedgerQuantity,
    decimal HoldingQuantity,
    decimal Difference);

/// <param name="CheckedAt">Quando a verificação rodou.</param>
/// <param name="CoinsChecked">Quantas moedas foram comparadas.</param>
/// <param name="Mismatches">Divergências encontradas. Vazio = razão e posição concordam.</param>
public record ReconciliationReport(
    DateTimeOffset CheckedAt,
    int CoinsChecked,
    IReadOnlyList<ReconciliationMismatch> Mismatches)
{
    public bool IsHealthy => Mismatches.Count == 0;
}

/// <summary>
/// Compara o razão (<c>transactions</c>) com a posição (<c>holdings</c>).
///
/// <para>
/// Este é o multiplicador de severidade de todos os outros defeitos: cada race, crash ou
/// delete produzia erro silencioso e PERMANENTE porque nada comparava. O sistema não sabia
/// quando estava errado — e o dono do capital também não.
/// </para>
/// <para>
/// Não há reconciliação contra fonte externa: o sistema não tem integração com corretora.
/// Esta é a reconciliação interna, que é a que faz sentido no escopo atual.
/// </para>
/// </summary>
public class ReconciliationService(IAppDbContext db)
{
    public async Task<ReconciliationReport> CheckAsync(CancellationToken ct = default)
    {
        // Materializa para somar no cliente: SUM sobre decimal no SQLite devolve valor
        // errado (o provider guarda decimal como TEXT).
        var transacoes = await db.Transactions
            .Select(t => new { t.CoinId, t.Type, t.Quantity })
            .ToListAsync(ct);

        var razaoPorMoeda = transacoes
            .GroupBy(t => t.CoinId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(t => t.Type == TransactionType.Buy ? t.Quantity : -t.Quantity));

        var posicoes = await db.Holdings
            .Include(h => h.Coin)
            .ToListAsync(ct);

        var divergencias = new List<ReconciliationMismatch>();

        foreach (var h in posicoes)
        {
            var razao = razaoPorMoeda.GetValueOrDefault(h.CoinId, 0m);
            if (razao == h.Quantity) continue;

            divergencias.Add(new ReconciliationMismatch(
                h.CoinId, h.Coin.Symbol, razao, h.Quantity, h.Quantity - razao));
        }

        // Moeda com razão mas SEM linha de posição: a posição deveria existir e não existe.
        foreach (var (coinId, razao) in razaoPorMoeda)
        {
            if (razao == 0m || posicoes.Any(h => h.CoinId == coinId)) continue;

            var symbol = await db.Coins.Where(c => c.Id == coinId)
                .Select(c => c.Symbol).FirstOrDefaultAsync(ct) ?? $"#{coinId}";
            divergencias.Add(new ReconciliationMismatch(coinId, symbol, razao, 0m, -razao));
        }

        return new ReconciliationReport(
            DateTimeOffset.UtcNow,
            razaoPorMoeda.Keys.Union(posicoes.Select(h => h.CoinId)).Count(),
            divergencias.OrderBy(d => d.Symbol).ToList());
    }

    /// <summary>
    /// Recalcula posições a partir do razão. É o único reparo do sistema para toda a classe
    /// de divergência — antes disto, <c>ApplyAsync</c> só era alcançável de dentro de uma
    /// escrita, e não havia como consertar uma posição sem reeditar transações.
    /// </summary>
    public async Task<ReconciliationReport> RecalculateAsync(int? coinId = null, CancellationToken ct = default)
    {
        var moedas = coinId is { } id
            ? new List<int> { id }
            : await db.Transactions.Select(t => t.CoinId).Distinct().ToListAsync(ct);

        await DbTransactionScope.ExecuteAsync(db, async token =>
        {
            foreach (var m in moedas.OrderBy(x => x))
                await HoldingRecalculator.ApplyAsync(db, m, token);
            return true;
        }, ct);

        return await CheckAsync(ct);
    }
}
