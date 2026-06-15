# Runbook HB-7 - Storage, Secretos y Retencion

> Fecha: 2026-06-15. Alcance: API/Web/Worker de NeoSTP Cloud, con foco en NeoScan,
> documentos fiscales, secretos por entorno y retencion para demo, staging y produccion.

## Objetivo

Dejar una guia operativa para correr NeoSTP Cloud sin secretos en el repositorio, con storage
auditable para documentos sensibles y con una politica de retencion clara antes de demos o
ambientes productivos.

## Alcance de Datos Sensibles

Datos tratados como sensibles:

- DTE, JSON sellado, PDF, eventos, libros fiscales y reportes contables.
- Archivos de NeoScan y DTE recibidos.
- Logos, firmas, certificados DTE y passwords de certificados.
- Passwords SMTP, `Jwt:Key`, API keys NeoConnect, secretos HMAC de webhooks.
- `Scan:Gemini:ApiKey`, credenciales FCM, tokens Meta WhatsApp y claves de pasarelas.
- DataProtection key ring, porque protege secretos cifrados en base de datos.

## Storage de NeoScan

Configuracion soportada:

| Provider | Configuracion | Uso recomendado |
|---|---|---|
| `Database` | `Scan:Storage:Provider=Database` | Default. El blob vive en SQL Server y se protege con el backup de BD. |
| `FileSystem` | `Scan:Storage:Provider=FileSystem` + `Scan:Storage:Root` | Archivos fuera de BD. Requiere ACL, cifrado de disco y backup del root. |

Reglas para `FileSystem`:

- Usar una ruta absoluta o UNC estable, fuera del repositorio y fuera de `bin/`.
- Dar permisos de lectura/escritura solo a la identidad de API/Web/Worker que lo necesite.
- Activar cifrado de volumen: BitLocker, LUKS o cifrado equivalente del proveedor cloud.
- Respaldar `Scan:Storage:Root` junto con la BD; las filas guardan claves relativas.
- Restaurar BD y root como una unidad logica; si falta el root, los metadatos quedan sin archivo.
- No registrar en logs rutas completas, nombres originales de archivos, certificados ni contenido OCR.

Guardrail implementado:

- `FileSystemScanBlobStorage.LeerAsync` solo acepta claves relativas generadas por la app.
- Rutas absolutas, traversal `..` y escapes fuera del root devuelven `null`.
- `/health/ready` valida escritura de `Scan:Storage:Root` cuando el provider es `FileSystem`.

## Backups

BD SQL Server:

- FULL diario.
- LOG cada 15 a 60 minutos segun RPO.
- Copia fuera del servidor bajo regla 3-2-1.
- Restore probado al menos antes de release mayor.

Storage externo:

- Si `Scan:Storage:Provider=FileSystem`, incluir `Scan:Storage:Root` en el plan de backup.
- Si `Hardening:Backup:StorageProvider=LOCAL`, usar una ruta fuera del repo y preferiblemente fuera
  del mismo volumen de la app.
- `Hardening:Backup:StorageProvider=AZURE_BLOB` o `S3` requiere credenciales del entorno, nunca en git.

## Secretos por Entorno

Checklist minimo:

- [ ] No commitear `appsettings.Local.json`, `.pfx`, dumps SQL, tokens ni passwords.
- [ ] `Jwt:Key` fuerte, de al menos 32 bytes, igual en API y Web.
- [ ] `ConnectionStrings:NeoStpDb` con usuario dedicado, no `sa` en produccion.
- [ ] `DataProtection` persistente y respaldado; rotarlo puede invalidar secretos cifrados.
- [ ] Certificados DTE cargados por UI/API y cifrados; nunca guardarlos como archivo en el repo.
- [ ] `Scan:Gemini:ApiKey` por variable de entorno, user-secrets o config externa.
- [ ] FCM, Meta WhatsApp, Wompi, PayPal, Stripe, MercadoPago y SMTP fuera del repo.
- [ ] API keys NeoConnect se guardan hasheadas; mostrar solo prefijo despues de crearlas.
- [ ] Secretos HMAC de webhooks se rotan creando secreto nuevo y avisando ventana al integrador.
- [ ] `Cors:AllowedOrigins` explicito en staging/produccion.

Rotaciones:

- JWT: reiniciar API/Web; los clientes reautentican.
- DataProtection: hacer backup del key ring antes; si se pierde, reingresar secretos cifrados.
- DTE: cargar certificado nuevo, probar en ambiente 00, luego activar 01.
- Gemini/FCM/WhatsApp/pasarelas: rotar en proveedor, actualizar config externa, smoke y revocar clave anterior.

## Retencion

Politica base:

| Dato | Retencion minima recomendada | Notas |
|---|---:|---|
| DTE, JSON sellado, PDF, eventos y libros fiscales | 10 anios | Obligacion fiscal recomendada; no purgar automaticamente. |
| Archivos NeoScan que soportan DTE/compra/gasto fiscal | 10 anios | Alinear con el documento fiscal que respaldan. |
| Auditoria operativa | 365 dias minimo recomendado | Job `Worker:LimpiezaAuditoria`; el servicio nunca baja de 30 dias. |
| Logs Serilog | 30 dias | Evitar secretos y payloads completos. |
| Tokens de portal | 30 dias por default | Revocables; al inactivar empresa se corta acceso. |
| Backups | Segun contrato/RPO/RTO | Separar fiscal, operativo y offboarding. |

`Worker:LimpiezaAuditoria`:

```json
{
  "Worker": {
    "LimpiezaAuditoria": {
      "Enabled": false,
      "RetencionDias": 365,
      "IntervaloHoras": 24,
      "BatchSize": 5000
    }
  }
}
```

## Health y Readiness

Endpoints:

- API: `GET /health/live` y `GET /health/ready`.
- Web: `GET /health/live` y `GET /health/ready`.

`/health/ready` debe estar verde antes de demo/release. Valida:

- Base de datos.
- Configuracion de correo.
- Storage local de logs.
- `Scan:Storage:Root` cuando `Scan:Storage:Provider=FileSystem`.

Si `Scan:Storage:Provider` tiene un valor distinto a `Database` o `FileSystem`, readiness falla para
evitar un ambiente aparentemente sano pero sin storage real.

## Preflight de Demo o Produccion

1. Revisar `git status --short` y confirmar que no hay archivos locales sensibles.
2. Ejecutar `dotnet build NeoSTP.slnx`.
3. Ejecutar `dotnet test NeoSTP.slnx`.
4. Validar `/health/ready` en API y Web.
5. Confirmar provider activo de `Scan:Provider` y `Scan:Storage`.
6. Confirmar que los logs no incluyen API keys, certificados, rutas absolutas sensibles ni payloads OCR completos.
7. Confirmar backup reciente de BD y, si aplica, `Scan:Storage:Root`.
8. Registrar evidencia de branch, commit, ambiente, providers y resultado de pruebas.

## Criterio de Cierre HB-7

- Storage NeoScan protegido contra traversal y rutas absolutas.
- Readiness valida storage configurado.
- Secretos documentados por entorno sin valores reales.
- Retencion fiscal, auditoria y logs documentada.
- README raiz, README API, contexto y planes enlazan este runbook.
- Tests HB-7 verdes junto con build y suite completa.
