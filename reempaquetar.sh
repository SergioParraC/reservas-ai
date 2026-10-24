#!/usr/bin/env bash
set -uo pipefail
cd /mnt/c/Users/djson/Desktop/Universidad/IA/ProyectoUniversidad

echo "== regenerar copia desde el repo y verificar =="
cp -f deploy/Caddyfile.tunnel paquete/deploy/Caddyfile.tunnel
cp -f deploy/desplegar-servidor.sh paquete/deploy/desplegar-servidor.sh
cp -f deploy/Caddyfile paquete/deploy/Caddyfile 2>/dev/null || true
sed -i 's/\r$//' paquete/deploy/Caddyfile.tunnel paquete/deploy/desplegar-servidor.sh
chmod +x paquete/deploy/desplegar-servidor.sh
grep -n 'file_serve' paquete/deploy/Caddyfile.tunnel
echo "bytes repo=$(wc -c < deploy/Caddyfile.tunnel) empaquetado=$(wc -c < paquete/deploy/Caddyfile.tunnel)"

echo "== rehacer tgz y verificar contenido =="
rm -f paquete-reservas.tgz
tar czf paquete-reservas.tgz -C paquete .
rm -rf /tmp/verif2 && mkdir -p /tmp/verif2
tar xzf paquete-reservas.tgz -C /tmp/verif2
diff -r paquete /tmp/verif2 && echo "PAQUETE-IDENTICO"
ls -lh paquete-reservas.tgz
sha256sum paquete-reservas.tgz
