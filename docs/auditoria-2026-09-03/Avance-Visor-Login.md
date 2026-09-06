# Visor de contraseña — 2026-09-03

> Este archivo describe el incremento anterior. El usuario autorizó continuar; el nuevo estado, componentes preparados y bloqueo de integración se registran en [GL-0B](Avance-GL0B-Preparado.md).

Entregado en rama, sin despliegue: botón mostrar/ocultar en login con SVG local, compatible con teclado,
aria-label/aria-pressed y ocultación automática al enviar, cambiar de pestaña o salir.
El código no copia ni registra contraseñas. Se corrigió además el orden de carga de jQuery antes de sus validadores.

Verificación: 13 comprobaciones de navegador contra el Razor real; vista revisada en escritorio y móvil.
Suite de solución: 1,094 unitarias + 9 integración aprobadas. Build final incremental sin errores/advertencias.
Se conserva la advertencia preexistente CS8604 en el test build de LotesInventarioTests.cs:137.

## Seguridad: bloqueo de la revisión automática

La continuación propuesta de enrolamiento MFA restringido y sesiones revocables fue rechazada por el
control automático de seguridad por su alcance sobre autenticación, JWT/cookies y SSO. También fue
rechazada la integración de límites HTTP en los hosts/controladores. Ninguna de esas dos propuestas
se aplicó. Se retiraron los cuatro archivos preparatorios nuevos y sus referencias; no hay una nueva
tabla de sesiones, campos o migración parcial de ese diseño en esta rama.

Se preservan las correcciones anteriores de [GL-0A](Avance-GL0A.md). AUTH-02 sigue parcial y AUTH-03 pendiente.
No se cambió el flujo central de login/SSO en este turno. No se iniciaron, publicaron o reiniciaron los
servicios del cliente, ni se aplicaron migraciones, commit/push o cambios de datos.

Para continuar se necesita autorización específica de la modificación de AuthService, hosts y
controladores API/Web para registrar/revocar sesiones, restringir MFA también en SSO y limitar intentos.
El corte futuro requeriría nuevos inicios de sesión; permanecería separado de cualquier despliegue o aplicación de migración.
