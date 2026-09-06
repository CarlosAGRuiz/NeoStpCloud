# GL-1C — diagnóstico Hacienda y recuperación del DTE existente

2026-09-04 · `codex/gl0a-auth-security` · Código local, **sin desplegar**. API/backend; Manuel mantiene la APK. DTE11 no se reabre.

## Cambios implementados

La guía compartida interpreta el mensaje y los campos de la respuesta, no un supuesto significado universal del número de código. Cubre los reportes aportados: `008` con `emisor.codActividad` y `096` con `emisor.direccion.municipio/departamento`. Se distingue empresa emisora de receptor; no se inventan actividades, identificaciones ni ubicaciones.

El catálogo histórico presentaba `096` como falta de autorización y `802` como duplicado de forma incondicional. Su presentación se vuelve contextual, sin modificar registros históricos ni migraciones antiguas. El mensaje técnico y las observaciones siguen disponibles para soporte. Los códigos no reconocidos remiten a diagnóstico, no a una corrección supuesta.

El servicio de transmisión ahora devuelve fallo con el documento existente cuando Hacienda no lo procesa. Se exige respuesta exitosa con estado PROCESADO y sello no vacío. Una respuesta HTTP 200, por sí sola, no confirma aceptación fiscal. El detalle y la consulta de diagnóstico incluyen la guía aunque no se haya ejecutado una sincronización de ocurrencias.

## Contrato API para Manuel e integradores

Se agrega `code` a `ApiResponse<T>` sin cambiar `errors`. `data` puede contener el DTE incluso cuando `success=false`. Guardar siempre `data.id` y conservar la misma clave de idempotencia por venta.

| Resultado de transmisión | HTTP | `code` | Siguiente paso |
| --- | --- | --- | --- |
| Rechazo con campos identificados | 422 | `HACIENDA_DATOS_INVALIDOS` | Corregir en el origen y recuperar ese DTE |
| Rechazo sin corrección identificada | 422 | `HACIENDA_RECHAZO` | Revisar respuesta técnica con soporte |
| Recepción incierta | 409 | `DTE_RESULTADO_INCIERTO` | Conciliar con Hacienda; no reenviar a ciegas |
| Autenticación MH fallida | 502 | `HACIENDA_AUTH_FAILED` | Revisar credenciales y ambiente de empresa |

Errores locales de validación conservan HTTP 400. Los replays de creación continúan siendo consultas idempotentes: pueden responder HTTP 200 y contener un DTE no procesado.

`data.diagnostico` contiene `codigo`, `mensaje`, `codigoHacienda`, `mensajeTecnico`, `observaciones`, `campos[]`, `siguientePaso`, `accionSugerida`, `requiereConsultaHacienda` y `reintentoAutomatico=false`. Cada campo contiene su ruta, sección (`EMPRESA`/`RECEPTOR_DTE`), explicación y acción. La guía es informativa, no una autorización para saltarse permisos ni un endpoint que ejecuta acciones.

- `GET /api/dte/documentos/{id}` y `GET /api/v1/dte/{id}`: detalle **local** del DTE y guía, con los permisos/scopes previos.
- `GET /api/dte/diagnostico/documentos/{id}`: diagnóstico ampliado, módulo NEODTE + `DTE.Diagnostico`, empresa del usuario. No habilita acceso entre empresas.
- Para rechazo confirmado: corregir los datos, regenerar el JSON existente y revisar/validar/firmar/enviar por las acciones autorizadas sobre ese ID. Cambiar la ficha de un cliente no implica que se haya cambiado automáticamente el receptor histórico: revisar el JSON antes de enviar.
- Para resultado incierto: la lectura GET no consulta Hacienda. Escalar a soporte con ID, número, código de generación, ambiente y TraceId. No crear otra factura ni cambiar de ambiente.

## Persistencia y seguridad de recuperación

Antes de contactar al receptor HTTP se guarda ENVIADO y la fecha de envío. Timeout, red, HTTP 5xx o respuesta ilegible quedan pendientes de conciliación en ENVIADO, fuera del selector automático de CONTINGENCIA. El siguiente envío secuencial se bloquea si la evidencia sigue siendo incierta, incluso después de reabrir un contexto de datos. También se impide borrar esa evidencia regenerando documentos históricos inciertos.

Las respuestas no procesadas se registran como ocurrencias de transmisión con empresa, documento, JSON enviado y respuesta. Al regenerar un rechazo confirmado se conserva el intento anterior y se limpia la fecha de envío del JSON vigente. No se marcan ocurrencias resueltas por suposición.

Una respuesta MH normal se conserva íntegra. En fallos de transporte se guarda un sobre `origen=NEOSTP_TRANSPORTE` con HTTP/clasificación y `respuestaOriginal` sin modificar. No contiene credenciales ni token de autenticación.

## Límites y siguiente bloque

No se desplegaron hosts, no se transmitió a Hacienda ni se modificaron datos del cliente. No hay una nueva migración en GL-1C, pero siguen pendientes las cuatro migraciones anteriores de esta rama. No iniciar API/Web/Worker sobre la base activa para aplicarlas implícitamente.

Antes de producción falta implementar y verificar la **conciliación remota autenticada**, y una exclusión atómica/cola durable para dos envíos manuales simultáneos que ya hubieran superado la lectura inicial. La marca previa reduce ventanas de pérdida de respuesta, pero no promete entrega remota exactamente una vez ni sustituye ese control de concurrencia. No se agrega un botón de desbloqueo sin evidencia remota.

Eventos y lotes conservan sus flujos propios; no se declaran cubiertos por el nuevo contrato de transmisión individual. La APK debe consumir los nuevos campos y estados HTTP en un despliegue coordinado. La adaptación visual completa de la Web sigue separada: el backend sí mejora el mensaje de fallo utilizado por la Web.

## Verificación

- 57 pruebas focalizadas aprobadas: errores reportados, origen empresa/receptor, errores desconocidos, duplicado, estados finales, HTTP 400/401/500, respuestas no JSON, ausencia/tipo incorrecto de sello, pérdida de respuesta, historial y recuperación con el mismo ID.
- Suite Release completa: 1,319 unitarias + 9 de integración aprobadas (1,328). La integración habitual usa InMemory; la comprobación SQL se ejecutó por separado.
- Compilación Release de solución: 0 errores; permanece la advertencia preexistente CS8604 en `Inventario/LotesInventarioTests.cs:137`. Sin cambios de esquema nuevos en este bloque.
- 32 comprobaciones aprobadas con `tools/DteSqlVerification --migration-chain`: cadena completa de migraciones, controles previos GL1A/GL1B y transmisión real del servicio contra transporte simulado. Se verificó ENVIADO durable, respuesta e intento histórico después de reabrir contexto SQL y bloqueo de reenvío incierto.
- SQL LocalDB 2025, instancia exclusiva de auditoría: se eliminó la base aleatoria `NeoStpDteAudit_7ba47fe4eff244a7b5c3f82a969f3421` al finalizar y se verificó que no quedaran bases de usuario antes de detener la instancia. No se usaron datos reales ni llamadas MH.
- Sigue pendiente evidencia sobre SQL Server 2022 y la ejecución E2E autorizada con consumidores compatibles antes del despliegue.
