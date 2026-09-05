using CryptoDash.Application.DTOs;
using CryptoDash.Application.Services;
using CryptoDash.Domain.Entities;
using CryptoDash.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Spec 05 — reconciliação razão↔posição.
///
/// É o multiplicador de severidade de todos os outros defeitos: cada race, crash ou delete
/// produzia erro silencioso e permanente porque nada comparava.
/// </summary>
public class ReconciliationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static CreateTransactionRequest Buy(int coinId, decimal qty, decimal price, DateTimeOffset at)
        => new(coinId, TransactionType.Buy, qty, price, 0m, at, null);

    [Fact]
    public async Task PortfolioIntegro_NaoAcusaDivergencia()
    {
        using var t = new TestDb();
        var btc = t.AddCoin("BTC");
        var sol = t.AddCoin("SOL", RiskTier.Growth);
        var svc = new PortfolioService(t.Db);
        await svc.CreateTransactionAsync(Buy(btc.Id, 0.5m, 500_000m, T0));
        await svc.CreateTransactionAsync(Buy(sol.Id, 10m, 700m, T0));

        var r = await new ReconciliationService(t.Db).CheckAsync();

        Assert.True(r.IsHealthy, "portfólio íntegro não deveria acusar divergência");
        Assert.Empty(r.Mismatches);
        Assert.Equal(2, r.CoinsChecked);
    }

    [Fact]
    public async Task DivergenciaForcada_EhDetectadaComMagnitudeCorreta()
    {
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);
        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 500_000m, T0));

        // Simula o lost update: a posição afirma ter mais cripto do que o razão comprova.
        var h = await t.Db.Holdings.FirstAsync(x => x.CoinId == coin.Id);
        h.Quantity = 2m;
        await t.Db.SaveChangesAsync();

        var r = await new ReconciliationService(t.Db).CheckAsync();

        Assert.False(r.IsHealthy);
        var d = Assert.Single(r.Mismatches);
        Assert.Equal("BTC", d.Symbol);
        Assert.Equal(1m, d.LedgerQuantity);
        Assert.Equal(2m, d.HoldingQuantity);
        Assert.Equal(1m, d.Difference);   // positivo = posição infla o patrimônio
    }

    [Fact]
    public async Task Recalculo_ReparaADivergencia()
    {
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);
        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 500_000m, T0));

        var h = await t.Db.Holdings.FirstAsync(x => x.CoinId == coin.Id);
        h.Quantity = 99m;
        await t.Db.SaveChangesAsync();

        var recon = new ReconciliationService(t.Db);
        Assert.False((await recon.CheckAsync()).IsHealthy);

        var depois = await recon.RecalculateAsync();

        Assert.True(depois.IsHealthy, "o recálculo deveria ter reparado a divergência");
        Assert.Equal(1m, t.HoldingFor(coin.Id)!.Quantity);
    }

    [Fact]
    public async Task PosicaoAusenteParaMoedaComRazao_EhDetectada()
    {
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);
        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 500_000m, T0));

        // Linha de posição some — crash entre o INSERT e o recálculo, ou reparo manual torto.
        t.Db.Holdings.RemoveRange(t.Db.Holdings);
        await t.Db.SaveChangesAsync();

        var r = await new ReconciliationService(t.Db).CheckAsync();

        Assert.False(r.IsHealthy);
        var d = Assert.Single(r.Mismatches);
        Assert.Equal(1m, d.LedgerQuantity);
        Assert.Equal(0m, d.HoldingQuantity);
    }

    [Fact]
    public async Task RecalculoPorMoeda_NaoTocaAsDemais()
    {
        using var t = new TestDb();
        var btc = t.AddCoin("BTC");
        var sol = t.AddCoin("SOL", RiskTier.Growth);
        var svc = new PortfolioService(t.Db);
        await svc.CreateTransactionAsync(Buy(btc.Id, 1m, 500_000m, T0));
        await svc.CreateTransactionAsync(Buy(sol.Id, 10m, 700m, T0));

        foreach (var h in await t.Db.Holdings.ToListAsync()) h.Quantity = 42m;
        await t.Db.SaveChangesAsync();

        await new ReconciliationService(t.Db).RecalculateAsync(btc.Id);

        Assert.Equal(1m, t.HoldingFor(btc.Id)!.Quantity);
        Assert.Equal(42m, t.HoldingFor(sol.Id)!.Quantity);   // intocada
    }

    [Fact]
    public async Task PosicaoNegativa_ApareceNoResumo_EmVezDeSerFiltrada()
    {
        // Posição negativa é o sinal de razão corrompido. O filtro `Quantity > 0` fazia
        // justamente a evidência do problema sumir do dashboard.
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);
        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 500_000m, T0));
        t.SetPrice(coin.Id, 600_000m);

        var h = await t.Db.Holdings.FirstAsync(x => x.CoinId == coin.Id);
        h.Quantity = -0.5m;
        await t.Db.SaveChangesAsync();

        var resumo = await svc.GetSummaryAsync();

        Assert.Contains(resumo.Holdings, x => x.CoinId == coin.Id && x.Quantity < 0m);
    }

    [Fact]
    public async Task PortfolioVazio_EhSaudavel()
    {
        using var t = new TestDb();
        var r = await new ReconciliationService(t.Db).CheckAsync();
        Assert.True(r.IsHealthy);
        Assert.Equal(0, r.CoinsChecked);
    }
}
