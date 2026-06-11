# Análisis de experiencia como cliente — recorrido completo de la web

> Fecha: 2026-06-11. Sesión real (ADMIN de la empresa demo) contra la web en HTTPS local.
> Se recorrieron las ~45 pantallas del menú, se ejercitaron formularios con datos como los
> ingresaría un cliente y se revisaron permisos efectivos del rol ADMIN.

## 1. Bugs encontrados y corregidos en este recorrido

| # | Hallazgo | Causa | Fix |
|---|---|---|---|
| 1 | **Crear cliente desde la web fallaba siempre** con "Tipo de documento desconocido: 13" | El select del formulario envía códigos MH del CAT-022 (13/36/03/02/37) pero `ClienteValidator` solo aceptaba códigos de texto (DUI/NIT/…) | `NormalizarTipoDocumento` acepta ambos y persiste el código interno; +8 tests |
| 2 | **Sucursales inaccesible para todos** (hasta para el ADMIN) | `SucursalesController` exigía permisos `Empresas.Ver`/`Empresas.Administrar` que **no existen** en el seed | Corregido a `Core.Empresa.Ver` / `Core.Sucursales.Administrar` |
| 3 | **Diagnóstico de errores Hacienda solo visible para SUPERADMIN** | El permiso `DTE.Diagnostico` que exigen la UI y el controller **nunca se sembró** | Permiso 420 creado y otorgado a ADMIN/OPERADOR/CONTADOR (migración `Fix_PermisosDiagnosticoApiKeys`) |
| 4 | **Integraciones (NeoConnect) denegado al ADMIN de empresa** | El rol ADMIN no tenía `Connect.ApiKeys.Ver/Administrar` (351/352) pese a que las API keys son por empresa | Otorgados al rol ADMIN en la misma migración |
| 5 | Layout del Diagnóstico destruido (corregido en commit anterior) | Bloque duplicado + `</div>` extra | `7999a45` |

Los puntos 2–4 significan que **ningún cliente había podido usar Sucursales, Diagnóstico ni
Integraciones desde la web** — solo el SuperAdmin en modo soporte. Moraleja para el checklist de
release: probar cada pantalla con el rol ADMIN real, no solo con SUPERADMIN (que bypassa permisos).

## 2. Lo que funciona bien (como cliente)

- Navegación clara por grupos (Facturación, Ventas, CRM, Cobros, Compras, RRHH, Tesorería,
  Inteligencia, Administración); diseño consistente `ns-*` en todas las pantallas.
- Las ~45 vistas responden 200 sin excepciones; los estados vacíos usan `ns-empty` con mensaje
  útil en vez de tablas en blanco.
- Formularios con validación específica por campo cuando fallan (p. ej. "DUI inválido. Formato
  esperado: 12345678-9") y resumen visible.
- Onboarding con checklist en el dashboard + asistente; carga masiva para clientes/productos.
- Flujos de negocio completos: venta POS→DTE, cobro→QR, cotización→DTE, conciliación con
  sugerencias, libros fiscales con CSV.

## 3. Mejoras sugeridas (vistas con ojos de cliente)

### Alta prioridad (fricción directa en el día a día)
1. **"Probar correo" vive en Hardening** (solo SuperAdmin). El cliente configura su SMTP en
   `/Correo` pero no puede probarlo. Mover/duplicar el botón "Enviar correo de prueba" a `/Correo`.
2. **Dashboard demasiado DTE-céntrico**: solo muestra 4 KPIs de DTE. Un dueño de negocio espera
   también: ventas del día (POS+DTE), cartera vencida (CxC), saldo de tesorería, alertas activas.
   Ya existen los servicios (Cobranza/Tesoreria/Profit) — es solo composición del dashboard.
3. **El cliente nuevo no tiene datos demo**: la primera impresión de NeoBI/Conta/Cobros es todo
   vacío. Sembrar opcionalmente 1 CCF + 1 compra + 1 gasto de ejemplo (borrables) o añadir en cada
   vacío un CTA que lleve a la acción que genera datos ("Emite tu primer CCF →").
4. **Recordatorios de cobro**: la pantalla no muestra el historial de lo enviado (existe en
   `Cobros_Recordatorios`); el cliente no sabe si ayer salieron o no. Añadir tabla de últimos envíos.

### Media prioridad
5. **Buscador global** (Ctrl+K) sobre clientes/productos/DTE — con 20+ pantallas, encontrar "la
   factura de Juan" requiere saber en qué módulo está.
6. **Acciones en lote en DTE**: enviar por correo o descargar PDF de varios documentos a la vez.
7. **Exportar a Excel** en listados grandes (clientes, productos, kardex) — hoy solo los libros
   fiscales y balanza tienen CSV.
8. **i18n**: el plumbing es/en quedó en V2.5 pero solo cubre el shell; traducir las pantallas de
   mayor uso (POS, DTE, Cobros) si los clientes en inglés son un mercado real; si no, posponer.
9. **Wizard de primer DTE**: la configuración DTE (certificado, correlativo, establecimiento MH)
   es la parte más técnica para un cliente nuevo; un asistente paso a paso reduciría soporte.

### Baja prioridad / pulido
10. Páginas de detalle de lote de contingencia: botón "Consultar en Hacienda" solo aparece con
    estado ENVIADO — bien; añadir auto-refresh o indicación de cuándo consulta el worker.
11. En `Cobros/Recordatorios` falta `asp-validation-summary` (único form sin él; hoy usa TempData).
12. Confirmaciones destructivas (anular movimiento, revocar enlace) sin diálogo de confirmación —
    un `confirm()` simple evita anulaciones accidentales.

## 4. Sugerencia de proceso

Añadir al checklist de release (Runbook §7): **smoke con usuario ADMIN real** (no SUPERADMIN)
recorriendo el menú completo — los bugs 2–4 habrían salido a la primera. Automatizable con un
test de integración que recorra las rutas del nav con un usuario por rol y exija 200.
