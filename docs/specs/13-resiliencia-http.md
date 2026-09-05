# 13 — Resiliência HTTP das integrações

**Fase 1 do roadmap.** `grep -rn "Polly|resilience|AddRateLimiter" src/` retornava zero.

## Problema

As quatro integrações (CoinGecko, Alternative.me, CryptoPanic, Anthropic) não têm retry,
backoff, jitter nem circuit breaker. Um 503 momentâneo ou um TCP reset custa o ciclo inteiro:
15 minutos de preço, 24 horas de OHLCV.

`PriceHistoryJob` dispara N chamadas sequenciais sem espaçamento contra um free tier de
~10-30 req/min — 11 moedas em rajada, com risco de ban do IP.

## Critério de pronto

- [ ] Retry com backoff exponencial e jitter nas quatro integrações
- [ ] Circuit breaker que para de bater numa API que está fora
- [ ] Espaçamento entre chamadas no `PriceHistoryJob`
- [ ] Timeout continua existindo
- [ ] Teste que prove o retry (handler que falha N vezes e depois responde)

## Abordagem

`Microsoft.Extensions.Http.Resilience` — o sucessor do `Polly.Extensions.Http` no .NET 8 —
aplicado no registro dos clientes Refit em `Infrastructure/DependencyInjection.cs`.

Parâmetros distintos por integração: a Anthropic é cara e lenta (menos tentativas); a CoinGecko
é gratuita e limitada (mais espaçamento, menos concorrência).

## Fora de escopo

Substituir o `catch (Exception)` genérico dos feeds por taxonomia de erro — muda a assinatura
de todos eles e merece spec própria.

---

## Verificação executada

5 casos em `ResilienceTests`, exercitando a **mesma** política que a produção usa
(`ResilienciaHttp.Aplicar`), com atrasos de 1 ms para não pagar o backoff real — o que se
verifica é a decisão de repetir, não a duração da espera.

| Caso | Resultado |
|---|---|
| 503 nas duas primeiras chamadas | 3 tentativas, resposta 200 |
| 503 permanente | 4 tentativas (1 + 3) e para |
| 400 | **1 tentativa** — não melhora com repetição, repetir só gasta cota |
| 429 (rate limit da CoinGecko) | tratado como transitório, 2 tentativas |
| Os quatro clientes externos | continuam resolvendo pela DI |

Parâmetros por integração: CoinGecko e Alternative.me com 3 tentativas; CryptoPanic com 2;
**Anthropic com 1** — repetir uma geração que já consumiu tokens é pagar duas vezes pelo mesmo
resultado.

`PriceHistoryJob` ganhou 3 s de espaçamento entre moedas: onze chamadas em rajada contra um
free tier de ~10-30 req/min tinham risco real de ban do IP, que derruba todo o pipeline de
preço, não só aquele job.

### Nota de implementação

A política foi extraída para `ResilienciaHttp` em vez de ficar privada no
`DependencyInjection`. O motivo é testabilidade: o Refit nomeia o cliente pelo nome completo do
tipo, e a primeira tentativa de teste — que substituía o transporte por nome — silenciosamente
criava um cliente **sem** a pilha de resiliência e passava a impressão de que o retry não
funcionava.

**Status: concluída.**
