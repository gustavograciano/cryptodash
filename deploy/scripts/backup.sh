#!/bin/sh
set -eu

TIMESTAMP=$(date -u +%Y%m%d_%H%M%S)
OUT="/backups/cryptodash_${TIMESTAMP}.sql.gz"

echo "[backup] dump iniciado em ${TIMESTAMP}"
pg_dump --no-owner --clean --if-exists | gzip > "${OUT}"
echo "[backup] gravado em ${OUT}"

# retencao 7 dias
find /backups -name 'cryptodash_*.sql.gz' -type f -mtime +7 -delete || true
echo "[backup] limpeza concluida"
