# GL1H-C — aplicación atómica de pagos, H-05a

Fecha local: 2026-09-05. Estado: núcleo H-05a validado localmente con fixtures; H-05b/H-06 pendientes. Continúa [H-04](GL1H-B-Webhook-Wompi-Durable.md). Este incremento prepara el consumidor interno; no habilita Wompi ni procesa operaciones reales.

## Cambio implementado

`BillingPaymentApplicationProcessor.ApplyVerifiedPaymentAsync(receiptId)` lee la recepción y el checkout de la base; no acepta empresa, monto, período ni un indicador de verificación aportados por un llamador. Se resuelve en un scope propio porque administra su unidad de trabajo. No tiene endpoint ni job/caller público. `Billing:PaymentApplication:Enabled` es false por defecto.

Solo acepta el estado reservado `VERIFIED_CAPTURED_PRODUCTION`, identidad coherente y modo productivo en recepción e intención. H-04 sigue bloqueado para producción y **no puede generar ese estado**. Los casos productivos probados son registros sintéticos creados dentro de fixtures, no cobros ni certificación de la captura real. `VERIFIED_SANDBOX` nunca genera pago o licencia comercial.

El ledger `Billing_PaymentApplications` tiene unicidad por checkout, recepción y pago. Guarda las identidades resultantes, el período aplicado y el snapshot comercial. Bajo el mismo lock de empresa de Billing, una transacción serializable confirma juntos pago, suscripción, licencia, módulos, ledger y cierre de intención. No hay llamadas de red ni email dentro de la transacción.

Un replay devuelve éxito sin repetir efectos, incluso si la suscripción fue cancelada después. Otra transacción para el mismo checkout devuelve `BILLING_ADDITIONAL_PAYMENT_RECONCILIATION`: no suma otro período. Un fallo de persistencia/commit devuelve `BILLING_APPLICATION_RETRY_REQUIRED` y la siguiente ejecución consulta el ledger antes de hacer cambios.

## Alcance comercial acotado

- Primera alta: sin suscripciones/licencias anteriores incompatibles; crea identidades locales sin inventar una suscripción recurrente remota.
- Renovación del mismo plan: mes calendario desde el mayor de fin del período previo y fecha confirmada del pago. Se conserva tiempo ya pagado; no se calcula desde la hora de reintento.
- Conversión de trial Wompi del mismo plan: comienza el mes en la fecha confirmada del pago. No cambia de proveedor ni reactiva trials vencidos.
- Cancelación pendiente, estados terminales, suspensión, cambio de plan, suscripción remota recurrente, historia ambigua o módulos adicionales activos: bloqueados.
- Las identidades anteriores al checkout permanecen en la intención; las nuevas se consultan en el ledger.

El checkout captura `CommercialSnapshotJson` versión 1 antes del POST. Incluye plan/precio/moneda/cupos/módulos, estado de clientes/suscripciones/licencias y módulos de empresa, sin contactos ni credenciales. Cualquier divergencia antes de aplicar el pago se rechaza. Las intenciones antiguas sin snapshot no se reconstruyen usando el catálogo actual.

## Límites que siguen abiertos

**H-05 no está cerrado por completo.** H-05b debe resolver versiones de cuotas y origen de módulos/addons: el guard de licencias todavía consulta el Plan vivo. Guardar un snapshot histórico no cambia esa lectura ni congela por sí mismo la política futura del catálogo.

H-06 debe consumir las capturas aceptadas, persistir estados/razones de conciliación visibles y el outbox. En este incremento los rechazos comerciales se devuelven como resultado interno; no se libera el checkout ni se borra la recepción. Los avisos adicionales quedan conservados y no aplicados. Antes de abrir checkout productivo, las transiciones no soportadas deben rechazarse también en el preflight previo al POST y la política debe quedar aceptada para las rutas expuestas.

El verificador productivo debe confirmar contractualmente la captura, no promover una aprobación o redirect. Sigue pendiente cuenta/aplicativo, callback HTTPS, sandbox real, estados rechazado/expirado/devolución y aceptación del mismo candidato. No cambiar IsProduction ni habilitar el procesador para saltar estas puertas.

## Validación y revisión

- Build Release final: 0 advertencias y 0 errores.
- Suite completa: **1,870 unitarias + 9 integración**, sin fallos ni omitidas. Son 44 unitarias más que H-04: 43 nuevas de aplicación y una variante adicional de replay Completed.
- Focal Billing: **347** casos aprobados, incluidos en la suite; no se suman de nuevo.
- SQL aislado: **28/28** comprobaciones, **89 migraciones**. Base `PaymentApplicationAudit_2d9b1480e60749d7b5cf07c0d5111777` eliminada y misma instancia dedicada detenida antes/después.
- El ensayo SQL validó primera ejecución completa: concurrencia, unicidad, rollback y cancelación; no utilizó proveedor real ni configuración activa.
- EF offline: modelo coincidente con la migración final.
- [Evidencia, TRX, logs, hashes y manifiesto](evidencia/gl1h-c-2026-09-05/README.md). Árbol local sobre HEAD `a8c5d163fb372359c4738f0c3814ef1ee14024eb`; sin commit ni despliegue. Los cortes A/B quedan preservados como históricos.

Migración offline `20260905150044_GL1H_AtomicPaymentApplication`: snapshot nullable en checkout (legacy seguro), ledger con FKs restrict e índices únicos, check del estado reservado de captura productiva. No aplicada a la base activa.

Roles: root implementó núcleo/modelo/EF/DI; `pruebas_backend` escribió pruebas separadas; `sql_evidencia` implementó y ejecutó arnés relacional aislado; `revisor_backend` revisó el núcleo independientemente. El primer focal encontró pérdida del Add de la intención durante la edición: se corrigió y se repitió la validación. Se conserva el intento fallido como diagnóstico, no como evidencia de aceptación.

## Próximo incremento para acercar salida en vivo

1. H-05b: aplicar o bloquear explícitamente cuotas/versiones, planes y módulos efectivos; preparar preflight de las transiciones expuestas.
2. H-06: conciliación con permiso y empresa explícitos, historial, consulta del proveedor sin nuevo POST y outbox durable.
3. H-07/H-08: cuenta/aplicativo, URLs, contratos finales y ensayo real de Wompi en pruebas; luego verificar captura productiva y sus gates.
4. REL/OPS/QA: separar candidato reproducible, restauración aislada, hosting/TLS/key ring y E2E del mismo artefacto. Conservar puertas fiscales NEO/cliente independientes.

[Plan maestro vigente](Plan-Continuidad-Auditado-2026-09-04.md). Producción global NO-GO; n1co continúa sin integrar.