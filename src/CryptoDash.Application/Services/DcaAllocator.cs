namespace CryptoDash.Application.Services;

public record DcaInput(int CoinId, string Symbol, decimal CurrentValueBrl, decimal TargetPct);
public record DcaOutput(int CoinId, string Symbol, decimal AmountBrl, decimal CurrentValueBrl, decimal TargetValueBrl, decimal GapBrl);

public static class DcaAllocator
{
    public static IReadOnlyList<DcaOutput> Allocate(IReadOnlyList<DcaInput> inputs, decimal aporteBrl, decimal minPerCoin = 5m)
    {
        if (aporteBrl <= 0 || inputs.Count == 0) return Array.Empty<DcaOutput>();

        var totalCurrent = inputs.Sum(i => i.CurrentValueBrl);
        var newTotal = totalCurrent + aporteBrl;

        var rows = inputs.Select(i =>
        {
            var targetValue = newTotal * (i.TargetPct / 100m);
            var gap = Math.Max(0, targetValue - i.CurrentValueBrl);
            return (i.CoinId, i.Symbol, i.CurrentValueBrl, TargetValue: targetValue, Gap: gap, i.TargetPct);
        }).ToList();

        var totalGap = rows.Sum(r => r.Gap);

        if (totalCurrent == 0)
        {
            // primeiro aporte: distribui proporcional ao target
            return rows.Select(r => new DcaOutput(
                r.CoinId, r.Symbol,
                Math.Round(aporteBrl * (r.TargetPct / 100m), 2),
                r.CurrentValueBrl, r.TargetValue, r.Gap)).ToList();
        }

        decimal[] alloc;
        if (totalGap >= aporteBrl && totalGap > 0)
        {
            alloc = rows.Select(r => Math.Round(aporteBrl * (r.Gap / totalGap), 2)).ToArray();
        }
        else
        {
            var resto = aporteBrl - totalGap;
            alloc = rows.Select(r => Math.Round(r.Gap + resto * (r.TargetPct / 100m), 2)).ToArray();
        }

        // ajusta para mínimo per-coin: se < minPerCoin redistribui pro maior alvo
        for (int i = 0; i < alloc.Length; i++)
        {
            if (alloc[i] > 0 && alloc[i] < minPerCoin)
            {
                var biggest = Array.IndexOf(alloc, alloc.Max());
                if (biggest != i)
                {
                    alloc[biggest] += alloc[i];
                    alloc[i] = 0m;
                }
            }
        }

        // ajusta diferença de arredondamento na maior alocação
        var diff = Math.Round(aporteBrl - alloc.Sum(), 2);
        if (Math.Abs(diff) >= 0.01m)
        {
            var idx = Array.IndexOf(alloc, alloc.Max());
            alloc[idx] = Math.Round(alloc[idx] + diff, 2);
        }

        return rows.Select((r, i) => new DcaOutput(r.CoinId, r.Symbol, alloc[i], r.CurrentValueBrl, r.TargetValue, r.Gap)).ToList();
    }
}
