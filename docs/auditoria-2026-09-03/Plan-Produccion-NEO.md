# Plan de salida a producción de NEO y evolución del producto

> Actualización 2026-09-05: preparación productiva solicitada por el usuario en ejecución. Ver [ensayo y estado actual de NEO](NEO-Preparacion-Produccion-2026-09-05.md). El estado "plan propuesto" y las observaciones del 3 de septiembre que siguen se conservan como línea base histórica; no sustituyen la evidencia del corte nuevo.

> Actualización 2026-09-04: este documento conserva el diseño operativo original. Para cierres GL0A–GL1G y siguiente sprint GL1H, usar la [auditoría actualizada](Auditoria-Actualizacion-2026-09-04.md) y el [plan vigente](Plan-Continuidad-Auditado-2026-09-04.md). Las fases GL-0A/B/C propuestas aquí no equivalen uno a uno a los bloques GL0A/B/C implementados después.

Fecha: 2026-09-03. Empresa: NEO SOFTWARE TECH PRO, EmpresaId 2 en la base auditada.
Estado: **plan propuesto, no ejecutado**. Go-live bloqueado hasta cerrar las puertas de seguridad, integridad y operación.

Documentos de control: [auditoría completa](Auditoria-Sistema-API.md), [plan separado del cliente](Plan-Cliente-Certificacion.md), [evidencia](evidencia/README.md).

## 1. Decisión y alcance

Siguiente sprint recomendado: **GL-0 — seguridad, separación fiscal y operación recuperable**. No comenzar por limpiar tablas ni cambiar PRUEBAS a PRODUCCION.

Dos objetivos independientes:

- NEO sale a producción con sus propios documentos autorizados y capacidad de operar de forma segura.
- El cliente termina sus cuatro matrices y obtiene su habilitación particular. Su cuenta, pruebas y certificado no se sustituyen por los de NEO.

El PDF aportado de NEO lista diez documentos en PRODUCCION: 01, 03, 04, 05, 06, 07, 08, 09, 11 y 14; tres eventos: retorno, contingencia e invalidación. **Donación 15 y Operaciones Especiales no están acreditados en ese PDF.** Deben permanecer fuera del alcance productivo salvo evidencia posterior. Los 625 escenarios locales completos no amplían esa autorización.

Salida fiscal mínima: emitir los tipos autorizados con datos correctos, permisos, contingencia/recuperación aplicables, representación/entrega y soporte. Pagos QR/pasarela, pausa voluntaria y otros módulos pueden liberarse después si están realmente deshabilitados hasta su aceptación. No se venderán como terminados por tener una pantalla.

## 2. Secuencia de trabajo con puertas de aceptación

No son fechas comprometidas ni sprints de duración fija; cada etapa termina por evidencia.

| Etapa | Trabajo concreto | Dependencia / aceptación |
|---|---|---|
| GL-0A Seguridad | Cerrar escalada SUPERADMIN, refresh de empresa, lockout/MFA y revocación; corregir o deshabilitar SSO/SSRF/webhooks vulnerables | SEC-01/TENANT-01/AUTH-01/02/03 cerrados con pruebas API/Web; sin P0 abierto |
| GL-0B Integridad fiscal | Ambiente inmutable, numeración/índices, filtros de reportes, idempotencia Connect/POS, errores confiables | Migración ensayada en SQL y reintentos sin duplicados; pruebas nunca cuentan como producción |
| GL-0C Operación | Tres servicios, configuración sin Mock inadvertido, claves persistentes, backup/restore y salud real | Arranque sin login y recuperación medidos; falla de Worker observable |
| GL-1 Producto fiscal | Tipos por plan/empresa, mensajes MH, filtros y coherencia Web/App | Regla central aplicada en todos los emisores; contratos y permisos verificados |
| GL-2 Preproducción y corte NEO | Datos, credenciales propias, validación fiscal, ensayo, respaldo fresco y habilitación controlada | Checklist go/no-go firmado; primera operación legítima conciliada |
| GL-3 Pausa de suscripción | Pausa voluntaria, tiempo pagado, reanudación inmediata sin cargo | Contratos API/Web/App, concurrencia, cuotas y facturación probados |
| GL-4 Cobros de comercio | Una pasarela completa primero, QR/link, webhooks y conciliación | Sandbox y piloto reales aprobados; no confundir con billing SaaS |
| GL-5 Consolidación | Extender proveedores, ERP/POS y módulos opcionales | SQL concurrente, QA de canales y observabilidad por módulo |

GL-1 puede avanzar después de estabilizar los contratos de GL-0. CERT-CLIENTE utiliza las correcciones comunes, pero su campaña y corte no dependen de activar NEO ni viceversa.

## 3. GL-0A: seguridad multiempresa

Entregables de código:

1. Separar capacidad de plataforma y roles de empresa. Un nombre de rol no debe conferir acceso global. Reservar SUPERADMIN y validar actor desde el contexto confiable tanto en API/Web como servicios.
2. Cubrir creación, edición, asignación de roles, membresías e invitaciones. Pruebas negativas con administrador tenant y dos empresas; administración global solo con ruta/actor autorizado.
3. Asociar refresh a sesión/empresa seleccionada y revalidar pertenencia, rol y estado. No volver silenciosamente al tenant principal.
4. Corregir expiración de bloqueo temporal; mantener suspensiones administrativas.
5. Cuotas específicas para login/MFA/refresh; códigos MFA erróneos cuentan; enrolamiento MFA sin acceso global previo.
6. Invalidar sesiones al bloquear usuario/cambiar credenciales o permisos según política documentada, con TTL y caché controlados.
7. Corregir SSO con validación de identidad/directorio o deshabilitarlo. Corregir salida webhook SSRF o deshabilitar registro/despacho. Firmas de billing obligatorias o endpoints deshabilitados.
8. Registrar auditoría útil sin JWT, contraseñas, claves ni token completo de portal.

Aceptación: arnés de auditoría ya no reproduce escalada ni cambio de tenant; pruebas de integración HTTP autentican roles reales; ninguna petición del tenant A puede leer/mutar IDs del B. El smoke 401 actual es solo una línea base, no el cierre de esta puerta.

## 4. GL-0B: integridad de documentos y movimientos

### Ambiente y numeración

- Separar explícitamente ambiente de ejecución ASP.NET (Production) del ambiente fiscal MH (PRUEBAS/PRODUCCION). Un host seguro puede servir empresas todavía en certificación.
- Documento conserva empresa, ambiente fiscal y contexto de emisión. El envío rechaza una combinación incompatible de documento, endpoint y credenciales antes de firmar/transmitir.
- Revisar alcance legal/técnico de numeración por empresa, tipo, establecimiento/punto de venta y período; alinear correlativos e índices con ese alcance y la estrategia de ambientes. No asumir reinicio anual ni desde 1 sin verificar historial/regla aplicable.
- Ensayar convivencia/migración de documentos existentes. Índice actual empresa+número impide reiniciar manteniendo números iguales sin rediseño.
- Idempotencia duradera en emisión API, Connect y promoción POS; respuesta repetible; consulta ante timeout; no volver a enviar un documento ya aceptado como nuevo.
- Reportes fiscales, cobranza y métricas productivas deben excluir PRUEBAS por diseño.

### ERP habilitado

Si POS/inventario/cobranza/tesorería estarán disponibles al corte:

- Resolver venta guardada sin salida de stock; política explícita de stock insuficiente.
- Número de ticket seguro, no Count+1.
- Transacciones/controles de concurrencia por saldo/lote, idempotencia de pago y movimiento.
- Ensayar simultáneamente dos ventas del último artículo, dos pagos del mismo saldo, dos movimientos bancarios y reintento de promoción.
- Cuadrar stock, caja, cobranza, tesorería y contabilidad tras éxito, error y reverso.

Los módulos que no pasen quedan bloqueados en backend, no solo sin menú.

## 5. GL-0C: servicios, secretos y recuperación

### 5.1 Arranque confiable

Estado actual: tareas API/Web AtLogOn e Interactive, Development; Worker no observado. Esto no cumple “enciendo la PC y funciona sin iniciar sesión”.

Implementación propuesta para el host Windows actual:

1. Publicar Web, API y Worker en Release a directorios versionados fuera de bin/Debug.
2. Integrar hosting de Windows Service e instalar tres servicios con nombre, directorio de trabajo y argumentos explícitos.
3. Cuenta dedicada de mínimo privilegio con permisos de lectura de binarios, escritura de logs/storage/claves y conexión SQL adecuados.
4. Inicio automático y recuperación ante fallo; dependencia/espera de SQL/red. Evitar instancias duplicadas y jobs ejecutados por dos Workers.
5. Migrar desde las tareas anteriores solo cuando el nuevo despliegue esté verificado; mantener reversión, evitar pelea por puertos.
6. Health liveness/readiness de los tres hosts; heartbeat y edad de cola del Worker, firma/proveedor efectivo visible sin secretos.
7. Reiniciar el equipo en una ventana autorizada y comprobar funcionamiento **sin login interactivo**.

Usar la publicación y el modelo de servicio soportado por ASP.NET Core: [guía oficial de Windows Service](https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/host-and-deploy/windows-service.md). No usar dotnet run o certificados de desarrollo como despliegue productivo.

El arranque automático no resuelve una PC apagada, cortes eléctricos, internet caído o mantenimiento. Antes de comprometer disponibilidad comercial se decide host dedicado/infraestructura disponible y continuidad eléctrica/red; no hay contratación ni migración de hosting autorizada por este documento.

### 5.2 Configuración y secretos

- ASP.NET Production, error page segura, HTTPS público y proxy conocido. Configurar forwarded headers de API para esquema/IP correctos sin confiar en cabeceras de cualquier origen.
- Validar configuración al inicio: cliente MH, firmador y cada proveedor habilitado deben ser explícitos y compatibles. No basta rechazar el texto Mock.
- Módulos deshabilitados requieren rutas/jobs deshabilitados; no poner PermitirMocksEnProduccion=true como atajo general.
- Persistir y proteger el key ring de DataProtection compartido por los hosts que descifran los mismos secretos. Respaldarlo y probar recuperación antes de cambiar identidad de servicio.
- Mantener separadas claves/configuración de pruebas y producción; inventario de propietarios, vigencia y rotación. Nunca guardar secretos en git ni enviarlos a logs.
- JWT fuerte y distinto por ambiente, cookies seguras, CORS allowlist, acceso administrativo protegido.
- Usuario runtime SQL con mínimo privilegio; migración de esquema por identidad de despliegue.

Persistir claves exige protegerlas también en reposo; elegir mecanismo compatible con la cuenta y recuperación: [Data Protection, documentación Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-10.0).

### 5.3 Backup y recuperación

- Programar respaldo físico SQL, no confiar en el manifiesto de BackupService.
- Copiar fuera del host y cifrar; incluir storage DTE/adjuntos, key ring, configuración recuperable y material fiscal mediante custodia segura.
- Verificar integridad y restaurar a un destino aislado. Probar login, descifrado, consulta de DTE/PDF y relaciones sin activar envíos del Worker.
- Medir tiempo de recuperación y pérdida máxima de datos; acordar RTO/RPO con negocio antes de fijarlos como promesa.
- Ensayar backup previo y posterior a migraciones, fallas de disco y recuperación de secretos.
- El .bak del 2026-09-02 pasó VERIFYONLY; falta restauración funcional y backup fresco de corte.

## 6. GL-1: tipos DTE configurables y errores claros

### 6.1 Tipos por plan y empresa

Modelo propuesto, manteniendo monolito modular:

- PlanTipoDte: capacidades comerciales de cada plan.
- EmpresaTipoDte: selección/override explícito y auditable, dentro de lo permitido.
- AutorizacionFiscalEmpresa: tipos/eventos acreditados por ambiente, vigencia y referencia de evidencia.
- Servicio en Application/Infrastructure que calcula capacidades y valida antes de crear, firmar, enviar, promover o retransmitir.
- Eventos en catálogo separado: retorno no es una factura adicional.

Regla productiva: soporte implementado ∩ plan ∩ selección empresa ∩ autorización fiscal. No permitir que un administrador tenant amplíe por sí mismo la autorización fiscal o los derechos del plan.

Migración: construir configuración inicial desde evidencia y asignaciones revisadas; no marcar todos los tipos para todos ni dejar a clientes bloqueados sin análisis. Los DTE históricos siguen consultables aunque el plan cambie; cambios de plan no borran documentos. Los eventos necesarios sobre documentos existentes se rigen por obligaciones/reglas del evento y permisos, no por ocultación arbitraria.

Contrato API propuesto: lectura de capacidades efectivas y administración autorizada de catálogo del plan/empresa. Nombres de rutas definitivos se ajustan al versionado existente; no son endpoints ya disponibles. Web/App consumen capacidades y actualizan caché al cambiar empresa/plan. Backend rechaza tipo no permitido aunque se manipule el cliente.

Aceptación:

- NEO recibe solo sus capacidades acreditadas; cliente su subconjunto acordado.
- Dos planes con conjuntos diferentes; downgrade/upgrade; permiso insuficiente; catálogo vacío; un evento no aparece como factura.
- Web, Android, API directa, POS y Connect coinciden; Worker no ignora restricción.
- Identificación visible y persistente de ambiente.

### 6.2 Mensajes de Hacienda y experiencia

- Adaptador central normaliza respuesta MH por código, campo y texto/contexto; no etiqueta todos los errores 096 de una sola manera.
- DTO de error con code, message, fieldErrors, suggestedAction, retryable, traceId y referencia del documento. Respuesta técnica íntegra solo para diagnóstico autorizado.
- Diferenciar “corrige datos del emisor”, “corrige receptor”, “credencial”, “documento aceptado previamente”, “servicio no disponible” y “estado incierto”.
- Nunca sugerir invalidar un aceptado o generar otro número automáticamente como solución genérica.
- Web/App muestran la misma acción y conservan formulario/datos; captura de errores desconocidos para clasificación sin ocultar el mensaje original.
- Incluir autocompletado de cliente, territorio coherente, estado PROCESADO completo y filtros server-side solicitados.

Aceptación: casos conocidos/desconocidos, schema inválido, mismatch emisor, credenciales, timeout y duplicado con unit tests, contrato API y recorridos de interfaz. El incidente «DTE11» cerrado no se reabre; una regresión solo usaría un fixture anonimizado existente, nunca una nueva emisión del documento.

## 7. GL-2: datos y corte productivo de NEO

### 7.1 Decisión recomendada de separación

Preferir una **base/entorno productivo limpio separado del de pruebas**, migrando solo datos maestros autorizados de NEO y conservando la base de pruebas como evidencia con acceso restringido. Esto reduce mezcla de reportes y numeración, pero requiere diseñar tenant/membresías, secretos y enrutamiento de Web/API/Worker. Es una propuesta, no una migración ejecutada.

Si se elige mantener la misma base multiempresa, son obligatorios los cambios DTE-01/02, aislamiento por ambiente y una limpieza selectiva probada. Nunca cambiar toda la base a producción porque una empresa ya esté habilitada.

### 7.2 Limpieza solicitada: únicamente pruebas propias

Al corte hay 796 DTE de PRUEBAS de EmpresaId 2 y 11 del cliente 23. Antes de cualquier borrado:

1. Volver a identificar empresa/NIT y contar documentos por ambiente; no confiar solo en IDs de este corte.
2. Inventariar dependencias: DTE/líneas/JSON/JWS/respuestas, eventos/lotes, certificación, colas, correos/webhooks, portal, pagos/caja, inventario y reportes derivados.
3. Crear respaldo físico fresco + archivos/claves; comprobar restauración aislada.
4. Exportar evidencia de certificación y registro de lo que se eliminaría. Guardar manifiesto con tablas, filtros, conteos y relaciones.
5. Preparar script con dry-run por defecto y guards: EmpresaId/NIT esperado, ambiente PRUEBAS, ausencia de producción y aprobación explícita del alcance.
6. Probar el script contra la copia, comprobar FK y verificar que empresa 23, catálogos y datos maestros seleccionados no cambian.
7. Pedir autorización de corte con manifiesto exacto y plan de reversión. Ejecutar solo en ventana controlada; pausar escritores/jobs afectados.
8. Validar conteos antes/después y conservar trazabilidad.

**No hacer TRUNCATE general, DELETE sin filtro, reseed global de identidades ni renumerar documentos aceptados.** Los IDs internos no son números fiscales y no necesitan empezar en 1 para salir a producción. Reiniciarlos puede romper relaciones/enlaces/auditoría. Si se quiere códigos comerciales legibles, crear una serie explícita, no reutilizar claves primarias.

La numeración productiva se inicializa únicamente tras verificar su historial, alcance y ausencia de emisiones previas; ni “limpiar” ni restaurar una copia autoriza reutilizar identidad fiscal.

### 7.3 Checklist previo de NEO

- [ ] Autorización y tipos del PDF confirmados vigentes en cuenta NEO.
- [ ] Certificado no vacío/correcto, titular y vigencia verificados; custodia de claves.
- [ ] Actividad, establecimiento/punto de venta, dirección y demás datos fiscales correctos.
- [ ] Release SHA y migraciones ensayadas, sin cambios sin identificar.
- [ ] GL-0 completo; riesgos de módulos opcionales mitigados por deshabilitación verificable.
- [ ] Tipos efectivos correctos en todos los canales.
- [ ] Separación/migración de datos aceptada; empresa cliente intacta.
- [ ] Numeración e idempotencia verificadas en SQL concurrente.
- [ ] Backup/restauración de BD, archivos y claves aceptados.
- [ ] Tres servicios funcionan sin login; TLS/DNS/proxy y acceso externo verificados.
- [ ] Correo real controlado y descarga de PDF/JSON correctos.
- [ ] Operador capacitado en rechazo, timeout, contingencia y soporte.
- [ ] Alerta de certificado, Worker caído, colas antiguas, rechazo sostenido y respaldo fallido.
- [ ] Responsable de negocio/fiscal y operación aprueban go-live.

### 7.4 Corte y primera emisión

1. Congelar despliegues/escrituras de NEO durante ventana acordada, no de otras empresas sin necesidad.
2. Respaldo fresco, migración controlada y activación de configuración de NEO.
3. Smoke de lectura, permisos y preparación sin emisión. La primera emisión productiva será una venta/operación legítima, autorizada y revisada, no un DTE ficticio.
4. Verificar aceptación/sello, consulta, representación, entrega y registro contable.
5. Conciliar diariamente al inicio: documentos emitidos/aceptados/inciertos, correlativos, rechazos, colas, pagos si habilitados, backup y vigencia de certificado.
6. Mantener release anterior compatible y atención de incidentes; ampliar gradualmente operadores/módulos.

### 7.5 Reversión segura

Antes de cualquier aceptación fiscal puede revertirse configuración/binarios/migración según ensayo y compatibilidad.

**Después de emitir un DTE productivo aceptado no se restaura una base anterior para “volver a empezar”.** Se detiene nueva emisión afectada, se consulta Hacienda, se preservan identidades/sellos y se aplica reparación hacia adelante. Rollback de binarios solo si es compatible con el esquema y datos nuevos. Recuperación de desastre exige reconciliar documentos aceptados después del último backup, nunca reutilizar números.

## 8. GL-3: pausa voluntaria y reanudación gratuita

Objetivo del usuario: pausar cuando no vende y reanudar inmediatamente cuando necesite vender, sin cargo por reactivación.

Diseño propuesto para aprobar antes de implementar:

- Estado propio PAUSED_BY_CUSTOMER, diferente de SUSPENDED, deuda, cancelación o bloqueo de seguridad.
- Guardar instante UTC de pausa, tiempo pagado restante, versión de concurrencia y eventos de transición.
- Congelar consumo de tiempo del servicio pagado mientras esté pausado; al reanudar conservar ese saldo. No reiniciar cuota mensual ni ampliar gratis cuotas ya consumidas por repetir pausa/reanudación.
- Reanudar no cobra comisión ni abre un checkout. Deuda previa/bloqueo de seguridad no se borra mediante pausa.
- Mantener login, historial, descargas, soporte, datos y recepción de webhooks/pagos ya iniciados. No interrumpir resolución de DTE inciertos, contingencias o deberes pendientes.
- Bloquear nuevas ventas/emisiones mientras pausado; acción “Reanudar y continuar” accesible con permiso en Web/App.
- Para tiempo pagado ya agotado debe definirse por negocio qué acceso corresponde; no prometer uso indefinido sin suscripción bajo la frase “reactivación gratuita”.
- Evitar doble cobro externo: detener/reprogramar recurrencia del proveedor según su contrato. No cambiar solo un flag local mientras la pasarela sigue cobrando.

Contrato API propuesto: consultar estado/transiciones permitidas, pausar y reanudar con idempotencia, versión y autorización por empresa. Cliente muestra saldo de tiempo y fecha estimada, y recibe mensaje de decisión claro.

Aceptación: pausa doble, reanudar doble, carreras Web/App, pausa con cobro pendiente, límites, cambio de plan, zona horaria y renovación; transición en una misma sesión sin costo de activación ni duplicación de período. Regla comercial final debe quedar en términos de servicio revisados, no inferida del estado SUSPENDED existente.

## 9. GL-4: QR y links de pago, API primero

### 9.1 Separar dos productos

1. **Billing SaaS:** empresa paga la suscripción a NEO.
2. **Cobro comercial:** comprador paga una venta al comercio cliente.

Reusar abstracciones seguras cuando convenga, pero mantener beneficiario, credenciales, órdenes, contabilidad y conciliación separados. No cobrar todas las ventas con la cuenta de NEO por defecto.

Elegir **una pasarela operable para la empresa/comercio** antes de completar integración: disponibilidad contractual, monedas, liquidación, comisiones, devolución, sandbox y autenticación de webhooks. El plan no afirma que todas las pasarelas existentes soporten ya el modelo requerido.

### 9.2 Flujo propuesto

- Backend crea PaymentIntent asociado a empresa y venta/saldo, con monto/moneda calculados en servidor e idempotency key.
- Backend crea orden/enlace de checkout alojado en proveedor; persiste IDs y vencimiento antes de responder.
- API devuelve link HTTPS y metadatos QR; Web/App generan/comparten QR del link, consultan estado y muestran expiración.
- El QR es un acceso al checkout, no prueba de pago ni promesa de compatibilidad con cualquier QR bancario.
- Webhook autenticado + consulta al proveedor validan beneficiario, monto, moneda, estado final y evento no procesado.
- Registrar pago una sola vez, aplicar al saldo o venta correspondiente y conciliar con tesorería; tratar pagos parciales si se habilitan.
- La vuelta del navegador a “éxito”, captura de pantalla o lectura del QR **no confirma el pago**.
- Modelo de estados: pendiente, esperando confirmación, pagado, fallido, expirado, cancelado y reembolsado; transiciones válidas, auditoría e idempotencia.

No almacenar PAN/CVV ni construir un formulario propio de tarjeta para este alcance; usar checkout alojado y revisar obligaciones aplicables con proveedor.

### 9.3 API, Web y Android

Operaciones propuestas: crear intención/link, consultar estado, listar/filtrar, cancelar cuando proceda, recibir webhook y tramitar devolución autorizada. Rutas versionadas definitivas se documentan al implementar.

- API aplica permisos, empresa, importe y estado; no confía en precio del dispositivo.
- Web: acción en venta/cobranza, QR, compartir, consulta de estado y filtros.
- Android: mismo contrato, compartir enlace, refrescar tras retorno de checkout, recuperar operación tras cierre/app sin red.
- Token de link público opaco, limitado a consulta/pago de esa intención; nunca un JWT administrativo dentro del QR.
- Credenciales protegidas por empresa; expiración, rotación y soporte.
- Retornos/pagos no sustituyen reglas de emisión fiscal; coordinar estado comercial y fiscal sin duplicar ninguno.

### 9.4 Aceptación

Sandbox: éxito capturado, rechazo, abandono, timeout, enlace expirado, webhook falso, repetido, atrasado o fuera de orden, importe/moneda/comercio incorrectos, pago parcial, devolución y red caída.

Piloto: operación real legítima y autorizada, recepción/conciliación de fondos y devolución cuando corresponda; métricas y soporte. Solo habilitar el proveedor que complete el ciclo. Wompi/PayPal con monto cero y recepción incompleta no se consideran terminados.

Referencias de contratos, a revalidar al elegir proveedor: [PayPal eventos](https://developer.paypal.com/api/rest/webhooks/event-names/) y [MercadoPago autenticación de notificaciones](https://www.mercadopago.com.br/developers/pt/docs/your-integrations/notifications/webhooks).

## 10. Pruebas de release y responsabilidad

| Responsable | Entrega verificable |
|---|---|
| Backend/seguridad | Correcciones, contratos, pruebas unitarias/SQL/HTTP y migraciones |
| Web/Android | Capacidades, errores, sesión/empresa, pantallas y regresiones en dispositivos |
| QA | Matriz por rol/tenant/canal, evidencia de concurrencia, recuperación y proveedores |
| Operación | Servicios, TLS/proxy, custodia, backups, alertas y despliegue/reversión |
| Responsable fiscal de NEO | Datos, autorización, tipos y primera operación legítima |
| Negocio | Política de planes/pausa, pasarela, disponibilidad y aceptación de riesgos |

Evidencia mínima de una release: SHA, artefactos/hash, migraciones, configuración redactada, pruebas automatizadas, ensayos SQL, checklist firmado, backup y restauración, health, validación de roles, escenarios DTE y rollback. Archivar con acceso restringido lo fiscal o personal.

Decisiones confirmadas: este equipo Windows con app.neostp.com y api.neostp.com; primera pasarela Wompi El Salvador. Ver [corte operativo del 5 de septiembre](OPS-Windows-Dominios-Recuperacion-2026-09-05.md). Siguen pendientes disponibilidad/RTO/RPO, estrategia de base separada, política final de tiempo/cuotas durante pausa, cuentas beneficiarias y ventana de corte. Se resuelven antes de su etapa, sin bloquear ahora las correcciones técnicas confirmadas de GL-0.

## 11. Criterio final go/no-go

GO únicamente si no quedan P0 abiertos y todo P1 de una funcionalidad expuesta está cerrado o esa funcionalidad está bloqueada de forma comprobable en API/Web/Worker; además se supera la validación fiscal, SQL, arranque sin sesión, recuperación y checklist de corte.

La publicación de este plan no activa producción ni autoriza borrar datos por sí sola. No se han limpiado tablas, cambiado ambientes, instalado servicios ni emitido documentos en esta auditoría.
