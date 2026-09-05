using System.Net;
using CryptoDash.Infrastructure;
using CryptoDash.Infrastructure.External;
using CryptoDash.Infrastructure.External.AlternativeMe;
using CryptoDash.Infrastructure.External.Anthropic;
using CryptoDash.Infrastructure.External.CoinGecko;
using CryptoDash.Infrastructure.External.CryptoPanic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CryptoDash.Application.Tests;

/// <summary>
/// Spec 13 — as integrações externas passam a sobreviver a falha transitória.
///
/// Antes, um 503 momentâneo ou um TCP reset custava o ciclo inteiro: 15 minutos de preço,
/// 24 horas de OHLCV.
/// </summary>
public class ResilienceTests
{
    /// <summary>Falha as N primeiras chamadas e depois responde, contando as tentativas.</summary>
    private sealed class HandlerInstavel(int falhasIniciais, HttpStatusCode statusDeFalha) : HttpMessageHandler
    {
        public int Tentativas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Tentativas++;
            var status = Tentativas <= falhasIniciais ? statusDeFalha : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    /// <summary>
    /// Monta um cliente com a MESMA política que a produção usa. Atrasos minúsculos para o
    /// teste não pagar o backoff real — o que se verifica aqui é a decisão de repetir, não a
    /// duração da espera.
    /// </summary>
    private static (HttpClient Client, HandlerInstavel Handler) ClienteCom(
        int falhas, int tentativas = 3, HttpStatusCode status = HttpStatusCode.ServiceUnavailable)
    {
        var handler = new HandlerInstavel(falhas, status);
        var services = new ServiceCollection();

        var builder = services.AddHttpClient("teste", c => c.BaseAddress = new Uri("https://exemplo.invalido"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        ResilienciaHttp.Aplicar(builder, tentativas, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(30));

        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<IHttpClientFactory>().CreateClient("teste"), handler);
    }

    [Fact]
    public async Task FalhaTransitoria_EhRepetidaAteObterResposta()
    {
        var (client, handler) = ClienteCom(falhas: 2);

        var r = await client.GetAsync("/simple/price");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(3, handler.Tentativas);   // 2 falhas + 1 sucesso
    }

    [Fact]
    public async Task FalhaPersistente_ParaDeTentar_EmVezDeInsistirParaSempre()
    {
        var (client, handler) = ClienteCom(falhas: int.MaxValue, tentativas: 3);

        var r = await client.GetAsync("/simple/price");

        Assert.NotEqual(HttpStatusCode.OK, r.StatusCode);
        // Insistir sem teto num free tier é o caminho para o ban de IP.
        Assert.Equal(4, handler.Tentativas);   // 1 original + 3 repetições
    }

    [Fact]
    public async Task ErroDeCliente_NaoEhRepetido()
    {
        // 400 não melhora com repetição — repetir só gasta cota.
        var (client, handler) = ClienteCom(falhas: 1, status: HttpStatusCode.BadRequest);

        await client.GetAsync("/simple/price");

        Assert.Equal(1, handler.Tentativas);
    }

    [Fact]
    public async Task RateLimit429_EhTratadoComoTransitorio()
    {
        // 429 é o que a CoinGecko devolve sob rate limit: esperar e repetir é a resposta certa.
        var (client, handler) = ClienteCom(falhas: 1, status: HttpStatusCode.TooManyRequests);

        var r = await client.GetAsync("/simple/price");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(2, handler.Tentativas);
    }

    [Fact]
    public void QuatroClientesExternos_ContinuamResolvendo()
    {
        // Guarda contra alguém adicionar integração nova sem passar pela política.
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Data Source=:memory:",
                ["Database:Provider"] = "Sqlite",
            }).Build());

        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<ICoinGeckoApi>());
        Assert.NotNull(sp.GetRequiredService<IAlternativeMeApi>());
        Assert.NotNull(sp.GetRequiredService<ICryptoPanicApi>());
        Assert.NotNull(sp.GetRequiredService<IAnthropicApi>());
    }
}
