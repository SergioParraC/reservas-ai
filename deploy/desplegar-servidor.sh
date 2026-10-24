#!/usr/bin/env bash
# Despliegue del paquete en el servidor: modo Cloudflare Tunnel (sin dominio ni puertos abiertos).
# Uso:  bash desplegar-servidor.sh
# Requiere: docker + docker compose v2, openssl. No necesita sudo si el usuario está en el grupo docker.
set -euo pipefail

ORIGEN="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="${DEST:-$HOME/reservas-app}"
PROYECTO="reservas-espacios"
RED="${PROYECTO}_interna"

echo "==> Destino: $DEST"
mkdir -p "$DEST/dist" "$DEST/deploy"
cp -f "$ORIGEN/docker-compose.prod.yml" "$ORIGEN/docker-compose.tunnel.yml" "$ORIGEN/.env.example" "$DEST/"
cp -f "$ORIGEN/deploy/Caddyfile.tunnel" "$DEST/deploy/"
[ -f "$ORIGEN/efbundle" ] && cp -f "$ORIGEN/efbundle" "$DEST/" && chmod +x "$DEST/efbundle"
rm -rf "${DEST:?}/dist"/* && cp -r "$ORIGEN/dist/." "$DEST/dist/"
cd "$DEST"

# 1) Imagen de la API (siempre se recarga: evita quedarse con una versión anterior)
echo "==> Cargando imagen desde api.tar"
docker load -i "$ORIGEN/api.tar"

# 2) Variables de entorno con secretos generados en el servidor
if [ ! -f .env.prod ]; then
  MSSQL_SA_PASSWORD="Reservas.$(openssl rand -hex 8)!"
  JWT_KEY="$(openssl rand -base64 48 | tr -d '\n')"
  ADMIN_PASS="Admin.$(openssl rand -hex 6)!"
  cat > .env.prod <<EOF
# Generado por desplegar-servidor.sh el $(date -u +%F' '%T) UTC
DOMINIO=localhost
CORREO_TLS=
IMAGEN_API=reservas-api:1.0.0
BD_NOMBRE=ReservasEspaciosDb
MSSQL_PID=Express
MSSQL_SA_PASSWORD=$MSSQL_SA_PASSWORD
JWT_KEY=$JWT_KEY
JWT_AUDIENCE=reservas-espacios-spa
JWT_EXPIRA_MINUTOS=60
SEED_ADMIN_EMAIL=admin@reservas.local
SEED_ADMIN_PASSWORD=$ADMIN_PASS
SEED_ADMIN_NOMBRE="Administrador del Sistema"
ENTRA_TENANT_ID=
ENTRA_CLIENT_ID=
ENTRA_AUDIENCE=
SMTP_HABILITADO=false
SMTP_REMITENTE_CORREO=reservas@reservas.local
DOMINIO_CORREO_INSTITUCIONAL=uninpahu.edu.co
EOF
  chmod 600 .env.prod
  printf '\n*** Credenciales iniciales ***\n  usuario: admin@reservas.local\n  clave:   %s\n  (quedan en %s/.env.prod)\n\n' "$ADMIN_PASS" "$DEST"
fi

set -a
# shellcheck disable=SC1091
. ./.env.prod
set +a

COMPOSE=(docker compose --env-file .env.prod -f docker-compose.prod.yml -f docker-compose.tunnel.yml)

# Modo de solo validación: no crea contenedores.
if [ "${1:-}" = "--validar" ]; then
  "${COMPOSE[@]}" config --quiet && echo "Configuración válida (modo --validar)"
  exit 0
fi

# 3) Base de datos y migraciones (en Production no se migran solas)
echo "==> Levantando SQL Server"
"${COMPOSE[@]}" up -d mssql
estado=""
for _ in $(seq 1 60); do
  estado="$(docker inspect -f '{{.State.Health.Status}}' reservas-mssql 2>/dev/null || echo starting)"
  [ "$estado" = "healthy" ] && break
  sleep 5
done
[ "$estado" = "healthy" ] || { echo "ERROR: SQL Server no llegó a healthy"; docker logs --tail 30 reservas-mssql; exit 1; }
echo "==> SQL Server healthy"

if [ -f efbundle ]; then
  echo "(aviso) efbundle presente pero ya no se usa: las migraciones corren con la propia imagen"
fi

# Migraciones con la imagen de la API: mismo código, misma configuración, sin artefacto extra.
echo "==> Aplicando migraciones"
"${COMPOSE[@]}" run --rm --no-deps api --migrar

# 4) API + Caddy + túnel
echo "==> Levantando API, Caddy y túnel"
"${COMPOSE[@]}" up -d

echo "==> Esperando la URL pública del túnel (hasta 2 min)"
URL=""
for _ in $(seq 1 24); do
  URL="$(docker logs reservas-tunel 2>&1 | grep -o 'https://[a-z0-9-]*\.trycloudflare\.com' | head -1 || true)"
  [ -n "$URL" ] && break
  sleep 5
done

echo "==> Estado de los contenedores"
"${COMPOSE[@]}" ps

if [ -n "$URL" ]; then
  echo
  echo "================ APLICACIÓN PUBLICADA ================"
  echo "  URL:       $URL"
  echo "  Salud:     $URL/health"
  echo "  Admin:     $URL  (admin@reservas.local)"
  echo "  Nota: la URL cambia cada vez que se reinicia reservas-tunel."
  echo "======================================================"
else
  echo "El túnel aún no publicó la URL. Revisa: docker logs reservas-tunel"
fi
