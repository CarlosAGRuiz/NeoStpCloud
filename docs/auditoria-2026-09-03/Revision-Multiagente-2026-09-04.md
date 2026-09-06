# Revisión multiagente — 2026-09-04

Rama `codex/gl0a-auth-security`, HEAD `a8c5d16`, más cambios locales GL0A–GL1C sin commit. Auditoría sobre el producto congelado; esta ronda agrega pruebas/evidencias, no corrige los defectos productivos ni despliega.

**Dictamen de esta ronda: NO-GO para producción.** Los conteos verdes no cubren todos los flujos fiscales. Ningún hallazgo se considera corregido por aparecer en este informe. Se completaron los seis roles, incluyendo dos revisiones independientes.

## Organización y criterio de revisión

Se separan auditoría de código, pruebas backend, diseño y pruebas web. Revisores independientes comprueban los resultados backend y las evidencias de diseño/web. Por capacidad, las tareas se ejecutan en tandas; las compilaciones son secuenciales.

- Auditoría backend: código y transiciones, permisos/empresa, límites de recuperación.
- Pruebas backend: compilación, suite ordinaria y caracterización de defectos.
- Diseño: inspección estática de Razor/CSS/JS, sin confundirla con prueba visual.
- Web: fixtures renderizadas desde Razor y pruebas aisladas en navegador; no representa el despliegue ni los datos del cliente.
- Revisión backend: calidad de aserciones, fixtures, transporte real registrado y alcance SQL.
- Revisión diseño/web: trazabilidad de capturas, escenarios y límites de lo efectivamente probado.

Se usó neostp-context para preservar módulos/permisos/empresa y distinguir Web de API; neostp para validar con Release sin ejecutar los hosts que migran/siembran al arrancar. Manuel conserva la APK. DTE11 no se reabre.

## Hallazgos backend

| ID | Prioridad | Hallazgo | Evidencia / límite |
| --- | --- | --- | --- |
| B1 | P1 | Dos llamadas a EnviarAsync pueden superar el guard inicial y transmitir dos veces; una respuesta tardía puede degradar PROCESADO a ERROR. | Código en DteDocumentosService.cs:954,1008,1010,1042. Reproducción con 2 contextos InMemory, barrera en autenticación y respuesta tardía controlada. Fixture inicial ENVIADO sin EnviadoAt: no es una emisión normal completa desde FIRMADO ni una prueba SQL. |
| B2 | P1 | InvalidarAsync local permite convertir un envío incierto en INVALIDADO y despacha webhook sin conciliación. La guía deja de exigir consulta. | DteDocumentosService.cs:1317 y DteDiagnosticoGuia.cs:35. Reproducción servicio real: envío simulado con timeout → ENVIADO → invalidación → relectura y webhook. |
| B3 | P1 | No existe actualización explícita del receptor histórico de un DTE rechazado. Corregir Cliente no actualiza el snapshot al regenerar. | Copia al crear en DteDocumentosService.cs:485; Generar carga empresa/detalles/JSON, no vuelve a copiar Cliente. No se probó con datos reales. |
| B4 | P2 | El diagnóstico API devuelve 400 para SuperAdmin porque no resuelve empresa de soporte. | DteDiagnosticoController.cs:96. Inspección estática; los filtros EmpresaId del servicio sí están presentes. |
| B5 | P1 | La política HTTP real reintenta automáticamente el POST de recepción hasta producir 4 intentos con el mismo payload, sin conciliar el resultado previo. | DependencyInjection.cs:135–144. Revisor independiente ejecutó AddInfrastructure y cliente/factory reales, sustituyendo solo transporte primario: 503 y HttpRequestException después de capturar cuerpo produjeron 4 POST cada uno. No demuestra 4 facturas creadas en MH; son retransmisiones del mismo DTE. |

Conciliación remota y exclusión atómica siguen pendientes. B1/B2/B3 tienen raíces preexistentes; GL1C no los cierra y la guía de recuperación hace visibles sus límites.

## Hallazgos de diseño y web

| ID | Prioridad | Hallazgo | Referencia |
| --- | --- | --- | --- |
| D1 | P1 | Details no presenta Model.Diagnostico ni acciones por RequiereConsultaHacienda; CONTINGENCIA conserva CTA de enviar. No es demostración de bypass backend. | Views/DteDocumentos/Details.cshtml:29,164 |
| D2 | P2 | Botones fiscales se muestran por estado sin filtrar permiso; el POST sí autoriza. | Details.cshtml:143–176 |
| D3 | P2 | Retorno busca solo entre primera página de hasta 200 documentos, sin búsqueda remota. | DteEventosController.cs:297 y CreateRetorno.cshtml:72 |
| D4 | P2 | Labels territoriales no coinciden con IDs manuales; filtros DTE carecen de asociación for/id. | Views/Clientes/_Form.cshtml:13,46,84,94; DteDocumentos/Index.cshtml:46 |
| D5 | P2 | El stepper asigna ERROR a envío e INVALIDADO al final aunque el documento no haya completado etapas anteriores. | Views/Shared/_StepperDte.cshtml:17,21 |

La inspección estática identificó D1–D5. Las pruebas de navegador reprodujeron D1/D2/D5 y las asociaciones de etiquetas del formulario Cliente de D4; la revisión independiente comprobó HTML y capturas. D3 sigue respaldado por código: una fixture de 200 opciones demuestra el límite de la búsqueda local, no una consulta real del documento 201 a la base.

Positivos: PROCESADO completa cinco puntos morados, el visor funciona con clic/teclado y la cascada territorial funciona con ParentCodigo sintético, incluyendo ocultación/limpieza para extranjeros. El autocompletado de cliente y la restricción de retorno a 01/11/14 se revisaron estáticamente. Nada de ello valida el catálogo ni los datos reales del cliente.

## Evidencia de pruebas backend

- `dotnet build NeoSTP.slnx -c Release --no-restore`: código 0, 0 errores/advertencias en la ejecución incremental del agente. La advertencia preexistente CS8604 puede reaparecer al recompilar tests.
- `dotnet test NeoSTP.slnx -c Release --no-build --no-restore --filter "Category!=AuditKnownDefect"`: 1,319 unitarias + 9 de integración, 0 fallidas/omitidas. Integración habitual InMemory, no SQL.
- TRX: `tmp/multiagent-qa/backend/acceptance-baseline_net10.0_20260904092113.trx` (integración) y `acceptance-baseline_net10.0_20260904092132.trx` (unitarias).
- `MultiAgentAuditRegressionTests.cs`: 2 caracterizaciones, `Category=AuditKnownDefect`, `tmp/multiagent-qa/backend/known-defects.trx`. **Verde significa que el defecto fue reproducido; no sumar estas pruebas a las garantías de producción.** Tras corregir deben convertirse en aserciones de comportamiento seguro.
- Los 32 checks SQL de la ronda anterior GL1C cubren reservas/creación/POS concurrentes y envío individual secuencial con receptor sustituido; no se ejecutaron nuevamente en esta ronda y no demuestran exclusión atómica de dos transmisiones ni la política real HTTP.
- Revisión independiente añadió `HaciendaResilienceAuditTests.cs`: 2 caracterizaciones `AuditKnownDefect`, `tmp/multiagent-qa/revision-backend/resilience-audit.trx`, código 0. Se mantuvieron las políticas de retry/circuit breaker/timeout de DI; endpoints y handler son sintéticos y no pueden abrir sockets. La tolerancia HttpClient.Timeout de la prueba se configuró a 55s; ambas reproducciones finalizaron en menos de 10s por caso.
- Las pruebas de contrato DTE llaman controladores con servicios sustituidos; verificar atributos no prueba autorización HTTP. Las nueve pruebas de integración usan InMemory, y el manifiesto de rutas móviles no envía peticiones.
- El revisor aceptó las dos reproducciones iniciales con reservas: B1 usa estado artificial ENVIADO sin fecha, no comprueba igualdad de payloads ni conservación de sello/ProcesadoAt/webhooks; se requiere aceptación adicional desde FIRMADO y SQL. La prueba SQL anterior de respuesta perdida sustituye etapas del servicio, salvo creación/consulta, y acredita replay, no entrega fiscal completa.

- El coordinador repitió la suite ordinaria tras agregar las pruebas de auditoría: 1,319 unitarias + 9 de integración, 0 fallidas/omitidas, código 0. TRX en `tmp/multiagent-qa/coordinator/coordinator-acceptance_net10.0_20260904092854.trx` (integración) y `coordinator-acceptance_net10.0_20260904092915.trx` (unitarias).
- Total separado: **1,328 pruebas ordinarias aprobadas y 4 casos de caracterización que confirman 3 defectos** (B1/B2/B5). Los cuatro casos `AuditKnownDefect` se excluyeron explícitamente de la suite ordinaria.
- SHA-256 del servicio auditado, verificado por el coordinador: `DteDocumentosService.cs` = `C06A562B71FEB36E88795C5955E5FF46B50764680B1A0F83EECF75DC0A5D9BF0`.

## Evidencia web y revisión independiente

- Edge headless sobre 11 vistas renderizadas con el motor Razor real, CSS/JS locales y datos sintéticos; resoluciones 1280×900 y 390×844. No se inició un servidor.
- Resultado: **17 comprobaciones aprobadas, 10 aserciones fallidas y 1 límite documentado**, con 16 capturas. Las diez aserciones agrupan problemas compartidos; no equivalen a diez defectos independientes.
- Reporte: `tmp/multiagent-qa/web/browser-report.json`. Arnés reproducible y límites: `tools/WebAuditPreview/README.md`. HTML, snapshots `*.model.json` y PNG en la misma carpeta del reporte.
- Los diagnósticos de las fixtures se generan con `DteDiagnosticoGuia.Crear`. Los escenarios de envío incierto incluyen `EnviadoAt` y respuesta de timeout coherentes.
- PROCESADO: cinco puntos `rgb(107, 56, 212)`. Login conserva valor y cambia `aria-pressed`; no hay desbordamiento de página a 390px. Esto no aprueba todo el diseño responsive: el revisor observó etiquetas del stepper muy juntas en móvil.
- El revisor inspeccionó independientemente nueve capturas, incluyendo login móvil, PROCESADO móvil, CONTINGENCIA, rol solo consulta, Cliente SV e INVALIDADO sin firma. Confirmó visualmente los problemas de guía, acciones visibles y etapas no completadas.
- Se utilizó un layout de auditoría, no AppShell. Las fuentes externas se bloquearon y alteran iconos/tipografía; ese efecto no se imputa al producto. No se probaron autenticación HTTP, navegación real, autorización middleware, envío de formularios, API, SQL ni Hacienda.
- El recolector registra fallos en JSON y puede terminar con código 0: **el código de salida no es una aprobación funcional**. Los resultados válidos son las aserciones del reporte.

## Siguiente bloque de corrección: GL1D

1. Evitar reintentos automáticos de POST fiscales de resultado incierto (B5), reservar de forma atómica el envío y proteger estados terminales frente a respuestas tardías (B1).
2. Impedir invalidación local de un envío incierto (B2); implementar conciliación autenticada y persistida antes de permitir nuevas acciones fiscales. No desbloquear a ciegas.
3. Proporcionar corrección explícita, autorizada y auditada del receptor en documentos recuperables (B3), sin alterar silenciosamente documentos procesados; resolver el acceso de soporte al diagnóstico con empresa explícita (B4).
4. Integrar la guía estructurada en Web, aplicar permisos a acciones visibles y basar etapas en evidencia real (D1/D2/D5). Corregir asociaciones de etiquetas y búsqueda paginada/remota de retorno (D4/D3).
5. Convertir las caracterizaciones en pruebas de comportamiento seguro. Añadir concurrencia SQL desde FIRMADO, transporte DI real con resultado perdido, conservación de sello/fechas/webhooks y E2E autorizado con roles reales.

La aceptación requiere cerrar los P1 con evidencia independiente. La compilación, una prueba sintética verde o la redacción de documentación no sustituyen esa condición. Este bloque queda priorizado; no se implementó durante la ronda de auditoría.

## Límites operativos

No se inició API/Web/Worker productivo, no se aplicaron migraciones a la base activa, no se enviaron DTE a Hacienda, no se modificó APK ni datos del cliente. Las pruebas y capturas sintéticas no sustituyen una prueba E2E autorizada del despliegue real. No se auditó exhaustivamente cada módulo comercial de la suite en esta ronda.
