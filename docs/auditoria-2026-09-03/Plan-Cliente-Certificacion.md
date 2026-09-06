# Plan del cliente: certificación y habilitación controlada

> Revisión 2026-09-04: CERT-0–4 y actualización del portal siguen pendientes; los conteos son históricos. Los cierres técnicos locales están en la [auditoría actualizada](Auditoria-Actualizacion-2026-09-04.md) y el [plan de continuidad](Plan-Continuidad-Auditado-2026-09-04.md).

Fecha: 2026-09-03. Cliente: DANIEL IMPORTADORA Y DISTRIBUIDORA, EmpresaId 23 en la base auditada. Estado: plan listo; ejecución pendiente. Fuente de alcance: tipos marcados con asterisco en la captura del portal aportada y confirmación explícita del usuario.

Dependencias: [auditoría](Auditoria-Sistema-API.md) y [plan de producción de NEO](Plan-Produccion-NEO.md). La autorización y las credenciales de NEO **no se reutilizan** para habilitar al cliente.

Actualización 2026-09-05: nueva captura reafirma cuatro tipos y 279 pendientes según imagen; el titular sólo tiene contadores. Preflight confirma PRUEBAS/certificado local y detecta geografía por corregir y cuota compartida. MAIL-23 cerrado en el corte posterior: configuración Hostinger persistida y recepción de prueba confirmada por el usuario. Ver [evidencia actual de correo](evidencia/cloudflare-hsts-mail-2026-09-05/README.md) y [corte de certificación](Cliente-Certificacion-Correo-2026-09-05.md). No hay nuevas transmisiones ni casos acreditados.

## 1. Objetivo y alcance acordado

Completar las pruebas obligatorias indicadas por el cliente y obtener evidencia conciliada antes de solicitar/activar producción para esa empresa.

| Tipo | Documento | Captura MH | Restantes en ese corte |
|---|---|---:|---:|
| 01 | Factura | 1 / 90 | 89 |
| 03 | Comprobante de Crédito Fiscal | 0 / 75 | 75 |
| 11 | Factura de Exportación | 0 / 90 | 90 |
| 14 | Factura de Sujeto Excluido | 0 / 25 | 25 |
| Total | Cuatro tipos, no cinco | 1 / 280 | **279** |

Antes de iniciar se actualiza el portal: si el conteo cambió, se recalcula el pendiente y se evita duplicar trabajo. La captura no especifica por sí sola los casos de cada matriz; deben contrastarse con los requisitos vigentes de la cuenta del cliente.

No se incorporan como obligación del cliente tipos sin asterisco ni metas de eventos de esa captura. Contingencia, retorno e invalidación sí se ensayan como capacidades del sistema cuando apliquen, sin inventar obligación de certificación para esta empresa. Cualquier requerimiento adicional mostrado posteriormente por Hacienda se registra como cambio de alcance.

El tema «DTE11» ya está cerrado por indicación del usuario. No se reabre ni se modifica su evidencia; los casos de Exportación aquí son únicamente los de certificación solicitados.

## 2. Condiciones previas: CERT-0

Responsables: técnico de backend/QA y operador autorizado del cliente. Sin transmisión hasta cumplir estos puntos.

1. Fijar release candidata con SHA, pruebas y respaldo; cerrar SEC-01, TENANT-01, AUTH-01/02 y los controles fiscales DTE-01/03 de la auditoría para los canales usados.
2. Confirmar EmpresaId, razón social/NIT, ambiente PRUEBAS, usuario MH, certificado y punto de venta del cliente. Comprobar vigencia, capacidad de firma y coincidencia de emisor sin imprimir secretos.
3. Contrastar actividad económica con el registro de ese contribuyente: no usar una actividad genérica solo porque exista en catálogo. Confirmar territorio y demás datos fiscales. No inventar códigos ni copiar configuración de NEO.
4. Tomar captura/exportación actual de matrices y conteos del portal; archivar fecha y responsable.
5. Reconciliar los 11 DTE locales: 1 procesado, 8 errores y 2 borradores al corte de auditoría. Vincular el aceptado solo al escenario que realmente satisface. No marcar errores como completados ni borrarlos para ocultar rechazos.
6. Crear datos de ensayo identificables y destinatarios controlados; impedir envío de correos/links a compradores reales durante certificación.
7. Verificar API, Web y Worker con proveedores efectivos correctos. Worker no puede iniciar con Mock por falta de configuración.
8. Definir un operador de ejecución y un revisor; bloquear emisión paralela no coordinada durante cada lote de certificación.

Aceptación CERT-0: ficha de emisor validada por el cliente, release identificada, ambiente visible, acceso mínimo correcto y línea base portal/local conciliada.

## 3. CERT-1: herramienta segura y experiencia fiscal

### 3.1 Parametrizar la ejecución

El arnés actual tools/CertHarness fija EmpresaId=2. **No ejecutarlo sin cambios para este cliente.**

Entregables de código:

- Parámetros obligatorios: empresa, ambiente de pruebas, tipo, escenario, máximo de documentos y destino de evidencia.
- Comprobación independiente del NIT esperado y empresa seleccionada antes de firmar. Si no coincide, abortar.
- Modo simulación/validación local que no envía a Hacienda; modo de transmisión explícito.
- Reanudar desde registro duradero; conservar documento, código de generación, número de control, sello, estado, escenario y respuesta MH.
- No recrear documento por timeout: consultar primero la identidad ya utilizada y resolver el estado incierto.
- Detener lote ante error no clasificado, ambiente incorrecto, firma fallida, inconsistencia o límite de servicio. Respetar Retry-After.
- Evidencia única y válida por escenario; no “completar” varios casos enlazando el mismo aceptado.
- Pruebas unitarias y SQL de aislamiento: una ejecución para 23 no lee/muta la emisión de 2.

Aceptación: dry-run valida selección y payload de los cuatro tipos; empresa/ambiente erróneos se rechazan; reanudar no duplica emisión.

### 3.2 Tipos habilitados y mensajes

Implementar GL-1 del plan de NEO antes de entregar la experiencia de cliente:

- API devuelve capacidades efectivas del tenant; inicialmente 01/03/11/14 para esta campaña.
- En producción, capacidades = soporte técnico ∩ plan ∩ configuración de empresa ∩ autorización fiscal de la empresa. En pruebas se usa el alcance de certificación, no una autorización productiva ficticia.
- Web, Android, Connect, POS y Worker usan la misma validación; no basta filtrar el selector.
- Errores señalan emisor/receptor, campo, razón y acción; conservan traceId y respuesta técnica protegida. Un rechazo no se arregla generando automáticamente otro DTE.
- Cliente registrado autocompleta documento, número, nombre, NRC y correo disponibles sin conservar datos del cliente anterior.
- El Salvador usa dependencia **país → departamento → municipio** del catálogo MH. Para extranjero no mostrar/exigir departamento/municipio salvadoreños y limpiar valores anteriores. Un municipio pertenece a un departamento; no invertir esa jerarquía.
- Estado final PROCESADO completa el indicador visual; búsquedas por número, código de generación, cliente, monto/rango, tipo, estado, fecha y ambiente, paginadas en servidor.
- Retorno solo muestra documentos elegibles según reglas reales del evento, no todos los procesados.

Aceptación: pruebas de contrato API y recorridos Web/App con campos faltantes, cambio de cliente/país, tipo no permitido, error MH conocido y desconocido, búsquedas y estado procesado. No afirmar que estas mejoras ya están hechas por estar en este plan.

## 4. CERT-2: ejecución en lotes con conciliación

Orden sugerido: 01 → 03 → 11 → 14, una vez CERT-0/1 aceptados. Si el portal establece dependencia u orden diferente, prevalece su requisito.

### Secuencia por tipo

1. Revisar matriz/casos de ese tipo en el portal y mapearlos a datos de prueba.
2. Elegir un escenario representativo pendiente. Validar esquema, totales y datos; guardar JSON y huella de contenido.
3. Firmar y transmitir **solo en PRUEBAS**, con la identidad fiscal preparada. Verificar estado PROCESADO y sello.
4. Comparar resultado local con portal y comprobar PDF/JSON/JWS. No considerar éxito HTTP como aceptación fiscal.
5. Ejecutar lotes pequeños, inicialmente de hasta cinco documentos, ajustados a los límites publicados/observados; no usar paralelismo masivo.
6. Tras cada lote, conciliar cantidad de aceptados distintos, escenarios cubiertos y errores. Guardar checkpoint antes del siguiente.
7. Si hay timeout: detener ese documento, consultar estado y recuperar aceptación/rechazo; no asignar otro correlativo por sospecha.
8. Si hay rechazo: identificar campo, corregir origen/configuración según mensaje y estado permitido, mantener trazabilidad del intento. No editar un aceptado.
9. Repetir hasta completar exactamente los casos pendientes, no simplemente producir N facturas con el mismo contenido.

### Matriz de aceptación funcional

| Tipo | Verificaciones locales además de lo exigido por la matriz MH |
|---|---|
| 01 | Receptor/identificación según escenario, operaciones y totales/impuestos, redondeo, efectivo/crédito cuando corresponda |
| 03 | Datos fiscales completos del receptor, coherencia de base/IVA/totales y escenarios específicos de CCF |
| 11 | Identificación y dirección exterior, país/moneda/campos de exportación según esquema y caso vigente |
| 14 | Identificación de sujeto excluido, retenciones/cálculos cuando proceda y coherencia del resumen |
| Todos | Empresa y ambiente correctos, número/código únicos, firma verificable, sello y consulta, PDF/JSON consistentes, no duplicación por reintento |

Los cálculos tributarios y reglas condicionales se contrastan con el esquema/matriz vigente y revisión del responsable fiscal; esta tabla no reemplaza esa normativa.

Registro mínimo por intento: empresa, ambiente, tipo, escenario, identidad DTE, fecha, hash del payload, resultado, sello, observaciones MH, acción de corrección y actor. No registrar contraseña MH, JWT, clave privada ni datos personales innecesarios.

## 5. CERT-3: cierre verificable de pruebas

Criterios acumulativos:

- Portal muestra las cuatro metas cumplidas: 90 + 75 + 90 + 25, actualizadas a la fecha del cierre.
- Evidencia local corresponde a empresa 23 y PRUEBAS; todos los aceptados tienen identidad/sello y escenario verificable.
- No hay estados inciertos ni reintentos automáticos pendientes que puedan duplicar documentos.
- Se revisaron permisos entre empresas y usuario operativo en Web/App.
- Cliente firma aceptación funcional; responsable fiscal verifica datos y resultado del portal.
- Se archiva reporte final protegido y se obtiene el requisito/habilitación de producción correspondiente a su cuenta. **El conteo completo no activa producción automáticamente.**

Entregable: expediente de certificación del cliente separado del de NEO, con conteos, matrices, evidencia, incidencias resueltas y autorización para la siguiente etapa.

## 6. CERT-4: producción del cliente, corte independiente

Solo después de CERT-3 y de que la plataforma haya superado los bloqueos del plan NEO:

1. Revisar autorización vigente del cliente, tipos realmente habilitados, credenciales/certificado productivos y aceptación de fecha de corte.
2. Elegir el destino productivo y estrategia de datos; preservar pruebas históricas fuera de reportes productivos.
3. Validar que no existan DTE productivos anteriores antes de definir el punto de numeración. No resetear IDs ni correlativos globales.
4. Configurar únicamente al cliente; no cambiar ambiente de NEO ni otra empresa como efecto lateral.
5. Prueba de preparación sin emisión; luego primera operación real legítima supervisada y autorizada por el cliente, con importe/receptor revisados.
6. Verificar aceptación, representación, entrega y registro contable. No fabricar ventas en producción para probar.
7. Conciliación diaria inicial y seguimiento de rechazos, estados inciertos, vencimientos y colas.

Rollback: antes de emitir puede revertirse configuración validada. Después de una aceptación fiscal se preservan documento y numeración; se detiene nueva emisión y se reconcilia, nunca se “deshace” restaurando una base anterior para reutilizar números.

## 7. Responsables, hitos y decisiones

| Hito | Responsable | Evidencia de cierre |
|---|---|---|
| CERT-0 | Backend + operador cliente | Identidad/ambiente y portal confirmados |
| CERT-1 | Backend + Web/Android + QA | Arnés parametrizado, permisos y contratos probados |
| CERT-2 | Operador autorizado + QA | Checkpoints por tipo y aceptación MH |
| CERT-3 | Cliente + responsable fiscal | Portal completo y expediente revisado |
| CERT-4 | Operación + cliente | Autorización, corte y primera operación conciliada |

No se promete duración de las 279 pruebas antes de conocer límites del servicio, escenarios y rechazos. Los lotes y decisiones se registran; no se deja una campaña transmitiendo sin supervisión.

Decisiones que se necesitan al ejecutar: acceso autorizado del operador, confirmación de datos fiscales/certificado, captura actual de portal y ventana de ejecución. No hace falta exponer esas credenciales en este documento ni en el chat.
