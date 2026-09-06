# CLI-23 — conciliación comercial preparada, sin aplicación

Lectura SELECT del 05/09/2026 a las 23:40:27 UTC. Empresa 23, identidad NIT verificada; SQL Server local 16, esquema 91. El selector de arranque apunta a `20260905T232433Z-9062dc25354442429d2ef07f76f24d94`. Esta lectura no verifica variables/CLI de los procesos y no cambia configuración, binarios ni la certificación en curso.

Evidencia: [preview saneado](../../tmp/client-commercial-2026-09-05/preview-20260905T234027Z-9659d57d395e4129933287bae00bc871.json). Herramienta reproducible: [Preview-Client23Commercial.ps1](../../tools/ClientCommercial/Preview-Client23Commercial.ps1), sólo SELECT, sin modo de escritura. Parser PowerShell: cero errores. No se compiló producto.

## Estado comprobado

| Registro | Actual |
|---|---|
| Empresa 23 | ACTIVA, PRUEBAS |
| Licencia 29 | ACTIVO; inicio 20/08/2026 16:55:48.0897544; fin 20/09/2026 16:55:48.0897544 |
| Plan 207 | STARTERFE, USD 15; límites 3 usuarios / 1 sucursal / 2 PV / 100 DTE |
| Suscripción 2 / BillingCustomer 2 | Mock, TRIALING; trial 20/08→03/09; período actual sin fechas; sin cancelación |
| Identidad externa | Customer tiene identificador almacenado; suscripción remota ausente. Sólo se consultó presencia, no su valor |
| Mensualidades / pagos | Cero Billing_Invoices y cero Billing_Payments de esta empresa |
| Adopción de pago | Cero Billing_PaymentApplications y cero Billing_CheckoutIntents |
| Conciliación anterior CLI23 | Cero marcadores de auditoría CLI23 |

El pago de implementación está confirmado por el usuario, pero no se conocen importe, fecha y referencia: se conserva como confirmación del acuerdo, sin fabricar un pago contable. Septiembre permanece sin pagar.

## Ajuste propuesto

Implementar después del cierre de la certificación congelada, conservando plan y proveedor actuales:

1. Suscripción 2: pasar de TRIALING a ACTIVE; conservar TrialStart/TrialEnd y CreatedAt. No crear otro trial ni suscripción remota. El registro Mock se conserva como integración heredada, sin usarlo para confirmar dinero.
2. Registrar acuerdo administrativo por empresa y suscripción: precio fijo USD 15, mes calendario `America/El_Salvador`, cobro vencido al último día local, primer mes exigible septiembre de 2026, sin agosto/prorrateo/recargos/suspensión automática. Separar esta política de las fechas que conceden acceso.
3. Período de septiembre: `[2026-09-01 06:00Z, 2026-10-01 06:00Z)`. Vencimiento comercial: fecha local 30/09/2026. Octubre y noviembre vencen 31/10 y 30/11. Guardar fecha local de vencimiento y frontera UTC del período como conceptos separados; no mostrar el 01/10 UTC como vencimiento local.
4. Crear una única mensualidad OPEN por USD 15, PaidAt nulo, asociada al acuerdo y mes 2026-09. No crear BillingPayment, ni poner PAID/SUCCEEDED, ni completar una aplicación de pago Wompi. El cobro de implementación tendrá registro separado cuando estén sus datos.
5. Continuidad: retirar el corte heredado del 20/09. Propuesta para el acceso administrativo mientras no se acuerde suspensión: licencia 29 ACTIVO, FechaInicio preservada y FechaFin nulo, con auditoría del motivo. Esto mantiene límites y módulos; no declara mensualidades pagadas ni cambia el precio. Usar 30/09 como FechaFin introduciría una suspensión no acordada. La baja administrativa explícita debe seguir operando.

La política anterior debe estar implementada antes de presentar el ciclo como completo. No se ejecutó este ajuste ni se generó un Apply que aparentaría completar el calendario mediante fechas sueltas.

## Qué permite y qué falta en esquema 91

`BillingInvoice` ya tiene Amount, Currency, Status, InvoiceDate, DueDate y PaidAt: puede representar una factura pendiente individual. No tiene período del servicio, referencia a acuerdo, zona horaria ni clave mensual única. `ExternalInvoiceId` corresponde al proveedor externo; no se reutiliza para inventar una identidad remota.

`BillingSubscription.CurrentPeriodStart/End` permite describir un período individual, pero no define el ancla mensual. El flujo manual `ConfirmarTransferenciaCoreAsync` fija fin en `DateTime.UtcNow.AddMonths(1)` y vuelve a activar una licencia finita; el procesador de pagos también usa AddMonths(1). Por eso pagar el 05/10 desplazaría el calendario. Corregir sólo datos dejaría esa regresión disponible.

Cambio mínimo posterior de producto/esquema:

- Acuerdo de facturación manual ligado a empresa/suscripción, con snapshot de plan/precio, período inicial, mes calendario, zona horaria y suspensión automática deshabilitada. No inventar un proveedor nuevo: es modalidad administrativa, separada del campo Provider.
- Mensualidad ligada al acuerdo, con `PeriodStartLocal`, `PeriodEndExclusiveLocal`, `DueLocalDate`, FK a BillingInvoice y unicidad `(AgreementId, PeriodStartLocal)`.
- Aplicación manual de un pago real a la mensualidad, con referencia verificable/idempotencia y sin renovación por aniversario. La implementación se clasifica separadamente y no salda septiembre.
- Los comandos de transferencia/checkout/cambio de plan deben reconocer el acuerdo: o aplican la política correcta o bloquean con un error concreto; nunca retornan al AddMonths actual ni adoptan Wompi de forma implícita.
- UI: diferenciar próxima fecha de cobro, período del servicio, saldo y acceso. Hoy Billing/Index muestra CurrentPeriodEnd como “Próxima renovación” sin conversión explícita a la fecha de cobro local.

No hace falta modificar catálogo global ni ampliar cupos para esto. Una futura migración requiere su propio ensayo; el diagnóstico previo que hablaba de base 79 queda superado por esta lectura de 91.

## Contrato de la aplicación posterior

- Preview por defecto y Apply explícito, ambos restringidos a empresa23/NIT y a los IDs observados; reconfirmar que siguen existiendo una sola licencia y suscripción elegibles.
- Transacción Serializable con `NeoSTP:BILLING:23`, lectura del estado dentro del lock y comparación con huella aprobada. Auditar antes/después, identidad de operador, motivo y mensualidad creada en la misma transacción.
- Si cambian precio/plan, fechas, estado, pagos, adopción Wompi, checkout u operación de proveedor pendiente: rechazar por drift; no sobreescribir.
- Clave idempotente por conciliación y por mes; replay devuelve el mismo acuerdo/mensualidad y no vuelve a sumar dinero. Auditoría no sustituye el índice único mensual.
- La verificación posterior debe demostrar septiembre OPEN/PaidAt nulo, cero nuevos pagos, historial trial preservado, sin cambios en módulos/cupos/global/NEO2/fiscal/campañas. Registrar hashes de evidencia saneada.

## Pruebas necesarias después del congelamiento

1. SQL aislado: dos aplicaciones concurrentes crean un acuerdo y una mensualidad; fallo antes de commit revierte todo; replay no cambia importes.
2. Septiembre/octubre/noviembre y febrero normal/bisiesto: vencimiento último día local, frontera UTC correcta; pago temprano o tardío no mueve el ancla.
3. Implementación pagada no liquida septiembre; pago duplicado no se aplica dos veces; no nace deuda de agosto ni recargo.
4. DTE y módulos siguen accesibles después del 20/09 y del vencimiento comercial cuando no se acordó suspensión; límites STARTERFE siguen aplicando. Una revocación administrativa explícita sí bloquea.
5. Empresa ajena/NEO2, acuerdo o licencia adoptada por ledger, cambio de plan y transferencia heredada no pueden saltarse los controles.
