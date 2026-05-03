namespace CryptoDash.Application.Services;

public static class IndicatorCalculator
{
    public static decimal? Ma(IReadOnlyList<decimal> closes, int period)
    {
        if (closes.Count < period) return null;
        var slice = closes.Skip(closes.Count - period).Take(period);
        return Math.Round(slice.Average(), 2);
    }

    public static decimal? RsiWilder(IReadOnlyList<decimal> closes, int period = 14)
    {
        if (closes.Count <= period) return null;
        decimal avgGain = 0m, avgLoss = 0m;
        for (int i = 1; i <= period; i++)
        {
            var diff = closes[i] - closes[i - 1];
            if (diff >= 0) avgGain += diff; else avgLoss -= diff;
        }
        avgGain /= period;
        avgLoss /= period;

        for (int i = period + 1; i < closes.Count; i++)
        {
            var diff = closes[i] - closes[i - 1];
            var gain = diff > 0 ? diff : 0m;
            var loss = diff < 0 ? -diff : 0m;
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;
        }

        if (avgLoss == 0) return 100m;
        var rs = avgGain / avgLoss;
        var rsi = 100m - 100m / (1m + rs);
        return Math.Round(rsi, 2);
    }

    public static decimal? AnnualizedVolatility(IReadOnlyList<decimal> closes, int period = 30)
    {
        if (closes.Count < period + 1) return null;
        var slice = closes.Skip(closes.Count - period - 1).ToList();
        var rets = new List<double>();
        for (int i = 1; i < slice.Count; i++)
        {
            if (slice[i - 1] == 0) continue;
            rets.Add((double)((slice[i] - slice[i - 1]) / slice[i - 1]));
        }
        if (rets.Count == 0) return null;
        var mean = rets.Average();
        var variance = rets.Sum(r => Math.Pow(r - mean, 2)) / rets.Count;
        var std = Math.Sqrt(variance);
        var ann = std * Math.Sqrt(365);
        return Math.Round((decimal)(ann * 100), 2);
    }

    public static decimal? DistanceFromMax(IReadOnlyList<decimal> closes, int period)
    {
        if (closes.Count == 0) return null;
        var window = closes.TakeLast(Math.Min(period, closes.Count)).ToList();
        var max = window.Max();
        if (max == 0) return null;
        var current = closes[^1];
        return Math.Round((max - current) / max * 100m, 2);
    }

    public static decimal? DistanceFromMin(IReadOnlyList<decimal> closes, int period)
    {
        if (closes.Count == 0) return null;
        var window = closes.TakeLast(Math.Min(period, closes.Count)).ToList();
        var min = window.Min();
        if (min == 0) return null;
        var current = closes[^1];
        return Math.Round((current - min) / min * 100m, 2);
    }

    public static IReadOnlyList<decimal> WeeklyCloses(IReadOnlyList<(DateOnly date, decimal close)> daily)
    {
        return daily
            .GroupBy(d => System.Globalization.ISOWeek.GetYear(d.date.ToDateTime(TimeOnly.MinValue)) * 100
                          + System.Globalization.ISOWeek.GetWeekOfYear(d.date.ToDateTime(TimeOnly.MinValue)))
            .OrderBy(g => g.Key)
            .Select(g => g.OrderByDescending(x => x.date).First().close)
            .ToList();
    }
}
