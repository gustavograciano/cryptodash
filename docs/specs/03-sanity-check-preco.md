# 03 — Sanity check de preço

**Bloqueador #4.**

## Problema

`PriceSnapshotJob` grava no banco qualquer número que a CoinGecko devolver. Não há validação
alguma. `CoinGeckoPriceFeed` colapsa campo ausente em `?? 0m`, então um `brl` nulo vira
**preço R$ 0,00 persistido**, sobrescrevendo um preço bom.

Cadeia de dano verificada na auditoria: preço zerado → `DcaAllocator` concentra o aporte
inteiro na moeda quebrada (valor atual zero = maior gap) → execução recusa (após spec da
idempotência) ou, com preço 10× errado, registra **quantidade 10× errada** de forma permanente.

## Critério de pronto

- [ ] Preço `<= 0` é rejeitado e **não** sobrescreve o snapshot existente
- [ ] Variação absurda contra o último snapshot é rejeitada e registrada em log de warning
- [ ] Uma moeda com dado ruim não impede a atualização das demais
- [ ] `UpdatedAt` **não** é tocado quando o dado é rejeitado — senão dado velho parece fresco
- [ ] Testes: preço zero, preço negativo, preço 10× fora da banda, resposta parcial

## Abordagem

Guarda em `PriceSnapshotJob`, antes de aplicar cada cotação:

1. `quote.PriceBrl <= 0` → rejeita
2. Existe snapshot anterior com preço > 0 e `|novo/anterior - 1| > BANDA` → rejeita
   - Banda default 0,40 (40% em 15 min), configurável em `appsettings`
   - Cripto se move muito: banda apertada demais gera falso positivo. 40% em 15 minutos é
     evento extremo, não rotina.
3. Rejeição → `LogWarning` com símbolo, preço antigo e novo; segue para a próxima moeda
4. Primeira cotação de uma moeda (sem snapshot anterior) só passa pelo teste (1)

## Fora de escopo

Circuit breaker e retry das integrações (Polly) — é a Fase 1 do roadmap.
