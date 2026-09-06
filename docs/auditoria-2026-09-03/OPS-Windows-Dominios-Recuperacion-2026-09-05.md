# OPS — Windows, dominios y recuperación real — 2026-09-05

Estado: preparación local validada; producción sigue NO-GO. El usuario confirmó este equipo Windows y los dominios app.neostp.com y api.neostp.com. Este corte continúa el informe OPS anterior y no equivale a instalar su código en los hosts activos.

## Resultado comprobado

- Build Release: 0 errores y 0 advertencias. Suite: 2112 unitarias y 9 integraciones aprobadas, sin omisiones. Las 39 pruebas de dominios/HTTPS están incluidas en las 2112; no se suman otra vez.
- Los fragmentos públicos de Web/API restringen Host al dominio de cada aplicación, mantienen puertos loopback 5031/5058 y CORS de API sólo para https://app.neostp.com. Configuran HTTP→HTTPS 308 al puerto 443 mediante binding explícito en ambos Program.
- Pruebas con TestServer y middleware del framework comprueban hosts incorrectos, CORS, conservación de ruta/query y método en la redirección, proxy loopback sin bucles y rechazo de esquema reenviado por un origen no confiable. No sustituyen pruebas sobre los servicios instalados.
- Desde la red pública: login HTTPS, Scalar y health responden 200; documentos DTE responde 401 sin autenticación. La validación de certificados no se deshabilitó. CSP y nosniff presentes.
- Hallazgo abierto: HTTP de login y health responde 200 sin redirección; HSTS no aparece en las respuestas HTTPS observadas. Por eso HTTPS público aún no está aceptado para el corte.
- Recuperación real: seis XML de claves cifrados, cuatro campos DTE presentes recuperados tanto desde el perfil actual como desde una copia independiente con ACL restringida. Origen/copia sin cambios y cero intentos de escritura de claves. No se exportaron contraseñas, tokens, claves XML ni hashes de secretos al repositorio.

## Cloudflare y configuración preparada

[Regla acotada a ambos dominios](../../../tools/ProductionDeployment/config/CLOUDFLARE-HTTPS.md), preparada y NO aplicada. El panel no fue accesible porque el navegador de automatización falló al inicializarse y no hay conector administrativo disponible. Aplicar la regla en el panel y repetir sondas HTTP sin seguir redirecciones, y luego HTTPS, antes de cerrar esta puerta. No se modificó DNS ni TLS global.

[Fragmentos de configuración](../../../tools/ProductionDeployment/config/README.md): no contienen secretos y no bastan para arrancar. Mantienen migración/seed/bootstrap, Worker, checkout, aplicación de pagos y webhook Wompi apagados. Producción del host y ambiente fiscal de cada empresa son configuraciones distintas. No copiar Local.json a producción ni cambiar hashes del candidato para incorporar configuración: componer una instalación separada y trazable.

## Recuperación y límites

[Evidencia de ambas fases](evidencia/ops-key-recovery-2026-09-05/README.md) y [procedimiento](../../../tools/KeyRecoveryVerification/README.md). Copia retenida fuera del repositorio en C:\ProgramData\NeoSTP-KeyRecovery-ec58523371504459bc195d0dc2e929af. ACL sin herencia, sólo usuario actual, SYSTEM y Administradores.

Se recuperaron contraseña MH y token MH de NEO 2 y DANIEL 23; contraseña del certificado está ausente en ambos registros. Esto no prueba que las credenciales sean válidas ante Hacienda. Sólo acredita mismo Windows y misma identidad; no acredita pérdida del perfil, otra cuenta de servicio, traslado a otro equipo o respaldo externo. El arnés contiene dos SELECT fijos y no usa EF; ApplicationIntent=ReadOnly no reemplaza permisos SQL. Las banderas de no escritura se respaldan en fuente y revisión, no en una auditoría independiente del servidor SQL.

La revisión independiente no encontró P1/P2 en el arnés dentro de ese alcance. La recuperación X509 sintética del corte anterior y esta recuperación real tienen alcances diferentes; ninguna reprotege las claves heredadas automáticamente.

## Próximo incremento operativo

1. OPS-WIN-01: decidir/provisionar la identidad concreta de los servicios y probar carga del perfil, acceso al certificado/clave privada, al ring heredado y al storage. Respaldar claves y archivos fuera del equipo, y validar recuperación. No arrancar con un ring vacío.
2. OPS-SQL-01: separar credencial de migración de la identidad SQL de runtime; reducir privilegios y probar permisos requeridos. La identidad observada actualmente tiene sysadmin. Preparar configuración protegida sin incorporar secretos al paquete.
3. OPS-CAP-01: configurar y validar proveedores del alcance de salida; para funciones opcionales sin proveedor, implementar deshabilitación efectiva en entrada/cola/Worker y probarla. Los guards actuales rechazan Mock/ausentes: escribir un nombre de proveedor real no demuestra que funcione. Billing, Scan, WhatsApp y Push carecen de configuración real aceptada; Wompi sigue sin cuenta/aplicativo.
4. OPS-EDGE-01: aplicar/verificar redirección pública y HSTS; después instalar la configuración y el candidato final, validar servicios sin sesión interactiva, HTTPS y health. La aplicación dispone del código de redirección, aún no desplegado.
5. Corte controlado: respaldo fresco, migración ensayada 79→89, conservación de ambas empresas, smoke de permisos/datos y configuración fiscal de NEO por UI con las credenciales que el titular tiene disponibles. Validar certificado, tipos fiscales efectivos, SMTP y operación legítima antes de primera emisión.

CLI-23 sigue abierto: cliente activo, implementación pagada, USD 15 al cierre de mes (septiembre: 30/09); corregir el vencimiento heredado 20/09 con trazabilidad, sin registrar esa mensualidad como pagada. H-06, sandbox Wompi, MFA del titular, correo y cuenta de cobro mantienen sus dependencias. N1co continúa sólo evaluado.

## Estado de la instalación activa

Este incremento no reinició tareas ni instaló servicios, no inició Worker, no migró SQL, no cambió ambientes fiscales y no envió DTE, correos ni cobros. El inventario previo sigue siendo 79 migraciones frente a 89 del código, binarios antiguos iniciados por tareas interactivas y NEO/DANIEL en PRUEBAS. No se volvió a consultar SQL para repetir ese inventario en este corte; la recuperación leyó únicamente sus campos acotados.
## Paquete de este corte

Candidato nuevo: `tmp/production-candidate/20260905T204702Z-25aabc1a42d34982baf5723f74af0408/`. Web 349, API 154, Worker 144: 647 hashes revalidados, sin archivos sobrantes; 65 assets fuente inspeccionados y login-password.js idéntico. SHA256 del manifiesto: `C2DBC6FD3C086915F1553E421DE0D9A95C4AD4822202206AF10C35771B711764`.

[Evidencia de build, pruebas y publicación](evidencia/production-windows-2026-09-05/README.md). PublicationComplete=true, ReadyForDeployment=false. Las pruebas del build Release y la publicación sin símbolos son validaciones separadas, no se afirma identidad binaria entre ellas. HEAD a8c5d163fb372359c4738f0c3814ef1ee14024eb con árbol de trabajo modificado; no se creó commit ni se desplegó. El candidato anterior y su evidencia se conservan como corte histórico.