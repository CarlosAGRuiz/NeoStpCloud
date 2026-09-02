# NeoSTP.Web

App MVC/Razor de NeoSTP Cloud. Es la interfaz operativa para empresas, DTE, clientes, POS,
compras, cobros, inventario, RRHH, CRM, portal y soporte. La Web no delega la firma DTE a la API:
usa sus propios servicios, configuracion local y DataProtection.

## Resumen

- Proyecto: `src/NeoSTP.Web/NeoSTP.Web.csproj`.
- Puerto local recomendado: `http://127.0.0.1:5031`.
- Health: `/health/live` y `/health/ready`.
- Auth: cookie de sesion; la empresa activa sale de `IEmpresaContext`.
- UI: Razor + Bootstrap 5 + `wwwroot/css/neostp.css` con componentes `ns-*`.
- Permisos: controllers con modulo/permiso; SuperAdmin puede usar modo soporte sobre empresa.

## Ejecucion local

```powershell
dotnet build NeoSTP.slnx
dotnet run --project src/NeoSTP.Web
```

La Web carga `src/NeoSTP.Web/appsettings.Local.json` si existe. Ese archivo esta ignorado por git y
debe contener solo configuracion local, nunca secretos commiteados.

## Inicio automatico local

Para que API y Web arranquen al iniciar sesion en la PC de pruebas:

```powershell
./scripts/install-local-autostart.ps1
```

El script publica en Release a `out/local-autostart`, copia los `appsettings.Local.json` de API/Web y
crea dos tareas programadas:

| Tarea | URL | Validacion |
|---|---|---|
| `NeoSTP API` | `http://127.0.0.1:5058` | `/health` |
| `NeoSTP Web` | `http://127.0.0.1:5031` | `/health/live` |

Es una tarea al iniciar sesion del usuario actual, no un Windows Service. Esto conserva el mismo
perfil de Windows para DataProtection, certificados y secretos locales usados al firmar.

Si el script falla con puertos ocupados, cerrar procesos viejos `NeoSTP.Web.exe` y `NeoSTP.Api.exe`,
o procesos `dotnet run` de Debug, y ejecutarlo nuevamente.

## DTE en Web

Rutas principales:

| Ruta | Uso |
|---|---|
| `/DteDocumentos` | Listado de DTE con filtros. |
| `/DteDocumentos/Create` | Nuevo DTE y seleccion de cliente/productos. |
| `/DteDocumentos/Details/{id}` | Detalle, stepper de estado, PDF/JSON/JWS/reenviar. |
| `/DteEventos/CreateRetorno` | Evento de retorno. |
| `/Clientes` | Clientes y datos fiscales/territoriales. |

### Clientes y territorio

- Si el pais es `SV` o queda vacio, departamento y municipio son visibles y deben seleccionarse
  juntos.
- Si el pais es distinto de `SV`, departamento y municipio se ocultan y se limpian antes de guardar.
- El municipio se filtra por departamento; no se acepta un municipio de otro departamento.
- El backend normaliza codigos MH de tipo documento (`13`, `36`, etc.) al codigo interno usado por
  clientes (`DUI`, `NIT`, etc.).

### Nuevo DTE

- Al seleccionar un cliente registrado, la Web llena nombre, tipo de documento, documento, NRC y
  correo desde la ficha del cliente.
- Mientras hay cliente registrado seleccionado, esos campos se muestran como solo lectura porque el
  servidor toma el snapshot desde `ClienteId`.
- Al volver a modo manual, los campos quedan editables.

### Listado y busquedas

`/DteDocumentos` permite filtrar por busqueda libre, tipo DTE, estado, fecha desde/hasta y rango de
monto. La busqueda libre cubre numero de control, codigo de generacion, nombre del receptor y
documento del receptor. La paginacion conserva todos los filtros.

### Retorno

La pantalla de retorno carga solo DTE procesados elegibles: 01 Factura, 11 Exportacion y 14 Sujeto
Excluido. El selector permite filtrar en cliente por tipo, numero de control, codigo de generacion,
cliente, documento y monto dentro de los documentos cargados.

### Procesado

Cuando un DTE llega a `PROCESADO`, el ultimo paso del stepper se pinta como completado igual que los
estados anteriores. El estado fiscal/badge del documento se mantiene separado.

## Troubleshooting DTE

Mensaje: `Documento creado pero no se pudo generar JSON: La empresa emisora tiene datos incompletos`

- El DTE ya fue creado como borrador, pero fallo la generacion JSON inmediata.
- Verificar Empresa -> Editar: NIT, correo y telefono son obligatorios para emitir.
- Con el servicio actual, el mensaje incluye `Detalle:` con el campo exacto faltante.
- Si la empresa fue creada con nombres visibles en departamento/municipio, el servicio los resuelve
  contra los catalogos antes del JSON. No hace falta modificar historicos solo por ese formato.
- Si se instalo autostart, confirmar que los ejecutables en `out/local-autostart` fueron publicados
  despues del ultimo commit y que `/health/live` responde 200.

## Guardrails

- No commitear `appsettings.Local.json`, certificados, passwords, API keys ni material privado.
- Mantener todo acceso operativo filtrado por `EmpresaId`.
- Para cambios compartidos de DTE, correr prueba focalizada y luego `dotnet test NeoSTP.slnx`.

