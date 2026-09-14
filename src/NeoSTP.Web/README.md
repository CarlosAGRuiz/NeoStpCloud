# NeoSTP.Web

## Próximo trabajo — App/API Comercial v1 (14 de septiembre de 2026)

La Web mostrará por separado el estado de entrega del correo al receptor y de la copia al emisor. Después de `PROCESADO`, el receptor conservará exactamente la plantilla y adjuntos actuales; el emisor recibirá otro correo independiente. No se usará CC ni BCC. Los fallos no revertirán el estado fiscal y podrán reintentarse por destino con permiso, antiforgery, idempotencia y auditoría.

La selección de tipo DTE también se alimentará del catálogo efectivo calculado por servidor para la empresa activa. Una empresa verá únicamente los tipos incluidos en su plan, sin afectar el acceso a sus documentos históricos. MFA continuará siendo opcional y la app móvil utilizará el mismo ciclo seguro de enrolamiento, confirmación, desafío y desactivación.

Alcance completo y criterios de aceptación: [Plan App/API Comercial v1](../../docs/PLAN-APP-API-COMERCIAL-V1.md).

## Estado actualizado — 10 de septiembre de 2026

- La insignia del encabezado consulta el ambiente fiscal de la empresa activa; no se elige producción en cada factura ni se deriva del entorno de alojamiento ASP.NET. Mantener empresas de prueba separadas de las productivas.
- Clientes y Nuevo DTE muestran **Departamento → Municipio → Distrito**. Las listas respetan sus relaciones, limpian selecciones incompatibles y ocultan territorio local para clientes extranjeros.
- Al elegir un cliente registrado, la factura muestra sus datos guardados, incluidos correo, dirección y distrito. Para corregir territorio se ofrece enlace a su ficha; el receptor manual permite seleccionar los tres campos. Escribir el distrito dentro de la dirección no sustituye su selección.
- Después de confirmar y persistir PROCESADO con sello, se intenta enviar PDF y JSON al receptor, con copia al correo de la empresa emisora del DTE. También aplica al confirmar por conciliación; no reenvía automáticamente facturas anteriores.
- Fallo de correo no revierte aceptación fiscal. Revisar auditoría `CORREO_AUTOMATICO` y entrega antes de usar Reenviar. SMTP aceptado no equivale a recibido; el envío aún no tiene cola durable ni reintentos automáticos.
- Regresión del cambio: 2,401 unitarias, 9 de integración y 9 comprobaciones de navegador sobre vistas Razor aisladas. Estas últimas no certifican sesión real, AppShell completo ni entrega SMTP. El estado de instalaciones concretas y sus evidencias se conservan de forma privada.

[Notas públicas y pendientes](../../docs/releases/2026-09-10.md). Los apartados GL siguientes son históricos; funciones de pago y workers siguen sujetos a sus bloqueos operativos.

> El estado operativo se registra en [continuidad y certificación](../../docs/MAIN-STANDARD.md). El release del cliente valida el esquema y mantiene desactivadas las migraciones y semillas al arrancar.

## Suscripción con cobro a fin de mes

`/billing` muestra el estado activo, la fecha local del próximo cobro, el período de servicio y las mensualidades pendientes o pagadas. Los acuerdos calendario cobran al último día de cada mes en El Salvador. `/billing/portal` dirige estos acuerdos a la misma pantalla; cambios y pagos se tramitan con administración para conservar el calendario contratado.

La continuidad del acceso y el pago de una mensualidad son datos distintos. Activar el acuerdo no crea un pago. El generador mensual se habilita en API con `Billing:Calendar:Enabled`; Web lo mantiene desactivado. Los módulos adicionales son autorizaciones exclusivas de la empresa y conservan la restricción fiscal a los tipos DTE contratados.

App MVC/Razor de NeoSTP Cloud. Es la interfaz operativa para empresas, DTE, clientes, POS,
compras, cobros, inventario, RRHH, CRM, portal y soporte. La Web no delega la firma DTE a la API:
usa sus propios servicios, configuracion local y DataProtection.

## Resumen

- Proyecto: `src/NeoSTP.Web/NeoSTP.Web.csproj`.
- Puerto local recomendado: `http://127.0.0.1:5031`.
- Health: `/health/live` y `/health/ready`.
- Auth: cookie de sesion; la empresa activa sale de `IEmpresaContext`.
- UI: Razor + Bootstrap 5 + `wwwroot/css/neostp.css` con componentes `ns-*`.
- Permisos: controllers con modulo/permiso; SuperAdmin puede usar modo soporte sobre empresa.

## GL-1G: cancelacion durable de suscripciones (sin desplegar)

- `POST /billing/cancel` conserva antiforgery y autorizacion por empresa, pero ya no depende de que
  una llamada remota y el guardado local ocurran como si fueran una sola transaccion. Primero registra
  la intencion durable con snapshot de plan y luego la procesa con una clave idempotente y un *lease*
  exclusivo.
- Una confirmacion remota se registra antes de cancelar localmente la suscripcion y su licencia. Esos
  cambios se aplican juntos. Ante timeout, lease vencido sin ACK o correlacion modificada, la Web recibe
  un error de conciliacion; el acceso local no se corta y la solicitud no se reenvia automaticamente.
- Cancelacion programada e inmediata mantienen la politica GL1F: nunca crean, extienden ni reviven una
  licencia. Adelantar una programada a inmediata genera una segunda intencion distinta y controlada.
- Empresa, plan, licencia, proveedor y recurso externo se validan antes de llamar al proveedor. Mientras
  haya una cancelacion pendiente o en conciliacion, checkout, portal, cambio de plan y mutaciones de
  transferencia se rechazan con `BILLING_CANCELLATION_PENDING`.
- La cuarentena usa una transicion condicional: una lectura obsoleta no puede sobrescribir un ACK ni
  el estado terminal `COMPLETED`.
- Checkout y portal toman el mismo bloqueo por empresa durante la comprobacion y creacion de sesion;
  dos modalidades de cancelacion tampoco pueden permanecer abiertas a la vez. El uso posterior del
  link y los webhooks sigue fuera de alcance hasta GL1H.
- El Worker que recupera operaciones pendientes permanece deshabilitado por defecto. No existe aun una
  pantalla operativa para resolver `REQUIRES_RECONCILIATION`; no habilitarlo hasta aplicar la migracion
  y aprobar el runbook de conciliacion.

Ver [cierre y evidencia GL1G](../../docs/MAIN-STANDARD.md).

**Gate de produccion GL1G:** no hubo despliegue, migracion de la base activa ni llamadas a proveedores
reales. La coordinacion de cancelaciones no completa checkout, links/QR de pago, montos/monedas ni
webhooks de cobro; las pasarelas continúan **NO-GO**.

## GL-1F: conciliacion, suscripciones y branding (sin desplegar)

### Conciliacion manual de un DTE

- Desde `/DteDocumentos/Details/{id}`, `POST /DteDocumentos/ConciliarHacienda` exige empresa activa,
  antiforgery y permiso `DTE.Emitir`. Consulta el intento ya transmitido del mismo DTE; no regenera,
  no firma y **nunca reenvia** el documento.
- La confirmacion solo se acepta cuando Hacienda devuelve una respuesta inequivoca para el ambiente,
  codigo de generacion y sello esperados. Si el resultado no es concluyente, la pantalla conserva el
  estado anterior y muestra el error; el detalle local por si solo no consulta Hacienda.
- No debe crearse otro DTE para resolver un timeout. El operador concilia el existente y solo procede
  segun su estado confirmado.

### Cancelacion de suscripciones

- `POST /billing/cancel` exige antiforgery, empresa activa y administrador valido de esa empresa (o
  administrador central real). Un rol tenant llamado `SUPERADMIN` no obtiene alcance global.
- Cancelacion inmediata: revoca la licencia ahora y no extiende su vencimiento. Cancelacion al final
  del periodo: conserva acceso solo hasta el menor fin futuro ya registrado entre periodo/trial y
  licencia; nunca crea, extiende ni revive una licencia. La misma solicitud es idempotente y una
  cancelacion programada puede adelantarse a inmediata.
- La operacion falla de forma cerrada ante transferencia pendiente, multiples suscripciones vigentes,
  multiples licencias activas o plan de licencia distinto. No elimina datos ni asignaciones de
  modulos; el acceso efectivo depende de que la licencia siga vigente.

### Branding de documentos

- `/branding` opera dentro de la empresa activa y las mutaciones requieren `DTE.Configurar` y
  antiforgery. Acepta logo/firma PNG, JPEG o WEBP estaticos de hasta 1 MiB, 4096 px por lado y
  4 megapixeles; MIME y formato real deben coincidir. APNG, WEBP animado y archivos corruptos o
  truncados se rechazan. La firma textual admite hasta 300 caracteres.
- El logo se aplica a PDF DTE, cobro QR, ticket POS y correo DTE; la firma grafica al PDF DTE. Un blob
  legacy invalido o sobredimensionado se omite sin repararlo ni borrarlo automaticamente.
- Branding es exclusivamente visual: no cambia el JSON/JWS fiscal, el estado del DTE ni sus totales.

**Gate de produccion:** no se desplegaron estos cambios ni se operaron la base del cliente o
proveedores reales. Stripe rechaza cerrado cuando el secreto de webhook falta/es placeholder y cuando
la firma o el JSON son invalidos, pero pasarelas y links de pago completos, correlacion confiable de
webhooks, conciliacion automatica y outbox durable de billing siguen **NO-GO**. No habilitar ninguna
pasarela —Stripe, MercadoPago, Wompi o PayPal— en produccion con este alcance.

## GL-0A: acceso y MFA (2026-09-03, pendiente de despliegue)

- Login incluye código de segundo factor: acepta TOTP o recuperación y lo envía al servicio compartido con API.
- Visor de contraseña con icono local, teclado y etiquetas accesibles; oculta nuevamente al enviar o abandonar la pestaña. No guarda ni registra el contenido.
- Visor verificado con Razor real y 13 comprobaciones de navegador aislado: [LoginPreview](../../tools/LoginPreview/README.md).
- Intentos MFA incorrectos cuentan para el bloqueo; un bloqueo administrativo no se libera por tiempo.
- Roles/claims de empresa no conceden administración global por el nombre SUPERADMIN.
- La lógica compartida de sesiones requiere GL0A_RefreshSessionContext. Migración preparada, **sin aplicar ni publicar**. El arranque normal puede aplicar migraciones/seed: coordinar y aprobar el corte antes de iniciar esta release.
- El incremento GL-0B siguiente integra revocación JWT/cookie y MFA restringido. Sigue pendiente validar el despliegue y el dispositivo Android.

Ver [avance y evidencia](../../docs/MAIN-STANDARD.md).

## GL-0B: acceso protegido (2026-09-04, sin desplegar)

- Login y cambio de contraseña comparten 10 POST/minuto por IP y proceso; las acciones MFA comparten
  otras 10. HTTP 429 incluye Retry-After y un mensaje en español. GET login, salud y logout no consumen
  esas cuotas. Configuración compartida Security:AuthRateLimit; solo se confía el proxy de loopback.
- La cookie incluye sesión persistida. Cada solicitud revalida usuario, credenciales, roles, permisos,
  empresa y membresía. Logout revoca la sesión del servidor; una copia de la cookie deja de autenticar.
- Las cookies anteriores a este cambio requieren nuevo login. Cambiar contraseña cierra sesión;
  su reset/cambio administrativo, bloqueo/desbloqueo, edición de usuario y MFA rotan SecurityStamp.
- Administrador global sin MFA: /Account/MfaEnrollment, POST /Account/MfaBegin, POST /Account/MfaConfirm.
  Secreto visible solo al iniciar configuración, clave manual para el autenticador, códigos de recuperación
  en una respuesta no-store y nuevo login tras confirmar. No se guardan secretos en URL/TempData/inputs.
- SSO con MFA: /Account/MfaVerification (GET/POST), acepta TOTP o recuperación. Solo al verificar emite
  cookie completa. El desafío se consume una vez; los fallos cuentan para el bloqueo del usuario.
- Los desafíos vencen en 10 minutos, no son persistentes aunque se marque Recordarme y no llevan
  permisos operativos. El middleware limita sus rutas; no usa excepciones amplias por prefijo o extensión.
  Todos los POST MFA tienen antiforgery y las vistas no muestran el menú operativo.
- Las credenciales completas tienen vencimiento absoluto de sesión según Jwt:RefreshTokenExpiryDays;
  la renovación deslizante de la cookie no permite superar ese límite del servidor.

Verificación visual: Razor real, 13 comprobaciones del visor y 14 de MFA en navegador aislado,
escritorio/móvil. Pruebas HTTP con controladores reales y JWT/cookies reales de datos sintéticos.
No se ejecutaron los Program de API/Web, seeds, migraciones ni SQL del cliente.

Antes del corte: revisar/aplicar por procedimiento aprobado GL0A_RefreshSessionContext y
GL0B_AuthSessionFoundation, desplegar coordinadamente API/Web y verificar Android/OIDC/SQL en entorno
aislado. La vinculación SSO por correo permanece abierta; no habilitarla en producción con esta evidencia.

## Ejecucion local

```powershell
dotnet build NeoSTP.slnx
dotnet run --project src/NeoSTP.Web
```

La Web carga `src/NeoSTP.Web/appsettings.Local.json` si existe. Ese archivo esta ignorado por git y
debe contener solo configuracion local, nunca secretos commiteados.

## Inicio automatico local

En instalaciones con servicios Windows, el arranque no debe depender del inicio de sesión.
No ejecutar los instaladores de tareas de desarrollo sobre servicios existentes. Las rutas,
identidades, claves y procedimientos de recuperación específicos se mantienen en documentación
operativa privada, fuera de estas notas públicas.

Para que API y Web arranquen al iniciar sesion en la PC de pruebas:

```powershell
./scripts/install-local-autostart.ps1
```

El script publica en Release a `out/local-autostart`, copia los `appsettings.Local.json` de API/Web y
crea dos tareas programadas:

| Tarea | URL | Validacion |
|---|---|---|
| `NeoSTP API` | `http://127.0.0.1:5058` | `/health` |
| `NeoSTP Web` | `http://127.0.0.1:5031` | `/health/live` |

Es una tarea al iniciar sesion del usuario actual, no un Windows Service. Esto conserva el mismo
perfil de Windows para DataProtection, certificados y secretos locales usados al firmar.

Si el script falla con puertos ocupados, cerrar procesos viejos `NeoSTP.Web.exe` y `NeoSTP.Api.exe`,
o procesos `dotnet run` de Debug, y ejecutarlo nuevamente.

## DTE en Web

Rutas principales:

| Ruta | Uso |
|---|---|
| `/DteDocumentos` | Listado de DTE con filtros. |
| `/DteDocumentos/Create` | Nuevo DTE y seleccion de cliente/productos. |
| `/DteDocumentos/Details/{id}` | Detalle, stepper de estado, conciliacion manual, PDF/JSON/JWS/reenviar. |
| `/DteEventos/CreateRetorno` | Evento de retorno. |
| `/Clientes` | Clientes y datos fiscales/territoriales. |

### Clientes y territorio

- Si el pais es `SV` o queda vacio, departamento y municipio son visibles y deben seleccionarse
  juntos.
- Si el pais es distinto de `SV`, departamento y municipio se ocultan y se limpian antes de guardar.
- El municipio se filtra por departamento; no se acepta un municipio de otro departamento.
- El backend normaliza codigos MH de tipo documento (`13`, `36`, etc.) al codigo interno usado por
  clientes (`DUI`, `NIT`, etc.).

### Nuevo DTE

- Al seleccionar un cliente registrado, la Web llena nombre, tipo de documento, documento, NRC y
  correo desde la ficha del cliente.
- Mientras hay cliente registrado seleccionado, esos campos se muestran como solo lectura porque el
  servidor toma el snapshot desde `ClienteId`.
- Al volver a modo manual, los campos quedan editables.

### Listado y busquedas

`/DteDocumentos` permite filtrar por busqueda libre, tipo DTE, estado, fecha desde/hasta y rango de
monto. La busqueda libre cubre numero de control, codigo de generacion, nombre del receptor y
documento del receptor. La paginacion conserva todos los filtros.

### Retorno

La pantalla de retorno carga solo DTE procesados elegibles: 01 Factura, 11 Exportacion y 14 Sujeto
Excluido. El selector permite filtrar en cliente por tipo, numero de control, codigo de generacion,
cliente, documento y monto dentro de los documentos cargados.

### Procesado

Cuando un DTE llega a `PROCESADO`, el ultimo paso del stepper se pinta como completado igual que los
estados anteriores. El estado fiscal/badge del documento se mantiene separado.

## Troubleshooting DTE

Mensaje: `Documento creado pero no se pudo generar JSON: La empresa emisora tiene datos incompletos`

- El DTE ya fue creado como borrador, pero fallo la generacion JSON inmediata.
- Verificar Empresa -> Editar: NIT, correo y telefono son obligatorios para emitir.
- Con el servicio actual, el mensaje incluye `Detalle:` con el campo exacto faltante.
- Si la empresa fue creada con nombres visibles en departamento/municipio, el servicio los resuelve
  contra los catalogos antes del JSON. No hace falta modificar historicos solo por ese formato.
- Si se instalo autostart, confirmar que los ejecutables en `out/local-autostart` fueron publicados
  despues del ultimo commit y que `/health/live` responde 200.

## Guardrails

- No commitear `appsettings.Local.json`, certificados, passwords, API keys ni material privado.
- Mantener todo acceso operativo filtrado por `EmpresaId`.
- Para cambios compartidos de DTE, correr prueba focalizada y luego `dotnet test NeoSTP.slnx`.
