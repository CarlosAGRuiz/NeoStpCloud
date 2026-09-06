# GL1H-D — condiciones adquiridas y validación antes del pago

Fecha: 2026-09-05. Alcance H-05b: implementación y comprobación local de la primera alta, conversión de trial y renovación del mismo plan mensual Wompi. Continúa el [núcleo H-05a](GL1H-C-Aplicacion-Atomica-Pagos.md). Producción global **NO-GO**.

## Comportamiento implementado

Las licencias aplicadas por Billing leen nombre, código, cuotas y módulos del snapshot conservado por el ledger de aplicación. Editar después el catálogo no cambia esas condiciones. El lector comprueba la correlación empresa, intención, captura, pago, suscripción, licencia y período; rechaza snapshots incompletos, duplicados, mal formados o incoherentes. No convierte una condición ausente en acceso ilimitado. La convención existente de cuota cero como ilimitada se normaliza a null para todos los consumidores.

Una empresa que ya tiene ledger no puede recuperar el comportamiento legacy sustituyendo su licencia. Solo las empresas sin adopción conservan la lectura del catálogo. La renovación anticipada mantiene la licencia vigente aunque el último período añadido todavía no comience.

El resolver, los límites de recursos, dashboard, alertas y menú Web consumen las condiciones efectivas. Un módulo pagado requiere además asignación activa de la empresa, ausencia de fecha de inactivación y módulo global activo. Se conserva la revocación administrativa y el interruptor global. Una licencia no vigente deja sus módulos efectivos inactivos sin borrar las asignaciones históricas.

La misma política valida la transición antes de persistir una nueva intención y llamar al proveedor, y vuelve a comprobarla al aplicar una captura. Rechaza cambios de plan, addons, historia ambigua, estados terminales, suscripciones remotas ajenas al flujo y renovaciones cuyas cuotas o módulos actuales difieren de lo adquirido. Una operación ya reservada conserva su identidad y semántica de replay.

Asignar un plan o activar un módulo adicional no puede sobreescribir los derechos pagados. Las mutaciones administrativas usan el mismo lock por empresa que Billing. Iniciar o confirmar una transferencia manual sobre una empresa con ledger queda bloqueado antes de alterar suscripción, pago o licencia. Las operaciones pendientes también impiden cambios incompatibles.

## Verificación y revisión

Build Release: **0 errores y 0 advertencias**. Suite final: **1,931 unitarias + 9 de integración**, sin fallos ni omitidas; 61 unitarias nuevas frente a H-05a. SQL: **32/32 comprobaciones**, 89 migraciones y limpieza de la base temporal, instancia inicial/final Stopped. EF offline sin cambios pendientes del modelo. El focal previo a las dos últimas regresiones de checkout aprobó 496 casos; las dos adicionales están incluidas en la suite final.

Los resultados finales y hashes se registran en [evidencia GL1H-D](evidencia/gl1h-d-2026-09-05/README.md). El focal es un subconjunto de la suite; no se suma otra vez.

La revisión independiente identificó dos vías adicionales: transferencias manuales sobre licencias adoptadas y módulos con Activo=true pero fecha de inactivación presente. Ambas fueron corregidas y recibieron pruebas de regresión. El primer focal detectó una expectativa histórica de módulo efectivo activo tras cancelar la licencia; se corrigió para exigir inactividad efectiva y comprobar que la asignación persistida sigue intacta.

El arnés SQL utiliza exclusivamente una base temporal EntitlementAudit_GUID en la instancia de auditoría. Aplica las 89 migraciones existentes y crea capturas sintéticas mediante el procesador real. Este incremento no modifica el modelo ni añade migraciones. La comprobación de EF se ejecuta offline. Razor se compila con la solución; este corte no incluye una sesión E2E autenticada del menú.

## Límites y siguiente bloque

H-05 queda aceptado localmente para las transiciones acotadas anteriores. La política amplia de downgrade/addons SAAS-08 sigue pendiente y esas transiciones permanecen bloqueadas. El lector no migra licencias históricas al ledger ni habilita automáticamente pagos.

Sigue H-06: recuperación administrativa de ACK perdido y resultados ambiguos mediante consulta autenticada, cuarentena durable de pagos adicionales, permisos por empresa, historial y outbox transaccional con reentrega. No repetir el POST para resolver incertidumbre ni liberar una intención solo porque venció localmente.

La cuenta/aplicativo Wompi todavía no existe según el usuario. No se han usado credenciales reales ni efectuado cobros. H-04 sigue limitado a sandbox y PaymentApplication.Enabled permanece false; el consumidor comercial no tiene caller productivo habilitado. Las capturas de los tests no certifican el proveedor. Quedan H-07/H-08, sandbox real, release identificada, operación, recuperación y puertas fiscales. n1co conserva únicamente su evaluación documental.