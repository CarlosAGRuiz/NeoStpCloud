# Preparación de producción de NEO — 2026-09-05

> Continuación del mismo día: [configuración, servicios y claves](OPS-Configuracion-Servicios-Claves-2026-09-05.md). Las observaciones y pruebas que siguen identifican el corte anterior; su evidencia permanece intacta.

Estado: incremento de arranque y ensayo aislado validado localmente; instalación activa sin desplegar este candidato y NEO fiscalmente en PRUEBAS. El usuario autorizó preparar producción y probar. Esa autorización no equivale a emitir documentos fiscales ficticios ni a cobrar pagos reales.

## Alcance y configuración observada

NEO es EmpresaId 2, NEO SOFTWARE TECH PRO, SOCIEDAD POR ACCIONES SIMPLIFICADA DE CAPITAL VARIABLE. Licencia Enterprise activa, sin fecha final, 18 módulos asignados. Datos fiscales principales y certificado almacenados; certificado con vencimiento registrado 2031-04-15. Esto no acredita por sí solo firma válida ni acceso actual a Hacienda PRODUCCION.

La configuración fiscal observada es PRUEBAS, establecimiento/punto 0001/0001. Usuario y contraseña cifrada de MH existen en ese contexto; no se presume que la contraseña sea la correspondiente a PRODUCCION. NEO no tiene SMTP propio ni cuenta de cobro configurados. El alcance autorizado documentado previamente contiene tipos 01, 03, 04, 05, 06, 07, 08, 09, 11 y 14; falta implementar/verificar la restricción central por plan y autorización fiscal antes de exponer todos los tipos.

NEO y DANIEL (EmpresaId 23) comparten NeoSTP_Cloud. La base activa conserva 79 migraciones; el código candidato conoce 89. API y Web observadas corren desde el 2 de septiembre, antes de estas correcciones. Los HTTP locales responden salud API 200, recurso protegido 401 y login Web 200; esas respuestas no son una prueba del candidato ni de HTTPS público.

## Incremento OPS-CODE: arranque controlado

- API y Web usan DatabaseStartup. En Production comprueban el historial exacto conocido y aplicado sin migrar ni crear datos. Rechazan esquemas antiguos, adicionales/desconocidos, inaccesibles o sin historial compatible.
- Production rechaza explícitamente migración, semillas de empresa/demo y bootstrap habilitados. No basta confiar en que el flag no se ejecutará después.
- Worker exige activación explícita en Production antes de registrar sus trabajos y cola. Conserva validación de esquema y valores predeterminados de base de solo lectura, también fuera de Production.
- Los errores de configuración/esquema no incluyen cadenas de conexión, SQL ni valores proporcionados.

[Fragmento de configuración](neo-production-startup.fragment.json): controles iniciales de seguridad, SIN secretos y SIN carga automática. Es un fragmento, no una configuración desplegable completa. ASPNETCORE_ENVIRONMENT/DOTNET_ENVIRONMENT=Production se fija en el despliegue; no cambia el ambiente fiscal de las empresas. Configurar proveedor MH, firmador, SQL, claves persistentes, red y proveedores realmente habilitados por separado.

appsettings.Local.json todavía se agrega con precedencia sobre entorno/argumentos en los hosts. El despliegue debe usar publicación y content root aislados, sin copiar el Local de desarrollo, y verificar configuración efectiva sanitizada. No habilitar PermitirMocksEnProduccion para sortear la aceptación. La validación actual de proveedores todavía necesita endurecimiento; este incremento no cierra todo OPS-CODE.

## Ensayo de base y seguridad fiscal

Se generó un respaldo físico nuevo COPY_ONLY con CHECKSUM y se verificó con RESTORE VERIFYONLY. Se restauró mediante MOVE en una base nueva con GUID sobre SQL Server 2022 (motor 16), sin reemplazar la base activa. El respaldo queda en carpeta protegida de SQL Server fuera del repositorio; las evidencias contienen metadatos y huellas, no filas exportadas, certificados ni credenciales.

La copia permite ensayar 79→89, comprobar rechazo del arranque antiguo, validar integridad y revisar planes y conservación de datos por empresa. El cambio fiscal de NEO se ensaya únicamente allí, eliminando de la copia las credenciales/tokens del contexto anterior y sin levantar hosts ni llamar a Hacienda. No reutilizar ese estado de ensayo como credenciales válidas para el corte real.

Resultados finales: **build Release de la solución con 0 advertencias/0 errores; 1961 unitarias y 9 integraciones aprobadas, sin omisiones; ensayo SQL 22/22**. Las 30 pruebas nuevas cubren DatabaseStartup (17) y WorkerStartupPolicy (13); son parte de las 1961, no se suman otra vez.

SQL Server 2022: la copia rechazó arranque Production con 79 migraciones sin escrituras; migración explícita de las diez pendientes a 89; arranque posterior aceptado sin semillas ni escrituras. DBCC CHECKDB registró cero filas/cero errores. NEO resolvió Enterprise con 18 módulos; DANIEL resolvió STARTERFE con exactamente CORE y NEODTE.

Las huellas comparan las columnas originales, salvo rowversion, de 74 tablas con EmpresaId directo, para empresas 2 y 23. No acreditan cada tabla indirecta ni cada flujo de los 18 módulos. Se conservaron esas huellas al migrar y las del cliente al cambiar solo la configuración fiscal clonada de NEO. El contexto de un documento PRUEBAS fue rechazado contra la configuración PRODUCCION del clon. Esto prueba separación de contexto, no autenticación/firma/envío productivos.

El respaldo retenido mide 41,181,696 bytes (39.27 MiB). Ambas bases de ensayo y sus MDF/LDF fueron retiradas con comprobación de GUID, motor y rutas exactas; el respaldo conserva su SHA-256. La base activa sigue en 79 migraciones. El estado CloneRetained=true del resultado corresponde al final del arnés; cleanup-results.json documenta la limpieza posterior.

[Carpeta de evidencias](evidencia/neo-production-2026-09-05/README.md): TRX finales, compilación, resultados SQL, restauración, limpieza y manifiestos. Revisión independiente final sin P1/P2 nuevos en este incremento. Se corrigió una omisión del arnés que no exigía igualdad fuente/clon; la ejecución final exige esa igualdad. Los intentos fallidos previos por digest NULL en tabla vacía y expectativa STARTER en lugar de STARTERFE se conservan en tmp/neo-production/logs/attempt1 y attempt2; no cuentan como aceptación final.

## Secuencia que falta para activar NEO de verdad

1. Completar y registrar la validación del candidato, migración/rollback y respaldos recuperables. El backup SQL de este ensayo no cubre todavía recuperación de storage, certificados externos, key ring y configuración, ni copia fuera del equipo.
2. Cerrar proveedores efectivos, alcance fiscal por empresa y funciones excluidas en backend. Enterprise no significa que todos los proveedores estén configurados ni que todo módulo esté aceptado.
3. Preparar publicación Production con identidad de servicio, mínimo privilegio SQL, HTTPS/proxy y claves recuperables. Verificar Web/API/Worker y arranque sin sesión interactiva. No iniciar este candidato contra la base activa de 79 migraciones.
4. Aplicar en ventana controlada las migraciones aprobadas, desplegar y comprobar salud, autenticación, empresa, permisos, consulta histórica y entrega. Mantener tareas automáticas deshabilitadas hasta su aceptación específica.
5. En NEO seleccionada y con DTE.Configurar, abrir /dte/configuracion, seleccionar PRODUCCION e introducir directamente la credencial correspondiente. SaveAsync exige una contraseña explícita al cambiar ambiente/usuario y elimina el token anterior. El usuario confirmó que dispone del usuario y contraseña de PRODUCCION y puede introducirlos directamente; su ingreso y validación siguen pendientes. Nunca solicitarla por chat.
6. Validar autenticación contra Hacienda y firma de NEO sin transmisión de documentos ficticios. Configurar correo propio y probar entrega a destinatario autorizado. La primera emisión productiva debe corresponder a una operación legítima y conciliar su respuesta, PDF y entrega.
7. Comprobar que DANIEL conserva su empresa, Starter/CORE/NEODTE e historial de PRUEBAS. Su certificación y corte fiscal son independientes. CLI-23 permanece pendiente: cliente activo, implementación pagada y USD 15 al final del mes; no transformar la mensualidad pendiente en pagada.

Las pruebas se pueden seguir ejecutando en el clon y en ambiente fiscal PRUEBAS. Cambiar un selector a PRODUCCION no elimina las puertas operativas ni demuestra que el cliente esté listo.
