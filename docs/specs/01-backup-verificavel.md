# 01 — Backup verificável

**Bloqueador #6.** Menor esforço da lista e protege o único mecanismo de recuperação que existe.

## Problema

`deploy/scripts/backup.sh:2` usa `set -eu`. Em um pipeline, `set -e` só observa o status do
**último** comando — aqui, `gzip`. Se `pg_dump` falhar (Postgres fora do ar, senha rotacionada,
disco cheio), `gzip` termina com sucesso e grava um `.sql.gz` **vazio, com nome válido e
timestamp atual**. O script imprime "gravado em ..." e sai com 0.

Pior: a retenção `find /backups -mtime +7 -delete` roda em seguida. Oito dias de falha
silenciosa apagam os últimos backups bons.

## Critério de pronto

- [ ] Falha do `pg_dump` faz o script sair com código != 0 e **não** deixar arquivo
- [ ] Dump válido é verificado antes de ser aceito (`gunzip -t` + tamanho mínimo)
- [ ] Retenção só roda **depois** de confirmar que o backup novo é válido
- [ ] Verificação manual documentada: simular falha e conferir que nenhum arquivo sobra e que
      os backups antigos permanecem

## Abordagem

1. `set -euo pipefail`
2. Escrever em arquivo temporário; validar; só então mover para o nome definitivo
3. `gunzip -t` no arquivo e checagem de tamanho mínimo (dump vazio comprimido tem ~20 bytes)
4. Podar antigos apenas após o `mv` bem-sucedido
5. Mensagem de erro que diga o que fazer

## Fora de escopo

Criptografia e cópia offsite — importantes, mas são mudança de infraestrutura, não do script.
Registrar como pendência.

---

## Verificação executada

Postgres 16 em container, schema com uma tabela e uma linha.

| Cenário | Script antigo | Script novo |
|---|---|---|
| Caminho feliz | arquivo gravado | `exit=0`, arquivo de 830 bytes gravado **e verificado** |
| `pg_dump` falha (senha errada) | **arquivo de 20 bytes gravado, `exit=0`, imprime "gravado em"** | `exit=1`, **nenhum arquivo**, backup anterior preservado |

O piso de tamanho foi calibrado durante a verificação: 1024 bytes chegou a rejeitar um dump
**válido** de 826 bytes. Baixado para 200 — um dump falho comprimido tem ~20 bytes, e a
guarda semântica real é o `grep CREATE TABLE`.

**Status: concluída.**
