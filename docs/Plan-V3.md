# Plan V3 - Expansion Operativa y Enterprise

> Inicio: 2026-06-20. V2/V2.5, API mobile, HB-0..HB-8 y V3-S1/V3-S2 tienen cierre operativo.
> V3 se ejecuta en incrementos API-first con evidencia de negocio, tenant, RBAC, auditoria y pruebas.

## Objetivo

Extender NeoSTP Cloud sobre flujos ya vendidos, evitando modulos aislados o dependencias externas
sin cliente. Cada sprint debe mejorar una cadena existente y quedar demostrable con datos seed.

## Priorizacion

| Iniciativa | Impacto | Dependencia externa | Riesgo transversal | Orden |
|---|---:|---:|---:|---:|
| Ordenes de compra y recepciones | Alto | Baja | Baja-media | 1 |
| Vacaciones y aguinaldo RRHH | Alto ES | Baja | Media | 2 |
| Catalogo contable personalizable | Medio-alto | Baja | Media | 3 |
| SDKs NeoConnect | Medio-alto | Baja | Baja | 4 |
| Multi-moneda | Alto | Media | Alta | 5 |
| SSO/SAML y white label | Enterprise | Alta | Alta | 6 |
| BI predictivo / marketplace | Exploratorio | Alta | Alta | 7 |

La primera iniciativa es Ordenes de compra porque completa el flujo Proveedor -> Orden ->
Factura/CxP -> Inventario -> Tesoreria usando servicios existentes.

## Reglas V3

- API-first; UI Web en el mismo epic para superficies operativas.
- Aislamiento obligatorio por `EmpresaId` y modulo contratado.
- Reutilizar permisos existentes cuando expresen correctamente la capacidad; crear permisos nuevos
  solo cuando separen un riesgo real.
- Montos y estados se calculan en servidor; el cliente no decide totales persistidos.
- Acciones significativas dejan auditoria.
- Seeds demo son idempotentes y no se habilitan en produccion.
- Migraciones aditivas, revisadas y sin destruccion accidental.
- Cada sprint termina con README, contexto, README API, plan, build y suite completa actualizados.

## Estado de Sprints

| Sprint | Estado | Resultado |
|---|---|---|
| V3-S1 | Cerrado operativo 100% | Ordenes de compra API core: borrador, edicion, emision, cancelacion y conversion unica a FacturaCompra/CxP + inventario. |
| V3-S2 | Cerrado operativo 100% | Recepciones parciales idempotentes, kardex, CxP consolidada y UI Web completa. |
| V3-S3 | Siguiente | Vacaciones y aguinaldo automatizados en NeoRRHH. |
| V3-S4 | Pendiente | Catalogo contable personalizable y cierre anual base. |
| V3-S5 | Pendiente | SDKs y ejemplos ejecutables NeoConnect. |
| V3-S6 | Pendiente | Multi-moneda y tipos de cambio con estrategia de redondeo. |
| V3-S7 | Pendiente | SSO/SAML, white label avanzado y controles enterprise. |

## V3-S1 - Ordenes de Compra API Core

### Entregado

- Entidades `OrdenCompra` y `OrdenCompraLinea` en modulo COMPRAS.
- Estados: `BORRADOR`, `EMITIDA`, `RECIBIDA`, `CANCELADA`.
- Totales server-side con IVA 13% mediante `OrdenCompraCalculator`.
- Borrador editable solo antes de emitir.
- Emision y cancelacion con transiciones controladas.
- Conversion unica de orden emitida a `FacturaCompra`:
  - Reusa proveedor, subtotal e IVA calculados.
  - Genera CxP/NeoProfit mediante `ICompraService`.
  - Envia solo productos tipo BIEN a entrada de inventario.
  - Vincula `FacturaCompraId` y evita conversion duplicada.
  - Usa transaccion EF en proveedor relacional.
- Auditoria `NEOCOMPRAS` para crear, editar, emitir, cancelar y recibir.
- Seed `OC-DEMO-0001` emitido e idempotente.
- Migracion `V3_S1_OrdenesCompra` con tablas, FKs e indices tenant.

### API

Todas las rutas requieren JWT, modulo `COMPRAS` y permisos existentes:

| Metodo | Ruta | Permiso | Uso |
|---|---|---|---|
| GET | `/api/compras/ordenes` | `Compras.Ver` | Listar por estado, proveedor y busqueda. |
| GET | `/api/compras/ordenes/{id}` | `Compras.Ver` | Detalle con lineas y factura vinculada. |
| POST | `/api/compras/ordenes` | `Compras.Gestionar` | Crear borrador. |
| PUT | `/api/compras/ordenes/{id}` | `Compras.Gestionar` | Editar borrador. |
| POST | `/api/compras/ordenes/{id}/emitir` | `Compras.Gestionar` | Emitir orden. |
| POST | `/api/compras/ordenes/{id}/cancelar` | `Compras.Gestionar` | Cancelar borrador/emitida. |
| POST | `/api/compras/ordenes/{id}/convertir-factura` | `Compras.Gestionar` | Recibir completa y crear CxP/inventario. |

### Guardrails

- Proveedor y productos deben pertenecer a la empresa y estar activos.
- No se permiten productos repetidos; el cliente consolida cantidad.
- Entrega esperada no puede preceder la fecha de orden.
- Solo `BORRADOR` se edita/emite.
- `RECIBIDA` y `CANCELADA` son estados terminales en S1.
- Una orden solo puede crear una factura.

### Validacion

- `OrdenCompraServiceTests`: calculo, tenant, estados, cancelacion, conversion e idempotencia.
- `V3OrdenCompraContractTests`: rutas, verbos, modulo y permisos.
- `EmpresaPruebaSeederTests`: orden demo unica tras dos ejecuciones.
- Build 0 warnings/0 errores y suite 721 unitarias + 9 integracion.

### No alcance S1

- Recepcion parcial o multiples entregas.
- Aprobacion por monto/niveles.
- PDF/envio al proveedor.
- UI Web de ordenes.
- Multi-moneda.

## V3-S2 - Recepcion Parcial y UI Web

### Entregado

- Entidades `OrdenCompraRecepcion` y `OrdenCompraRecepcionLinea` con tenant, fecha, referencia,
  observaciones, detalle recibido y movimiento de inventario vinculado.
- Estado `PARCIAL` y acumulados recibido/pendiente por linea.
- `POST /api/compras/ordenes/{id}/recepciones` protegido por modulo/permisos.
- Idempotency key unica por empresa: un reintento devuelve la recepcion existente sin repetir kardex.
- Validacion de pertenencia, cantidades positivas, fecha y limite pendiente.
- Transaccion serializable para recepciones y conversion a factura.
- Bienes generan `RECEPCION_COMPRA` en kardex; servicios quedan trazados sin movimiento fisico.
- Regla de facturacion consolidada: con recepciones, la CxP solo se crea al completar la orden y no
  vuelve a registrar inventario. La ruta S1 directa se conserva para clientes sin recepciones.
- UI Web `/Compras/Ordenes`: listado/filtros, crear/editar borrador, detalle, emitir, cancelar,
  recibir parcialmente, historial, convertir y abrir la CxP vinculada.
- Seed demo idempotente con `OC-DEMO-0001` parcial, `RC-DEMO-0001` y kardex enlazado.
- Migracion `V3_S2_RecepcionesOrdenCompra` aditiva con FKs e indices de idempotencia/trazabilidad.

### Guardrails

- Solo ordenes `EMITIDA` o `PARCIAL` aceptan recepciones.
- Una recepcion no puede exceder el pendiente de ninguna linea.
- Una orden parcialmente recibida no puede cancelarse ni facturarse.
- `RECIBIDA` requiere que todas las lineas alcancen su cantidad ordenada.
- Una orden completa genera una sola factura/CxP consolidada.

### Validacion

- `OrdenCompraServiceTests`: parcial→completa, exceso bloqueado, reintento idempotente y factura
  sin duplicar inventario.
- `V3OrdenCompraContractTests`: endpoint de recepciones, verbos, modulo y permisos.
- `DemoReadinessContractTests`: rutas y vistas Razor de ordenes.
- `EmpresaPruebaSeederTests`: recepcion demo y movimiento enlazado unicos tras dos ejecuciones.
- Build 0 warnings/0 errores y suite 725 unitarias + 9 integracion.

## V3-S3 - Vacaciones y Aguinaldo NeoRRHH

Siguiente sprint recomendado.

Alcance inicial:

- Politicas por empresa para acumulacion, antiguedad y periodos.
- Solicitud/aprobacion de vacaciones, saldo y calendario.
- Calculo de aguinaldo segun antiguedad y reglas vigentes de El Salvador.
- Impacto trazable en planilla, API, UI Web, auditoria, seed y pruebas.
