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
