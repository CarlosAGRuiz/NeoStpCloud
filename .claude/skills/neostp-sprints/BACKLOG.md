# Backlog operativo por sprints — NeoSTP Cloud / NeoSTP Business Suite

> Documento maestro de planificación. Estado base capturado el 2026-05-31.

## Estado base

- Proyecto: NeoSTP Cloud Web
- Rama: main
- Sprint actual ya entregado: Sprint 12
- Build: Verde
- Tests: 106/106 pasando
- Stack: .NET 10, ASP.NET Core MVC/Razor, Web API, SQL Server 2022, EF Core 10, Worker Service, Serilog, QuestPDF, MailKit, Polly, JWT/Cookies, DataProtection.

El sistema ya cuenta con:

- Core administrativo
- Empresas
- Usuarios
- Roles
- Permisos
- Planes
- Módulos
- Licenciamiento
- Sucursales
- Puntos de venta
- Clientes
- Productos
- Configuración DTE
- Firma RS512
- Transmisión real a Hacienda
- PDF con QR
- Correo
- Dashboard empresa/SuperAdmin
- Worker de contingencia
- Worker de limpieza de tokens
- Empresa de pruebas automática
- Toggles Mock/Real

Objetivo: pasar de sistema DTE funcional a plataforma SaaS completa, certificable, vendible y escalable.

---

# Sprint 13 — Catálogos MH y mantenimiento de catálogos

## Objetivo
Crear la base sólida para manejar todos los catálogos MH e internos desde UI/API, eliminando hardcodeos y permitiendo importar, editar, versionar y auditar catálogos sin recompilar.

## Justificación
El sistema ya tiene catálogos parciales, pero hay deuda técnica con CAT-008, CAT-013, CAT-014, CAT-015, CAT-019, CAT-020, CAT-024 y otros. Para completar la certificación DTE y mantener la plataforma a futuro, los catálogos deben administrarse desde un módulo formal.

## Alcance
- Extender Core_Catalogos y Core_CatalogoItems.
- Agregar soporte para versión, parent, metadata y estado activo.
- Crear CRUD de catálogos.
- Crear CRUD de ítems.
- Crear importación CSV/Excel/JSON.
- Crear exportación CSV/Excel/JSON.
- Crear cascadas territoriales.
- Crear mapeos internos hacia códigos MH.
- Auditar cambios.

## Tareas técnicas
1. Extender entidad Core_Catalogo.
2. Extender entidad Core_CatalogoItem.
3. Agregar campos: Version, ParentCodigo, MetadataJson, EsSistema, Activo, CreatedBy, UpdatedBy.
4. Crear migración EF.
5. Crear CatalogoAdminService.
6. Crear endpoints:
   - GET /api/catalogos
   - POST /api/catalogos
   - PUT /api/catalogos/{codigo}
   - GET /api/catalogos/{codigo}/items
   - POST /api/catalogos/{codigo}/items
   - PUT /api/catalogos/{codigo}/items/{id}
   - DELETE /api/catalogos/{codigo}/items/{id}
   - POST /api/catalogos/{codigo}/import
   - GET /api/catalogos/{codigo}/export
   - GET /api/catalogos/{codigo}/items?parent=
7. Crear vistas: /Catalogos, /Catalogos/Details/{codigo}, /Catalogos/Import.
8. Crear permisos: Core.Catalogos.Ver, Core.Catalogos.Administrar, Core.Catalogos.Importar.
9. Crear seed inicial de catálogos MH prioritarios.
10. Agregar pruebas unitarias del importador.

## Catálogos prioritarios
- CAT-001 Ambiente
- CAT-002 Tipo Documento
- CAT-005 Tipo Contingencia
- CAT-008 Distrito
- CAT-009 Tipo Establecimiento
- CAT-012 Departamento
- CAT-013 Municipio
- CAT-014 Unidad de Medida
- CAT-015 Tributos
- CAT-016 Condición Operación
- CAT-017 Forma Pago
- CAT-019 Actividad Económica
- CAT-020 País
- CAT-022 Tipo Documento Identificación
- CAT-024 Motivo Invalidación

## Criterios de aceptación
- Build verde.
- Tests existentes siguen pasando.
- Se pueden crear/editar/inactivar catálogos.
- Se pueden importar/exportar ítems.
- Departamento → Municipio → Distrito funciona en cascada.
- Los catálogos del sistema no se eliminan físicamente.
- Cambios auditados.
- Sistema sin hardcodeos para valores territoriales básicos.

---

# Sprint 14 — Certificación DTE: matriz, progreso y escenarios

## Objetivo
Construir el módulo operativo para controlar la matriz de pruebas de Hacienda, visualizar avances, registrar pruebas, generar documentos de prueba, diagnosticar errores y saber cuándo se puede solicitar autorización.

## Alcance
- Crear tablas de certificación.
- Crear matriz por tipo DTE/evento.
- Crear pantalla de progreso.
- Asociar documentos enviados a escenarios.
- Mostrar completados, pendientes y rechazados.
- Crear acciones para generar prueba, reintentar y revisar errores.
- Preparar resumen general para autorización.

## Matriz mínima
- Factura: 90
- CCF: 75
- Nota de Remisión: 50
- Nota de Crédito: 50
- Nota de Débito: 25
- Retención: 50
- Liquidación: 75
- Documento Contable de Liquidación: 50
- Exportación: 90
- Sujeto Excluido: 25
- Donación: 25
- Invalidación: 5
- Contingencia: 5
- Retorno: 5
- Operaciones Especiales: 5

## Tablas
- Dte_CertificacionMatriz
- Dte_CertificacionEscenarios
- Dte_CertificacionPruebas
- Dte_CertificacionErrores

## Tareas técnicas
1. Crear entidades de certificación.
2. Crear migración EF.
3. Crear seed de matriz oficial.
4. Crear CertificacionDteService.
5. Crear endpoints:
   - GET /api/certificacion/resumen
   - GET /api/certificacion/matriz
   - GET /api/certificacion/tipos/{codigo}/escenarios
   - POST /api/certificacion/tipos/{codigo}/generar-prueba
   - POST /api/certificacion/documentos/{id}/marcar-completado
   - POST /api/certificacion/documentos/{id}/reintentar
   - GET /api/certificacion/errores
6. Crear vistas: /Certificacion, /Certificacion/Matriz, /Certificacion/Tipo/{codigo}, /Certificacion/Errores.
7. Crear progress bars por tipo.
8. Crear resumen: total requerido, completado, pendiente, con error.
9. Asociar Dte_Documentos con pruebas de certificación.
10. Agregar pruebas del cálculo de progreso.

## Criterios de aceptación
- Matriz completa en UI.
- Cada tipo DTE/evento muestra requerido, completado y pendiente.
- Asociar DTE procesado a un escenario.
- Ver avance general y detalle por tipo.
- Errores registrados y consultables.
- Pantalla permite saber qué falta para solicitar autorización.
- Datos aislados por EmpresaId.

---

# Sprint 15 — Eventos DTE persistentes + UI + PDF

## Objetivo
Formalizar el módulo de eventos DTE para que invalidación, contingencia, retorno y operaciones especiales no queden solo como llamadas técnicas, sino como entidades persistentes con auditoría, respuesta MH, JSON, PDF y UI.

## Alcance
- Persistir eventos.
- Guardar JSON y JWS de eventos.
- Guardar respuesta de Hacienda.
- Asociar eventos a documentos.
- Crear UI de eventos.
- Crear PDF/representación gráfica de evento.
- Integrar eventos con certificación.

## Eventos
- Invalidación
- Contingencia
- Retorno
- Operaciones Especiales

## Tablas
- Dte_Eventos
- Dte_EventoJson
- Dte_EventoRespuestasHacienda
- Dte_EventoDocumentosRelacionados

## Tareas técnicas
1. Crear entidades Dte_Eventos.
2. Crear migración EF.
3. Extraer lógica de eventos a DteEventoService.
4. Persistir evento antes/después de enviar.
5. Guardar JSON sin firmar.
6. Guardar JWS firmado.
7. Guardar respuesta cruda de Hacienda.
8. Guardar estado interno del evento.
9. Crear endpoints:
   - GET /api/dte/eventos
   - GET /api/dte/eventos/{id}
   - POST /api/dte/eventos/invalidacion
   - POST /api/dte/eventos/contingencia
   - POST /api/dte/eventos/retorno
   - POST /api/dte/eventos/operaciones-especiales
   - GET /api/dte/eventos/{id}/json
   - GET /api/dte/eventos/{id}/pdf
10. Crear vistas: /DteEventos, /DteEventos/Details/{id}, /DteEventos/CreateInvalidacion, /DteEventos/CreateContingencia, /DteEventos/CreateRetorno, /DteEventos/CreateOperacionesEspeciales.
11. Crear DteEventoPdfService.
12. Agregar auditoría.
13. Crear pruebas de estructura de eventos.

## Criterios de aceptación
- Evento persistido.
- Ver detalle del evento.
- Descargar JSON y PDF.
- Guarda respuesta MH.
- Asocia evento al documento original.
- Eventos procesados cuentan para matriz de certificación.
- Tests existentes siguen pasando.

---

# ~~Sprint 16~~ ✅ ENTREGADO — Contingencia avanzada y recepción por lotes

> **Estado:** Completado. Migración `Sprint16_ContingenciaLotes` aplicada. Build verde. 179/179 tests.
> Entidades `DteContingenciaLote`/`DteContingenciaLoteDetalle`, `ContingenciaLoteService`,
> `HttpHaciendaLoteClient` (POST `/fesv/recepcionlote`), `HttpHaciendaConsultaLoteClient` (GET `/fesv/recepcion/consultadtelote/{codigoLote}`),
> mocks, `ContingenciaLoteWorker` (10 min), API REST 7 endpoints, UI MVC `/DteContingencia`, `/DteContingencia/Lotes`, `/DteContingencia/DetalleLote/{id}`.



## Alcance
- UI de cola de contingencia.
- Reintento manual.
- Evento de contingencia desde UI.
- Recepción por lote.
- Consulta de lote.
- Estados de lote.
- Alertas de documentos pendientes.

## Tablas
- Dte_ContingenciaLotes
- Dte_ContingenciaLoteDetalles
- Dte_ContingenciaIntentos

## Tareas técnicas
1. Crear entidades de lote de contingencia.
2. Crear migración EF.
3. Crear ContingenciaService.
4. Crear HaciendaRecepcionLoteClient.
5. Crear HaciendaConsultaLoteClient.
6. Crear endpoints:
   - GET /api/dte/contingencia/resumen
   - GET /api/dte/contingencia/documentos
   - POST /api/dte/contingencia/documentos/{id}/reintentar
   - POST /api/dte/contingencia/evento
   - POST /api/dte/contingencia/lotes
   - POST /api/dte/contingencia/lotes/{id}/consultar
7. Crear vistas: /Contingencia, /Contingencia/Lotes, /Contingencia/Lotes/Details/{id}.
8. Mostrar documentos pendientes por antigüedad.
9. Mostrar vencimientos 24h/72h.
10. Crear alertas visuales.
11. Agregar pruebas del flujo de reintento/lote.

## Criterios de aceptación
- UI muestra documentos en contingencia.
- Reintento manual.
- Generar evento de contingencia.
- Generar y consultar lote.
- Estado por documento.
- Registra intentos y errores.
- Worker sigue operando.

---

# Sprint 17 — Diagnóstico de errores Hacienda

## Objetivo
Crear un módulo de diagnóstico que traduzca errores técnicos de Hacienda a mensajes entendibles, con causa probable y acción sugerida.

## Alcance
- Catálogo de errores MH.
- Pantalla de errores.
- Asociación error → documento/evento.
- Recomendación de corrección.
- Visualización de JSON enviado y respuesta MH.
- Filtros por tipo, código, fecha y estado.

## Tareas técnicas
1. Crear tabla Dte_ErrorCatalogo.
2. Crear tabla Dte_ErrorOcurrencias.
3. Crear seed de códigos comunes: 001, 002, 006, 095, 096, 802, FIRMA_FAILED, HACIENDA_AUTH_FAILED, FIRMA_MOCK_NO_ENVIABLE.
4. Crear DiagnosticoHaciendaService.
5. Crear endpoints:
   - GET /api/dte/diagnostico/errores
   - GET /api/dte/diagnostico/documentos/{id}
   - GET /api/dte/diagnostico/eventos/{id}
6. Crear vistas: /DiagnosticoHacienda, /DiagnosticoHacienda/Documento/{id}.
7. Mostrar: código, mensaje técnico, causa probable, acción sugerida, JSON enviado, respuesta MH.
8. Agregar pruebas del mapper de errores.

## Criterios de aceptación
- Error MH muestra explicación clara.
- Consulta por documento/evento.
- Recomendaciones visibles.
- Filtrar por código.
- Soporte diagnostica sin revisar BD manualmente.

---

# Sprint 18 — Legal + consentimiento

## Objetivo
Cerrar el mínimo legal para venta asistida y registro formal de usuarios.

## Alcance
- LegalController.
- Páginas públicas legales.
- Lectura de documentos legales.
- Reemplazo de placeholders.
- Checkbox obligatorio en registro.
- Registro de consentimiento.

## Rutas
- /legal/terms
- /legal/privacy
- /legal/cookies
- /legal/dpa

## Tablas
- Core_UserConsents

## Tareas técnicas
1. Crear LegalController.
2. Crear LegalDocumentService.
3. Crear LegalDocumentViewModel.
4. Crear vistas legales.
5. Crear configuración LegalOptions.
6. Reemplazar placeholders desde configuración.
7. Agregar checkbox obligatorio al registro.
8. Validar aceptación en frontend.
9. Validar aceptación en backend.
10. Guardar consentimiento: UsuarioId, EmpresaId, ConsentType, Version, AcceptedAt, AcceptedFromIp, AcceptedUserAgent.
11. Crear pruebas de rutas legales.
12. Crear pruebas de registro sin consentimiento.

## Criterios de aceptación
- Las 4 páginas legales responden 200.
- Slug inexistente responde 404.
- Registro no avanza sin aceptar términos.
- Consentimiento queda registrado.
- No se rompe signup/provisioning.
- Tests verdes.

---

# Sprint 19 — Billing self-service

## Objetivo
Permitir que un cliente se registre, elija plan, inicie trial, pague y mantenga su suscripción sin intervención manual.

## Alcance
- Trial 14 días.
- Stripe Billing.
- MercadoPago.
- Checkout.
- Portal billing.
- Upgrade/downgrade.
- Webhooks.
- Activación automática de licencias.
- Emails transaccionales.

## Tablas
- Billing_Customers
- Billing_Subscriptions
- Billing_Payments
- Billing_Invoices
- Billing_WebhookEvents
- Billing_PlanProviderMappings

## Tareas técnicas
1. Crear entidades Billing.
2. Crear migración EF.
3. Crear IBillingService.
4. Crear IPaymentProvider.
5. Crear StripeBillingProvider.
6. Crear MercadoPagoBillingProvider.
7. Crear BillingWebhookHandler.
8. Crear endpoints:
   - GET /billing
   - GET /billing/checkout
   - POST /billing/checkout
   - GET /billing/portal
   - POST /billing/change-plan
   - POST /api/billing/webhooks/stripe
   - POST /api/billing/webhooks/mercadopago
9. Crear vistas Billing.
10. Integrar con Core_EmpresaPlan.
11. Integrar con Core_EmpresaModulos.
12. Integrar con límites de plan.
13. Hacer webhooks idempotentes.
14. Enviar emails: Trial iniciado, Pago exitoso, Pago fallido, Suscripción cancelada, Plan actualizado.

## Estados
- TRIALING
- ACTIVE
- PAST_DUE
- CANCELED
- INCOMPLETE
- EXPIRED
- SUSPENDED

## Criterios de aceptación
- Cliente puede iniciar trial.
- Cliente puede pagar.
- Pago exitoso activa plan.
- Pago fallido marca PAST_DUE.
- Cancelación mantiene acceso hasta fin de período.
- Webhooks duplicados no duplican acciones.
- Licencias se actualizan automáticamente.

---

# Sprint 20 — Hardening pre-producción

## Objetivo
Preparar NeoSTP Cloud para operar con clientes reales de forma segura, estable y recuperable.

## Alcance
- Backup off-site.
- k6 baseline.
- OWASP ZAP en CI.
- Quotas API.
- MFA SuperAdmin.
- IP allowlist.
- Disaster Recovery.
- Monitoreo básico.

## Tablas
- Ops_BackupJobs
- Core_ApiUsageLog
- Core_ApiQuotas
- Core_AdminIpAllowlist

## Tareas técnicas
1. Crear BackupService.
2. Crear StorageService para Azure Blob/S3.
3. Crear BackupWorker.
4. Crear endpoint/pantalla de backups.
5. Crear scripts k6.
6. Crear GitHub Action OWASP ZAP.
7. Crear ApiQuotaService.
8. Aplicar rate limit por: EmpresaId, UsuarioId, ApiKeyId, PlanId, Módulo.
9. Implementar MFA obligatorio para SuperAdmin.
10. Implementar IP allowlist configurable.
11. Crear documento Disaster Recovery.
12. Crear pruebas smoke críticas.

## Criterios de aceptación
- Backups programados funcionando.
- Backup sube a storage externo.
- k6 baseline ejecutable.
- OWASP ZAP corre en CI.
- Quotas responden 429 al exceder.
- SuperAdmin requiere MFA.
- IP allowlist configurable.
- DR documentado.
- Tests verdes.

---

# Sprint 21 — UI/UX AppShell + design system

## Objetivo
Modernizar la interfaz base de NeoSTP Cloud con una experiencia SaaS profesional, modular y consistente.

## Alcance
- AppShell.
- Sidebar nueva.
- Navbar nueva.
- Indicador de ambiente.
- Componentes reutilizables.
- Coexistencia Bootstrap/Tailwind o CSS modular.
- Aplicar primero a páginas principales.

## Componentes
- AppShell, Sidebar, Navbar, Breadcrumb, MetricCard, ModuleCard, StatusBadge, DataTable, FilterPanel, FormSection, StepperDTE, CertificationProgressBar, LicenseUsageCard, AlertPanel, ConfirmModal, EmptyState, ErrorState, LoadingState.

## Tareas técnicas
1. Crear estructura CSS nueva.
2. Crear variables de diseño.
3. Crear componentes parciales.
4. Crear nuevo layout base.
5. Migrar sidebar y navbar.
6. Agregar indicador de ambiente: MOCK, PRUEBAS, PRODUCCIÓN.
7. Agregar indicador de empresa actual.
8. Agregar módulo activo/bloqueado.
9. Aplicar al Dashboard, DTE Documentos, Certificación DTE.

## Criterios de aceptación
- Layout responsive.
- Sidebar fija en desktop.
- Menú sigue respetando permisos.
- Módulos bloqueados se muestran claramente.
- Estados DTE tienen badges consistentes.
- Dashboard se ve moderno.
- No se rompe lógica existente.

---

# Sprint 22 — NeoProfit básico

## Objetivo
Crear el primer módulo financiero usando DTE emitidos para calcular ventas, IVA, costos y ganancia estimada.

## Alcance
- Ventas por período.
- IVA generado.
- Ganancia bruta estimada.
- Margen por producto.
- Ranking de productos y clientes.
- Ventas por sucursal.
- Dashboard financiero.

## Tablas
- Profit_Gastos
- Profit_Compras
- Profit_SnapshotsMensuales
- Profit_Alertas

## Endpoints
- GET /api/profit/dashboard
- GET /api/profit/productos
- GET /api/profit/clientes
- GET /api/profit/sucursales
- GET /api/profit/tendencia
- POST /api/profit/gastos
- POST /api/profit/compras

## Reglas
- Solo contar PROCESADO.
- Excluir RECHAZADO e INVALIDADO.
- Nota de Crédito resta.
- Nota de Débito suma.
- Sujeto Excluido no genera IVA.
- Producto sin costo se marca como costo pendiente.

## Criterios de aceptación
- Solo empresas con NEOPROFIT acceden.
- Dashboard muestra ventas.
- Ganancia usa CostoUnitario.
- Ranking productos/clientes funciona.
- Ventas por sucursal funcionan.
- Datos respetan EmpresaId.
- Tests de ProfitCalculator pasan.

---

# Sprint 23 — NeoScanAI integrado

## Objetivo
Integrar NeoScanAI como módulo oficial dentro de NeoSTP Cloud para recibir, revisar y convertir resultados en compras, gastos o DTE recibidos.

## Alcance
- Bandeja de documentos.
- Recepción de resultados.
- Revisión manual.
- Corrección de campos.
- Confirmación.
- Registro como compra/gasto/DTE recibido.
- Alimentar NeoProfit.

## Tablas
- Scan_Documentos
- Scan_DocumentoResultados
- Scan_DocumentoCampos
- Scan_DocumentoArchivos
- Scan_DocumentoEventos
- Dte_DocumentosRecibidos

## Estados
- RECIBIDO, PROCESANDO, PROCESADO, REQUIERE_REVISION, CONFIRMADO, RECHAZADO, ERROR.

## Endpoints
- GET /api/scanai/documentos
- GET /api/scanai/documentos/{id}
- POST /api/scanai/documentos
- POST /api/scanai/documentos/{id}/resultado
- POST /api/scanai/documentos/{id}/confirmar
- POST /api/scanai/documentos/{id}/rechazar
- POST /api/scanai/documentos/{id}/registrar-gasto
- POST /api/scanai/documentos/{id}/registrar-compra
- POST /api/scanai/documentos/{id}/registrar-dte-recibido

## Criterios de aceptación
- Solo empresas con NEOSCANAI acceden.
- Cloud recibe resultado desde NeoScanAI.
- Documento aparece en bandeja.
- Usuario revisa/corrige/confirma/rechaza campos.
- Confirmar como gasto/compra alimenta Profit.
- Se controla límite mensual de escaneos.
- Se auditan confirmaciones.

---

# Sprint 24 — NeoConnect API comercial

## Objetivo
Convertir la API existente en producto comercial para integradores, ERPs y sistemas externos.

## Alcance
- API Keys.
- Webhooks.
- Logs API.
- Usage dashboard.
- Sandbox.
- Documentación pública.
- Rate limits por ApiKey.

## Tablas
- Connect_ApiKeys
- Connect_Webhooks
- Connect_WebhookDeliveries
- Connect_ApiLogs
- Connect_SandboxSettings

## Endpoints
- GET /api/connect/api-keys
- POST /api/connect/api-keys
- PATCH /api/connect/api-keys/{id}/revoke
- GET /api/connect/webhooks
- POST /api/connect/webhooks
- POST /api/connect/webhooks/{id}/test
- GET /api/connect/logs
- GET /api/connect/usage

## Criterios de aceptación
- Empresa puede crear API Key.
- API Key se guarda hasheada.
- Webhooks se disparan por cambios DTE.
- Logs muestran consumo.
- Rate limit funciona por ApiKey.
- Pantalla de integradores y documentación visible.

---

# Sprint 25 — NeoPOS básico

## Objetivo
Crear punto de venta web integrado con productos, clientes, caja y DTE.

## Alcance
- Caja, apertura, venta rápida, carrito, métodos de pago.
- Conversión a DTE.
- Cierre y corte de caja.

## Tablas
- Pos_Cajas
- Pos_Aperturas
- Pos_Ventas
- Pos_VentaDetalles
- Pos_Pagos
- Pos_Cierres

## Endpoints
- GET /api/pos/cajas
- POST /api/pos/cajas/apertura
- POST /api/pos/cajas/cierre
- GET /api/pos/ventas
- POST /api/pos/ventas
- POST /api/pos/ventas/{id}/emitir-dte

## Criterios de aceptación
- Cajero abre caja.
- Cajero crea venta.
- Venta genera DTE.
- Cierre muestra resumen por método de pago.
- Venta alimenta dashboard/Profit.
- Se respetan permisos y EmpresaId.

---

# Sprint 26 — NeoPortal Clientes

## Objetivo
Crear portal para que receptores consulten documentos, descarguen PDF/JSON y generen solicitudes.

## Alcance
- Consulta pública segura.
- Descarga PDF/JSON.
- Historial por cliente.
- Estado de cuenta.
- Solicitudes.

## Tablas
- Portal_Accesos
- Portal_Solicitudes
- Portal_TokensPublicos

## Endpoints
- GET /portal/documentos/{codigoGeneracion}/pdf
- GET /portal/documentos/{codigoGeneracion}/json
- POST /portal/solicitud
- GET /api/portal/clientes/{id}/documentos
- GET /api/portal/clientes/{id}/estado-cuenta

## Criterios de aceptación
- Receptor consulta documento, descarga PDF/JSON.
- Acceso protegido por token.
- Receptor crea solicitud.
- Empresa ve solicitudes.

---

# Sprint 27 — NeoSTP Mobile API readiness

## Objetivo
Preparar la API para la app móvil sin construir todavía toda la app.

## Alcance
- Endpoints móviles optimizados.
- Selección empresa/sucursal/PV.
- Dashboard móvil.
- DTE básico móvil.
- Documentos.
- Seguridad de dispositivo.

## Tablas
- Mobile_Dispositivos
- Mobile_Sesiones
- Mobile_SyncLog

## Endpoints
- POST /api/mobile/devices/register
- PATCH /api/mobile/devices/{id}/authorize
- PATCH /api/mobile/devices/{id}/revoke
- GET /api/mobile/context
- GET /api/mobile/dashboard
- GET /api/mobile/documentos
- POST /api/mobile/dte/factura

## Criterios de aceptación
- Usuario móvil autentica.
- Selecciona empresa/sucursal/PV.
- Dispositivo registrado.
- Admin puede revocar dispositivo.
- API respeta módulos y permisos.

---

# Sprint 28 — NeoSTP Mobile MVP

## Objetivo
Crear la primera versión de la app móvil conectada a NeoSTP Cloud.

## Alcance
- Login, selector empresa, selector sucursal/PV.
- Dashboard.
- Clientes y productos.
- Crear factura básica.
- Ver documentos y PDF.
- Reenviar correo.
- Escaneo QR si NeoScanAI está listo.

## Criterios de aceptación
- Login funciona.
- Usuario ve empresa autorizada.
- Crea factura básica.
- Consulta documentos y ve PDF.
- Reenvía correo.
- Respeta permisos y módulos.

---

# Sprint 29 — SuperAdmin operativo avanzado

## Objetivo
Fortalecer el panel interno de NeoSTP para operación SaaS.

## Alcance
- Salud del sistema, empresas en riesgo, billing resumen, consumo global.
- Backups, incidentes, logs operativos.
- Modo soporte auditado.

## Tareas
1. Dashboard salud sistema.
2. Panel empresas vencidas.
3. Panel consumo por empresa.
4. Panel errores recientes.
5. Panel backups.
6. Panel incidentes.
7. Auditoría avanzada del modo soporte.
8. Indicadores de churn risk básicos.

## Criterios de aceptación
- SuperAdmin ve estado general.
- Detecta clientes vencidos y errores recurrentes.
- Ve backups.
- Registra incidentes.
- Todo acceso soporte queda auditado.

---

# Sprint 30 — Preparación comercial y documentación

## Objetivo
Dejar el producto listo para demostraciones, onboarding, ventas asistidas y primeros clientes.

## Alcance
- Documentación técnica.
- Manuales (usuario, SuperAdmin).
- Runbooks.
- Demo scripts.
- Planes comerciales.
- Checklists onboarding/producción.

## Entregables
- README actualizado.
- Contexto maestro actualizado.
- Manual de emisión DTE.
- Manual de certificación.
- Manual SuperAdmin.
- Runbook de incidentes.
- Runbook backup/restore.
- Demo script para clientes.
- Tabla de planes/precios.
- Checklist alta de cliente.
- Checklist salida a producción.

## Criterios de aceptación
- Documentación lista.
- Demo reproducible.
- Onboarding definido.
- Soporte opera con runbooks.
- Ventas tiene material.

---

# Orden recomendado de ejecución

1. Sprint 13 — Catálogos MH
2. Sprint 14 — Certificación DTE
3. Sprint 15 — Eventos DTE persistentes
4. Sprint 16 — Contingencia avanzada
5. Sprint 17 — Diagnóstico Hacienda
6. Sprint 18 — Legal
7. Sprint 19 — Billing
8. Sprint 20 — Hardening
9. Sprint 21 — UI/UX
10. Sprint 22 — NeoProfit
11. Sprint 23 — NeoScanAI
12. Sprint 24 — NeoConnect
13. Sprint 25 — NeoPOS
14. Sprint 26 — NeoPortal
15. Sprint 27 — Mobile API
16. Sprint 28 — Mobile MVP
17. Sprint 29 — SuperAdmin avanzado
18. Sprint 30 — Documentación y salida comercial

---

# Prioridad por impacto

## Crítico técnico/fiscal
- Sprint 13 Catálogos
- Sprint 14 Certificación
- Sprint 15 Eventos
- Sprint 16 Contingencia
- Sprint 17 Diagnóstico Hacienda

## Crítico comercial
- Sprint 18 Legal
- Sprint 19 Billing
- Sprint 20 Hardening
- Sprint 21 UI/UX

## Diferenciadores comerciales
- Sprint 22 NeoProfit
- Sprint 23 NeoScanAI
- Sprint 24 NeoConnect

## Operación avanzada
- Sprint 25 NeoPOS
- Sprint 26 NeoPortal
- Sprint 27 Mobile API
- Sprint 28 Mobile MVP
- Sprint 29 SuperAdmin avanzado
- Sprint 30 Documentación

---

# Regla operativa para trabajar con Codex/Claude Code

No entregar todos los sprints en un solo prompt de implementación.

Formato:
1. Dar contexto corto del sistema.
2. Indicar sprint exacto.
3. Indicar objetivo.
4. Indicar alcance.
5. Indicar archivos esperados.
6. Indicar restricciones.
7. Indicar criterios de aceptación.

Ejemplo:
> "Implementa Sprint 13 Parte 1: extender Core_Catalogos y Core_CatalogoItems con Version, ParentCodigo, MetadataJson, EsSistema y Activo. Crear migración EF, actualizar seed básico y agregar tests. No tocar DTE Generator todavía."

---

# Definición de completado global

Cada sprint se considera terminado cuando:
- Build verde.
- Tests existentes pasan.
- Nuevos tests agregados pasan.
- Migración EF aplicada si corresponde.
- No se agregan secretos al repo.
- No se rompe multiempresa.
- Se respeta EmpresaId.
- Se respeta licenciamiento por módulo.
- Se registra auditoría en acciones críticas.
- README/contexto maestro se actualiza si cambia el estado del sistema.
