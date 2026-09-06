# GL1G — Cierre de coordinación durable de cancelaciones Billing

Fecha: 2026-09-04. Rama `codex/gl0a-auth-security`, HEAD `a8c5d16` más cambios locales preservados.
**Alcance GL1G verificado localmente; PRODUCCIÓN NO-GO.** No es evidencia de despliegue, cobro real ni
habilitación de una pasarela.

## Resultado

La cancelación ya no llama al proveedor antes de tener una intención recuperable en SQL. El flujo
implementado es:

1. Autorizar usuario/empresa y validar una única suscripción/licencia compatible.
2. Confirmar en SQL `Billing_ProviderOperations` con empresa, suscripción, plan, licencia, proveedor,
   recurso externo, modo y clave idempotente estable.
3. Reclamar la intención mediante un lease. Solo un procesador puede ejecutar el efecto externo.
4. Enviar la clave al proveedor cuando su adaptador la soporta; Stripe la usa como idempotency key.
5. Persistir el ACK remoto antes de iniciar el cambio local.
6. Cancelar suscripción/licencia y completar la operación en una transacción serializable protegida
   por empresa.

Un timeout, excepción, respuesta fallida, lease vencido sin ACK o cambio en los snapshots no provoca
un reenvío automático. La operación pasa a `REQUIRES_RECONCILIATION`; suscripción y licencia locales
quedan intactas. Un lease vencido que sí contiene ACK durable puede terminar únicamente la fase local,
sin repetir la llamada al proveedor.

La cancelación programada conserva como máximo el fin ya pagado; la inmediata revoca el acceso sin
extenderlo. Una programada puede adelantarse a inmediata, pero una inmediata no se revive mediante una
solicitud programada posterior.

## Componentes

- Entidad y configuración EF `BillingProviderOperation`, con snapshot del plan, `rowversion`, claves
  foráneas restrictivas, índice único de idempotencia e índice de trabajo por estado/fechas.
- Migración `20260905002545_GL1G_BillingProviderOperations`, generada mediante la factoría offline que
  rechaza conexiones. No fue aplicada a ninguna base activa ni del cliente; sí se aplicó y eliminó en
  bases LocalDB sintéticas de auditoría mediante el arnés protegido.
- `BillingProviderOperationProcessor`, que separa intención, efecto externo, ACK y commit local.
- Sobrecarga idempotente de `IPaymentProvider`; implementación explícita para Stripe.
- `BillingProviderOperationWorker`, registrado y deshabilitado por defecto mediante
  `Worker:BillingProviderOperations:Enabled=false`.

## Defecto hallado durante la verificación

La primera ejecución SQL descubrió que el procesador abría la transacción local bajo
`SqlServerRetryingExecutionStrategy`. EF rechazó esa combinación después de que el proveedor ya podía
haber confirmado. La base sintética se eliminó en el bloque de limpieza.

Se corrigió encapsulando exclusivamente la fase local posterior al ACK en una estrategia de cero
reintentos. La llamada externa permanece fuera de esa transacción y no puede ser repetida por EF. La
segunda ejecución SQL aprobó todas las comprobaciones.

## Evidencia

- Compilación `Release` de la solución completa: **0 errores y 0 advertencias**, log nativo en
  `tmp/gl1g/final4-artifacts/gl1g-final4-build.log`.
- Unitarias completas: **1,616/1,616 aprobadas**, 0 omitidas, en
  `tmp/gl1g/final4-artifacts/unit/gl1g-final4-unit.trx`.
- Integración completa: **9/9 aprobadas**, 0 omitidas, en
  `tmp/gl1g/final4-artifacts/integration/gl1g-final4-integration.trx`.
- Total disjunto xUnit: **1,625 pruebas aprobadas**. Los filtros siguientes son subconjuntos y no se
  suman nuevamente.
- Casos focales GL1G: **26/26 aprobados** en
  `tmp/gl1g/final4-artifacts/focal/gl1g-final4-focal.trx`: intención previa, reuse, ACK, fallo y timeout,
  prevalidación de tenant/plan/licencia/proveedor/recurso, lease vivo/vencido, recuperación local,
  ACK sin lease, licencia creada después de la intención, bloqueo de mutaciones, exclusión de modos,
  idempotencia y cancelación programada/inmediata.
- Billing/SaaS: **93/93 aprobadas** en
  `tmp/gl1g/final4-artifacts/billing/gl1g-final4-billing.trx`.
- SQL Server LocalDB sintético: **63/63 comprobaciones aprobadas** sobre la cadena real de migraciones.
  Incluye dos procesadores, una sola llamada al proveedor, persistencia entre contextos, unicidad,
  correlación atómica, prevalidación tenant sin efecto externo, carrera determinista donde una
  cuarentena obsoleta no puede sobrescribir `ACK + COMPLETED`, carrera separada que conserva sólo el
  ACK mientras sigue `PROCESSING`, terminalidad de conciliación, handoff de `BillingService` observado
  desde otro contexto, y fallos inyectados al persistir el ACK y guardar la fase local. Evidencia
  sanitizada en `tmp/gl1g/final4-artifacts/sql/gl1g-final4-sql.json`; base aleatoria
  `BillingAudit_5a062fb016fc4545a5a5545364bcf1d7` eliminada antes de escribir el archivo.
- `dotnet ef migrations has-pending-model-changes` mediante `SchemaDesign --offline-schema`: modelo y
  última migración sincronizados, sin abrir conexión.
- Los procesos activos anteriores conservaron salud HTTP 200 en API `:5058/health` y Web
  `:5031/health/live`. Son binarios previos y no demuestran que GL1G esté desplegado.

Las pruebas usaron empresas, proveedor, IDs, correos y base sintéticos. No se consultó ni modificó la
base del cliente y no se ejecutó un cobro, cancelación o correo externo real.

La revisión cruzada detectó además que la correlación se comprobaba demasiado tarde, que una intención
no conservaba su plan y que una cuarentena basada en una lectura antigua podía sobrescribir un ACK o
`COMPLETED`. El procesador ahora valida empresa, plan, licencia, proveedor y recurso antes del efecto
externo; las mutaciones Billing quedan congeladas mientras haya cancelación abierta; y la cuarentena
usa un `UPDATE` condicional por estado, lease y ACK nulo. `COMPLETED` es terminal. También se endureció
el arranque de producción: si el Worker Billing está habilitado pero `Billing:Provider` falta o está en
`Mock`, el host se bloquea salvo override operacional explícito.

Una operación `PROCESSING` sin lease se concilia si no tiene ACK; si el ACK ya es durable, adquiere un
lease nuevo y termina sólo la fase local. Dos modalidades de cancelación no pueden permanecer abiertas
a la vez. Checkout y portal toman el mismo bloqueo por empresa durante la comprobación y creación de
sesión, aunque su ciclo posterior continúa fuera de alcance hasta GL1H.

## Límites y bloqueos abiertos

1. **P1 — checkout y correlación de cobro:** todavía no se persiste de extremo a extremo la relación
   empresa–plan–checkout–pago–suscripción–recurso externo para todos los proveedores.
2. **P1 — importe y moneda:** MercadoPago, Wompi y PayPal no están acreditados transportando el monto y
   moneda reales del plan; no habilitar cobros con valores incompletos o cero.
3. **P1 — webhooks:** falta autenticación y cobertura equivalente para cada proveedor, deduplicación y
   aplicación transaccional de pago, período y licencia. El ingreso endurecido de Stripe no certifica
   el ciclo completo.
4. **P1 — conciliación operativa:** falta consulta remota específica por proveedor y una superficie
   administrativa auditada para resolver `REQUIRES_RECONCILIATION`. El Worker permanece apagado hasta
   contar con este runbook y monitoreo.
5. **P1 — otras operaciones Billing:** checkout, cambio de plan, activación por webhook y notificaciones
   aún no usan de forma integral este mismo journal/outbox. El correo de cancelación sigue siendo
   best-effort después del commit.
6. Persisten las demás puertas fiscales, de despliegue, respaldo/rollback y prueba externa autorizada
   registradas en GL1F y en el plan de producción. DTE11 continúa cerrado y la APK pertenece a Manuel.

## Decisión

GL1G cierra localmente el defecto específico “proveedor canceló y la licencia local quedó activa” para
el flujo nuevo: existe intención durable, ACK separado y finalización atómica, sin reintento externo
ciego. No corrige ni acredita el ciclo completo de cobro.

Siguiente bloque recomendado: **GL1H — checkout y webhooks correlacionados**. Debe transportar el precio
y moneda reales, persistir el agregado empresa–plan–pago–suscripción–proveedor, autenticar/deduplicar
cada webhook y activar período/licencia de forma atómica. Hasta demostrarlo en sandbox y SQL,
**no habilitar pasarelas ni producción**.

No hubo despliegue, migración de base activa, limpieza/reseed, emisión fiscal, llamadas reales a
Hacienda/proveedores, reinicio de servicios, commit ni push.
