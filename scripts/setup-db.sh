#!/usr/bin/env bash
set -euo pipefail

: "${PORTFOLIO_DB_PASSWORD:?Set PORTFOLIO_DB_PASSWORD first. Copy .env.example to .env or export it in your shell.}"

CONTAINER_NAME="${PORTFOLIO_DB_CONTAINER:-portfolio-sql}"
SQLCMD="/opt/mssql-tools18/bin/sqlcmd"

echo "Waiting for SQL Server container '${CONTAINER_NAME}'..."
for _ in $(seq 1 60); do
  if docker exec "${CONTAINER_NAME}" "${SQLCMD}" -S localhost -U sa -P "${PORTFOLIO_DB_PASSWORD}" -C -Q "SELECT 1" >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

docker exec "${CONTAINER_NAME}" "${SQLCMD}" -S localhost -U sa -P "${PORTFOLIO_DB_PASSWORD}" -C -i /db/PortfolioDB.sql

echo "PortfolioDB initialized."
echo "Use: export PORTFOLIO_DB_CONNECTION=\"Server=localhost,1433;Database=PortfolioDB;User Id=sa;Password=<your-password>;Encrypt=True;TrustServerCertificate=True;\""
