# CERT-2 — cuatro tipos autorizados y piloto del cliente DANIEL

## Alcance y estado del corte local

Prioridad confirmada por el usuario: completar las pruebas del cliente 23, manteniendo únicamente 01 Factura, 03 Crédito Fiscal, 11 Exportación y 14 Sujeto Excluido. La captura aporta metas 90/75/90/25 y un aprobado en Factura: 279 pendientes según esa captura. No aporta una matriz detallada ni una lectura actual del portal. Los ensayos locales siguientes no acreditan avance en esos contadores.

La autorización fiscal por empresa se persiste en `Dte_Configuracion.TiposDteAutorizadosCsv`. La API ofrece los tipos disponibles y la Web los consume. Crear, generar, validar, firmar y enviar (incluido lote de contingencia) comprueban la autorización. Configuración vacía o mal formada deniega; NULL mantiene la compatibilidad explícita de empresas todavía no provisionadas. El formulario ordinario de configuración no puede ampliar esta autorización. Se conserva la consulta de documentos históricos.

La campaña interna permite un presupuesto separado, finito y durable para PRUEBAS. Sólo el consumo persistido y coherente entre campaña, documento, empresa, NIT, tipo, huellas y fechas se excluye de la cuota comercial. Marcar un documento como CERT o PRUEBAS no concede exención. La caducidad/revocación bloquea nuevos avances fiscales sin convertir retroactivamente consumos válidos en consumo comercial. No cambia Starter Facturación ni activa módulos adicionales.

## Geografía y documentos anteriores

La dirección confirmada corresponde a La Libertad / La Libertad Centro / San Juan Opico. El catálogo oficial contrastado aporta 05/24/15; fuente y celdas en [evidencia oficial](evidencia/mh-reference-2026-09-05/README.md). La resolución valida el parentesco antes de convertir nombres a códigos, rechaza ambigüedad y no sustituye municipio por distrito. El código 15 no debe inventarse como municipio legacy.

La revisión acotada de respuestas anteriores identificó ocho rechazos almacenados de Hacienda (código 096), dos borradores y una Factura PROCESADO con sello. La factura aceptada era v1 y emitió municipio 26; este dato corrige la hipótesis de que v1 necesariamente exige el municipio antiguo. No prueba que el territorio anterior fuera correcto para el establecimiento. Los ocho rechazos no se trataron como respuestas perdidas ni se reenviaron.

## Evidencia técnica validada

- Compilación Release: 0 errores y 0 advertencias. Suite final: 2,304 unitarias y 9 integraciones aprobadas, sin omisiones. Las 195 focales están incluidas en la suite, no se suman aparte.
- Base temporal de fundación: 26 comprobaciones SQL, incluida competencia de 12 solicitudes por un cupo, replay y rollback. Fue retirada conservando evidencia.
- Backup COPY_ONLY/CHECKSUM nuevo, verificación y restauración protegida. Ensayo de 79 a 91 migraciones: 22 comprobaciones aprobadas, DBCC sin errores y preservación de las columnas originales verificadas en 74 tablas con EmpresaId. Esto no es una prueba de toda la base ni de convivencia entre binarios de distintas versiones.
- Aprovisionamiento aplicado exclusivamente a la copia: CSV 01,03,11,14, dirección confirmada y campaña piloto de cuatro documentos, uno por tipo. El alta de San Juan Opico añade una referencia al catálogo global; la configuración fiscal y campaña afectan sólo a empresa 23.
- Runner: 29 autocomprobaciones y recorrido SQL de cuatro borradores, consumos y correlativos dentro de una transacción revertida. Reversión verificada, cuota comercial sin cambios y bloqueo por expiración/revocación probado.
- Los cuatro JSON pasan los esquemas oficiales locales: 01 v2, 03 v4, 11 v3 y 14 v2. El runner selecciona estas versiones explícitamente en memoria; no modifica la selección global del host. No hay esquemas locales oficiales de las versiones antiguas de 01/03/14.

Evidencia del corte: `tmp/client-certification-release-2026-09-05/` (build, TRX, backup/rehearsal y provisión de copia), `tmp/certification-access-2026-09-05/`, `tmp/cert-territory-2026-09-05/` y `tmp/client-certification-runner/20260905T222553Z-5ab79d92cce2462cb9fa44e696994bf4.json`. Se conservan intentos fallidos y resultados históricos; no reemplazarlos por el último resultado.

## Restricciones del piloto y siguiente ejecución

Campaña reservada: `5f681bfb-149d-4650-9a1b-5723feeebef3`, matriz interna `CLIENT23-PILOT-4-V1`, cuatro consumos máximos. Una reserva previa se consulta y nunca continúa enviándose automáticamente, aunque permaneciera BORRADOR. El transporte sólo admite HTTPS apitest.dtes.mh.gob.sv, rutas de autenticación/recepción previstas y una recepción por ejecución; bloquea redirecciones y otros destinos. Correo y webhooks externos están deshabilitados para este recorrido. Receptores sintéticos y concepto explícito de prueba, sin operación comercial real.

El siguiente paso es actualizar el esquema y el binario activo de forma coordinada, provisionar empresa 23, verificar la UI autenticada y ejecutar un piloto por tipo, deteniéndose ante rechazo o resultado incierto. El usuario volvió a iniciar sesión como Carlos.Mena en DANIEL. La UI anterior muestra todavía once tipos y once documentos históricos.

El inventario de procesos con QueryFullProcessImageName confirma Web/API desde `src/.../bin/Debug/net10.0`, iniciados el 2 de septiembre mediante procesos dotnet. Las tareas programadas apuntan a `out/local-autostart`, pero figuran Ready y no son los procesos que atienden los puertos 5031/5058. No atribuir la configuración efectiva únicamente a los archivos de out. El despliegue temporal conserva Development y la identidad Windows/DPAPI, con migraciones/semillas/bootstrap explícitamente apagados. Cambiar sólo a Production impediría arrancar con la configuración actual.

La certificación completa, el cierre de producción general de NEO, la provisión de servicios sin sesión Windows, recuperación fuera del equipo y CLI-23 continúan pendientes. SMTP del cliente está configurado y el usuario confirmó recepción. Septiembre USD 15 vence el 30/09; no se marca pagado ni se sustituye el acuerdo por Wompi.

## Actualización activa — 22:46 UTC

El cambio activo fue ejecutado después del ensayo y de la revisión independiente. Se detuvieron los procesos Debug identificados y sus padres dotnet, verificando rutas, propietarios y puertos. Stop-Process solicitó un acceso que Windows denegó; la detención se realizó con el permiso específico PROCESS_TERMINATE concedido por Windows, sin elevar privilegios ni cambiar ACL. Las tareas anteriores se exportaron a la carpeta protegida y se deshabilitaron durante el cambio.

La actualización explícita79→91 creó respaldo COPY_ONLY/CHECKSUM fresco y comprobó VERIFYONLY. Resultado `tmp/client-schema-upgrade/408bfa5534054db8bef2f31e50194768/results.json`: historial91 exacto, DBCC0, preservación de todas las filas/columnas originales de103tablas (todoslos tenants/globales; excluidosrowversion/historialEF). Ese respaldo fresco se verificó, pero su restauración completa no se volvió a ensayar; la restauración anterior es otra evidencia.

Se aplicó la configuración23enNeoSTP_Cloud: CSV01,03,11,14, direcciónCentro/Opico y campaña piloto4. NEO2 no cambió de ambiente. API/Web arrancaron desde el candidato preparado mediante las tareas Windows existentes, conservando identidad/clave y con las cinco opciones de migración/semillas/bootstrap desactivadas en CLI. PID/path/CLI/hashInfrastructure confirmados en `live-runtime.json`. Los dos `/health/ready` locales, el API público y el loginWeb público respondieron200. Las tareas figuranRunning; Worker no se instaló.

La sesión anterior requirió autenticarse nuevamente tras el cambio. Verificación visual autenticada pendiente de la nueva entrada. Primer intento piloto01se detuvo antesdeSQL/reserva/autenticación/envío por un guard de herramienta que exigía contraseña de certificado; el firmadorHaciendaCert usaXMLsincontraseña. Se corrige ese preflight conforme al formato real antes de continuar; no se solicitó al cliente una credencial que ese formato no necesita.

### Verificación autenticada y rechazo piloto

Tras renovar la sesión, se verificaron exactamente los cuatro tipos en filtro, menú NuevoDTE y selector del formulario. Acceder directamente a Create?tipo=05 devuelve403; Create?tipo=01abre el formulario. No se guardó ningún borrador desdeUI. Evidencia `live-ui-types.json`.

Primer envío externo: documento1016, una autenticación y una recepción, respuestaHTTP400/MHERROR096sin sello. El diagnóstico persistido es rechazo confirmado paraCORREGIR_DATOS, no recepción perdida. Jsonidentificacion.version=2, pero VersionDte/sobre=1. Se detuvieron los otros tres pilotos; no hay pruebas nuevas aceptadas todavía. Se corrige el producto para persistir versión desde el JSON generado y denegar sobres/JWS incoherentes antes de HTTP. La recuperación revisada debe conservar1016/UUID/control/consumo, la respuesta original y un marcador único de reintento; no crear otrafactura ni ampliar el presupuesto.

## Actualización — corrección activa y primeras aceptaciones, 23:14 UTC

El candidato `20260905T230231Z-ee09ccfa549d4c93849d0eb7108fe08b` está activo en Web/API. Compilación sin errores ni advertencias, **2,330 unitarias y 9 integraciones aprobadas**. La generación persiste la versión efectiva del JSON; el transporte deniega antes de HTTP si el sobre no coincide con la identidad del JWS. La suite anterior de 2,304 corresponde al corte previo, no al actual. El cambio de binarios no aplicó migraciones. Salud local y pública 200; sesión autenticada y cuatro tipos nuevamente verificados sin modificar el formulario que el usuario tenía abierto.

El runner de recuperación pasó 54 autocomprobaciones sin SQL ni red. El reintento exclusivo de 1016 conservó UUID, número de control, consumo y huellas del ledger; archivó la respuesta anterior y registró un marcador durable que impide repetirlo. Hacienda aceptó **1016, Factura v2, con sello**, el 05/09 a las 23:11 UTC. Una recepción y ningún cupo adicional. Evidencia: `tmp/client-certification-runner/20260905T231056Z-a2723eebdd594a71ab862d39f4df6c06.json`.

El piloto **1017, Crédito Fiscal v4**, fue rechazado de forma confirmada con HTTP 400/MH 009: el NIT sintético del receptor no existe. Las versiones JSON y sobre sí coinciden; no es evidencia de otro fallo de generación. Su UUID/control y consumo se conservan para una recuperación revisada con receptor reconocido. Evidencia saneada: `tmp/client-certification-release-2026-09-05/pilot-03-response.json`.

Una vez identificada esa causa exclusiva del receptor CCF, continuó el piloto independiente de Exportación: **1018, Factura de Exportación v3, aceptada con sello**, a las 23:14 UTC. Evidencia: `tmp/client-certification-runner/20260905T231404Z-5f0ce4a5d3f04f729d48831c2b44850e.json`.

Hay dos nuevas aceptaciones locales comprobadas en Hacienda; aún no se leyó el contador del portal. Sujeto Excluido, recuperación de CCF y campaña posterior siguen abiertos. Además, se detectó que la Web ordinaria conserva el selector global legacy, mientras el runner selecciona los esquemas nuevos en memoria: se prepara una política operacional por empresa, NIT y ambiente para que DANIEL use las mismas versiones sin cambiar la selección de NEO ni de los otros clientes.

## Cuatro pilotos aceptados y política activa — 23:28 UTC

El reintento exclusivo de CCF 1017 fue aceptado con sello usando los datos públicos verificados de NEO como receptor, exclusivamente en PRUEBAS y con concepto sin operación comercial. Se conservaron UUID, control y consumo; la respuesta 009 original quedó preservada. La huella del snapshot fiscal de NEO permaneció igual antes/después y no se modificó su configuración. Antes del reintento se agregó la referencia global de Ayutuxtepeque 06/23/03, verificada con el Excel oficial (Hoja1, fila158); no se alteraron las direcciones de empresas.

El piloto de Sujeto Excluido 1019, con documento sintético tipo37 (Otro) para el ensayo, fue aceptado con sello. El catálogo oficial identifica Pasaporte como03; la denominación anterior de pasaporte para37 era incorrecta. Resultado de pilotos:

| DTE | Tipo | Versión | Estado |
|---|---|---:|---|
| 1016 | 01 Factura | 2 | PROCESADO con sello |
| 1017 | 03 Crédito Fiscal | 4 | PROCESADO con sello |
| 1018 | 11 Exportación | 3 | PROCESADO con sello |
| 1019 | 14 Sujeto Excluido | 2 | PROCESADO con sello |

Son cuatro documentos y seis recepciones durante este trabajo: dos rechazos corregidos sobre sus mismos documentos y cuatro aceptaciones. No se consumieron nuevos cupos para los reintentos. Los documentos anteriores no se reenviaron.

La política operacional `Dte:TenantSchemas:23` comprueba NIT, ambiente PRUEBAS y perfil `MH_20260825`. La comparten generación y resolución territorial; una entrada incompatible bloquea. Sin entrada se conserva el comportamiento global. Los eventos fiscales no cambian de esquema. Validación del corte: **2,354 unitarias y 9 integraciones aprobadas**, incluidas las 219 focales; runner 66 autocomprobaciones. No sumar los cortes históricos a estos totales.

Candidato activo: `20260905T232433Z-9062dc25354442429d2ef07f76f24d94`, manifiesto SHA256 `98722A2B38A922BBC389FDF7274136F0A729F4EB0382867B03911FC9E75C92D8`. Configuración privada de ambos hosts incluye sólo la entrada de DANIEL; el flag global se conserva. El cambio no alteró el esquema SQL91. Salud pública/local 200 y sesión existente operable: los cuatro pilotos aparecen PROCESADO y el filtro sólo contiene los cuatro tipos autorizados.

Evidencia nueva: `tmp/dte-tenant-schema-2026-09-05/{full-build.log,full-tests.log,live-release.json,live-ui-health.json}`, `tmp/client-certification-runner/20260905T232409Z-ecf819dd6ee34e8b9fd031d382e43798.json` y `20260905T232433Z-d40f9c852d7c4e92bdca8d5b1f9ef12e.json`.

El piloto de los cuatro tipos queda comprobado; la campaña de 275 documentos adicionales y el contraste final del portal siguen pendientes. Ningún resultado aquí equivale a autorización productiva del cliente ni al cierre de producción general de NEO.
