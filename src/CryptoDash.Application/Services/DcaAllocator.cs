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

        decimal[] alloc;
        if (totalCurrent == 0)
        {
            // Primeiro aporte: não há gap a fechar, então distribui proporcional ao alvo.
            // Este ramo RETORNAVA aqui, pulando a reconciliação de centavos e o piso por
            // moeda — e a soma não fechava: R$ 1.825,36 saía distribuído como R$ 1.825,37.
            alloc = rows.Select(r => Math.Round(aporteBrl * (r.TargetPct / 100m), 2)).ToArray();
        }
        else if (totalGap >= aporteBrl && totalGap > 0)
        {
            alloc = rows.Select(r => Math.Round(aporteBrl * (r.Gap / totalGap), 2)).ToArray();
        }
        else
        {
            var resto = aporteBrl - totalGap;
            alloc = rows.Select(r => Math.Round(r.Gap + resto * (r.TargetPct / 100m), 2)).ToArray();
        }

        AplicarPisoPorMoeda(alloc, minPerCoin);
        ReconciliarCentavos(alloc, aporteBrl);

        return rows.Select((r, i) => new DcaOutput(r.CoinId, r.Symbol, alloc[i], r.CurrentValueBrl, r.TargetValue, r.Gap)).ToList();
    }

    /// <summary>
    /// Junta alocações pequenas demais na maior, para não sugerir compra de trocados.
    /// Só move quando existe destino maior — se todas estiverem abaixo do piso, o valor fica
    /// onde está em vez de sumir.
    /// </summary>
    private static void AplicarPisoPorMoeda(decimal[] alloc, decimal minPerCoin)
    {
        for (var i = 0; i < alloc.Length; i++)
        {
            if (alloc[i] <= 0m || alloc[i] >= minPerCoin) continue;

            var maior = Array.IndexOf(alloc, alloc.Max());
            if (maior == i) continue;

            alloc[maior] += alloc[i];
            alloc[i] = 0m;
        }
    }

    /// <summary>
    /// Faz a soma fechar EXATAMENTE com o aporte. Sem isto o usuário aporta um valor e o
    /// sistema registra outro, e a diferença — de centavos por vez — acumula sem rastro.
    ///
    /// A sobra vai para a maior alocação positiva, e nunca para uma linha zerada pelo piso:
    /// devolver valor a uma moeda que acabou de ser esvaziada desfaria o próprio piso.
    /// </summary>
    private static void ReconciliarCentavos(decimal[] alloc, decimal aporteBrl)
    {
        var diff = Math.Round(aporteBrl - alloc.Sum(), 2);
        if (diff == 0m) return;

        var idx = -1;
        for (var i = 0; i < alloc.Length; i++)
            if (alloc[i] > 0m && (idx < 0 || alloc[i] > alloc[idx])) idx = i;

        // Todas zeradas (aporte minúsculo somado a arredondamento para baixo): a sobra vai
        // para a primeira linha, senão o aporte inteiro desapareceria.
        if (idx < 0) idx = 0;

        alloc[idx] = Math.Round(alloc[idx] + diff, 2);
    }
}
