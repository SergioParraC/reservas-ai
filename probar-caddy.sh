#!/usr/bin/env bash
set -uo pipefail
export DOCKER_CONFIG=/tmp/dc
cd /mnt/c/Users/djson/Desktop/Universidad/IA/ProyectoUniversidad

echo "== 1) merge de compose (volúmenes y puertos) =="
MSSQL_SA_PASSWORD='P.Clave.2026!' JWT_KEY='0123456789abcdef0123456789abcdef' IMAGEN_API='reservas-api:1.0.0' \
DOMINIO=localhost CORREO_TLS='' SEED_ADMIN_EMAIL='a@b.c' SEED_ADMIN_PASSWORD='x' \
docker compose -f docker-compose.prod.yml -f docker-compose.tunnel.yml config 2>/dev/null \
  | sed -n '/caddy:/,/^  [a-z]/p' | head -40

echo "== 2) arranque real de caddy con Caddyfile.tunnel =="
docker rm -f prueba-caddy >/dev/null 2>&1
docker run --rm --name prueba-caddy \
  -v "$PWD/deploy/Caddyfile.tunnel:/etc/caddy/Caddyfile:ro" \
  -e DOMINIO=localhost -e CORREO_TLS='' \
  caddy:2-alpine caddy run --config /etc/caddy/Caddyfile 2>&1 | head -25 &
sleep 8
docker ps -a --filter name=prueba-caddy --format '{{.Status}}'
docker rm -f prueba-caddy >/dev/null 2>&1
