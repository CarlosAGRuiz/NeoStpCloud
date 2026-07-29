# Planes, usuarios y permisos

Referencia del modelo comercial y de seguridad. **Los datos salen del seed de la aplicación**
(`SeedData.cs`), no de una tabla mantenida a mano: si cambia el seed, cambia esto.

Última verificación contra la base: **2026-07-27**.

---

## 1. Planes comerciales

Siete planes activos. **Los límites se aplican de verdad**: `LicenciaGuardService` bloquea la
creación de usuarios y la emisión de DTE al pasarse del cupo (`LIMIT_EXCEEDED`), y una empresa que
no esté `ACTIVA` no puede operar.

| Plan | Código | $/mes | DTE/mes | Usuarios | Sucursales | Puntos de venta | Módulos |
|---|---|---:|---:|---:|---:|---:|---:|
| Starter | `STARTER` | 15 | 100 | 1 | 1 | 2 | 2 |
| Pyme | `PYME` | 35 | 500 | 3 | 1 | 3 | 3 |
| Pro | `PRO` | 75 | 2.000 | 8 | 3 | 8 | 7 |
| Contador | `CONTADOR` | 120 | 5.000 | 25 | 10 | 20 | 4 |
| Business Full | `BUSINESSFULL` | 150 | 10.000 | 25 | 10 | 25 | 16 |
| Integrador API | `INTEGRADORAPI` | 250 | 30.000 | 10 | 5 | 10 | 3 |
| Enterprise | `ENTERPRISE` | 400 | 50.000 | 100 | 50 | 100 | 18 |

> **Contador e Integrador API no son "planes chicos"**: tienen pocos módulos a propósito porque
> venden profundidad en una sola dirección. Contador vende multi-empresa y libros fiscales;
> Integrador API vende la API. No se comparan por número de módulos con Business Full.

### Qué desbloquea cada plan

| # | Módulo | Starter | Pyme | Pro | Contador | Business Full | Integr. API | Enterprise |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| 100 | CORE | ● | ● | ● | ● | ● | ● | ● |
| 101 | NEODTE | ● | ● | ● | ● | ● | ● | ● |
| 102 | NEOPOS | | ● | ● | | ● | | ● |
| 103 | NEOSCANAI | | | ● | | ● | | ● |
| 104 | NEOPROFIT | | | | | ● | | ● |
| 105 | NEOBI | | | | ● | ● | | ● |
| 106 | NEOCONNECT | | | | | | ● | ● |
| 107 | NEOPORTAL | | | | | | | ● |
| 108 | CONTINGENCIA | | | ● | | ● | | ● |
| 109 | EVENTOSDTE | | | | | ● | | ● |
| 110 | INVENTARIO | | | ● | ● | ● | | ● |
| 111 | COMPRAS | | | | | ● | | ● |
| 112 | GASTOS | | | | | ● | | ● |
| 113 | NEORRHH | | | | | ● | | ● |
| 114 | NEOCRM | | | ● | | ● | | ● |
| 115 | NEOTESORERIA | | | | | ● | | ● |
| 116 | NEOCONTA | | | | | ● | | ● |
| 117 | NEOAGENDA | | | | | ● | | ● |

El bloqueo es **en servidor**: `RequireModuloAttribute` en los controladores web y `RequireModule`
en los de API. Si el plan no incluye el módulo, la pantalla no abre aunque se escriba la URL —
muestra qué hace el módulo y en qué plan viene.

### Por tipo de negocio

No hay versiones distintas del producto por rubro: es el mismo sistema con módulos y una plantilla
de catálogo que se aplica en el onboarding.

| Negocio | Plan sugerido | Qué lo resuelve |
|---|---|---|
| Negocio que hoy factura a mano | Starter | Solo facturación electrónica |
| Tienda, restaurante | Pyme | Cobra en mostrador y factura en el acto |
| Ferretería | Pro | Inventario + precios por volumen y unidades alternativas |
| Farmacia | Pro / Business Full | Inventario con lotes, vencimientos y consumo FEFO |
| Salón, barbería, spa | Business Full | Agenda por empleado sin traslapes + comisiones |
| Despacho contable | Contador | Varias empresas con un login + consolidado + libros |
| Software house / e-commerce | Integrador API | API con llaves por scope y webhooks |
| Corporativo, cadena | Enterprise | SSO, sucursales, aprobaciones, portal, todo |

---

## 2. Usuarios existentes

Contraseña de todas las cuentas demo: **`Demo2026$`**

| Usuario | Nombre | Tipo | Empresa | Plan | Rol | Membresías |
|---|---|---|---|---|---|---:|
| `superadmin` | SuperAdmin NeoSTP | SUPERADMIN | — (global) | — | SUPERADMIN | 0 |
| `demo.exportacion` | Demo Profesional Exportación | ADMIN | NeoSTP (la real) | Starter | ADMIN | 0 |
| `demo.starter` | Marta Recinos | ADMIN | Tienda La Esquina | Starter | ADMIN | 0 |
| `demo.pos` | Julio Menéndez | ADMIN | El Buen Sabor | Pyme | ADMIN | 0 |
| `demo.contador` | Lucía Portillo | ADMIN | Contadores Asociados | Contador | ADMIN | **4** |
| `demo.negocios` | Ricardo Alvarenga | ADMIN | Grupo Vertical | Business Full | ADMIN | 0 |
| `demo.enterprise` | Andrea Bonilla | ADMIN | Corporación Industrial | Enterprise | ADMIN | 0 |

- **`superadmin`** no pertenece a ninguna empresa. Entra a todo por diseño y **evade la validación de
  permisos**. Por eso nunca hay que probar una pantalla nueva con él: un permiso mal otorgado se ve
  bien con superadmin y falla con un ADMIN real.
- **`demo.exportacion`** es de la empresa real de Carlos (NEO SOFTWARE TECH PRO), con 50 DTE de
  prueba incluidos los de exportación. **No es una cuenta demo desechable.**
- **`demo.contador`** tiene 4 membresías (E1): opera las otras cuatro empresas demo con un solo login
  y ve el consolidado del grupo en `/Grupo`.

El ambiente demo se re-siembra con `DemoComercial:Enabled=true`. Es idempotente y **nunca borra
empresas con documentos emitidos**.

---

## 3. Roles del sistema

Cinco roles sembrados, marcados `EsSistema` (no se pueden borrar). Cada empresa puede crear roles
propios además de estos.

| Id | Código | Nombre | Para quién | Permisos |
|---:|---|---|---|---:|
| 500 | `SUPERADMIN` | SuperAdmin NeoSTP | Nosotros, no el cliente | 83 |
| 501 | `ADMIN` | Administrador | El dueño o gerente del cliente | 80 |
| 502 | `OPERADOR` | Operador | Cajero, vendedor, recepción | 41 |
| 503 | `CONTADOR` | Contador | Contador interno o externo | 30 |
| 504 | `READONLY` | Solo lectura | Consulta, auditoría | 14 |

**Cómo se combina con los módulos:** el permiso dice *qué puede hacer* la persona; el módulo dice
*qué compró* la empresa. Hacen falta los dos. Un OPERADOR con `Pos.Vender` en una empresa Starter no
entra al POS, porque el plan no incluye NEOPOS.

### Qué puede cada rol, en corto

- **ADMIN** — todo lo de su empresa: configuración, usuarios, roles, DTE, POS, inventario, compras,
  cobros, RRHH, tesorería, contabilidad, integraciones, SSO y exportar los datos. No tiene los
  permisos de plataforma (`SuperAdmin.*`, `Ops.Hardening.*`).
- **OPERADOR** — opera y vende: emite y consulta DTE, vende y anula en POS, mueve inventario,
  gestiona clientes/productos, cobra, agenda citas. No configura ni ve nómina ni tesorería.
- **CONTADOR** — mira y reporta: consulta DTE, invalida, ve libros fiscales, contabilidad,
  tesorería, compras, nómina y auditoría. Casi no escribe.
- **READONLY** — solo consulta: empresa, usuarios, catálogos, clientes, productos, DTE, reportes,
  CRM, portal y contabilidad.

---

## 4. Catálogo de permisos

**83 permisos** en 16 módulos. El código es la clave que usan `[RequirePermiso("…")]` en API y
`Has("…")` en Web.

| Módulo | Permisos |
|---|---|
| CORE (24) | `Core.Empresa.Ver/Editar`, `Core.Usuarios.Ver/Crear/Editar/Bloquear`, `Core.Roles.Administrar`, `Core.Sucursales.Administrar`, `Core.PuntosVenta.Administrar`, `Core.Auditoria.Ver`, `Core.Catalogos.Ver/Administrar/Importar`, `Core.Correo.Configurar`, `Clientes.Ver/Crear/Editar`, `Productos.Ver/Crear/Editar`, `Cobros.Ver/Gestionar`, `Seguridad.Sso.Gestionar`, `Datos.Exportar` |
| NEODTE (10) | `DTE.Emitir/Consultar/Reenviar/Invalidar/Configurar/Contingencia/Diagnostico`, `DTE.Eventos.Ver`, `Core.Certificacion.Ver/Operar` |
| NEOCRM (8) | `Crm.Contactos.*`, `Crm.Oportunidades.*`, `Crm.Actividades.*`, `Crm.Cotizaciones.*` (Ver/Gestionar cada uno) |
| NEOCONNECT (6) | `API.Configurar`, `Connect.ApiKeys.Ver/Administrar`, `Connect.Webhooks.Ver/Administrar`, `Connect.Logs.Ver` |
| ADMIN (5) | `SuperAdmin.Empresas.Administrar`, `SuperAdmin.Planes.Administrar`, `SuperAdmin.Soporte.Entrar`, `Ops.Hardening.Ver/Administrar` |
| COMPRAS (5) | `Compras.Ver/Gestionar/Aprobar`, `Compras.Proveedores.Ver/Gestionar` |
| NEOPOS (4) | `Pos.Ver/Vender/Anular/Configurar` |
| NEORRHH (4) | `Rrhh.Empleados.Ver/Gestionar`, `Rrhh.Nomina.Ver/Gestionar` |
| NEOTESORERIA (4) | `Tesoreria.Cuentas.Ver/Gestionar`, `Tesoreria.Movimientos.Ver/Gestionar` |
| INVENTARIO (2) | `Inventario.Ver/Gestionar` |
| NEOAGENDA (2) | `Agenda.Ver/Gestionar` |
| NEOCONTA (2) | `Conta.Ver/Gestionar` |
| NEOPORTAL (2) | `Portal.Enlaces.Ver/Gestionar` |
| NEOPROFIT (2) | `Profit.Ver/Gestionar` |
| NEOSCANAI (2) | `ScanAI.Ver/Confirmar` |
| NEOBI (1) | `Reportes.Ver` |

**Siguiente id libre: 427.**

### Permisos sensibles

| Permiso | Qué habilita | Quién lo tiene |
|---|---|---|
| `Compras.Aprobar` | Autorizar órdenes sobre el umbral de la empresa | SUPERADMIN, ADMIN |
| `Seguridad.Sso.Gestionar` | Configurar el SSO corporativo y el auto-aprovisionamiento | SUPERADMIN, ADMIN |
| `Datos.Exportar` | Descargar **todos** los datos de la empresa en un ZIP | SUPERADMIN, ADMIN |
| `Connect.ApiKeys.Administrar` | Crear llaves de API con acceso programático | SUPERADMIN, ADMIN |
| `Connect.Webhooks.Administrar` | Suscribir webhooks a eventos del negocio | SUPERADMIN, ADMIN |
| `SuperAdmin.Soporte.Entrar` | Entrar a la empresa de un cliente en modo soporte | Solo SUPERADMIN |
| `Ops.Hardening.*` | Backups, cuotas, allowlist de IP | Solo SUPERADMIN |

---

## 5. Reglas al agregar permisos o módulos

Tres errores ya ocurrieron y cada uno pasó desapercibido por la misma razón: **el superadmin evade
la validación**, así que en pruebas todo se ve bien.

1. **Un permiso nuevo hay que otorgarlo a algún rol.** Sembrarlo no basta. Pasó con NeoAgenda
   (422/423), aprobación de compras (424) y los webhooks de NeoConnect (353–355): la pantalla
   existía y nadie salvo el superadmin podía entrar. Lo cubre
   `PermisosOtorgadosTests.TodoPermisoSembrado_EstaOtorgadoAAlgunRol`.
2. **Un controlador web de un módulo vendible necesita `[RequireModulo("CODIGO")]`** además de
   `[Authorize]`. Sin eso el módulo queda accesible por URL a cualquier plan, y la escalera de
   precios deja de ser real.
3. **Probar con un ADMIN real, nunca con el superadmin.** Es la única forma de ver los dos errores
   anteriores antes de que los vea un cliente.

Además: el estado de una empresa es **`ACTIVA`** (femenino, `EmpresaEstados.Activa`), no `ACTIVO`.
Con el valor equivocado el enforcement la trata como suspendida y sus usuarios no entran.

---

## Referencias

- Guion de venta y recorrido de demostración: [`Guia-Demo-Ventas.md`](Guia-Demo-Ventas.md)
- Checklist para producción: [`Runbook-Salida-Produccion.md`](Runbook-Salida-Produccion.md)
- SSO corporativo: [`SSO-Enterprise.md`](SSO-Enterprise.md)
- API e integraciones: [`NeoConnect-API-v1.md`](NeoConnect-API-v1.md)
- Fuente de verdad del seed: `src/NeoSTP.Infrastructure/Persistence/Seed/SeedData.cs`
