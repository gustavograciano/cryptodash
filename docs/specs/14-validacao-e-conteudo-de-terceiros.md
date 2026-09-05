# 14 — Validação de entrada e conteúdo de terceiros

## Problemas

**1. `UpsertPlanAsync` valida o dia e não valida o dinheiro.** No mesmo método:

```csharp
if (req.DayOfMonth < 1 || req.DayOfMonth > 28) throw new ArgumentException(...);
plan.AmountBrl = req.AmountBrl;   // sem validação alguma
```

`PUT /api/v1/aports/plan` com `amountBrl: -300` devolve 200 e persiste. A partir daí o
`DcaAllocator` cai na guarda `aporteBrl <= 0`, devolve lista vazia, e o card de aporte — o
recurso principal do produto — **morre em silêncio**, sem mensagem de erro.

**2. Prompt injection no digest.** `AnthropicDigestGenerator` interpola título e URL de
notícias de terceiros direto no conteúdo enviado ao modelo:

```csharp
sb.AppendLine($"- [{n.Source}] {n.Title} ({string.Join(",", n.Coins)}) — {n.Url}");
```

É o único ponto do sistema em que texto controlado por terceiros vira recomendação lida por
humano para decidir aporte. Uma manchete construída para isso pode alterar o resumo.

## Critério de pronto

- [ ] `AmountBrl` validado: positivo e com teto sensato
- [ ] Conteúdo de terceiros delimitado no prompt, com instrução de tratá-lo como dado
- [ ] Testes para ambos

## Abordagem

1. Validação no `UpsertPlanAsync`, seguindo o padrão do serviço (`ArgumentException`, que o
   controller já mapeia para 400).
2. Delimitar as notícias em bloco marcado e instruir o modelo, no system prompt, a tratar tudo
   dentro dele como dado a resumir — nunca como instrução. **Não é blindagem completa**;
   reduz a superfície e é o que cabe sem um segundo modelo de verificação.

---

## Verificação executada

5 casos em `PlanValidationTests`, verdes. Suíte: **113/113**.

Cobertos: valor negativo, zero, acima do teto, dia inválido (a validação que já existia não
podia se perder na mudança) e — o mais importante — **plano inválido não sobrescreve o plano
que estava funcionando**, porque a validação roda antes de qualquer escrita.

O teto de R$ 10.000.000 não é regra de negócio: é proteção contra dedo gordo, na ausência de
qualquer confirmação na interface.

### Prompt injection

O conteúdo de terceiros passou a ir dentro de `<noticias>...</noticias>`, com instrução no
system prompt para tratar tudo ali como **dado a resumir, nunca como instrução**, e para
ignorar texto que peça mudança de comportamento ou recomendação de compra e venda.

`Sanitizar` neutraliza `<` e `>` (para `‹` e `›`) e colapsa quebras de linha, impedindo que uma
manchete **feche o delimitador** e escreva fora dele — a forma mais direta de texto de terceiros
virar instrução.

**Isto não é blindagem.** Nada em delimitação é: reduz a superfície. Defesa real exigiria um
segundo modelo verificando a saída, o que está fora do escopo desta fase — e o digest continua
sendo texto não verificado que o usuário lê antes de decidir aporte.

**Status: concluída.**
