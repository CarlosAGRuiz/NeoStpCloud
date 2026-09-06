# Candidato de publicación aislado

Este procedimiento prepara binarios para revisión. No configura producción, instala servicios, modifica tareas programadas, inicia hosts, ejecuta workers ni conecta con bases o proveedores.

## Inventario

- `scripts/install-local-autostart.ps1` sirve para el entorno local: publica API/Web, copia explícitamente `appsettings.Local.json`, detiene y registra tareas programadas. No se reutiliza para producción.
- `.github/workflows/ci.yml` ejecuta restore/build/tests; no entrega tres publicaciones de producción.
- Los tres csproj son .NET 10 y no tienen exclusiones explícitas para `appsettings.Local.json`. Web/API usan Web SDK; Worker usa Worker SDK.
- Los tres Program cargan opcionalmente `appsettings.Local.json`. El candidato excluye todos los `appsettings*`, incluso el archivo base, para evitar asumir que la configuración de desarrollo sea válida en producción.

## Contrato con operaciones

`Publish-ProductionCandidate.ps1 -ValidateOnly` valida rutas y reglas sin dotnet ni escribir un candidato. Sin ese switch realiza publicaciones Release, framework-dependent, secuenciales con paquetes ya restaurados. Requiere el turno exclusivo de compilación del coordinador. La importación MSBuild se limita a ese comando; no altera proyectos ni configuración del repositorio.

Cada ejecución utiliza una ruta nueva `tmp/production-candidate/<UTC>-<GUID>/` con carpetas `web`, `api`, `worker`, `logs` y `candidate-manifest.json`. No acepta rutas de destino arbitrarias, no reutiliza directorios y no borra resultados anteriores. Rechaza enlaces/reparse points en los destinos. Los tres roots deben contener su DLL, deps y runtimeconfig.

`ProductionCandidate.targets` excluye configuración local, llaves, certificados, storage, logs, uploads y backups del contenido antes de asignar rutas. Filtra también `ResolvedFileToPublish` antes de copiar al destino, con una lista explícita de archivos de runtime y assets. StaticWebAssets usa otro pipeline: el preflight inspecciona todos los wwwroot existentes antes de dotnet (paths, reparse points y texto); un target adicional valida su metadata antes de copiar. El marcador virtual del SDK `#[.{fingerprint}]?` se normaliza sólo para validar la ruta relativa. Se conservan LICENSE, LICENSE.md y LICENSE.txt legítimos. El postflight exige el marcador del filtro, vuelve a aplicar las reglas, rechaza enlaces y busca claves privadas/conexiones con contraseña en archivos de texto sin mostrar su contenido. Registra SHA256 y tamaño de cada archivo por root. Es un control de publicación, no un detector universal de secretos embebidos en código/binarios; estos requieren revisión del código y del origen de compilación.

`PublicationComplete=true` significa que los tres roots pasaron la validación; **`ReadyForDeployment` siempre queda false**. El candidato carece de configuración de entorno y no debe ejecutarse directamente. El paso siguiente debe validar configuración explícita de producción con referencias protegidas a secretos, rutas persistentes y permisos de DataProtection/storage, bindings, ambiente, esquema existente y separación de empresas. Nunca se importan esos recursos persistentes a los roots de publicación.

El preflight operativo posterior debe tomar exactamente estos tres roots, verificar todos los hashes del manifiesto y rechazar cambios/agregados/ausencias; su evidencia debe enlazar CandidateId y hash del manifiesto. Las validaciones de configuración y esquema no sustituyen pruebas reales de credenciales productivas. Ningún paso de este script pide ni recibe secretos.

## Estado

Publicación real completada el 2026-09-05 en `tmp/production-candidate/20260905T184102Z-7c558335c51647c3bb8ca49beafc271c/`: Web 349, API 154 y Worker 144 archivos (647 hashes revalidados, cero fallas). Preflight revisó 65 assets fuente; login-password.js está presente con hash idéntico. El manifiesto y publication-verification.json enlazan la evidencia. Dos intentos anteriores se conservaron: rechazo de licencias de dependencias y rechazo del marcador virtual de fingerprint en esas licencias; ambos fueron correcciones del tooling, sin cambios de producto. No se inició ningún host, servicio ni acceso a SQL. El turno dotnet fue devuelto al coordinador para build/suite completos.
Corte posterior Windows/dominios: candidato 20260905T204702Z-25aabc1a42d34982baf5723f74af0408, 647 archivos revalidados. Ver ../../docs/auditoria-2026-09-03/OPS-Windows-Dominios-Recuperacion-2026-09-05.md. Incluye binding HTTPS actualizado, todavía sin configuración privada ni instalación.
