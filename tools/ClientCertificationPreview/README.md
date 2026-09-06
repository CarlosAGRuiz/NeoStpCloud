# Preview local de certificación del cliente 23

Arnés de diagnóstico para generar en memoria un documento sintético de cada tipo 01, 03, 11 y 14 mediante `DteGeneratorService` del código fuente actual. No firma, transmite, reserva correlativos, guarda documentos, inicia hosts ni registra servicios de aplicación. `ReadyForTransmission` es siempre `false` por diseño. `Passed` sólo indica que se generaron los cuatro documentos; no significa validación fiscal ni autorización de emisión.

## Ejecución desde la raíz del repositorio

```powershell
dotnet restore tools/ClientCertificationPreview/ClientCertificationPreview.csproj --source https://api.nuget.org/v3/index.json --nologo
dotnet build tools/ClientCertificationPreview/ClientCertificationPreview.csproj -c Release --no-restore --nologo
dotnet tools/ClientCertificationPreview/bin/Release/net10.0/ClientCertificationPreview.dll --self-test
dotnet tools/ClientCertificationPreview/bin/Release/net10.0/ClientCertificationPreview.dll --empresa 23 --expected-nit 06232705261148
```

El origen NuGet explícito evita los feeds DevExpress globales inexistentes observados en este Windows; no cambia la configuración global. `NJsonSchema` está fijado a 11.1.0 mediante `PackageReference`, sin depender del directorio `bin` de otra herramienta.

Los argumentos de identidad se verifican antes de leer configuración o abrir SQL. La conexión se obtiene de los JSON publicados de la API en orden base, Development y Local; sólo se conserva la selección de conexión y opciones territoriales/esquema. Se permiten exclusivamente el servidor local, base `NeoSTP_Cloud`, motor 16, empresa 23 con NIT esperado y ambiente `PRUEBAS`. SQL contiene únicamente consultas `SELECT` fijas y la conexión se cierra antes de generar documentos. `ApplicationIntent=ReadOnly` no sustituye a los permisos SQL: la ausencia de escrituras procede del código acotado del arnés.

La consulta de empresa proyecta sólo identidad coincidente, ambiente, actividad, territorio y códigos de establecimiento/punto de venta. No selecciona credenciales, tokens, certificados, dirección, nombre ni contenido fiscal existente. Nombres, receptor, detalles, direcciones, contactos e identificadores de documento son sintéticos; el NIT esperado se usa sólo en memoria. Los errores de receptor sintético no son hallazgos sobre el receptor o emisor real del cliente. La salida contiene indicadores, tipos/versiones, códigos de error y rutas de campo; no exporta payloads.

## Configuración, catálogos y esquemas

- La generación usa el código fuente actual compilado, no el binario desplegado. Los posibles overrides del proceso activo no se inspeccionan: `RunningProcessConfigurationVerified=false`.
- Se registran hashes de los fuentes, de la selección no secreta de opciones de generación y de cada esquema utilizado. No se publica configuración secreta ni su hash.
- El territorio se resuelve con el mismo helper puro `DteTerritoryResolver` utilizado por la aplicación, filtrando municipio y distrito por sus padres. La normalización restante del tipo de establecimiento es una equivalencia local. Catálogo de empresa tiene preferencia sobre el global; selecciones ambiguas o inactivas se rechazan. El helper conserva su compatibilidad explícita con códigos históricos numéricos ausentes del catálogo nuevo cuando distrito es null y la versión no lo requiere; eso no acredita una relación de catálogo nueva.
- La propuesta Centro/Opico sólo se genera si el catálogo instalado permite validar departamento, municipio, distrito, códigos MH y cadena de padres completa. Las referencias oficiales externas encontradas por la auditoría no se inyectan como si ya existieran en SQL. Los códigos del catálogo sintético de los self-checks sirven para probar el mecanismo, no constituyen una fuente fiscal.
- La versión se obtiene del JSON generado con las opciones efectivas seleccionadas. No se fuerza `EsquemaNuevo=true` para aprovechar esquemas más recientes disponibles.
- Los esquemas se leen exclusivamente de `tools/CertHarness/schemas/svfe-json-schemas`. Antes de NJsonSchema se inspecciona todo el JSON y se rechazan referencias `$ref`, `$dynamicRef` y `$recursiveRef` que no sean fragmentos locales `#` o `#/…`; no se permite resolver referencias externas.

## Corrección territorial posterior, 2026-09-05

La versión actual del arnés usa el generador corregido para 01/03/11/14: conserva el municipio resuelto por catálogo y exige distrito cuando el formato emitido lo requiere. No convierte un distrito en municipio por inferencia sobre una versión legacy. La auditoría observó un documento 01 v1 del cliente procesado con municipio `26`; esa observación específica contradice asumir que toda v1 necesita un código municipal antiguo, pero no prueba una regla general de aceptación de Hacienda. Las versiones siguen sin forzarse.

FEX ya no sustituye el municipio o distrito por configuración global. Una muestra incompleta con distrito ausente falla con `DTE_TERRITORIO_DISTRITO` antes de generar; los self-checks demuestran en memoria el caso y una propuesta sintética completa 05/24/15. El catálogo sintético no se guarda ni significa que el catálogo del cliente esté corregido. No se repitió SQL durante este incremento.

La ejecución de `--self-test` pasó 19/19 y quedó capturada en `tmp/cert-territory-2026-09-05/preview-self-checks.json`. El build acotado del arnés pasó con 0 errores y 0 advertencias. La prueba focal de generador, resolución y lote de contingencia pasó 77/77, con TRX `tmp/cert-territory-2026-09-05/generator-territory-final.trx`. El primer intento focal detectó dos fixtures FEX sin tipo de persona; se corrigieron los fixtures antes de la ejecución final. No se consultó SQL ni se emitieron documentos para estas verificaciones.

Una futura ejecución autorizada de lectura guarda `preview-territory-fixed-sanitized.json`, conservando el reporte anterior como evidencia histórica. `ReadyForTransmission` permanece en `false`; las versiones legacy sin esquema local compatible siguen pendientes de validación correspondiente.

## Resultado histórico previo a la corrección, 2026-09-05

La ejecución SQL de lectura produjo `tmp/client-certification-2026-09-05/preview-sanitized.json`. Generó 01 v1, 03 v3, 11 v3 y 14 v1; `EsquemaNuevo` de los JSON publicados resultó `false`. No hay esquemas locales compatibles con las versiones efectivas de 01, 03 y 14, por lo que su validación de esquema queda pendiente. El FEX 11 v3 pasó el esquema local, pero el generador sustituyó el municipio almacenado por un default y utilizó un distrito por defecto: pasar el esquema no acredita corrección territorial.

Centro/Opico permanece bloqueado para la propuesta del arnés porque no se pudo verificar la cadena completa en el catálogo SQL instalado. El indicador de padre municipal no verificado tampoco demuestra por sí solo un departamento equivocado. Ningún dato real fue corregido por este preview.

Los 18 self-checks aislados pasaron: guardas de identidad, normalización, propuesta incompleta/ambigua por parentesco, referencias externas rechazadas, versiones efectivas con ambos valores del toggle y exposición del fallback FEX. No abren SQL ni envían solicitudes. Son verificaciones internas del arnés, no una suite xUnit, ni los escenarios oficiales de certificación, ni evidencia de avance del portal MH. El resumen registrado de esa ejecución está en `tmp/client-certification-2026-09-05/preview-verification-summary.json`.

El build inicial de dependencias mostró una advertencia nullable en un archivo de campaña ajeno al arnés y una advertencia local que se corrigió. El build final acotado del arnés, con `-p:BuildProjectReferences=false` sobre dependencias ya compiladas, terminó con 0 errores y 0 advertencias. Esto no afirma un build final de toda la solución.
