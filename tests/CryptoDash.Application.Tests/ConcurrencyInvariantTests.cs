using CryptoDash.Application.DTOs;
using CryptoDash.Application.Services;
using CryptoDash.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Spec 04 — atomicidade e lost update.
///
/// A posição é reconstruída por replay do razão inteiro (read-modify-write). Sem token de
/// concorrência, duas escritas na mesma moeda sobrescreviam uma à outra e a posição divergia
/// do razão em silêncio — divergência permanente, porque nada reconcilia.
/// </summary>
public class ConcurrencyInvariantTests
{
    private static readonly DateTimeOffset T0 = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    private static CreateTransactionRequest Buy(int coinId, decimal qty, decimal price, DateTimeOffset at)
        => new(coinId, TransactionType.Buy, qty, price, 0m, at, null);

    [Fact]
    public async Task EscritaConcorrenteNaMesmaPosicao_FalhaEmVezDeSobrescrever()
    {
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);
        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 100_000m, T0));

        // Dois contextos independentes carregam a MESMA posição — o interleaving que antes
        // fazia 0,01 BTC desaparecer.
        await using var ctxA = t.NewContext();
        await using var ctxB = t.NewContext();

        var a = await ctxA.Holdings.FirstAsync(h => h.CoinId == coin.Id);
        var b = await ctxB.Holdings.FirstAsync(h => h.CoinId == coin.Id);

        a.Quantity = 2m;
        a.Version++;
        await ctxA.SaveChangesAsync();

        // B ainda carrega a versão antiga: sem o token, este save venceria com dado obsoleto.
        b.Quantity = 3m;
        b.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctxB.SaveChangesAsync());
    }

    [Fact]
    public async Task VersaoEhIncrementadaACadaRecalculo()
    {
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);

        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 100_000m, T0));
        var v1 = t.HoldingFor(coin.Id)!.Version;

        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 110_000m, T0.AddDays(1)));
        var v2 = t.HoldingFor(coin.Id)!.Version;

        Assert.True(v2 > v1, $"versão não avançou: {v1} -> {v2}");
    }

    [Fact]
    public async Task TransacaoRejeitada_NaoDeixaLancamentoOrfao()
    {
        // Atomicidade: antes, o INSERT ficava commitado e o recálculo podia não acontecer.
        // Aqui a venda a descoberto é recusada — nada pode sobrar no razão.
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);
        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 100_000m, T0));

        var r = await svc.CreateTransactionAsync(
            new CreateTransactionRequest(coin.Id, TransactionType.Sell, 5m, 120_000m, 0m, T0.AddDays(1), null));

        Assert.False(r.Success);
        Assert.Equal(1, await t.Db.Transactions.CountAsync());
        Assert.Equal(t.LedgerNetQuantity(coin.Id), t.HoldingFor(coin.Id)!.Quantity);
    }

    [Fact]
    public async Task DeleteEhAtomico_RazaoEPosicaoContinuamBatendo()
    {
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);

        await svc.CreateTransactionAsync(Buy(coin.Id, 1m, 100_000m, T0));
        var segunda = await svc.CreateTransactionAsync(Buy(coin.Id, 2m, 110_000m, T0.AddDays(1)));

        var del = await svc.DeleteTransactionAsync(segunda.Value!.Id);

        Assert.True(del.Success, del.Error);
        Assert.Equal(t.LedgerNetQuantity(coin.Id), t.HoldingFor(coin.Id)!.Quantity);
        Assert.Equal(1m, t.HoldingFor(coin.Id)!.Quantity);
    }

    [Fact]
    public async Task SequenciaDeEscritas_MantemRazaoEPosicaoAlinhados()
    {
        // Rede de segurança da spec: a transação e o token não podem quebrar o caminho normal.
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        var svc = new PortfolioService(t.Db);

        for (var i = 0; i < 15; i++)
        {
            var r = await svc.CreateTransactionAsync(Buy(coin.Id, 0.01m, 100_000m + i * 1_000m, T0.AddMinutes(i)));
            Assert.True(r.Success, r.Error);
        }

        Assert.Equal(t.LedgerNetQuantity(coin.Id), t.HoldingFor(coin.Id)!.Quantity);
        Assert.Equal(0.15m, t.HoldingFor(coin.Id)!.Quantity);
    }
}
