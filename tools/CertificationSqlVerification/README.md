# Verificación SQL de la reserva de certificación

Ejecutar desde la raíz del repositorio, con el turno de compilación disponible:

```powershell
dotnet run --project tools/CertificationSqlVerification/CertificationSqlVerification.csproj --configuration Release
```

La herramienta lee la conexión local de API en memoria, comprueba servidor local y catálogo esperado, y cambia el catálogo a `master` **antes de abrir una conexión**. Exige SQL Server 16 en este mismo equipo Windows. Crea una base `NeoCertificationAudit_<GUID>`; no abre ni consulta `NeoSTP_Cloud`.

`EnsureCreated` construye el esquema del modelo actual, incluidos los catálogos estáticos `HasData` definidos por el modelo. No contiene empresas ni campañas. Se agregan dos empresas ficticias, una licencia y campañas sintéticas. No se ejecutan migraciones, DatabaseSeeder, hosts, firma, correo ni clientes Hacienda. Una campaña sintética activa no habilita ninguna campaña de un cliente real.

Las comprobaciones llaman al servicio interno real `CertificationCampaignQuotaService` con conexiones y transacciones SQL independientes, aislamiento Serializable y bloqueo de aplicación real. Incluyen doce solicitudes por el último cupo, doce repeticiones de una clave, conflicto de contenido, rollback, presupuesto por tipo, caducidad, revocación, tenant, NIT, ambiente y licencia. La herramienta guarda borrador y consumo en la transacción del llamador. No prueba aún la integración del servicio con las rutas productivas de creación/envío.

La limpieza exige el mismo nombre generado y el mismo DB_ID capturado al crear la base. Solo elimina esa base y verifica su ausencia. Un fallo de limpieza marca el resultado como no aprobado; no se buscan ni eliminan bases de ejecuciones anteriores.

La evidencia JSON se escribe bajo `tmp/certification-sql/<GUID>/results.json`, con nombres de comprobación y huellas de fuentes. No contiene conexión, credenciales, clientes reales ni documentos fiscales. Estas comprobaciones no representan casos oficiales procesados por Hacienda y no reducen los 279 pendientes de la captura.
