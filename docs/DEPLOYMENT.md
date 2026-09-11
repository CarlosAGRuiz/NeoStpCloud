# Deployment de NeoSTP Cloud

Esta es la guía pública para desplegar una misma versión de API, Web y Worker. No contiene secretos, nombres privados de servidores ni evidencia de clientes.

## Matriz de ambientes

| Recurso | STAGING | PRODUCTION |
|---|---|---|
| Web | https://staging.neostp.com | https://app.neostp.com |
| API | https://staging-api.neostp.com | https://api.neostp.com |
| Base | NeoSTP_Staging | NeoSTP_Production |
| Hacienda por empresa | PRUEBAS | PRODUCCION |
| Data root | C:\ProgramData\NeoSTP\Staging | C:\ProgramData\NeoSTP\Production |
| Key ring | ...\Staging\DataProtection | ...\Production\DataProtection |
| Logs y backups | carpetas de STAGING | carpetas de PRODUCTION |

Los overlays versionados appsettings.Staging.json y appsettings.Production.json fijan dominios, nombres de base, rutas separadas y flags seguros. Los valores sensibles siempre llegan desde el secret store o variables de entorno del servicio.

## Preflight obligatorio

1. Publicar los tres proyectos desde el mismo commit o tag.
2. Crear una identidad de servicio distinta por ambiente y concederle solo los permisos requeridos.
3. Crear la base indicada por el overlay; nunca restaurar STAGING encima de PRODUCTION.
4. Crear el data root, su carpeta DataProtection, logs y backups con ACL para la identidad de servicio.
5. Instalar un certificado RSA de al menos 2048 bits con llave privada accesible por esa identidad.
6. Inyectar secretos y configuración particular.
7. Aplicar el script SQL aprobado.
8. Iniciar API, Web y después Worker.
9. Validar /health/live, /health/ready, login y tenant antes de abrir tráfico.

## Variables mínimas por servicio

Usar la sintaxis de configuración de ASP.NET Core:

    ASPNETCORE_ENVIRONMENT=Staging|Production
    ConnectionStrings__NeoStpDb=<secret-store>
    DataProtection__CertificateThumbprint=<secret-store>
    Jwt__Key=<secret-store>                       # API y Web

Si una capacidad opcional no está contratada, conservar su provider como Disabled. Al activarla, inyectar el provider y todas sus opciones en la misma operación:

    Email__Provider=Smtp
    Email__Smtp__Host=<config>
    Email__Smtp__Username=<secret-store>
    Email__Smtp__Password=<secret-store>

    Scan__Provider=Gemini
    Scan__Gemini__ApiKey=<secret-store>

    WhatsApp__Provider=Meta
    WhatsApp__Meta__Token=<secret-store>
    WhatsApp__Meta__PhoneNumberId=<config>

    Push__Provider=Fcm
    Push__Fcm__ProjectId=<config>
    Push__Fcm__ClientEmail=<secret-store>
    Push__Fcm__PrivateKey=<secret-store>

Billing exige las credenciales propias del provider seleccionado. Disabled devuelve un error operativo explícito y nunca simula éxito. Hacienda y el firmador DTE no admiten Disabled en STAGING ni PRODUCTION.

## Migrations

API, Web y Worker tienen migrations, seed y bootstrap deshabilitados en ambientes desplegados. El flujo es:

    migration revisada
    → script SQL idempotente versionado como artefacto de release
    → backup previo
    → aplicar en STAGING
    → smoke
    → aprobación
    → aplicar en PRODUCTION
    → validar schema

Generación reproducible, sin conectarse a una base ni aplicar cambios:

    ./tools/Database/New-MigrationRelease.ps1 -ReleaseVersion v1.0.0-rc.1

El comando restaura la versión local fijada de dotnet-ef, valida que el modelo no tenga cambios
pendientes, compara deploy/migrations/manifest.json contra las migrations rastreadas y produce:

    artifacts/migrations/v1.0.0-rc.1/
    ├── NeoSTP.Migrations.idempotent.sql
    └── manifest.json

El manifest del artefacto fija commit, migration final y hashes SHA-256 del snapshot y del SQL.
Debe conservarse junto con los binarios de la misma release. El SQL continúa requiriendo revisión,
backup, ensayo en STAGING y aprobación antes de PRODUCTION.

El arranque falla si el historial de migrations no coincide exactamente con el modelo publicado.

## Activación del Worker

Los overlays dejan Worker:Enabled=false. Después del preflight y health de API/Web, el operador cambia Worker__Enabled=true, reinicia el servicio y verifica ejecución, errores y latencia de jobs.

## Rollback

1. Retirar tráfico o detener Worker.
2. Reinstalar los tres artefactos de la versión anterior.
3. Revertir schema únicamente con un script previamente revisado; si no es seguro, restaurar el backup aislado.
4. Restaurar el mismo key ring y certificado. Nunca borrar llaves históricas.
5. Repetir health, login, tenant y smoke fiscal.

DNS, TLS, Cloudflare, monitoreo externo, backup off-site y una restauración completa siguen siendo gates de infraestructura: no pueden declararse completos únicamente con estos archivos.

Para el host Windows inicial de STAGING y su instalación reproducible, ver
[STAGING local en Windows](LOCAL-STAGING.md).
