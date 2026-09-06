# Configuración pública del despliegue Windows confirmado

Host aprobado: este Windows con Cloudflare. Web: https://app.neostp.com. API: https://api.neostp.com.

Estos fragmentos se aplican a un despliegue preparado, aún NO a la instalación activa. No contienen SQL, JWT, credenciales de proveedores ni material de DataProtection. No bastan para arrancar el candidato y no sustituyen la comprobación de configuración efectiva. El publicador de binarios continúa excluyendo todo appsettings; conservar sus hashes e incorporar configuración en una etapa de instalación separada.

- web: AllowedHosts=app.neostp.com, escucha loopback5031 y referencia API HTTPS pública.
- api: AllowedHosts=api.neostp.com, escucha loopback5058 y CORS sólo https://app.neostp.com.
- worker: sin listener y apagado hasta aceptar sus trabajos.
- Todos: migraciones/seed/bootstrap apagados. Checkout, aplicación de pagos y webhooks Wompi apagados. No se asignan proveedores ficticios para sortear validaciones.

Fijar explícitamente Production en la configuración del servicio. Componer después los valores privados mediante entorno o archivo protegido, manteniendo dominio/puertos y flags acordados. No copiar appsettings.Local.json de desarrollo. Antes del corte verificar que no hay otra instancia ocupando los puertos; detener las tareas actuales sólo tras aceptación y respaldo.

Health local requiere Host del dominio correcto; localhost como Host se rechaza por diseño. El proxy confiable es únicamente loopback. Verificar salud pública después del corte; no confiar en un Host arbitrario ni ampliar el allowlist para corregir una sonda mal configurada.

No cambian el ambiente fiscal guardado en SQL de NEO ni de DANIEL. Hacienda PRODUCCION se configura por empresa después de validar despliegue/credencial, no mediante ASPNETCORE_ENVIRONMENT.

Web/API configuran HttpsRedirection:HttpsPort=443 y RedirectStatusCode=308. Las 39 pruebas de HostFiltering/CORS/redirección pasaron; preservan ruta/query y no generan bucles con el proxy loopback. La regla pública Cloudflare está preparada en CLOUDFLARE-HTTPS.md, aún sin aplicar.
