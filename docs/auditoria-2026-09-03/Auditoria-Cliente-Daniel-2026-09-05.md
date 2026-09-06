# Auditoría de configuración — DANIEL IMPORTADORA Y DISTRIBUIDORA

Fecha de consulta: 2026-09-05, America/El_Salvador. EmpresaId **23**. Razón social confirmada en SQL: DANIEL IMPORTADORA Y DISTRIBUIDORA, SOCIEDAD POR ACCIONES SIMPLIFICADA DE CAPITAL VARIABLE.

## Alcance y resultado

Se consultó mediante SELECT la base indicada en la configuración local de la API. Las configuraciones locales de API y Web apuntan al mismo servidor y catálogo, aunque sus cadenas no son idénticas. El análisis de capacidades usa el código del repositorio. No se ha identificado el binario de un despliegue remoto ni se ha iniciado sesión como el cliente.

**Confirmado:** no existe configuración de correo propia, cuenta de cobro ni cuenta de tesorería de esta empresa. La licencia está vigente, pero su suscripción conserva un trial vencido. El administrador no tiene MFA. El ambiente fiscal almacenado es PRUEBAS.

**Matiz sobre correo:** los archivos de Web/API sí tienen SMTP global Gmail configurado, con usuario y contraseña presentes. El código recurre a ese remitente cuando falta SMTP por empresa. Por eso no se concluye que sea imposible enviar; falta independencia del remitente y prueba de entrega. No se enviaron correos.

No se modificó ningún dato empresarial, plan, permiso, credencial o ambiente. No hubo migraciones, cobros, firma, autenticación SMTP/MH, acceso al portal ni despliegue. Los secretos solo se comprobaron por presencia; no se extrajeron ni descifraron.

## Plan, vigencia y consumo

| Concepto | Configuración observada |
|---|---|
| Empresa | ACTIVA |
| Plan | STARTERFE — Starter Facturación; PlanId 207 |
| Precio de catálogo | USD 15 mensuales; no demuestra tarifa pactada ni pago recibido |
| Licencia | Id 29, ACTIVO; 20/08/2026 a 20/09/2026 |
| Usuarios | 1 de 3 |
| Sucursales | 0 de 1 |
| Puntos de venta | 0 de 2 |
| Cuota DTE | 100 mensuales; 0 documentos creados en septiembre al consultar |
| Módulos asignados | CORE y NEODTE; ambos activos e incluidos en el plan |
| Suscripción | TRIALING, proveedor Mock; trial terminó el 03/09/2026 |
| Período comercial corriente | Sin fechas CurrentPeriodStart/End |
| Pagos de suscripción | Ninguno registrado |
| Suscripción remota | No configurada |

Hay dos fechas diferentes: final del trial y final de la licencia. El resolver legacy del código local usa la licencia para conceder acceso, por lo que trial vencido no equivale automáticamente a bloqueo hoy. Se debe aclarar si se trata de cortesía, prueba extendida o servicio contratado y corregirlo por una operación administrativa trazable. La ausencia de pagos locales no demuestra que no haya pagado por fuera.

No se migró esta licencia al nuevo registro de aplicaciones de pago GL1H. No se debe habilitar Wompi ni convertir al cliente automáticamente.

## Qué tiene permitido y qué queda fuera

El único usuario es Carlos.Mena (Id 25), ADMIN activo, con rol de sistema ADMIN y **78 permisos**. Su empresa principal es la 23; no tiene membresías adicionales registradas. ADMIN no es SUPERADMIN de plataforma.

| Capacidad | Evaluación con configuración y código local |
|---|---|
| Consultar y administrar datos de empresa | Cuenta con permisos; empresa activa |
| Clientes y productos | CORE activo; permisos de lectura, creación y edición |
| Preparar/consultar DTE y descargar representaciones | NEODTE activo y permisos; emisión real sujeta a validación fiscal y entorno |
| Configurar DTE y operar certificación | Permisos presentes; la configuración sigue en PRUEBAS |
| Administrar usuarios, roles, sucursales y PV | Permisos presentes; respetando las cuotas |
| Configurar correo | CORE + Core.Correo.Configurar; no requiere comprar un módulo adicional |
| Gestionar cartera y registrar pagos manuales | CORE + Cobros.Ver/Gestionar; sujeto a reglas de documentos y saldo |
| Mostrar cuenta bancaria o generar QR/instrucciones de cobro | Capacidad en CORE, pero no hay CuentaCobro configurada |
| Tesorería y conciliación contable bancaria | NEOTESORERIA no contratado/asignado |
| POS, inventario, compras, RRHH, CRM, contabilidad, BI, Profit, ScanAI, portal, Connect y agenda | Sin asignación de sus módulos; varios permisos existen en el rol, pero no constituyen licencia |
| Eventos y contingencia como módulos separados | EVENTOSDTE y CONTINGENCIA no asignados; revisar las capacidades fiscales mínimas antes del corte, sin asumir que el plan elimina necesidades operativas |
| Facturación fiscal productiva | No acreditada: ambiente local PRUEBAS; portal/habilitación no consultados |
| Cobro bancario o con tarjeta automático | No hay configuración ni validación de ese ciclo comercial |

Los permisos del rol son más amplios que el plan. Eso puede ser válido si cada entrada aplica también licencia y empresa; ocultar el menú no sustituye ese control.

### Discrepancias de seguridad del código local

- Las rutas Web /DteRecibidos y /DteRecibidos/{id} comprueban ScanAI.Ver y empresa, pero no el módulo NEOSCANAI. El administrador tiene ese permiso, aunque no tiene el módulo. Se trata de lectura de datos de su empresa; no se demostró acceso a otra empresa ni se probó esta ruta con su sesión.
- DteDocumentosController Web comprueba permisos, pero no aplica el mismo guard de módulo que la API. La creación cuenta con validación de licencia/cuota; eso no sustituye NEODTE. Hoy el cliente sí tiene NEODTE, pero la discrepancia importa ante revocación o cambio de plan.
- Se verificó que POS, CRM, agenda, inventario y tesorería sí aplican guard de módulo en los controladores inspeccionados. No se afirma un acceso general a todos los módulos.

Estos hallazgos requieren corrección y pruebas negativas con un usuario Starter antes de garantizar que todos los módulos no contratados son inaccesibles. La auditoría no modificó controladores ni permisos.

## Correo

No hay fila en Com_ConfiguracionCorreo para EmpresaId 23. Tener correo de contacto en Core_Empresas no configura SMTP.

Web/API, según sus archivos base y locales, seleccionan Smtp, smtp.gmail.com, puerto 587 y STARTTLS; existe usuario, contraseña y remitente global Gmail. El servicio TenantEmailSender usa ese transporte si no encuentra configuración empresarial activa. No se autenticó ni verificó entrega, titularidad, reputación, cuotas o validez de esa contraseña.

Los archivos revisados del Worker seleccionan Mock y no contienen SMTP utilizable. Variables de entorno y configuración del proceso desplegado no fueron acreditadas. Si opera con esos valores y sin SMTP del cliente, puede guardar un correo simulado y devolver éxito sin entrega externa. Debe comprobarse el proceso real antes de habilitar recordatorios automáticos.

Pendiente: definir remitente del cliente, configurar SMTP/TLS y credencial mediante el mecanismo seguro, y realizar una prueba autorizada hacia destinatario controlado. Pantalla /Correo; API /api/correo. No solicitar contraseña por chat.

No existen configuración ni historial de recordatorios de cobro. Sus dos clientes registrados sí tienen correo almacenado.

## Cuentas y recepción de pagos

No hay filas en Cobros_CuentasCobro ni Tes_Cuentas de esta empresa. Tampoco hay pagos registrados en Cobros_Pagos.

Para mostrar instrucciones de transferencia y QR basta una cuenta de cobro dentro de CORE: banco, cuenta, titular y tipo, revisados con el cliente. No hace falta contratar Tesorería para esa función. Pantalla /Cobros/Cuentas; API /api/cobros/cuentas.

Tesorería lleva cuentas, saldos y movimientos internos y es un módulo distinto. Una CuentaCobro no abre una cuenta bancaria, verifica titularidad ni conecta con el banco. Un QR puede contener texto bancario o una URL; no confirma que entró dinero. Registrar manualmente un pago requiere un documento elegible, importe y saldo válidos, pero no exige una cuenta de cobro.

Una cuenta/aplicativo Wompi para ventas del comercio es otro proceso, separado de Wompi para cobrar las suscripciones de NEO.

## Configuración fiscal y datos básicos

- NIT, NRC, dirección, teléfono, correo de contacto y actividad están presentes. No se verificó su exactitud contra registros externos.
- Actividad guardada: 46391, «Venta al por mayor de productos varios». Requiere confirmar que corresponde a esta empresa; no sustituirla por un valor genérico.
- Departamento «La Libertad», municipio «La Libertad Este», distrito vacío. El código intenta traducir nombres mediante catálogos, por lo que almacenar texto no demuestra por sí solo un error.
- El generador local usa valores territoriales globales por defecto en algunas variantes de DTE cuando falta distrito. No se encontró override territorial en los archivos Web/API revisados. Hay que completar distrito y validar la dirección resultante por tipo de documento; no asumir que el default de otra localidad es correcto.
- No hay sucursal ni punto de venta registrados como entidades. Sí existe configuración fiscal CASA_MATRIZ, M001, P001. El flujo permite IDs opcionales y puede utilizar esa configuración; la ausencia de filas no equivale a imposibilidad absoluta de emitir. Conviene normalizar las entidades y su correspondencia fiscal.
- Ambiente PRUEBAS; usuario y contraseña MH protegida presentes; certificado presente.
- Fechas del certificado almacenadas: 15/08/2026 a 14/08/2031. No se verificó el certificado ni su vigencia/autorización remota.
- No hay contraseña protegida de certificado. Esto no demuestra por sí solo que no pueda firmar: depende del formato y firmador; no se ensayó firma.
- Última prueba almacenada: OK, 20/08/2026. Es histórica.
- Hay 2 clientes y 1 producto. Los 11 DTE son facturas 01 de PRUEBAS: 1 PROCESADO, 8 ERROR y 2 BORRADOR. No hay DTE de PRODUCCION en el corte.

No se consultó el portal de Hacienda ni se recalcularon matrices. El alcance de certificación anterior se conserva en su expediente; una prueba aceptada local no acredita producción.

## Seguridad de la cuenta y versión

El administrador está activo, sin bloqueo vigente y con cero intentos fallidos acumulados; último login registrado 31/08/2026. Hay hash de contraseña, pero no se inspeccionó ni probó su contraseña. MFA no está habilitado ni enrolado. No hay SSO, API keys o cuotas API específicas del cliente; su ausencia no significa necesariamente ausencia de controles globales.

El código/configuración revisados establecen mínimo 8 caracteres, mayúscula, minúscula y dígito, sin símbolo obligatorio; bloqueo tras 5 fallos por 15 minutos. Esto no acredita la fortaleza de la contraseña existente ni el comportamiento del proceso instalado.

Constan aceptaciones PRIVACY y TERMS versión 1.0 del 20/08/2026. No se evaluó contenido legal ni vigencia de términos.

La base consultada tiene **79 migraciones**, última 20260825233802_CAT020_PaisIso3166Alfa2; el candidato local fue ensayado con **89**. No se aplicó ninguna. Algunas tablas nuevas de sesión/Billing no existen en esta base. El código nuevo no debe ejecutarse contra ella sin preparar y ensayar la actualización compatible. Los resultados de tests locales no demuestran que esos cambios ya protejan la instalación del cliente.

## Orden recomendado para completar al cliente

| Prioridad | Acción concreta | Aceptación |
|---|---|---|
| Antes de actualizar | Identificar binario/configuración reales, respaldo y restauración aislada, ensayo de migraciones | Versión y base compatibles; recuperación comprobada |
| Antes de garantizar límites del plan | Corregir discrepancias de módulos Web/API y validar rol Starter | Acceso permitido a CORE/NEODTE y rechazo de funciones excluidas por URL/API |
| Alta | Conciliar trial vencido, vigencia de licencia y forma de renovación | Estado comercial y fechas coherentes, sin corte inesperado el 20/09 |
| Alta | Activar MFA para el administrador y revisar permisos del rol | Enrolamiento y recuperación verificados; sin alterar roles globales compartidos a ciegas |
| Alta | Definir y configurar correo del cliente | Mensaje autorizado entregado con remitente esperado, sin Mock |
| Alta | Registrar cuenta de cobro para transferencias | Banco/cuenta/titular revisados; instrucciones y QR correctos |
| Antes de producción fiscal | Completar distrito, validar actividad/territorio/establecimiento y campaña del cliente | Datos y tipos propios verificados, errores locales conciliados y evidencia externa vigente |
| Según operación | Registrar sucursal/PV y logo; configurar recordatorios | Configuración coherente y pruebas funcionales dentro del plan |

El alcance propuesto es completar Starter y su operación fiscal; no habilitar módulos ERP ni migrar al cliente a Wompi por defecto.

Evidencia sanitizada: [directorio de auditoría](evidencia/cliente-daniel-2026-09-05/README.md). Consulta reproducible: [Get-CompanyConfigurationReadOnly.ps1](../../tools/CompanyPreflight/Get-CompanyConfigurationReadOnly.ps1). Código de referencia: [correo](../../src/NeoSTP.Infrastructure/Comunicaciones/TenantEmailSender.cs), [cuentas/QR](../../src/NeoSTP.Infrastructure/Services/CobroQrService.cs), [licencia](../../src/NeoSTP.Infrastructure/Services/EmpresasService.cs), [DTE recibidos](../../src/NeoSTP.Web/Controllers/DteRecibidosController.cs), [territorio](../../src/NeoSTP.Application/Dte/TerritorialOptions.cs).
