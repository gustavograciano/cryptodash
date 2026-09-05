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
