# GL1H — contrato Wompi El Salvador y siguiente incremento

Fecha local: 2026-09-04. Decisión del usuario: Wompi como primera pasarela de Billing SaaS.
Alcance implementado/validado: [GL1H-A](GL1H-A-Checkout-Durable.md) y [GL1H-B H-04](GL1H-B-Webhook-Wompi-Durable.md). El núcleo H-05a se registra en [GL1H-C](GL1H-C-Aplicacion-Atomica-Pagos.md); H-05b se registra en [GL1H-D](GL1H-D-Cuotas-Modulos-Preflight.md). H-06 y verificación productiva siguen pendientes.

## Decisión de integración

Se adopta el enlace hospedado de un pago. La intención durable conserva el precio mensual y la
correlación; no se promete un débito recurrente automático. Wompi publica otro contrato para
[cargos recurrentes](https://docs.wompi.sv/metodos-api/cargos-recurrentes), fuera de este incremento.
`ExternalPlanId` es el identificador obligatorio del mapping comercial local: EnlacePago no crea
ni consulta un plan remoto por ese campo.

OAuth utiliza el aplicativo y API Secret en el servidor oficial de identidad. El cliente verifica
cuenta y aplicativo mediante consulta autenticada antes de crear el enlace. `ProviderAccountId`
representa `Cuenta.idCuenta`; `BeneficiaryId` representa el aplicativo `Aplicativo.idAplicativo`,
El `clientIdApi` de ese aplicativo debe corresponder al `AppId` OAuth configurado; no se exige que los dos IDs distintos sean iguales. No se obtienen estas identidades del formulario.
[Autenticación oficial](https://docs.wompi.sv/auntenticacion/autenticacion).

El enlace fija importe USD, cantidad uno y máximo un pago exitoso. Conserva la correlación local
como identificador del comercio. Se exige un ID numérico positivo, URL hospedada válida y modo de
prueba concordante. No se acepta una URL QR ni el retorno del comercio como sustituto del enlace.
[Contrato EnlacePago](https://docs.wompi.sv/metodos-api/enlace-de-pago).

No se encontró garantía documentada de idempotencia para ese POST. El transporte no reintenta métodos
inseguros ni sigue redirects. Un timeout o respuesta inválida deja la intención para conciliación.
El gate de producción es explícito en código; `Billing:Checkout:Enabled` no basta para abrir cobros reales.
Pruebas de Wompi no representan cobros reales.
[Modo de prueba](https://docs.wompi.sv/metodos-api/transaccion_prueba).

## Desglose de continuidad — H-04/H-05 locales terminados para el alcance acotado; H-06 pendiente

| Ticket | Implementación requerida | Aceptación mínima |
| --- | --- | --- |
| H-04a | Endpoint Wompi acotado, cuerpo original y un solo header `wompi_hash`; HMAC SHA256 con API Secret y comparación constante | Firma falsa, body modificado, header duplicado, payload grande: cero efecto |
| H-04b | Inbox durable con proveedor/cuenta/transacción únicos, hash y datos mínimos; snapshot de entorno de la intención | Replay simultáneo conserva un evento; cuenta/aplicativo/entorno ajenos quedan rechazados o en conciliación sin conceder acceso |
| H-04c | Consulta autenticada del enlace guardado y detalle de transacción | Enlace pertenece al aplicativo/correlación, contiene la transacción; ID, aprobación, importe y entorno coinciden |
| H-05a | Aplicar pago y período bajo lock empresa, enlazando checkout, suscripción y licencia en una transacción | Dos eventos no suman dos períodos; sandbox no activa licencia productiva; cancelación terminal no revive |
| H-05b | Snapshot de módulos/plan y reglas de renovación/downgrade/addons | Módulos y período correctos o transición bloqueada explícitamente |
| H-06a | Conciliación administrativa por consulta remota, auditada y sin POST nuevo | Recupera ACK perdido sin otra creación; no libera una intención solo por vencimiento local |
| H-06b | Outbox transaccional de notificación, reentrega y estado visible | Caída después de commit no pierde notificación ni duplica pago |
| H-07/H-08 | Contratos finales API/Web/Android; SQL concurrente, HTTP y sandbox autorizado | Estados de pago/expiración/rechazo/devolución comprobables, revisión independiente y evidencia del mismo candidato |

La firma oficial usa el cuerpo exacto y el API Secret del aplicativo. `FechaTransaccion` es fecha de
negocio; no se documentó un timestamp de entrega que permita descartar todo evento antiguo. La
notificación puede llegar tarde: autenticación, deduplicación y consulta remota deben decidir el efecto.
[Firma](https://docs.wompi.sv/webhook/validar-webhook),
[payload oficial](https://docs.wompi.sv/webhook/definicion-webhook).

El detalle de transacción no expone cuenta/aplicativo/enlace. Por ello se requiere consultar también
el enlace: `GET /EnlacePago/{id}` entrega aplicativo y transacciones; `GET /TransaccionCompra/{id}`
entrega ID, aprobación, monto y `esReal`. No asumir que `idExterno` equivale a la correlación del enlace.
[Swagger oficial](https://api.wompi.sv/swagger/v1/swagger.json).

## Configuración antes del sandbox

Cuenta y aplicativo pertenecen a las suscripciones SaaS de NEO; los cobros de los clientes/comercios
pertenecen a COBRO-01 y usan otro beneficiario. Las opciones nuevas son IDs, URLs y modo de operación;
las credenciales se aportarán por el mecanismo seguro del host, nunca se documentan en el repositorio.
No se requiere enviar secretos en el chat. Mantener `Checkout.Enabled=false` hasta preparar el entorno
sintético/aislado, el receptor de pruebas y su ventana autorizada. Una migración offline o prueba con
handler falso no equivale a certificación sandbox, despliegue o aceptación comercial.
Limitación del incremento H-03: los callbacks configurados deben usar HTTPS en el mismo origen
(success, cancel y webhook). Si Web y API se despliegan en dominios separados, antes del sandbox deberá sustituirse
esta restricción por la validación de las URLs explícitas de servidor antes de abrir el sandbox.