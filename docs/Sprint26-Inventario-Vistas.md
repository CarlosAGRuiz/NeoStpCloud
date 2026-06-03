# Sprint 26.1 — Inventario de vistas MVC + Matriz de acciones CRUD

> **Entregable de la Fase 26.1.** Levantado directamente del código (`src/NeoSTP.Web/Controllers` y
> `src/NeoSTP.Web/Views`), no de mockups. Es la base para priorizar la modernización de las fases 26.2–26.6.
>
> **Fecha:** Sprint 26 · **Rama:** `main` · **Build:** ✅ · **Tests:** 259 unit + 2 integración.

---

## 1. Metodología

- **Fuente:** 20 controladores MVC + 71 vistas `.cshtml`.
- **Madurez visual** medida por adopción del design system (`ns-toolbar/empty/pill/badge/metric/mono/kv`)
  vs. restos de Bootstrap plano / emojis decorativos:
  - 🟢 **Moderna** — usa AppShell + patrones `ns-*` consistentes.
  - 🟡 **Parcial** — re-tematizada por Bootstrap global pero sin patrones `ns-*` (emojis, badges `bg-*`).
  - 🔴 **Básica** — Bootstrap plano, sin toolbar/empty/badges semánticos; forms sin header operativo.
- **Criticidad operativa:**
  - **C1 Crítica de operación** — bloquea facturación/cobro/cumplimiento si falla.
  - **C2 Administrativa** — gestión de datos maestros y configuración.
  - **C3 Auxiliar** — soporte, lectura, legal.

---

## 2. Clasificación por nivel

| Nivel | Vistas |
|---|---|
| **C1 Crítica de operación** | DTE Documentos (Index/Details/Create), DTE Eventos, Contingencia, Diagnóstico Hacienda, Certificación, Billing (Index/Checkout/Portal/Transferencias), Config DTE |
| **C2 Administrativa** | Clientes, Productos, Catálogos, Empresas, Sucursales/PV, Usuarios, Planes, Integraciones (NeoConnect), Hardening |
| **C3 Auxiliar** | Home/Dashboard, Soporte, Legal, Account (Login/ChangePassword), Error/AccessDenied |

---

## 3. Matriz de acciones por vista

> Leyenda acciones: **L**=listar · **V**=ver detalle · **C**=crear · **E**=editar · **D**=desactivar/inactivar ·
> **X**=eliminar físico · **R**=restaurar · **Re**=reintentar · **Dl**=descargar · **Im**=importar · **Ex**=exportar ·
> **Au**=auditar/trazar. En _Faltantes_ se listan las que el negocio esperaría y hoy no existen.

### 3.1 C1 — Críticas de operación

| Vista | Controlador / Acciones | Permisos | Madurez | Acciones actuales | Faltantes (candidatas) |
|---|---|---|---|---|---|
| `/DteDocumentos` | `Index` | `DTE.Emitir`/`DTE.Consultar` | 🟢 | L + filtros + búsqueda | Filtro por estado más granular (error/contingencia separados), export listado, badges por los 9 estados |
| `/DteDocumentos/Details/{id}` | `Details`, `Generar`, `Validar`, `Firmar`, `Enviar`, `Invalidar`, `Pdf`, `Json`, `Reenviar` | `DTE.Emitir`/`Invalidar`/`Reenviar`/`Consultar` | 🟢 (StepperDTE) | V, generar, validar, firmar, enviar, invalidar, Dl PDF/JSON, reenviar correo | **Ver detalle MH / respuesta cruda inline**, **descargar JWS**, **revalidar/refirmar** tras error, **registrar nota interna**, trazabilidad de intentos |
| `/DteDocumentos/Create` | `Create` (GET/POST) | `DTE.Emitir` | 🟡 form | C con líneas dinámicas | Header operativo, validaciones visibles consistentes |
| `/DteEventos` | `Index` | `DTE.Eventos.Ver` | 🟢 | L + filtros tipo/estado | Export, reintento de evento RECHAZADO |
| `/DteEventos/Details/{id}` | `Details`, `Json`, `Pdf` | `DTE.Eventos.Ver` | 🟡 (emojis) | V, Dl JSON/PDF | Modernizar a `ns-*`, respuesta MH inline estandarizada |
| `/DteEventos/Create*` | Invalidacion/Contingencia/Retorno/OpEspeciales | `DTE.Invalidar`/`Contingencia`/`Emitir` | 🟡 form | C (4 tipos) | Header operativo + validaciones visibles |
| `/DteContingencia` | `Index`, `ReintentarDocumento` | `DTE.Contingencia` | 🟡 | L, Re documento | Modernizar, trazabilidad de reintentos |
| `/DteContingencia/Lotes` `/DetalleLote` | `Lotes`, `DetalleLote`, `CrearLote`, `ConsultarLote` | `DTE.Contingencia` | 🟡 (emojis) | L, V, crear lote, consultar lote | Modernizar a `ns-*`, estado de lote claro |
| `/DiagnosticoHacienda` | `Index`, `Documento`, `Evento`, `MarcarResuelta`, `Sincronizar` | `DTE.Diagnostico` | 🟡 (emojis) | L, V doc/evento, marcar resuelta, sincronizar | Modernizar, enlazar acción correctiva directa al DTE con error |
| `/Certificacion` (+ Matriz/Tipo/Errores) | `Index`, `Matriz`, `Tipo`, `Errores`, `GenerarPrueba`, `Reintentar`, `AsociarDte`, `AsociarEvento` | `Core.Certificacion.Ver`/`Operar` | 🟢 | L, generar prueba, reintentar, asociar DTE/evento, ver error | (Sprint 25 ya cubrió flujos asistidos) — pulido de evidencia descargable |
| `/Billing` | `Index`, `ChangePlan`, `Cancel`, `Portal`, `OpenExternalPortal` | (empresa) | 🟡 | V plan/suscripción, cambiar plan, cancelar, portal | Estados de plan completos (trial/vencido/suspendido/pendiente), Dl comprobante |
| `/Billing/Checkout` | `Checkout`, `StartTrial`, `CreateCheckout` | (empresa) | 🟢 (Sprint 25.1 planes reales) | trial, checkout multi-método | Verificar que no muestre beneficios inexistentes en `Planes` |
| `/Billing/Transferencia(s)` | `Transferencia`, `SubirComprobante`, `Transferencias`, `ConfirmarTransferencia`, `RechazarTransferencia` | `EsAdmin` (SuperAdmin) | 🟢 | subir comprobante, confirmar, rechazar | Dl comprobante, filtro por estado |
| `/DteConfiguracion` | `Index`, `Save`, `UploadCertificado`, `EliminarCertificado`, `ProbarConexion` | `DTE.Configurar` | 🟡 (emojis) | V, guardar, subir/eliminar cert, probar conexión | Modernizar, indicador de vigencia de certificado |

### 3.2 C2 — Administrativas

| Vista | Controlador / Acciones | Permisos | Madurez | Acciones actuales | Faltantes (candidatas) |
|---|---|---|---|---|---|
| `/Clientes` | `Index`, `Create`, `Edit`, `Inactivar`, `Importar` | `Clientes.Ver`/`Crear`/`Editar` | 🟢 (Index) / 🟡 (forms) | L, C, E, D, Im | **Restaurar** (reactivar), historial, Ex, validación de dependencias antes de inactivar |
| `/Productos` | `Index`, `Create`, `Edit`, `Inactivar`, `Importar` | `Productos.Ver`/`Crear`/`Editar` | 🟢 (Index) / 🟡 (forms) | L, C, E, D, Im | **Restaurar**, historial, Ex, mapeo unidad→CAT-014 |
| `/Catalogos` | `Index`, `Details`, `Import`, `Export` | `Core.Catalogos.Ver`/`Administrar`/`Importar` | 🟢 (Index) / 🟡 (Details/Import) | L, V, Im, Ex | Modernizar Details/Import; **proteger edición de catálogos normativos** (`EsSistema`), restaurar ítems internos |
| `/Empresas` | `Index`, `Create`, `Edit`, `Licencia` | `Core.Empresa.Ver`/`Editar`, `SuperAdmin.Empresas.Administrar` | 🟢 (Index) / 🟡 (forms/Licencia) | L, C, E, V licencia | Au explícita en cambios, confirmación en acciones sensibles |
| `/Sucursales` (+ PuntosVenta) | `Index`, `Create`, `Edit`, `Inactivar`, `PuntosVenta`, `CreatePuntoVenta`, `EditPuntoVenta`, `InactivarPuntoVenta` | `Empresas.Ver`/`Administrar` | 🟢 (Index) / 🟡 (PV) | L, C, E, D (sucursal y PV) | Restaurar, confirmaciones consistentes |
| `/Usuarios` | `Index`, `Create`, `Edit`, `Bloquear`, `Desbloquear` | `Core.Usuarios.Ver`/`Crear`/`Editar`/`Bloquear` | 🟢 (Index) / 🟡 (forms) | L, C, E, bloquear/desbloquear | Reset password desde UI, historial de accesos |
| `/Planes` | `Index` | (lectura) | 🔴 | L | Modernizar; consistencia con datos reales de `Core_Planes` |
| `/Integraciones` (NeoConnect) | `Index`, `CrearApiKey`, `RevocarApiKey`, `CrearWebhook`, `EliminarWebhook`, `TestWebhook` | `Connect.ApiKeys.*`/`Webhooks.*`/`Logs.Ver` | 🟢 | L, C/revoke key, C/X/test webhook | (recién entregado Sprint 24) |
| `/Hardening` | `Index`, `EjecutarBackup`, `CrearCuota`, `EliminarCuota`, `AgregarIp`, `ToggleIp`, `EliminarIp` | `Ops.Hardening.Ver`/`Administrar` | 🔴 (emojis, `bg-*`) | L, ejecutar backup, CRUD cuotas/IP | **Modernizar a `ns-*`** (es la más atrasada visualmente) |

### 3.3 C3 — Auxiliares

| Vista | Controlador / Acciones | Permisos | Madurez | Notas |
|---|---|---|---|---|
| `/Home` (empresa) | `Index` | (auth) | 🟢 | Metric cards; ya alineado |
| `/Home/SuperAdmin` | `Index` (rama SA) | SUPERADMIN | 🟡 (emoji) | Pulir a `ns-metric` consistente |
| `/Soporte` | `Index`, `Entrar`, `Salir` | SUPERADMIN | 🟡 | Selección de empresa para modo soporte |
| `/Legal/{slug}` | `Index` | anónimo | 🟡 | Documentos legales públicos |
| `/Account/Login`, `/ChangePassword`, `/AccessDenied` | — | anónimo/auth | 🟢 (Login) | Login tiene layout propio |

---

## 4. Riesgos de eliminación (NO permitir borrado físico sin regla explícita)

| Entidad / Vista | Riesgo | Regla actual | Acción Sprint 26 |
|---|---|---|---|
| **DTE Documentos** | Documento fiscal con valor legal | `InvalidarAsync` bloquea invalidar un PROCESADO (debe ir por evento de anulación); no hay borrado físico | Mantener: **sin eliminación física**; sólo invalidación/anulación por flujo legal |
| **DTE Eventos** | Registro fiscal | Sin borrado | Mantener sin borrado |
| **Billing Payments / Invoices / Subscriptions** | Trazabilidad de pagos | Transferencias se confirman/rechazan, no se borran | **Sin eliminación**; sólo cambios de estado auditados |
| **Core_Auditoria** | Bitácora legal | Append-only | **Sin edición ni borrado** |
| **Catálogos oficiales MH** (`EsSistema`) | Datos normativos | Ítems `EsSistema` no se borran (sólo inactivan); con hijos no se elimina | Reforzar en UI: ocultar/booquear botón eliminar para normativos |
| **Clientes / Productos** | Referenciados por DTE | Sólo `Inactivar` (soft) | Añadir **restaurar** + validar dependencias; nunca borrado físico si hay DTE asociado |
| **Empresas / Sucursales / PV** | Multiempresa + correlativos | Soft inactivar | Confirmación + auditoría; sin borrado físico |
| **API Keys (NeoConnect)** | Seguridad | Revocación (no borrado) | Mantener revocar, no borrar |

---

## 5. Pantallas con datos hardcoded / posible desalineación con BD

| Pantalla | Hallazgo | Estado |
|---|---|---|
| `/Billing/Checkout` | Antes usaba nombres/precios/IDs del mockup | ✅ corregido Sprint 25.1 (planes reales vía `IPlanesService`) — **verificar Portal/Index** |
| `/Planes` | Vista 🔴 básica; confirmar que lista `Core_Planes` reales y no un mockup | ⚠️ **verificar en 26.5** |
| `/Billing/Index` y `/Portal` | Revisar que beneficios/estados mostrados existan en `Core_Planes`/suscripción real | ⚠️ **verificar en 26.5** |
| `README.md` | Roadmap desactualizado (NeoConnect marcado 🔜 pese a estar entregado Sprint 24; "Versión actual: Carga masiva") | ⚠️ **actualizar en 26.7** |

> No se detectaron tablas con datos quemados en las vistas C1/C2 principales (DTE, Clientes, Productos,
> Catálogos, Certificación leen de BD). El foco de "anti-hardcode" queda en Billing/Planes.

---

## 6. Plan de modernización priorizado (propuesta para 26.2–26.6)

Orden por **reducción de soporte operativo** (criterio del sprint):

1. **26.3 — DTE y facturas con errores** (C1, mayor impacto): en `/DteDocumentos/Details`
   añadir ver respuesta MH inline + descargar JWS + revalidar/refirmar/reenviar según estado +
   nota interna + trazabilidad de intentos. Modernizar `/DteEventos/Details`, `/DteContingencia/*`,
   `/DiagnosticoHacienda/*` a `ns-*`.
2. **26.4 — Catálogos y datos maestros**: proteger catálogos normativos en UI; añadir
   **restaurar** + validación de dependencias en Clientes/Productos; modernizar Catálogos Details/Import.
3. **26.5 — Billing/planes/pagos**: alinear Index/Portal/Planes con BD real; estados de plan
   completos; descargar comprobante; acciones de pago por rol.
4. **26.2 — Patrones UI empresariales** (transversal): consolidar `_ListToolbar`, `_FormHeader`,
   `_ConfirmAction`, estilos para acciones destructivas/reintento/bloqueadas — se extrae de 26.3/26.4.
5. **Modernizar `/Hardening`** (🔴 la más atrasada) y `/Planes` (🔴).
6. **26.6 — Certificación**: ya cubierta por Sprint 25; sólo pulido de evidencia descargable.

### Patrones reutilizables a crear (26.2)
- `_ListToolbar.cshtml` — búsqueda + filtros + acción primaria (estandariza `ns-toolbar`).
- `_FormHeader.cshtml` — título operativo + estado + acciones primaria/secundaria.
- `_ConfirmAction.cshtml` — botón + modal de confirmación para acciones destructivas/irreversibles.
- Clases utilitarias: `.ns-action--danger`, `.ns-action--retry`, `.ns-action--locked` (acción bloqueada por permiso/estado).

---

## 7. Criterios de aceptación aplicables (recordatorio del sprint)

- Ninguna vista modernizada debe depender de datos quemados que contradigan la BD.
- Cada acción visible: **permiso** + **estado válido** + **confirmación** si es destructiva/irreversible.
- Entidades con impacto fiscal/pagos/auditoría: **sin eliminación física** sin regla explícita.
- Consistencia con AppShell; funcional en modo empresa y modo soporte SuperAdmin.
- Build + tests relevantes verdes antes de commit.
