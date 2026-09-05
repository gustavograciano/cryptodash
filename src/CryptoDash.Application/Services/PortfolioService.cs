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
            .Where(h => h.Quantity > 0)
            .ToListAsync(ct);

        var totalBrl = holdings.Sum(h => h.Quantity * (h.Coin.PriceSnapshot?.PriceBrl ?? 0m));
        var totalCost = holdings.Sum(h => h.Quantity * h.AvgCostBrl);
        var totalPnl = totalBrl - totalCost;
        var totalPnlPct = totalCost == 0 ? 0 : Math.Round((totalPnl / totalCost) * 100m, 2);

        var change24hBrl = holdings.Sum(h =>
        {
            var price = h.Coin.PriceSnapshot?.PriceBrl ?? 0m;
            var change = h.Coin.PriceSnapshot?.Change24hPct ?? 0m;
            var value = h.Quantity * price;
            return value * change / 100m;
        });
        var prevTotal = totalBrl - change24hBrl;
        var change24hPct = prevTotal == 0 ? 0 : Math.Round((change24hBrl / prevTotal) * 100m, 2);

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

        db.Transactions.Add(tx);
        await db.SaveChangesAsync(ct);

        await HoldingRecalculator.ApplyAsync(db, req.CoinId, ct);
        await db.SaveChangesAsync(ct);

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
        db.Transactions.Remove(tx);
        await db.SaveChangesAsync(ct);
        await HoldingRecalculator.ApplyAsync(db, coinId, ct);
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }
}
