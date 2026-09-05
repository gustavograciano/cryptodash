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
