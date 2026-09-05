# 12 — Dados de mercado deixam de ser fabricados

Três defeitos que injetam número inventado na base que alimenta os "Sinais técnicos".

## Problemas

**1. Volume diário inflado ~24×.** `CoinGeckoMarketDataFeed` soma todos os pontos de
`total_volumes` do dia:

```csharp
byDate[date] = (current.open, current.high, current.low, current.close, current.volume + v[1]);
```

Mas `total_volumes` da CoinGecko é **volume rolante de 24h**, não incremental. Somar 24 leituras
horárias de uma média móvel produz ~24× o valor real.

**2. OHLC fabricado.** Com `days > 90` a CoinGecko devolve **um ponto por dia**, então
`open = high = low = close`. A coluna `high_brl` guarda um fechamento e é exibida como máxima —
a mesma coluna tem semânticas diferentes conforme a granularidade da janela.

**3. `MarketIndicatorsJob` persiste dado inventado quando o feed falha:**

```csharp
FearGreedIndex = fear?.Value ?? 50,
FearGreedLabel = fear?.Label ?? "Neutral",
BtcDominancePct = global?.BtcDominancePct ?? 0,
```

"Fear & Greed 50 / Neutral" e "BTC Dominance 0,00%" aparecem no painel como leitura de mercado,
indistinguíveis de dado real.

## Critério de pronto

- [ ] Volume do dia deixa de ser soma de leituras rolantes
- [ ] Vela sem intraday é marcada como tal
- [ ] Falha de feed **não** grava indicador: a linha não existe em vez de conter ficção
- [ ] Testes cobrindo os três casos

## Abordagem

1. **Volume:** usar a **última** leitura do dia — o rolante de 24h ao fim do dia é a melhor
   aproximação disponível — em vez da soma.
2. **OHLC:** gravar `HasIntraday` na linha, para que o consumidor saiba se high/low são reais
   ou repetição do fechamento.
3. **Indicadores:** se `fear` for nulo, não escrever esses campos; se ambos os feeds falharem,
   não gravar linha nenhuma e logar em `Warning`.

---

## Verificação executada

5 casos em `MarketDataFabricationTests`, com cliente falso da CoinGecko — sem rede.

| Caso | Antes | Agora |
|---|---|---|
| 4 leituras rolantes de ~500 mil | volume 2.025.000 (soma) | **520.000** (última leitura) |
| Vela com 2+ pontos | indistinguível | `HasIntraday = true`, high/low reais |
| Vela com 1 ponto | high/low = fechamento, sem aviso | `HasIntraday = false` |
| Preço de R$ 0,00007 | zerado pelo arredondamento em 2 casas | preservado (10 casas) |

`MarketIndicatorsJob` deixou de gravar ficção: sem nenhum feed, **não grava linha** e loga em
`Warning`; sem Fear & Greed e sem linha existente, não cria linha só com dominância, porque a
data da linha vem do próprio Fear & Greed e seria palpite.

### Dado histórico

O default `false` de `has_intraday` é conservador: linhas antigas não passam a alegar extremos
que não se pode provar. O `PriceHistoryJob` reescreve a janela recente a cada execução, então
o histórico próximo se corrige sozinho.

**Volume antigo permanece inflado** em até ~24× fora dessa janela. Não alimenta nenhum
indicador hoje — `IndicatorCalculator` usa apenas fechamentos —, mas está registrado.

**Status: concluída.**
