# 09 — Frescor de preço e alerta de job parado

**Bloqueador #9.** "Eu perceberia que algo parou?" Hoje, não.

## Problema

`PriceSnapshot.UpdatedAt` existe, chega ao DTO (`HoldingDto.PriceUpdatedAt`) e ao tipo
TypeScript — e **nunca é renderizado**. Se o `PriceSnapshotJob` parar, o dashboard continua
exibindo números com aparência normal por tempo indefinido. `/health/ready` responde 200
porque o banco está de pé. Nenhum alerta dispara.

Nenhum job tem tratamento de exceção, retry ou alarme: uma falha vira uma linha de log no
console e o dado do dia não existe.

## Critério de pronto

- [ ] O dashboard mostra a idade do preço e destaca visualmente quando está obsoleto
- [ ] `/health/ready` passa a reportar `unhealthy` quando o último snapshot está velho demais
- [ ] Falha de job é registrada com severidade de erro e fica consultável
- [ ] Testes: snapshot velho → readiness degradado; DTO carrega a idade correta

## Abordagem

1. **Frontend:** `HoldingsCard` exibe a idade relativa do preço; acima do limiar (ex.: 30 min)
   aplica destaque de atenção. O dado já vem no DTO — é só renderizar.
2. **Readiness:** `HealthController.Ready` passa a checar a idade do snapshot mais recente
   além da conexão com o banco. Limiar configurável, default generoso (ex.: 1 hora) para não
   gerar falso alarme com o job de 15 minutos.
3. **Falha de job:** `IJobListener` do Quartz registrando exceções em `Error`, ou try/catch
   por job. Sem sink persistente de log isso ainda é fraco — registrar a dependência da
   Fase 1 (observabilidade) e não fingir que resolve alerta de verdade.

## Fora de escopo

Métricas, traces e alerta externo (e-mail/Telegram) — Fase 1 do roadmap. Esta spec entrega
**visibilidade**, não notificação ativa.

---

## Verificação executada

5 casos em `HealthReadinessTests` + 5 em `format.test.ts`. Suíte: **96/96 .NET, 11/11 frontend.**

Entregue:

- `HoldingsCard` exibe a idade do preço sob a cotação; acima de 30 min vira `⚠ há N min` em
  vermelho. O dado já vinha no DTO e nunca era renderizado.
- `/health/ready` passa a checar a idade do snapshot mais recente além da conexão: devolve
  **503 `degraded`** com `priceAgeSeconds` quando o preço passa de 1 hora.
- `JobFailureListener` registra exceção de job em `Error` — antes, falha de job virava silêncio
  completo.

### Bug encontrado ao rodar de verdade

A primeira versão do readiness usava `OrderByDescending(p => p.UpdatedAt)` no banco. O SQLite
**não suporta `ORDER BY` sobre `DateTimeOffset`** e o endpoint devolvia **HTTP 500** em
Development — readiness quebrado é pior do que readiness ausente. Só apareceu porque a API foi
levantada e o endpoint chamado; nenhum teste unitário pegaria. Ordenação passou para o cliente.

Verificado ao vivo: `{"status":"ready","priceAgeSeconds":0.95,"maxPriceAgeSeconds":3600}` com
HTTP 200.

**Status: concluída.** Alerta ativo (e-mail/Telegram) e métricas continuam fora de escopo:
esta spec entrega visibilidade, não notificação.
