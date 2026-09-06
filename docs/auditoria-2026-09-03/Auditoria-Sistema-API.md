# Auditoría integral de NeoSTP Cloud y API

Fecha de corte: 2026-09-03. Estado: auditoría realizada; remediaciones y habilitación productiva pendientes.

## 1. Dictamen

**NO APTO todavía para habilitar producción multiempresa.** La compilación y las pruebas automatizadas pasan, pero se reprodujo una elevación desde administración de empresa a permisos globales y se confirmó pérdida de la empresa seleccionada al renovar sesión. También hay bloqueos de operación, separación de ambientes y cobros.

Esto no significa que todo el sistema falle: la base funcional es amplia y los últimos arreglos de facturación están integrados. Significa que un resultado verde de pruebas no sustituye las garantías de seguridad, integridad y operación necesarias para venderlo.

Entregables relacionados:

- [Plan de certificación del cliente](Plan-Cliente-Certificacion.md).
- [Plan de producción NEO y mejoras de producto](Plan-Produccion-NEO.md).
- [Evidencia y reproducción](evidencia/README.md).

El tema reportado como **«DTE11» se mantiene cerrado por indicación del usuario**; no se investigó de nuevo ni se retransmitió. Factura de Exportación, tipo 11, se incluye únicamente dentro de la campaña de certificación marcada en la imagen, no como reapertura del incidente.

## 2. Versión y alcance real

| Elemento | Corte auditado |
|---|---|
| Repositorio | NeoStpCloud, rama main |
| HEAD local / origin/main | a8c5d163fb372359c4738f0c3814ef1ee14024eb |
| Cambios recientes relevantes | 172f13e regeneración de JSON en reintento; 7c55d39 territorio del emisor; 0d33b0f saneamiento emisor/NIT; 149b8e3 catálogo país |
| Plataforma | .NET 10, MVC/Razor, API, Application, Domain, Infrastructure, EF Core/SQL Server, Worker |
| Android revisado como consumidor | neocloud_mobile_android, main b12ee82745c6a3a53a2e4887cd37be19cd755f45 |
| Superficie inventariada | 39 controladores / 360 acciones API; 51 controladores / 365 acciones Web |
| Total | 725 acciones de controlador por reflexión; no equivale a 725 rutas HTTP únicas |
| Base consultada | NeoSTP_Cloud; consultas de lectura a empresas, DTE, eventos, certificación, migraciones y respaldo |

Métodos: lectura de arquitectura y código, revisión de contratos y configuración efectiva, inventario de autorización, compilación, pruebas existentes, reproducciones aisladas con EF InMemory, consultas SQL de lectura y GET locales sin sesión.

**Límites:** no se ejecutó un pentest autenticado completo contra todas las rutas, prueba de carga, explotación de SSRF, auditoría criptográfica del certificado, restauración integral, emisión nueva a Hacienda, pago real, entrega real de correo/push ni validación visual de todas las pantallas. Android tuvo revisión de integración, no compilación ni QA en dispositivo. No se verificó la infraestructura pública externa mediante un pentest. Los riesgos estáticos se distinguen de defectos reproducidos. Esta auditoría no es una certificación tributaria ni una garantía de ausencia de vulnerabilidades.

## 3. Evidencia obtenida

| Comprobación | Resultado |
|---|---|
| dotnet test NeoSTP.slnx --no-restore; repetición Release --no-build --no-restore | 1,005 pruebas unitarias + 9 de integración aprobadas en ambas ejecuciones; 0 omitidas |
| dotnet build NeoSTP.slnx -c Release --no-restore | 0 errores; 1 advertencia CS8604 en LotesInventarioTests.cs:137 |
| Dependencias NuGet, incluidas transitivas | Sin vulnerabilidades reportadas por la fuente consultada; no cubre todo el sistema operativo ni garantiza seguridad |
| Arnés tools/SystemAudit | 7 observaciones reproducibles; usa datos sintéticos y EF InMemory, sin conexión real |
| GET API protegidos sin credenciales | 147 rutas verificadas con 401; no se obtuvo información empresarial |
| Límite de API | Primera pasada: 102 respuestas 401 y 45 respuestas 429. Segunda pasada pausada: las 45 dieron 401. Se conservan ambas evidencias |
| API local /health | HTTP 200 |
| Web local /health/live y /health/ready | HTTP 200; disponibilidad/configuración, no prueba de entrega ni de Hacienda |
| Respaldo físico SQL del 2026-09-02 | Aproximadamente 41 MB; RESTORE VERIFYONLY correcto |
| Restauración integral | Pendiente: VERIFYONLY no demuestra recuperación funcional de BD, archivos y claves |

Las 9 pruebas denominadas integración usan EF InMemory. No prueban índices SQL, aislamiento transaccional, bloqueos, concurrencia ni migraciones sobre SQL Server. El arnés de auditoría imprime observaciones: un campo Accepted=true en una reproducción puede representar un defecto, **no un test de seguridad aprobado**.

## 4. Estado de empresas y autorización fiscal

| Empresa | Estado observado |
|---|---|
| NEO, EmpresaId 2 | ACTIVA, configuración MH PRUEBAS, 796 DTE de pruebas; 625 escenarios locales COMPLETADO |
| Evidencia NEO | 605 DTE distintos + 20 eventos distintos sustentan esas 625 filas; no se observó reutilización entre esas filas completadas |
| Eventos NEO | Retorno: 5 procesados / 17 rechazados; contingencia: 6 procesados / 6 rechazados; invalidación: 10 procesados; operaciones especiales: 5 procesados |
| DANIEL, EmpresaId 23 | ACTIVA, PRUEBAS, 11 DTE: 2 borradores, 8 errores, 1 procesado; sin filas de certificación local |
| Cliente, captura del portal MH | 01: 1/90; 03: 0/75; 11: 0/90; 14: 0/25. Total 1/280; 279 pendientes |

El avance del portal es el de la imagen aportada, no una consulta nueva autenticada. Debe actualizarse antes de ejecutar lotes.

El PDF aportado de NEO, fechado 2026-08-30, identifica estado PRODUCCION para diez documentos: **01, 03, 04, 05, 06, 07, 08, 09, 11 y 14**, y tres eventos: **retorno, contingencia e invalidación**. No muestra Donación 15 ni Operaciones Especiales. No se puede convertir “13 capacidades” en “13 tipos de factura” ni extender esa autorización al cliente.

El certificado con sufijo “(1)” tiene contenido; el archivo homónimo sin ese sufijo está vacío. Se debe seleccionar/verificar el material válido por empresa y ambiente. No se copiaron llaves, contraseñas ni certificados a los entregables.

## 5. Hallazgos priorizados

Severidad: **P0** crítico, bloquea exposición multiempresa; **P1** alto, bloquea el flujo afectado o exige deshabilitarlo de forma verificable; **P2** mejora necesaria/riesgo moderado. Evidencia **R** = reproducción aislada; **E** = comprobación del entorno; **C** = revisión estática, pendiente ensayo integral.

### Seguridad e identidad

| ID | Nivel / evidencia | Hallazgo e impacto | Corrección y criterio de cierre |
|---|---|---|---|
| SEC-01 | P0 / R+C | Administración de empresa puede asignar tipo/rol global SUPERADMIN o crear un rol con ese código. El handler concede cualquier permiso al encontrar ese nombre. Se reprodujo concesión de SuperAdmin.Planes.Administrar sin permiso explícito. No se explotó una cuenta real. | Separar administración de plataforma de roles tenant; reservar códigos; validar actor confiable en servicios, no campos enviados por el cliente. Pruebas API/Web de crear/editar usuarios, roles y membresías deben rechazar escalada y conservar administración legítima |
| SEC-02 | P1 / C | SSO enlaza una cuenta local por correo antes de validar la configuración de directorio de su empresa; la validación de TenantId está en el alta por dominio, no en ese enlace. Con SSO habilitado hay riesgo de vincular una identidad de directorio incorrecto. | Validar proveedor, emisor, subject, directorio y política de correo verificado en login y enlace; exigir prueba de posesión de cuenta local para enlace. Mantener SSO deshabilitado hasta ensayos negativos |
| TENANT-01 | P1 / R | Cambiar a empresa 1002 y renovar token devuelve empresa principal 1001. Operaciones posteriores pueden dirigirse al tenant equivocado aunque el usuario tenga acceso a ambos. | Persistir contexto de sesión/empresa del refresh y revalidar membresía/rol. Cambio, refresh, revocación y pertenencia eliminada deben conservar o rechazar explícitamente el contexto |
| AUTH-01 | P1 / R | Vencido BloqueadoHasta, la contraseña correcta sigue rechazándose con AUTH_USER_INACTIVE porque permanece estado BLOQUEADO. Bloqueo temporal se vuelve indefinido. | Recuperar automáticamente solo bloqueos temporales vencidos; no levantar suspensiones administrativas. Test con reloj controlado |
| AUTH-02 | P1 / R+C | Seis códigos MFA incorrectos no aumentan intentos ni bloquean. Las rutas /api/auth/ eluden la cuota general. La indicación de enrolar MFA no restringe por sí misma el token emitido a un superadministrador sin MFA. | Limitador específico para contraseña, MFA y refresh; contador/ventana adecuados; token restringido de enrolamiento. No habilitar permisos globales antes de MFA |
| AUTH-03 | P1 / C | No se identificó validación de revocación/security stamp por petición JWT ni revalidación equivalente de cookie. Bloquear usuario o cambiar credenciales no invalida inmediatamente todas las sesiones ya emitidas. | Definir TTL y revocación; probar access token y cookie previos a bloqueo, cambio de contraseña y retirada de rol; invalidar cachés |
| AUTH-04 | P2 / C | Login busca username/email sin tenant; índices de unicidad son por empresa. Identidades repetidas pueden resolver de forma ambigua. | Identidad global con membresías o contexto de empresa explícito; migración sin fusionar personas distintas; pruebas con emails/usernames duplicados |

Referencias:

- [UsuariosService](../../src/NeoSTP.Infrastructure/Services/UsuariosService.cs), líneas 97, 125, 212 y 248.
- [RolesService](../../src/NeoSTP.Infrastructure/Services/RolesService.cs), línea 48; [PermisoAuthorizationHandler](../../src/NeoSTP.Api/Authorization/PermisoAuthorizationHandler.cs), línea 10.
- [AuthService](../../src/NeoSTP.Infrastructure/Auth/AuthService.cs), líneas 61–142, 184, 242–365 y RefreshAsync.
- [UsuarioConfiguration](../../src/NeoSTP.Infrastructure/Persistence/Configurations/UsuarioConfiguration.cs), líneas 32–33.
- [SsoAuthenticationExtensions](../../src/NeoSTP.Web/Auth/SsoAuthenticationExtensions.cs), línea 63; [ApiQuotaMiddleware](../../src/NeoSTP.Api/Middlewares/ApiQuotaMiddleware.cs), línea 21.

### DTE, certificación y contratos de API

| ID | Nivel / evidencia | Hallazgo e impacto | Corrección y criterio de cierre |
|---|---|---|---|
| DTE-01 | P1 / C | Correlativo identificado por empresa/tipo, sin ambiente; número de control único empresa/número. Envío toma ambiente de configuración actual, mientras documento tiene su propio ambiente. Cambiar configuración o resetear correlativos con datos retenidos puede mezclar ambientes o colisionar. | Contexto fiscal inmutable del documento, guard de coincidencia con credenciales/destino y diseño de numeración por alcance fiscal verificado. No resetear hasta decidir separación y ensayar migración con datos existentes |
| DTE-02 | P1 / C | NeoProfit y reportes fiscales filtran PROCESADO pero no ambiente; documentos de pruebas conservados pueden contaminar cifras productivas. | Ambiente explícito en consultas/contratos/exportaciones; datos de PRUEBAS nunca suman en reportes productivos. Revisar también cobranza y saldos derivados |
| DTE-03 | P1 / C | Connect crea borrador en cada emisión; promoción POS verifica estado, genera y luego vincula. No se identificó idempotencia duradera para reintentos/concurrencia de esos flujos. | Clave de idempotencia por empresa/operación, persistencia atómica, misma respuesta para repetición; consultar estado ante timeout antes de generar otra identidad fiscal |
| CERT-01 | P2 / C+E | MarcarCompletado valida empresa/tipo/sello, pero no impide vincular un mismo DTE/evento aceptado a escenarios diferentes ni valida el contenido específico de cada escenario. Puede dar falsa confianza en otras ejecuciones. En NEO las 625 filas sí usan evidencia distinta. | Validar correspondencia de escenario y evidencia única, ambiente de pruebas y conciliación con portal; no considerar una matriz local equivalente a autorización MH |
| API-01 | P2 / C | ApiControllerBase y AuthController usan ErrorCode para elegir HTTP pero lo omiten del cuerpo. No hay normalización global equivalente para todas las excepciones/validaciones. Catálogo genérico de Hacienda puede atribuir una causa incorrecta al mismo código. | Contrato estable con code, message, fieldErrors, suggestedAction, retryable y traceId; conservar respuesta MH original de forma protegida. Web/App deben mostrar qué corregir, no un “ERROR” genérico |

Referencias:

- [DteCorrelativoConfiguration](../../src/NeoSTP.Infrastructure/Persistence/Configurations/DteCorrelativoConfiguration.cs):12 y [DteDocumentoConfiguration](../../src/NeoSTP.Infrastructure/Persistence/Configurations/DteDocumentoConfiguration.cs):104.
- [DteDocumentosService](../../src/NeoSTP.Infrastructure/Services/DteDocumentosService.cs):376, 401, 907; creación ya utiliza transacción y bloqueo SQL por empresa. **No se atribuye aquí una carrera al generador DTE que ya tiene ese bloqueo.**
- [ProfitService](../../src/NeoSTP.Infrastructure/Services/ProfitService.cs):316 y [ReporteFiscalService](../../src/NeoSTP.Infrastructure/Services/ReporteFiscalService.cs):98.
- [ConnectDteService](../../src/NeoSTP.Infrastructure/Services/ConnectDteService.cs), EmitirAsync; [PosService](../../src/NeoSTP.Infrastructure/Services/PosService.cs):266.
- [CertificacionDteService](../../src/NeoSTP.Infrastructure/Services/CertificacionDteService.cs):183 y 263; [CertHarness](../../tools/CertHarness/Program.cs):35 fija EmpresaId=2.
- [ApiControllerBase](../../src/NeoSTP.Api/Controllers/ApiControllerBase.cs):17 y 28; [AuthController](../../src/NeoSTP.Api/Controllers/AuthController.cs):171.

### Pagos, integraciones y consistencia financiera

| ID | Nivel / evidencia | Hallazgo e impacto | Corrección y criterio de cierre |
|---|---|---|---|
| BILL-01 | P1 / C | Wompi crea enlace con monto 0; PayPal usa 0.00; falta mapping cae en mock_price y el resolver puede caer a proveedor por defecto/Mock. No están listos solo por configurar credenciales. | Importes/precios reales server-side, mapping obligatorio, persistir intención/orden/empresa/moneda y rechazar proveedor desconocido. Sandbox completo con pago, rechazo, expiración y devolución antes de abrir cobros |
| BILL-02 | P1 / C | Stripe acepta payload sin verificar firma si falta secreto, sin guard de ambiente en esa rama. MercadoPago procesa body sin verificar autenticidad. PayPal trata ORDER.APPROVED como pago exitoso; no equivale a captura completada. Solo existen receptores públicos Stripe/MP aunque hay lógica para otros proveedores. | Firma y timestamp obligatorios, validación del recurso en proveedor, monto/moneda/comercio, unicidad de evento y estado final correcto. Proveedores no terminados deben quedar inaccesibles, no solo ocultos |
| CONNECT-01 | P1 / R+C | Se acepta http://127.0.0.1:5058/health como destino de webhook. Despacho usa HttpClient sin política de salida explícita. Riesgo SSRF a servicios internos si el actor puede configurar destinos. No se hizo petición interna como explotación. | HTTPS, validación de host/IP/DNS, bloqueo loopback/redes privadas/metadata, control de redirecciones y egress; ensayos con DNS rebinding/redirect en entorno aislado |
| CONNECT-02 | P2 / C | Secreto HMAC de webhook almacenado directamente; DTO expone solo prefijo, no un mecanismo identificado de entrega inicial para que el receptor verifique firmas. | Cifrar en reposo, mostrar una vez de forma autorizada, rotar/revocar con auditoría; comprobar verificación HMAC interoperable |
| ERP-01 | P1 / C | POS guarda la venta y luego llama salida de inventario sin evaluar Result. Stock insuficiente puede dejar venta completada sin movimiento; también calcula número usando Count+1. | Política explícita de stock, operación atómica o compensación trazable; nunca omitir silenciosamente movimiento. Correlativo de ticket seguro con concurrencia y cancelaciones |
| ERP-02 | P1 / C | Tesorería lee saldo, calcula y guarda sin control de concurrencia observado; cobranza verifica saldo antes de aplicar; inventario modifica existencias/lotes. Hay riesgo de saldo perdido, sobrepago o consumo simultáneo. | Pruebas reales SQL y transacciones/rowversion/locks por agregado, idempotencia de movimientos y reconciliación. Es riesgo estático, no pérdida demostrada en datos reales |

Referencias: [WompiBillingProvider](../../src/NeoSTP.Infrastructure/Billing/WompiBillingProvider.cs):58; [PayPalBillingProvider](../../src/NeoSTP.Infrastructure/Billing/PayPalBillingProvider.cs):62; [BillingService](../../src/NeoSTP.Infrastructure/Billing/BillingService.cs):77; [PaymentProviderResolver](../../src/NeoSTP.Infrastructure/Billing/PaymentProviderResolver.cs); [BillingWebhookController](../../src/NeoSTP.Api/Controllers/BillingWebhookController.cs):44 y MercadoPago; [BillingWebhookHandler](../../src/NeoSTP.Infrastructure/Billing/BillingWebhookHandler.cs):98; [ConnectWebhookService](../../src/NeoSTP.Infrastructure/Services/ConnectWebhookService.cs):52, 66, 222 y 254; [ConnectWebhookDispatcher](../../src/NeoSTP.Infrastructure/Services/ConnectWebhookDispatcher.cs):171; [PosService](../../src/NeoSTP.Infrastructure/Services/PosService.cs):150–159 y 328; [TesoreriaService](../../src/NeoSTP.Infrastructure/Services/TesoreriaService.cs):147–170; [CobranzaService](../../src/NeoSTP.Infrastructure/Services/CobranzaService.cs); [InventarioService](../../src/NeoSTP.Infrastructure/Services/InventarioService.cs).

La documentación primaria de PayPal distingue aprobación de orden y captura: [eventos](https://developer.paypal.com/api/rest/webhooks/event-names/), [autorización y captura](https://developer.paypal.com/v5/checkout/auth-capture/). La autenticidad de notificaciones debe seguir el contrato del proveedor: [webhooks MercadoPago](https://www.mercadopago.com.br/developers/pt/docs/your-integrations/notifications/webhooks).

### Operación, salida comercial y cobertura

| ID | Nivel / evidencia | Hallazgo e impacto | Corrección y criterio de cierre |
|---|---|---|---|
| OPS-01 | P1 / E+C | API/Web responden, pero autoarranque está registrado AtLogOn/Interactive y Development. No se observó Worker ejecutándose. Encender PC sin iniciar sesión no garantiza servicios ni retransmisión/recordatorios. Worker además puede seleccionar Mock por configuración ausente. | Publicar tres hosts Release, identidad dedicada, arranque automático independiente de login, reinicio ante fallo, mismos proveedores reales compatibles. Ensayar reinicio de máquina sin sesión interactiva |
| OPS-02 | P1 / E+C | BackupService produce manifiesto/conteos, no respaldo recuperable de SQL. Hay .bak físico verificado, pero falta restauración funcional incluyendo archivos y claves. | SQL backup programado, copia fuera del host, retención/cifrado, ensayo aislado y RPO/RTO medidos; no autorizar limpieza solo porque Ops_BackupJobs diga COMPLETADO |
| OPS-03 | P1 / C+E | Guards solo detectan texto Mock de cinco proveedores, no Hacienda/firmador ni valores desconocidos/faltantes; API no configura ForwardedHeaders; DataProtection no fija repositorio persistente compartido. Cambiar identidad al instalar servicios puede impedir descifrar secretos. | Configuración validada al inicio, módulos opcionales realmente deshabilitados, proxy confiable, claves persistidas/protegidas para Web/API/Worker y recuperables tras reinicio/cambio de host |
| OPS-04 | P2 / C | Web/API invocan seeding/migraciones al arrancar. Requiere permisos amplios y despliegues coordinados; falta ensayo SQL de actualización/restauración en este corte. | Migración de release controlada con script revisado e identidad separada; hosts no compiten por modificar esquema |
| PROD-01 | P1 / C | Plan tiene módulos y límites, pero no lista de tipos DTE permitidos. No está implementada la separación entre catálogo técnico, contrato comercial y autorización fiscal por empresa. | Entitlements API-first y enforcement central en todos los canales; no ampliar autorización por comprar un plan |
| PROD-02 | P2 / C | Suscripción tiene SUSPENDED por suspensión, no pausa voluntaria/reanudación que conserve tiempo pagado. QR actual genera URL/instrucciones, no un ciclo de pago verificado. | Implementar pausa propia y cobros de comercio según especificación del plan, separados del cobro de la suscripción SaaS |
| QUAL-01 | P1 / E | Suite verde no cubre SQL real, autenticación/roles de todos los endpoints, recuperación, proveedores externos ni Android ejecutado. | Matriz de pruebas de release con evidencias por rol/empresa/canal y flujos habilitados, sin llamar “certificado” a lo no ejecutado |

Referencias: [install-local-autostart](../../scripts/install-local-autostart.ps1):4, 73, 87; [Worker/Program](../../src/NeoSTP.Worker/Program.cs):30–38; [BackupService](../../src/NeoSTP.Infrastructure/Services/BackupService.cs):15 y 98; [ProductionGuards](../../src/NeoSTP.Infrastructure/Diagnostics/ProductionGuards.cs):14–41; [DependencyInjection](../../src/NeoSTP.Infrastructure/DependencyInjection.cs):112, 195–219; [API/Program](../../src/NeoSTP.Api/Program.cs):99; [Web/Program](../../src/NeoSTP.Web/Program.cs):120; [Plan](../../src/NeoSTP.Domain/Core/Licenciamiento/Plan.cs); [BillingSubscription](../../src/NeoSTP.Domain/Core/Billing/BillingSubscription.cs); [CobroQrService](../../src/NeoSTP.Infrastructure/Services/CobroQrService.cs).

## 6. Cobertura por módulo y aceptación pendiente

“Revisado” significa código/superficie y suite disponibles, no recorrido manual completo ni cobertura del 100 % de sus ramas.

| Área | Revisión / controles observados | Prueba que falta para habilitarla |
|---|---|---|
| Identidad, usuarios, roles, SSO, empresas | Autorización, refresh, lockout y MFA; fallos anteriores | Dos tenants, roles mínimos, escalada negativa, revocación y SSO adverso |
| Planes, módulos, sucursales, puntos de venta | RBAC y licencias generales; falta granularidad DTE | Cambio de plan sin fuga de permisos ni alteración fiscal; límites y downgrade |
| Clientes, productos y catálogos | Contratos, filtros de empresa y fixes territoriales presentes | Web/App: elegir cliente autocompleta, país extranjero oculta y limpia territorio, El Salvador departamento→municipio, datos persistidos coherentes |
| DTE y diagnóstico | Pipeline y últimas correcciones; errores/ambiente/idempotencia pendientes | 01/03/11/14 en pruebas y tipos NEO autorizados; regeneración segura; duplicado/timeout; PDF/JSON/JWS coherentes |
| Eventos y contingencia | Evidencia local NEO incluye retornos procesados | Evento→lote→retransmisión→sello, caída de Worker, reanudación, rechazo y consulta; elegibilidad por tipo |
| Certificación | Matrices y comprobación de sellos; brecha de vínculo/escenario | Portal conciliado, evidencia distinta y funcionalmente correcta por escenario |
| API administrativa | Inventario 360 acciones; 147 GET protegidos devolvieron 401 | Matriz autenticada 401/403/404/409/422/429, tenant ajeno, inputs inválidos, paginación y contratos |
| Web | Inventario 365 acciones; POST explícitos inventariados incluyen antiforgery | Cookies/CSRF con sesión, navegación por rol, estados visuales, accesibilidad y búsquedas |
| Android | Revisión de consumidor/API y pantalla QR existente | Build release, dispositivo, refresh multiempresa, errores, offline/reintentos y vínculos de pago |
| Billing y QR | Proveedores y webhooks auditados; no listos para cobro real | Sandbox y conciliación de importe/moneda/empresa; duplicados; devoluciones |
| Connect / integraciones | Autenticación propia en v1, API keys y despacho | Idempotencia, SSRF, HMAC, rotación, aislamiento, reintentos |
| POS / caja | Venta, stock, número ticket y promoción DTE | Dos cajas concurrentes, stock insuficiente, anular/compensar, promoción repetida |
| Inventario / lotes | Stock y FEFO revisados; tests existentes | Dos transacciones por último lote, costo, reverso y saldo SQL |
| Compras / CxP | Superficie y flujos de registro/inventario/finanzas | Falla intermedia no deja compra, deuda o stock parcial; proveedor ajeno y reversos |
| Cobranza / tesorería | Saldos, registros y concurrencia revisados | Pago simultáneo, sobrepago, reverso, conciliación bancaria y contable |
| Contabilidad / reportes / Profit | Cálculos y filtro de estado; falta ambiente | Cuadre con DTE reales, exclusión pruebas, apertura/cierre y doble partida |
| RRHH | API, permisos y suite disponibles | Nómina de referencia, descuentos/redondeos, acceso a datos personales; revisión fiscal/laboral específica pendiente |
| CRM / agenda | Superficie, dependencia de alertas/Worker | Dueño/asignación tenant, agenda, recordatorio único y permisos |
| Portal de clientes | Tokens aleatorios/hasheados, expiración y vínculo a empresa/documento | Revocado/expirado, descargas cruzadas, no registrar token sensible en logs |
| Scan / archivos / branding | Límites de tamaño/tipo y guard de ruta en almacenamiento | Archivo malformado, cuota, extracción real, malware y control de acceso |
| Correo / alertas / notificaciones | Proveedores y Workers | Entrega real controlada, retry, no duplicar ni enviar pruebas a terceros |
| Hardening / legal / auditoría | Cabeceras, config, backup, exportación y registros | Retención y acceso, redacción de secretos/tokens, recuperación de datos y revisión legal especializada |

## 7. Controles positivos que deben conservarse

- JWT valida emisor, audiencia, expiración y firma; API CORS fuera de Development no permite cualquier origen por defecto.
- Hay aislamiento por EmpresaId y permisos en gran parte de los servicios, además de autenticación propia de Connect v1. Sus métodos AllowAnonymous no significan automáticamente acceso sin clave.
- Los GET protegidos son rechazados sin sesión. Los únicos receptores API sin autorización declarativa fuera del grupo AllowAnonymous son webhooks de billing, que requieren autenticidad por firma, no JWT.
- POST explícitos de Web inventariados incluyen antiforgery; ello no sustituye pruebas autenticadas.
- Creación de DTE tiene transacción y bloqueo SQL por empresa; conservarlos al introducir ambiente/idempotencia.
- Secretos DTE/SMTP usan protección de aplicación; asegurar portabilidad de las claves, no reemplazar por texto plano.
- Tokens de portal tienen entropía, almacenamiento hasheado, vencimiento y vinculación a documento.
- El respaldo físico SQL existente pasó verificación; conservarlo además de hacer uno fresco.

## 8. Orden de corrección y puerta de salida

1. **GL-0A Seguridad:** SEC-01, TENANT-01, AUTH-01/02/03. Deshabilitar SSO y conectores/cobros inseguros hasta corregirlos.
2. **GL-0B Integridad y operación:** DTE-01/02/03, OPS-01/02/03 y SQL real para los flujos financieros que se habilitarán.
3. **GL-1 Producto fiscal:** tipos por plan/empresa, errores claros, permisos y pruebas compartidas API/Web/App.
4. **CERT-CLIENTE:** completar únicamente cuatro matrices acordadas, con portal conciliado.
5. **GL-2 NEO:** ensayo de producción, respaldo/recuperación, configuración por empresa y decisión formal go/no-go.
6. **GL-3/4:** pausa de suscripción y cobros QR/pasarela, con proveedores verificados; pueden ir después del arranque fiscal si están deshabilitados de forma efectiva.

No hay fecha responsable de salida hasta cerrar GL-0 y medir los ensayos. Ocultar una opción en Web no deshabilita la API ni el Worker.

## 9. Acciones no realizadas en esta auditoría

No se borraron datos, reseedearon IDs, reiniciaron servicios, aplicaron migraciones productivas, cambiaron credenciales, activó ambiente MH producción, emitieron DTE/eventos, procesaron pagos ni modificó código funcional de producción. Se añadieron documentos, evidencias y un arnés aislado. La implementación se ejecuta en el sprint correspondiente con verificación y autorización operativa de corte.
