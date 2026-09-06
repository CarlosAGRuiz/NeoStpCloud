# Campaña finita de certificación del cliente 23

Ejecutor operacional dedicado a una nueva campaña de **275 documentos** en PRUEBAS. No representa una matriz oficial ni acredita por sí mismo el cumplimiento del portal MH. El programa no tiene bucle de transmisión: cada invocación `--run-next` puede transmitir un único documento.

## Alcance fijo

| Tipo | Versión del JSON | Casos nuevos |
|---|---:|---:|
| 01 | 2 | 88 |
| 03 | 4 | 74 |
| 11 | 3 | 89 |
| 14 | 2 | 24 |

Empresa 23, NIT esperado, ambiente PRUEBAS, esquema SQL exacto de 91 migraciones y servidor SQL local 16 predeterminado. GUID reservado: `7bc8a3fb-08d6-4c8d-974a-01cde7fd702a`. La campaña piloto `5f681bfb-149d-4650-9a1b-5723feeebef3` conserva sus cuatro consumos y presupuesto.

Antes de preparar o ejecutar se requieren cuatro pilotos coherentes con sus consumos, JSON, JWS, respuesta y sello: 1016/01, 1017/03, 1018/11 y el único 14 de la campaña piloto. Todos deben estar PROCESADO, sin extrapolar el campo `version` de la respuesta MH a la versión del documento.

Las plantillas copian la proyección fiscal persistida de los pilotos aceptados. Para 03 también se verifica el snapshot revisado de NEO, empresa 2, como receptor autorizado de estas pruebas; no se modifica esa empresa. Para 14 se exige el pasaporte sintético del piloto aceptado. Las cantidades varían entre 1 y 5, los precios unitarios entre 1 y 10, y cada descripción identifica el ordinal del caso. Son operaciones sintéticas sin operación comercial real.

## Publicación y plan congelados

El ejecutor lee exclusivamente los literales del selector `Start-StagedHost.ps1`; nunca lo ejecuta ni lo interpreta como código. Valida ID, SHA del manifiesto, rutas dentro del workspace sin reparse points y hashes de los binarios API publicados. Sus ensamblados Infrastructure, Application y Domain deben coincidir con los de la publicación activa.

La configuración procede del directorio API activo y conserva `Dte:EsquemaNuevo=false`. Se exige la política `Dte:TenantSchemas:23` con NIT esperado, PRUEBAS y perfil `MH_20260825`. No existe un override global de esquemas.

El plan registra sólo evidencias saneadas: hashes del ejecutor y dependencias, publicación, esquemas, catálogos, identidad fiscal y receptores de los pilotos. Los valores fiscales permanecen en SQL/memoria y no se exportan al reporte. Cambiar el plan impide reutilizar la campaña. **No recompilar, sustituir dependencias, cambiar publicación o modificar los datos sellados durante la ejecución.**

## Comandos

Ejecutar desde la raíz del repositorio. Son comandos separados, no una secuencia automática.

```powershell
# Sin conexiones SQL ni proveedores: comprobaciones sintéticas.
dotnet tools/ClientCertificationBatch/bin/Release/net10.0/ClientCertificationBatch.dll --self-test

# Predeterminado: SELECT y validación local de los 275 casos; no firma, autenticación ni transmisión.
dotnet tools/ClientCertificationBatch/bin/Release/net10.0/ClientCertificationBatch.dll --preview

# Operación explícita: crea la nueva campaña ACTIVE por 48 horas y su auditoría del plan.
# Repetir no aumenta presupuesto ni extiende el vencimiento.
dotnet tools/ClientCertificationBatch/bin/Release/net10.0/ClientCertificationBatch.dll --prepare-campaign

# Operación explícita: primer caso pendiente; una recepción máxima y ninguna continuación programada.
dotnet tools/ClientCertificationBatch/bin/Release/net10.0/ClientCertificationBatch.dll --run-next
```

No se admiten parámetros de empresa, cantidades, UUID alternativo, reanudación ni reintentos. Los reportes se escriben en `tmp/client-certification-batch/` y contienen flags, IDs, hashes y códigos saneados; no payloads, certificados, contraseñas, tokens ni datos del receptor.

## Reserva, parada y recuperación

La preparación y la reserva usan transacción Serializable y el applock `NeoSTP:DTE-LIMIT:23`. Una reserva persiste juntos el borrador nuevo, correlativo, consumo y auditoría IN_PROGRESS antes de cruzar HTTP. La clave idempotente se deriva de campaña/tipo/ordinal. Se verifica que la reserva nueva no incremente la cuota comercial dentro de esa transacción.

El transporte reutilizado del runner limita a una autenticación y una recepción por invocación, exclusivamente por POST a las rutas oficiales de PRUEBAS; no permite redirecciones, cookies ni reintentos de HTTP. Antes de cada operación comprueba nuevamente identidad, plan, campaña, autorización, licencia, consumo y marcador. No arranca hosts/workers ni envía correos o webhooks.

Un rechazo, respuesta incierta, fallo del pipeline o discrepancia deja auditoría STOPPED. Una reserva previa, incluso BORRADOR o IN_PROGRESS, nunca autoriza retransmitir al repetir el comando. Una invocación concurrente que encuentre un intento abierto detiene conservadoramente la campaña. Un corte del proceso antes de registrar STOP conserva IN_PROGRESS; la siguiente invocación lo detecta y bloquea.

STOPPED no tiene comando de desbloqueo. Requiere revisión humana de la evidencia y una operación posterior expresamente diseñada y autorizada. No se eliminan consumos ni se restablecen contadores. Si falla SQL al registrar STOP, el reporte lo indica y el intento incompleto permanece bloqueante. Las guardas STOP corresponden a este ejecutor; no sustituyen la autorización y conciliación del producto ni detienen acciones manuales externas.

El marcador ACCEPTED requiere estado PROCESADO y coincidencia del sello con la respuesta guardada. Los casos ya aceptados se omiten; la próxima invocación selecciona el siguiente ordinal. Alcanzar 275 casos aceptados localmente exige una posterior comprobación del portal para afirmar el cumplimiento de sus contadores y demás requisitos.

## Verificación y límites del incremento

- Build Release con `DebugType=None` y `DebugSymbols=false`: 0 errores y 0 advertencias.
- 32 self-tests pasados; incluyen los 275 payloads sintéticos contra sus esquemas locales, aislamiento/huellas de consumos, límites, ausencia de referencias comerciales, rechazo previo al HTTP y rutas.
- Los self-tests no abren SQL, no firman DTE y no hacen peticiones a proveedores.
- La revisión independiente de fuente se cerró sin P1/P2 pendientes después de corregir el sellado de catálogos/receptores/dependencias, el binding de opciones HTTP y el vínculo exacto de campaña antes del transporte.
- El coordinador ejecutó la vista previa SQL real el 2026-09-05: 275 esquemas y cuatro pilotos aceptados verificados. El implementador no ejecutó preparaciones, SQL de escritura ni transmisiones.
- La atomicidad y las carreras del **nuevo orquestador** se revisaron estáticamente; no se ha ensayado su rollback/carrera en SQL aislado. Las pruebas anteriores de la reserva del producto no equivalen a esa prueba integral.

Huella del ejecutor congelado para la primera preparación: `6F87264293D852FDCE6EAEE69E192F86CF298D2786A9BCB86F0564BA28A65809`.

Plan observado por el coordinador antes de preparar: `FD21C5AC3D9BC27A03693299DE54D083D25CCFCA68C646D069C77680D14C160E`.
