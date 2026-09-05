# 07 — Base de custo sem deriva

**Invariante 6.** Dois testes vermelhos hoje: `Invariante6` e `Invariante6b`.

## Problema

`HoldingRecalculator.Replay` calcula o `totalCost` real e depois **descarta**, persistindo
apenas `Math.Round(totalCost / qty, 2)`. `GetSummaryAsync` então **reconstrói** o custo como
`Quantity * AvgCostBrl`. O erro por unidade (até R$ 0,005) é multiplicado pela quantidade.

Dois níveis:

- **Ativo hoje.** 430 MATIC reconstroem custo de R$ 541,80 contra R$ 541,69 realmente gastos.
  R$ 0,11 de deriva com o universo de moedas atual.
- **Latente.** Moeda a R$ 0,00007 → custo médio arredonda para R$ 0,00 → base de custo
  destruída, e o P&L passa a reportar a posição inteira como lucro. Só arma se alguém
  cadastrar moeda barata direto no banco, que é o único caminho existente para cadastrar moeda.

## Critério de pronto

- [ ] `Invariante6` e `Invariante6b` verdes
- [ ] Custo total da posição é um **fato persistido**, não derivado de média arredondada
- [ ] `AvgCostBrl` continua existindo para exibição, mas deixa de ser fonte da verdade
- [ ] Invariantes 1, 3, 4, 5 e 7 continuam verdes
- [ ] Migration aplicada e verificada contra Postgres real

## Abordagem

1. `Holding.TotalCostBrl` (`numeric(20,10)`) persistido diretamente do `Replay`
2. `GetSummaryAsync` usa `h.TotalCostBrl` em vez de `h.Quantity * h.AvgCostBrl`
3. `AvgCostBrl` passa a ser derivado para exibição; manter a coluna para não quebrar a UI
4. Migration: coluna nova anulável → backfill `quantity * avg_cost_brl` → NOT NULL.
   **Atenção:** o backfill carrega a deriva existente. É o melhor conhecido; aportes futuros
   ficam corretos. Documentar.
5. Aumentar a escala de `PriceBrl`/`AvgCostBrl` para acomodar moeda sub-centavo é mudança
   maior — avaliar dentro desta spec ou registrar como pendência separada.

---

## Verificação executada

`Invariante6` e `Invariante6b` **verdes**. Suíte: 91 casos, 86 verdes — restam só as 5 da
invariante 2 (spec 08).

Duas mudanças, e as duas eram necessárias:

1. **`Holding.TotalCostBrl` persistido.** O custo total real era calculado e descartado; o
   resumo o reconstruía como `Quantity * AvgCostBrl`, multiplicando o erro de arredondamento
   pela quantidade.
2. **Precisão de custo e preço de `(18,2)` para `(20,10)`**, e a média deixou de ser
   arredondada em 2 casas. Sem isso a `Invariante6` continuaria vermelha: moeda a R$ 0,00007
   arredondava a média para R$ 0,00 e o P&L reportava a posição inteira como lucro.

### Cuidado com a mudança de precisão

`numeric(18,2)` tem 16 dígitos inteiros; `numeric(20,10)` tem 10. A parte decimal cresce — que
é o objetivo — mas a **inteira encolhe**. O teto por unidade passa a ~10 bilhões de reais,
folgado para preço unitário de cripto, e é a razão pela qual a narrowing é aceitável.

### Backfill verificado contra Postgres

O scaffold gerava `defaultValue: 0m`, o que deixaria toda posição existente com custo ZERO e
faria o P&L reportar a posição inteira como lucro. Adicionado
`UPDATE holdings SET total_cost_brl = quantity * avg_cost_brl`.

Testado com uma posição pré-existente de 0,5 BTC a R$ 500.000: resultou em
`total_cost_brl = 250000.0000000000`.

O backfill carrega a deriva de arredondamento antiga — é o único dado disponível. Para
eliminá-la, rodar `POST /api/v1/portfolio/holdings/recalculate` depois do deploy, que
reconstrói o custo a partir do razão.

**Status: concluída.**
