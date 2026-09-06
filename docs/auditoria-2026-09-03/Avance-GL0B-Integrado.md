# GL-0B — integración de sesiones, MFA y límites HTTP

Fecha: 2026-09-04. Rama: `codex/gl0a-auth-security`.
**Integrado en código; NO desplegado. No es autorización de producción.**

> Histórico: los pendientes SSO/concurrencia/SQL/Android avanzaron en [GL-0C](Avance-GL0C-SSO-SQL-Android.md). Consultar ese registro para el estado actual; los resultados siguientes corresponden exclusivamente a GL-0B.

Este informe reemplaza el estado operativo del [registro preparatorio](Avance-GL0B-Preparado.md).
La auditoría original permanece como línea base, no como una descripción de los cambios posteriores.

## Código completado en este incremento

- Web utiliza el limitador compartido: login/cambio de contraseña, MFA y respuesta 429 en español.
  API agrega la política MFA a la nueva verificación. Cuotas de negocio y API keys no se mezclan.
- AuthService persiste sesiones en login local, SSO y cambio de empresa; refresh queda vinculado
  a su sesión/empresa y conserva su vencimiento absoluto. Ya no se emiten sesiones sin contexto
  para cambiar de empresa; se requiere una sesión FULL válida perteneciente al usuario.
- JWT y cookie incluyen identificador/propósito. Sus eventos de autenticación verifican en cada
  solicitud revocación, expiración, estado, credenciales, contexto y autorizaciones actuales.
  /me devuelve los datos de la sesión vigente. No hay fallback para credenciales anteriores sin sesión.
- Logout revoca la sesión padre y se audita también desde Web, sin requerir refresh. Rotar un
  refresh no permite evitar la revocación; enviar otro token en el cuerpo no revoca otra sesión.
  API admite logout con cuerpo vacío. No se cancelan retrospectivamente solicitudes ya autenticadas.
- SecurityStamp rota en bloqueo temporal, edición/estado de usuario, bloqueo/desbloqueo
  administrativo, cambio/reset de contraseña y confirmación/desactivación MFA. La modificación
  de roles/permisos/empresa se detecta por revalidación y huella de autorización.
- El administrador global legítimo sin MFA recibe únicamente un desafío MFA_ENROLL de 10 minutos,
  sin empresa, roles, permisos ni refresh. Un nombre de rol o tipo SUPERADMIN aislado no concede esa identidad.
- SSO con MFA recibe MFA_VERIFY, también restringido. La verificación consume el desafío y emite
  otra sesión completa; los fallos cuentan para el bloqueo. Repetir SSO no reinicia el contador.
  Solo un bloqueo temporal vencido puede liberarse automáticamente, nunca el administrativo.
- El middleware de desafíos deniega operaciones y endpoints anónimos de negocio. Solo admite
  las acciones marcadas para ese propósito; en Web los assets se permiten por metadata explícita.
- Web incluye enrolamiento, confirmación, verificación y recuperación, sin menú operativo, con
  antiforgery y no-store. La clave se muestra al generarla y no se acepta de vuelta desde el navegador.
  Confirmar obliga a nuevo login; los códigos no se guardan en TempData/URL. Recordarme no persiste desafíos.
- El administrador global no puede deshabilitar el MFA obligatorio. SSO revalida que el rol de
  aprovisionamiento sea asignable al tenant; esto NO resuelve por sí solo la vinculación por correo.
- El visor de contraseña existente se conservó y volvió a verificarse.

## Contratos de consumidores

| Situación | Respuesta / siguiente paso |
|---|---|
| Sesión completa | `User.sessionPurpose = FULL`, JWT y refresh vinculados a sesión |
| Administrador sin MFA | `mfaEnrollmentRequired = true`, usar enroll/confirm, guardar recoveryCodes y nuevo login |
| SSO pendiente de MFA | `mfaVerificationRequired = true`, POST `/api/auth/mfa/verify` con `code` |
| Desafío intentando operar | 403 explicando el paso MFA requerido |
| Credencial revocada, histórica o vencida | 401 API / login Web; descartar credenciales locales |
| Presupuesto de intentos agotado | 429 + Retry-After; esperar, no repetir inmediatamente |

Web redirige a `/Account/MfaEnrollment` o `/Account/MfaVerification` según el propósito.
Un login correcto no significa necesariamente acceso operativo: el consumidor debe revisar esos campos.
Android no fue modificado ni publicado en este incremento.

## Verificación

- Suite completa: **1,187 unitarias + 9 integración = 1,196 aprobadas**, sin fallos ni omitidas.
  Son 39 pruebas más que el incremento previo. El test build conserva CS8604 preexistente en
  `tests/NeoSTP.Tests.Unit/Inventario/LotesInventarioTests.cs:137`.
- Build Release final incremental: **0 errores y 0 advertencias**. `git diff --check` limpio.
  Hubo una colisión temporal de compilación al renderizar Razor en paralelo con otro build;
  se repitió secuencialmente y pasó, sin detener procesos del cliente.
- Tests de servicios: emisión persistida, logout sin refresh, token rotado, sesión ajena, empresa
  seleccionada, pérdida de permisos, MFA obligatorio, desafío SSO de un uso y bloqueo acumulado.
- Tests HTTP: controladores reales, AuthService/MfaService y validación JWT/cookie reales, usando
  exclusivamente credenciales sintéticas, EF InMemory y DataProtection efímero.
- Casos HTTP negativos: cookie/JWT copiados después de logout, credenciales históricas sin sesión,
  cambio de contraseña, bloqueo, retirada de rol, empresa suspendida, negocio desde desafío,
  bypass de antiforgery, desactivación MFA global y logout sin cuerpo.
- Limitador API: 23 pruebas de controlador más 13 de middleware; límites Web y MFA comprobados
  adicionalmente en el recorrido real de las pantallas.
- UI: 13 comprobaciones del visor + 14 de MFA; Razor real en Edge sin red, escritorio y móvil,
  sin overflow, sin menú operativo, códigos ocultos y formularios con antiforgery.
- Las pruebas denominadas integración usan InMemory. No prueban transacciones, índices ni bloqueos SQL.
- No se ejecutaron los Program de API/Web/Worker, configuración del cliente, seeds ni conexiones SQL.

Los comandos reproducibles son `dotnet test NeoSTP.slnx -c Release --no-restore`,
`dotnet build NeoSTP.slnx -c Release --no-restore` y los scripts de [LoginPreview](../../tools/LoginPreview/README.md).
El esquema se comprobó con [SchemaDesign](../../tools/SchemaDesign/README.md), cuya fábrica bloquea conexiones.

## Pendientes y límites antes del corte

1. **SEC-02 abierto:** SSO todavía puede vincular por correo. Exigir prueba de posesión de la cuenta
   local y validar proveedor/directorio/issuer/correo verificado antes de habilitar SSO productivo.
   No se cambiaron toggles ni credenciales de proveedores.
2. **Concurrencia MFA pendiente:** confirmación de enrolamiento y consumo de recovery todavía
   hacen lectura/modificación/guardado sin control de versión propio. Hacerlos atómicos y probar
   intentos simultáneos; no interpretar las pruebas secuenciales como garantía de un solo consumo concurrente.
3. Ensayar rotación de refresh, consumo del desafío y logout concurrentes con SQL Server aislado.
   Los tokens de concurrencia de sesión/refresh y SaveChanges no sustituyen esa evidencia.
4. Probar Android y OIDC reales. El callback del proveedor externo no se ejecutó contra Google/Entra;
   los ensayos parten de identidades federadas sintéticas y prueban el desafío local resultante.
5. Medir costo de validar sesiones/roles en base por solicitud y límites detrás de NAT/proxy.
   El limitador es por IP/proceso, no distribuido, y se reinicia al reiniciar cada host.
6. Coordinar API/Web, backup/restauración, revisión SQL y nuevo login. Se necesitan las migraciones
   **GL0A_RefreshSessionContext y GL0B_AuthSessionFoundation**, preparadas pero no aplicadas.
   El modelo no agrega cambios posteriores a esas migraciones. No usar el arranque normal para ensayar:
   ejecuta migraciones/seed. Un despliegue parcial dejaría el host antiguo con su comportamiento anterior.

AUTH-02/AUTH-03 tienen mitigaciones implementadas y evidencia local, pero no se declaran cerrados
operativamente mientras falten estos ensayos y el corte autorizado. El resto de hallazgos de la auditoría
continúa con su estado previo. DTE11 sigue cerrado según la indicación del usuario.

**Sin despliegue, reinicio, migraciones aplicadas, borrado de datos, reseteo de IDs/correlativos,
emisión DTE, commit ni push.**
