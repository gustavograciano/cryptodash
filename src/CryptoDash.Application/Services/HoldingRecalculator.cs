using CryptoDash.Application.Abstractions;
using CryptoDash.Domain.Entities;
using CryptoDash.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CryptoDash.Application.Services;

/// <summary>Resultado do replay do razão de uma moeda.</summary>
/// <param name="Quantity">Posição líquida: Σ compras − Σ vendas. Pode ser negativa se o
/// razão já contiver dados corrompidos — nesse caso a divergência fica visível em vez de
/// ser silenciosamente absorvida.</param>
/// <param name="TotalCostBrl">Custo total da posição remanescente, sem arredondamento.</param>
/// <param name="AvgCostBrl">Custo médio unitário, arredondado para persistência.</param>
/// <param name="PositionWentNegative">Verdadeiro se em ALGUM ponto do replay a posição
/// ficou negativa — ou seja, houve alienação sem lastro.</param>
public readonly record struct HoldingState(
    decimal Quantity,
    decimal TotalCostBrl,
    decimal AvgCostBrl,
    bool PositionWentNegative);

/// <summary>
/// Reconstrói a posição de uma moeda a partir do razão de transações.
///
/// Fonte única da verdade para o cálculo de custo médio. Antes existiam duas cópias
/// literais deste código — em PortfolioService e em AportsService — sem nenhum teste
/// garantindo que concordassem.
/// </summary>
public static class HoldingRecalculator
{
    /// <summary>
    /// Ordem canônica do razão. Precisa ser total e determinística: sem ela, duas
    /// execuções do mesmo replay podem produzir posições diferentes.
    ///
    /// 1. <c>ExecutedAt</c> — ordem econômica dos fatos.
    /// 2. Compras antes de vendas no MESMO instante. Não se pode alienar o que ainda não
    ///    foi adquirido; processar a aquisição primeiro é a única leitura coerente de dois
    ///    eventos carimbados no mesmo momento. É isto que torna o resultado independente
    ///    da ordem em que as transações foram digitadas.
    /// 3. <c>Id</c> — desempate final estável, para o caso de duas operações do mesmo tipo
    ///    no mesmo instante. Sem ele o resultado dependeria da ordem em que o banco
    ///    devolve as linhas, que não é garantida sem ORDER BY.
    /// </summary>
    public static IEnumerable<Transaction> InLedgerOrder(IEnumerable<Transaction> transactions) =>
        transactions
            .OrderBy(t => t.ExecutedAt)
            .ThenBy(t => t.Type == TransactionType.Buy ? 0 : 1)
            .ThenBy(t => t.Id);

    /// <summary>
    /// Replay puro do razão. Sem I/O, sem estado — a mesma entrada sempre produz a mesma saída.
    /// </summary>
    public static HoldingState Replay(IEnumerable<Transaction> transactions)
    {
        decimal qty = 0m;
        decimal totalCost = 0m;
        var wentNegative = false;

        foreach (var t in InLedgerOrder(transactions))
        {
            if (t.Type == TransactionType.Buy)
            {
                // A taxa de compra capitaliza na base de custo.
                totalCost += t.TotalBrl + t.FeeBrl;
                qty += t.Quantity;
                continue;
            }

            // Venda: baixa o custo proporcional à parcela que havia lastro para fechar.
            if (qty > 0m)
            {
                var avg = totalCost / qty;
                var fechavel = Math.Min(t.Quantity, qty);
                totalCost -= avg * fechavel;
            }

            // A quantidade INTEIRA da venda é subtraída, mesmo que exceda a posição.
            // Truncar aqui (o comportamento anterior) fazia o razão e a posição contarem
            // histórias diferentes, sempre inflando a posição registrada.
            qty -= t.Quantity;

            if (qty <= 0m)
            {
                // Posição fechada (ou negativa): não há base de custo remanescente.
                totalCost = 0m;
                if (qty < 0m) wentNegative = true;
            }
        }

        var avgCost = qty > 0m ? Math.Round(totalCost / qty, 2) : 0m;
        return new HoldingState(qty, totalCost, avgCost, wentNegative);
    }

    /// <summary>
    /// Recalcula e aplica a posição da moeda no contexto. NÃO chama SaveChanges:
    /// quem orquestra decide a fronteira transacional.
    /// </summary>
    public static async Task ApplyAsync(IAppDbContext db, int coinId, CancellationToken ct = default)
    {
        var transactions = await db.Transactions
            .Where(t => t.CoinId == coinId)
            .ToListAsync(ct);

        var state = Replay(transactions);

        var holding = await db.Holdings.FirstOrDefaultAsync(h => h.CoinId == coinId, ct);
        if (holding is null)
        {
            db.Holdings.Add(new Holding
            {
                CoinId = coinId,
                Quantity = state.Quantity,
                AvgCostBrl = state.AvgCostBrl,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            holding.Quantity = state.Quantity;
            holding.AvgCostBrl = state.AvgCostBrl;
            holding.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Simula a inclusão de uma transação e informa se ela deixaria a posição negativa
    /// em algum ponto do razão — inclusive em lançamentos retroativos, que podem tornar
    /// negativa uma posição intermediária sem afetar o saldo final.
    /// </summary>
    public static async Task<bool> WouldGoNegativeAsync(
        IAppDbContext db, Transaction candidata, CancellationToken ct = default)
    {
        var existentes = await db.Transactions
            .Where(t => t.CoinId == candidata.CoinId)
            .ToListAsync(ct);

        // Id máximo: a candidata ainda não foi persistida e, uma vez inserida, receberá o
        // maior Id — logo ordena por último dentro do seu grupo (ExecutedAt, tipo).
        var simulada = new Transaction
        {
            Id = int.MaxValue,
            CoinId = candidata.CoinId,
            Type = candidata.Type,
            Quantity = candidata.Quantity,
            PriceBrl = candidata.PriceBrl,
            TotalBrl = candidata.TotalBrl,
            FeeBrl = candidata.FeeBrl,
            ExecutedAt = candidata.ExecutedAt
        };

        return Replay(existentes.Append(simulada)).PositionWentNegative;
    }
}
