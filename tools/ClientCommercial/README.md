# CLI23: acuerdo mensual administrativo

Empresa23, plan207 STARTERFE USD15/mes. La primera mensualidad es septiembre2026, vencimiento local30/09. El acuerdo conserva trial histórico, activa suscripción, quita corte heredado de licencia20/09 y habilita EVENTOSDTE/CONTINGENCIA por empresa. No crea pagos, no marca implementación como mensualidad pagada y no cambia configuración fiscal ni tipos01/03/11/14.

Requiere migración `20260906152921_CLI23_CalendarBillingAndCompanyModuleGrants` aplicada tras ensayo. No inicia hosts ni aplica migraciones.

## Conciliación

1. `dotnet build tools/ClientCommercial/ClientCommercial.csproj`
2. Preview: `dotnet run --no-build --project tools/ClientCommercial -- --settings-root <release/api>`.
3. Revisar Snapshot/Proposal y conservar Fingerprint.
4. Apply: añadir `--apply --expected-fingerprint <Fingerprint> --actor-id <id-superadmin-persistido>`.
5. Repetir preview. Esperar suscripción ACTIVE, licencia ACTIVO/FechaFin null, septiembre OPEN15USD/PaidAt null, cero pagos y ambos complementos autorizados. Replay valida integridad y no repone accesos revocados.

La herramienta solo permite SQL local NeoSTP_Cloud y verifica identidad fiscal, empresa23/NIT, licencia29, suscripción2, cliente billing2, plan207 y sus límites. El apply toma lock `NeoSTP:BILLING:23` en transacción Serializable, rechaza drift/preexistencia de pagos/checkout/adopción, y audita antes/después junto con la escritura.

Ensayo sobre clon: agregar `--allow-rehearsal --rehearsal-database NeoClientBilling92_<32-hex-minúsculas>` al preview y apply. La conexión usa el mismo servidor local/configuración y reemplaza solo el nombre de base con ese patrón estricto. El reporte indica base y modo ensayo. La herramienta no crea/restaura/borra el clon ni escribe en la base original durante el ensayo.

Después de aplicar CLI23 al clon, agregar `--verify-calendar-rehearsal --apply` junto con ambas opciones de ensayo. Ejecuta dos ciclos concurrentes para octubre, replay y fallo inyectado antes de commit para noviembre; comprueba unicidad, rollback, ningún pago creado y acceso conservado. Este comando modifica solo el clon y se rechaza en la base real.

## Generación de meses siguientes

Configurar `Billing:Calendar:Enabled=true` en un host persistente (preferentemente API). Genera mensualidades cada hora bajo el mismo lock. La clave única `(AgreementId,PeriodStartLocal)` impide duplicados incluso con varios hosts. Incluye meses pendientes desde septiembre; no emite agosto, recargos, cargos automáticos ni suspensiones. Cancelación/suspensión explícita detiene el ciclo y nunca se reactiva acceso automáticamente. Revisar logs y tabla Billing_CalendarPeriods al comienzo del siguiente mes.

## Aplicar un pago real recibido

Solo una vez que administración haya verificado importe, referencia y fecha bancaria:

`dotnet run --no-build --project tools/ClientCommercial -- --settings-root <release/api> --apply --actor-id <superadmin> --pay-period-id <mensualidad> --amount 15 --reference <referencia-verificada> --paid-at-utc <fecha-ISO8601-UTC>`

La mensualidad debe pertenecer a empresa23; importe y moneda deben coincidir. La referencia se conserva únicamente en datos de billing. Replay de mismo pago es idempotente; referencia usada u otro pago se rechaza. El pago no cambia ancla mensual ni acceso. Pago de implementación debe registrarse separadamente cuando se conozcan datos y nunca usar este comando para saldar la mensualidad sin dinero recibido.

Cambios de plan, checkout de pasarela, portal remoto y transferencia heredada se bloquean con `BILLING_CALENDAR_MANAGED` mientras el acuerdo permanezca activo. Migrar al proveedor real requiere una conciliación explícita posterior. Esta herramienta no es un endpoint público.
