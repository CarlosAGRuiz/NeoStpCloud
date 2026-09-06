# CERT-0: inventario de certificación del cliente 23

`Get-ClientCertificationReadiness.ps1` es una comprobación de solo lectura, restringida a la empresa 23 y al NIT esperado confirmado. Rechaza parámetros diferentes antes de leer configuración o abrir SQL. Después verifica conexión local, base `NeoSTP_Cloud`, SQL Server 16, identidad de empresa y ambiente exactamente `PRUEBAS`.

No usa EF, AddInfrastructure, hosts, certificados, firma, DataProtection, autenticación MH, SMTP ni otros servicios externos. Solo ejecuta SELECT fijos. La configuración SQL se consume en memoria sin imprimirla. Los resultados contienen códigos de estado, conteos, fechas y flags de presencia; no selecciona valores de credenciales, XML/JSON/JWS fiscales, certificados, sellos, direcciones ni contenido de escenarios.

## Comando

Desde la raíz del repositorio:

```powershell
./tools/CompanyPreflight/Get-ClientCertificationReadiness.ps1 -EmpresaId 23 -ExpectedNit '06232705261148'
```

Resultado sanitizado: `tmp/client-certification-2026-09-05/readiness-sanitized.json`.

Pruebas negativas efectuadas: empresa 2 y NIT vacío fueron rechazados con código de salida 1, `DatabaseConnectionOpened=false` y `ReadyForTransmission=false`, antes de configuración/SQL.

## Interpretación

- `Passed=true` indica que el inventario autorizado pudo completarse. `ReadyForTransmission` siempre queda en `false` por diseño.
- Los contadores 01=1/90, 03=0/75, 11=0/90 y 14=0/25 proceden exclusivamente de la captura suministrada. Su fecha es desconocida y el portal no se consultó. La resta aritmética no define 279 escenarios oficiales ni acredita una autorización de Hacienda.
- Las matrices y asociaciones son estado **local**. Un DTE PROCESADO con sello almacenado no se convierte automáticamente en una asociación ni valida el contenido de un escenario. No se escribe ni se sincroniza la matriz.
- Las descripciones que contienen `Detalle pendiente` o están vacías se clasifican como genéricas; otras serían candidatas a revisión, no escenarios oficiales validados. El inventario no exporta descripciones.
- `UncertainResultCandidateCount` identifica documentos ENVIADO/ERROR con fecha de envío y sin sello. Requieren clasificar su evidencia durable; no significa que todos sean respuestas perdidas o resultados realmente inciertos en Hacienda.
- Cuota: replica únicamente el cálculo legacy del catálogo para una licencia vigente. Cuenta **todos** los documentos con `CreatedAt >= inicio del mes UTC`, sin filtrar tipo, ambiente o estado y sin límite superior de fecha; borradores, errores y PRUEBAS también cuentan. Si existe adopción de pagos, no presume el catálogo como cuota efectiva. La validación de snapshots no se reproduce.
- Con 79 migraciones y las tablas nuevas de Billing ausentes, `CurrentEntitlementReaderTablesPresent=false`. El cálculo informativo de cuota no demuestra que el guard nuevo pueda ejecutarse ni identifica el comportamiento exacto del binario publicado.
- Los flags de geografía revisan el **formato almacenado**, no su equivalencia en catálogos. El código existente puede normalizar etiquetas. Los ceros de sucursales/puntos tampoco impiden por sí solos el fallback del establecimiento configurado.

## Resultado del 5 de septiembre de 2026

Identidad y PRUEBAS confirmados; 79 migraciones. Se observaron once documentos 01: uno PROCESADO con sello, ocho ERROR candidatos a revisión y dos borradores. No hay documentos de los otros tres tipos. Los 280 escenarios locales de esos cuatro tipos tienen descripción genérica. No hay asociaciones; la factura procesada tampoco está asociada.

La licencia STARTERFE vigente tiene cuota de catálogo 100; consumo desde el inicio de septiembre UTC: 0. Esto no equivale a capacidad para 279 altas en el mes ni define el alcance de la campaña. SMTP del tenant activo: 0; distrito almacenado ausente. La corrección de municipio/distrito confirmada por el usuario se revisa en el plan del coordinador; este script no aplica cambios ni inventa un mapeo de catálogo.

No se habilita transmisión, se actualiza licencia, se reserva numeración ni se completa certificación con este resultado.
