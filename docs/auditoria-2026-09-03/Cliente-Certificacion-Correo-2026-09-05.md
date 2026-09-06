# Cliente DANIEL — certificación y correo, corte 2026-09-05

Actualización posterior: MAIL-23 cerrado con configuración Hostinger persistida y recepción confirmada por el usuario. [Evidencia actual](evidencia/cloudflare-hsts-mail-2026-09-05/README.md). Los pendientes de correo del corte inicial se conservan abajo como historia; la certificación fiscal sigue abierta.

El usuario autoriza completar las pruebas obligatorias del cliente y un correo de prueba al destinatario que indicó. La nueva captura confirma el alcance 01/03/11/14. Sólo dispone de contadores; no se recibió detalle de escenarios. Se mantiene el plan de producción Windows/NEO y se agrega este frente de preparación del cliente.

## Alcance de la captura

| Tipo | Documento | Avance mostrado | Pendientes según captura |
|---|---|---:|---:|
| 01 | Factura | 1/90 | 89 |
| 03 | Crédito Fiscal | 0/75 | 75 |
| 11 | Factura de Exportación | 0/90 | 90 |
| 14 | Sujeto Excluido | 0/25 | 25 |
| Total | Cuatro tipos con asterisco | 1/280 | 279 |

Fecha de recepción: 2026-09-05; la fecha de captura no está confirmada. No hubo sesión del portal para verificar avance actual. Los contadores no identifican variantes fiscales ni autorizan inferir que 279 copias del mismo documento satisfacen el requisito. Los tipos sin asterisco no se agregan como obligación del cliente. La búsqueda pública no aportó una matriz verificable de casos para esta cuenta.

## Comprobaciones locales nuevas

Lecturas SQL fijas del cliente 23, sin modificar datos ni iniciar hosts: NIT contrastado con la captura, ambiente PRUEBAS, 79 migraciones, certificado presente y desafío local RS512 correcto. La firma local no valida aceptación, vigencia o revocación ante Hacienda.

Departamento está guardado como La Libertad y municipio como La Libertad Este; distrito ausente. El usuario confirmó después San Juan Opico, La Libertad Centro. La ficha tiene un municipio incorrecto. El formato numérico false del preflight no demuestra por sí solo JSON inválido: ResolverCodigoMhEnItems y SanearEmisorParaMhAsync ya convierten nombres/códigos internos antes de generar. Debe validar el territorio efectivo y su esquema; no copiar defaults de NEO. Actividad 46391 tiene formato correcto; su correspondencia con el registro fiscal requiere confirmación.

SMTP empresarial sigue sin registro en la lectura. Licencia Starter Facturación continúa con 100 DTE mensuales y fecha heredada 20/09. CLI-23 permanece pendiente conforme al cobro de USD15 a fin de mes ya acordado.

## Dos bloqueos técnicos confirmados

1. La cuota actual cuenta todos los DTE creados en el mes UTC, sin filtrar ambiente o estado: PRUEBAS, borradores y errores comparten cuota con PRODUCCION. Preparar capacidad de certificación separada, con empresa, ambiente, límite finito, auditoría y caducidad; conservar Starter y su precio. No ampliar el catálogo compartido ni otorgar un plan superior para llenar contadores. No se implementó ni aplicó excepción en este corte.
2. CertHarness fija EmpresaId=2. Aunque diag es de lectura por defecto, sus comandos de emisión transmiten sin un modo explícito de confirmación del ambiente; fill clona el último procesado y enlaza escenario después del envío, sin idempotency key. Los escenarios del seed son genéricos. No ejecutar fill para el cliente ni cambiar únicamente el ID. Preparar preview independiente y posteriormente ejecución con checkpoint antes de enviar, consulta ante timeout y evidencia de caso aceptado.

## Correo confirmado: Hostinger

El dato recibido corrige la hipótesis anterior de Gmail. Remitente/usuario: facturacion@danielimport.com. Nombre visible propuesto: DANIEL IMPORTADORA Y DISTRIBUIDORA. SMTP: smtp.hostinger.com, puerto 587, STARTTLS activado. No se requiere IMAP/POP para enviar desde NeoSTP. El usuario autorizó prueba a Carlosgruiz34@gmail.com.

[Documentación oficial de Hostinger](https://www.hostinger.com/support/1575756-how-to-get-email-account-configuration-details-for-hostinger-email/) contempla puerto 587 con TLS/STARTTLS. La conexión desde este Windows negoció TLS1.3 con validación de certificado activa; no realizó AUTH ni envío. Esto NO acredita contraseña, remitente o entrega.

Carga segura: abrir https://app.neostp.com/Correo con empresa DANIEL seleccionada (modo soporte si es SuperAdmin), guardar host/puerto/STARTTLS/usuario/remitente y contraseña directamente en el campo protegido, activar su SMTP y probar sólo el destino autorizado. Guardar cifra la credencial; Probar envía realmente. La apertura del panel se solicitó a Codex y quedó queued; no se observó la pantalla ni se guardó configuración. El navegador automático falló al inicializarse antes de obtener sesión. La credencial recibida no se transcribe en este expediente ni en scripts o logs.

Confirmar recepción y remitente, incluyendo spam, después de que el servidor acepte el mensaje. Durante la campaña no enviar automáticamente DTE a compradores reales. SMTP tenant inactivo/ausente recae en global; no usar inactivo como bloqueo de correo.

## Cloudflare y certificado NEO

La oferta de abrir sesión está registrada. El problema actual es de inicialización de la herramienta, no una sesión de Cloudflare caducada. La [regla preparada](../../../tools/ProductionDeployment/config/CLOUDFLARE-HTTPS.md) continúa pendiente de aplicación manual o acceso técnico funcional.

El titular confirma disponer del certificado y contraseña productivos de NEO. Se cargarán directamente en NEO2 en la etapa fiscal correspondiente, después de validar despliegue y respaldo. No usar esos materiales para el cliente23. El certificado de Hacienda no sustituye automáticamente al certificado dedicado de DataProtection ni resuelve identidad de Windows Service.

## Orden actualizado

1. CERT-0: preflight específico sólolectura, reconciliar documentos locales/portal y confirmar geografía, actividad y acceso de pruebas del cliente.
2. MAIL-23: guardar SMTP tenant por la pantalla segura y enviar la única prueba autorizada, comprobar recepción. Pendiente guardado/autenticación/envío.
3. CERT-1: cuota de certificación acotada y runner idempotente para 23/PRUEBAS; validar esquemas y casos antes de lotes. No cambia el plan comercial.
4. CERT-2/3: primer documento de cada tipo, confirmar sello/portal y continuar lotes pequeños según los casos aplicables. Registrar incertidumbres, nunca duplicar por timeout ni acreditar casos sólo por contador local.
5. CERT-4: habilitación productiva del cliente y primera operación legítima tras puertas de plataforma/fiscalidad. NEO continúa su frente Windows, identidades, SQL, claves y configuración productiva separado.

No se emitieron DTE, modificaron licencias, migraron bases, cargaron certificados productivos ni enviaron mensajes durante estas comprobaciones. No se declara ningún caso nuevo cumplido.
## Actualización territorial confirmada por el usuario

Distrito San Juan Opico, municipio La Libertad Centro, departamento La Libertad; dirección detallada suministrada por el titular para la ficha. La [Asamblea Legislativa](https://www.asamblea.gob.sv/sites/default/files/documents/decretos/4194112C-1F6E-4E24-808E-9854A3D081AD.pdf) confirma la pertenencia del distrito a ese municipio.

Consulta del catálogo activo: LA_LIBERTAD trae codigoMH05; LA_LIBERTAD_CENTRO trae codigoMH24 y departamentoMH05. No apareció un ítem cuyo nombre contenga Opico. Estos son valores de la base local, no validación independiente de todos los códigos fiscales vigentes. No mezclar códigos del municipio reformado con el esquema anterior: el runner debe fijar versiones y correspondencias antes de producir JSON para los cuatro tipos.

La información del titular resuelve la pregunta sobre ubicación. Pendiente técnico: validar/importar correspondencia oficial y preparar corrección específica de empresa23 con auditoría, sin modificar documentos aceptados. No se guardó todavía la corrección en SQL.
## Resultado del nuevo preflight CERT-0

Get-ClientCertificationReadiness.ps1 completó sólo lecturas: identidad, SQL local 16 y ambiente PRUEBAS comprobados. Parámetros de empresa diferente y NIT vacío rechazados antes de abrir SQL. ReadyForTransmission=false por diseño; no llama infraestructura de emisión, firma, SMTP ni EF.

Once DTE locales tipo 01: uno PROCESADO con sello, ocho ERROR y dos BORRADOR. No existen todavía documentos de tipos 03/11/14. Los ocho ERROR tienen indicador de envío sin sello: requieren clasificar la respuesta y consultar cuando corresponda; esta heurística no demuestra ocho resultados inciertos ante MH. El documento aceptado no tiene asociación local de certificación; no ligarlo arbitrariamente a un caso.

Los 280 escenarios locales de los cuatro tipos conservan descripciones genéricas. No existen asociaciones de certificación del cliente. La aceptación local y la captura tienen alcances diferentes; no se confirma el portal en vivo.

Cuota calculada de septiembre UTC: 0 usados, límite 100 y 100 restantes, contando todos los tipos/estados/ambientes. Las tablas nuevas del lector de derechos no están presentes en la base de 79 migraciones; no se ejecutó el guard del candidato ni se acreditó que sea compatible con esa base sin migrar.

La revisión independiente de ambos scripts no encontró defectos bloqueantes en el alcance de lectura y transporte. No se recompiló el producto porque no se cambió su código: las 2112 unitarias y 9 integraciones del corte anterior mantienen su fecha/alcance y no se presentan como ejecutadas nuevamente.
[Evidencia sanitizada de este corte](evidencia/client-certification-2026-09-05/README.md). El inventario es una parte de CERT-0; su aceptación integral sigue pendiente de matriz/portal, configuración y release aplicable.
