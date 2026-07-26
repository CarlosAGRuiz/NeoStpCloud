# Guía de demostración comercial — NeoSTP Cloud

Ambiente de demo con **5 cuentas**, una por punto de la escalera de precios. Cada una
entra al mismo sistema; lo que cambia es **qué módulos desbloquea su plan**. Ese es el
argumento de venta central: *no vendemos software distinto por rubro, vendemos el mismo
sistema con el plan que cada negocio necesita.*

**Contraseña de todas las cuentas: `Demo2026$`**

| # | Usuario | Empresa | Plan | Precio/mes | Qué demuestra |
|---|---|---|---|---|---|
| 1 | `demo.starter` | Tienda La Esquina | STARTER | **$15** | Facturación electrónica y nada más. El piso de entrada. |
| 2 | `demo.pos` | El Buen Sabor | PYME | **$35** | Facturación + punto de venta. Cobra en mostrador. |
| 3 | `demo.contador` | Contadores Asociados | CONTADOR | **$120** | Un contador con varias empresas: cambia entre ellas y ve el consolidado. |
| 4 | `demo.negocios` | Grupo Vertical | BUSINESS FULL | **$150** | Tipos de negocio: farmacia, ferretería, salón. |
| 5 | `demo.enterprise` | Corporación Industrial | ENTERPRISE | **$400** | Corporativo: SSO, API, portal, sucursales, aprobaciones. |

---

## Recorrido sugerido (20–25 minutos)

### 1. Arranca por el dolor: `demo.starter` — $15

Entra y muestra el **dashboard**: DTE del mes, facturado, estado ante Hacienda.
Emite una factura en vivo (**Nueva Factura**) y muestra el PDF y el correo al cliente.

> "Esto es lo mínimo que la ley exige. Quince dólares al mes y el negocio ya factura
> electrónicamente. El que hoy factura a mano entra por aquí."

Abre el menú lateral y hazlo notar: **no hay inventario, ni compras, ni caja**. Ese es el
gancho para subir de plan.

### 2. El salto natural: `demo.pos` — $35

Mismo sistema, ahora con **Punto de Venta**. Muestra una venta de mostrador y cómo se
convierte en factura. Aquí ya hay ventas POS registradas.

> "El restaurante o la tienda cobra en caja y factura en el mismo acto. Veinte dólares
> más al mes."

### 3. El argumento más fuerte para vender volumen: `demo.contador` — $120

Entra y ve directo al chip **"Grupo"** en la barra superior → **Consolidado de grupo**.

Verás las 5 empresas en una sola tabla: ventas del mes, IVA débito, cartera por cobrar,
vencido, rechazados y alertas — con el total del grupo abajo. Dos empresas aparecen
marcadas como *por atender* (tienen cartera vencida).

> "Un contador que lleva 20 clientes no quiere 20 sistemas ni 20 contraseñas. Entra una
> vez, ve todo el grupo, y con un clic entra a operar en el cliente que necesite."

Usa el botón **Entrar** de cualquier fila para saltar a esa empresa, y el selector de
empresa del encabezado para volver. **Este es el diferenciador contra la competencia local.**

### 4. Un sistema, muchos rubros: `demo.negocios` — $150

Aquí está el catálogo completo de operación: **Inventario** (con lotes y vencimientos para
farmacia), **Compras** con aprobación por monto, **Agenda** de citas para salón,
**Tesorería**, **Contabilidad**, **RRHH**, **NeoProfit**.

Muestra **Inventario → Lotes** (control de vencimientos, farmacia) y **Agenda** (citas y
comisiones, salón). Menciona precios por volumen y unidades alternativas (ferretería).

> "Farmacia, ferretería, salón, tienda: el mismo sistema. No hay versión distinta por
> rubro, hay módulos que se activan. Por eso podemos venderle a cualquier negocio."

### 5. El techo: `demo.enterprise` — $400

Muestra **SSO corporativo** (entrar con la cuenta de Microsoft/Google de la empresa),
**NeoConnect** (API y webhooks para integrar con el ERP del cliente), **Portal** de
clientes, **múltiples sucursales** con traslados de inventario, y **aprobación de órdenes
de compra** sobre un umbral.

> "Aquí ya no compite el precio, compite el control: quién aprueba, quién entra, qué se
> integra. Este es el cliente que firma a un año."

---

## Tabla de módulos por plan (para responder en frío)

| Plan | Precio | DTE/mes | Usuarios | Sucursales | Módulos |
|---|---|---|---|---|---|
| Starter | $15 | 100 | 1 | 1 | Facturación electrónica |
| Pyme | $35 | 500 | 3 | 1 | + Punto de venta |
| Pro | $75 | 2.000 | 8 | 3 | + Inventario, CRM, escaneo de documentos, contingencia |
| Contador | $120 | 5.000 | 25 | 10 | Facturación + libros fiscales + inventario, multi-empresa |
| Business Full | $150 | 10.000 | 25 | 10 | Casi todo: compras, gastos, RRHH, tesorería, contabilidad, agenda, NeoProfit |
| Integrador API | $250 | 30.000 | 10 | 5 | Facturación + NeoConnect (API/webhooks) para software houses |
| Enterprise | $400 | 50.000 | 100 | 50 | Todo + portal de clientes + SSO |

> Los límites son reales: el sistema **bloquea** al pasarse del cupo de usuarios o de DTE
> del mes, y suspende el acceso si la suscripción vence. No es letra menuda, está
> implementado.

## Preguntas frecuentes del prospecto

**"¿Esto está certificado por Hacienda?"**
El sistema genera, firma y transmite DTE contra los servicios del Ministerio de Hacienda,
y maneja contingencia e invalidaciones. Cada empresa carga su propio certificado.

**"¿Y si se cae el internet o Hacienda?"**
Hay modo contingencia: se sigue facturando y los documentos se transmiten después,
automáticamente.

**"Ya tengo un sistema contable / ERP."**
Con el plan Integrador API o Enterprise se conecta por API y webhooks; no hay que
reemplazar lo que ya usa.

**"¿Puedo empezar barato y crecer?"**
Sí — es el mismo sistema, se cambia el plan y se activan módulos. No hay migración ni
pérdida de datos.

---

## Notas para quien prepara el ambiente

- El ambiente se siembra solo con `DemoComercial:Enabled=true` en configuración
  (`DemoComercialSeeder`). Es **idempotente**: si las empresas ya existen, no las duplica.
- El seeder **no borra** empresas que tengan documentos emitidos.
- Los datos son de demostración en ambiente de **PRUEBAS**; ninguna factura demo se
  transmitió a Hacienda.
- Para reconstruir desde cero: borrar las empresas con NIT `0614010101100X` y volver a
  arrancar la aplicación.
