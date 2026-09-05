# 02 — Fuso horário do ExecutedAt

**Achado durante a implementação da idempotência.** Corrompe todo registro novo.

## Problema

`AportExecuteModal.tsx:9` e `TransactionModal.tsx:15` fazem:

```ts
useState(new Date().toISOString().slice(0, 16))
```

Isso produz a hora **UTC** e a coloca em um `<input type="datetime-local">`, que o navegador
interpreta como hora **LOCAL**. No submit, `new Date(executedAt).toISOString()` reinterpreta
o valor como local e converte para UTC.

Verificado por simulação em `America/Sao_Paulo`:

```
instante real (UTC):     2026-09-05T17:00:00.000Z   (14:00 BRT)
valor exibido no input:  2026-09-05T17:00           <- usuário lê como 17:00 local
ExecutedAt enviado:      2026-09-05T20:00:00.000Z   <- 3 horas no futuro
```

Consequências: o campo mostra a hora errada ao usuário; todo `ExecutedAt` fica deslocado pelo
offset do fuso; e uma compra em 31/12 às 22h BRT é registrada em 01/01 — **ano fiscal errado**.

## Critério de pronto

- [ ] O campo abre com a hora **local** correta
- [ ] O `ExecutedAt` enviado corresponde ao instante real escolhido
- [ ] Vale para os dois modais
- [ ] Teste que trave o comportamento (round-trip local → ISO → local)

## Abordagem

Formatar o valor inicial a partir dos componentes locais, não do ISO em UTC:

```ts
const paraDatetimeLocal = (d: Date) => {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
};
```

O submit (`new Date(valor).toISOString()`) já está correto **desde que** o valor no input seja
local — que passa a ser.

Extrair para `frontend/src/lib/format.ts` e usar nos dois modais.

## Dados já gravados

Registros existentes têm o deslocamento embutido e **não** são corrigidos por esta spec —
seria preciso saber o fuso de cada lançamento. Documentar como pendência e avisar o usuário:
transações lançadas antes desta correção têm `ExecutedAt` adiantado pelo offset local.

---

## Verificação executada

Instalado `vitest` no frontend — não havia harness de teste algum. 6 casos em
`src/lib/format.test.ts`, verdes com `TZ=America/Sao_Paulo`.

Comparação direta das duas implementações, usuário escolhendo 05/09/2026 14:30 local:

| | valor no input | ExecutedAt enviado | desvio |
|---|---|---|---|
| Antigo | `2026-09-05T17:30` (errado) | `2026-09-05T20:30Z` | **+3h** |
| Novo | `2026-09-05T14:30` | `2026-09-05T17:30Z` | 0h |

Virada de ano, compra em 31/12 às 22h local:

| | data registrada |
|---|---|
| Antigo | **2027-01-01** — ano fiscal errado |
| Novo | 2026-12-31 |

**Status: concluída.** Dados já gravados não foram corrigidos — ver seção acima.
