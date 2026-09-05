using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace CryptoDash.Infrastructure.External;

/// <summary>
/// Política de resiliência aplicada a toda integração externa.
///
/// <para>
/// Sem isto, um 503 momentâneo ou um TCP reset custava o ciclo inteiro: 15 minutos de preço,
/// 24 horas de OHLCV.
/// </para>
/// <para>
/// O jitter existe para que várias moedas falhando ao mesmo tempo não repitam em bloco e
/// transformem a recuperação numa nova rajada. O circuit breaker evita continuar batendo numa
/// API que está fora — que, num free tier, é justamente o caminho para o ban de IP.
/// </para>
/// </summary>
public static class ResilienciaHttp
{
    internal static IHttpClientBuilder Aplicar(
        IHttpClientBuilder builder, int tentativas, TimeSpan atrasoBase, TimeSpan timeoutTotal)
    {
        builder.AddResilienceHandler("externa", p =>
        {
            p.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = tentativas,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = atrasoBase,
            });

            p.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromSeconds(60),
                BreakDuration = TimeSpan.FromSeconds(30),
            });

            p.AddTimeout(timeoutTotal);
        });

        return builder;
    }
}
