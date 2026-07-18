# Runbook — Salida a producción y primeros clientes

**Fecha:** 2026-07-18 · **Contexto:** cierre del plan pre-verticales (`ec7263c`..`fab86f6`), suite 829 unit + 9 integración verde. Complementa `Runbook-Demo-Release.md`, `Sprint20-Disaster-Recovery.md` y `Runbook-Storage-Secretos-Retencion.md`.

El código ya trae implementados todos los providers reales; salir a producción es **configuración + credenciales + verificación**. El guard `ProductionGuards` bloquea el arranque en `Production` si algún provider crítico sigue en Mock, así que los pasos 1-2 no se pueden saltar por accidente.

---

## 1. Providers: de Mock a real

| Config | Mock (hoy) | Real disponible | Credencial necesaria |
|---|---|---|---|
| `Email:Provider` | Mock | `Smtp` | Host/puerto/usuario/clave SMTP |
| `Billing:Provider` | Mock | `Stripe` / `Wompi` / `MercadoPago` / PayPal / Transferencia | Llaves + webhook secrets de la pasarela elegida |
| `Push:Provider` | Mock | `Fcm` (`FcmPushSender`) | Service account JSON de Firebase |
| `Scan:Provider` | Mock | `Gemini` (`GeminiScanExtractionService`) | API key de Gemini |
| `WhatsApp:Provider` | Mock | Meta (V2.5) | Token + phone id de WhatsApp Business |
| Firmador DTE | `MockDteSignerService` | `Pkcs12DteSignerService` / `HaciendaCertMhDteSignerService` | Certificado .p12/credenciales MH **por empresa** |

- Verificación de NIT contra MH: **no hay API pública**; queda la validación local de formato (`NitVerificationService`) con hook listo si MH publica una.
- `Ops:PermitirMocksEnProduccion` debe quedar **ausente o en false**. Solo se pone `true` en demos con nombre de ambiente Production, de forma consciente.

## 2. Secretos y configuración (por ambiente, nunca en el repo)

- [ ] `ConnectionStrings` a SQL Server productivo (usuario propio, no `sa`).
- [ ] `Jwt:Key` nueva y fuerte (la plantilla trae `REPLACE_IN_LOCAL_OR_SECRETS`).
- [ ] `Cors` con los orígenes reales del front/app (deny-by-default ya aplica sin lista).
- [ ] Llaves de pasarela + webhook secrets (Stripe/Wompi/MP según mercado).
- [ ] Certificado de firma DTE de cada empresa cargado vía configuración DTE (proceso por cliente, no global).
- [ ] Storage de scans y retención según `Runbook-Storage-Secretos-Retencion.md`.

## 3. Infraestructura

- [ ] Contenedores: `docker-compose.yml` + Dockerfiles de Api/Web/Worker ya existen; fijar tags de release.
- [ ] Dominio + TLS delante de Web y Api (HSTS ya activo en código).
- [ ] Redis si se quiere caché distribuida (`Cache:Provider`), opcional al inicio.
- [ ] OpenTelemetry: apuntar el exporter OTLP al collector productivo.
- [ ] Backups programados (BackupWorker) + prueba de restauración según `Sprint20-Disaster-Recovery.md`.

## 4. Base de datos

- [ ] Generar script SQL de migraciones (`dotnet ef migrations script`) y revisarlo — **no** aplicar `database update` a ciegas en producción.
- [ ] Primer arranque siembra catálogos MH, módulos (17), planes (7) y permisos vía `DatabaseSeeder` (idempotente).
- [ ] `EmpresaPrueba:Enabled=false` en producción.

## 5. Verificación post-despliegue (en orden)

1. Los tres procesos arrancan **sin** disparar el guard de Mocks (si truena, lee el mensaje: lista los providers pendientes).
2. `/health` de la Api responde; `/openapi/v1.json` accesible.
3. Correo real de prueba desde `/Hardening` → "Probar correo".
4. Login superadmin + crear empresa real + asignar plan.
5. Configuración DTE de la empresa: certificado cargado, **ambiente PRUEBAS de MH**, emitir FCF de prueba end-to-end (borrador→generar→validar→firmar→enviar→PROCESADO) con la matriz de `Sprint11-Matriz-Pruebas-DTE.md`.
6. Cambiar a ambiente PRODUCCIÓN de MH solo cuando la certificación del cliente esté aprobada.
7. Webhook de billing de prueba (Stripe CLI / sandbox de la pasarela).
8. ZAP baseline (workflow existente) contra el dominio productivo.

## 6. Go-to-market por vertical

| Vertical | Estado | Paquete sugerido | Acción al alta |
|---|---|---|---|
| **Tiendas** | Listo para vender | Pyme+ (POS) | Plantilla TIENDA en onboarding |
| **Ferretería** | Listo para vender | Pyme/Pro | Plantilla FERRETERIA + escalas en `/Productos/Precios` |
| **Farmacia** | Piloto 2-4 semanas | Pro+ (INVENTARIO) | Plantilla FARMACIA + marcar "controla lote" en productos con vencimiento |
| **Salón** | Piloto 2-4 semanas | Business Full (NEOAGENDA) | Plantilla SALON + % comisión en empleados |

Durante pilotos: revisar semanalmente alertas generadas, fricción de UI en agenda/lotes, y el resumen simple de NeoProfit con el dueño (es el gancho de retención).

## 7. Pendientes conocidos que NO bloquean la salida

- Split por responsabilidades de `DteDocumentosService` (hoy partial classes; siguiente paso: servicios separados).
- ~11 servicios de Infrastructure aún sin tests directos (los administrativos ya están cubiertos).
- Verificación NIT online (sin API pública de MH).
- App móvil: publicar en stores cuando FCM esté con credenciales reales.
