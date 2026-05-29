# Sprint 11 — Runbook: de Mocks a Integraciones Reales

Guía paso a paso para llevar una empresa de pruebas desde el modo *Mock* (default)
hasta emitir DTE reales contra el **ambiente de pruebas de Hacienda El Salvador**.

> ⚠️ **Todo este flujo es contra `apitest.dtes.mh.gob.sv` (PRUEBAS).** No usar credenciales
> ni certificados de producción hasta el Sprint de salida a producción.

---

## 0. Requisitos previos (los provees tú)

| Recurso | Dónde se obtiene |
| ------- | ---------------- |
| Certificado `.pfx` / `.crt` del emisor | Portal de Hacienda (firma electrónica) |
| Password del certificado | Definido al exportar el `.pfx` |
| Usuario MH (NIT del emisor) | Credenciales del ambiente de pruebas MH |
| Password MH | Credenciales del ambiente de pruebas MH |
| NIT / NRC reales del emisor | Documentación fiscal de la empresa |
| Código de establecimiento MH | Asignado por Hacienda al registrar el establecimiento |
| Código de punto de venta MH | Asignado por Hacienda |

---

## 1. Provisionar la empresa de pruebas (automático)

El seeder `EmpresaPruebaSeeder` crea la empresa completa al arrancar la **Api**, de forma
**idempotente** (si ya existe por NIT, no hace nada).

### 1.1 Configurar `appsettings.Local.json` de la **Api**

```json
{
  "EmpresaPrueba": {
    "Enabled": true,
    "Nit": "06140000000000",
    "Nrc": "000000-0",
    "RazonSocial": "NeoSTP Pruebas, S.A. de C.V.",
    "NombreComercial": "NeoSTP Pruebas",
    "CodigoActividad": "62010",
    "ActividadEconomica": "Programación informática",
    "Departamento": "06",
    "Municipio": "0614",
    "Direccion": "Col. Escalón, San Salvador",
    "Telefono": "2222-0000",
    "Correo": "facturacion@neostp.com",
    "PlanCodigo": "ENTERPRISE",
    "Admin": {
      "Username": "admin.prueba",
      "Email": "admin@neostp.com",
      "Password": "ChangeMe!2026",
      "NombreCompleto": "Administrador de Pruebas"
    },
    "Sucursal": {
      "Codigo": "0001",
      "Nombre": "Casa Matriz",
      "TipoEstablecimientoCodigo": "CASA_MATRIZ",
      "CodigoEstablecimientoMh": "0001"
    },
    "PuntoVenta": {
      "Codigo": "0001",
      "Nombre": "Principal",
      "CodigoPuntoVentaMh": "0001"
    },
    "Dte": {
      "AmbienteCodigo": "PRUEBAS",
      "UsuarioMh": "06140000000000",
      "TipoEstablecimientoCodigo": "CASA_MATRIZ",
      "CodigoEstablecimientoMh": "0001",
      "CodigoPuntoVentaMh": "0001"
    }
  }
}
```

> Los **secretos** (password MH, certificado PFX, password del certificado) **NO** se
> ponen aquí — se cargan en el paso 3/4 vía UI para que queden cifrados con DataProtection.

### 1.2 Arrancar la Api

```powershell
dotnet run --project src/NeoSTP.Api
```

En los logs verás:

```
EmpresaPruebaSeeder: empresa 'NeoSTP Pruebas, S.A. de C.V.' (NIT 06140000000000) creada con
plan ENTERPRISE, 13 módulos, sucursal 'Casa Matriz', PV 'Principal' y admin 'admin.prueba'…
```

### 1.3 Verificar

- Login en la Web con `admin.prueba` / la password configurada.
- `/Empresas` → la empresa aparece activa.
- `/Sucursales` → "Casa Matriz" con su punto de venta "Principal".
- `/DteConfiguracion` → ambiente PRUEBAS, usuario MH y códigos de establecimiento prellenados.

> **Una vez creada, pon `EmpresaPrueba:Enabled = false`** para que no vuelva a evaluarse.

---

## 2. Validar la configuración DTE (estado “Completa”)

Ir a **`/DteConfiguracion`** y revisar que estén presentes:

- Ambiente **PRUEBAS**
- Usuario MH
- Tipo de establecimiento + código de establecimiento MH
- Código de punto de venta MH

Aún falta cargar **password MH** y **certificado PFX** (pasos 3 y 4). La configuración
mostrará estado **Incompleta** hasta entonces.

---

## 3. Cargar credenciales MH y activar Hacienda real

### 3.1 Password MH (cifrado)

En `/DteConfiguracion` → campo **Password MH** → guardar. Se cifra con DataProtection
(`PasswordMhCifrado`), nunca se devuelve en respuestas de la API.

### 3.2 Cambiar el toggle a HTTP

En `appsettings.Local.json` de **Api** (y **Worker** si retransmite):

```json
{
  "Hacienda": {
    "Client": "Http",
    "PruebasBaseUrl": "https://apitest.dtes.mh.gob.sv",
    "TimeoutSeconds": 30
  }
}
```

> Con `Client = Http` se activan los clientes `HttpHaciendaAuthClient` y
> `HttpHaciendaReceptionClient` con resiliencia Polly (retry + circuit breaker + timeouts).

### 3.3 Probar conexión

`/DteConfiguracion` → botón **Probar Conexión** (o `POST /api/dte/configuracion/probar-conexion`).

- ✅ **OK** → autenticación correcta contra apitest, queda registrado en `UltimaPrueba*` y auditoría.
- ❌ **HACIENDA_AUTH_FAILED** → revisar usuario/password MH y la base URL. HTTP 502.

---

## 4. Cargar certificado y activar firma real Pkcs12

### 4.1 Subir el PFX

`/DteConfiguracion` → **Certificado** → cargar `.pfx` (base64) + password del certificado.
Se guardan `CertificadoBlob`, `CertificadoHuella`, fechas de vigencia y
`PasswordCertificadoCifrado` (cifrado).

### 4.2 Cambiar el toggle de firma

```json
{
  "Dte": {
    "Signer": "Pkcs12"
  }
}
```

> Con `Signer = Pkcs12` se usa `Pkcs12DteSignerService` (RS256, header con `x5t`).

### 4.3 Validar firma

Firmar un DTE de prueba (paso 6). Si falla → **FIRMA_FAILED** (HTTP 502): revisar password
del certificado y vigencia del PFX.

---

## 5. Correo (opcional en pruebas)

### Opción A — Mock (recomendado para iniciar)

```json
{ "Email": { "Provider": "Mock", "MockOutbox": "logs/email-outbox" } }
```

Los correos quedan como `.eml` en `logs/email-outbox/` — útil para validar el contenido
sin SMTP real.

### Opción B — SMTP real

```json
{
  "Email": {
    "Provider": "Smtp",
    "From": { "Address": "facturacion@neostp.com", "DisplayName": "NeoSTP Cloud" },
    "Smtp": { "Host": "smtp.tu-proveedor.com", "Port": 587, "UseStartTls": true,
              "Username": "...", "Password": "..." }
  }
}
```

---

## 6. Ejecutar la matriz de pruebas DTE

Ver **`Sprint11-Matriz-Pruebas-DTE.md`** para el checklist completo de los 5 tipos
(01, 03, 05, 06, 14) con el flujo Validar → Firmar → Enviar → Descargar → Reenviar.

---

## Tabla de toggles (resumen)

| Toggle | Mock (default) | Real (Sprint 11) |
| ------ | -------------- | ---------------- |
| `Hacienda:Client` | `Mock` | `Http` → apitest |
| `Dte:Signer` | `Mock` | `Pkcs12` → PFX cargado |
| `Email:Provider` | `Mock` → `.eml` | `Smtp` (opcional) |

## Errores comunes

| Error | Causa | Acción |
| ----- | ----- | ------ |
| `HACIENDA_AUTH_FAILED` (502) | Usuario/password MH incorrectos o base URL mala | Reingresar password MH, verificar `PruebasBaseUrl` |
| `FIRMA_FAILED` (502) | Password del certificado errado o PFX vencido | Recargar PFX, verificar vigencia |
| `EMAIL_FAILED` (502) | SMTP mal configurado | Volver a `Provider = Mock` o corregir host/credenciales |
| Configuración “Incompleta” | Falta password MH, PFX o códigos de establecimiento | Completar campos en `/DteConfiguracion` |
