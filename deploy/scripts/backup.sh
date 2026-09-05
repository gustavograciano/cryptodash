#!/bin/sh
# Backup do Postgres com verificação.
#
# O `set -e` sozinho NÃO protege um pipeline: ele observa apenas o status do último comando.
# Sem `pipefail`, um `pg_dump` que falha seguido de `gzip` que funciona produz um arquivo
# vazio, com nome válido e timestamp atual, e o script sai com 0. Oito dias assim, e a
# retenção apaga os últimos backups bons.
set -euo pipefail

TIMESTAMP=$(date -u +%Y%m%d_%H%M%S)
FINAL="/backups/cryptodash_${TIMESTAMP}.sql.gz"
TEMP="${FINAL}.part"

# Piso deliberadamente baixo: um dump falho comprimido tem ~20-50 bytes, e o teste real de
# conteúdo é o grep por CREATE TABLE mais abaixo. Piso alto demais rejeita banco pequeno
# legítimo — 1024 chegou a barrar um dump válido de 826 bytes durante a verificação.
TAMANHO_MINIMO_BYTES=200

# Só remove o parcial; um backup já validado nunca é apagado por falha desta execução.
limpar() { rm -f "${TEMP}"; }
trap limpar EXIT

echo "[backup] iniciando ${TIMESTAMP}"

# Escreve em arquivo temporário: o nome definitivo só passa a existir depois da verificação,
# então um arquivo com nome de backup é, por construção, um backup válido.
pg_dump --no-owner --clean --if-exists | gzip > "${TEMP}"

if [ ! -s "${TEMP}" ]; then
  echo "[backup] ERRO: dump vazio. Backup NAO gravado e antigos preservados." >&2
  exit 1
fi

TAMANHO=$(wc -c < "${TEMP}")
if [ "${TAMANHO}" -lt "${TAMANHO_MINIMO_BYTES}" ]; then
  echo "[backup] ERRO: dump de ${TAMANHO} bytes, abaixo do minimo de ${TAMANHO_MINIMO_BYTES}." >&2
  echo "[backup] Backup NAO gravado e antigos preservados." >&2
  exit 1
fi

# Integridade do gzip: pega truncamento por disco cheio ou conexão interrompida.
if ! gzip -t "${TEMP}" 2>/dev/null; then
  echo "[backup] ERRO: arquivo corrompido (gzip -t falhou). Backup NAO gravado." >&2
  exit 1
fi

# O dump precisa conter DDL de verdade, não apenas cabeçalho do pg_dump.
if ! gzip -dc "${TEMP}" | grep -q "CREATE TABLE"; then
  echo "[backup] ERRO: dump sem CREATE TABLE — provavelmente conectou mas nao leu o schema." >&2
  echo "[backup] Backup NAO gravado e antigos preservados." >&2
  exit 1
fi

mv "${TEMP}" "${FINAL}"
trap - EXIT
echo "[backup] gravado e verificado: ${FINAL} (${TAMANHO} bytes)"

# A retenção roda SÓ depois de existir um backup novo e válido. Se qualquer verificação
# acima falhar, o script já saiu e os antigos continuam onde estão.
REMOVIDOS=$(find /backups -name 'cryptodash_*.sql.gz' -type f -mtime +7 -print -delete | wc -l)
echo "[backup] retencao: ${REMOVIDOS} backup(s) com mais de 7 dias removido(s)"
