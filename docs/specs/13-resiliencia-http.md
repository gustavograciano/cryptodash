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
