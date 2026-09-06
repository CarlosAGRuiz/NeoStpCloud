# GL-0B — componentes preparados; integración pendiente

> Registro histórico: el estado de integración que sigue ya fue reemplazado. El 2026-09-04 se
> conectaron límites API/Web, sesiones revocables JWT/cookie y MFA restringido, sin desplegar.
> Consultar el [informe actual](Avance-GL0B-Integrado.md) para evidencia, contratos y pendientes.
> El detalle siguiente conserva el estado anterior, incluyendo los bloqueos de edición ya superados.

Fecha local: 2026-09-03. Rama: codex/gl0a-auth-security.
**No está terminado ni habilitado en los hosts. No aprobar producción con este avance.**

El usuario autorizó expresamente continuar AuthService, hosts/controladores API/Web y SSO,
aceptando nuevo login en el despliegue futuro, sin desplegar ni aplicar migraciones ahora.

## Código y pruebas de este incremento

| Componente | Verificación | Estado operativo |
|---|---|---|
| AuthSessionService / SessionUserInfoFactory | 20 pruebas con EF InMemory | No registrado en DI ni llamado por JWT/cookies |
| AuthRateLimiting | 13 pruebas HTTP con TestServer | No registrado en API/Web ni asociado a sus rutas |
| AuthSession + campos de soporte + migración | Build y SQL offline revisado; modelo coincide | Migración preparada, no aplicada |

El validador comprueba expiración, revocación, estado del usuario, credenciales y permisos actuales,
contexto de empresa y claims exactos. Prepara propósitos FULL, MFA_ENROLL y MFA_VERIFY; los dos últimos
no llevan empresa, roles ni permisos operativos. La huella de credenciales solo se almacena en servidor.

El limitador preparado tiene ventanas por IP y políticas separadas para login (10/minuto),
MFA (10/minuto) y refresh (30/minuto), sin cola, con HTTP 429, Retry-After y no-store.
La respuesta de API es JSON estándar y la de Web HTML en español, sin reflejar contraseñas.
Configuración futura: Security:AuthRateLimit (WindowSeconds, LoginPermits, MfaPermits, RefreshPermits).
Los presupuestos son por proceso, no distribuidos. El código usa RemoteIpAddress, nunca encabezados
crudos; las pruebas verifican el procesamiento de forwarded headers con proxy local confiable.
La configuración del proxy de la API real todavía no se modificó.

## Bloqueo de integración

La revisión automática rechazó dos propuestas, ninguna aplicada:

1. Integración conjunta de emisión, renovación, cierre de sesión y desafíos MFA/SSO.
2. Integración del limitador en hosts/controladores, aun después de las 13 pruebas HTTP.

El editor de parches puntuales falla al leer archivos existentes por un error del sandbox
(helper_unknown_error). El reemplazo de archivos completos permitió crear las piezas aisladas,
pero la revisión rechazó ese formato sobre los hosts y el núcleo de autenticación por el riesgo
de alterar accesos/arranque. No se intentó aplicarlo mediante shell ni otro mecanismo alternativo.

La autorización del usuario no equivale a aprobación del cambio por el control automático.
Para continuar hace falta una edición puntual revisable y aprobación de su alcance reducido.
Siguiente incremento propuesto: integrar y probar solo el limitador de la API; después Web;
por separado, sesiones JWT/cookie y MFA/SSO. Sin iniciar hosts que ejecuten migraciones/seed.

## Lo que NO está implementado todavía

- AuthService no crea ni revoca Core_AuthSessions; los refresh siguen con la lógica GL-0A anterior.
- SecurityStamp aún no se rota en cambios de contraseña/administración/MFA.
- UserInfo contiene campos preparatorios SessionId/SessionPurpose, pero la emisión actual no los utiliza.
  Los consumidores no deben interpretarlos como prueba de una sesión persistida.
- No hay hooks OnTokenValidated/OnValidatePrincipal para invalidar JWT/cookies.
- MfaEnrollmentRequired sigue siendo un indicador, no una restricción operativa.
- No se cambió el flujo SSO, ni se implementó su desafío MFA o su UI.
- No se activaron límites HTTP específicos de autenticación.

Por tanto AUTH-02 sigue parcial y AUTH-03 pendiente. SEC-02/SSO también permanece abierto.

## Verificación

- dotnet test NeoSTP.slnx -c Release --no-restore: **1,127 unitarias + 9 integración aprobadas**;
  0 fallos, 0 omitidas. Las 33 nuevas se ejecutan sin bases/servicios del cliente.
- dotnet build NeoSTP.slnx -c Release --no-restore: **0 errores**, una advertencia preexistente
  CS8604 en LotesInventarioTests.cs:137.
- Tests no utilizan Program de API/Web: TestServer solo monta rutas de prueba y el middleware preparado.
  No equivalen a validación de las rutas reales o del ciclo login/refresh/logout autenticado.
- Las 20 pruebas de sesión no demuestran concurrencia transaccional de SQL Server.
- Visor de contraseña ya entregado anteriormente; no se cambió en este incremento.
- git diff --check sin errores.

## Migración preparada; NO aplicar aún

[20260904030641_GL0B_AuthSessionFoundation](../../src/NeoSTP.Infrastructure/Persistence/Migrations/20260904030641_GL0B_AuthSessionFoundation.cs)
agrega SecurityStamp en usuarios, SessionId nullable en refresh y Core_AuthSessions.
FK de refresh a sesión sin cascada; FK de sesión a usuario con cascada. No modifica datos fiscales,
IDs, correlativos ni seeds. El default de SecurityStamp conserva Guid.Empty hasta su integración.

[SQL idempotente de revisión](evidencia/auth-session-foundation.sql), generado con
[SchemaDesign](../../tools/SchemaDesign/README.md): no carga configuración real y bloquea conexiones SQL.
El modelo coincide con la última migración. La existencia de la migración no activa ninguna protección.

No se desplegó, reinició, aplicó migraciones, borró datos, emitió DTE, hizo commit ni push.
Los cambios GL-0A previos se preservaron; DTE11 permanece cerrado.
