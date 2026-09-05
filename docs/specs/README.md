# Specs — Fase 0: bloqueadores para dinheiro real

Cada spec descreve **um** problema verificado no código, o critério objetivo de pronto e os
testes que provam a correção. A ordem é de execução, definida por dano × esforço.

Origem: auditoria de prontidão para dinheiro real. Os itens abaixo são o que restou da seção
"NÃO COLOQUE DINHEIRO REAL ANTES DE RESOLVER ISTO" depois das correções já entregues na
branch `test/financial-invariants` (invariantes 3, 5 e 7 + idempotência e atomicidade do
aporte).

| # | Spec | Bloqueador | Estado |
|---|---|---|---|
| 01 | [Backup verificável](01-backup-verificavel.md) | #6 backup falha em silêncio | ✅ |
| 02 | [Fuso horário do ExecutedAt](02-fuso-horario-executedat.md) | achado novo | ⬜ |
| 03 | [Sanity check de preço](03-sanity-check-preco.md) | #4 | ⬜ |
| 04 | [Atomicidade e lost update](04-atomicidade-portfolio.md) | #3 | ⬜ |
| 05 | [Reconciliação razão↔posição](05-reconciliacao.md) | #1 | ⬜ |
| 06 | [Endurecimento de segurança](06-seguranca.md) | #7, #8 | ⬜ |
| 07 | [Base de custo sem deriva](07-base-de-custo.md) | invariante 6 | ⬜ |
| 08 | [Centavos do primeiro aporte](08-centavos-dca.md) | invariante 2 | ⬜ |
| 09 | [Frescor e alerta](09-frescor-e-alerta.md) | #9 | ⬜ |

## Regras para estas specs

- Nenhuma spec é considerada pronta sem teste que **falhe antes e passe depois**, salvo
  quando o item for infraestrutura não testável em unidade (spec 01) — nesse caso, o
  critério é verificação manual documentada.
- Não afrouxar asserção existente para fazer spec passar. A suíte em
  `tests/CryptoDash.Application.Tests` é a linha de base: hoje **38 verdes / 7 vermelhos**.
- Os 7 vermelhos atuais são as invariantes 2 e 6, endereçadas pelas specs 07 e 08.
