# Invariantes financeiras — CryptoDash

Suíte que codifica as sete propriedades que precisam ser verdadeiras para o registro
financeiro do CryptoDash ser confiável. Ela existe para ser a **rede de proteção da Fase 0**:
as correções de transacionalidade, concorrência e sanidade de preço mexem exatamente na
aritmética que estes testes cobrem.

```bash
dotnet test tests/CryptoDash.Application.Tests
```

## Estado atual: 13 verdes · 16 vermelhos

**Os vermelhos são intencionais.** Eles asseveram o comportamento *correto*, não o atual.
Cada falha é um bug real da auditoria, reproduzido de forma determinística. À medida que a
Fase 0 avança, eles viram verdes — e é isso que dá o critério objetivo de conclusão.

Não faça um teste passar afrouxando a asserção. Se um deles precisar mudar, é porque a
regra de negócio mudou — e isso é uma decisão, não um ajuste.

## Placar por invariante

| # | Invariante | Estado | Bug que expõe |
|---|---|---|---|
| 1 | `Σ compras − Σ vendas == Holding.Quantity` | 🟢 3/3 | — (vale enquanto não há venda a descoberto) |
| 2 | `Σ alocações do DCA == aporte` | 🔴 5/9 | Ramo do primeiro aporte retorna antes da reconciliação de centavos |
| 3 | Custo médio determinístico com `ExecutedAt` empatado | 🔴 | **0 BTC vs 1 BTC** para o mesmo conjunto de transações |
| 4 | Delete reproduz a posição sem aquela transação | 🟢 | — |
| 5 | Venda > posição é rejeitada | 🔴 2/2 | `Math.Min` trunca em silêncio; razão e posição divergem |
| 6 | Custo médio nunca zera com quantidade > 0 | 🔴 2/2 | `Math.Round(totalCost/qty, 2)` destrói a base de custo |
| 7 | Variação 24h bate com o cálculo independente | 🔴 6/8 | Álgebra usa o preço de hoje como base do ontem |

## O que cada vermelho está dizendo

**Invariante 3 — o mais grave.** Uma compra e uma venda de 1 BTC com o mesmo `ExecutedAt`
produzem holding de **0 BTC** se a compra foi inserida primeiro e **1 BTC** se a venda foi.
O `OrderBy(t => t.ExecutedAt)` é estável, então quem desempata é a ordem de inserção.
Correção: critério de desempate determinístico (`ThenBy(t => t.Id)`) e tratar venda sem
posição como erro, não como no-op.

**Invariante 5 — a divergência sempre infla.** Comprar 1 BTC, vender 1,5 e comprar 1 deixa
o razão em 0,5 BTC e o holding em 1,0 BTC. O excedente some sem erro, e o erro é sempre a
favor de mostrar mais cripto do que existe.

**Invariante 6 — dois níveis.** O zeramento total (moeda a R$ 0,00007 → custo médio R$ 0,00)
é latente: só arma se alguém cadastrar moeda barata direto no banco. Mas o **6b é ativo
hoje**: uma posição de 430 MATIC reconstrói custo de R$ 541,80 contra R$ 541,69 realmente
gastos — R$ 0,11 de deriva, porque o serviço descarta o `totalCost` real e o resumo o
reconstrói a partir do custo médio arredondado.

**Invariante 7 — quantificado.** Com uma moeda a +35% em 24h, o cabeçalho reporta
R$ 10.500,00 de variação quando a real é R$ 7.777,78 — **35% a mais**. Com −40%, reporta
−R$ 12.000,00 quando a real é −R$ 20.000,00: **esconde R$ 8.000 de prejuízo**. E nos casos
extremos: a +100% exibe `0,00%`; a +150% exibe `−300%` ao lado de um valor positivo em verde.

**Invariante 2 — centavos, mas sistemáticos.** No primeiro aporte de um usuário novo a soma
não fecha: aporte de R$ 1.825,36 distribui R$ 1.825,37.

## Escopo e limites

Os testes usam **SQLite em memória com o `AppDbContext` real** — mesmo mapeamento EF, mesmos
conversores de valor, mesma tradução de query. Não são mocks.

SQLite é dinamicamente tipado e **não aplica `HasPrecision(18,2)`**. Portanto esta suíte
verifica a aritmética dos serviços, não a quantização feita pelo Postgres. Truncamento na
fronteira do banco precisa de teste de integração contra Postgres real.

## Próxima tranche

Fora do escopo de invariantes, mas antes de qualquer dinheiro real: idempotência do
`POST /aports/execute`, atomicidade do `ExecuteAsync`, lost update sob concorrência,
rejeição de preço inválido, e detecção de divergência razão↔posição.
