# Análisis técnico — foco empresas (Enterprise / Contador / cadenas)

**Fecha:** 2026-07-18 · **Base:** `main` @ `90902b5` (844 tests verdes) · **Complementa:** `Analisis-Integral-2026-07.md`

Pregunta que responde: ¿qué tenemos hoy para clientes empresariales (planes Enterprise $400 y Contador $120, cadenas Business Full) y qué hay que construir o mejorar para servirlos de verdad?

---

## 1. Lo que ya tenemos a nivel empresa (verificado en código)

| Capacidad | Estado |
|---|---|
| Aislamiento multi-tenant por EmpresaId en todos los servicios | ✅ |
| Enforcement comercial: límites de plan + suspensión efectiva (Entrega 7) | ✅ |
| Sucursales y puntos de venta con bloque MH correcto en numeroControl | ✅ |
| POS por sucursal/punto de venta con sesiones de caja | ✅ |
| Seguridad: JWT, MFA TOTP, lockout, ~423 permisos, roles por empresa, allowlist IP admin, cuotas 429 | ✅ |
| Libros fiscales (ventas consumidor/contribuyente, compras, resumen F07) con export CSV | ✅ |
| NeoConnect: API keys con scopes, webhooks DTE con reintentos, OpenAPI público | ✅ |
| Contabilidad básica + balanza + conciliación bancaria; RRHH/nómina; tesorería | ✅ |
| Branding por empresa (logo/firma en PDF y correo), i18n es/en | ✅ |
| Observabilidad OTel, backups, DR runbook, auditoría transversal, retención | ✅ |

## 2. Brechas enterprise encontradas (verificadas, no supuestas)

### 🔴 E1 — Un usuario solo puede pertenecer a UNA empresa
`Usuario.EmpresaId` es 1:1. **El plan Contador vende "múltiples clientes" pero el modelo no lo soporta**: un contador necesita credenciales separadas por cada empresa cliente. Lo mismo bloquea holdings/grupos (Enterprise). Es el equivalente estructural de lo que pasaba con los límites de plan: se vende algo que el sistema no entrega.

**Construir:** entidad `UsuarioEmpresa` (membresía N:M con rol por empresa), selector de empresa activa en sesión (web y móvil), claims emitidos por empresa activa, invitaciones entre empresas. Toca auth/EmpresaContext — es la pieza más valiosa y la más delicada.

### 🔴 E2 — Inventario ciego a sucursales
`ExistenciaProducto`, `MovimientoInventario` y `LoteProducto` no tienen `SucursalId`: el stock es un solo número por empresa. Una cadena (el pitch de Business Full) no sabe cuánto hay en cada tienda ni puede trasladar mercadería con rastro.

**Construir:** `SucursalId` opcional en las tres entidades (null = bodega central, compatible con datos actuales), traslados entre sucursales (salida+entrada atómica con documento), existencia consolidada + por sucursal en la UI, y el POS descontando de su sucursal.

### 🟡 E3 — Sin SSO corporativo
Solo credenciales propias + TOTP. Los clientes Enterprise suelen exigir inicio de sesión con **Microsoft Entra ID / Google Workspace** (OIDC). El pipeline de autenticación está limpio para agregarlo como esquema adicional sin tocar el modelo de permisos.

### 🟡 E4 — Órdenes de compra sin flujo de aprobación
Estados actuales: BORRADOR → EMITIDA → PARCIAL/RECIBIDA/CANCELADA. No existe "POR_APROBAR": cualquier usuario con el permiso emite una OC de cualquier monto. Empresas medianas piden **límite de aprobación por monto** (arriba de $X requiere aprobador).

**Construir:** estado `POR_APROBAR`, umbral configurable por empresa, permiso `Compras.Aprobar`, alerta al aprobador (infraestructura de alertas ya existe).

### 🟡 E5 — Sin vista consolidada de grupo
Depende de E1: cuando un contador/holding tenga varias empresas, querrá un dashboard consolidado (ventas, IVA por declarar, CxC, alertas por empresa). Hoy cada empresa es una isla incluso para superadmin.

### 🟢 E6 — Webhooks solo de DTE
NeoConnect emite `DTE.Procesado/Rechazado/Contingencia/Invalidado`. Integradores enterprise querrán también `Cobros.PagoConfirmado`, `Inventario.StockBajo`, `Cita.Creada`. El dispatcher con reintentos ya existe — es agregar eventos.

### 🟢 E7 — Export contable a sistemas externos
Los libros salen en CSV; falta exportar **asientos contables** en formato importable (CSV genérico + columnas estándar) para clientes con contabilidad externa (SAP/QuickBooks/contador).

### 🟢 E8 — Portabilidad de datos
Para cerrar contratos enterprise ayuda ofrecer export completo de los datos de la empresa (clientes, productos, DTEs, kardex) en un ZIP de CSVs — también es la salida digna si un cliente se va (genera confianza para entrar).

## 3. Roadmap propuesto

| Orden | Pieza | Por qué primero | Tamaño |
|---|---|---|---|
| **E1** | Membresías multi-empresa | Repara la promesa del plan Contador; desbloquea E5 | L |
| **E2** | Inventario por sucursal + traslados | Repara la promesa de "cadena" de Business Full | M |
| **E4** | Aprobaciones de OC por monto | Rápida, muy pedida por medianas | S |
| **E3** | SSO OIDC (Entra/Google) | Requisito de compra Enterprise | M |
| **E5** | Dashboard consolidado de grupo | Se apoya en E1 | M |
| **E6-E8** | Webhooks + exports + portabilidad | Cierres de venta e integración | S c/u |

E1 y E2 son las dos que, igual que el enforcement de la Entrega 7, **alinean lo que se vende con lo que el sistema entrega**. Recomiendo atacarlas en ese orden.
