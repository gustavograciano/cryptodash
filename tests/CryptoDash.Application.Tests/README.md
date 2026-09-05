# Invariantes financeiras — CryptoDash

Suíte que codifica as sete propriedades que precisam ser verdadeiras para o registro
financeiro do CryptoDash ser confiável. Ela existe para ser a **rede de proteção da Fase 0**:
as correções de transacionalidade, concorrência e sanidade de preço mexem exatamente na
aritmética que estes testes cobrem.

```bash
dotnet test tests/CryptoDash.Application.Tests
```

## Estado atual: 38 verdes · 7 vermelhos

> Invariantes **3, 5 e 7 corrigidas**. Restam 2 e 6.
>
> Somam-se 14 testes de **execução de aporte** (`AportExecutionInvariantTests`), todos verdes:
> idempotência, atomicidade e validação server-side. Ver a seção no fim.

## Armadilhas da fixture (leia antes de escrever teste novo)

**`SumAsync` sobre coluna `decimal` não é confiável no SQLite.** O provider guarda decimal
como TEXT e a agregação em SQL devolve valor errado — um teste chegou a comparar 300 contra
210. Some no cliente: `(await q.ToListAsync()).Sum(x => x.Campo)`.

**Uma única conexão compartilhada.** `TestDb` abre uma `SqliteConnection(":memory:")` e todos
os contextos a compartilham. Isso significa que um contexto enxerga escritas **não
commitadas** de outro — logo a suíte não consegue representar honestamente duas requisições
genuinamente simultâneas. Corrida real só contra Postgres, em teste de integração.

**Os vermelhos são intencionais.** Eles asseveram o comportamento *correto*, não o atual.
Cada falha é um bug real da auditoria, reproduzido de forma determinística. À medida que a
Fase 0 avança, eles viram verdes — e é isso que dá o critério objetivo de conclusão.

Não faça um teste passar afrouxando a asserção. Se um deles precisar mudar, é porque a
regra de negócio mudou — e isso é uma decisão, não um ajuste.

## Placar por invariante

| # | Invariante | Estado | Bug |
|---|---|---|---|
| 1 | `Σ compras − Σ vendas == Holding.Quantity` | 🟢 3/3 | — |
| 2 | `Σ alocações do DCA == aporte` | 🔴 5/9 | Ramo do primeiro aporte retorna antes da reconciliação de centavos |
| 3 | Custo médio determinístico com `ExecutedAt` empatado | 🟢 2/2 | **corrigido** |
| 4 | Delete reproduz a posição sem aquela transação | 🟢 | — |
| 5 | Venda > posição é rejeitada | 🟢 3/3 | **corrigido** |
| 6 | Custo médio nunca zera com quantidade > 0 | 🔴 2/2 | `Math.Round(totalCost/qty, 2)` destrói a base de custo |
| 7 | Variação 24h bate com o cálculo independente | 🟢 8/8 | **corrigido** |

## Invariantes 3 e 5 — como foram corrigidas

Ambas viviam no mesmo `RecalculateHoldingAsync`, que existia **duplicado literalmente** em
`PortfolioService` e `AportsService`. As duas cópias foram substituídas por
`HoldingRecalculator`, fonte única da verdade.

**Ordem canônica do razão.** O replay agora ordena por `ExecutedAt`, depois **compras antes
de vendas no mesmo instante**, depois `Id`. O critério do meio é o que torna o resultado
independente da ordem de digitação: não se pode alienar o que ainda não foi adquirido, então
processar a aquisição primeiro é a única leitura coerente de dois eventos carimbados no mesmo
momento. O `ThenBy(t => t.Id)` fecha o desempate para operações do mesmo tipo — sem ele o
resultado dependeria da ordem em que o banco devolve as linhas, que não é garantida sem
`ORDER BY`.

**Venda a descoberto é rejeitada, não truncada.** `CreateTransactionAsync` simula o razão
inteiro com a transação candidata antes de persistir e recusa a operação se a posição ficaria
negativa em qualquer ponto. Simular o razão inteiro (e não apenas a posição corrente) é o que
pega o lançamento **retroativo**: uma venda pode ter lastro no instante dela e ainda assim
deixar sem lastro uma venda posterior — coberto por `Invariante5c`.

**O replay ficou fiel.** A quantidade inteira da venda é subtraída mesmo quando excede a
posição, em vez de truncada por `Math.Min`. Assim razão e posição nunca contam histórias
diferentes; dados corrompidos de antes da correção aparecem como posição negativa em vez de
serem absorvidos em silêncio.

> ⚠️ **Pendência conhecida.** `GetSummaryAsync` filtra `Where(h => h.Quantity > 0)`, então uma
> posição negativa vinda de dados legados sumiria do dashboard em vez de gritar. Vale uma
> verificação de integridade na base existente antes de considerar o assunto encerrado.

## Invariante 7 — como foi corrigida

A variação de 24h é reconstruída a partir do preço de **ontem**, posição a posição:
`P_ontem = P_hoje / (1 + c/100)` e `variação = qty × (P_hoje − P_ontem)`. A base do
percentual passa a ser o valor de ontem somado diretamente, não derivado por subtração.

O cálculo anterior aplicava o percentual sobre o valor de hoje. O erro cresce com a
variação: superestimava o ganho em 35% numa alta de 35%, escondia R$ 8.000 de prejuízo
numa queda de 40%, exibia `0,00%` numa alta de 100% (`prevTotal` virava exatamente zero e
caía na guarda) e invertia o sinal acima disso.

Posições sem preço, ou com variação ≤ −100%, não têm preço anterior definido e ficam fora
dos **dois** lados da conta — não contaminam nem a variação nem a base.

> ⚠️ **Limitação que permanece.** O cálculo assume que a quantidade de hoje era a mesma
> ontem. No dia do aporte, a cripto comprada minutos antes entra na conta como se estivesse
> em carteira há 24 horas. Corrigir isso exige persistir o valor da carteira diariamente —
> é mudança de modelo, não de fórmula, e está fora desta suíte.

## O que cada vermelho restante está dizendo

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

Antes de qualquer dinheiro real, e ainda sem cobertura:

- **Lost update sob concorrência** em `HoldingRecalculator.ApplyAsync` — read-modify-write do
  razão inteiro, sem `RowVersion` nem lock. `PortfolioService.CreateTransactionAsync` e
  `DeleteTransactionAsync` ainda fazem esse par sem transação.
- **Sanity check de preço** no `PriceSnapshotJob`: hoje qualquer número da CoinGecko entra no
  banco. O aporte já recusa preço ≤ 0, mas um preço 10× errado passa.
- **Reconciliação** razão ↔ posição, com alerta e bloqueio — nada detecta divergência.
- **Comando de recálculo de posição.** `ApplyAsync` só roda dentro de escrita; não existe
  caminho para reparar uma posição divergente sem reeditar transações.

## Execução de aporte — idempotência e atomicidade

`AportExecutionInvariantTests`, 14 casos verdes. A propriedade central: **uma intenção de
aporte produz no máximo um conjunto de transações, e ou produz todas ou não produz nenhuma.**

Como funciona:

- **Chave de idempotência sob índice único.** Vem do header `Idempotency-Key`; na ausência
  dele o servidor deriva `nat:{planId}:{executedAt até o minuto}` — o que protege o bundle
  já publicado, porque o modal congela `executedAt` no mount. Quem arbitra duplicatas é o
  banco, não um `SELECT` antes do `INSERT`: em READ COMMITTED, duas requisições simultâneas
  passariam as duas pelo `SELECT`.
- **Fingerprint do payload.** Mesma chave + mesmo conteúdo devolve a resposta original
  (`replayed: true`). Mesma chave + conteúdo diferente é **409**, não replay — devolver o
  resultado antigo faria um aporte real desaparecer sem erro e sem rastro.
- **Uma única fronteira transacional.** A chave é reivindicada no primeiro `SaveChanges`,
  junto com as transações, para que o perdedor de uma corrida pare no índice único antes de
  segurar lock em qualquer holding. Moedas são processadas em ordem de `CoinId` para não
  gerar deadlock entre dois aportes concorrentes.
- **Fail-closed.** Moeda sem cotação recusa o aporte inteiro em vez de pulá-la em silêncio;
  a soma das alocações tem de fechar com o total; plano inativo, valor negativo, moeda
  repetida e quantidade que arredonda para zero são recusados. `AmountBrl` gravado é a soma
  real das transações, nunca o número enviado pelo cliente.

### Limites conhecidos

- **Corrida genuinamente paralela não é coberta** (ver armadilhas da fixture). O caminho
  índice-único → catch → replay é exercitado de forma sequencial.
- **A migration foi verificada à mão contra Postgres real** (aplicada sobre uma tabela com
  linhas pré-existentes, conferindo backfill, `NOT NULL` e rejeição de duplicata). Não há
  teste automatizado de migration — dev usa `EnsureCreated` e nunca as exercita.
