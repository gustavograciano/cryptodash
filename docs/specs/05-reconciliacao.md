# 05 — Reconciliação razão↔posição

**Bloqueador #1.** É o multiplicador de severidade de todos os outros: cada race, crash ou
delete produz erro silencioso e permanente porque nada compara.

## Problema

`grep -rn "reconcil" src/` retorna **zero**. Não existe job, endpoint, query ou log que
compare `Σ transações` com `Holding.Quantity`. Quando divergem, o sistema não percebe, não
corrige, não bloqueia e não alerta.

Agravante descoberto na spec 04: `GetSummaryAsync` filtra `Where(h => h.Quantity > 0)`, então
uma posição **negativa** — que é justamente o sinal de razão corrompido — **some do
dashboard** em vez de gritar.

E não existe caminho de reparo: `HoldingRecalculator.ApplyAsync` só é chamado de dentro de
`CreateTransactionAsync`, `DeleteTransactionAsync` e `ExecuteAsync`. Não há como recalcular
uma posição divergente sem reeditar transações.

## Critério de pronto

- [ ] Verificação que compara, por moeda, `Σ compras − Σ vendas` com `Holding.Quantity`
- [ ] Divergência é registrada com severidade de erro, identificando moeda e magnitude
- [ ] Endpoint de reparo que recalcula a posição a partir do razão
- [ ] Posição negativa ou divergente **aparece** no dashboard em vez de ser filtrada
- [ ] Testes: forçar divergência via escrita direta e provar que é detectada e reparável

## Abordagem

1. `ReconciliationService` em Application, com `CheckAsync()` devolvendo a lista de
   divergências (coinId, símbolo, quantidade no razão, quantidade na posição, diferença).
2. `ReconciliationJob` (Quartz) de hora em hora, chamando `CheckAsync` e logando em `Error`
   quando houver divergência.
3. `GET /api/v1/portfolio/reconciliation` — expõe o resultado.
4. `POST /api/v1/portfolio/holdings/recalculate` — reexecuta `ApplyAsync` por moeda, dentro
   de transação. É o único reparo do sistema para toda essa classe de erro.
5. `GetSummaryAsync` deixa de filtrar `Quantity > 0`: passa a filtrar `Quantity != 0`, para
   que posição negativa apareça. Conferir o efeito nas invariantes 7d/7e.

## Fora de escopo

Reconciliação contra fonte externa (corretora) — não há integração e não haverá nesta fase.

---

## Verificação executada

7 casos em `ReconciliationTests`, verdes. Suíte total: **72 casos, 65 verdes**; os 7 vermelhos
seguem sendo as invariantes 2 e 6.

Entregue:

- `ReconciliationService.CheckAsync` — compara por moeda, detecta divergência de quantidade
  **e** posição ausente para moeda com razão
- `ReconciliationService.RecalculateAsync(coinId?)` — reparo, dentro de transação
- `ReconciliationJob` — de hora em hora, log em `Error` nomeando moeda, magnitude e a ação
  corretiva
- `GET /api/v1/portfolio/reconciliation` — **409** quando há divergência, não 200: um 200
  faria monitoramento externo tratar o estado corrompido como normal
- `POST /api/v1/portfolio/holdings/recalculate`
- `GetSummaryAsync` passou de `Quantity > 0` para `Quantity != 0`

O sinal da diferença é informação: positivo significa que a posição afirma ter **mais** cripto
do que o razão comprova — a direção que infla o patrimônio exibido.

A soma é feita no cliente de propósito: `SUM` sobre `decimal` no SQLite devolve valor errado.

**Status: concluída.**
