# M8 — DevEx / CI-CD / despliegue

## M8.1 — Dockerización (Web, Api, Worker) + compose para dev

Cada host tiene un `Dockerfile` multi-stage (SDK para build, runtime/aspnet para ejecutar).
El **build context es la raíz del repo** (para resolver los `ProjectReference`).

```bash
# Build individual
docker build -f src/NeoSTP.Api/Dockerfile    -t neostp-api .
docker build -f src/NeoSTP.Web/Dockerfile    -t neostp-web .
docker build -f src/NeoSTP.Worker/Dockerfile -t neostp-worker .
```

### Entorno completo con docker compose

`docker-compose.yml` levanta SQL Server 2022 + Api (8080) + Web (8081) + Worker.

```bash
# Define secretos en un .env (gitignored) o variables del host:
#   SA_PASSWORD=Your_strong!Passw0rd
#   JWT_KEY=<clave-32+ chars>
docker compose up -d --build
```

- La cadena de conexión apunta a `Server=db` (red interna de compose); el Worker espera a que
  la BD esté `healthy`.
- **Secretos:** nunca se hornean en la imagen (`.dockerignore` excluye `appsettings.Local.json`).
  Se inyectan por variable de entorno (`ConnectionStrings__NeoStpDb`, `Jwt__Key`, etc.) o `.env`.
- Web expone `8081`, Api `8080`; ambos con health checks en `/health/ready` (ver M3.2).

## Pendiente (mayor alcance)

- **M8.2** Pipeline de despliegue por entorno (dev/staging/prod) con migraciones controladas
  (revisar SQL con `dotnet ef migrations script` antes de aplicar).
- **M8.3** Datos demo/seed reproducibles + reseteo rápido (hoy en la skill `neostp`:
  `seed-empresa-demo`, `seed-reset`).
