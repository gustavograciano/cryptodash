using CryptoDash.Application.Abstractions;
using CryptoDash.Application.Common;
using CryptoDash.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CryptoDash.Application.Services;

public class MarketService(IAppDbContext db)
{
    public async Task<MarketOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var indicator = await db.MarketIndicators
            .OrderByDescending(m => m.Date).FirstOrDefaultAsync(ct);
        var snaps = await db.PriceSnapshots.Include(p => p.Coin).ToListAsync(ct);

        var movers = snaps.Where(s => s.Coin.IsActive).ToList();
        var top = movers.OrderByDescending(s => s.Change24hPct).Take(3)
            .Select(s => new MoverDto(s.Coin.Symbol, s.Coin.Name, s.PriceBrl, s.Change24hPct)).ToList();
        var bot = movers.OrderBy(s => s.Change24hPct).Take(3)
            .Select(s => new MoverDto(s.Coin.Symbol, s.Coin.Name, s.PriceBrl, s.Change24hPct)).ToList();

        return new MarketOverviewDto(
            indicator?.FearGreedIndex ?? 50,
            indicator?.FearGreedLabel ?? "Neutral",
            indicator?.BtcDominancePct ?? 0,
            indicator?.TotalMarketCapUsd ?? 0,
            indicator?.Date is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null,
            top, bot);
    }

    public async Task<Result<CoinSnapshotDto>> GetCoinAsync(string symbol, CancellationToken ct = default)
    {
        var coin = await db.Coins
            .Include(c => c.PriceSnapshot)
            .FirstOrDefaultAsync(c => c.Symbol == symbol.ToUpper(), ct);
        if (coin is null) return Result<CoinSnapshotDto>.Fail("Moeda não encontrada");

        var s = coin.PriceSnapshot;
        return Result<CoinSnapshotDto>.Ok(new CoinSnapshotDto(
            coin.Id, coin.Symbol, coin.Name,
            s?.PriceBrl ?? 0, s?.PriceUsd ?? 0,
            s?.Change24hPct ?? 0, s?.Volume24hBrl ?? 0, s?.MarketCapBrl ?? 0,
            s?.UpdatedAt));
    }

    public async Task<Result<IReadOnlyList<OhlcvDto>>> GetHistoryAsync(string symbol, string range, CancellationToken ct = default)
    {
        var coin = await db.Coins.FirstOrDefaultAsync(c => c.Symbol == symbol.ToUpper(), ct);
        if (coin is null) return Result<IReadOnlyList<OhlcvDto>>.Fail("Moeda não encontrada");

        var days = range switch { "30d" => 30, "90d" => 90, "1y" => 365, _ => 90 };
        var since = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-days));
        var rows = await db.PriceHistory
            .Where(p => p.CoinId == coin.Id && p.Date >= since)
            .OrderBy(p => p.Date)
            .Select(p => new OhlcvDto(p.Date, p.OpenBrl, p.HighBrl, p.LowBrl, p.CloseBrl, p.VolumeBrl))
            .ToListAsync(ct);
        return Result<IReadOnlyList<OhlcvDto>>.Ok(rows);
    }

    public async Task<Result<CoinIndicatorsDto>> GetIndicatorsAsync(string symbol, CancellationToken ct = default)
    {
        var coin = await db.Coins.FirstOrDefaultAsync(c => c.Symbol == symbol.ToUpper(), ct);
        if (coin is null) return Result<CoinIndicatorsDto>.Fail("Moeda não encontrada");

        var rows = await db.PriceHistory
            .Where(p => p.CoinId == coin.Id)
            .OrderBy(p => p.Date)
            .Select(p => new { p.Date, p.CloseBrl })
            .ToListAsync(ct);

        var closes = rows.Select(r => r.CloseBrl).ToList();
        var ma200 = IndicatorCalculator.Ma(closes, 200);
        var ma200Signal = ma200 is null || closes.Count == 0 ? "n/a" : (closes[^1] > ma200 ? "above" : "below");

        var weekly = IndicatorCalculator.WeeklyCloses(rows.Select(r => (r.Date, r.CloseBrl)).ToList());
        var rsi = IndicatorCalculator.RsiWilder(weekly, 14);
        var rsiSignal = rsi is null ? "n/a" : (rsi < 30 ? "oversold" : rsi > 70 ? "overbought" : "neutral");

        var vol = IndicatorCalculator.AnnualizedVolatility(closes, 30);
        var distMax90 = IndicatorCalculator.DistanceFromMax(closes, 90);
        var distMax365 = IndicatorCalculator.DistanceFromMax(closes, 365);
        var distMin90 = IndicatorCalculator.DistanceFromMin(closes, 90);

        return Result<CoinIndicatorsDto>.Ok(new CoinIndicatorsDto(
            coin.Symbol, ma200, ma200Signal, rsi, rsiSignal,
            vol, distMax90, distMax365, distMin90));
    }
}
