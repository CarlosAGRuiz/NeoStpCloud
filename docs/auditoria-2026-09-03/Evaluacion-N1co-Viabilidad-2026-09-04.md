# Evaluación de viabilidad n1co para NeoSTP

Fecha: 2026-09-04. Alcance solicitado: revisar documentación e imagen de tarifas; **no incorporar n1co**.
Evaluación técnica documental por coordinador, auditor_saas y revisor_backend. Sin autenticación,
pruebas de API, cobros ni cambios de código/configuración. Wompi conserva su elección en el plan.

**Dictamen: candidato viable, condicionado a aclaraciones técnicas y comerciales.** La mejor entrada
sería checkout hospedado para un pago; las suscripciones recurrentes también tienen soporte documentado.
La documentación no basta para aceptar producción ni para afirmar que sea más económico que Wompi.

| Uso en NeoSTP | Viabilidad estimada | Condición principal |
| --- | --- | --- |
| Enlace de cobro desde Web/app | Alta para una futura prueba aislada | Confirmación servidor y recuperación de respuestas perdidas |
| Mostrar ese enlace como QR | Alta como propuesta de presentación | El QR contiene el enlace; no demuestra pago ni liquidación |
| Cobrar la suscripción SaaS de NEO | Media-alta | Vincular inequívocamente empresa, comprador, suscripción y pago |
| Renovaciones automáticas | Documentadas; integración condicionada | Eventos, cancelación y conservación del período pagado |
| Cobros de empresas clientes a sus propios compradores | Condicionada | Cuenta/credenciales/beneficiario por comercio; sucursal no equivale a empresa |
| Pausa, downgrade y prorrateo de NeoSTP | No comprobados por esta revisión | Confirmación del proveedor y política local explícita |

Estas valoraciones son conclusiones de arquitectura, no resultados de un sandbox.

CheckoutLink permite enviar importe, referencia, URLs de retorno y vencimiento; devuelve identidad de
orden y enlace. Existe consulta autenticada por código que incluye estado, total y moneda de la tienda.
Esto encaja conceptualmente con la intención durable que ya tiene NeoSTP. No encontré una garantía de
unicidad/idempotencia por referencia ni recuperación inequívoca cuando se pierde el código devuelto.
[Endpoints CheckoutLink](https://n1-docs.pages.dev/docs/checkoutlink-api/endpoints/).

La app solicitaría el enlace a nuestro backend, abriría el checkout hospedado y consultaría el estado
al volver. Credenciales y decisión de activar licencia permanecerían en el servidor. Es una propuesta
de arquitectura; no se verificó todavía el comportamiento móvil, 3DS o retorno desde navegador.
No haría falta reemplazar la base durable; sí implementar y revisar un adaptador y su procesamiento
de eventos. El contrato actual de checkout único no cubre por sí solo toda la recurrencia.

n1co documenta planes y cobros recurrentes. La API de planes devuelve un enlace hospedado de
suscripción. Es un enlace del plan, no evidencia de una sesión exclusiva de una empresa NeoSTP:
antes de automatizar licencias debe definirse una correlación que el comprador no pueda modificar.
[Introducción a suscripciones](https://n1-docs.pages.dev/docs/integration-api/epay/subscription/description/),
[Planes](https://n1-docs.pages.dev/docs/integration-api/epay/subscription/plans/).

También existe creación directa de suscripciones con métodos de pago tokenizados, consulta de detalle
/órdenes y cancelación. No quedó documentado el contrato de pausa, cambio de plan, prorrateo o cancelación
al terminar el período. No equiparar una suscripción cancelada a pérdida inmediata de acceso ya pagado.
[Suscripciones](https://n1-docs.pages.dev/docs/integration-api/epay/subscription/subscriptions/).

La API de tokenización recibe número de tarjeta y CVV. Para una integración propia habría que aclarar
la captura hospedada y los permisos del token; no copiar ejemplos que expongan un Bearer privilegiado
en el navegador. Para el primer alcance favorecería el enlace hospedado, evitando que NeoSTP reciba
los datos de tarjeta. [Métodos de pago](https://n1-docs.pages.dev/docs/integration-api/epay/paymentmethods/).

La documentación Merchant describe sucursales y operación comercial; no demuestra una plataforma
marketplace con subcomercios o liquidación dividida. Para COBRO-01, cada comercio debe conservar su
identidad y su beneficiario; no asumir que locationCode/ locationId sustituye EmpresaId.
[Merchant](https://n1-docs.pages.dev/docs/integration-api/merchant/intro/).

Hay dos familias de integración con endpoints y credenciales distintos: CheckoutLink e Integration
API v3. Deben mantenerse configuraciones independientes de pruebas/producción; no mezclar sus URLs
ni reutilizar llaves por similitud de nombres.
[Ambientes CheckoutLink](https://n1-docs.pages.dev/docs/checkoutlink-api/environment/),
[Ambientes v3](https://n1-docs.pages.dev/docs/integration-api/environment/).

La especificación v3 mejora la consulta: Orders/{id} expone identidad del comercio, pago, cargos,
devoluciones y contracargos; Store identifica el comercio autenticado. Esto permite diseñar una
verificación más completa. Aun así, hay que confirmar los permisos disponibles para nuestra cuenta y
la semántica de captura: pago aprobado y liquidación bancaria son hitos distintos. Refunds recibe orden
y motivo; no se documentó un importe parcial en ese contrato.
[Especificación pública v3](https://api.n1co.com/swagger/v3/swagger.json),
[Pagos y devoluciones](https://n1-docs.pages.dev/docs/integration-api/epay/payment/payments/).

Para EPay directo, la guía 3DS usa iframe/postMessage y confirmación posterior con authenticationId.
Hay referencias de dominio distintas entre ejemplo y validación; soporte móvil y dominios por entorno
necesitan confirmación. Esto no demuestra una limitación del checkout hospedado, que es otro recorrido.
[Guía 3DS](https://n1-docs.pages.dev/docs/integration-api/authentication3ds/).
La revisión del webhook encontró una inconsistencia concreta: la guía prescribe HMAC SHA256, pero
el ejemplo Node utiliza createHash y Base64; Python/PHP calculan HMAC hexadecimal. Debe obtenerse un
vector firmado válido y confirmar el formato real. Esto señala un defecto de ejemplos; no demuestra
que el mecanismo del proveedor sea inseguro. Los eventos distinguen pago, reversión y autenticación
3DS: autenticar una tarjeta no equivale a cobrar. La idempotencia, eventos repetidos/desordenados y
consulta remota siguen siendo responsabilidades del flujo local.
[Webhook oficial](https://n1-docs.pages.dev/docs/developer-tool/webhook/).

**Tarifas aportadas por el usuario.** La imagen indica 1.98% para tarjetas Agrícola, 2.30% Visa de otros
bancos, 3.75% Mastercard de otros bancos y $0.15 de servicio 3DS por transacción; las comisiones no
incluyen IVA. No aparece fecha de vigencia, identificación del comercio ni alcance API/suscripciones.

| Tarjeta según imagen | Comisión porcentual | Costo estimado cobro $25 | Costo estimado cobro $100 |
| --- | --- | --- | --- |
| Agrícola | 1.98% | $0.65 | $2.13 |
| Visa otros bancos | 2.30% | $0.73 | $2.45 |
| Mastercard otros bancos | 3.75% | $1.09 | $3.90 |

Cálculo ilustrativo: importe × tasa + $0.15, suponiendo que ese cargo 3DS aplica una vez. Valores
redondeados a centavos, **antes de IVA y otros cargos**; no son liquidaciones netas. Confirmar el IVA
aplicable a cada concepto y redondeo de liquidación. El costo fijo pesa proporcionalmente más en pagos
pequeños. La mezcla real de bancos/marcas determina el costo promedio de NeoSTP.

La página comercial pública remite las comisiones a la app; los términos describen tarifas y 3DS
negociados con el comercio. Por tanto, la imagen sirve como propuesta para analizar, no como tarifa
universal verificada ni prueba de que incluya recurrencia API.
[Producto n1coLinks](https://n1co.com/n1cobusiness-sv-n1colinks/),
[Términos comerciales](https://n1co.com/terminos-y-condiciones-business/).

Preguntas concretas para n1co, preparadas para revisión; **no enviadas**:

1. ¿La cotización aplica a El Salvador, CheckoutLink API y suscripciones recurrentes? ¿El cargo 3DS
   se cobra por intento, transacción exitosa o liquidación, y qué impuestos/cargos adicionales aplican?
2. ¿Qué plazos de abono, reservas, retenciones, contracargos y tratamiento de comisiones en devoluciones
   corresponden al comercio NEO?
3. ¿Existe clave idempotente o garantía única de orderReference? ¿Cómo recuperar una creación aceptada
   cuya respuesta se perdió, sin volver a crear/cobrar?
4. ¿Cuál es la firma exacta del webhook? Facilitar vector cuerpo/header/clave de prueba, política de
   reentrega, ID estable del evento y semántica de pago capturado/reversión.
5. ¿Cómo vincular el enlace hospedado de suscripción con una intención y empresa específicas sin usar
   correo ni campos editables como prueba de identidad?
6. ¿Qué endpoints permiten cancelación diferida, pausa/reanudación, cambio de plan y prorrateo?
7. ¿Cómo se autorizan y liquidan comercios independientes cuando NeoSTP genera sus enlaces?

Recomendación: conservar n1co como candidato y solicitar estas aclaraciones antes de decidir una
prueba aislada. No sustituir Wompi ni abrir un segundo adaptador durante esta revisión. El siguiente
bloque ya previsto sigue siendo GL1H-B: webhook verificado, aplicación atómica de pago/licencia y
conciliación; esta evaluación no acredita avance de implementación de ese bloque.