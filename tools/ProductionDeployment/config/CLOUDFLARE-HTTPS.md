# Redirección pública NeoSTP — aplicada y verificada

Actualización 2026-09-05: regla 24084a852fb744bd93cabdd5afa750b6 activa en Cloudflare. Cinco sondas externas aprobadas: HTTP→HTTPS 308 conserva ruta/query en app y API, destinos HTTPS 200 y DTE anónimo 401. [Evidencia de redirección](../../../docs/auditoria-2026-09-03/evidencia/cloudflare-https-2026-09-05/README.md).

HSTS inicial también aplicado: regla debfc7dc7c3f4bf2afd30d0fba68d85f, filtro `(http.host in {"app.neostp.com" "api.neostp.com"} and ssl)`, Set static `Strict-Transport-Security: max-age=86400`. Cinco sondas posteriores aprobadas. Sin includeSubDomains/preload. Coordinar esta sustitución del encabezado con la política del origen cuando se despliegue el candidato; no prolongar sin verificar recuperación y disponibilidad. [Evidencia HSTS](../../../docs/auditoria-2026-09-03/evidencia/cloudflare-hsts-mail-2026-09-05/README.md).

## Preparación histórica, anterior al despliegue


Comprobación externa del 5 de septiembre: HTTP devuelve 200 en app.neostp.com/Account/Login y api.neostp.com/health. HTTPS funciona con validación de certificado; falta forzar la redirección HTTP y no se observa HSTS.

En Cloudflare, zona neostp.com, Rules / Redirect Rules / Create rule / Single Redirect:

- Nombre: NeoSTP app y API: HTTP a HTTPS.
- Filtro: `(http.host in {"app.neostp.com" "api.neostp.com"} and not ssl)`.
- Destino dinámico: `concat("https://", http.host, http.request.uri.path)`.
- Estado 308; conservar query string activado.

La regla propuesta sólo cubre los dos hostnames autorizados. El JSON adjunto representa una regla individual: no usarlo para sustituir todo un ruleset ni borrar reglas existentes. Verificar primero que no exista una regla equivalente. No cambiar DNS, TLS global ni Always Use HTTPS de toda la zona para resolver este caso acotado.

Resultado esperado: http://app.neostp.com/Account/Login?ReturnUrl=%2F → 308 → https://app.neostp.com/Account/Login?ReturnUrl=%2F. HTTPS no debe redirigirse a sí mismo. Probar ambas direcciones sin seguir redirecciones y después sus destinos, sin contraseñas ni cobros.

No se pudo automatizar el panel: no hay conector administrativo Cloudflare disponible y el navegador falló al inicializarse (helper_unknown_error). Cloudflare no se modificó. El titular debe aplicar esta regla en su panel o proporcionar acceso administrativo mediante un canal seguro, sin enviar tokens por chat.

Fuentes oficiales: [crear regla](https://developers.cloudflare.com/rules/url-forwarding/single-redirects/create-dashboard/) y [ajustes](https://developers.cloudflare.com/rules/url-forwarding/single-redirects/settings/). La aplicación tiene preparado el respaldo HttpsRedirection.HttpsPort=443 y 308 para su despliegue productivo; todavía no modifica la instalación activa.
