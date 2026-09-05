using CryptoDash.Infrastructure.Jobs;
using Xunit;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Spec 03 — sanidade da cotação antes de persistir.
///
/// A regra existe porque o feed colapsa campo ausente em <c>?? 0m</c>: sem guarda, um `brl`
/// nulo no JSON da CoinGecko sobrescreve um preço bom com R$ 0,00 e o patrimônio aparece
/// zerado. Um preço 10× errado é pior — vira quantidade 10× errada no aporte, para sempre.
/// </summary>
public class PriceSanityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.00001)]
    public void PrecoNaoPositivo_EhRejeitado(decimal recebido)
    {
        Assert.False(PriceSnapshotJob.PrecoEhPlausivel(recebido, 600_000m, out var motivo));
        Assert.Contains("zero", motivo);
    }

    [Fact]
    public void PrecoZero_EhRejeitado_MesmoSemSnapshotAnterior()
    {
        // É o caso do primeiro ciclo depois de cadastrar uma moeda: não há base de
        // comparação, mas zero continua sendo um preço impossível.
        Assert.False(PriceSnapshotJob.PrecoEhPlausivel(0m, null, out _));
    }

    [Fact]
    public void PrimeiraCotacao_EhAceita_SemBaseDeComparacao()
    {
        Assert.True(PriceSnapshotJob.PrecoEhPlausivel(600_000m, null, out _));
    }

    [Fact]
    public void PrecoDezVezesMaior_EhRejeitado()
    {
        // O cenário do relatório: BTC real R$ 600.000, API devolve R$ 6.000.000.
        Assert.False(PriceSnapshotJob.PrecoEhPlausivel(6_000_000m, 600_000m, out var motivo));
        Assert.Contains("banda", motivo);
    }

    [Fact]
    public void PrecoDezVezesMenor_EhRejeitado()
    {
        // O mais perigoso: no aporte, preço 10× menor vira 10× MAIS cripto registrada.
        Assert.False(PriceSnapshotJob.PrecoEhPlausivel(60_000m, 600_000m, out _));
    }

    [Theory]
    [InlineData(600_000, 630_000)]   // +5%
    [InlineData(600_000, 560_000)]   // −6,7%
    [InlineData(600_000, 830_000)]   // +38,3%, ainda dentro da banda
    [InlineData(600_000, 361_000)]   // −39,8%, ainda dentro da banda
    public void VariacaoDentroDaBanda_EhAceita(decimal anterior, decimal recebido)
    {
        Assert.True(PriceSnapshotJob.PrecoEhPlausivel(recebido, anterior, out _));
    }

    [Theory]
    [InlineData(600_000, 900_000)]   // +50%
    [InlineData(600_000, 300_000)]   // −50%
    public void VariacaoForaDaBanda_EhRejeitada(decimal anterior, decimal recebido)
    {
        Assert.False(PriceSnapshotJob.PrecoEhPlausivel(recebido, anterior, out _));
    }

    [Fact]
    public void SnapshotAnteriorZerado_NaoBloqueiaRecuperacao()
    {
        // Se um preço ruim entrou antes desta guarda existir, a moeda ficaria presa: toda
        // cotação nova pareceria variação infinita contra zero. O anterior inválido é
        // tratado como ausência de base.
        Assert.True(PriceSnapshotJob.PrecoEhPlausivel(600_000m, 0m, out _));
    }

    [Fact]
    public void BandaEhSimetrica()
    {
        // Exatamente na fronteira, para os dois lados.
        const decimal anterior = 100m;
        Assert.True(PriceSnapshotJob.PrecoEhPlausivel(anterior * (1 + PriceSnapshotJob.BandaMaxima), anterior, out _));
        Assert.True(PriceSnapshotJob.PrecoEhPlausivel(anterior * (1 - PriceSnapshotJob.BandaMaxima), anterior, out _));
        Assert.False(PriceSnapshotJob.PrecoEhPlausivel(anterior * 1.41m, anterior, out _));
        Assert.False(PriceSnapshotJob.PrecoEhPlausivel(anterior * 0.59m, anterior, out _));
    }
}
