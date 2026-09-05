using CryptoDash.Api.Controllers;
using CryptoDash.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Spec 09 — readiness precisa refletir se o sistema está FUNCIONANDO, não só se o banco
/// responde.
///
/// Antes, o job de preço podia estar parado há dias e <c>/health/ready</c> devolvia 200:
/// o dashboard seguia mostrando números com aparência normal e nada indicava a parada.
/// </summary>
public class HealthReadinessTests
{
    private static ObjectResult Executar(TestDb t)
        => (ObjectResult)new HealthController(t.Db).Ready(default).GetAwaiter().GetResult();

    [Fact]
    public void PrecoFresco_Responde200()
    {
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        t.SetPrice(coin.Id, 600_000m);

        var r = Executar(t);

        Assert.Equal(200, r.StatusCode);
    }

    [Fact]
    public void PrecoVelho_Degrada_Para503()
    {
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        t.SetPrice(coin.Id, 600_000m);

        // Job parado: o preço envelheceu além do limiar.
        var snap = t.Db.PriceSnapshots.First(s => s.CoinId == coin.Id);
        snap.UpdatedAt = DateTimeOffset.UtcNow - HealthController.IdadeMaximaDoPreco - TimeSpan.FromMinutes(5);
        t.Db.SaveChanges();

        var r = Executar(t);

        Assert.Equal(503, r.StatusCode);
    }

    [Fact]
    public void SemNenhumPreco_Degrada()
    {
        // Primeiro boot antes do job rodar, ou base recém-restaurada: não há o que confiar.
        using var t = new TestDb();
        Assert.Equal(503, Executar(t).StatusCode);
    }

    [Fact]
    public void PrecoNaFronteiraDoLimiar_AindaEhAceito()
    {
        using var t = new TestDb();
        var coin = t.AddCoin("BTC");
        t.SetPrice(coin.Id, 600_000m);

        var snap = t.Db.PriceSnapshots.First(s => s.CoinId == coin.Id);
        snap.UpdatedAt = DateTimeOffset.UtcNow - HealthController.IdadeMaximaDoPreco + TimeSpan.FromMinutes(1);
        t.Db.SaveChanges();

        Assert.Equal(200, Executar(t).StatusCode);
    }

    [Fact]
    public void OMaisRecenteEhQueVale_NaoOMaisAntigo()
    {
        // Uma moeda parada não pode degradar o sistema inteiro se as outras estão atualizando;
        // e uma moeda atualizada não pode mascarar... o critério é o mais recente, e a
        // divergência por moeda é assunto do indicador de frescor na interface.
        using var t = new TestDb();
        var btc = t.AddCoin("BTC");
        var sol = t.AddCoin("SOL", Domain.Enums.RiskTier.Growth);
        t.SetPrice(btc.Id, 600_000m);
        t.SetPrice(sol.Id, 800m);

        var antigo = t.Db.PriceSnapshots.First(s => s.CoinId == btc.Id);
        antigo.UpdatedAt = DateTimeOffset.UtcNow.AddDays(-3);
        t.Db.SaveChanges();

        Assert.Equal(200, Executar(t).StatusCode);
    }
}
