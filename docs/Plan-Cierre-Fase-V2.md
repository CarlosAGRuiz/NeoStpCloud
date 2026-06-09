# Plan de cierre Fase V2 - NeoSTP Cloud

> Fecha de corte: 2026-06-09.
> Base revisada contra los ultimos commits hasta `dadba1e docs(api): documentar superficie REST`.
> Objetivo: cerrar V2 como ERP/CRM-mini vendible, API-first y listo para operar clientes reales en El Salvador.

---

## 1. Resumen ejecutivo

V2 ya cerro la base fuerte de ERP operativo: DTE, onboarding, NeoConnect, NeoProfit, backend mobile, RRHH, Tesoreria, Compras/CxP, Inventario, NeoPOS/caja y correo SMTP por empresa.

Lo pendiente para declarar V2 cerrada ya no es construir el ERP base; es completar los modulos comerciales/fiscales que faltan, endurecer contratos API y preparar escala operativa.

Siguiente sprint recomendado: **V2-C2 NeoPortal Clientes 107**.

Razon: V2-C0 quedo cerrado como contrato API-first documentado y V2-C1 quedo cerrado como backend/API NEOCRM. El siguiente bloqueo funcional para cerrar V2 es el autoservicio del receptor/cliente.

---

## 2. Estado despues de los ultimos commits

### Cerrado, no rehacer

| Area | Estado |
|---|---|
| NEORRHH 113 | Empleados, contratos, planilla quincenal, recibo PDF, exportes ISSS/AFP y API REST. |
| NEOTESORERIA 115 | Cuentas banco/caja, movimientos, resumen y UI/API. |
| COMPRAS 111 | Proveedores, facturas, pagos, CxP, integracion NeoProfit/Tesoreria. |
| INVENTARIO 110 | Existencias, kardex, costo promedio, entradas/salidas/ajustes/stock minimo. |
| Integracion inventario | Compra -> entrada, POS -> salida, anulacion POS -> devolucion, alerta stock bajo. |
| NEOPOS 102 | Ventas, tickets, impresion red, correo por empresa, promocion a DTE, sesiones/corte de caja. |
| Correo por empresa | Web y API `/api/correo`, secreto cifrado y fallback global. |
| API docs | `src/NeoSTP.Api/README.md`, OpenAPI y Scalar. |
| Seed/backfill | Empresa de pruebas activa modulos nuevos del plan al arrancar. |

### Pendientes reales para cierre V2

| Bloque | Pendiente | Prioridad |
|---|---|---|
| C0 | Contratos API, pruebas de cobertura y documentacion API | Cerrado |
| C1 | NEOCRM 114 API-first: contactos, oportunidades, etapas y actividades | Cerrado backend/API |
| C2 | NeoPortal Clientes 107: consulta/descarga DTE y estado de cuenta | Alta |
| D1 | NEOBI fiscal 105: libro IVA ventas/compras, F-07, retenciones | Alta |
| D2 | NEOCONTA 116 minima: asientos base, balanza simple, enlace fiscal | Media-Alta |
| D3 | Recordatorios automaticos de cobro y WhatsApp/email | Media - primer corte implementado |
| D4 | Conciliacion bancaria basica | Media |
| E1 | Observabilidad: OpenTelemetry + panel SaaS de metricas | Media |
| E2 | Escala: storage externo para blobs + Redis cache | Media |
| E3 | Seguridad/cumplimiento: rotacion claves, backups restaurables, retencion/borrado | Alta |
| E4 | DevEx: pipeline despliegue por entorno + seed demo reproducible | Media |
| E5 | UX: a11y/i18n y cierre de vistas legacy | Media |

---

## 3. Criterio de cierre de V2

V2 se considera cerrada cuando:

1. Build y tests pasan en `NeoSTP.slnx`.
2. Los flujos criticos tienen pruebas unitarias o de integracion:
   - login/auth/me
   - DTE emision en un paso
   - POS venta -> inventario -> corte caja -> DTE
   - compra -> inventario -> CxP -> tesoreria
   - RRHH planilla -> gasto -> tesoreria
   - NeoConnect API Key -> DTE/clientes/productos
   - portal receptor -> PDF/JSON/estado cuenta
3. Cada modulo final respeta `EmpresaId`, permisos, licenciamiento, auditoria y cuotas cuando aplica.
4. OpenAPI, `src/NeoSTP.Api/README.md`, README raiz y contexto del proyecto estan actualizados.
5. No hay secretos en repo; `appsettings.Local.json` y user-secrets cubren desarrollo.
6. Migraciones son revisadas, idempotentes y aplicables desde Api/Web.
7. UI usa `ns-*`, sin pantallas legacy bloqueantes para operacion diaria.
8. Hay runbook minimo de despliegue, backup, restore y soporte.

---

## 4. Roadmap de cierre por sprints

### V2-C0 - Consolidacion API-first y contratos

Objetivo: congelar la superficie actual antes de agregar NEOCRM/NeoPortal.

Estado 2026-06-09: **cerrado primer corte**.

Entregables:

- OpenAPI/README revisado contra controllers reales.
- Tests de contrato basicos para controllers API nuevos.
- Tests de integracion de flujos criticos actuales.
- Tabla de DTOs estables para mobile/integradores.
- `src/NeoSTP.Api/README.md` sincronizado con cambios.
- Checklist de breaking changes para futuras APIs.

Entregado:

- `docs/API-Contracts-V2-C0.md` con reglas de contrato, errores, paginacion, tenant y versionamiento.
- `src/NeoSTP.Api/README.md` actualizado con NEOCRM y recordatorios de cobro.
- `ApiContractCoverageTests` cubre que `CrmController` use `Authorize`, `RequireModule("NEOCRM")` y permisos por accion HTTP.

Alcance tecnico:

- Usar `WebApplicationFactory` para Api cuando sea viable.
- Cubrir auth, tenant, permisos y errores esperados.
- No crear nuevas tablas salvo que los tests requieran seed controlado.

Validacion:

- `dotnet build NeoSTP.slnx`.
- `dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj`.
- `dotnet test tests/NeoSTP.Tests.Integration/NeoSTP.Tests.Integration.csproj`.

Resultado esperado:

- Base API confiable para CRM, Portal, app mobile e integradores.

### V2-C1 - NEOCRM 114

Objetivo: cerrar el ciclo comercial previo a la venta.

Estado 2026-06-09: **cerrado completo (backend + API + web)**.

Entregables:

- Modulo `NEOCRM` 114 + permisos seed.
- Entidades: `ContactoCrm`, `OportunidadCrm`, `ActividadCrm`, `EtapaPipelineCrm`.
- Esquema de cotizaciones: `CotizacionCrm` y `CotizacionCrmLinea`.
- Servicio `ICrmService` con CRUD, resumen y cambios de etapa.
- API `/api/crm/*`.
- Migracion `20260609214258_V2_C1_NeoCrm`.
- Migracion `20260609223000_V2_C1_NeoCrm_Cotizaciones`.
- Documento de esquema `docs/NEOCRM-Schema-V2-C1.md`.
- Tests `CrmServiceTests` para etapas por defecto, aislamiento por empresa, cierre ganado y resumen de actividades.

Segunda entrega (cerrada):

- ✅ UI Web `Crm/*`: pipeline visual kanban por etapas (`Crm/Index`), ficha de oportunidad con
  actividades y cotizaciones (`Crm/Oportunidad`), contactos con edición inline (`Crm/Contactos`),
  actividades con completar/cancelar (`Crm/Actividades`), cotizaciones con líneas dinámicas
  (`Crm/NuevaCotizacion`) y detalle con cambio de estado + conversión (`Crm/Cotizacion`). Grupo "CRM" en el menú.
- ✅ Servicio/API de cotización formal → DTE: `ICrmService.{List,Get,Crear,CambiarEstado}Cotizacion*` +
  `ConvertirCotizacionADteAsync` (reusa `IConnectDteService.EmitirAsync`; precios IVA incluido → FC 01 directo,
  CCF 03 con cliente NRC; al procesar enlaza `DteDocumentoId` en cotización y oportunidad, marca CONVERTIDA).
  Endpoints `/api/crm/cotizaciones/*` (convertir exige `DTE.Emitir`). Permisos 414/415 `Crm.Cotizaciones.Ver/Gestionar`
  (migración `V2_C1_NeoCrm_PermisosCotizaciones`). Numeración `COT-000001` por empresa.
- ✅ Alertas para actividades CRM vencidas: derivación `ACTIVIDAD_CRM_VENCIDA` en `AlertaGeneracionService`
  (upsert por clave, corre en el `AlertaGeneracionWorker`; entra al centro de notificaciones + push).
- Tests: `CrmServiceTests` +5 (totales IVA incluido, flujo de estados, convertir éxito/fallo/duplicado),
  `AlertaGeneracionServiceTests` +1.

Reglas:

- Todo contacto/oportunidad queda por `EmpresaId`.
- Una oportunidad puede vincular `ClienteId`, `DteDocumentoId` y `CuentaCobroId`.
- Actividades vencidas generan alerta de usuario.

Validacion:

- Tests de calculo de pipeline/estado.
- Tests de servicio con aislamiento por empresa.
- Smoke API de crear oportunidad, mover etapa y registrar actividad.

Validado:

- `dotnet build NeoSTP.slnx`.
- `dotnet test tests/NeoSTP.Tests.Unit/NeoSTP.Tests.Unit.csproj --filter "CrmServiceTests|ApiContractCoverageTests" --no-build`.

### V2-C2 - NeoPortal Clientes 107

Objetivo: permitir al receptor consultar documentos y estado de cuenta sin soporte manual.

Entregables:

- Modulo `NEOPORTAL` 107 + permisos/planes.
- Tokens publicos firmados y expirables para acceso de receptor.
- Portal publico para PDF/JSON por documento.
- Estado de cuenta por cliente/receptor.
- Reenvio de DTE y enlace de pago/QR desde portal.
- API interna para generar/revocar enlaces.

Reglas:

- El token nunca permite cruzar empresa.
- El receptor solo ve documentos propios.
- El portal no expone datos internos de otros clientes.

Validacion:

- Tests de token valido/expirado/revocado.
- Tests de acceso a documento correcto vs documento de otra empresa.
- Descarga PDF/JSON con content-type correcto.

### V2-D1 - NEOBI fiscal 105

Objetivo: cerrar reportes fiscales mensuales basicos.

Entregables:

- Libro IVA ventas.
- Libro IVA compras.
- Resumen F-07.
- Retenciones/percepciones donde ya existan datos.
- Export CSV y PDF.
- UI `/Reportes` o `/NeoBI`.
- API `/api/reportes/fiscal/*`.

Fuentes:

- DTE emitidos procesados.
- DTE recibidos.
- Compras/CxP.
- Anulaciones, NC/ND y sujetos excluidos segun reglas fiscales.

Validacion:

- Tests de calculadora fiscal pura.
- Snapshots CSV/PDF smoke.
- Casos NC resta, ND suma, invalidado excluye.

### V2-D2 - NEOCONTA 116 minima

Objetivo: no construir un ERP contable completo, sino dejar base contable auditable.

Entregables:

- Modulo `NEOCONTA` 116 + permisos.
- Catalogo minimo de cuentas.
- Asientos automaticos basicos:
  - venta DTE
  - cobro
  - compra
  - pago proveedor
  - planilla
  - gasto
- Balanza simple por periodo.
- Export CSV.

Reglas:

- Cada asiento se vincula a documento origen.
- Asientos automaticos pueden anularse solo con reversa, no borrado.
- Multiempresa estricto.

Validacion:

- Tests de doble partida.
- Tests de reversa por anulacion.
- Tests de balanza por periodo.

### V2-D3 - Recordatorios automaticos y WhatsApp/email

Objetivo: reducir cartera vencida y automatizar seguimiento.

Entregables:

- Configuracion por empresa de recordatorios.
- Worker de vencimientos CxC.
- Plantillas por canal.
- Envio email usando `ITenantEmailSender`.
- Integracion WhatsApp Business API como proveedor pluggable.
- Auditoria/log de notificaciones.

Validacion:

- Tests de seleccion de facturas vencidas.
- Tests de idempotencia para no duplicar recordatorios.
- Mock provider para WhatsApp/email.

Primer corte implementado:

- Endpoint interno `POST /api/cobros/recordatorios/ejecutar` con permiso `Cobros.Gestionar`.
- Servicio `IRecordatorioCobroService` con seleccion de facturas vencidas, limite por empresa y canales email/WhatsApp.
- Log idempotente `Cobros_Recordatorios` para auditoria diaria por DTE/canal.
- Worker `RecordatorioCobroWorker` deshabilitado por defecto y configurable en `Worker:RecordatoriosCobro`.
- Correo via `ITenantEmailSender`; WhatsApp queda pluggable con `IWhatsAppSender` y proveedor mock.
- Tests unitarios de envio, omision por destinatario faltante e idempotencia diaria.

Pendiente para cerrar D3:

- Pantalla/configuracion por empresa de reglas, plantilla y frecuencia.
- Proveedor real WhatsApp Business API.
- Tests de integracion API/Worker con base relacional.

### V2-D4 - Conciliacion bancaria basica

Objetivo: conciliar movimientos de banco contra cobros, pagos y tesoreria.

Entregables:

- Import CSV de movimientos bancarios.
- Reglas de matching por monto, fecha, referencia y cliente/proveedor.
- Pantalla de conciliacion.
- Estado conciliado/parcial/no conciliado.
- Generacion o enlace a `MovimientoTesoreria`.

Validacion:

- Tests de matching exacto y ambiguo.
- Tests de anulacion/desconciliacion.
- Export de pendientes.

### V2-E1 - Observabilidad y panel SaaS

Objetivo: operar clientes reales con visibilidad.

Entregables:

- OpenTelemetry tracing/metrics con export OTLP.
- Metricas por empresa: requests, DTE/min, errores MH, webhooks, latencias.
- Panel SuperAdmin operativo.
- Health ready ampliado: DB, storage, correo, Hacienda segun config.

Validacion:

- Smoke de endpoints health.
- Verificacion de spans/metricas en ambiente local o collector mock.

### V2-E2 - Escala: storage externo y Redis

Objetivo: preparar despliegue multiinstancia.

Entregables:

- Migracion gradual de blobs grandes a `IStorageService`.
- Referencias en BD para logo/firma/scan/certificados donde aplique.
- Redis/distributed cache para catalogos/lookups/licencias.
- Invalidacion cache por cambios de catalogos/licencias.

Validacion:

- Tests storage local vs externo mock.
- Tests cache hit/miss/invalidation.
- Sin exponer secretos ni bytes sensibles en logs.

### V2-E3 - Seguridad, cumplimiento y restore

Objetivo: reducir riesgo de produccion.

Entregables:

- Rotacion de clave JWT documentada.
- Plan de rotacion de certificados/firma DTE.
- Politica de retencion/borrado por empresa.
- Backup restaurable probado.
- Auditoria ampliada para acciones criticas: permisos, licencias, claves, portal, contabilidad.

Validacion:

- Runbook de restore.
- Test/smoke de backup job.
- Checklist de secretos.

### V2-E4 - DevEx y despliegue

Objetivo: pasar de desarrollo local a releases repetibles.

Entregables:

- Pipeline de despliegue dev/staging/prod.
- Migraciones controladas por entorno.
- Seed demo reproducible.
- Script de reset demo.
- Checklist release V2.

Validacion:

- Build/test en pipeline.
- Smoke de Api/Web/Worker.
- Documentacion de rollback.

### V2-E5 - UX final, a11y e i18n base

Objetivo: cerrar deuda visual/operativa antes de V2 release.

Entregables:

- Revision de vistas legacy.
- Accesibilidad: labels, foco, contraste, navegacion teclado.
- i18n base es/en para textos principales donde sea practico.
- Componentes de grid/export/filtros guardados reutilizables.

Validacion:

- Smoke visual de pantallas criticas.
- Checklist a11y manual.
- Sin textos cortados en mobile/desktop.

---

## 5. Orden recomendado

| Orden | Sprint | Por que va ahi |
|---|---|---|
| 1 | V2-C0 API-first | Protege lo ya construido y estabiliza contratos. |
| 2 | V2-C1 NEOCRM | Abre ciclo comercial antes de portal/BI. |
| 3 | V2-C2 NeoPortal | Da autoservicio al receptor y reduce soporte. |
| 4 | V2-D1 NEOBI fiscal | Alto valor local: libros IVA/F-07. |
| 5 | V2-D2 NEOCONTA minima | Base contable sin sobredimensionar. |
| 6 | V2-D3 Recordatorios | Monetiza Cobros/CxC y mejora flujo de caja. |
| 7 | V2-D4 Conciliacion | Cierra tesoreria operativa. |
| 8 | V2-E1/E2/E3 | Escala, observabilidad y seguridad para produccion. |
| 9 | V2-E4/E5 | Release final: despliegue, demo y UX. |

---

## 6. Backlog que no bloquea cierre V2

Estos puntos pueden moverse a V2.5/V3 si el objetivo es cerrar V2 con disciplina:

- SSO/SAML enterprise.
- White label avanzado.
- Marketplace.
- SDKs oficiales NeoConnect en varios lenguajes.
- Multi-moneda avanzada y exportacion compleja.
- BI predictivo o analitica avanzada.
- App Flutter completa si se mantiene como repo externo; solo bloquearia V2 si se define V2 como release mobile tambien.

---

## 7. Dependencias y riesgos

| Riesgo | Mitigacion |
|---|---|
| Agregar CRM/Portal rompe contratos existentes | V2-C0 antes de C1/C2. |
| Fiscal/conta crece demasiado | Limitar D1/D2 a reportes y asientos minimos auditables. |
| WhatsApp introduce dependencia externa | Proveedor pluggable y mock en tests. |
| Storage/Redis complica deploy local | Mantener LOCAL/in-memory como default y externo por config. |
| Seguridad de Portal | Tokens expirables/revocables y pruebas de aislamiento por empresa/documento. |
| Deuda de docs | Actualizar README/API/contexto como parte de cada sprint. |

---

## 8. Entregable final de Fase V2

Al cerrar V2, NeoSTP Cloud debe poder demostrar:

1. Una empresa se configura, emite DTE, cobra, compra, paga, maneja inventario y vende por POS.
2. RRHH y tesoreria interna operan sin herramienta externa basica.
3. CRM crea oportunidades y las convierte en venta/DTE/cobro.
4. Clientes finales consultan documentos y estado de cuenta en NeoPortal.
5. Administracion obtiene libros fiscales basicos y balanza minima.
6. Integradores usan NeoConnect con contratos documentados y testeados.
7. SuperAdmin puede monitorear salud, uso, errores y soporte.
8. El despliegue tiene backup/restore, secretos, observabilidad y pipeline documentados.

Ese es el corte razonable para declarar **Fase V2 cerrada** y pasar a V3.
