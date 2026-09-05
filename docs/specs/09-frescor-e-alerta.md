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
