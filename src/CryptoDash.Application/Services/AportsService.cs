using CryptoDash.Application.Abstractions;
using CryptoDash.Application.Common;
using CryptoDash.Application.DTOs;
using CryptoDash.Domain.Entities;
using CryptoDash.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CryptoDash.Application.Services;

public class AportsService(IAppDbContext db)
{
    public async Task<AportPlanDto?> GetPlanAsync(CancellationToken ct = default)
    {
        var plan = await db.AportPlans.OrderByDescending(p => p.Id).FirstOrDefaultAsync(p => p.IsActive, ct);
        return plan is null ? null : new AportPlanDto(plan.Id, plan.AmountBrl, plan.DayOfMonth, plan.IsActive);
    }

    public async Task<AportPlanDto> UpsertPlanAsync(UpdateAportPlanRequest req, CancellationToken ct = default)
    {
        if (req.DayOfMonth < 1 || req.DayOfMonth > 28) throw new ArgumentException("dayOfMonth deve estar entre 1 e 28");
        var plan = await db.AportPlans.OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
        if (plan is null)
        {
            plan = new AportPlan { CreatedAt = DateTimeOffset.UtcNow };
            db.AportPlans.Add(plan);
        }
        plan.AmountBrl = req.AmountBrl;
        plan.DayOfMonth = req.DayOfMonth;
        plan.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return new AportPlanDto(plan.Id, plan.AmountBrl, plan.DayOfMonth, plan.IsActive);
    }

    public async Task<NextAportDto?> GetNextAsync(CancellationToken ct = default)
    {
        var plan = await db.AportPlans.OrderByDescending(p => p.Id).FirstOrDefaultAsync(p => p.IsActive, ct);
        if (plan is null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var next = new DateOnly(today.Year, today.Month, Math.Min(plan.DayOfMonth, DateTime.DaysInMonth(today.Year, today.Month)));
        if (next < today)
        {
            var m = today.AddMonths(1);
            next = new DateOnly(m.Year, m.Month, Math.Min(plan.DayOfMonth, DateTime.DaysInMonth(m.Year, m.Month)));
        }
        var daysUntil = next.DayNumber - today.DayNumber;

        var suggestions = await BuildSuggestionAsync(plan.AmountBrl, ct);
        return new NextAportDto(plan.Id, plan.AmountBrl, next, daysUntil, suggestions);
    }

    public async Task<IReadOnlyList<AportAllocationDto>> GetRebalanceSuggestionAsync(decimal? customAmount, CancellationToken ct = default)
    {
        var plan = await db.AportPlans.OrderByDescending(p => p.Id).FirstOrDefaultAsync(p => p.IsActive, ct);
        var amount = customAmount ?? plan?.AmountBrl ?? 300m;
        return await BuildSuggestionAsync(amount, ct);
    }

    private async Task<IReadOnlyList<AportAllocationDto>> BuildSuggestionAsync(decimal amount, CancellationToken ct)
    {
        var targets = await db.TargetAllocations
            .Include(t => t.Coin).ThenInclude(c => c.Holding)
            .Include(t => t.Coin).ThenInclude(c => c.PriceSnapshot)
            .Where(t => t.TargetPct > 0)
            .ToListAsync(ct);

        var inputs = targets.Select(t =>
        {
            var qty = t.Coin.Holding?.Quantity ?? 0m;
            var price = t.Coin.PriceSnapshot?.PriceBrl ?? 0m;
            return new DcaInput(t.CoinId, t.Coin.Symbol, qty * price, t.TargetPct);
        }).ToList();

        var alloc = DcaAllocator.Allocate(inputs, amount);
        return alloc.Select(a => new AportAllocationDto(a.CoinId, a.Symbol, a.AmountBrl, a.CurrentValueBrl, a.TargetValueBrl, a.GapBrl)).ToList();
    }

    public async Task<Result<ExecuteAportResponse>> ExecuteAsync(ExecuteAportRequest req, CancellationToken ct = default)
    {
        var plan = await db.AportPlans.FirstOrDefaultAsync(p => p.Id == req.PlanId, ct);
        if (plan is null) return Result<ExecuteAportResponse>.Fail("Plano não encontrado");

        var snaps = await db.PriceSnapshots.ToDictionaryAsync(s => s.CoinId, ct);
        var txIds = new List<int>();

        foreach (var a in req.Allocations.Where(x => x.AmountBrl > 0))
        {
            if (!snaps.TryGetValue(a.CoinId, out var snap) || snap.PriceBrl <= 0) continue;
            var qty = Math.Round(a.AmountBrl / snap.PriceBrl, 10);
            var tx = new Transaction
            {
                CoinId = a.CoinId,
                Type = TransactionType.Buy,
                Quantity = qty,
                PriceBrl = snap.PriceBrl,
                TotalBrl = a.AmountBrl,
                FeeBrl = 0m,
                ExecutedAt = req.ExecutedAt,
                Notes = $"Aporte automatico (plan #{plan.Id})",
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Transactions.Add(tx);
            await db.SaveChangesAsync(ct);
            txIds.Add(tx.Id);
            await RecalculateHoldingAsync(a.CoinId, ct);
        }
        await db.SaveChangesAsync(ct);

        var executed = new AportExecuted
        {
            PlanId = plan.Id,
            AmountBrl = req.AmountBrl,
            ExecutedAt = req.ExecutedAt,
            TransactionIds = txIds
        };
        db.AportsExecuted.Add(executed);
        await db.SaveChangesAsync(ct);

        return Result<ExecuteAportResponse>.Ok(new ExecuteAportResponse(executed.Id, txIds));
    }

    private async Task RecalculateHoldingAsync(int coinId, CancellationToken ct)
    {
        var txs = (await db.Transactions.Where(t => t.CoinId == coinId).ToListAsync(ct)).OrderBy(t => t.ExecutedAt).ToList();
        decimal qty = 0m;
        decimal totalCost = 0m;
        foreach (var t in txs)
        {
            if (t.Type == TransactionType.Buy)
            {
                totalCost += t.TotalBrl + t.FeeBrl;
                qty += t.Quantity;
            }
            else if (qty > 0)
            {
                var avg = totalCost / qty;
                var sellQty = Math.Min(t.Quantity, qty);
                totalCost -= avg * sellQty;
                qty -= sellQty;
            }
        }
        var avgCost = qty > 0 ? Math.Round(totalCost / qty, 2) : 0m;

        var holding = await db.Holdings.FirstOrDefaultAsync(h => h.CoinId == coinId, ct);
        if (holding is null)
        {
            db.Holdings.Add(new Holding { CoinId = coinId, Quantity = qty, AvgCostBrl = avgCost, UpdatedAt = DateTimeOffset.UtcNow });
        }
        else
        {
            holding.Quantity = qty;
            holding.AvgCostBrl = avgCost;
            holding.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
