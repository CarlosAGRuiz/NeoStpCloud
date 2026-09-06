# Wompi El Salvador: modelo, tarifas y preparación de la API

Revisión documental: 2026-09-04, hora local de El Salvador. Alcance: fuentes públicas oficiales y opciones públicas del repositorio. No se inspeccionaron secretos ni cuentas, no se autenticó contra el proveedor, no se ejecutaron cobros y no se activó producción.

## Decisión para NeoSTP

Wompi se mantiene como primera pasarela de Billing SaaS. El incremento GL1H usa un enlace hospedado de un pago para cobrar un período de servicio; ese flujo no es todavía una domiciliación mensual. El dinero de las suscripciones de NeoSTP corresponde a NEO como comercio. Los cobros de ventas de las empresas clientes son otro módulo, con su propia identidad y beneficiario.

El detalle técnico y las puertas de continuidad están en [GL1H-Wompi-Contrato-Continuidad.md](GL1H-Wompi-Contrato-Continuidad.md). Este documento prepara la configuración; no certifica que exista una cuenta Wompi real conectada ni sustituye evidencia de sandbox.

## Qué ofrece Wompi

El sitio oficial publica enlaces/QR, botones, API, plugins y POS. Los compradores pueden pagar con Visa/Mastercard; existen alternativas de puntos, cuotas y Bitcoin según configuración. La API permite checkout hospedado o integración directa. Para NeoSTP se prioriza checkout hospedado: la captura de tarjeta ocurre en Wompi. Un QR solamente abre el enlace, no acredita pago. [Sitio oficial](https://wompi.sv/), [API Wompi](https://wompi.sv/NuestrosServicios/API).

Wompi también dispone de `POST /EnlacePagoRecurrente`: permite definir monto y día mensual, y devuelve una URL para que el comprador se afilie y acepte el servicio. Es un contrato separado del enlace de un pago y queda fuera del adaptador GL1H-A actual. No asumir que cambiar la URL convierte un pago único en suscripción automática. [Cargos recurrentes](https://docs.wompi.sv/metodos-api/cargos-recurrentes).

Los términos de usuarios contemplan autorización previa de cargos recurrentes, renovación y cancelación. Si falla el cobro, describen intentos cada cuatro horas el día acordado y el siguiente. La deuda, suspensión y relación de servicio son responsabilidad del comercio y su cliente. NeoSTP debe definir la política de acceso sin inventar una extensión de período por un intento de cobro. [Términos de usuarios, actualización publicada 10/04/2025](https://wompi.sv/TerminosCondiciones/Usuario).

## Cuánto se paga

| Concepto | Publicación oficial y límite de interpretación |
| --- | --- |
| Comisión ordinaria | El sitio anuncia **3.50% para compras normales**. No se presenta como cotización particular de la cuenta de NEO. |
| Alta y operación | La portada anuncia cuenta gratuita, sin costo de implementación y sin cuota mensual de manejo. Tarifas indica sin membresía. |
| Cuotas | Hay condiciones especiales para cuotas de tarjetas Banco Agrícola; no se aplica automáticamente el 3.50% a todos los plazos. |
| Recurrencia | No se encontró tarifa pública diferenciada y concluyente para nuestro futuro contrato de cargos recurrentes. Confirmar su comisión y habilitación comercial. |
| 3DS | La FAQ indica incorporación automática. No se encontró en las fuentes revisadas un precio separado verificable por autenticación 3DS; tampoco se concluye por ello que todos los contratos la incluyan gratuitamente. |

Fuentes de precios: [Portada](https://wompi.sv/), [Tarifas y simulador](https://wompi.sv/tarifas), [FAQ 3DS](https://www.wompi.sv/soporte/faq).

### IVA, retenciones y ejemplo orientativo

La calculadora oficial distingue IVA del 13% sobre la comisión, anticipo del 2% de IVA y otras retenciones; pide condición de contribuyente y advierte que el resultado es aproximado. Su etiqueta de comisión dice IVA incluido, mientras que los términos indican que la tarifa no incluye impuestos u otras deducciones legales. Esta presentación no permite fijar el neto final de NEO sin verificar su configuración fiscal y liquidación contractual. [Calculadora](https://wompi.sv/tarifas), [Términos del comercio](https://wompi.sv/TerminosCondiciones/Comercio).

Ejemplo aritmético, no liquidación: sobre una venta de USD 100, el 3.50% representa USD 3.50. **Si** a esa comisión se añade IVA del 13%, comisión más IVA sería USD 3.955 (aproximadamente USD 3.96). El resultado no incluye retenciones, ajustes ni condiciones especiales. No prometer recibir USD 96.50 o USD 96.04/96.05 como neto contractual.

Para presupuestar: separar importe bruto cobrado, comisión del proveedor, impuestos/retenciones y neto recibido. No almacenar la comisión como un impuesto del producto ni descontarla de la licencia pagada: el cliente paga el importe comercial completo; la liquidación del adquirente es otro movimiento contable.

## Modelo de cuenta y desembolso

En el modelo agregador, Wompi procesa pagos y desembolsa el neto después de descuentos. La cuenta de depósito debe pertenecer al mismo titular registrado como comercio y ser de Banco Agrícola; puede haber varias cuentas asignadas a aplicativos. Esto no demuestra soporte marketplace ni permiso para recibir ventas de terceros como si fueran ingresos de NEO. [Términos del comercio](https://wompi.sv/TerminosCondiciones/Comercio).

La comunicación comercial indica abonos diarios/día siguiente; la FAQ precisa que el primer desembolso generalmente ocurre después de un día hábil desde la primera transacción. No equivalen a liquidación instantánea ni garantizan todos los días calendario. Confirmar corte, feriados y retenciones con Wompi para la cuenta contratada. [Tarifas](https://wompi.sv/tarifas), [FAQ](https://wompi.sv/soporte/faq).

El modelo **Payment Gateway** tiene contrato de afiliación con Banco Agrícola: las reglas de tarifas, liquidación, impuestos y controversias dependen de ese acuerdo. No aplicar la tarifa agregadora como precio universal de Gateway. [Condiciones Gateway](https://wompi.sv/TerminosCondiciones/Gateway).

## Requisitos comerciales y credenciales

La FAQ actual pide registro completo, cuenta de depósitos Banco Agrícola habilitada para abonos/cargos, información bancaria actualizada y representación legal vigente para empresas. Una página antigua de soporte todavía menciona seis meses de antigüedad; no aparece en la FAQ actual. Confirmar el requisito aplicable durante el alta, sin tratar la página antigua como condición vigente universal. [FAQ actual](https://www.wompi.sv/soporte/faq), [soporte anterior](https://wompi.sv/Soporte/SoportePyR).

En el panel, el detalle del aplicativo proporciona **APP ID** y **API Secret**. OAuth usa `client_id`, `client_secret`, `grant_type=client_credentials`, `audience=wompi_api` contra `https://id.wompi.sv/connect/token`; la API recibe Bearer. Las credenciales deben introducirse mediante el mecanismo seguro del host; no enviarlas en el chat, Markdown, Git o logs. [Autenticación oficial](https://docs.wompi.sv/auntenticacion/autenticacion).

## Configuración preparada, sin secretos

Usar el aplicativo de NEO para Billing SaaS y mantenerlo en **desarrollo** durante las pruebas. Wompi usa los mismos orígenes oficiales; el modo depende del aplicativo. `EsProductiva=false` en una notificación representa una prueba sin cobro real. [Modo de prueba](https://docs.wompi.sv/metodos-api/transaccion_prueba).

| Clave NeoSTP | Valor o procedencia |
| --- | --- |
| `Billing:Wompi:BaseUrl` | `https://api.wompi.sv` |
| `Billing:Wompi:IdUrl` | `https://id.wompi.sv` |
| `Billing:Wompi:AppId` | APP ID para autenticación, introducido de forma segura |
| `Billing:Wompi:ApiSecret` | API Secret, únicamente almacén seguro/configuración local protegida |
| `Billing:Wompi:WebhookEnabled` | `false` hasta preparar el receptor y la ventana de pruebas |
| `Billing:Wompi:IsProduction` | `false` para el alcance actual; producción tiene guard explícito en código |
| `Billing:Wompi:CheckoutWebhookUrl` | URL HTTPS del receptor Wompi de pruebas cuando esté disponible y verificado |
| `Billing:Checkout:Enabled` | Mantener `false` hasta completar preparación y ventana de sandbox |
| `Billing:Checkout:Provider` | `Wompi` al preparar el entorno autorizado |
| `Billing:Checkout:ProviderAccountId` | `Cuenta.idCuenta`, obtenido de consulta autenticada autorizada |
| `Billing:Checkout:BeneficiaryId` | `Aplicativo.idAplicativo`; su `clientIdApi` debe coincidir con APP ID OAuth |
| `Billing:Checkout:SuccessUrl` / `CancelUrl` | URLs HTTPS controladas por NeoSTP, validadas según el contrato implementado |

No inventar `ProviderAccountId` ni suponer que es la cuenta bancaria. No asumir que `idAplicativo` siempre es igual al APP ID de OAuth: el contrato local valida `clientIdApi`. Los IDs empresariales se obtienen/verifican con los endpoints de identidad durante el sandbox autorizado. [Swagger](https://api.wompi.sv/swagger/v1/swagger.json).

Para la firma de webhook se usa el **API Secret del aplicativo**, HMAC-SHA256 del cuerpo original y header `wompi_hash`. La propiedad legacy `WebhookSecret` no convierte el protocolo en otro esquema. No activar licencias desde redirects; verificar el enlace, transacción, monto y entorno. [Firma oficial](https://docs.wompi.sv/webhook/validar-webhook).

### Orden de trabajo

1. H04 validado localmente: inbox durable y verificación del webhook/consulta remota con dobles y SQL sintético; ver [cierre H04](GL1H-B-Webhook-Wompi-Durable.md).
2. Completar H05: pago, período y licencia atómicos, con reglas claras para sandbox y cancelaciones.
3. Completar H06: conciliación administrativa y notificaciones recuperables.
4. Preparar configuración segura, identidad de cuenta/aplicativo y receptor HTTPS de pruebas.
5. Ejecutar sandbox autorizado y conservar evidencia sanitizada de cada transición.
6. Revisar tarifa efectiva, IVA/retenciones, cuenta de liquidación y requisitos de producción antes de considerar la apertura real.

La escritura de esta guía no registra cuentas, no habilita una pasarela y no prueba una transferencia bancaria ni una captura. La ejecución técnica restante se registra en el plan GL1H; la emisión fiscal y las ventas multiempresa conservan sus puertas independientes.
## Preparación realizada en esta ejecución

El usuario confirmó que todavía no tiene cuenta ni aplicativo. Se preparó la
[plantilla sandbox](config/wompi.sandbox.example.json), sin datos inventados ni secretos. No se carga
automáticamente ni modifica el proveedor por defecto. `Checkout.Enabled` y `Wompi.WebhookEnabled`
permanecen false. Los IDs, URLs y secreto vacíos impiden una conexión accidental.

La ruta del receptor implementado para probar localmente es `POST /api/billing/webhooks/wompi`.
La URL completa se definirá con el dominio HTTPS real de API; no se supone una dirección pública.
Para el primer sandbox, los callbacks deben satisfacer la validación de origen del adaptador actual.
El alta comercial y aplicativo se realizan en el panel de Wompi; después se introducen credenciales
por configuración protegida del host y se verifican cuenta/aplicativo en una ventana autorizada.
No pegar API Secret en este documento ni en el chat. No hay proveedor real conectado todavía.