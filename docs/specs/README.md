# Specs — prontidão para dinheiro real

Cada spec descreve **um** problema verificado no código, o critério objetivo de pronto e os
testes que provam a correção. A ordem é de execução, definida por dano × esforço.

Origem: auditoria de prontidão para dinheiro real. Os itens abaixo são o que restou da seção
"NÃO COLOQUE DINHEIRO REAL ANTES DE RESOLVER ISTO" depois das correções já entregues na
branch `test/financial-invariants` (invariantes 3, 5 e 7 + idempotência e atomicidade do
aporte).

## Fase 0 — bloqueadores

| # | Spec | Bloqueador | Estado |
|---|---|---|---|
| 01 | [Backup verificável](01-backup-verificavel.md) | #6 backup falha em silêncio | ✅ |
| 02 | [Fuso horário do ExecutedAt](02-fuso-horario-executedat.md) | achado novo | ✅ |
| 03 | [Sanity check de preço](03-sanity-check-preco.md) | #4 | ✅ |
| 04 | [Atomicidade e lost update](04-atomicidade-portfolio.md) | #3 | ✅ |
| 05 | [Reconciliação razão↔posição](05-reconciliacao.md) | #1 | ✅ |
| 06 | [Endurecimento de segurança](06-seguranca.md) | #7, #8 | ✅ |
| 07 | [Base de custo sem deriva](07-base-de-custo.md) | invariante 6 | ✅ |
| 08 | [Centavos do primeiro aporte](08-centavos-dca.md) | invariante 2 | ✅ |
| 09 | [Frescor e alerta](09-frescor-e-alerta.md) | #9 | ✅ |

## Fase 1 — confiabilidade do dado e da operação

| # | Spec | Origem | Estado |
|---|---|---|---|
| 10 | [Segredos fora do versionamento](10-segredos-fora-do-versionamento.md) | auditoria de segurança | ✅ |
| 11 | [TLS renovável e cabeçalhos](11-tls-renovavel-e-cabecalhos.md) | CRIT-20 | ⚠️ |
| 12 | [Dados de mercado fabricados](12-dados-de-mercado-fabricados.md) | CRIT-14/15/16 | ✅ |
| 13 | [Resiliência HTTP](13-resiliencia-http.md) | INT-01 | ⬜ |
| 14 | [Validação e conteúdo de terceiros](14-validacao-e-conteudo-de-terceiros.md) | SEC-06 | ⬜ |
| 15 | [Correções de interface](15-correcoes-de-interface.md) | FE | ⬜ |

⚠️ = implementada, verificação pendente.

## Regras para estas specs

- Nenhuma spec é considerada pronta sem teste que **falhe antes e passe depois**, salvo
  quando o item for infraestrutura não testável em unidade (spec 01) — nesse caso, o
  critério é verificação manual documentada.
- Não afrouxar asserção existente para fazer spec passar. A suíte em
  `tests/CryptoDash.Application.Tests` é a linha de base: hoje **96 verdes / 0 vermelhos** (.NET) + 11 (frontend).
- As invariantes 2 e 6 foram fechadas pelas specs 08 e 07 — a suíte está inteiramente verde.
