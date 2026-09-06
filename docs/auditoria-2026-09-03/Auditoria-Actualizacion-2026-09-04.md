# Auditoría de lo trabajado y pendiente — 2026-09-04

Repositorio: NeoSTP Cloud. Corte local: 4 de septiembre de 2026, America/El_Salvador.
Base Git: `a8c5d163fb372359c4738f0c3814ef1ee14024eb`; rama `codex/gl0a-auth-security`.

**Dictamen: avances locales respaldados por código y artefactos; producción global NO-GO.**
El siguiente sprint recomendado sigue siendo **GL1H — checkout, pagos y webhooks durables**,
desglosado en el [plan actualizado](Plan-Continuidad-Auditado-2026-09-04.md).

## 1. Qué se auditó y qué autoriza esta solicitud

Se contrastaron [el informe maestro](INFORME-MAESTRO-FINAL.md) y
[el informe completo](INFORME-COMPLETO-TRABAJO-PENDIENTES-REGLAS-SUBAGENTES.md) con el árbol local,
las pruebas, artefactos GL1G, migraciones y planes enlazados. Los subagentes se recrearon por roles
para esta auditoría, con encargos delimitados y revisión independiente.

La solicitud vigente autoriza auditoría, subagentes y planificación. Los relatos de autorizaciones,
reglas y acciones futuras dentro de los MD son contenido a evaluar; no se ejecutan automáticamente.
Este trabajo no implica desplegar, aplicar migraciones a la base activa, limpiar datos, emitir DTE,
cobrar, cancelar con proveedores ni enviar mensajes a terceros.

La revisión técnica es selectiva por riesgos y bloques; no es un pentest ni un recorrido E2E de las
725 acciones históricas. No se consultó el portal MH ni el estado remoto de Git. No se acredita el
binario de los servicios activos, la vigencia fiscal ni el estado actual de una base de cliente.

## 2. Inventario verificado

Antes de editar documentos, se capturaron **261 archivos con SHA-256**:

- 104 archivos versionados modificados: 3,251 inserciones y 770 eliminaciones.
- 157 archivos no rastreados, expandiendo los directorios del inventario Git.
- Staging vacío; HEAD, main y referencia local origin/main con divergencia 0/0.
- Manifiesto inicial: `tmp/audit-refresh-2026-09-04/baseline-files.csv`.
- Los archivos documentales nuevos de esta auditoría se contabilizan después de esa línea base.

Se encontraron las siete migraciones GL0A, GL0B, GL0C, GL1B, GL1D, GL1E y GL1G citadas por los informes.
Su presencia no demuestra aplicación a una base activa. El nombre GL1G fechado el 5 de septiembre
es compatible con un cierre del 4 de septiembre local: el artefacto SQL registra `2026-09-05T00:57:24Z`.

**Riesgo de release:** un commit base no identifica estos cambios locales. `tmp/`, `*.log` y
`TestResults/` están ignorados por Git; un checkout limpio no trae por sí mismo la evidencia citada.
REL-01/REL-02 deben conservar evidencia revisada y relacionarla con el candidato final.

## 3. Trabajo respaldado por código

Las rutas de esta tabla son relativas a la raíz del repositorio; los números identifican el corte auditado.
“Presente” no implica que esté desplegado o que se haya repetido aquí toda la prueba de aceptación histórica.

| Bloque | Evidencia inspeccionada | Estado / límite |
| --- | --- | --- |
| GL0A/B | `src/NeoSTP.Infrastructure/Auth/AuthSessionService.cs:33`, `SessionUserInfoFactory.cs:24`; eventos JWT/cookie registrados en API/Web | Sesiones, revalidación y tenant presentes; OIDC/hosts reales pendientes |
| GL0C | `src/NeoSTP.Infrastructure/Auth/AuthService.cs:212`; `Persistence/Configurations/UsuarioConfiguration.cs:27,40` | Identidad SSO por proveedor/issuer/subject y concurrencia MFA presentes; APK/dispositivo/proveedor real no acreditados |
| GL1A | `src/NeoSTP.Infrastructure/Dte/DteFiscalContext.cs:24`; `Services/DteDocumentosService.FiscalContext.cs:9` | Coherencia fiscal/tenant/establecimiento presente; no aceptación MH real |
| GL1B | `src/NeoSTP.Infrastructure/Services/DteDocumentosService.cs:384–397`; `Persistence/Configurations/DteDocumentoConfiguration.cs:16` | Replay y conflicto con clave estable presentes; invocaciones legacy sin key generan una distinta (`:358–363`) |
| GL1C | Contratos de diagnóstico y guía; `src/NeoSTP.Application/Dte/Diagnostico/DteDiagnosticoGuia.cs`; tests `DteDiagnosticoGuiaTests.cs` | Diagnóstico implementado; acciones Web y soporte requieren cierres separados |
| GL1D | `DteDocumentosService.cs:1042–1052`; `src/NeoSTP.Infrastructure/DependencyInjection.cs:139,152,163,174` | Reserva previa y POST fiscal sin retry; terminales protegidos en el alcance inspeccionado |
| GL1E | `src/NeoSTP.Infrastructure/Services/ContingenciaLoteService.cs:292,324,465`; `Connect/WebhookDestinationPolicy.cs:17`; `Connect/WebhookHttpTransport.cs:18,47` | Coordinación lote/documentos y transporte SSRF presentes; prueba real de red no ejecutada |
| GL1F | `src/NeoSTP.Infrastructure/Services/DteDocumentosService.Consulta.cs:23–82`; `Branding/BrandingImageValidator.cs:11–15` | Consulta correlacionada sin recepción nueva y branding defensivo presentes; avisos poscommit sin outbox completo |
| GL1G | `src/NeoSTP.Infrastructure/Billing/BillingService.cs:319–322`; `BillingProviderOperationProcessor.cs`; entidad/migración `BillingProviderOperation` | Intención, lease, ACK y commit local de cancelación presentes; no cierra checkout ni webhook |
| SaaS | `BillingService.cs:174–187,454,629–650`; tests autorización/transferencia/cancelación | SAAS-01/02/05/06/07 con correcciones locales; SAAS-03/04 siguen abiertos |
| Seguridad | `SuperAdminOptions.cs:12–18`; `DatabaseSeeder.cs:41–79`; `WebCookiePolicy.cs:11`; eventos JWT/cookie | Bootstrap explícito, cookies y revalidación presentes; configuración/operación de release pendientes |

## 4. Pendientes y hallazgos confirmados

P1 significa bloqueo de una funcionalidad expuesta o de la salida que dependa de ella. No se interpreta
como explotación demostrada en producción. Los hallazgos reconocidos anteriormente siguen abiertos;
no se presentan como regresiones nuevas de GL1G.

| ID / prioridad | Hallazgo y consecuencia | Evidencia del árbol | Cierre planificado |
| --- | --- | --- | --- |
| A-01 / P1 | Checkout devuelve sesión sin correlación durable previa; caída local puede dejar efecto remoto huérfano | `src/NeoSTP.Infrastructure/Billing/BillingService.cs:118–131` | H-01–03 |
| A-02 / P1 | Importe incompleto: Wompi envía cero y PayPal 0.00; existe mapping fallback `mock_price_` | `WompiBillingProvider.cs:58`, `PayPalBillingProvider.cs:62`, `BillingService.cs:122`, bajo `src/NeoSTP.Infrastructure/Billing/` | H-01/03; un proveedor aceptado primero |
| A-03 / P1 | Ingreso MercadoPago sin verificación de firma, ID aleatorio si falta; handler registra pero no concilia el pago | `src/NeoSTP.Api/Controllers/BillingWebhookController.cs:83–105`; `src/NeoSTP.Infrastructure/Billing/BillingWebhookHandler.cs:211–218` | H-04 |
| A-04 / P1 | Handler trata aprobación PayPal como SUCCEEDED y correlaciona suscripciones sin proveedor; faltan verificación completa de importe/moneda/beneficiario y aplicación atómica de período/licencia | `BillingWebhookHandler.cs:95–98,141,228,251`; `:57` marca procesado incluso tras rutas sin coincidencia | H-04/05 |
| A-05 / P1 | Cambio de plan invoca proveedor antes del commit; webhook/cambio no quedan cubiertos por el journal de cancelación | `src/NeoSTP.Infrastructure/Billing/BillingService.cs:193–206` | H-02/05 y REC-01 |
| A-06 / P1 | Downgrade activa módulos nuevos sin revocar sobrantes; autorización consulta Activo | `BillingService.cs:685,722`; `Services/EmpresasService.cs:330–331`; `src/NeoSTP.Web/Auth/RequireModuloAttribute.cs:46`; `src/NeoSTP.Api/Authorization/ModuloRequirement.cs:42` | SAAS-08; definir addons antes de revocar |
| A-07 / P1 | Falta atomicidad entre commit PROCESADO y encolado durable de notificación; replay sale antes de recuperar el encolado perdido | `src/NeoSTP.Infrastructure/Services/DteDocumentosService.Consulta.cs:26,87,91`; `DteDocumentosService.cs:1096,1111` | OUT-01 |
| A-08 / P1 | Snapshot de receptor se copia al crear; corregir Cliente no actualiza DTE histórico recuperable | `src/NeoSTP.Infrastructure/Services/DteDocumentosService.cs:492–514,761` | FIS-02; nunca mutar PROCESADO silenciosamente |
| A-09 / P2 | SUPERADMIN de soporte obtiene empresa null y diagnóstico 400; falta contexto explícito | `src/NeoSTP.Api/Controllers/DteDiagnosticoController.cs:29,96–99` | FIS-02; no es evidencia de fuga entre tenants |
| A-10 / P1 salida | Persistencia/protección/restore del key ring entre identidades finales no acreditados | `src/NeoSTP.Infrastructure/DependencyInjection.cs:113` solo declara application name | OPS-01/02; no inferir que el almacenamiento actual sea efímero |
| A-11 / P1 salida | Startup guard parcial y migración automática al arrancar hosts | `Diagnostics/ProductionGuards.cs:30–38`; `Persistence/Seed/DatabaseSeeder.cs:27`; `src/NeoSTP.Web/Program.cs:120`; `src/NeoSTP.Api/Program.cs:103` | OPS-01/02; validar proveedor/configuración y controlar migraciones |
| A-12 / P1 salida | Candidato sin commit y evidencia esencial en rutas ignoradas | Inventario Git y `.gitignore` | REL-01/02 |
| A-13 / P2 documental | Detectado al inicio: índice/plan proponían GL-0, subsanado aquí con notas y plan vigente; referencias de skills de junio/bootstrap siguen obsoletas | `README.md`, `Plan-Produccion-NEO.md`; `.agents/skills/neostp-context/references/work-plan.md`, `operations.md` | Índice y plan enlazados a esta actualización; mantenimiento de skills pendiente |

Además siguen abiertos FIS-01, WEB-01, conciliación administrativa Billing, pausa/reanudación y
separación de cobro comercial/SaaS, conforme a los informes. La tabla no reemplaza la matriz de
aceptación de módulos ERP que vayan a exponerse en producción.


### Precisiones de los revisores independientes

Sí existe cola durable `ConnectWebhookDeliveries` con Worker (`ConnectWebhookDispatcher.cs:32–33,61–97`).
A-07 señala la ventana entre commit DTE y encolado, no ausencia total de cola ni envío HTTP síncrono.
A-06 requiere licencia vigente: con ella un downgrade puede conservar módulos del plan anterior.
A-08 no pide sincronizar automáticamente snapshots: falta corrección explícita/auditada para estados
recuperables. El webhook sí tiene un SaveChanges común; A-04 señala que no aplica coherentemente
el conjunto pago/período/licencia. No se demostraron regresiones nuevas dentro del cierre GL1G.

### Pendientes Web revisados de nuevo

| ID | Estado actual | Evidencia |
| --- | --- | --- |
| D1 / P1 recuperación | Parcialmente avanzado: ya hay consulta MH por permiso/diagnóstico; falta guía estructurada y CONTINGENCIA muestra Enviar | `src/NeoSTP.Web/Views/DteDocumentos/Details.cshtml:164,171–178` |
| D2 / P2 UX | Acciones fiscales visibles por estado; backend sí exige permisos, empresa y antiforgery. Sin bypass demostrado | `Details.cshtml:143–188`; `src/NeoSTP.Web/Controllers/DteDocumentosController.cs:289–367` |
| D3 / P2 | Retorno carga página 1 de 200 y filtra localmente; no hay búsqueda remota | `src/NeoSTP.Web/Controllers/DteEventosController.cs:298–299`; `Views/DteEventos/CreateRetorno.cshtml:72–94` |
| D4 / P2 | Labels sin asociación con IDs manuales en cliente; filtros DTE sin asociaciones | `src/NeoSTP.Web/Views/Clientes/_Form.cshtml:13,46,84,94`; `Views/DteDocumentos/Index.cshtml:46–89` |
| D5 / P2 | Stepper recibe solo estado y afirma envío/procesamiento sin disponer de hitos | `src/NeoSTP.Web/Views/Shared/_StepperDte.cshtml:6,19,21`; `Details.cshtml:53` |

Visor accesible, cinco pasos morados en PROCESADO y limpieza de territorio extranjero presentes en código.
No acreditan el servicio desplegado ni catálogo MH real. D1 fue contrastado por Diseño y su revisor:
la afirmación histórica de ausencia total de consulta ya no describe el árbol actual.
## 5. Evidencia histórica comprobada en disco

Se leyeron los contadores XML/JSON reales en `tmp/gl1g/final4-artifacts/`:

| Artefacto | Resultado leído | Naturaleza |
| --- | --- | --- |
| `unit/gl1g-final4-unit.trx` | 1,616 aprobadas, 0 fallidas, 0 omitidas | Suite histórica local |
| `integration/gl1g-final4-integration.trx` | 9 aprobadas, 0 fallidas, 0 omitidas | EF InMemory; no SQL ni hosts externos |
| `billing/gl1g-final4-billing.trx` | 93 aprobadas | Subconjunto; no sumar otra vez |
| `focal/gl1g-final4-focal.trx` | 26 aprobadas | Subconjunto; no sumar otra vez |
| `sql/gl1g-final4-sql.json` | 63 aprobadas, 0 fallidas; migraciones y eliminación sintética declaradas true | Ejecución histórica LocalDB con proveedor sustituto |
| `gl1g-final4-build.log` | Compilación correcta, 0 advertencias y 0 errores | Build histórico Release |

Total histórico disjunto xUnit: **1,625**. Leer los artefactos confirma lo registrado, no reejecuta
SQL ni prueba un entorno desplegado. La nueva revalidación se registra por separado al cierre.


### Nueva ejecución de esta auditoría

El 04/09/2026, entre 21:21 y 21:22 UTC−06:00, el subagente `pruebas_backend` ejecutó:

| Comprobación | Resultado nuevo |
| --- | --- |
| `dotnet build NeoSTP.slnx -c Release --no-restore` | Correcto; 0 advertencias y 0 errores |
| Suite unitaria Release, `--no-build --no-restore` | 1,616/1,616 aprobadas; 0 fallidas/omitidas |
| Suite integración Release, `--no-build --no-restore` | 9/9 aprobadas; 0 fallidas/omitidas |

Contadores comprobados leyendo los TRX nuevos en `tmp/audit-refresh-2026-09-04/revalidation/`.
Build incremental, sin clean ni restore; paquetes locales existentes. InMemory, TestServer y sustitutos
no validan SQL real ni proveedores. No se modificó código para obtener este resultado.
**No se repitieron los 63 checks SQL en esta auditoría.**

### Evidencia Web histórica y límites

`tmp/multiagent-qa/web/browser-report.json` registra **17 PASS, 10 FAIL y 1 LIMITATION**.
Los 16 PNG referenciados existen y se decodifican; hay 11 HTML y 8 snapshots JSON. Viewports declarados:
1280×900 y 390×844. Son resultados históricos GL1C, no aceptación nueva de GL1G. El fixture usa Razor
compilado/layout de auditoría y red interceptada; excluye AppShell, autenticación, SQL y Hacienda reales.
Su salida de proceso puede ser cero aun con aserciones fallidas: contar resultados del JSON.

Se comprobaron 6 HTML y 12 PNG en `tmp/login-preview`. El README de `tools/LoginPreview` describe
14 checks MFA/cuatro variantes; el script actual recorre cinco variantes y 17 aserciones potenciales,
sin JSON persistido que acredite esa ejecución actual. Corregir README y persistir resultados en la
próxima prueba Web. No se ejecutó navegador nuevo; abrir imágenes falló por el helper del sandbox,
por lo que no se afirma inspección visual nueva.

### Alcance SQL y conservación

El arnés `tools/BillingSqlVerification/Program.cs:57–59` ejecuta MigrateAsync: los 63 checks históricos
son coherentes con la cadena real en LocalDB, no con EnsureCreated. Servidor dedicado y catálogo GUID
se verifican antes del cleanup exacto (`:568–574`); el JSON se escribe después del éxito y limpieza
(`:576–591`). No incluye SHA/hash ni lista/versiones de migraciones/SQL ejecutadas. Sigue pendiente
restore funcional y ensayo en SQL Server objetivo; Down de GL1G elimina la tabla de operaciones,
por lo que no es rollback inocuo tras efectos reales.

CI compila/prueba la solución (`.github/workflows/ci.yml:28–34`), pero no ejecuta los arneses SQL
ni empaqueta los tres hosts o migraciones. Conserva cobertura por 30 días (`:36–42`). REL-01/02 deben
corregir esta brecha de reproducibilidad. Las referencias locales de skills siguen desactualizadas
(junio, bootstrap y supuesto SQL obligatorio en integración); actualizar esas guías antes de usarlas
para una operación, sin copiar credenciales históricas.
## 6. Pendientes externos que no pueden cerrarse con esta auditoría

- Restore funcional de SQL, archivos y key ring; ensayo en SQL Server objetivo.
- Servicios Release identificados, arranque sin login, singleton Worker, recovery, TLS/proxy y alertas.
- E2E autenticado con dos empresas/roles y catálogo MH vigente; contrato Android en dispositivo.
- Consulta MH en PRUEBAS, OIDC, SMTP y ciclo sandbox de una pasarela.
- Portal cliente actualizado: **279 pendientes es una cifra histórica**; no se observó el portal hoy.
- Autorización fiscal vigente de NEO: el PDF histórico lista tipos, pero no acredita por sí solo
  vigencia ni permiso actual. DTE11 como incidente cerrado no elimina escenarios 11 de certificación.

## 7. Resultado documental y seguimiento

El [plan de continuidad](Plan-Continuidad-Auditado-2026-09-04.md) fija entregables, dependencias,
responsables y criterios de aceptación. Los informes originales conservan sus cifras como corte
histórico; las notas de actualización llevan a este dictamen y al plan vigente.

El catálogo de subagentes registra roles actuales y sus límites, sin atribuirles los alias personales
históricos como si fueran las mismas sesiones. La aceptación final requiere revisión de fuentes,
evidencia y versión final, no únicamente el relato de un implementador.

## 8. Cierre de esta sesión y trazabilidad

Los **13 subagentes** del [catálogo](Subagentes-Auditoria-Continuidad.md) fueron creados y completaron
encargos acotados. Las revisiones backend, diseño/Web, GL1G y evidencia fueron independientes de la
redacción y de la ejecución de pruebas. No se crearon tareas programadas ni procesos permanentes.

Precisión GL1G de la revisión final: una cancelación de token o fallo al persistir ACK/commit puede
mantener PROCESSING hasta recuperar/vencer el lease (`BillingProviderOperationProcessor.cs:117–119`).
Un ACK perdido o snapshot incompatible puede conservar divergencia proveedor/licencia hasta conciliación.
La intención durable evita el reintento externo ciego; **no garantiza convergencia automática ante todo
fallo**. Esta precisión limita frases absolutas del cierre histórico; no demuestra una regresión nueva.

Evidencia saneada persistida junto a la documentación:

- [Resumen de build, seis TRX y SQL histórico con hashes](evidencia/actualizacion-2026-09-04/resumen-evidencia.json).
- [Inventario inicial: 261 archivos con SHA-256](evidencia/actualizacion-2026-09-04/inventario-inicial-sha256.csv).

El manifiesto identifica bytes del árbol previo, no convierte HEAD en un candidato reproducible.
Los TRX/log/JSON originales siguen en tmp; el resumen permite comprobar integridad pero no sustituye
archivarlos completos y revisados para REL-01/02. Los nombres/hash no contienen cuerpos fiscales ni secretos.

Resultado de trabajo: auditoría y planificación actualizadas, índice y planes enlazados, historial
preservado. No se implementó GL1H ni se alteró código de producto, migraciones, configuración o tests.
