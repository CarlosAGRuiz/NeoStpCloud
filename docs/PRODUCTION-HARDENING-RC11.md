# Production Hardening RC11

Fecha: 2026-09-14
Alcance: política MFA opcional y activación inicial segura del Worker.

## Política MFA

- MFA/TOTP es una función de seguridad opcional para todos los usuarios y roles.
- Un usuario sin MFA recibe una sesión completa; no existe enrolamiento forzado.
- Si el usuario habilita MFA, el login local exige TOTP o un código de recuperación.
- SSO con MFA habilitado conserva el desafío restringido `MFA_VERIFY`.
- Deshabilitar MFA requiere un código TOTP o de recuperación válido.
- Las sesiones históricas `MFA_ENROLL` se rechazan y requieren iniciar sesión otra vez.

## Perfil inicial del Worker en producción

- Servicio `NeoSTP.Worker`: inicio automático y proceso en ejecución.
- Compuerta maestra: `Worker:Enabled=true`.
- Único job habilitado inicialmente: `Worker:LimpiezaTokens:Enabled=true`.
- Retención inicial: 30 días; primera ejecución 30 segundos después del arranque y luego cada 24 horas.
- Permanecen deshabilitados retransmisión/contingencia DTE, webhooks, notificaciones, alertas, cobros,
  proveedores de billing, limpieza de auditoría, backup programado y tareas de fondo.
- La emisión y transmisión normal de la primera factura es síncrona y no depende de estos jobs.
- Retransmisión y lotes de contingencia se habilitan solo después de validar el primer DTE productivo.

## Verificación de salida

1. Compilación Release y suites Unit/Integration en verde.
2. API y Web saludables en la release RC11.
3. Worker en ejecución sobre la misma release, sin proveedores Mock y sin aplicar migraciones/seed.
4. Log de arranque y primera ejecución de `LimpiezaTokensWorker` sin errores.
5. Configuración externa respaldada antes del cambio; rollback conserva RC10 y el respaldo de configuración.
