# GL-0C — SSO, concurrencia SQL y consumidor Android

2026-09-04. Código local; **NO desplegado ni autorizado para producción**.
Backend: `codex/gl0a-auth-security`, base `a8c5d16`. Android: `codex/auth-session-mfa`, base
`b12ee82745c6a3a53a2e4887cd37be19cd755f45`, checkout hermano `../neocloud_mobile_android`.

## Implementado

- SSO identifica por proveedor + issuer + sujeto estable. Se eliminó la vinculación automática por correo.
  Las cuentas existentes y vinculaciones históricas sin issuer requieren contraseña local y MFA si está
  habilitado. Una identidad ya vinculada no puede reemplazarse silenciosamente ni cruzar empresas.
- El callback toma identidad exclusivamente del cookie protegido de OIDC; un proveedor desconocido,
  issuer ausente o sujeto ausente se rechaza. La pantalla de vinculación tiene CSRF, no-store y rate limit.
  El cookie externo vence a los 10 minutos sin renovación deslizante.
- Entra valida issuer, claves del proveedor y directorio GUID autorizado; no se mantiene ValidateIssuer=false.
  Google requiere issuer reconocido, correo verificado y `hd` del Workspace autorizado. La política de la
  empresa se revalida incluso para sujetos vinculados; cambiarla rota SecurityStamp. Plataforma global usa
  acceso local con MFA, no el SSO corporativo. No se habilitaron proveedores ni se cambiaron sus secretos.
- MFA usa MfaVersion como token de concurrencia para enrolamiento, confirmación, desactivación y consumo
  de recuperación. La solicitud perdedora no devuelve claves/códigos. IntentosFallidos usa CAS con reintentos;
  emitir sesiones ante conflicto falla de forma cerrada. SsoIssuer también protege la vinculación concurrente.
- Android reconoce FULL/MFA_VERIFY/MFA_ENROLL. Los desafíos no conceden permisos ni persisten tras reinicio;
  MFA_VERIFY tiene pantalla y ruta restringidas. La administración global conserva su restricción a Web.
- Android ahora trata 401 como error HTTP, renueva con una sola solicitud concurrente y no resucita la sesión
  después de logout. Guarda cada par de tokens en una única entrada segura, descarta sesiones heredadas,
  comunica revocación a Riverpod y llama logout aun sin refresh. No reintenta 429/errores de red en bucle.
- Confirmación MFA en Android entrega los códigos una vez y pide nuevo login. Cambio de contraseña/MFA
  cierra el contexto local. MFA admite recuperación alfanumérica. Visor de contraseña accesible conservado.
  El workflow APK ejecutará analyze/test antes de compilar; no fue despachado ni publicado.

Estas reglas siguen la separación entre identidad estable y atributos de correo descrita por
[Microsoft Entra](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation) y
[Google OIDC](https://developers.google.com/identity/openid-connect/openid-connect).

## Evidencia ejecutada

- `dotnet test NeoSTP.slnx -c Release --no-restore`: **1,215 unitarias + 9 integración = 1,224** sin fallos ni omitidas.
  Permanece advertencia CS8604 preexistente de LotesInventarioTests:137 en el test build.
- Build Release final incremental: **0 errores, 0 advertencias**. `git diff --check` limpio en ambos repositorios.
- [AuthSqlVerification](../../tools/AuthSqlVerification/README.md): **11 comprobaciones concurrentes/transaccionales**
  en SQL real aislado; además cadena completa de migraciones sobre base vacía, sin pendientes. SQL 2025 LocalDB,
  no InMemory. Las bases generadas se eliminaron al terminar. No se conectó a la base del cliente.
- Modelo EF consistente con la nueva migración `20260904125956_GL0C_SsoIdentityMfaConcurrency`, generada offline.
  Añade MfaVersion, SsoIssuer e índice único compuesto. No rellena issuer por suposición en cuentas históricas.
- Razor real en Edge aislado: **30 comprobaciones** (13 visor + 17 MFA/SSO), escritorio/móvil y revisión visual
  de vinculación. Sin desbordamiento; CSRF y campos de contraseña verificados. No se ejecutaron hosts reales.
- Flutter **3.47.2 / Dart 3.13.2**, SDK local de prueba en `C:/Neo/Tools/flutter-auth-audit`:
  **16 pruebas** de contrato, concurrencia de refresh, revocación, recuperación, visor y navegación restringida.
  `flutter analyze --no-pub`: **sin incidencias**.
  HTTP y almacenamiento nativo se simulan; no se llamó al túnel ni a la API activa.

## Puertas externas todavía necesarias

1. OIDC real con cuentas/credenciales autorizadas de Google y Entra: callback, consentimiento, revocación y
   dominios registrados. Los tests locales prueban opciones/claims/servicios; no sustituyen al proveedor real.
2. APK y dispositivo: el intento `flutter build apk --debug --no-pub` se detuvo por **Android SDK ausente**.
   No se instaló SDK Android ni se aceptaron licencias por el usuario. No hay APK generado/publicado.
3. Ensayo de restauración/migración en la versión SQL del despliegue y con un backup autorizado, carga y
   configuración proxy/NAT. No basta un esquema vacío ni un limitador en memoria por proceso.
4. Corte coordinado API/Web/Android con backup, revisión SQL y login nuevo. GL0A + GL0B + GL0C están pendientes
   **en la base del cliente**. No arrancar hosts sobre ella para probar: aplican migraciones/seed al iniciar.
   El rollback del índice antiguo puede fallar si se han vinculado sujetos iguales en issuers distintos:
   usar restauración coordinada aprobada, no Down a ciegas.

SEC-02 y concurrencia MFA tienen correcciones y evidencia local; no se declara cerrado el pase operativo
a producción. Los demás hallazgos conservan su estado. DTE11 continúa cerrado por indicación del usuario.
Sin cambios a certificados, datos fiscales, IDs/correlativos, servicios activos, commit ni push.
