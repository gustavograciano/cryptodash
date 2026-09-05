using CryptoDash.Application.Services;
using Xunit;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Invariante 2 — o motor de DCA distribui exatamente o aporte: nem um centavo a mais,
/// nem a menos. É a única regra do sistema que decide para onde vai dinheiro novo.
///
/// <see cref="DcaAllocator"/> é uma função pura, então estes testes não tocam no banco.
/// </summary>
public class DcaAllocatorInvariantTests
{
    /// <summary>Alvos reais do DbSeeder: BTC 55 · PAXG 22 · SOL 15 · AAVE 8.</summary>
    private static IReadOnlyList<DcaInput> ComTargetsSeedados(params decimal[] valoresAtuais) => new[]
    {
        new DcaInput(1, "BTC",  valoresAtuais.ElementAtOrDefault(0), 55m),
        new DcaInput(2, "PAXG", valoresAtuais.ElementAtOrDefault(1), 22m),
        new DcaInput(3, "SOL",  valoresAtuais.ElementAtOrDefault(2), 15m),
        new DcaInput(4, "AAVE", valoresAtuais.ElementAtOrDefault(3), 8m),
    };

    [Fact]
    public void Invariante2_CarteiraExistente_SomaDasAlocacoesIgualAoAporte()
    {
        var alloc = DcaAllocator.Allocate(ComTargetsSeedados(4_000m, 1_800m, 900m, 400m), 300m);
        Assert.Equal(300m, alloc.Sum(a => a.AmountBrl));
    }

    [Fact]
    public void Invariante2b_PrimeiroAporte_SomaDasAlocacoesIgualAoAporte()
    {
        // Ramo `totalCurrent == 0`: retorna ANTES da reconciliação de arredondamento
        // e antes do piso por moeda. É o caminho do primeiro aporte de um usuário novo.
        var alloc = DcaAllocator.Allocate(ComTargetsSeedados(), 300m);
        Assert.Equal(300m, alloc.Sum(a => a.AmountBrl));
    }

    [Theory]
    [InlineData(0.05)]   // aporte minúsculo: cada arredondamento pesa
    [InlineData(7.77)]
    [InlineData(33.33)]
    [InlineData(100.01)]
    public void Invariante2c_PrimeiroAporte_ComTresAlvosIguais_SomaBate(decimal aporte)
    {
        var inputs = new[]
        {
            new DcaInput(1, "A", 0m, 33.33m),
            new DcaInput(2, "B", 0m, 33.33m),
            new DcaInput(3, "C", 0m, 33.34m),
        };

        var alloc = DcaAllocator.Allocate(inputs, aporte);
        Assert.Equal(aporte, alloc.Sum(a => a.AmountBrl));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2026)]
    [InlineData(31337)]
    public void Invariante2d_SomaBateParaCombinacoesAleatorias(int seed)
    {
        var rng = new Random(seed);
        var falhas = new List<string>();

        for (var caso = 0; caso < 500; caso++)
        {
            var n = rng.Next(2, 7);

            // Alvos aleatórios normalizados para somar exatamente 100, como
            // UpdateAllocationAsync exige.
            var brutos = Enumerable.Range(0, n).Select(_ => (decimal)rng.Next(1, 60)).ToArray();
            var soma = brutos.Sum();
            var alvos = brutos.Select(b => Math.Round(b / soma * 100m, 2)).ToArray();
            alvos[0] += 100m - alvos.Sum();

            var inputs = Enumerable.Range(0, n)
                .Select(i => new DcaInput(i + 1, $"C{i}",
                    rng.Next(4) == 0 ? 0m : Math.Round((decimal)(rng.NextDouble() * 20_000), 2),
                    alvos[i]))
                .ToList();

            var aporte = Math.Round((decimal)(rng.NextDouble() * 2_000 + 1), 2);

            var total = DcaAllocator.Allocate(inputs, aporte).Sum(a => a.AmountBrl);
            if (total != aporte)
                falhas.Add($"aporte {aporte:F2} → distribuído {total:F2} (dif {total - aporte:+0.00;-0.00}) " +
                           $"| {n} moedas | atuais [{string.Join(", ", inputs.Select(i => i.CurrentValueBrl.ToString("F0")))}]");
        }

        Assert.True(falhas.Count == 0,
            $"{falhas.Count}/500 combinações não distribuíram o aporte exato:\n  " +
            string.Join("\n  ", falhas.Take(8)));
    }

    [Fact]
    public void Invariante2e_NenhumaAlocacaoEhNegativa()
    {
        // O ajuste final de centavos soma a diferença na maior alocação; o piso por moeda
        // move valores entre índices. Nenhum dos dois pode produzir valor negativo.
        var rng = new Random(99);
        for (var caso = 0; caso < 500; caso++)
        {
            var inputs = Enumerable.Range(0, rng.Next(2, 6))
                .Select(i => new DcaInput(i + 1, $"C{i}",
                    Math.Round((decimal)(rng.NextDouble() * 50_000), 2),
                    Math.Round((decimal)(rng.NextDouble() * 40 + 5), 2)))
                .ToList();
            var normal = inputs.Sum(i => i.TargetPct);
            inputs = inputs.Select(i => i with { TargetPct = Math.Round(i.TargetPct / normal * 100m, 2) }).ToList();

            var aporte = Math.Round((decimal)(rng.NextDouble() * 1_000 + 1), 2);

            foreach (var a in DcaAllocator.Allocate(inputs, aporte))
                Assert.True(a.AmountBrl >= 0m, $"alocação negativa para {a.Symbol}: {a.AmountBrl}");
        }
    }

    [Fact]
    public void Invariante2f_MoedaComAlvoPositivo_NaoDesapareceDaSugestaoSemMotivo()
    {
        // Uma carteira zerada com 4 alvos deve gerar 4 sugestões: se uma moeda some,
        // o usuário nunca aporta nela e o desvio dela cresce para sempre.
        var alloc = DcaAllocator.Allocate(ComTargetsSeedados(), 300m);
        Assert.Equal(4, alloc.Count(a => a.AmountBrl > 0m));
    }
}
