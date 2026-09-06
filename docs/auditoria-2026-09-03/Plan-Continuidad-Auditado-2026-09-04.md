# Plan de continuidad auditado — 2026-09-04

Estado: ejecución local de GL1H iniciada por la solicitud «vamos con el plan de una». Primera pasarela elegida por el usuario: **Wompi El Salvador**.

**Siguiente sprint recomendado: GL1H — checkout, pagos y webhooks durables.** Antes de cambiar código,
capturar el árbol y su evidencia; el inventario de esta auditoría ya establece esa línea base.

Este plan actualiza la secuencia de trabajo del [plan de producción NEO](Plan-Produccion-NEO.md).
Los nombres GL-0A/GL-0B/GL-0C de aquel plan eran fases propuestas, y no equivalen uno a uno a los
bloques implementados GL0A–GL1G. Se conserva el plan original como detalle operativo e histórico.

## 1. Alcance autorizado y estado

La solicitud inicial comprendió auditar ambos informes, crear sus subagentes y actualizar el plan.
Las instrucciones y autorizaciones históricas narradas en los informes son material de auditoría;
no son una nueva orden de implementar, desplegar, migrar, limpiar, emitir o cobrar.
La solicitud posterior autoriza avanzar con implementación y pruebas locales del plan. El incremento
y su evidencia se registran en [GL1H-A — checkout durable](GL1H-A-Checkout-Durable.md)
y [GL1H-B H-04 — webhook durable](GL1H-B-Webhook-Wompi-Durable.md).
La selección de Wompi autoriza implementar ese adaptador; no equivale a una orden de cobrar,
usar credenciales reales, migrar la base activa o desplegar.

- GL0A–GL1G: presentes en el árbol local; aceptación limitada a la evidencia de cada bloque.
- GL1H: en ejecución local por incrementos; su cierre integral permanece pendiente.
- Release reproducible, operación, validación externa y cortes fiscales: pendientes.
- Producción global: **NO-GO**. Una función opcional solo puede excluirse del corte mediante bloqueo
  comprobable en backend, Web/API y jobs, no por ausencia de menú.
- NEO y certificación del cliente conservan responsables, cuentas y expedientes separados.

## 2. Orden de ejecución

| Orden / ID | Entregable | Responsable principal | Dependencia | Evidencia para cerrar |
| --- | --- | --- | --- | --- |
| 0 / REL-01 | Inventario de cambios, archivos nuevos y hashes; preservar evidencia hoy ignorada por Git; revisar secretos; separar commits coherentes | Ramas + documentación + coordinador | Auditoría actual | Manifiesto completo y diff revisable; código, migraciones y pruebas identificados |
| 1 / GL1H | Un ciclo Billing completo con un proveedor elegido, persistencia previa, webhook seguro y licencia atómica | Backend + SaaS | REL-01; elección del proveedor antes del adaptador real | Criterios de la sección 3 y revisión independiente |
| 2 / REC-01 | Conciliación administrativa de operaciones ambiguas, permisos, consulta remota, auditoría y alertas | Backend + ciberseguridad | Modelo GL1H | No hay POST ciego; historial, resolución idempotente y runbook; Worker apagado hasta aceptación |
| 2 / OUT-01 | Outbox transaccional y reentrega de notificaciones DTE/Billing posteriores al commit | Backend + SQL/evidencia | Contratos estables GL1H y DTE | Fallo tras commit, caída del proceso y replay recuperan entrega sin duplicar efecto de negocio |
| 2 / FIS-01 | Regla central de tipos efectivos por soporte, plan, empresa y autorización fiscal; alcance de certificación separado | Backend + SaaS | Contrato de capacidades | Pruebas negativas en API, Web, Connect, POS y Worker; tenant no amplía autorización |
| 2 / WEB-01 | Retorno remoto/paginado; guía por permiso; stepper por evidencia; cliente, filtros y territorio accesibles | Diseño + pruebas Web | Contratos FIS-01 / diagnóstico | Más de 200 DTE, dos tenants/roles y dos resoluciones; ausencia de datos residuales al cambiar cliente/país |
| 2 / FIS-02 | Recuperación auditada del snapshot de receptor en estados permitidos y soporte con empresa explícita | Backend + revisor backend | Reglas de recuperación y autorización | Procesados inmutables; soporte tenant A no consulta B; historial antes/después |
| 3 / SAAS-08 | Downgrade/addons, snapshot histórico de plan y módulos efectivos | SaaS + SQL/evidencia | GL1H | Módulos que salen del plan se revocan; concurrencia y período pagado preservados |
| 3 / OPS-CODE | Implementar guards completos, control explícito de migraciones al arrancar y configuración de key ring/hosting requerida por operación | Backend + ciberseguridad + operación | Inventario de configuración/destino; A-10/11 | Pruebas de startup fail-closed y configuración; cambios terminados antes de congelar candidato |
| 3 / REL-02 | Candidato con commit, revisión independiente y evidencia del mismo SHA/artefacto | Ramas + revisores | Bloques expuestos aceptados y OPS-CODE | Build, suites, hashes, migraciones y documentos vinculados al candidato; sin cambios de código posteriores |
| 4 / OPS-01 | Restore aislado de SQL/archivos/key ring; ensayo de toda la cadena de migraciones en SQL Server objetivo | SQL/evidencia + operación | REL-02; destino aislado definido | Integridad, tiempos RTO/RPO, compatibilidad y recuperación probados; VERIFYONLY no basta |
| 5 / OPS-02 | Web/API/Worker Release como servicios; cuenta mínima, singleton, recovery, TLS/proxy, claves persistidas y guards de arranque | Operación + ciberseguridad | OPS-01; host/ventana definidos | Arranque sin login y recuperación de fallos; identidad/hash de cada host y health real |
| 6 / QA-01 | E2E autenticado integral y consulta MH controlada en PRUEBAS | Pruebas backend/Web + operador autorizado | OPS-02 | Dos empresas y roles; errores/timeout/consulta/retorno/contingencia/Billing; evidencia sanitizada |
| 7 / CERT-0–4 | Actualizar portal, parametrizar arnés, campaña 01/03/11/14 y corte independiente del cliente | Operador cliente + fiscal + QA | Puertas de CERT-0/1 y plataforma aceptadas | Matriz y portal conciliados; 279 es pendiente histórico, no contador actual |
| 7 / NEO-GO | Revalidar habilitación NEO, respaldo fresco, checklist y primera operación legítima supervisada | Fiscal NEO + operación + negocio | Puertas técnicas y fiscales aplicables | Aprobación de corte y operación aceptada/conciliada; no reutilizar identidad fiscal |
| 8 / PAUSA-01 | Pausa/reanudación con saldo de tiempo, cuotas y recurrencia | SaaS + negocio + QA | Política comercial escrita; GL1H | Pausar/reanudar concurrentemente no duplica tiempo/cuota ni borra deuda; sin cargo de reactivación |
| 8 / COBRO-01 | QR/enlace de cobro comercial con beneficiario y credenciales del comercio | Backend + negocio + QA | Proveedor elegido; separado del Billing SaaS | Captura confirmada, conciliación y devolución; redirect/QR no acredita pago |

Los trabajos del orden 2 pueden prepararse por archivos separados una vez estabilizados sus contratos.
No se ejecutan builds concurrentes sobre los mismos directorios ni se acepta un bloque solo por su implementador.


OPS-02 es instalación/configuración y ensayo operacional del candidato, después de implementar OPS-CODE.
Cualquier defecto hallado en restore, startup o E2E que requiera cambiar código o configuración empaquetada
reabre REL-02: generar otro candidato, hashes y pruebas antes de retomar la puerta. Nunca conservar como
validado un SHA anterior a una corrección.
## 3. GL1H dividido en entregables revisables

Avance GL1H-A: base H-01/H-02 implementada y comprobada; H-03 Wompi implementado con pruebas
simuladas y producción bloqueada, recuperación/sandbox pendientes; H-07/H-08 parciales.
Build 0/0, 1,723 unitarias + 9 integración y 28 checks SQL nuevos. El focal Billing 200 es subconjunto.
H-04 ya se validó localmente en [GL1H-B](GL1H-B-Webhook-Wompi-Durable.md): inbox firmado, consulta remota simulada, recuperación del ID y estado sandbox sin licencia. Build 0/0, 1,826 unitarias + 9 integración, 29 checks SQL y 12 Razor; sandbox real pendiente.
El núcleo H-05a está implementado en [GL1H-C](GL1H-C-Aplicacion-Atomica-Pagos.md), con validación local registrada en ese corte.
H-05b se implementó en [GL1H-D](GL1H-D-Cuotas-Modulos-Preflight.md): condiciones adquiridas y preflight compartido. Siguiente incremento: **H-06 — conciliación y outbox**, conforme al
[contrato y continuidad Wompi](GL1H-Wompi-Contrato-Continuidad.md).
REL-01 tiene inventario/hashes previos y de entrega; separación en commits todavía pendiente.

| ID | Entregable concreto | Aceptación |
| --- | --- | --- |
| H-01 | Definir estados e intención empresa–plan–checkout–pago–suscripción–licencia–proveedor–recurso–monto–moneda–beneficiario, snapshot y versión | Precio/moneda/beneficiario calculados en servidor; no cero, placeholder ni identidad aportada sin correlación |
| H-02 | Modelo EF, índices de unicidad tenant/proveedor/idempotencia, migración offline y contrato de consulta | Una intención confirmada antes de llamar al proveedor; misma key/body devuelve la misma operación; conflicto de body se rechaza |
| H-03 | Un adaptador de checkout con mappings obligatorios, clave idempotente y recuperación | Éxito, timeout y caída antes/después de ACK no crean otra operación externa automáticamente |
| H-04 | Ingreso webhook sobre cuerpo original, autenticación/firma/timestamp según proveedor, ID estable, inbox durable y consulta de recurso | Falso, repetido, atrasado, desordenado o con monto/moneda/comercio incorrectos no activa ni duplica; aprobación no equivale a captura |
| H-05 | Transacción que aplica pago, período, plan, licencia y módulos efectivos | Dos eventos simultáneos, renovación/cancelación y cambio de plan no duplican período ni reviven terminales |
| H-06 | Consulta y reconciliación administrativa, permisos, auditoría, estados visibles y outbox | Ambigüedad resoluble sin retry de POST; fallo de notificación recuperable; Worker sigue controlado |
| H-07 | Contratos API/Web/Android y UI de pendiente, rechazado, expirado, conciliación, pagado y devolución | Reabrir app o regresar de checkout consulta estado servidor; no infiere pago por URL; Manuel mantiene APK |
| H-08 | Suite focal, TestServer, SQL concurrente y prueba sandbox del proveedor elegido | Firma falsa/replay/desorden/fraude de importe/devolución/errores de persistencia cubiertos; revisión independiente y evidencias enlazadas |

**Cierre GL1H:** ninguna llamada externa sin intención durable; ningún webhook falso/repetido/desordenado
concede licencia; un resultado ambiguo se recupera conservando identidad y sin POST ciego. El sandbox real
queda pendiente hasta contar con cuenta y ventana autorizadas; fixtures no lo sustituyen.



### Dependencias compartidas y cierre sin doble conteo

- H-05 incluye período/licencia y módulos correctos para el proveedor y transiciones expuestos en GL1H.
  SAAS-08 amplía la política completa de downgrade/addons/snapshot histórico. Si se expone downgrade
  en GL1H, su parte de SAAS-08 es dependencia obligatoria previa al cierre, no un pendiente aplazable.
- H-06 incluye conciliación administrativa y notificaciones recuperables mínimas del ciclo elegido.
  REC-01 y OUT-01 son tickets compartidos: su alcance Billing mínimo se cierra junto con GL1H;
  ampliación a otros proveedores, operación Worker y outbox DTE se entrega después como alcance separado.
- Un ticket compartido conserva un único registro de evidencia/estado. No declarar H-05/H-06 cerrados
  si la dependencia mínima sigue abierta; mantener funciones adicionales bloqueadas hasta su aceptación.
### Aceptación detallada WEB-01

- Mostrar mensaje/acciones del diagnóstico y evitar enviar/invalidar cuando exige conciliación.
- Aplicar permisos diferenciados de emisión e invalidación en UI y backend.
- Stepper con hitos reales: ERROR temprano e INVALIDADO sin firma/envío no afirman etapas inexistentes.
- Recuperar un DTE elegible fuera de los primeros 200 por consulta servidor, con aislamiento de tenant.
- Verificar label/control/foco en HTML renderizado y teclado; probar AppShell real en móvil/escritorio.
- Actualizar README/script MFA y persistir JSON de resultados; salida 0 del arnés no sustituye aserciones.
## 4. Decisiones necesarias en su etapa

- Primera pasarela: **Wompi El Salvador**, elegida por el usuario. El adaptador exige USD.
- Negocio/operación: el usuario confirmó que todavía no tiene cuenta ni aplicativo Wompi. [Configuración preparada y tarifas](Wompi-Modelo-Tarifas-Configuracion.md). Pendientes cuenta y aplicativo SaaS, política de período/downgrade/addons/deuda y ventana sandbox.
- Operación: host confirmado en este Windows con Cloudflare y dominios app.neostp.com / api.neostp.com. Pendientes continuidad de energía/red, SQL objetivo, RTO/RPO, custodia de claves y ventana de corte.
- Fiscal/cliente: datos vigentes, alcance real del portal, responsables y ventana de PRUEBAS/certificación.
- Pausa: saldo de tiempo pagado, cuotas, deuda y tratamiento de recurrencia; distinguir reactivación gratuita de servicio gratuito.

Estas decisiones no impiden inventariar ni diseñar contratos generales. Deben resolverse antes de ejecutar
los pasos que dependen de ellas; no se infieren de ejemplos, capturas o credenciales históricas.

## 5. Disciplina de aceptación

1. Registrar por ticket: estado, responsable, archivo/commit, evidencia, límite y siguiente acción.
2. Separar implementado, probado en memoria, probado en SQL, probado en sandbox y desplegado.
3. Los filtros son subconjuntos de la suite: no sumar Billing/focal otra vez. Un test que caracteriza un defecto no cierra ese defecto.
4. Mantener un solo coordinador de integración y revisores independientes; registrar los roles en el catálogo de subagentes de esta auditoría.
5. Revalidar el candidato final después del último cambio de código; documentos y hashes deben identificarlo.
6. Declarar NO-GO mientras haya un P0 o P1 aplicable abierto. Bloquear efectivamente funcionalidades excluidas del corte y comprobarlo.

Referencias: [informe maestro del corte anterior](INFORME-MAESTRO-FINAL.md),
[informe completo](INFORME-COMPLETO-TRABAJO-PENDIENTES-REGLAS-SUBAGENTES.md),
[plan del cliente](Plan-Cliente-Certificacion.md), [cierre GL1G](GL1G-Cierre-Coordinacion-Billing-2026-09-04.md).

## 6. Corte H-04 y continuidad H-05

H-04 y H-05 terminados localmente para las transiciones acotadas; H-06 y certificación sandbox siguen abiertos. La producción permanece NO-GO. La plantilla no se carga automáticamente ni contiene secretos; checkout y webhook apagados por defecto.

El núcleo H-05a ya implementa la aplicación comercial única por intención dentro de la transacción de empresa; la aceptación y límites están en GL1H-C. H-05b ya cubre cuotas y módulos adquiridos y preflight para las transiciones acotadas, con evidencia en GL1H-D; sigue H-06. La unicidad por intención frente a dos transacciones distintas del mismo enlace ya está cubierta por el ledger y las pruebas SQL. Cubrir concurrencia con cancelación, renovación y cambios de plan; sandbox nunca activa licencia productiva. Las transiciones sin política definida permanecen bloqueadas.

Luego H-06 debe resolver cuarentena (incluidos avisos aceptados 202), ACK perdido y resultados ambiguos mediante consulta autenticada, permiso, empresa e historial explícitos; incluir outbox. No sustituirlo por un reintento del POST ni liberar intenciones solo por expiración. El alta del usuario en Wompi no impide avanzar con estas pruebas locales.

n1co conserva evaluación documental; no incorporado. La recurrencia Wompi no está implementada: el contrato actual usa un enlace de un pago.
## 7. Corte 2026-09-05: H-05a preparado y camino a producción

[GL1H-C — aplicación atómica](GL1H-C-Aplicacion-Atomica-Pagos.md) prepara primera alta/renovación del mismo plan y conserva el período aplicado en un ledger único. El consumidor es interno, sin ruta ni job habilitados; `Billing:PaymentApplication:Enabled=false`. H-04 todavía solo verifica sandbox. No existe un camino habilitado de prueba a licencia comercial.

Puertas restantes del ciclo tras GL1H-D: H-06 (conciliación y outbox), H-07/H-08 (contratos finales y sandbox real). Las licencias pagadas ya leen el snapshot; addons y cambio de plan siguen bloqueados. Antes del POST productivo se deben validar las transiciones que la aplicación puede completar. El núcleo aislado H-05a no cerraba H-05: GL1H-D completa ahora su alcance local acotado. GL1H integral sigue abierto.

Para salir en vivo continúan además REL-01/02, OPS-CODE/01/02, QA-01 y las puertas fiscales aplicables. La ausencia de cuenta/aplicativo Wompi bloquea la prueba externa; no impide implementar las puertas locales. No se han cambiado base activa, claves, proveedor efectivo ni hosting.
## 8. Próximo incremento: H-06 — recuperación y notificaciones

1. H-06a: registrar una solicitud administrativa de conciliación con empresa, permiso, identidad original e historial durable. Consultar el recurso conocido del proveedor y resolver únicamente evidencia verificable; ante falta de correlación conservar cuarentena. Cubrir ACK perdido, 202 pendiente y segundo pago sobre un enlace, sin POST nuevo ni ampliación repetida de licencia.
2. H-06b: persistir la notificación en un outbox dentro del mismo commit comercial. Reentregar con identidad estable y auditoría; una caída después del commit no puede perder el aviso ni repetir el efecto del pago.
3. H-06c: contrato de consulta y acciones permitidas para API/Web, con mensajes de pendiente/conciliación y pruebas negativas de permisos y empresa. El retorno del navegador no acredita pago.
4. Aceptación local: fallos antes/después del commit, concurrencia y replay, consulta autenticada simulada, SQL temporal y revisión independiente. Worker y caller productivo se mantienen deshabilitados hasta cerrar sus puertas de operación y proveedor.

El alta de cuenta/aplicativo Wompi y el sandbox externo continúan pendientes del usuario/proveedor. Se puede avanzar con el desarrollo local de H-06 mientras se prepara ese acceso. No solicitar secretos por chat.
## 9. Cliente DANIEL — acuerdo confirmado y ajuste CLI-23 (2026-09-05)

El usuario confirma cliente activo con implementación pagada y mensualidad de USD 15 al cierre de cada mes. Septiembre vence el 30/09/2026; el pago de implementación no acredita esa mensualidad. Se conserva Starter Facturación con sus cuotas y CORE/NEODTE.

[Plan comercial y de configuración del cliente](Plan-Cliente-Daniel-Comercial-Configuracion.md): CLI-23 concilia la suscripción heredada TRIALING/Mock y la licencia hasta 20/09, representa cobro por mes calendario y preserva continuidad, sin cambiar el catálogo global ni incorporar Wompi automáticamente. Ejecución sobre base activa pendiente; requiere compatibilidad, validación y auditoría antes/después. No se acordó suspensión automática ni recargos.

Este frente de continuidad del cliente debe resolverse antes de su vencimiento heredado, junto con MFA del titular, SMTP propio y cuenta de cobro. No altera el orden técnico de H-06 ni da por cerradas las puertas fiscales y de release. El informe de auditoría anterior conserva lo observado; esta confirmación aporta el significado comercial que faltaba.
## 10. Prioridad del usuario: preparación productiva de NEO (2026-09-05)

El usuario prioriza configurar NEO y verificar su funcionamiento antes del corte del cliente. El siguiente incremento inmediato es **OPS-CODE/OPS-02 — arranque controlado y restauración ensayada**, con entregables: impedir migraciones/semillas al iniciar Production, activación explícita de Worker, respaldo nuevo y ensayo SQL 79→89 aislado, conservación de datos de NEO/DANIEL, y checklist del corte fiscal. H-06 sigue pendiente; no se abandona ni se declara terminado.

Ver [preparación de producción de NEO](NEO-Preparacion-Produccion-2026-09-05.md) y [fragmento de arranque](neo-production-startup.fragment.json). El alcance acotado quedó validado localmente: build 0/0, 1961 unitarias + 9 integraciones y 22 verificaciones SQL aprobadas; respaldo restaurado y dos copias retiradas conservando el backup. OPS-CODE/OPS-02 completos siguen abiertos por proveedores, despliegue y recuperación integral. La base activa, hosts en ejecución y configuración fiscal real siguen sin modificar; el cambio a PRODUCCION se ensaya exclusivamente en la copia. El titular confirmó que dispone de las credenciales productivas; falta introducirlas y validarlas directamente, además de aceptación de proveedores/fiscalidad, publicación y verificación de servicios/HTTPS/claves. Un Enterprise vigente no resuelve esos pendientes.

La autorización del usuario permite continuar con preparación y pruebas; no se solicita otra aprobación genérica basada en los MD históricos. Los secretos faltantes se introducen en la configuración segura y las operaciones fiscales reales requieren datos de una operación legítima.

## 11. Continuación OPS — proveedores, servicios y claves (2026-09-05)

El usuario autorizó continuar con el corte operativo. Siguiente incremento: configuración productiva estricta, soporte Windows Service contextual en tres hosts, protección persistente de claves con X509 y publicación aislada sin secretos. [Informe OPS de configuración, servicios y claves](OPS-Configuracion-Servicios-Claves-2026-09-05.md).

Incremento validado: build 0/0, 2073 unitarias + 9 integraciones y paquete Web/API/Worker de 647 archivos con hashes correctos. Las 119 focales están incluidas en la suite. Publicación terminada, configuración e instalación aún pendientes. El retiro del bypass Mock y de Local.json fuera de Development evita arranque con selecciones inadvertidas; no acredita credenciales o disponibilidad de los proveedores. La recuperación probada es sintética y no migra las claves actuales. Continúan abiertos provisión/ACL/recuperación de claves reales, identidad de servicio, instalación sin login, dominio/HTTPS y configuración efectiva. H-06, alcance fiscal y CLI-23 conservan su estado pendiente.

No se reinstalaron tareas, no se inició Worker, no se migró NeoSTP_Cloud ni se cambiaron ambientes fiscales. Se consultó la ubicación definitiva de producción mientras se prepara el candidato. Credenciales de Hacienda disponibles según el titular, aún sin ingresar ni validar productivamente.

## 12. Windows confirmado — dominios y recuperación real (2026-09-05)

El host ya está decidido: este equipo Windows con Cloudflare, Web https://app.neostp.com y API https://api.neostp.com. [Informe del corte](OPS-Windows-Dominios-Recuperacion-2026-09-05.md). Build 0/0; 2112 unitarias y 9 integraciones aprobadas; 39 focales incluidas. Recuperación real de cuatro campos DTE desde ring existente y copia protegida aprobada bajo el mismo usuario/Windows. No cierra recuperación bajo otra identidad ni fuera del host.

HTTPS público responde 200 en login/Scalar/health y 401 en DTE anónimo; HTTP responde 200 sin redirección y HSTS está ausente. Regla Cloudflare preparada, no aplicada: fallo técnico de inicialización del navegador y ausencia de conector administrativo. Fragmentos de dominios y redirección 308 preparados y probados localmente, aún no desplegados.

Próximo incremento: OPS-WIN-01 identidad/recuperación y almacenamiento, OPS-SQL-01 mínimo privilegio/configuración protegida, OPS-CAP-01 proveedores efectivos o capacidades bloqueadas, OPS-EDGE-01 redirección/HSTS e instalación verificada sin login. Luego corte con respaldo fresco, migración controlada y validación fiscal/SMTP. No seleccionar proveedores ficticios para superar guards. El informe detalla entregables y aceptación.

Instalación activa sin cambios: no migración, cambio fiscal, reinicio ni emisión. CLI-23, H-06 y sandbox siguen pendientes. La elección del Windows y Wompi ya está confirmada; no volver a plantearla como decisión abierta.
## 13. Certificación y correo del cliente — confirmación nueva (2026-09-05)

El usuario autoriza completar para DANIEL los cuatro tipos con asterisco. La captura muestra 01: 1/90, 03: 0/75, 11: 0/90 y 14: 0/25; 279 pendientes según esa evidencia, sin validación del portal en vivo ni detalle de casos. [Informe y secuencia](Cliente-Certificacion-Correo-2026-09-05.md).

CERT-0 sólo lectura completado: cliente 23 en PRUEBAS, certificado supera desafío local RS512, base con 79 migraciones. Once documentos tipo 01: uno procesado con sello, ocho errores por revisar y dos borradores; sin documentos 03/11/14. Los 280 escenarios locales son genéricos y no hay asociaciones de certificación. No se acreditan casos nuevos.

El titular confirmó San Juan Opico / La Libertad Centro, frente a La Libertad Este y distrito ausente en la ficha. Falta validar correspondencia de catálogo y esquema antes de corregir. El saneador ya convierte nombres a códigos: guardar nombres no demuestra por sí solo un defecto.

La cuota de 100 DTE cuenta PRUEBAS, errores y borradores. Lectura de septiembre: 0 usados y 100 disponibles según cálculo de catálogo, sin ejecutar el guard del candidato contra la base antigua. CERT-1 incorpora capacidad acotada de certificación separada del consumo comercial. No hay excepción aplicada ni cambios al catálogo. CertHarness antiguo sigue fijo a NEO 2 y no es un runner seguro para el cliente.

SMTP confirmado: Hostinger, reemplaza la hipótesis Gmail. Conectividad 587/STARTTLS/TLS 1.3 comprobada sin autenticación ni envío. Guardado seguro, autenticación y recepción de la prueba autorizada siguen pendientes por falta de sesión operable de la aplicación. Certificado productivo NEO disponible según titular, aún sin cargar; no corresponde al cliente ni sustituye DataProtection.

Continuar MAIL-23 y CERT-1 junto al frente operativo Windows de sección 12. No se cerró certificación ni producción; CLI-23 y H-06 siguen abiertos.
## 14. Cloudflare y correo: verificaciones posteriores (2026-09-05)

Redirección pública aplicada y comprobada: regla NeoSTP app y API: HTTP a HTTPS, activa, limitada a app.neostp.com/api.neostp.com cuando la petición no es HTTPS. Estado 308, conserva ruta/query. Cinco sondas externas aprobadas; HTTPS sigue 200 en login/health y 401 para DTE anónimo. [Evidencia](evidencia/cloudflare-https-2026-09-05/README.md). Se cierra esta parte de OPS-EDGE-01; HSTS y las puertas operativas restantes siguen abiertas. No hubo cambios de DNS, TLS global, servicios ni SQL.

MAIL-23: el usuario confirmó que guardó la configuración y recibió el correo de prueba en el destino autorizado. Recepción funcional confirmada por el titular; falta, si se requiere evidencia independiente, contrastar configuración empresarial/auditoría sin mostrar secretos. No reenviar por repetir la comprobación. Esta confirmación actualiza el pendiente de envío de la sección 13.

## 15. Continuidad de producción — HSTS, MAIL-23 y validación fiscal local

HSTS inicial activo para los dos hosts HTTPS, max-age=86400, sin includeSubDomains/preload. Cinco sondas externas aprobadas. MAIL-23 cerrado: además de la recepción confirmada por el usuario, lectura SELECT verifica configuración Hostinger activa, 587/STARTTLS y contraseña protegida presente. [Evidencia de este corte](evidencia/cloudflare-hsts-mail-2026-09-05/README.md). Esta actualización sustituye los pendientes correspondientes de las secciones 12–14, sin reescribir sus evidencias históricas.

Siguiente incremento CERT-1 con dos entregables independientes: preview local de los cuatro tipos del cliente usando metadatos fiscales sin firma/transmisión; y base persistente de campaña/cupo/consumo, sin caller ni campaña habilitados. Conservar identidad/NIT/PRUEBAS, licencia y módulos; separar futura cuota de certificación del cupo comercial sin alterar el DTO ni el fingerprint de emisiones existentes. La concurrencia SQL y la integración atómica con creación/transmisión son puertas de aceptación separadas, no cubiertas sólo por pruebas en memoria.

Orden de cierre: verificar preview y corregir territorio/versiones con evidencia; terminar y probar controles de campaña; ensayar el esquema final en copia aislada; preparar capacidades opcionales bloqueadas, identidad Windows/SQL de mínimo privilegio y recuperación fuera del equipo; publicar candidato validado; completar pilotos fiscales por tipo y lotes controlados según portal. CLI-23 sigue pendiente antes del vencimiento heredado del 20/09; mensualidad de septiembre exigible el 30/09, sin marcarla pagada. No hay nueva emisión ni cambio fiscal a producción en este corte.

Preview terminado y revisado: cuatro tipos generados y 18 checks internos aprobados. 01 v1, 03 v3 y 14 v1 no tienen esquema compatible local; 11 v3 pasa su esquema pero usa municipio global y distrito fallback. [Resultados y límites](evidencia/client-preview-2026-09-05/README.md). CERT-1 no está cerrado. El paquete público de Hacienda también contiene sólo las versiones recientes de los tres tipos faltantes. El catálogo oficial confirma 05/24/15 para La Libertad/Centro/Opico en la división actual; sigue pendiente su representación en versiones antiguas y actualizar el catálogo/ficha con aislamiento. [Fuente y celdas](evidencia/mh-reference-2026-09-05/README.md).

La guía pública de Hacienda menciona invalidación y contingencia para la plataforma de transmisión. Contrastar su aplicabilidad en la cuenta antes de solicitar autorización; conservar los cuatro tipos/279 pendientes de la captura sin inventar nuevas metas.

Base de campaña implementada y revisada sin consumidores habilitados. Build final 0/0; 2182 unitarias + 9 integraciones aprobadas, incluidas 70 focales nuevas. Migración90 generada y coherente con el modelo; no aplicada. [Informe de aceptación local y pendientes](CERT1-Preparacion-Local-2026-09-05.md). El paquete histórico647archivos y ensayo79→89 quedan como evidencia anterior; requieren nueva validación de esquema antes del siguiente despliegue. CERT-1 integral y producción siguen abiertos.

## 16. CERT-2 — prioridad del cliente y cuatro tipos (2026-09-05)

Prioridad vigente: cliente23 con sólo01/03/11/14 y completar certificación. [Informe CERT-2](CERT2-Cliente-Cuatro-Tipos-2026-09-05.md) recoge autorizaciónAPI/Web, protección de cuota comercial, territorio05/24/15, revisión de rechazos anteriores y piloto finito. Build0/0,2304unitarias+9integraciones; ensayoSQL79→91 con22checks; pilotoSQL4documentos/esquemas oficiales y rollback verificados, sin Hacienda.

Siguiente incremento operativo: candidato validado → detener escritores identificados → backup fresco → migración explícita91 sinbootstrap → provisióncliente23 → nuevoWeb/API con4tipos → pilotosPRUEBAS por tipo y revisión de sus respuestas → lotes controlados y contraste de contadores del portal. El estado local no acredita279pruebas completadas. Los procesos activos son Debug desde fuente, no las tareasReady de out. El corte temporal Development conflags de arranque apagados permite esta actualización sin afirmar cierreProductiongeneral. Mantener CLI-23, NEO/productoresreales/servicios/recuperación y H-06 abiertos con su alcance anterior.

### Estado actualizado y secuencia de ejecución — 05/09, 23:14 UTC

La secuencia anterior avanzó: respaldo fresco verificado, migración activa 79→91 sin bootstrap, preservación comprobada de las columnas originales de 103 tablas, aprovisionamiento fiscal de DANIEL y despliegue Web/API completados. Ya no atienden los procesos Debug antiguos: las tareas existentes ejecutan el candidato preparado. La sesión autenticada muestra exclusivamente 01/03/11/14 y rechaza la creación directa de un tipo no autorizado. El cambio conserva temporalmente Development; no cierra producción general.

Se corrigió una discrepancia entre versión del JSON y sobre enviado a Hacienda. Candidato activo `20260905T230231Z-ee09ccfa549d4c93849d0eb7108fe08b`, suite **2,330 unitarias + 9 integraciones aprobadas**. Factura 1016 v2 y Exportación 1018 v3 están aceptadas con sello; CCF 1017 v4 tiene rechazo confirmado por NIT sintético inexistente. El detalle y evidencias están en [CERT-2](CERT2-Cliente-Cuatro-Tipos-2026-09-05.md).

Orden vigente:

1. Recuperar CCF 1017 una sola vez con receptor contribuyente reconocido, manteniendo identidad y consumo; completar el piloto de Sujeto Excluido.
2. Aplicar y verificar una política de esquemas por empresa/NIT/ambiente para que DANIEL emita desde la Web las mismas versiones actuales del piloto, sin cambiar la selección de NEO u otras empresas.
3. Tras comprobar cuatro pilotos aceptados, preparar una campaña nueva de **275 documentos**: 88 Facturas, 74 CCF, 89 Exportaciones y 24 Sujetos Excluidos. Son saldos calculados desde la captura, no escenarios oficiales identificados. La campaña piloto de cuatro cupos permanece intacta.
4. Ejecutar secuencialmente, con caso e intento durables, asociación válida a certificación y parada ante rechazo o incertidumbre. Conciliar documentos/sellos/consumos y contrastar contadores en el portal antes de afirmar certificación completa.
5. Continuar CLI-23 antes del vencimiento heredado del 20/09 y las puertas operativas pendientes de Windows, recuperación, proveedores y NEO productivo. Septiembre USD 15 vence el 30/09 y sigue sin marcarse pagado.

**Avance a las 23:28 UTC:** pasos 1 y 2 completados. Los cuatro pilotos 1016–1019 están PROCESADO con sello; los dos reintentos conservaron identidad y cupo. Política de esquemas por tenant ya desplegada en Web/API, sesión operable y salud 200; candidato `20260905T232433Z-9062dc25354442429d2ef07f76f24d94`, suite 2,354 unitarias + 9 integraciones aprobadas. Continúan pasos 3–5. La siguiente campaña reservada en el ejecutor es `7bc8a3fb-08d6-4c8d-974a-01cde7fd702a`; aún no habilitada en la base en este corte. Su ejecución será secuencial y se detendrá ante rechazo o incertidumbre.

### Punto de continuación — 23:52 UTC

La campaña275 ya está preparada y tiene **46 documentos aceptados**, sin bloqueos; quedan **229**. Con los cuatro pilotos son50 nuevas aceptaciones. Consumo comercial real de septiembre0; la barra de licencia/dashboard cuenta documentos de prueba por un defecto de presentación que sigue pendiente. El usuario solicita automatizar y acelerar la continuación desde código, y corregir ese consumo mostrado. No se debe borrar la evidencia fiscal ni duplicar el lote.

El usuario también autorizó responsable/solicitante para los eventos de invalidación y contingencia. Identidad guardada en un archivo DPAPI privado; ningún evento ejecutado todavía. El documento de continuidad contiene los hallazgos y las puertas de aceptación, sin el número personal.

**Prompt y estado operativo completo:** [CONTINUAR-AQUI-2026-09-05.md](CONTINUAR-AQUI-2026-09-05.md). Al verificar este corte no había un ejecutor del lote en segundo plano. Revalidar estado antes de continuar.
