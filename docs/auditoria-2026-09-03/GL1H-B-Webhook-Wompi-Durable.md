# GL1H-B — webhook Wompi durable y verificación de pruebas

Fecha del corte: 2026-09-04, El Salvador (ejecución UTC 2026-09-05). Estado: H-04 validado localmente para sandbox; no conectado a Wompi. Continúa [GL1H-A](GL1H-A-Checkout-Durable.md).

## Resultado y alcance

Se implementó H-04 para enlaces de un pago de Wompi El Salvador. `POST /api/billing/webhooks/wompi` valida el HMAC SHA256 de los bytes originales con API Secret y un único header `wompi_hash`; limita el cuerpo a 64 KiB, rechaza JSON ambiguo y verifica cuenta, aplicativo y modo. El callback no depende de un JWT del usuario.

La tabla `Billing_PaymentNotifications` conserva una recepción durable antes de cualquier consulta externa. Su identidad única incluye proveedor, cuenta, ambiente y transacción. Almacena hashes y campos mínimos; no conserva el cuerpo, tarjeta ni información personal del comprador. El hash semántico normaliza importe a dos decimales y fecha a UTC: cambios de orden, escala decimal o datos adicionales no crean un falso conflicto.

Después se consulta Cuenta, Aplicativo, EnlacePago y TransaccionCompra mediante el cliente Wompi restringido. Se cruzan identidad, correlación, transacción contenida en el enlace, aprobación, importe y entorno. OAuth es el único POST de esta verificación; los GET pueden reintentarse y ninguna operación financiera se reenvía. Respuestas excesivas, identidades incoherentes y fechas ambiguas quedan sin verificar.

La reserva usa transacción SQL y lock de transacción; la finalización usa lock de empresa, lease y rowversion. El éxito guarda `VERIFIED_SANDBOX` en la recepción y `PAYMENT_VERIFIED_SANDBOX` en la intención, recuperando el ID externo si el ACK del checkout se perdió. La consulta y el replay conservan la referencia, sin ofrecer otro enlace de pago. La vista dice «Prueba de pago verificada» y aclara que no hay cobro real ni activación de servicios.

**No se aplica pago comercial, período, suscripción, licencia ni módulos.** H-05 y H-06 continúan pendientes. Producción está bloqueada en código y ambos interruptores siguen apagados por defecto. El usuario confirmó que no tiene cuenta ni aplicativo; se preparó una plantilla sin secretos que no se carga automáticamente.

## Respuestas y recuperación

| Caso | Resultado |
| --- | --- |
| Firma/identidad inválida | 401, sin recepción ni efecto comercial |
| Payload inválido/grande | 400/413 |
| Transacción existente con semántica distinta | 409, preserva evidencia original |
| Recepción verificada repetida | 202 con la misma recepción, sin otra consulta |
| Verificación concurrente en lease vigente | 503, sin segundo verificador |
| Checkout desconocido o snapshot incompatible | 202 tras guardar cuarentena, sin consulta ni licencia |
| Consulta remota fallida/inconsistente | 503 con referencia durable; replay puede volver a consultar |
| Fallo del commit final | 503; reserva durable permite recuperar después de vencer el lease |

El 202 de cuarentena acepta custodia local, no confirma pago. H-06 debe permitir resolver esas recepciones: no hay Worker ni conciliación automática habilitados. La fecha del evento se contrasta con el checkout y el recurso remoto; no se inventa un timestamp de entrega ni se descarta todo aviso atrasado. Fechas sin offset/Z quedan pendientes de confirmar con fixtures reales del proveedor.

## Validación

- Build final Release: 0 advertencias y 0 errores.
- Suite final: **1,826 unitarias + 9 integración**, sin fallos ni omitidas. Son 103 unitarias nuevas frente al corte A; las 301 pruebas focales previas están incluidas en la suite, no se suman otra vez.
- SQL aislado: **29/29** comprobaciones y **88 migraciones** completas. Base `WompiWebhookAudit_c506c66b0427443cabde1b140c025d6e` eliminada; instancia dedicada empezó y terminó `Stopped`.
- Primer intento SQL: el fixture reutilizaba un enlace entre intenciones y la unicidad SQL lo rechazó. Se corrigió únicamente el arnés, se repitió completo y se conservó el intento fallido.
- EF offline: sin cambios pendientes respecto a la migración.
- Razor real con fixtures aisladas: **12 checks** de títulos, referencia, texto sandbox y ausencia de enlace para volver a pagar. Sin navegador ni AppShell autenticado.
- [Paquete de evidencia, TRX, SQL, logs y manifiesto](evidencia/gl1h-b-2026-09-04/README.md). Árbol local sobre HEAD `a8c5d163fb372359c4738f0c3814ef1ee14024eb`; sin commit ni despliegue. La evidencia A anterior se conserva como corte histórico.

Migración offline: `20260905043506_GL1H_WompiPaymentInbox`. Agrega únicamente el ambiente a checkout y la tabla de recepciones, restricciones, FK e índices. No se aplicó a la base activa.

Las pruebas de proveedor usan transportes simulados. TestServer verifica HTTP; SQL aislado comprueba concurrencia y rollback con verificador sintético. Ninguna de esas pruebas certifica el contrato real, desembolso o sandbox de Wompi.

## Revisión independiente

- Coordinador: modelo, receptor, API, EF, integración, vista, configuración y cierre.
- `revisor_backend`: verificador remoto y 51 casos; revisión independiente del receptor. Detectó escala decimal en hash semántico, corregida y cubierta por regresión.
- `pruebas_backend`: pruebas del receptor/HTTP y de recuperación/replay del checkout, en archivos separados del implementador.
- `sql_evidencia`: arnés relacional, cadena de migraciones, concurrencia y cleanup de base temporal propia.
- `auditor_saas`: investigación de ofertas, tarifa y requisitos; [guía Wompi](Wompi-Modelo-Tarifas-Configuracion.md).

La revisión encontró un límite que H-05 debe resolver: dos transacciones distintas del mismo enlace pueden producir dos recepciones verificadas. Cada una es evidencia legítima, pero la aplicación comercial debe ser única por intención y conciliar un pago adicional, sin duplicar período ni módulos.

## Continuidad concreta

1. H-05: aplicación comercial única y transaccional por intención, período/licencia/módulos; mantener sandbox separado; probar carreras de pago, cancelación y renovación, además de dos transacciones distintas para un mismo checkout.
2. H-06: conciliación administrativa con permiso y empresa explícitos, historial, consulta remota sin nuevo POST, tratamiento de ACK perdido/cuarentena y outbox de notificaciones.
3. H-07/H-08: completar estados de rechazado, expirado, devolución y contratos Android; abrir una prueba real de sandbox solo con cuenta/aplicativo y receptor HTTPS preparados.
4. REL-01/02 y operación: separar cambios y evidencia en candidato reproducible, validar el mismo artefacto y continuar puertas operacionales del [plan](Plan-Continuidad-Auditado-2026-09-04.md).

La recurrencia nativa mensual de Wompi es una integración adicional. El incremento actual conserva enlaces de un pago; n1co permanece únicamente evaluado y sin incorporar.