# 04 — Atomicidade e lost update no PortfolioService

**Bloqueador #3.** O aporte já foi corrigido; o lançamento manual não.

## Problema

Dois defeitos no mesmo lugar.

**Atomicidade.** `CreateTransactionAsync` (linhas 176/179) e `DeleteTransactionAsync`
(208/210) fazem `SaveChanges` → `HoldingRecalculator.ApplyAsync` → `SaveChanges`, sem
transação. Falha entre os dois deixa a transação gravada e a posição não recalculada — o
usuário vê a compra listada e o patrimônio não muda.

**Lost update.** `ApplyAsync` é read-modify-write do razão inteiro da moeda. Nenhuma entidade
tem token de concorrência. Duas requisições concorrentes na mesma moeda:

```
T1: INSERT transação A ✓
T2: INSERT transação B ✓
T1: lê razão → [A]        (não vê B)
T2: lê razão → [A, B]
T2: UPDATE holding = 0.02 ✓
T1: UPDATE holding = 0.01   ← sobrescreve; 0.01 BTC some
```

A janela **aumentou** com a transação do aporte: ele agora segura o UPDATE de holdings por
mais tempo antes de commitar.

## Critério de pronto

- [ ] `CreateTransactionAsync` e `DeleteTransactionAsync` em uma única transação
- [ ] `Holding` com token de concorrência; escrita conflitante falha em vez de sobrescrever
- [ ] Conflito é reprocessado (retry) e não vaza erro para o usuário no caso benigno
- [ ] Teste que reproduza o interleaving e prove que o razão e a posição não divergem
- [ ] Invariantes 1, 3, 4 e 5 continuam verdes

## Abordagem

1. Mesmo padrão do `ExecuteAsync`: cast para `DbContext`, `BeginTransactionAsync`,
   `ChangeTracker.Clear()` em todo caminho que não commita. Como esta é a **segunda**
   ocorrência do cast, promover para um helper em `Application/Common` — era exatamente o
   critério da proposta aprovada ("abstração depois da segunda ocorrência").
2. `Holding.RowVersion` como concurrency token. No Postgres, `xmin`
   (`.IsRowVersion()` via `UseXminAsConcurrencyToken()`); no SQLite dos testes não há
   equivalente nativo — usar um `byte[]`/contador gerenciado pela aplicação para que o
   mecanismo seja o mesmo nos dois providers.
3. `catch (DbUpdateConcurrencyException)` → recarregar e repetir o replay, limitado a N
   tentativas.

## Nota

`DbUpdateConcurrencyException` **herda** de `DbUpdateException`. O `catch (DbUpdateException)`
do `ExecuteAsync` a captura hoje; verificar que o fluxo de idempotência não a engole.
