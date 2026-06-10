# Análisis de pruebas tipo cliente — Cierre Fase V2

> Fecha: 2026-06-10. Ambiente: API (`http://localhost:5058`) y Web (`http://localhost:5031`)
> corriendo contra SQL Server local con datos reales de la empresa de pruebas
> (admin `NeoStp`, empresa NIT 06231111251090). Las llamadas se hicieron por HTTP como las haría
> la app móvil o un integrador; el portal se ejercitó sin sesión, como un receptor real.

## 1. Resumen ejecutivo

**Veredicto: V2 operativa de punta a punta.** Los cinco sprints del cierre (C2 Portal, D1 BI
fiscal, D2 Conta, D3 Recordatorios, D4 Conciliación) funcionan en vivo con datos reales, las
validaciones negativas responden con los códigos correctos y las suites automatizadas quedaron en
**653 tests unitarios + 7 de integración, 0 fallos**.

| Bloque | Resultado | Evidencia clave |
|---|---|---|
| Salud / auth | ✅ | `health/live`, `health/ready` (BD Healthy), login JWT, `/auth/me` con permisos. |
| C2 NeoPortal | ✅ | Enlace generado → HTML/JSON/PDF públicos sin sesión; token inválido 404; revocado 404. |
| D1 NEOBI fiscal | ✅ | F-07 real: ventas netas $1,681.41, débito $218.59; libros consumidor (3 días) y CSV 200 `text/csv`. |
| D2 NEOCONTA | ✅ | 8 cuentas sembradas, asientos generados idempotentes, balanza **cuadrada** $1,900.00 = $1,900.00. |
| D3 Recordatorios | ✅ | Config PUT/GET por empresa con plantillas; activo sin canales → 400; ejecución reporta 0 vencidas (correcto). |
| D4 Conciliación | ✅ | Import CSV 3 líneas, sugerencias ALTA/MEDIA correctas, auto-concilia solo ALTA, manual + desconciliar + dedupe de reimporte. |
| Módulos previos | ✅ | CRM, Cobros, POS, Profit, Lookups, Alertas, ScanAI, Tesorería, Inventario responden 200 con datos. |

## 2. Detalle por flujo

### 2.1 Salud y autenticación

- `GET /health/live` → 200 Healthy; `GET /health/ready` → 200 con check `database: Healthy` (58 ms).
- `POST /api/auth/login` (`usernameOrEmail` + password) → JWT; `GET /api/auth/me` devuelve
  empresa, roles y permisos. Probar con el cuerpo equivocado (campo `username`) devuelve 400 con
  validación — el contrato es estricto, bien para integradores.

### 2.2 C2 — NeoPortal del receptor

1. Se eligió un DTE real PROCESADO (`DTE-01-M001P001-000000000000028`).
2. `POST /api/portal/enlaces/documento/45` `{diasValidez: 7}` → enlace con token (43 chars,
   base64url) **devuelto solo en la creación**; el listado posterior no expone el token. ✔
3. Sin sesión: `GET /portal/{token}` → 200 HTML con el número de control; `/json` → 200
   `application/json` (2.1 KB); `/pdf` → 200 `application/pdf` (121 KB). ✔
4. Token inventado → 404 (sin filtración de existencia). ✔
5. `POST /api/portal/enlaces/2/revocar` → el mismo token pasa a 404 de inmediato. ✔

### 2.3 D1 — NEOBI fiscal

- `GET /api/reportes/fiscal/f07?anio=2026&mes=6` → resumen real coherente:
  netas gravadas $1,681.41 + débito $218.59 = $1,900.00 facturado del mes (cuadra con la balanza).
- Libro de consumidor final agrupado por día (3 filas), contribuyentes y compras vacíos porque la
  empresa demo no tiene CCF ni compras en el mes — correcto, no inventa datos.
- Export CSV → 200 `text/csv` con BOM, abre en Excel.

### 2.4 D2 — NEOCONTA

- `GET /api/conta/cuentas` siembra y devuelve el catálogo mínimo (8 cuentas) en el primer uso.
- `POST /api/conta/asientos/generar?anio=2026&mes=6` es idempotente: la segunda corrida solo crea
  asientos de documentos nuevos; quedaron 5 asientos del periodo.
- `GET /api/conta/balanza` → `cuadrada: true`, totalDebe = totalHaber = $1,900.00, con CxC
  $1,900.00 contra IVA débito $218.59 + Ventas $1,681.41. La doble partida cierra contra el F-07.

### 2.5 D3 — Recordatorios de cobro configurables

- `PUT /api/cobros/recordatorios/configuracion` guarda activo, umbral de días, frecuencia,
  máximo, canales y plantillas; `GET` devuelve exactamente lo guardado.
- Negativo: activar sin ningún canal → **400 VALIDATION**. ✔
- `POST /api/cobros/recordatorios/ejecutar` → `evaluadas: 0` (la empresa no tiene facturas a
  crédito vencidas hoy) — el selector no toma facturas de contado ni al día. ✔

### 2.6 D4 — Conciliación bancaria

Escenario realista: cuenta `BANCO-V2`, dos movimientos internos (INGRESO $350 ref TRX-9001 el
06-08; EGRESO $120.75 el 06-09) y un CSV "del banco" con 3 líneas: abono $350 con la misma
referencia, cargo $120.75 un día después sin referencia, y un cargo desconocido de $45.

- Import → 3 insertadas, 0 errores.
- Sugerencias → exactamente lo esperado: línea 1 ↔ interno 1 **ALTA** (referencia + misma fecha),
  línea 2 ↔ interno 2 **MEDIA** (1 día de diferencia), línea 3 sin candidato. ✔
- `conciliar-sugeridos` aplicó **solo la ALTA** (1). ✔
- Conciliación manual de la MEDIA → OK; intentar conciliar el cargo de $45 contra un INGRESO →
  **409** (rechazado: signo/estado). ✔
- Desconciliar → vuelve a pendiente; reimportar el mismo CSV → 0 insertadas, 3 omitidas (dedupe). ✔
- Resumen consistente en todo momento (montoNoConciliado $165.75 = $120.75 + $45). ✔

### 2.7 Regresión de módulos previos (lectura)

`crm/resumen`, `crm/oportunidades`, `crm/cotizaciones`, `cobros/resumen`, `cobros/pendientes`,
`pos/ventas`, `profit/dashboard`, `lookups/catalogo/*`, `lookups/departamentos`,
`alertas/resumen`, `scanai/documentos`, `tesoreria/resumen`, `inventario/existencias` → todos 200
con datos de la empresa. Sin regresiones observadas tras las migraciones nuevas.

## 3. Suites automatizadas

```
NeoSTP.Tests.Unit         653/653  ✔  (incluye +10 ConciliacionBancaria, +9 Portal, +6 LibroIva, +6 Contabilidad, +5 Recordatorios)
NeoSTP.Tests.Integration    7/7    ✔
```

Migraciones aplicadas a la BD local en este cierre: `V2_C2_NeoPortal`, `V2_D2_NeoConta`,
`V2_D3_ConfigRecordatorios`, `V2_D4_ConciliacionBancaria`.

## 4. Hallazgos y observaciones (no bloqueantes)

1. **Conciliación 1:1.** Una línea bancaria concilia contra un solo movimiento interno; depósitos
   agrupados (N:1) requieren partir el movimiento interno. Anotado en el plan para V2.5.
2. **El intento inválido de conciliar devolvió 409 en vez de 400** porque el movimiento interno
   elegido ya estaba conciliado (la validación de estado corre antes que la de monto). Cualquiera
   de los dos rechazos es correcto; solo es relevante para documentación de integradores.
3. **Libros de contribuyentes/compras vacíos en la demo.** Para una demo comercial conviene
   sembrar al menos un CCF y una compra del mes, así los tres libros muestran datos.
4. **Dependencias externas siguen mock por diseño**: WhatsApp Business, OCR real de NeoScan, FCM
   push y verificación de NIT en línea de MH. Todas pluggables por configuración (V2.5).
5. Los 404 iniciales en `profit/resumen` y `lookups/catalogos/*` durante el smoke fueron rutas
   mal escritas en la prueba (las reales son `profit/dashboard` y `lookups/catalogo/{codigo}`);
   los endpoints están sanos.

## 5. Conclusión

Los criterios de cierre del plan (`docs/Plan-Cierre-Fase-V2.md` §3) se cumplen: build y tests
verdes, flujos críticos probados en vivo (incluido el portal del receptor sin sesión), módulos
nuevos aislados por empresa con permisos y módulo de licencia, migraciones aplicadas, docs y
runbook al día, y sin secretos en el repo. **Fase V2: cerrada.**
