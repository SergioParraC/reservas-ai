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

Todo lo anterior va a un archivo **`.env.prod`** en el servidor (nunca a Git),
partiendo de `.env.example`.

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
cd src/frontend && npm ci && npm run build   # genera dist/frontend
```

Antes del build, en `src/frontend/src/environments/environment.production.ts` pon
`apiBaseUrl: 'https://<DOMINIO>'` (mismo dominio servido por Caddy).

## 6. Migraciones (en producción son un paso explícito)

`Program.cs` solo migra automáticamente en `Development`. Genera el bundle una vez:

```bash
dotnet ef migrations bundle --project src/Backend.Infrastructure \
  --startup-project src/Backend.Presentation.Api -o efbundle -r linux-x64 --self-contained
```

En el servidor, con la base ya arriba:

```bash
docker compose -f docker-compose.prod.yml up -d mssql
docker run --rm --network reservas-espacios_interna \
  -v "$PWD/efbundle:/efbundle" mcr.microsoft.com/dotnet/runtime-deps:10.0 \
  /efbundle --connection "Server=mssql,1433;Database=<BD_NOMBRE>;User Id=sa;Password=<MSSQL_SA_PASSWORD>;Encrypt=True;TrustServerCertificate=True;"
```

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

## 8. Checklist antes de decir "está publicado"

- [ ] `.env.prod` con permisos `chmod 600` y fuera de Git.
- [ ] Puerto **1433 cerrado** al exterior (en `docker-compose.prod.yml` mssql no publica puertos).
- [ ] `ASPNETCORE_ENVIRONMENT=Production` y `Https__Redirigir=false` (TLS lo termina Caddy).
- [ ] `Cors__OrigenesPermitidos__0=https://<DOMINIO>` (sin `localhost`).
- [ ] `JWT_KEY` y `MSSQL_SA_PASSWORD` distintos de los de desarrollo.
- [ ] `SEED_ADMIN_PASSWORD` cambiada tras el primer login.
- [ ] Respaldo: `docker run --rm -v reservas-espacios_mssql-datos:/d -v $PWD:/b alpine tar czf /b/mssql-$(date +%F).tgz /d`.
- [ ] Renovación TLS automática (Caddy la hace sola; comprobar `docker logs reservas-caddy`).
