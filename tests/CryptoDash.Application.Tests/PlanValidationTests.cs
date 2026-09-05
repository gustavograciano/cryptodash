using CryptoDash.Application.DTOs;
using CryptoDash.Application.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Spec 14 — o plano de aporte valida o dinheiro, não só o dia.
///
/// Um `amountBrl` negativo era persistido com 200 e depois o `DcaAllocator` caía na guarda
/// `aporteBrl &lt;= 0`, devolvia lista vazia, e o card de aporte — o recurso principal do
/// produto — morria em silêncio, sem erro na tela.
/// </summary>
public class PlanValidationTests
{
    private static AportsService Servico(TestDb t) => new(t.Db);

    [Theory]
    [InlineData(-300)]
    [InlineData(-0.01)]
    [InlineData(0)]
    public async Task ValorNaoPositivo_EhRecusado(decimal valor)
    {
        using var t = new TestDb();

        await Assert.ThrowsAsync<ArgumentException>(
            () => Servico(t).UpsertPlanAsync(new UpdateAportPlanRequest(valor, 5, true)));

        Assert.Equal(0, await t.Db.AportPlans.CountAsync());
    }

    [Fact]
    public async Task ValorAcimaDoTeto_EhRecusado()
    {
        // Proteção contra dedo gordo: não há confirmação na interface.
        using var t = new TestDb();

        await Assert.ThrowsAsync<ArgumentException>(
            () => Servico(t).UpsertPlanAsync(
                new UpdateAportPlanRequest(AportsService.TetoDoAporte + 1m, 5, true)));
    }

    [Fact]
    public async Task ValorValido_EhAceito()
    {
        using var t = new TestDb();

        var plano = await Servico(t).UpsertPlanAsync(new UpdateAportPlanRequest(300m, 5, true));

        Assert.Equal(300m, plano.AmountBrl);
        Assert.Equal(1, await t.Db.AportPlans.CountAsync());
    }

    [Fact]
    public async Task DiaInvalido_ContinuaSendoRecusado()
    {
        // A validação que já existia não pode ter sido perdida na mudança.
        using var t = new TestDb();

        await Assert.ThrowsAsync<ArgumentException>(
            () => Servico(t).UpsertPlanAsync(new UpdateAportPlanRequest(300m, 31, true)));
    }

    [Fact]
    public async Task PlanoInvalido_NaoSobrescreveOPlanoExistente()
    {
        // A validação roda antes de qualquer escrita: uma tentativa ruim não pode danificar
        // o plano que estava funcionando.
        using var t = new TestDb();
        var svc = Servico(t);
        await svc.UpsertPlanAsync(new UpdateAportPlanRequest(300m, 5, true));

        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.UpsertPlanAsync(new UpdateAportPlanRequest(-1m, 10, false)));

        var plano = await svc.GetPlanAsync();
        Assert.Equal(300m, plano!.AmountBrl);
        Assert.Equal(5, plano.DayOfMonth);
        Assert.True(plano.IsActive);
    }
}
