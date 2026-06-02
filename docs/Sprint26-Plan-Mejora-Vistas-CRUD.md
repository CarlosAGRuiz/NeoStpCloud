# Sprint 26 - Modernizacion de vistas y acciones CRUD

## Sprint recomendado

El siguiente sprint recomendado es **Sprint 26: Modernizacion empresarial de vistas + matriz de acciones CRUD**.

El foco no es solo mejorar apariencia. El sprint debe convertir las vistas existentes en superficies operativas completas: datos reales, acciones esperadas por rol, estados claros, trazabilidad y patrones reutilizables para que la suite se sienta consistente y empresarial.

## Objetivos

- Levantar inventario de todas las vistas MVC actuales y clasificarlas por modulo, rol, criticidad y madurez visual.
- Modernizar vistas basicas hacia el AppShell/design system actual sin introducir datos quemados ni mocks comerciales.
- Definir por vista que acciones aplican: crear, editar, eliminar, desactivar, restaurar, reintentar, descargar, auditar o exportar.
- Priorizar acciones que reducen soporte operativo: facturas con error, catalogos, clientes, productos, billing y certificacion.
- Mantener reglas de negocio: multiempresa por `EmpresaId`, RBAC, permisos por plan/modulo, auditoria y soporte SuperAdmin con empresa seleccionada.

## Alcance por fases

### 26.1 Inventario y matriz de acciones

- Crear una matriz por modulo con: ruta, vista, controlador, modelo, permisos, estado visual, acciones existentes y acciones faltantes.
- Separar vistas en tres niveles: criticas de operacion, administrativas y auxiliares.
- Identificar pantallas que aun muestran datos hardcoded o copias de mockup que no coinciden con base de datos.
- Marcar riesgos de eliminacion: entidades con dependencias, documentos fiscales, pagos, auditoria y catalogos oficiales.

### 26.2 Patrones UI empresariales

- Consolidar patrones reutilizables para listados: filtros, busqueda, estado, acciones por fila, empty states, paginacion y exportacion.
- Consolidar patrones para formularios: header operativo, estado, validaciones visibles, acciones primarias/secundarias y confirmaciones.
- Agregar estilos consistentes para acciones destructivas, acciones de reintento y acciones bloqueadas por permiso/estado.
- Evitar tarjetas decorativas innecesarias; priorizar tablas densas, paneles de resumen y controles claros.

### 26.3 DTE y facturas con errores

- Mejorar listados de DTE para distinguir borrador, validado, firmado, enviado, procesado, rechazado, error y contingencia.
- En facturas con error agregar acciones segun estado: ver detalle MH, corregir, revalidar, refirmar, reenviar, descargar JSON, descargar JWS/PDF y registrar nota interna.
- Mostrar trazabilidad de intentos y respuesta cruda de Hacienda sin obligar al usuario a buscar en logs.
- Validar que las acciones no permitan modificar documentos fiscales ya finalizados salvo flujos legales permitidos.

### 26.4 Catalogos y datos maestros

- Catalogos oficiales: vista moderna con busqueda, filtros, detalle y proteccion contra edicion/eliminacion cuando sean datos normativos.
- Catalogos internos: permitir editar, desactivar y restaurar cuando aplique; evitar eliminacion fisica si hay referencias.
- Clientes y productos: agregar editar, desactivar, restaurar, historial, importacion/exportacion y validaciones de dependencias.
- Sucursales, puntos de venta y empresas: acciones administrativas con auditoria y confirmacion.

### 26.5 Billing, planes y pagos

- Alinear todas las vistas de billing con planes reales de base y estado actual de suscripcion/licencia.
- Agregar acciones para factura/pago: ver detalle, descargar comprobante, reintentar pago, cambiar metodo, cancelar o verificar transferencia segun rol.
- Mejorar estados de plan: trial, activo, vencido, suspendido, cancelado y pendiente de pago.
- Revisar que el checkout y portal no muestren precios o beneficios que no existan en `Planes`.

### 26.6 Certificacion y flujos de prueba

- Mantener la matriz como centro operativo para pruebas DTE/eventos.
- Agregar acciones contextuales por escenario: crear prueba, asociar existente, reintentar, ver error, ver DTE/evento, descargar evidencia.
- Mejorar explicaciones funcionales sin convertir la pantalla en documentacion extensa.
- Asegurar que una prueba creada desde DTE o desde certificacion actualice ambos lados.

### 26.7 QA y cierre

- Ejecutar build Web/API y pruebas unitarias relevantes por modulo tocado.
- Revisar pantallas criticas con navegador local cuando haya cambios visuales amplios.
- Actualizar README con el avance real del sprint.
- Crear commit con alcance cerrado y sin archivos temporales.

## Entregables

- Documento de inventario y matriz de acciones por vista.
- Vistas modernizadas por prioridad, empezando por DTE con errores, catalogos y billing.
- Acciones CRUD/operativas agregadas solo donde sean seguras y soportadas por negocio.
- Validaciones y permisos revisados para cada accion nueva.
- README actualizado al cierre del sprint.

## Criterios de aceptacion

- Ninguna vista modernizada debe depender de datos quemados que contradigan la base.
- Cada accion visible debe tener permiso, estado valido y confirmacion cuando sea destructiva o irreversible.
- Las entidades con impacto fiscal, pagos o auditoria no deben permitir eliminacion fisica sin regla explicita.
- Las pantallas deben mantener consistencia visual con AppShell y funcionar en modo empresa y soporte SuperAdmin.
- Build y pruebas relevantes pasan antes de commit/push.
