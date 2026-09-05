using CryptoDash.Application.Abstractions;
using CryptoDash.Application.Common;
using CryptoDash.Application.DTOs;
using CryptoDash.Domain.Entities;
using CryptoDash.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CryptoDash.Application.Services;

public class PortfolioService(IAppDbContext db)
{
    public async Task<PortfolioSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var holdings = await db.Holdings
            .Include(h => h.Coin).ThenInclude(c => c.PriceSnapshot)
            .Include(h => h.Coin).ThenInclude(c => c.TargetAllocation)
            // != 0, não > 0: posição NEGATIVA é o sinal de razão corrompido, e filtrá-la
            // fazia justamente a evidência do problema sumir do dashboard.
            .Where(h => h.Quantity != 0)
            .ToListAsync(ct);

        var totalBrl = holdings.Sum(h => h.Quantity * (h.Coin.PriceSnapshot?.PriceBrl ?? 0m));
        var totalCost = holdings.Sum(h => h.Quantity * h.AvgCostBrl);
        var totalPnl = totalBrl - totalCost;
        var totalPnlPct = totalCost == 0 ? 0 : Math.Round((totalPnl / totalCost) * 100m, 2);

        // Variação de 24h reconstruída a partir do preço de ONTEM, posição a posição:
        //   P_ontem = P_hoje / (1 + c/100)   e   variação = qty * (P_hoje − P_ontem)
        //
        // O cálculo anterior aplicava o percentual sobre o valor de HOJE (qty * P_hoje * c),
        // usando a base errada. O erro cresce com c: superestimava o ganho em 35% numa alta
        // de 35%, escondia R$ 8.000 de prejuízo numa queda de 40%, exibia 0,00% numa alta de
        // 100% (prevTotal virava exatamente zero e caía na guarda) e invertia o sinal acima
        // disso (prevTotal negativo).
        //
        // Posições sem preço, ou com variação <= −100%, não têm preço anterior definido:
        // ficam fora dos DOIS lados da conta, para não contaminar nem a variação nem a base.
        decimal valorAtualComparavel = 0m;
        decimal valorOntem = 0m;
        foreach (var h in holdings)
        {
            var price = h.Coin.PriceSnapshot?.PriceBrl ?? 0m;
            var change = h.Coin.PriceSnapshot?.Change24hPct ?? 0m;
            var fator = 1m + change / 100m;
            if (price <= 0m || fator <= 0m) continue;

            valorAtualComparavel += h.Quantity * price;
            valorOntem += h.Quantity * (price / fator);
        }

        var change24hBrl = valorAtualComparavel - valorOntem;
        var change24hPct = valorOntem == 0m ? 0m : Math.Round((change24hBrl / valorOntem) * 100m, 2);

        var holdingDtos = holdings.Select(h =>
        {
            var price = h.Coin.PriceSnapshot?.PriceBrl ?? 0m;
            var value = h.Quantity * price;
            var cost = h.Quantity * h.AvgCostBrl;
            var pnl = value - cost;
            var pnlPct = cost == 0 ? 0 : Math.Round((pnl / cost) * 100m, 2);
            return new HoldingDto(
                h.CoinId, h.Coin.Symbol, h.Coin.Name,
                h.Quantity, h.AvgCostBrl, price, value,
                h.Coin.PriceSnapshot?.Change24hPct ?? 0m,
                pnl, pnlPct,
                h.Coin.PriceSnapshot?.UpdatedAt);
        }).ToList();

        var allocation = holdingDtos.Select(h =>
        {
            var coin = holdings.First(x => x.CoinId == h.CoinId).Coin;
            var currentPct = totalBrl == 0 ? 0 : Math.Round((h.ValueBrl / totalBrl) * 100m, 2);
            var targetPct = coin.TargetAllocation?.TargetPct ?? 0m;
            var deviationPct = targetPct == 0 ? 0 : Math.Round(((currentPct - targetPct) / targetPct) * 100m, 2);
            var sev = Math.Abs(deviationPct) switch
            {
                < 10 => "ok",
                < 25 => "warn",
                _ => "alert"
            };
            return new AllocationItemDto(h.CoinId, h.Symbol, currentPct, targetPct, deviationPct, sev);
        }).ToList();

        return new PortfolioSummaryDto(
            Math.Round(totalBrl, 2),
            Math.Round(totalCost, 2),
            Math.Round(totalPnl, 2),
            totalPnlPct,
            Math.Round(change24hBrl, 2),
            change24hPct,
            holdingDtos,
            allocation);
    }

    public async Task<IReadOnlyList<HoldingDto>> GetHoldingsAsync(CancellationToken ct = default)
    {
        var summary = await GetSummaryAsync(ct);
        return summary.Holdings;
    }

    public async Task<IReadOnlyList<AllocationTargetDto>> GetAllocationAsync(CancellationToken ct = default)
    {
        return await db.TargetAllocations
            .Include(t => t.Coin)
            .OrderBy(t => t.Coin.Symbol)
            .Select(t => new AllocationTargetDto(t.CoinId, t.Coin.Symbol, t.TargetPct))
            .ToListAsync(ct);
    }

    public async Task<Result<bool>> UpdateAllocationAsync(UpdateAllocationRequest req, CancellationToken ct = default)
    {
        var sum = req.Targets.Sum(t => t.TargetPct);
        if (Math.Abs(sum - 100m) > 0.01m)
            return Result<bool>.Fail($"Soma dos targets deve ser 100%, recebido {sum}%");

        var existing = await db.TargetAllocations.ToListAsync(ct);
        foreach (var t in req.Targets)
        {
            var current = existing.FirstOrDefault(x => x.CoinId == t.CoinId);
            if (current is null)
            {
                db.TargetAllocations.Add(new TargetAllocation
                {
                    CoinId = t.CoinId,
                    TargetPct = t.TargetPct,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                current.TargetPct = t.TargetPct;
                current.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<TransactionDto>> CreateTransactionAsync(CreateTransactionRequest req, CancellationToken ct = default)
    {
        var coin = await db.Coins.FirstOrDefaultAsync(c => c.Id == req.CoinId, ct);
        if (coin is null) return Result<TransactionDto>.Fail("Moeda não encontrada");
        if (req.Quantity <= 0) return Result<TransactionDto>.Fail("Quantidade deve ser positiva");
        if (req.PriceBrl <= 0) return Result<TransactionDto>.Fail("Preço deve ser positivo");

        var total = req.Quantity * req.PriceBrl;
        var fee = req.FeeBrl ?? 0m;

        var tx = new Transaction
        {
            CoinId = req.CoinId,
            Type = req.Type,
            Quantity = req.Quantity,
            PriceBrl = req.PriceBrl,
            TotalBrl = total,
            FeeBrl = fee,
            ExecutedAt = req.ExecutedAt,
            Notes = req.Notes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Alienação sem lastro é rejeitada, nunca truncada em silêncio. A simulação
        // considera o razão inteiro, e não apenas a posição atual, porque um lançamento
        // retroativo pode zerar uma posição intermediária sem alterar o saldo final.
        if (req.Type == TransactionType.Sell && await HoldingRecalculator.WouldGoNegativeAsync(db, tx, ct))
        {
            var posicaoAtual = await db.Holdings
                .Where(h => h.CoinId == req.CoinId)
                .Select(h => (decimal?)h.Quantity)
                .FirstOrDefaultAsync(ct) ?? 0m;

            return Result<TransactionDto>.Fail(
                $"Venda de {req.Quantity} {coin.Symbol} excede a posição disponível em " +
                $"{req.ExecutedAt:yyyy-MM-dd HH:mm} (posição atual: {posicaoAtual}).");
        }

        // Uma fronteira só: sem ela, o INSERT ficava commitado e o recálculo da posição podia
        // não acontecer — o usuário via a compra listada e o patrimônio não mudava.
        await ComRetryDeConcorrenciaAsync(async token =>
        {
            db.Transactions.Add(tx);
            await db.SaveChangesAsync(token);          // materializa o Id para o replay
            await HoldingRecalculator.ApplyAsync(db, req.CoinId, token);
            return true;
        }, ct);

        return Result<TransactionDto>.Ok(new TransactionDto(
            tx.Id, tx.CoinId, coin.Symbol, tx.Type,
            tx.Quantity, tx.PriceBrl, tx.TotalBrl, tx.FeeBrl,
            tx.ExecutedAt, tx.Notes));
    }

    public async Task<IReadOnlyList<TransactionDto>> ListTransactionsAsync(int? coinId, int skip, int take, CancellationToken ct = default)
    {
        var q = db.Transactions.Include(t => t.Coin).AsQueryable();
        if (coinId.HasValue) q = q.Where(t => t.CoinId == coinId.Value);
        var rows = await q.ToListAsync(ct);
        return rows
            .OrderByDescending(t => t.ExecutedAt)
            .Skip(skip).Take(Math.Clamp(take, 1, 200))
            .Select(t => new TransactionDto(
                t.Id, t.CoinId, t.Coin.Symbol, t.Type,
                t.Quantity, t.PriceBrl, t.TotalBrl, t.FeeBrl,
                t.ExecutedAt, t.Notes))
            .ToList();
    }

    public async Task<Result<bool>> DeleteTransactionAsync(int id, CancellationToken ct = default)
    {
        var tx = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tx is null) return Result<bool>.Fail("Transação não encontrada");
        var coinId = tx.CoinId;
        await ComRetryDeConcorrenciaAsync(async token =>
        {
            db.Transactions.Remove(tx);
            await db.SaveChangesAsync(token);
            await HoldingRecalculator.ApplyAsync(db, coinId, token);
            return true;
        }, ct);
        return Result<bool>.Ok(true);
    }

    /// <summary>
    /// Executa a escrita numa transação e reprocessa quando outra requisição alterou a
    /// posição no meio do caminho.
    ///
    /// O recálculo é read-modify-write do razão inteiro: sem token de concorrência, a última
    /// escrita vencia com dado obsoleto e a posição divergia do razão em silêncio. Com o
    /// token, o conflito vira exceção — e reprocessar é a resposta certa, porque o replay é
    /// determinístico: refazer sobre o estado novo dá o resultado correto.
    /// </summary>
    private async Task ComRetryDeConcorrenciaAsync(Func<CancellationToken, Task<bool>> trabalho, CancellationToken ct)
    {
        const int maxTentativas = 3;
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await DbTransactionScope.ExecuteAsync(db, trabalho, ct);
                return;
            }
            catch (DbUpdateConcurrencyException) when (tentativa < maxTentativas)
            {
                // ExecuteAsync já fez rollback e limpou o tracker; a próxima tentativa relê
                // do zero.
                DbTransactionScope.ClearTracking(db);
            }
        }
    }
}
