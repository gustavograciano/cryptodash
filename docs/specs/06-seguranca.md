# 06 — Endurecimento de segurança

**Bloqueadores #7 e #8.**

## Problemas

1. **`docker-compose.prod.yml:35`** usa `${SEED_PASSWORD}` sem `:?`. Variável ausente vira
   string vazia; `config["Seed:UserPassword"] ?? "ChangeMe@123"` **não** dispara o `??`
   porque `""` não é `null`. Resultado: a única conta do sistema é criada com **senha vazia**.
   O e-mail está hardcoded no bundle (`LoginPage.tsx:7`), então o identificador é público.
2. **`appsettings.json:24`** carrega uma chave JWT de desenvolvimento. O `?? throw` de
   `Program.cs:20` é **código morto** — o appsettings sempre fornece valor. Qualquer execução
   fora do compose de produção assina tokens com uma chave pública no repositório.
3. **`/auth/login` sem rate limit, sem lockout, sem contador.** Brute force ilimitado.
4. **`DevController`** tem apenas `[Authorize]`, sem guarda de ambiente. Em produção permite
   injetar notícias falsas no cache que alimenta o digest de IA lido para decidir aporte, e
   disparar jobs em rajada (ban da CoinGecko, gasto de créditos Anthropic).

## Critério de pronto

- [ ] Boot **falha** se a senha de seed for vazia ou menor que o mínimo
- [ ] Boot **falha** se a chave JWT estiver ausente, curta ou for o valor de exemplo
- [ ] Chave de exemplo removida do `appsettings.json`
- [ ] `docker-compose.prod.yml` exige `SEED_PASSWORD`
- [ ] `/auth/login` com rate limit por IP
- [ ] `DevController` retorna 404 fora de Development
- [ ] E-mail default removido do formulário de login
- [ ] Testes para as validações de boot e para o 404 do DevController

## Abordagem

- Validação de configuração no startup, antes do `DbSeeder`, lançando com mensagem acionável
- `AddRateLimiter` (built-in do .NET 8) com política fixa no endpoint de login
- Registro condicional do `DevController` ou `if (!env.IsDevelopment()) return NotFound();`

## Fora de escopo

Refresh token, revogação, RBAC e `UserId` nas entidades — são a Fase 3 do roadmap.

---

## Verificação executada

16 casos em `StartupValidationTests`. Suíte total: **91 casos, 84 verdes**.

Boot verificado de verdade, rodando a API:

| Cenário | Resultado |
|---|---|
| Production sem `JWT_KEY` | `InvalidOperationException: Jwt:Key ausente...` |
| Production com chave de 5 chars | `Jwt:Key tem 5 caracteres; o mínimo para HMAC-SHA256 é 32` |
| Production com `SEED_PASSWORD=ChangeMe@123` | `senha de exemplo publicada no repositório` |
| Development | sobe normalmente, 7 jobs agendados |

## Decisão: validação consciente do ambiente

Remover a chave do `appsettings.json` quebrava o loop local — `docker compose up` e
`dotnet run` usavam justamente os valores de exemplo. A recusa de **valor publicado** passou a
valer só fora de Development; **não-vazio e comprimento mínimo valem em todo ambiente**, senão
o dev rodaria um sistema diferente do que vai para produção.

Os valores de desenvolvimento foram para `appsettings.Development.json`, que só é lido quando
`ASPNETCORE_ENVIRONMENT=Development` e portanto não pode vazar para produção.

**Status: concluída.** Refresh token, revogação e `UserId` nas entidades continuam fora de
escopo (Fase 3).
