using CryptoDash.Infrastructure.Configuration;
using Xunit;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Spec 06 — o boot precisa falhar alto quando a configuração deixaria o sistema aberto.
///
/// Os dois guards anteriores eram inalcançáveis na prática: o <c>?? throw</c> da chave JWT
/// nunca disparava porque o <c>appsettings.json</c> sempre fornecia valor, e o
/// <c>?? "ChangeMe@123"</c> da senha não dispara para string vazia — <c>""</c> não é
/// <c>null</c> — de modo que um deploy com variável faltando criava a única conta do sistema
/// com senha VAZIA.
/// </summary>
public class StartupValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChaveJwtAusente_DerrubaOBoot(string? chave)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => StartupValidation.ValidarChaveJwt(chave));
        Assert.Contains("Jwt:Key", ex.Message);
    }

    [Fact]
    public void ChaveJwtCurta_DerrubaOBoot()
    {
        // HMAC-SHA256 precisa de 256 bits; chave curta é falsa sensação de segurança.
        var ex = Assert.Throws<InvalidOperationException>(() => StartupValidation.ValidarChaveJwt("curta-demais"));
        Assert.Contains("32", ex.Message);
    }

    [Theory]
    [InlineData("dev-only-key-change-me-please-min-32-characters-long")]
    [InlineData("change-me-min-32-chars-please-please-please")]
    [InlineData("generate-with-openssl-rand-hex-32")]
    public void ChaveJwtDeExemplo_DerrubaOBoot(string chave)
    {
        // Estas chaves estão publicadas no repositório: qualquer pessoa forjaria um token.
        var ex = Assert.Throws<InvalidOperationException>(() => StartupValidation.ValidarChaveJwt(chave));
        Assert.Contains("exemplo", ex.Message);
    }

    [Fact]
    public void ChaveJwtValida_Passa()
    {
        StartupValidation.ValidarChaveJwt(new string('a', 64));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SenhaDeSeedVazia_DerrubaOBoot(string? senha)
    {
        // O caso real: `${SEED_PASSWORD}` sem `:?` no compose vira string vazia.
        var ex = Assert.Throws<InvalidOperationException>(() => StartupValidation.ValidarSenhaDeSeed(senha));
        Assert.Contains("SEM senha", ex.Message);
    }

    [Fact]
    public void SenhaDeSeedCurta_DerrubaOBoot()
    {
        Assert.Throws<InvalidOperationException>(() => StartupValidation.ValidarSenhaDeSeed("abc"));
    }

    [Theory]
    [InlineData("ChangeMe@123")]
    [InlineData("changeme")]
    [InlineData("PASSWORD")]
    public void SenhaDeExemplo_DerrubaOBoot(string senha)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => StartupValidation.ValidarSenhaDeSeed(senha));
        Assert.Contains("exemplo", ex.Message);
    }

    [Fact]
    public void SenhaValida_Passa()
    {
        StartupValidation.ValidarSenhaDeSeed("uma-senha-de-verdade-9134");
    }

    // ── Development ──────────────────────────────────────────────────────────
    // A recusa de valor de exemplo é inegociável fora de Development, mas ali só quebraria
    // o loop local: `docker compose up` e `dotnet run` usam justamente esses valores.

    [Fact]
    public void EmDevelopment_ValorDeExemplo_EhAceito()
    {
        StartupValidation.ValidarChaveJwt(
            "dev-only-key-change-me-please-min-32-characters-long", permitirValorDeExemplo: true);
        StartupValidation.ValidarSenhaDeSeed("ChangeMe@123", permitirValorDeExemplo: true);
    }

    [Fact]
    public void EmDevelopment_VaziosContinuamDerrubandoOBoot()
    {
        // O que a spec protege é a conta sem senha e a chave ausente — isso vale em todo
        // ambiente, senão o dev roda com um sistema diferente do que vai para produção.
        Assert.Throws<InvalidOperationException>(
            () => StartupValidation.ValidarChaveJwt("", permitirValorDeExemplo: true));
        Assert.Throws<InvalidOperationException>(
            () => StartupValidation.ValidarSenhaDeSeed("", permitirValorDeExemplo: true));
    }

    [Fact]
    public void EmDevelopment_ChaveCurtaContinuaDerrubandoOBoot()
    {
        // HMAC-SHA256 lançaria em runtime de qualquer forma; falhar no boot é melhor.
        Assert.Throws<InvalidOperationException>(
            () => StartupValidation.ValidarChaveJwt("curta", permitirValorDeExemplo: true));
    }
}
