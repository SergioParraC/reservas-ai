#!/usr/bin/env bash
# Recolección de datos (SOLO LECTURA) para completar docs/despliegue.md §1.
# Uso en el servidor:  bash diagnostico-servidor.sh 2>&1 | tee ~/reporte-servidor.txt
set -uo pipefail

linea() { printf '\n=== %s ===\n' "$1"; }

linea "Sistema"
hostnamectl 2>/dev/null || { hostname; uname -a; }
echo "Arquitectura: $(uname -m)   (se espera x86_64 para el bundle EF linux-x64)"
echo "CPU: $(nproc) núcleos"; free -h | head -2; df -h / | tail -1

linea "Red local"
ip -4 -o addr show scope global 2>/dev/null | awk '{print $2, $4}'
ip route 2>/dev/null | grep default
grep -i '^nameserver' /etc/resolv.conf 2>/dev/null | head -3

linea "Docker"
docker --version 2>&1
docker compose version 2>&1
systemctl is-active docker 2>&1
docker info --format '{{.ServerVersion}}' 2>&1 | head -1
id -nG "$USER" | tr ' ' '\n' | grep -qx docker && echo "docker: usuario en el grupo (sin sudo)" || echo "docker: usuario NO está en el grupo (usar sudo)"

linea "Puertos en escucha (80/443/1433/8080 importan)"
sudo ss -ltnp 2>/dev/null | head -25 || ss -ltn 2>/dev/null | head -25

linea "Firewall"
sudo ufw status 2>&1 | head -6
sudo nft list ruleset 2>/dev/null | head -6

linea "SSH"
systemctl is-active ssh 2>&1
sudo grep -Ei '^[[:space:]]*(port|passwordauthentication|pubkeyauthentication|permitrootlogin)' \
  /etc/ssh/sshd_config /etc/ssh/sshd_config.d/*.conf 2>/dev/null

linea "Salida a Internet / IP pública"
curl -fsS --max-time 10 https://api.ipify.org || echo "SIN salida a Internet"
echo
curl -fsS --max-time 10 https://ipinfo.io/json 2>/dev/null | head -15

linea "Aviso CGNAT"
echo "Compara la IP pública anterior con la IP WAN de tu router (página de administración)."
echo "Si no coinciden, o la pública está en 100.64.0.0/10, tu ISP usa CGNAT:"
echo "  -> el reenvío de puertos del router NO funcionará; hará falta un túnel (Cloudflare Tunnel / Tailscale)."

linea "Fecha/hora (afecta a TLS)"
date -u
