# Despliegue en servidor propio (WSL → servidor personal)

Instructivo para pasar de lo local (`docker-compose.yml`) a un entorno público.
Solo hay que **rellenar los datos del §1**; el resto son comandos fijos.

## 1. Datos que faltan (el formulario)

Rellena la columna "Tu valor" y guárdala fuera del repositorio.

| # | Dato | Dónde se obtiene | Tu valor |
| --- | --- | --- | --- |
| 1 | `SERVIDOR_HOST` (IP o dominio del servidor) | Te lo da el proveedor / `hostname -I` en el servidor | |
| 2 | `SERVIDOR_USUARIO` | Usuario con `sudo` en el servidor | |
| 3 | `SERVIDOR_PUERTO_SSH` | Normalmente `22` | |
| 4 | `RUTA_LLAVE_SSH` | Archivo `.pem`/`id_ed25519` (Windows: `C:\Users\<tu-usuario>\.ssh\id_ed25519`) | |
| 5 | `DOMINIO` | Dominio o subdominio que apuntará al servidor (registro **A**) | |
| 6 | `CORREO_TLS` | Correo para Let's Encrypt (avisos de expiración) | |
| 7 | `IMAGEN_API` | `usuario/reservas-api:1.0.0` en Docker Hub o GHCR | |
| 8 | `REGISTRO_USUARIO` + token | Usuario y token de Docker Hub/GHCR | |
| 9 | `MSSQL_SA_PASSWORD` | Generar (§2) — mínimo 8, mayúscula, minúscula, dígito y símbolo | |
| 10 | `JWT_KEY` | Generar (§2) — 32+ caracteres aleatorios | |
| 11 | `SEED_ADMIN_EMAIL` | Correo del administrador inicial | |
| 12 | `SEED_ADMIN_PASSWORD` | Contraseña del administrador inicial | |
| 13 | `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID`, `ENTRA_AUDIENCE` | Portal de Entra ID (opcional; dejar vacío si solo hay login local) | |
| 14 | `BD_NOMBRE` | `ReservasEspaciosDb` (o el nombre que quieras) | |
| 15 | `DOMINIO_CORREO_INSTITUCIONAL` | Dominio de los correos institucionales, p. ej. `uninpahu.edu.co` (varios separados por comas) | |

Todo lo anterior va a un archivo **`.env.prod`** en el servidor (nunca a Git),
partiendo de `.env.example`.

### 1.1 Recolectar los datos del servidor (una sola vez)

Entra por SSH y ejecuta el script de solo lectura incluido en el repositorio:

```bash
# Opción A: si el repositorio ya está clonado en el servidor
bash deploy/diagnostico-servidor.sh 2>&1 | tee ~/reporte-servidor.txt

# Opción B: copiarlo desde tu WSL
scp deploy/diagnostico-servidor.sh sergio-parra@192.168.1.10:~
ssh sergio-parra@192.168.1.10 'bash ~/diagnostico-servidor.sh 2>&1 | tee ~/reporte-servidor.txt'
```

Traer el reporte a tu WSL/Windows:

```bash
scp sergio-parra@192.168.1.10:~/reporte-servidor.txt .
```

El reporte responde: arquitectura, CPU/RAM/disco, IP local y puerta de enlace,
versión de Docker, puertos ocupados (¿80/443 libres?), firewall, configuración de
SSH, IP pública y si hay **CGNAT**.

> El servidor está en `192.168.1.10`, una IP privada: para publicarlo en Internet
> hay que reenviar los puertos **80 y 443** en el router hacia esa IP y tener una
> IP pública real (o usar Cloudflare Tunnel si el ISP aplica CGNAT).

### 1.2 Resultado del diagnóstico (23-oct-2026)

| Dato | Valor obtenido | Estado |
| --- | --- | --- |
| Equipo | `sergio-parra-HP-Pavilion-TS-14-Notebook-PC` (portátil HP) | ⚠️ impedir suspensión (§1.4) |
| SO / arquitectura | Ubuntu 24.04.5 LTS / `x86_64` | ✅ |
| CPU / RAM / disco | 4 vCPU / 15 GiB (12 GiB libres) / 308 GB libres | ✅ |
| IP local | `192.168.1.10/24` en `eno1` (cable), puerta `192.168.1.1` | ⚠️ es DHCP: reservar la IP |
| Docker | 29.9.2 + Compose v5.6.1, usuario en grupo `docker` | ✅ sin `sudo` |
| Puertos 80/443/1433/8080 | libres | ✅ |
| Firewall (`ufw`) | inactivo | ⚠️ activar (§1.4) |
| SSH | activo, puerto 22, login por contraseña | ⚠️ pasar a llave (§3) |
| IP pública | `152.203.144.91` (Colombia Telecom / Movistar, Bogotá) | ⚠️ confirmar CGNAT (§1.3) |
| Zona horaria | UTC | ✅ (TLS sin problema) |

### 1.3 ¿Hay CGNAT? (paso bloqueante)

1. Abre `http://192.168.1.1` → estado/WAN → anota la **IP WAN** del router.
2. Si es `152.203.144.91` → hay IP pública real:
   - *Port forwarding* del router: `80 → 192.168.1.10:80` y `443 → 192.168.1.10:443`.
   - Reserva DHCP para la MAC de `eno1`, o IP estática, para que no cambie.
   - Sigue con Caddy + Let's Encrypt (§7).
3. Si es distinta, `10.x`, `100.64.0.0/10` o `192.168.x` → **CGNAT**: el reenvío no
   funcionará. Usa **Cloudflare Tunnel** (`cloudflared` en el servidor, dominio en
   Cloudflare); en ese caso Caddy y los puertos 80/443 no son necesarios, y se
   publica con `cloudflared tunnel --url http://localhost:8080` (o un túnel nombrado).

### 1.4 Endurecer el servidor antes de publicar

```bash
# Nunca suspender el equipo (es un portátil conectado 24/7)
sudo systemctl mask sleep.target suspend.target hibernate.target hybrid-sleep.target
sudo sed -i 's/^#\?HandleLidSwitch=.*/HandleLidSwitch=ignore/' /etc/systemd/logind.conf

# Firewall: abrir 22 ANTES de activar, o se pierde el acceso SSH
sudo ufw allow 22/tcp && sudo ufw allow 80,443/tcp && sudo ufw --force enable

# Autenticación por llave (desde tu WSL, ver §3) y luego:
# sudo sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication no/' /etc/ssh/sshd_config
# sudo systemctl reload ssh
```

Comprueba que no haya contenedores previos ocupando el stack:

```bash
docker ps --format 'table {{.Names}}\t{{.Ports}}'
docker compose ls
```

> En este servidor ya corre **Pi-hole** (`/srv/docker/pihole/compose.yaml`) en modo
> host, pero **no escucha en 80/443**, así que no choca con Caddy. Si en el futuro
> se mueve su panel al puerto 80, habría que cambiar uno de los dos.

> El router se administra en `https://192.168.1.1:8000/`. Ese **no** es el dato que
> falta: dentro del panel hay que ir a *Estado / Estado de Internet / WAN* y anotar
> la IP que asigna el ISP para compararla con `152.203.144.91`.

### 1.5 Sin dominio: opciones gratuitas

| Opción | URL que obtienes | ¿Abrir puertos? | Notas |
| --- | --- | --- | --- |
| **Cloudflare Quick Tunnel** (recomendado para la demo) | `https://algo-random.trycloudflare.com` | No | Sin registro, sin dominio, funciona incluso con CGNAT. La URL cambia al reiniciar |
| Cloudflare Tunnel nombrado | `https://reservas.tudominio` | No | Necesita un dominio propio dado de alta en Cloudflare |
| **DuckDNS** | `https://tuapp.duckdns.org` | Sí (80/443) | Gratis y estable; se actualiza la IP con su token. Caddy emite el certificado por HTTP-01 |
| No-IP Free | `tuapp.ddns.net` | Sí | Hay que confirmar el hostname cada 30 días |
| FreeDNS (afraid.org) | `sub.dominio.org` | Sí | Interfaz antigua pero funciona |
| Tailscale Funnel | `https://maquina.tailXXXX.ts.net` | No | HTTPS automático; ancho de banda limitado |
| sslip.io / nip.io | `152.203.144.91.sslip.io` | Sí | Sin registro; Let's Encrypt puede limitar por dominio |

Con Cloudflare Quick Tunnel **no hace falta dominio ni tocar el router**, por lo que
es la vía más corta para dejar la app accesible desde Internet.

### 1.6 Correo de notificaciones (opcional)

El backend ya soporta cola de correos (`Smtp:*`). En la demo se deja
`SMTP_HABILITADO=false`; si se activa, usar un proveedor con contraseña de
aplicación (Brevo, Mailtrap, Gmail) y completar `SMTP_HOST/USUARIO/CONTRASENA`.




## 2. Generar los secretos (en WSL)

```bash
openssl rand -base64 32                       # JWT_KEY
openssl rand -base64 24                      # MSSQL_SA_PASSWORD (añade un símbolo)
```

## 3. Habilitar SSH en tu WSL Ubuntu (hoy está inactivo)

```bash
sudo apt update && sudo apt install -y openssh-server
sudo service ssh start && sudo service ssh status
```

Si vas a entrar desde otra máquina de tu red, en PowerShell **como administrador**:

```powershell
netsh interface portproxy add v4tov4 listenport=2222 listenaddress=0.0.0.0 `
  connectport=22 connectaddress=(wsl -d Ubuntu hostname -I).Split()[0]
New-NetFirewallRule -DisplayName "WSL SSH 2222" -Direction Inbound -LocalPort 2222 -Protocol TCP -Action Allow
```

Para llegar al servidor personal desde WSL, copia tu llave una vez:

```bash
ssh-copy-id -i ~/.ssh/id_ed25519.pub -p <SERVIDOR_PUERTO_SSH> <SERVIDOR_USUARIO>@<SERVIDOR_HOST>
ssh -p <SERVIDOR_PUERTO_SSH> <SERVIDOR_USUARIO>@<SERVIDOR_HOST> 'docker --version; sudo -n true && echo sudo-ok; free -h'
```

Debe responder con la versión de Docker y `sudo-ok`. Si no, faltan permisos o Docker en el servidor.

## 4. Publicar la imagen de la API

```bash
# En WSL, desde la raíz del repositorio
docker login
docker build -t <IMAGEN_API> .
docker push <IMAGEN_API>
```

Sin registro (alternativa): `docker save <IMAGEN_API> -o api.tar` + `scp api.tar servidor:` + `docker load -i api.tar`.

## 5. Compilar el SPA

```bash
cd src/frontend && npm ci && npm run build   # 'production' es la configuración por defecto
mkdir -p ../../dist && cp -r dist/frontend/browser/. ../../dist/
```

Antes del build, en `src/frontend/src/environments/environment.production.ts`:

- Con dominio propio (Caddy + TLS): `apiBaseUrl: 'https://<DOMINIO>'`.
- Con Cloudflare Tunnel o cualquier despliegue de **un solo origen**: `apiBaseUrl: ''`
  (mismo host, sin CORS).


## 6. Migraciones (en producción son un paso explícito)

`Program.cs` solo migra automáticamente en `Development`. En otros entornos se usa el
argumento `--migrar`, que aplica el esquema, siembra el administrador y termina sin
levantar el servidor HTTP (misma imagen, misma configuración, sin artefactos extra):

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d mssql
docker compose --env-file .env.prod -f docker-compose.prod.yml run --rm --no-deps api --migrar
```

El seed del administrador también corre al arrancar la API en cualquier entorno; es
idempotente, así que reiniciar el contenedor no duplica usuarios.

## 7. Levantar el entorno público (en el servidor)

```bash
git clone <repo> app && cd app       # o scp de docker-compose.prod.yml, deploy/ y dist/
cp .env.example .env.prod && nano .env.prod    # <- aquí van los datos del §1
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d
docker compose --env-file .env.prod -f docker-compose.prod.yml ps
```

Verificación:

```bash
curl -fsS https://<DOMINIO>/health          # Healthy
curl -fsS -o /dev/null -w '%{http_code}\n' https://<DOMINIO>/
ss -ltnp | grep -E ':(80|443|1433)\b'       # 1433 NO debe aparecer
```

### 7.1 Variante recomendada: Cloudflare Tunnel (sin dominio, sin abrir puertos)

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml \
  -f docker-compose.tunnel.yml up -d
docker logs reservas-tunel 2>&1 | grep -o 'https://[a-z0-9-]*\.trycloudflare\.com' | head -1
```

La URL `https://<aleatorio>.trycloudflare.com` ya es accesible desde Internet:
Caddy sirve el SPA en `/` y reenvía `/api/*` y `/health` a la API. En este modo
`.env.prod` puede llevar `DOMINIO=localhost` y `apiBaseUrl: ''` en el SPA.
Al reiniciar el contenedor `reservas-tunel` la URL cambia (usar un túnel nombrado
si se necesita una URL fija).


## 8. Checklist antes de decir "está publicado"

- [ ] `.env.prod` con permisos `chmod 600` y fuera de Git.
- [ ] Puerto **1433 cerrado** al exterior (en `docker-compose.prod.yml` mssql no publica puertos).
- [ ] `ASPNETCORE_ENVIRONMENT=Production` y `Https__Redirigir=false` (TLS lo termina Caddy).
- [ ] `Cors__OrigenesPermitidos__0=https://<DOMINIO>` (sin `localhost`).
- [ ] `JWT_KEY` y `MSSQL_SA_PASSWORD` distintos de los de desarrollo.
- [ ] `SEED_ADMIN_PASSWORD` cambiada tras el primer login.
- [ ] Respaldo: `docker run --rm -v reservas-espacios_mssql-datos:/d -v $PWD:/b alpine tar czf /b/mssql-$(date +%F).tgz /d`.
- [ ] Renovación TLS automática (Caddy la hace sola; comprobar `docker logs reservas-caddy`).

## 9. Consultar la base de datos

### 9.1 Sin abrir ningún puerto (lo más rápido)

```bash
PASS="$(grep '^MSSQL_SA_PASSWORD=' ~/reservas-app/.env.prod | cut -d= -f2-)"
docker exec -it reservas-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -P "$PASS" \
  -Q "SELECT name FROM sys.databases"
docker exec -it reservas-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -P "$PASS" \
  -d ReservasEspaciosDb -Q "SELECT COUNT(*) AS usuarios FROM AspNetUsers"
```

### 9.2 SSMS / Azure Data Studio desde tu equipo (túnel SSH)

El 1433 no está publicado: se abre solo en `127.0.0.1` del servidor y se entra por túnel.

```bash
# 1) En el servidor: publicar 1433 solo en loopback
docker compose --env-file .env.prod -f docker-compose.prod.yml \
  -f docker-compose.acceso-bd.yml up -d mssql

# 2) En tu equipo (deja esta terminal abierta)
ssh -N -L 1433:127.0.0.1:1433 sergio-parra@192.168.1.10
```

En SSMS: servidor `localhost,1433`, autenticación **SQL Server**, usuario `sa`,
contraseña de `.env.prod` y marcar *Trust server certificate*.

Para cerrar el acceso: `Ctrl+C` en el túnel y volver a levantar mssql sin el archivo
`docker-compose.acceso-bd.yml`.

Opcional, usuario de solo lectura para no usar `sa`:

```sql
CREATE LOGIN consulta WITH PASSWORD = 'Clave.Consulta.2026!';
USE ReservasEspaciosDb;
CREATE USER consulta FOR LOGIN consulta;
ALTER ROLE db_datareader ADD MEMBER consulta;
```

### 9.3 Alternativa de red local (menos segura)

Publicar `192.168.1.10:1433:1433` permite conectarse desde cualquier equipo de la casa
sin túnel, pero deja el `sa` accesible en la LAN. No usar en redes compartidas.

