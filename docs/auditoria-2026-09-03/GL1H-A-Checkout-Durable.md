# GL1H-A — base durable de checkout

Fecha local: 2026-09-04. Rama: `codex/gl0a-auth-security`, base `a8c5d16` más cambios locales.

La solicitud «vamos con el plan» autoriza implementar el siguiente bloque. Este incremento ejecuta
la base H-01/H-02, el adaptador H-03 y los contratos iniciales de H-07. **No cierra GL1H completo ni habilita una pasarela.**
El usuario eligió **Wompi El Salvador** durante la ejecución. Se incorpora el adaptador H-03 para
pruebas con transporte HTTP simulado, sin credenciales ni efectos externos. Siguiente incremento:
**GL1H-B — H-04/H-05, webhook Wompi verificado y aplicación atómica**, seguido de conciliación y outbox.

## Comportamiento implementado

- `BillingCheckoutIntent` conserva correlación, tenant, plan/código/nombre/precio/moneda/período,
  proveedor/cuenta/beneficiario, mapping, referencias locales opcionales, URLs y hash idempotente.
- Precio y moneda provienen del plan y deben coincidir con mapping activo. Se rechazan cero, mapping
  ausente/inconsistente, proveedores desconocidos/Mock y fallback al método antiguo sin importe.
- El registro `PROCESSING` con lease se confirma bajo el bloqueo SQL de empresa **antes** del proveedor.
  La llamada externa sucede fuera de la transacción. Solo la reserva nueva autoriza esa llamada.
- Misma clave/payload recupera la operación; otro payload produce `IDEMPOTENCY_CONFLICT`. El fingerprint
  contiene intención del cliente (plan y método), no URLs/defaults del servidor que pueden cambiar.
- ACK de sesión se guarda mediante actualización condicional y unicidad proveedor/cuenta/recurso.
  Solo entonces se devuelve el enlace. `AWAITING_PAYMENT` no crea pago ni concede licencia.
- Timeout, cancelación o ACK inválido/fallo de persistencia conservan referencia y conciliación;
  no se repite el POST. Si SQL no permite guardar cuarentena, queda `PROCESSING` durable; al vencer
  su lease, la consulta lo muestra como conciliación sin escribir ni retransmitir.
- El vencimiento local de enlace no libera el bloqueo de una operación que pudo cobrarse. La consulta
  no cierra ni reactiva operaciones; hace falta conciliación remota en H-06.
- Trial, portal, cambio de plan, cancelación y creación/verificación de transferencia observan el
  checkout abierto bajo el mismo lock de empresa. GL1G conserva su entidad y procesador de cancelación.
- No se crean clientes remotos por la ruta legacy antes de la intención ni se reutiliza un ID de
  cliente sin alcance de cuenta. Wompi crea el enlace directamente; otros proveedores deberán resolver ese contrato de forma durable.

## API y Web

| Operación | Contrato |
| --- | --- |
| POST `/api/billing/empresas/{empresaId}/checkouts` | Sesión autenticada; administrador de empresa o plataforma validado en BD; cuerpo `{ planId, metodo }`; un `Idempotency-Key` estable de 8–128 caracteres ASCII alfanuméricos o `-_.:` |
| GET `/api/billing/empresas/{empresaId}/checkouts/{correlationId}` | Lectura autenticada y acotada a empresa; devuelve importe, moneda, estado, referencia y enlace solo cuando procede |
| Web POST `/billing/checkout/session` | Antiforgery, permiso, clave estable en formulario; conduce a la página de estado incluso ante resultado incierto |
| Web GET `/billing/checkouts/{correlationId}` | Estado y referencia, actualizar consulta, continuar al mismo enlace si está vigente; no deduce pago del redirect |

Errores conservan `code`, `traceId` y correlación disponible en `data`. HTTP: 403 permiso, 404 recurso,
400 clave/URL, 409 conflicto/pendiente/conciliación, 422 mapping y 503 capability no habilitada.

`IBillingCheckoutProvider` es opt-in separado de `IPaymentProvider`. Wompi implementa el contrato nuevo;
los restantes adaptadores no se exponen por esta ruta. Transferencia conserva su flujo manual separado.
El adaptador Wompi rechaza modo productivo explícitamente hasta cerrar el ciclo H-04/H-05/H-06.

Opciones nuevas de servidor bajo `Billing:Checkout`: `Enabled` (false por defecto), `Provider`,
`ProviderAccountId`, `BeneficiaryId`, `SuccessUrl`, `CancelUrl` y `LeaseSeconds`. Cuenta/beneficiario son
identificadores, no secretos. URLs HTTPS explícitas, sin credenciales embebidas; no se obtienen del Host
enviado por el cliente. No se cambió configuración real ni se habilitó el Worker.

## Persistencia y límites

Migración offline: `20260905034928_GL1H_CheckoutIntentFoundation`. Añade una sola tabla,
FK restrictivas, rowversion, unicidad por empresa/clave y por proveedor/cuenta/checkout, y checks de
importe positivo/ACK coherente. Su `Down` elimina intenciones: no usarlo tras efectos reales sin
procedimiento de recuperación que preserve identidades. No se aplicó a la base activa.

Las referencias a suscripción/licencia son snapshots iniciales opcionales. No existe aún captura,
aplicación atómica de pago/período/licencia, reembolso, cierre administrativo ni outbox del ciclo nuevo.
Los webhooks legacy no constituyen la aceptación H-04 y no deben conectarse a este agregado como si
ya lo verificaran. Gate comercial cerrado hasta el cierre integral del proveedor elegido.

## Validación y continuidad

Se conserva la línea base previa en `tmp/gl1h/baseline-files.csv`. Evidencia nueva en `tmp/gl1h/`;
los resultados finales están archivados en [el resumen de evidencia](evidencia/gl1h-a-2026-09-04/resumen-evidencia.json).

| Verificación | Resultado | Límite |
| --- | --- | --- |
| Build Release solución | 0 errores / 0 advertencias | Árbol local; no publicación |
| Suite completa | 1,723 unitarias + 9 integración; 0 fallos/omitidas | Los 200 casos focales Billing son subconjunto, no se suman |
| Incremento de pruebas | 107 casos adicionales: 53 intención, 8 HTTP, 43 Wompi, 3 transporte DI | Las 3 pruebas legacy se adaptaron al rechazo de la ruta antigua; no cuentan como casos nuevos |
| SQL concurrente | 28/28; cadena real de 87 migraciones | LocalDB sintético; proveedor falso; no Wompi real ni SQL cliente |
| Integridad de schema | Sin cambios pendientes desde la migración | Fábrica offline impide abrir conexión |
| Razor | 5 vistas/estados renderizados y 11 comprobaciones de HTML | Vistas reales, contenedor sintético; sin E2E de navegador/AppShell/Android |
| Revisión independiente | Hallazgos aplicables H-03 corregidos y revisados por revisor_backend | No acredita webhook, captura, licencia ni sandbox |

La base temporal SQL se eliminó tras las comprobaciones y la instancia dedicada volvió a `Stopped`.
Los hashes del servicio de checkout, migración y arnés SQL coinciden con los probados; los cambios
posteriores afectaron adaptador HTTP, pruebas, vista/herramienta y documentación, no ese código SQL.
Los TRX y salidas seleccionadas se conservan en la carpeta de evidencia (salidas `.txt` para que no
sean excluidas por `*.log`). Los hashes del código entregado se registran en `archivos-entrega.csv`.

Revisión: se corrigieron el fingerprint sensible a configuración posterior, el retry HTTP de POST,
la igualdad indebida de IDs OAuth/aplicativo y el límite de respuesta aplicado después del buffering.
Se conserva documentada la restricción de callbacks al mismo origen. Ver [contrato Wompi y siguiente
incremento](GL1H-Wompi-Contrato-Continuidad.md).

**Aceptación del incremento:** H-01/H-02 base comprobada; H-03 código comprobado con transporte
simulado, recuperación y sandbox pendientes; H-07 parcial; H-08 parcial. **GL1H completo sigue abierto.**
No hay commit/candidato publicado: los cambios previos GL0A–GL1G y este incremento permanecen locales.

Pendientes ordenados:

1. Pasarela elegida: Wompi El Salvador. Pendiente asignar cuenta/aplicativo SaaS y ventana de pruebas.
2. H-03: validar el adaptador implementado en sandbox autorizado; recuperación remota depende de H-06.
   La clave local no representa una garantía de deduplicación del proveedor.
3. H-04/H-05: firma/cuerpo original/inbox/dedupe, consulta del recurso y aplicación transaccional
   de captura/período/licencia/módulos, coordinada con GL1G. Aprobación no equivale a captura.
4. H-06: conciliación administrativa, permisos/auditoría/outbox y recuperación de estados abiertos.
5. H-08: ampliar SQL/HTTP y sandbox del proveedor elegido; revisión independiente del candidato.
6. REL-01/02: integrar cambios revisados en commits reproducibles y archivar evidencia completa;
   después ejecutar las puertas de operación/certificación del plan.

El inventario previo y la migración offline no equivalen a release publicada, restore objetivo,
servicios instalados, emisión MH ni cobro. Producción global continúa **NO-GO**.
