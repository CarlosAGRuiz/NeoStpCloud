# CERT-1: preparación local del cliente y continuidad de producción

## Resultado y alcance

HSTS inicial quedó aplicado y verificado en los dominios públicos; correo de DANIEL guardado y prueba recibida. [Evidencia operativa](evidencia/cloudflare-hsts-mail-2026-09-05/README.md). Son cambios efectivos, distintos del código candidato que todavía no se desplegó.

El preview local generó los cuatro tipos del cliente sin persistirlos, firmarlos ni transmitirlos. Detectó el fallback territorial en FEX y ausencia local de esquemas compatibles para las versiones efectivas de Factura, CCF y Sujeto Excluido. [Detalle](evidencia/client-preview-2026-09-05/README.md). El catálogo público confirmó códigos actuales para la dirección aportada; la ficha del cliente sigue pendiente de corrección versionada. [Referencia oficial](evidencia/mh-reference-2026-09-05/README.md).

## Base de campañas implementada

Tres entidades y tablas nuevas: campaña, presupuesto por tipo y consumo vinculado a un DTE único. Migración `20260905214616_CERT1_CampaignQuotaFoundation`, sin semillas ni modificaciones a filas existentes. El modelo fuente pasa a 90 migraciones; la base activa conserva su corte de 79.

`CertificationCampaignQuotaService` es interno, sin registro DI, ruta, worker ni campaña habilitada. Requiere empresa/NIT/PRUEBAS, período UTC acotado, estado activo, presupuesto finito por los cuatro tipos, licencia vigente y módulos CORE/NEODTE. En SQL exige transacción Serializable y el mismo bloqueo por empresa de la creación DTE.

Prepara borrador y consumo juntos en el tracker; no guarda ni confirma la transacción. Comprueba que las referencias pertenecen a la empresa y no agrega entidades empresariales por navegación. La idempotencia usa scope CERT y mantiene intactos DTO/fingerprint de emisión existentes. El replay recupera el consumo, no autoriza retransmisión. Un error fiscal posterior no reembolsa el cupo automáticamente.

Esta base todavía no sustituye el cupo comercial: el guard mensual existente permanece vigente. La futura integración debe crear DTE y consumo en un mismo commit y excluir sólo documentos de prueba respaldados por el ledger autorizado. No se concedió cupo adicional al cliente ni se reclasificaron sus 11 documentos históricos.

## Revisión y aceptación

El revisor independiente encontró dos P2, corregidos antes del cierre: longitud del operador mayor que la columna DTE y aceptación de transacciones con aislamiento insuficiente. Se añadieron pruebas de borde y rechazo antes de conexión. La segunda revisión estática no detectó otros P1/P2 en el alcance interno sin consumidores.

Verificación final aprobada: build Release de toda la solución con 0 errores y 0 advertencias; 2182 unitarias y 9 integraciones, sin fallos ni omitidas. Las 70 pruebas focales de campaña están incluidas en las 2182. EF confirma que no hay cambios de modelo pendientes después de la migración; SQL 89→90 generado y revisado sin ejecutarlo. [Logs, TRX, SQL y hashes](evidencia/cert1-foundation-2026-09-05/README.md).

No utilizar esta evidencia como prueba de despliegue. La carrera SQL del último cupo, rollback, commit incierto y ensayo 79→90 permanecen pendientes. La validación histórica 79→89 y su paquete no certifican este nuevo modelo.

## Siguiente incremento

1. Corregir la selección de territorio en generación y validar el mapeo de la ficha del cliente según versión admitida; conservar el caso que reproduce el defecto como evidencia histórica.
2. Obtener/verificar esquemas compatibles con el ambiente y conciliar los ocho errores históricos sin repetir documentos por incertidumbre. Contrastar requisitos actuales del portal.
3. Probar campaña en SQL aislado, con dos escritores compitiendo por el último cupo y rollback; integrar creación/transmisión con permisos, auditoría y reconciliación antes de habilitar pilotos.
4. Ensayar migraciones 79→90 sobre copia protegida y publicar un candidato identificado después de sus verificaciones. Resolver capacidades opcionales, identidad Windows/SQL y recuperación fuera del equipo antes del corte.
5. Ajustar CLI-23 para continuidad y cobro mensual al cierre; después completar pilotos y lotes de certificación. Las credenciales productivas de NEO y DANIEL se gestionan por separado.

Producción fiscal y certificación del cliente continúan abiertas. Los 279 pendientes provienen de la captura; este incremento no añade casos aceptados por Hacienda.
