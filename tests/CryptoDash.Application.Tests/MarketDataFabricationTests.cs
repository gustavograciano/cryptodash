using CryptoDash.Application.Abstractions;
using CryptoDash.Infrastructure.External.CoinGecko;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;
using Xunit;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Spec 12 — o pipeline de mercado deixa de produzir número inventado.
///
/// Estes dados alimentam a tabela "Sinais técnicos", que o usuário lê para decidir aporte.
/// </summary>
public class MarketDataFabricationTests
{
    /// <summary>Cliente falso: devolve o payload que o teste montar, sem rede.</summary>
    private sealed class FakeCoinGecko(CoinGeckoMarketChartResponse resposta) : ICoinGeckoApi
    {
        public Task<Dictionary<string, CoinGeckoPriceResponse>> GetSimplePriceAsync(
            string ids, string vsCurrencies, bool include24hChange, bool include24hVol,
            bool includeMarketCap, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<CoinGeckoMarketChartResponse> GetMarketChartAsync(
            string id, string vsCurrency, int days, CancellationToken ct = default)
            => Task.FromResult(resposta);

        public Task<CoinGeckoGlobalResponse> GetGlobalAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private static long Ms(int dia, int hora) =>
        new DateTimeOffset(2026, 6, dia, hora, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static Task<IReadOnlyList<OhlcvPoint>> Agregar(CoinGeckoMarketChartResponse r) =>
        new CoinGeckoMarketDataFeed(new FakeCoinGecko(r), NullLogger<CoinGeckoMarketDataFeed>.Instance)
            .GetMarketChartAsync("bitcoin", 30);

    [Fact]
    public async Task VolumeDiario_NaoEhSomaDasLeiturasRolantes()
    {
        // total_volumes da CoinGecko é volume ROLANTE de 24h. Somar 4 leituras de ~500.000
        // produzia 2.000.000 — quatro vezes o valor real. Com 24 leituras horárias, ~24x.
        var r = new CoinGeckoMarketChartResponse
        {
            Prices = new()
            {
                new() { Ms(1, 0), 100m }, new() { Ms(1, 6), 110m },
                new() { Ms(1, 12), 90m }, new() { Ms(1, 18), 105m },
            },
            Total_volumes = new()
            {
                new() { Ms(1, 0), 500_000m }, new() { Ms(1, 6), 510_000m },
                new() { Ms(1, 12), 495_000m }, new() { Ms(1, 18), 520_000m },
            }
        };

        var pontos = await Agregar(r);

        var dia = Assert.Single(pontos);
        Assert.Equal(520_000m, dia.Volume);   // última leitura, não a soma (2.025.000)
    }

    [Fact]
    public async Task VelaComVariosPontos_EhMarcadaComoIntraday()
    {
        var r = new CoinGeckoMarketChartResponse
        {
            Prices = new() { new() { Ms(2, 0), 100m }, new() { Ms(2, 12), 120m } },
            Total_volumes = new() { new() { Ms(2, 12), 1_000m } }
        };

        var dia = Assert.Single(await Agregar(r));

        Assert.True(dia.HasIntraday);
        Assert.Equal(120m, dia.High);
        Assert.Equal(100m, dia.Low);
    }

    [Fact]
    public async Task VelaComUmUnicoPonto_NaoSeApresentaComoIntraday()
    {
        // É o que acontece com days > 90: a fonte devolve um ponto por dia e High/Low viram
        // repetição do fechamento. Sem a marca, a mesma coluna teria duas semânticas.
        var r = new CoinGeckoMarketChartResponse
        {
            Prices = new() { new() { Ms(3, 0), 100m } },
            Total_volumes = new() { new() { Ms(3, 0), 1_000m } }
        };

        var dia = Assert.Single(await Agregar(r));

        Assert.False(dia.HasIntraday);
        Assert.Equal(dia.Close, dia.High);
        Assert.Equal(dia.Close, dia.Low);
    }

    [Fact]
    public async Task PrecoSubCentavo_NaoEhZeradoPelaAgregacao()
    {
        // O arredondamento em 2 casas zerava qualquer ativo sub-centavo antes mesmo de chegar
        // ao banco.
        var r = new CoinGeckoMarketChartResponse
        {
            Prices = new() { new() { Ms(4, 0), 0.00007m }, new() { Ms(4, 12), 0.00009m } },
            Total_volumes = new() { new() { Ms(4, 12), 1_000m } }
        };

        var dia = Assert.Single(await Agregar(r));

        Assert.True(dia.Close > 0m, $"fechamento zerado: {dia.Close}");
        Assert.Equal(0.00009m, dia.High);
    }

    [Fact]
    public async Task DiasDistintos_NaoMisturamVolume()
    {
        var r = new CoinGeckoMarketChartResponse
        {
            Prices = new() { new() { Ms(5, 0), 100m }, new() { Ms(6, 0), 200m } },
            Total_volumes = new() { new() { Ms(5, 0), 111m }, new() { Ms(6, 0), 222m } }
        };

        var pontos = await Agregar(r);

        Assert.Equal(2, pontos.Count);
        Assert.Equal(111m, pontos[0].Volume);
        Assert.Equal(222m, pontos[1].Volume);
    }
}
