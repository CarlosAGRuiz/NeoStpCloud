# Sprint 11 — Matriz de Pruebas DTE

Checklist de validación end-to-end contra el **ambiente de pruebas de Hacienda**
(`apitest.dtes.mh.gob.sv`). Ejecutar **después** de completar el runbook
(`Sprint11-Runbook-Mocks-a-Real.md`): empresa provisionada, `Hacienda:Client=Http`,
`Dte:Signer=Pkcs12`, certificado y credenciales MH cargados.

---

## Tipos de DTE a probar

| Código MH | Tipo | Particularidad de cálculo |
| --------- | ---- | ------------------------- |
| **01** | Factura Consumidor Final | IVA **incluido** en gravada (informativo) |
| **03** | Comprobante de Crédito Fiscal (CCF) | IVA **separado**, requiere NRC del receptor |
| **05** | Nota de Crédito | IVA separado, requiere documento relacionado, **resta** |
| **06** | Nota de Débito | IVA separado, requiere documento relacionado, **suma** |
| **14** | Factura Sujeto Excluido | **sin IVA**, va como No Sujeta |

---

## Flujo por documento (10 pasos)

Para **cada** tipo, ejecutar y marcar:

```
[ ] 1.  Crear borrador          → estado BORRADOR
[ ] 2.  Generar JSON            → estado GENERADO    (POST /api/dte/documentos/{id}/generar)
[ ] 3.  Validar                 → estado VALIDADO    (POST /api/dte/documentos/{id}/validar)
[ ] 4.  Firmar                  → estado FIRMADO     (POST /api/dte/documentos/{id}/firmar)
[ ] 5.  Enviar a Hacienda       → estado ENVIADO     (POST /api/dte/documentos/{id}/enviar)
[ ] 6.  Recibir respuesta MH    → PROCESADO / RECHAZADO / CONTINGENCIA
[ ] 7.  Descargar JSON          → (GET /api/dte/documentos/{id}/json)
[ ] 8.  Descargar PDF (con QR)  → (GET /api/dte/documentos/{id}/pdf)
[ ] 9.  Reenviar por correo     → (POST /api/dte/documentos/{id}/reenviar)
[ ] 10. Revisar auditoría       → acciones GENERAR/VALIDAR/FIRMAR/ENVIAR registradas
```

Estados esperados del ciclo:

```
BORRADOR → GENERADO → VALIDADO → FIRMADO → ENVIADO → PROCESADO
                                                    ↘ RECHAZADO
                                                    ↘ CONTINGENCIA
```

---

## Datos maestros de prueba (crear antes)

### Clientes (`/Clientes`)

```
[ ] Cliente Consumidor Final  (sin NRC, tipo CONSUMIDOR_FINAL)
[ ] Cliente Contribuyente     (con NIT + NRC, tipo CONTRIBUYENTE)  ← requerido para CCF (03)
```

### Productos (`/Productos`)

```
[ ] Producto gravado con IVA       (BIEN, IVA 13%)
[ ] Servicio gravado con IVA       (SERVICIO, IVA 13%)
[ ] Producto/ítem sujeto excluido  (para DTE 14)
```

Validar formatos: **DUI** `########-#`, **NIT** `####-######-###-#`, **NRC** 1-7 dígitos.

---

## Checklist por tipo

### ☐ 01 — Factura Consumidor Final
- Receptor: Consumidor Final (puede ir sin receptor identificado).
- Línea: producto gravado.
- Verificar: IVA incluido en gravada, total en letras correcto.
- [ ] Llega a respuesta de Hacienda (PROCESADO ideal).
- [ ] PDF incluye QR + código de generación.

### ☐ 03 — Comprobante de Crédito Fiscal
- Receptor: **Contribuyente con NRC** (obligatorio).
- Verificar: IVA separado en el resumen (tributo "20").
- [ ] Llega a respuesta de Hacienda.
- [ ] PDF correcto.

### ☐ 05 — Nota de Crédito
- Requiere **documento relacionado** (un CCF 03 previo).
- Verificar: monto resta del relacionado.
- [ ] Llega a respuesta de Hacienda.

### ☐ 06 — Nota de Débito
- Requiere **documento relacionado**.
- Verificar: monto suma al relacionado.
- [ ] Llega a respuesta de Hacienda.

### ☐ 14 — Factura Sujeto Excluido
- Receptor: sujeto excluido (DUI/NIT).
- Verificar: **sin IVA**, va como compra/no sujeta.
- [ ] Llega a respuesta de Hacienda.

---

## Endpoints validados

```http
POST /api/dte/configuracion/probar-conexion
POST /api/dte/documentos/{id}/generar
POST /api/dte/documentos/{id}/validar
POST /api/dte/documentos/{id}/firmar
POST /api/dte/documentos/{id}/enviar
GET  /api/dte/documentos/{id}/pdf
GET  /api/dte/documentos/{id}/json
POST /api/dte/documentos/{id}/reenviar
```

---

## Criterios de aceptación del Sprint 11

```
[ ] 1.  Empresa NeoSTP de pruebas creada (seeder idempotente).
[ ] 2.  Plan ENTERPRISE/BusinessFull asignado con módulos.
[ ] 3.  Sucursal Casa Matriz + punto de venta Principal configurados.
[ ] 4.  Configuración DTE completa (estado "Completa").
[ ] 5.  Certificado PFX cargado y protegido (DataProtection).
[ ] 6.  Hacienda:Client=Http funcionando contra apitest (Probar Conexión OK).
[ ] 7.  Dte:Signer=Pkcs12 firma correctamente (JWS válido).
[ ] 8.  Al menos una Factura 01 llega a respuesta de Hacienda.
[ ] 9.  PDF incluye QR y código de generación.
[ ] 10. Correo (mock o SMTP) genera salida correcta.
[ ] 11. Auditoría registra acciones críticas.
[ ] 12. Tests existentes siguen pasando (97/97).
```

---

## Registro de resultados

| Tipo | Estado final MH | Sello recibido | PDF/QR | Correo | Fecha | Notas |
| ---- | --------------- | -------------- | ------ | ------ | ----- | ----- |
| 01   |                 |                |        |        |       |       |
| 03   |                 |                |        |        |       |       |
| 05   |                 |                |        |        |       |       |
| 06   |                 |                |        |        |       |       |
| 14   |                 |                |        |        |       |       |
