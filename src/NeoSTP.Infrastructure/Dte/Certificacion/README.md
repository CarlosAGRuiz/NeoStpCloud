# CERT-1 — fundamento de cuota de campaña

Estado: estructura y staging interno. No es una campaña activada ni autorización para transmitir.

- Tres tablas nuevas: campaña, presupuesto por tipo y consumo único por documento/clave.
- No se registran servicios en DI ni existen rutas, jobs, seed de campaña, flags de bypass o emisores nuevos.
- `CertificationCampaignQuotaService.StageConsumptionAsync` recibe contexto de certificación separado del DTO fiscal existente. La identidad persistida, PRUEBAS, tipos 01/03/11/14, vigencia UTC y presupuestos positivos son obligatorios.
- Conserva validación de licencia vigente, snapshot de derechos, CORE/NEODTE y asignación operativa de módulos.
- En SQL Server exige una transacción **Serializable** del caller y obtiene el mismo applock `NeoSTP:DTE-LIMIT:{EmpresaId}`. Otros aislamientos se rechazan antes de consultar: un snapshot anterior al lock no garantiza presupuesto fresco.
- El documento debe ser nuevo, BORRADOR, sin JSON previo ni navegaciones de empresa/cliente/producto/documento relacionado. Las referencias se proporcionan por FK y se comprueba su pertenencia al tenant. Sólo se agregan detalles nuevos del agregado.
- No hace SaveChanges ni Commit. Al tener éxito prepara conjuntamente documento y consumo; ambos deben confirmarse en la transacción del caller. Los consumos pendientes en el mismo tracker también cuentan. Ante fallo no agrega otro documento.
- Idempotencia separada: scope corto CERT, hash de campaña+clave y fingerprint del hash fiscal+tipo+referencia de escenario. El DTO/fingerprint ordinario no cambia. Un replay puede recuperar el consumo después del vencimiento: no permite firmar ni transmitir.
- El contador mensual excluye exclusivamente consumos persistidos coherentes con el DTE, empresa/NIT, tipo, PRUEBAS, hashes CERT, fecha de creación y presupuesto finito. Los documentos ordinarios PRUEBAS y los históricos sin ledger siguen contando. La revocación posterior no reclasifica un consumo histórico válido como comercial.
- `CertificationCampaignAccess` bloquea generación, validación, firma y claims de envío individual/lote cuando el consumo no es coherente, la campaña no está activa/vigente o faltan licencia/módulos/configuración fiscal válidos. Se revalida después de autenticar y antes de reservar el envío. Consultas y replay no conceden permiso para enviar.

## Integración posterior necesaria

1. Entrada interna de operador con identidad/permiso comprobado y matriz oficial conciliada; nunca un booleano libre en el request público.
2. Reutilizar el execution strategy y la transacción de CreateBorradorAsync. Resolver replay antes de consumir correlativo; construir el agregado mediante el pipeline fiscal, stage del consumo y un único SaveChanges/Commit. No crear ni enviar desde este fundamento.
3. Verificar operativamente los gates implementados de caducidad/revocación en claims individuales y lotes. Los envíos ya iniciados se concilian; no se reenvían automáticamente.
4. Alinear métricas con la exclusión comercial estricta implementada. Mantener STARTERFE, precios, licencias y snapshots sin cambios. No clasificar automáticamente documentos históricos.
5. Provisionar campaña finita y presupuestos por tipo desde requisitos actuales; no derivar escenarios o autorización del número histórico 279.

## Comprobaciones pendientes

Los tests InMemory verifican reglas, staging y replay; no prueban atomicidad SQL, constraints ni carreras. Es obligatoria una copia SQL aislada para ensayar 79→90, conservar datos por empresa y comprobar último cupo entre procesos, clave simultánea, rollback y commit incierto. No se ejecutó ese ensayo ni se aplicó la migración al entorno activo.

La migración se genera y su SQL se revisa mediante `tools/SchemaDesign`, cuya fábrica bloquea conexiones. El candidato de 89 migraciones no contiene este fundamento; el runtime de 79 tampoco es apto para ejecutarlo. Migración y binarios coherentes deben desplegarse conjuntamente después de los gates operativos.
