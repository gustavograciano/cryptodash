using CryptoDash.Application.DTOs;
using CryptoDash.Application.Services;
using CryptoDash.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Invariantes de execução de aporte: idempotência, atomicidade e validação server-side.
///
/// A propriedade central é simples de enunciar e difícil de garantir: <b>uma intenção de
/// aporte produz no máximo um conjunto de transações</b>, e ou produz todas ou não produz
/// nenhuma.
/// </summary>
public class AportExecutionInvariantTests
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 5, 13, 30, 0, TimeSpan.Zero);

    /// <summary>Carteira com plano ativo, alvos e cotações — o cenário do caminho feliz.</summary>
    private static (TestDb Db, AportsService Svc, int PlanId, int Btc, int Sol) Cenario()
    {
        var t = new TestDb();
        var btc = t.AddCoin("BTC");
        var sol = t.AddCoin("SOL", RiskTier.Growth);
        t.SetPrice(btc.Id, 600_000m);
        t.SetPrice(sol.Id, 800m);
        t.SetTarget(btc.Id, 70m);
        t.SetTarget(sol.Id, 30m);

        var plan = new Domain.Entities.AportPlan
        {
            AmountBrl = 300m,
            DayOfMonth = 5,
            IsActive = true,
            CreatedAt = T0
        };
        t.Db.AportPlans.Add(plan);
        t.Db.SaveChanges();

        return (t, new AportsService(t.Db), plan.Id, btc.Id, sol.Id);
    }

    private static ExecuteAportRequest Pedido(int planId, int btc, int sol, string? chave = null,
        decimal valorBtc = 210m, decimal valorSol = 90m) =>
        new(planId, valorBtc + valorSol, T0,
            new[]
            {
                new AportAllocationDto(btc, "BTC", valorBtc, 0m, 0m, 0m),
                new AportAllocationDto(sol, "SOL", valorSol, 0m, 0m, 0m),
            },
            chave);

    // ─────────────────────────────────────────────────────────────────────────
    // IDEMPOTÊNCIA
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DuploClique_ComMesmaChave_GravaUmUnicoAporte()
    {
        var (t, svc, plan, btc, sol) = Cenario();
        using var _ = t;

        var req = Pedido(plan, btc, sol, chave: "abc-123");
        var primeira = await svc.ExecuteAsync(req);
        var segunda = await svc.ExecuteAsync(req);

        Assert.True(primeira.Success, primeira.Error);
        Assert.True(segunda.Success, segunda.Error);

        Assert.False(primeira.Value!.Replayed);
        Assert.True(segunda.Value!.Replayed, "a segunda chamada deveria ser replay, não gravação nova");
        Assert.Equal(primeira.Value.AportExecutedId, segunda.Value.AportExecutedId);
        Assert.Equal(primeira.Value.TransactionIds, segunda.Value.TransactionIds);

        Assert.Equal(1, await t.Db.AportsExecuted.CountAsync());
        Assert.Equal(2, await t.Db.Transactions.CountAsync());
    }

    [Fact]
    public async Task DuploClique_SemChaveDoCliente_ChaveNaturalAindaDeduplica()
    {
        // Protege o bundle já publicado, que não envia o header: o modal congela ExecutedAt
        // no mount, então os dois cliques mandam exatamente o mesmo instante.
        var (t, svc, plan, btc, sol) = Cenario();
        using var _ = t;

        await svc.ExecuteAsync(Pedido(plan, btc, sol));
        var segunda = await svc.ExecuteAsync(Pedido(plan, btc, sol));

        Assert.True(segunda.Success, segunda.Error);
        Assert.True(segunda.Value!.Replayed);
        Assert.Equal(1, await t.Db.AportsExecuted.CountAsync());
        Assert.Equal(2, await t.Db.Transactions.CountAsync());
    }

    [Fact]
    public async Task MesmaChave_ComPayloadDiferente_EhConflito_NaoReplaySilencioso()
    {
        // Sem esta checagem trocaríamos duplicação por PERDA SILENCIOSA: um aporte real
        // devolvido como se fosse o anterior, sem erro e sem rastro.
        var (t, svc, plan, btc, sol) = Cenario();
        using var _ = t;

        await svc.ExecuteAsync(Pedido(plan, btc, sol, chave: "k1", valorBtc: 210m, valorSol: 90m));
        var outro = await svc.ExecuteAsync(Pedido(plan, btc, sol, chave: "k1", valorBtc: 100m, valorSol: 200m));

        Assert.False(outro.Success, "mesma chave com outro conteúdo não pode devolver sucesso");
        Assert.StartsWith(AportsService.ConflitoDeChave, outro.Error);
        Assert.Equal(1, await t.Db.AportsExecuted.CountAsync());
    }

    [Fact]
    public async Task ChavesDiferentes_PermitemDoisAportesLegitimos()
    {
        // Idempotência não pode virar bloqueio: dois aportes distintos precisam passar.
        var (t, svc, plan, btc, sol) = Cenario();
        using var _ = t;

        var a = await svc.ExecuteAsync(Pedido(plan, btc, sol, chave: "k1"));
        var b = await svc.ExecuteAsync(Pedido(plan, btc, sol, chave: "k2"));

        Assert.True(a.Success, a.Error);
        Assert.True(b.Success, b.Error);
        Assert.False(b.Value!.Replayed);
        Assert.NotEqual(a.Value!.AportExecutedId, b.Value.AportExecutedId);
        Assert.Equal(2, await t.Db.AportsExecuted.CountAsync());
        Assert.Equal(4, await t.Db.Transactions.CountAsync());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ATOMICIDADE
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AporteRecusado_NaoDeixaNenhumaEscritaParcial()
    {
        // SOL sem cotação: antes, BTC virava transação e SOL era pulado em silêncio,
        // enquanto o aporte registrava o valor integral.
        var (t, svc, plan, btc, sol) = Cenario();
        using var _ = t;
        t.SetPrice(sol, 0m);

        var r = await svc.ExecuteAsync(Pedido(plan, btc, sol, chave: "k1"));

        Assert.False(r.Success);
        Assert.Contains("SOL", r.Error);
        Assert.Equal(0, await t.Db.Transactions.CountAsync());
        Assert.Equal(0, await t.Db.AportsExecuted.CountAsync());
        Assert.Equal(0, await t.Db.Holdings.CountAsync());
    }

    [Fact]
    public async Task ValorRegistradoNoAporte_EhASomaRealDasTransacoes()
    {
        var (t, svc, plan, btc, sol) = Cenario();
        using var _ = t;

        var r = await svc.ExecuteAsync(Pedido(plan, btc, sol, chave: "k1"));
        Assert.True(r.Success, r.Error);

        var executado = await t.Db.AportsExecuted.AsNoTracking().SingleAsync();
        var txs = await t.Db.Transactions.AsNoTracking().ToListAsync();
        var somaTx = txs.Sum(x => x.TotalBrl);

        Assert.True(somaTx == executado.AmountBrl && executado.AmountBrl == 300m,
            $"aporte={executado.AmountBrl} somaTx={somaTx} n={txs.Count} " +
            $"[{string.Join(", ", txs.Select(x => $"coin{x.CoinId}:{x.TotalBrl}"))}] " +
            $"ids=[{string.Join(",", executado.TransactionIds)}]");
    }

    [Fact]
    public async Task PosicaoResultante_BateComORazao()
    {
        var (t, svc, plan, btc, sol) = Cenario();
        using var _ = t;

        await svc.ExecuteAsync(Pedido(plan, btc, sol, chave: "k1"));

        foreach (var coinId in new[] { btc, sol })
            Assert.Equal(t.LedgerNetQuantity(coinId), t.HoldingFor(coinId)!.Quantity);
    }

    [Fact]
    public async Task ReplayNaoRecalculaPosicao_NemDuplicaQuantidade()
    {
        var (t, svc, plan, btc, sol) = Cenario();
        using var _ = t;

        await svc.ExecuteAsync(Pedido(plan, btc, sol, chave: "k1"));
        var qtdBtc = t.HoldingFor(btc)!.Quantity;

        await svc.ExecuteAsync(Pedido(plan, btc, sol, chave: "k1"));

        Assert.Equal(qtdBtc, t.HoldingFor(btc)!.Quantity);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VALIDAÇÃO SERVER-SIDE
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SomaDasAlocacoesQueNaoFechaComOTotal_EhRecusada()
    {
        // O servidor não é obrigado a acreditar no total que o cliente enviou.
        var (t, svc, plan, btc, sol) = Cenario();
        using var _ = t;

        var adulterado = new ExecuteAportRequest(plan, 300m, T0, new[]
        {
            new AportAllocationDto(btc, "BTC", 210m, 0m, 0m, 0m),
            new AportAllocationDto(sol, "SOL", 500m, 0m, 0m, 0m),   // soma 710, total diz 300
        }, "k1");

        var r = await svc.ExecuteAsync(adulterado);

        Assert.False(r.Success);
        Assert.Equal(0, await t.Db.Transactions.CountAsync());
    }

    [Fact]
    public async Task PlanoInativo_EhRecusado()
    {
        var (t, svc, plan, btc, sol) = Cenario();
        using var _ = t;
        var p = await t.Db.AportPlans.SingleAsync(x => x.Id == plan);
        p.IsActive = false;
        await t.Db.SaveChangesAsync();

        var r = await svc.ExecuteAsync(Pedido(plan, btc, sol, chave: "k1"));

        Assert.False(r.Success);
        Assert.Equal(0, await t.Db.Transactions.CountAsync());
    }

    [Fact]
    public async Task AlocacaoNegativa_EhRecusada()
    {
        var (t, svc, plan, btc, sol) = Cenario();
        using var _ = t;

        var r = await svc.ExecuteAsync(Pedido(plan, btc, sol, chave: "k1", valorBtc: -50m, valorSol: 350m));

        Assert.False(r.Success);
        Assert.Equal(0, await t.Db.Transactions.CountAsync());
    }

    [Fact]
    public async Task QuantidadeQueArredondaParaZero_EhRecusada()
    {
        // Custo entraria na base com quantidade zero e inflaria o custo médio para sempre.
        var (t, svc, plan, btc, sol) = Cenario();
        using var _ = t;
        t.SetPrice(btc, 1_000_000_000_000m);

        var r = await svc.ExecuteAsync(Pedido(plan, btc, sol, chave: "k1", valorBtc: 0.01m, valorSol: 299.99m));

        Assert.False(r.Success);
        Assert.Equal(0, await t.Db.Transactions.CountAsync());
    }

    [Fact]
    public async Task MoedaRepetidaNasAlocacoes_EhRecusada()
    {
        var (t, svc, plan, btc, _) = Cenario();
        using var _t = t;

        var r = await svc.ExecuteAsync(new ExecuteAportRequest(plan, 300m, T0, new[]
        {
            new AportAllocationDto(btc, "BTC", 150m, 0m, 0m, 0m),
            new AportAllocationDto(btc, "BTC", 150m, 0m, 0m, 0m),
        }, "k1"));

        Assert.False(r.Success);
        Assert.Equal(0, await t.Db.Transactions.CountAsync());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SUGESTÃO
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SugestaoNaoIncluiMoedaSemCotacao()
    {
        // Antes, moeda sem preço entrava na sugestão com valor atual zero — o que lhe dava
        // o maior gap e a maior fatia — e era descartada em silêncio na execução. Como a
        // execução agora recusa o aporte inteiro, sugeri-la seria um beco sem saída.
        var (t, svc, _, btc, sol) = Cenario();
        using var _t = t;
        t.SetPrice(sol, 0m);

        var sugestao = await svc.GetRebalanceSuggestionAsync(300m);

        Assert.DoesNotContain(sugestao, s => s.CoinId == sol);
        Assert.Contains(sugestao, s => s.CoinId == btc);
        Assert.Equal(300m, sugestao.Sum(s => s.AmountBrl));
    }
}
