# 15 — Correções de interface

Defeitos verificados que fazem a tela mentir.

## Problemas

**1. Medidor Fear & Greed com o ponteiro 90° fora.** `FearGreedGauge.tsx:2`:

```tsx
const angle = -90 + (value / 100) * 180;
```

Varre de −90° a +90° — a metade **direita** do círculo. Mas o arco desenhado é o semicírculo
**superior** (`M10,100 A90,90 0 0,1 190,100`). Com índice 50 ("Neutral") a agulha aponta para o
extremo de ganância; com 100 aponta para baixo, **fora da `viewBox`**.

**2. `PriceChartModal` apresenta falha de API como ausência de dado.** O `error` do `useQuery`
não é desestruturado: em 500, 401 ou rede caída, `data` cai no default `[]` e a tela diz
"Sem histórico — aguarde o job rodar", instruindo o usuário a esperar por algo que não vem.

**3. A coluna "Desvio" mistura duas unidades.** O backend calcula desvio **relativo**
(`(atual − alvo)/alvo`) e a tabela o imprime com o mesmo formatador percentual das colunas
"Atual" e "Alvo", que são percentuais **absolutos** do portfólio. Nada na tela distingue os dois.

## Critério de pronto

- [ ] Índice 0 aponta para a esquerda, 50 para cima, 100 para a direita
- [ ] Falha de API no gráfico aparece como erro, distinta do estado vazio
- [ ] A coluna de desvio deixa claro que é relativa ao alvo
- [ ] Teste para o cálculo do ângulo

## Abordagem

1. `angle = 180 + (value / 100) * 180` — varre o semicírculo superior da esquerda para a
   direita, que é o arco efetivamente desenhado. Extrair para função pura e testar.
2. Desestruturar `error` e renderizar estado de erro distinto do vazio.
3. Rotular a coluna como desvio relativo ao alvo.

---

## Verificação executada

7 casos em `FearGreedGauge.test.ts`. Frontend: **18/18**.

O ângulo foi extraído para `anguloDoPonteiro`, função pura e testada:

| Índice | Antes | Agora |
|---|---|---|
| 0 (medo extremo) | apontava para cima | esquerda, `x ≈ 30` |
| 50 (neutro) | **direita — extremo de ganância** | cima, `y ≈ 30` |
| 100 (ganância extrema) | **para baixo, `y = 170`, fora da viewBox** | direita, `x ≈ 170` |

Um teste varre os 101 valores e exige que a ponta nunca saia da metade superior do desenho;
outro prova o bug antigo diretamente, comparando as duas fórmulas. Valores fora de 0–100 são
presos aos extremos — o índice vem de fonte externa e um valor absurdo não pode jogar a agulha
para fora.

`PriceChartModal` passou a desestruturar `isError` e mostrar mensagem de falha distinta do
estado vazio: antes, erro de API caía no ramo "Sem histórico — aguarde o job rodar" e mandava
o usuário esperar por algo que não viria.

A coluna passou a se chamar **"Desvio rel. ao alvo"**, com `title` explicando a fórmula — ela
é uma razão sobre o alvo, e estava sendo impressa com o mesmo formatador das colunas "Atual" e
"Alvo", que são percentuais absolutos do portfólio.

**Status: concluída.**
