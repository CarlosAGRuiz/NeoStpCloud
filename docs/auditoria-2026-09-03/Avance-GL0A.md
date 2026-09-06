# Avance GL-0A — privilegios y sesiones

Fecha local: 2026-09-03. Rama: codex/gl0a-auth-security. Base: a8c5d16.
Estado: implementado y verificado en rama, **sin despliegue, commit/push ni migraciones aplicadas**.
El usuario autorizó expresamente modificar AuthService para refresh, desbloqueo temporal y MFA.

## Entregado: SEC-01

- Crear/editar usuarios valida identidad y ámbito confiable: un tenant no puede asignar tipo SUPERADMIN ni roles privilegiados, ajenos, inactivos o inexistentes.
- Rechazos no dejan usuarios/roles parcialmente creados ni eliminan asignaciones anteriores.
- Roles tenant no pueden llamarse SUPERADMIN, incluir permisos de plataforma ni editar roles globales.
- Listas y asignación de roles/permisos respetan el ámbito. Roles propios inactivos pueden administrarse para reactivación.
- Membresías y configuración del rol por defecto SSO validan pertenencia y privilegios.
- La autorización global exige identidad SUPERADMIN sin empresa y rol correspondiente, no solo el nombre de un rol.
- La emisión de claims filtra roles inactivos, ajenos o privilegiados no autorizados; conserva al administrador de plataforma legítimo.
- No se rediseñó el enlace/login SSO; SEC-02 sigue pendiente.

## Entregado: TENANT-01 y AUTH-01

- Cada refresh nuevo conserva ContextEmpresaId y ContextInitialized: login local, SSO, cambio de empresa y rotación.
- Refresh y /api/auth/me resuelven el contexto seleccionado, no sustituyen silenciosamente la empresa por la principal.
- Se revalidan empresa activa, membresía y rol; una membresía retirada o un rol inactivo/ajeno/privilegiado impide renovar.
- Al operar como miembro se usan exclusivamente su rol y permisos; no se hereda el tipo administrativo de la empresa principal.
- Sesiones simultáneas conservan sus respectivos contextos.
- Refresh antiguo sin contexto explícito exige nuevo login (HTTP 401); no se infiere la empresa.
- RevokedAt es token de concurrencia: una segunda rotación se rechaza. Logout mantiene semántica idempotente.
- Solo un bloqueo BLOQUEADO con fecha temporal vencida se libera; un bloqueo administrativo sin fecha o una cuenta INACTIVA no se habilita.
- Un intento incorrecto después del vencimiento inicia una nueva ventana, no perpetúa la anterior.

## MFA: corrección parcial de AUTH-02

- Contraseña y MFA incorrectos comparten el contador y bloqueo configurado; con el valor actual de pruebas, el quinto fallo bloquea.
- Código ausente no genera sesión ni borra intentos previos; código válido restablece el contador solo al completar login.
- Login Web acepta y envía TOTP o código de recuperación al mismo servicio que API.
- Reenrolar/reconfirmar no reemplaza un MFA ya activo ni sus códigos de recuperación.
- MFA activo sin secreto no acepta autenticación por omisión: falla cerrado.

**AUTH-02 no se declara cerrado:** faltan limitadores específicos de las rutas de autenticación y la sesión restringida de enrolamiento del administrador. El indicador MfaEnrollmentRequired todavía no limita por sí mismo sus permisos. Tampoco se implementó revocación inmediata integral de access tokens/cookies (AUTH-03).

## Verificación

- dotnet test NeoSTP.slnx -c Release --no-restore: **1,094 unitarias + 9 integración aprobadas**, 0 fallos/omitidas.
- 41 regresiones nuevas en este incremento; se conservan las 48 de SEC-01 del incremento anterior.
- dotnet build NeoSTP.slnx -c Release --no-restore: **0 errores / 0 advertencias** en el build final incremental.
- El test build mostró únicamente la advertencia preexistente CS8604 en LotesInventarioTests.cs:137.
- Modelo EF y última migración coinciden; SQL generado revisado.
- Arnés: SEC-01 sigue rechazado; AUTH-01 permite login tras vencimiento; MFA registra 5 fallos/bloqueo; refresh mantiene empresa 1002.
- [Evidencia actual](evidencia/reproducciones-gl0a-auth.json), [baseline](evidencia/reproducciones-aisladas.json), [resumen verificable](evidencia/verificacion-gl0a-auth.md).
- Pruebas en EF InMemory y controladores/servicios aislados: no equivalen a concurrencia SQL real, HTTP autenticado completo ni prueba visual/dispositivo de Web/Android.

## Migración preparada, NO aplicada

[20260904021242_GL0A_RefreshSessionContext](../../src/NeoSTP.Infrastructure/Persistence/Migrations/20260904021242_GL0A_RefreshSessionContext.cs) agrega únicamente:

- ContextEmpresaId: int nullable.
- ContextInitialized: bit con default false para sesiones históricas.

El prefijo de la migración está en UTC (2026-09-04); corresponde a la jornada local 2026-09-03.
No reinicia IDs ni correlativos, no borra sesiones/documentos, no modifica catálogos ni datos fiscales.
No se añade cascada/FK de empresa; al utilizar el contexto se valida su existencia y acceso actuales.

[SQL idempotente revisado](evidencia/refresh-context.sql). Se generó con [SchemaDesign](../../tools/SchemaDesign/README.md), que no carga secretos ni inicia hosts y bloquea explícitamente conexiones SQL.

Antes de desplegar:

1. Ensayar migración y rotación concurrente en SQL aislado con backup/restauración.
2. Coordinar la publicación API/Web/Worker y comunicar nuevo login para los refresh históricos.
3. Revisar el arranque actual, que puede aplicar migraciones/seed automáticamente. No iniciar esta release sobre la base del cliente sin aprobación del corte.
4. Completar los demás controles GL-0 antes de habilitar producción.

## Siguiente incremento recomendado

Completar AUTH-02 (enrolamiento restringido y rate limit) y AUTH-03 (revocación inmediata JWT/cookie), con pruebas HTTP aisladas. SEC-02/SSO y CONNECT-01 continúan abiertos junto con las demás puertas del [plan de producción](Plan-Produccion-NEO.md).

No se tocaron servicios activos, datos del cliente, certificados ni ambiente MH. DTE11 permanece cerrado.
