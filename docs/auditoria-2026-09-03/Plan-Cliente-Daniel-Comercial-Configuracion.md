# Cliente DANIEL — acuerdo comercial y configuración pendiente

Actualización: 2026-09-05. EmpresaId 23. Este plan incorpora la confirmación directa del usuario posterior a la auditoría; el informe y sus lecturas SQL se conservan como evidencia histórica.

Estado posterior MAIL-23: configuración Hostinger guardada y activa, 587/STARTTLS, credencial protegida presente; el usuario confirmó recepción de la prueba. [Evidencia](evidencia/cloudflare-hsts-mail-2026-09-05/README.md). El apartado de datos de correo y el cierre histórico al pie describen preparación previa; no volver a solicitar esos datos. CLI-23, MFA y cuenta de cobro conservan sus pendientes.

## Condiciones comerciales confirmadas

- Cliente comercialmente activo, con implementación pagada según confirmación del usuario. No es una prueba gratuita.
- Starter Facturación (STARTERFE), USD 15 mensuales, con CORE y NEODTE; cuotas 3 usuarios, 1 sucursal, 2 puntos de venta y 100 DTE mensuales.
- Cobro mensual al cierre de cada mes por el servicio de ese mes, no por aniversario del alta del día 20.
- Mensualidad de septiembre de 2026: USD 15 pendientes de cobro al **30/09/2026**.
- Siguientes vencimientos: 31/10/2026 y 30/11/2026; después el último día real de cada mes, usando el calendario de El Salvador.
- El pago de implementación es independiente de las mensualidades. No se conoce aquí su importe, fecha exacta o referencia; no crear un asiento con datos inventados ni aplicarlo como pago de septiembre.
- No se acordó una deuda adicional de agosto, prorrateo, recargo o plazo de gracia. Este plan no los crea.
- La actividad comercial no modifica por sí misma el ambiente fiscal PRUEBAS ni acredita autorización de Hacienda.

## Corrección operativa por implementar — CLI-23

La lectura del 05/09 mostró empresa ACTIVA, licencia ACTIVO hasta 20/09 y suscripción Mock/TRIALING con trial vencido el 03/09. Son datos que no reflejan el acuerdo comercial confirmado.

| Entregable | Resultado y aceptación |
|---|---|
| CLI-23a — estado comercial | Conciliar la suscripción como activa, preservando historial de alta y trial; no iniciar otra prueba ni alterar el plan global compartido |
| CLI-23b — calendario | Representar USD 15 por mes calendario, exigibles a fin de mes; septiembre vence el 30/09. Febrero y meses de 30/31 días deben ser correctos |
| CLI-23c — continuidad | Corregir la vigencia heredada del 20/09 para que no interrumpa el servicio antes del cierre acordado. Separar vencimiento del cobro de suspensión; política posterior a vencimiento por definir |
| CLI-23d — registro de cobro | Mensualidad pendiente hasta confirmar recepción real; aplicación única con referencia y auditoría. La implementación pagada se registra separadamente cuando existan sus datos de soporte |
| CLI-23e — compatibilidad | Ensayar actualización sobre copia aislada e identificar binario/esquema activos antes de usar el candidato nuevo en la base de 79 migraciones |

No basta sustituir TRIALING por ACTIVE o poner la fecha de cobro dentro de FechaFin: los campos actuales también controlan acceso, y el flujo manual existente renueva desde el momento de confirmación con AddMonths(1). Ese comportamiento no representa por sí solo el cobro por mes calendario acordado.

Mantener la modalidad actual sin incorporar automáticamente al cliente a Wompi ni usar el proveedor Mock como prueba de pago real. La conciliación propuesta es específica de EmpresaId 23, con auditoría y validación de identidad. No cambiar el precio, cuotas o módulos del catálogo para todas las empresas.

Este turno actualiza el acuerdo y plan de trabajo. **No se ejecutó una corrección SQL ni se cambió la fecha de vigencia en la base.** La ejecución debe comprobar estado actual, conservar evidencia antes/después y evitar duplicar mensualidades.

## MFA — responsabilidad del titular de la cuenta

El cliente debe enrolar su propio usuario desde su sesión: iniciar activación, añadir el QR o clave a su aplicación autenticadora, ingresar un código válido para confirmar y guardar los códigos de recuperación en un lugar privado.

NEO puede guiarlo. No se activa poniendo únicamente un indicador en la base, ni se configura con el teléfono del implementador. No pedirle el QR, secreto ni códigos de recuperación. Tras activación, verificar un inicio de sesión con segundo factor y que dispone de recuperación.

La ruta del código local nuevo es /Account/MfaEnrollment. La pantalla/ruta de la instalación que usa el cliente debe verificarse antes de darle instrucciones de navegación definitivas; la auditoría no identificó su binario instalado.

## Correo — datos que pedir al cliente

1. Dirección desde la que quiere enviar facturas y avisos, y nombre visible del remitente.
2. Proveedor del buzón: servicio contratado, correo empresarial o proveedor de hosting.
3. Servidor SMTP, puerto y modo de seguridad indicados por ese proveedor.
4. Usuario de autenticación SMTP, normalmente la dirección completa.
5. Credencial específica de envío o contraseña de aplicación si el proveedor la admite. El titular la introduce directamente o mediante un canal seguro de configuración; no incluirla en el chat ni en el expediente.
6. Una dirección de prueba controlada por él y autorización para enviar allí un mensaje de prueba.

El conector actual usa SMTP y autenticación por usuario/contraseña cuando existe usuario. No implementa OAuth SMTP. Primero identificar el proveedor y verificar el método que acepta; no prometer compatibilidad con cualquier buzón ni pedir la contraseña principal antes de esa revisión.

El cliente puede introducir los datos en /Correo o NEO puede configurarlos con su autorización. La contraseña se almacena protegida. La prueba debe confirmar recepción con el remitente empresarial esperado. CORE y el permiso Core.Correo.Configurar ya están disponibles para su administrador.

Mientras falte correo empresarial, el código utiliza el SMTP global si está configurado. Tener el correo de contacto en la ficha de empresa no modifica ese remitente.

## Cuenta de cobro — datos y titularidad

Para instrucciones de transferencia se requieren: banco, número de cuenta, titular exacto y nombre descriptivo. Conviene confirmar tipo de cuenta y moneda para que las instrucciones sean claras; la CuentaCobro actual no tiene campos separados para esos dos últimos datos.

El titular debe suministrar y validar los datos. Puede registrarlos él en /Cobros/Cuentas, o NEO puede hacerlo con su autorización; no es técnicamente obligatorio que él capture los campos.

No se necesita acceso a banca electrónica, contraseña bancaria, PIN, token ni códigos de autorización. Registrar estas instrucciones no permite retirar fondos ni confirma depósitos automáticamente.

La función está incluida en CORE; no requiere habilitar Tesorería. Tras guardar, revisar cuenta y titular en la vista/QR/PDF que recibiría el comprador, sin efectuar una transferencia de prueba por defecto.

## Secuencia práctica

1. NEO prepara CLI-23 para ajustar estado, calendario y continuidad sin marcar mensualidades futuras pagadas.
2. Cliente activa MFA con acompañamiento.
3. Cliente identifica su proveedor de correo y entrega datos no secretos; se acuerda cómo introducir la credencial y realizar la prueba.
4. Cliente registra o valida su cuenta de cobro.
5. NEO completa la revisión fiscal, compatibilidad y controles del plan detallados en la auditoría.

Referencias: [auditoría inicial](Auditoria-Cliente-Daniel-2026-09-05.md), [plan general](Plan-Continuidad-Auditado-2026-09-04.md), [certificación independiente del cliente](Plan-Cliente-Certificacion.md).

Actualización posterior del05/09: el buzón confirmado esHostinger, noGmail. Datos de SMTP y evidencia de transporte en [certificación y correo](Cliente-Certificacion-Correo-2026-09-05.md). La configuración fue suministrada por el titular, pero aún no se guardó ni probó autenticación/entrega en la aplicación. No volver a pedir proveedor; mantener credencial fuera de los documentos.
