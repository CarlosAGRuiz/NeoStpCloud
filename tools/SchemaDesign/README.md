# SchemaDesign — EF sin conexión

Herramienta exclusiva para generar/revisar migraciones y SQL sin arrancar API/Web/Worker.
No carga appsettings, secretos ni configuración del cliente. La fábrica requiere --offline-schema
y su interceptor rechaza cualquier apertura de conexión, síncrona o asíncrona.
No sirve para aplicar migraciones.

Compilar antes de usar --no-build:

~~~powershell
dotnet restore tools/SchemaDesign/SchemaDesign.csproj --source https://api.nuget.org/v3/index.json
dotnet build tools/SchemaDesign/SchemaDesign.csproj -c Release --no-restore
dotnet ef migrations has-pending-model-changes --project src/NeoSTP.Infrastructure --startup-project tools/SchemaDesign --configuration Release --no-build --context NeoStpDbContext -- --offline-schema
~~~

Para revisar el incremento GL-0A ya preparado:

~~~powershell
dotnet ef migrations script 20260825233802_CAT020_PaisIso3166Alfa2 20260904021242_GL0A_RefreshSessionContext --idempotent --output tmp/refresh-context.sql --project src/NeoSTP.Infrastructure --startup-project tools/SchemaDesign --configuration Release --no-build --context NeoStpDbContext -- --offline-schema
~~~

La generación usa el proveedor SQL Server sin ejecutar el SQL. Aplicarlo requiere un procedimiento
separado con aprobación, backup y ensayo de restauración. No usar el arranque normal de la aplicación
como mecanismo de revisión: actualmente ejecuta migraciones/seed.
