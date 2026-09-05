using CryptoDash.Application.DTOs;
using CryptoDash.Application.Services;
using CryptoDash.Domain.Enums;
using Xunit;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Invariante 7 — a variação de 24h exibida no cabeçalho precisa ser a variação real
/// do patrimônio, calculada de forma independente a partir dos preços.
///
/// Este é o número mais visível do produto: é o que o usuário lê antes de decidir aportar.
/// </summary>
public class PortfolioSummaryInvariantTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static CreateTransactionRequest Buy(int coinId, decimal qty, decimal price)
        => new(coinId, TransactionType.Buy, qty, price, 0m, T0, null);

    /// <summary>
    /// Variação real em BRL = Σ qty × (P_agora − P_24h), com P_24h = P_agora / (1 + c).
    /// </summary>
    private static decimal VariacaoEsperadaBrl(decimal qty, decimal precoAgora, decimal changePct)
    {
        var preco24hAtras = precoAgora / (1m + changePct / 100m);
        return qty * (precoAgora - preco24hAtras);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(-12)]
    [InlineData(35)]
    [InlineData(-40)]
    public async Task Invariante7_VariacaoEmReais_BateComOCalculoIndependente(decimal changePct)
    {
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);

        const decimal qty = 0.05m;
        const decimal precoAgora = 600_000m;

        await svc.CreateTransactionAsync(Buy(coin.Id, qty, 500_000m));
        t.SetPrice(coin.Id, precoAgora, changePct);

        var resumo = await svc.GetSummaryAsync();
        var esperado = Math.Round(VariacaoEsperadaBrl(qty, precoAgora, changePct), 2);

        Assert.True(
            Math.Abs(resumo.Change24hBrl - esperado) <= 0.01m,
            $"variação 24h reportada R$ {resumo.Change24hBrl:F2}, esperada R$ {esperado:F2} " +
            $"(erro de R$ {Math.Abs(resumo.Change24hBrl - esperado):F2} com {changePct}% de variação)");
    }

    [Fact]
    public async Task Invariante7b_AltaDeCemPorCento_NaoPodeExibirZero()
    {
        // prevTotal = totalBrl − change24hBrl. Com c = +100%, prevTotal vira exatamente
        // zero e a guarda `prevTotal == 0 ? 0` devolve 0,00%: a carteira dobrou e o
        // cabeçalho informa que nada aconteceu.
        using var t = new TestDb();
        var coin = t.AddCoin("SOL", RiskTier.Growth);
        var svc = new PortfolioService(t.Db);

        await svc.CreateTransactionAsync(Buy(coin.Id, 10m, 400m));
        t.SetPrice(coin.Id, 1_000m, 100m);

        var resumo = await svc.GetSummaryAsync();

        Assert.True(resumo.Change24hPct > 0m,
            $"carteira dobrou em 24h e o percentual exibido foi {resumo.Change24hPct}%");
    }

    [Fact]
    public async Task Invariante7c_AltaAcimaDeCemPorCento_NaoPodeInverterOSinal()
    {
        // Acima de +100%, prevTotal fica negativo e o percentual inverte: valor em reais
        // positivo (verde) com percentual negativo, na mesma linha.
        using var t = new TestDb();
        var coin = t.AddCoin("SOL", RiskTier.Growth);
        var svc = new PortfolioService(t.Db);

        await svc.CreateTransactionAsync(Buy(coin.Id, 10m, 400m));
        t.SetPrice(coin.Id, 1_500m, 150m);

        var resumo = await svc.GetSummaryAsync();

        Assert.True(resumo.Change24hBrl > 0m, "a variação em reais deveria ser positiva");
        Assert.True(resumo.Change24hPct > 0m,
            $"variação em reais R$ {resumo.Change24hBrl:F2} (positiva) " +
            $"mas percentual {resumo.Change24hPct}% (sinal invertido)");
    }

    [Fact]
    public async Task Invariante7d_TotalDoResumo_IgualASomaDasPosicoes()
    {
        // Coerência interna: o total do cabeçalho tem de fechar com a soma das linhas
        // da tabela de posições. Se divergirem, o usuário não consegue conferir nada.
        using var t = new TestDb();
        var btc = t.AddCoin("BTC");
        var sol = t.AddCoin("SOL", RiskTier.Growth);
        var svc = new PortfolioService(t.Db);

        await svc.CreateTransactionAsync(Buy(btc.Id, 0.0137m, 480_000m));
        await svc.CreateTransactionAsync(Buy(sol.Id, 13.7m, 733m));
        t.SetPrice(btc.Id, 611_333m, 3.5m);
        t.SetPrice(sol.Id, 812.45m, -2.25m);

        var resumo = await svc.GetSummaryAsync();

        Assert.Equal(resumo.TotalBrl, Math.Round(resumo.Holdings.Sum(h => h.ValueBrl), 2));
        Assert.Equal(resumo.TotalCostBrl, Math.Round(resumo.Holdings.Sum(h => h.Quantity * h.AvgCostBrl), 2));
        Assert.Equal(resumo.TotalPnlBrl, Math.Round(resumo.Holdings.Sum(h => h.PnlBrl), 2));
    }

    [Fact]
    public async Task Invariante7e_AlocacaoAtualSoma100PorCento()
    {
        // A tabela de desvio precisa fechar em 100%: se não fecha, "atual vs alvo"
        // não é comparável e o semáforo de rebalanceamento mente.
        using var t = new TestDb();
        var btc = t.AddCoin("BTC");
        var sol = t.AddCoin("SOL", RiskTier.Growth);
        var paxg = t.AddCoin("PAXG", RiskTier.Hedge);
        var svc = new PortfolioService(t.Db);

        await svc.CreateTransactionAsync(Buy(btc.Id, 0.011m, 500_000m));
        await svc.CreateTransactionAsync(Buy(sol.Id, 7m, 700m));
        await svc.CreateTransactionAsync(Buy(paxg.Id, 0.09m, 21_000m));
        t.SetPrice(btc.Id, 533_333m);
        t.SetPrice(sol.Id, 777.77m);
        t.SetPrice(paxg.Id, 22_222m);
        t.SetTarget(btc.Id, 55m);
        t.SetTarget(sol.Id, 15m);
        t.SetTarget(paxg.Id, 30m);

        var resumo = await svc.GetSummaryAsync();
        var somaAtual = resumo.Allocation.Sum(a => a.CurrentPct);

        Assert.True(Math.Abs(somaAtual - 100m) <= 0.05m,
            $"a alocação atual soma {somaAtual}%, não 100%");
    }
}
