# NeoSTP v1 — hardening RC9

Este documento es el runbook operativo del sprint RC9. No sustituye el respaldo,
la evidencia de staging ni la aprobación go/no-go.

## Alcance entregado

- Certificados fiscales protegidos con ASP.NET Core Data Protection y propósito por empresa.
- Validación local de PKCS#12 y XML Hacienda antes de persistir.
- Conversión explícita, idempotente y con bloqueo SQL de certificados legacy activos e históricos.
- Firma de DTE y eventos con descifrado sólo en memoria y borrado del buffer al finalizar.
- Resolución central de tenant para endpoints API de dashboard/datos.
- Catálogos DTE de soporte cargados para la empresa seleccionada.
- Calendario fiscal/comercial basado en El Salvador; timestamps técnicos siguen en UTC.
- MFA obligatorio para SUPERADMIN y ADMIN de empresa.
- Auditoría de mutaciones DTE confirmada en la misma transacción de base de datos.
- Worker con doble compuerta: bandera maestra y bandera individual por job.

## Conversión de certificados en producción

La conversión no llama a Hacienda, no firma ni emite documentos. Aun así modifica
material fiscal sensible y exige ventana de mantenimiento.

1. Detener Web, API y Worker, dejando cerradas las escrituras.
2. Crear backup completo de SQL Server y copia separada del key ring de Data Protection.
3. Verificar que ambos respaldos son legibles y registrar sus hashes.
4. Publicar RC9 con `Dte:CertificateProtection:AllowLegacyPlaintext=true` sólo durante esta fase.
5. Ejecutar desde la carpeta publicada de API:

   ```powershell
   .\NeoSTP.Api.exe --inspect-dte-certificates
   ```

6. Registrar los conteos `ActiveTotal`, `HistoryTotal`, `Protected` y `Legacy`.
7. Ejecutar la conversión una sola vez:

   ```powershell
   .\NeoSTP.Api.exe --migrate-dte-certificates
   .\NeoSTP.Api.exe --verify-dte-certificates
   ```

8. Exigir `Legacy=0` y `Protected=ActiveTotal+HistoryTotal`.
9. Cambiar inmediatamente `AllowLegacyPlaintext=false` en la configuración externa.
10. Reiniciar API/Web, probar login, configuración DTE y una firma local controlada en STAGING.
11. Crear un nuevo backup cifrado y ejecutar una restauración aislada con copia del key ring.

### Rollback

Después de convertir los blobs no se puede volver a RC8: RC8 interpreta el envelope
como si fuera el certificado original. El rollback válido es RC9 en modo compatible,
restaurar SQL + key ring del backup previo, o corregir hacia delante. Nunca restaurar
la base sin el key ring correspondiente.

## Activación gradual del Worker

En STAGING y PRODUCTION `Worker:Enabled=true` no activa ningún job por sí solo.
Cada job requiere su propia bandera:

| Orden sugerido | Bandera | Prerrequisito |
|---:|---|---|
| 1 | `Worker:LimpiezaTokens:Enabled` | Backup y consulta de retención |
| 2 | `Worker:NotificationOutbox:Enabled` | Proveedor Push configurado o deshabilitado conscientemente |
| 3 | `Worker:WebhookDelivery:Enabled` | Destinos HTTPS probados |
| 4 | `Worker:GeneracionAlertas:Enabled` | Outbox activo |
| 5 | `Worker:RetransmisionContingencia:Enabled` | Sólo después de smoke MH PRUEBAS |
| 6 | `Worker:ContingenciaLote:Enabled` | Sólo después de smoke MH PRUEBAS |
| 7 | `Worker:RecordatoriosCobro:Enabled` | Email/WhatsApp real validado |
| 8 | `Worker:BillingProviderOperations:Enabled` | Proveedor de cobros real validado |
| 9 | `Worker:LimpiezaAuditoria:Enabled` | Retención aprobada y backup |
| 10 | `Worker:BackgroundTasks:Enabled` | Capacidad y observabilidad |
| 11 | `Worker:Backup:Enabled` | También `Hardening:Backup:WorkerEnabled=true` |

Activar un job, observar al menos dos ciclos, revisar errores/duplicados/latencia y
recién entonces continuar con el siguiente.

## Compuertas RC9

- Build Release: cero errores y cero advertencias.
- Unit + Integration: todas las pruebas no dependientes de SQL en verde.
- Gates SQL Server reales en verde.
- `git diff --check` y escaneo de secretos en verde.
- STAGING: smoke por roles, MFA, filtros mensuales, configuración DTE, firma local y MH PRUEBAS.
- Backup y restore aislado con key ring verificados.
- PR con checks obligatorios y revisión aprobada.
- Producción: despliegue primero con Worker completamente apagado.

Los checks de GitHub bloqueados por facturación no se consideran aprobados: deben
restaurarse o ejecutarse manualmente con evidencia antes del merge.
