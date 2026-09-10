# Auditoría WEB aislada — 2026-09-04

## Verificación de distrito — corte 08/09/2026

`dotnet run --project tools/WebAuditPreview/WebAuditPreview.csproj -c Release --no-restore -- <raiz-repo> --district-mail` renderiza Clientes/Edit y DteDocumentos/Create con datos sintéticos. `node tools/WebAuditPreview/verify-district.cjs <raiz-repo>` ejecuta 9 comprobaciones de cascada, autocompletado, extranjero y móvil. Resultado observado: 9 aprobadas, cero errores JavaScript. HTML, capturas y reporte local en `tmp/district-mail/web/`. Sin SQL, SMTP, Hacienda ni solicitudes reales. Los resultados inferiores pertenecen al arnés anterior.

Renderiza 11 fixtures desde las vistas Razor compiladas reales de NeoSTP.Web y ejecuta
interacciones en Edge headless mediante Playwright. No modifica `src`, no levanta API/Web,
no registra Infrastructure, no abre SQL, no transmite DTE ni emplea datos/credenciales reales.
No hay servidor ni puerto. Todas las solicitudes del navegador se interceptan: se sirven
solo HTML generado y CSS/JS locales, y se bloquea cualquier petición externa o no GET.

## Reproducción

```powershell
dotnet restore tools/WebAuditPreview/WebAuditPreview.csproj --source https://api.nuget.org/v3/index.json
dotnet run --project tools/WebAuditPreview/WebAuditPreview.csproj -c Release --no-restore -- C:/Neo/NeoSTPBusinnesSuite/NeoStpCloud
node tools/WebAuditPreview/verify.cjs C:/Neo/NeoSTPBusinnesSuite/NeoStpCloud
```

Node debe poder resolver Playwright (`NODE_PATH` solo para el proceso si se usa el runtime
bundled). No ejecutar en paralelo con otro build que escriba los mismos proyectos.

Salida: `tmp/multiagent-qa/web/` contiene HTML, snapshots `dte-*.model.json`,
16 capturas PNG y `browser-report.json`. Los diagnósticos de los snapshots se generan con
`DteDiagnosticoGuia.Crear`, no una copia del traductor. Datos y sellos son sintéticos.

## Resultado observado

27 aserciones: **17 PASS y 10 FAIL**, más **1 limitación explícita**. Las 10 fallidas
son hallazgos de UI agrupados, no diez vulnerabilidades independientes:

- Details no muestra el mensaje/corrección estructurados de 008/096 ni la guía de
  conciliación para ENVIADO/CONTINGENCIA, aunque el DTO sí los contiene.
- CONTINGENCIA ofrece Enviar a Hacienda, también al usuario fixture con solo DTE.Consultar;
  ofrece Invalidar. No se enviaron los formularios: esto no demuestra evasión de RBAC backend.
- ERROR previo a firma pinta Validado y Firmado completos; INVALIDADO sin envío pinta
  Enviado completo. Los estados son fixtures sin los hitos correspondientes.
- Cuatro labels de cliente apuntan a IDs inexistentes; clicar Departamento no enfoca su select.

Positivos: visor de login con clic/Espacio/Enter conserva valor y foco, accesibilidad del
toggle y móvil 390px sin overflow; PROCESADO tiene cinco puntos morados `rgb(107,56,212)`;
municipio inicial conservado, cambio departamento limpia municipio incompatible, país
extranjero oculta/deshabilita/limpia territorio y volver a SV lo habilita; filtro retorno
encuentra un DTE cargado por número, código de generación, cliente y monto. Sin pageerror JS.

## Límites indispensables

- Login es vista autónoma real. Las demás usan `_AuditLayout`, un contenedor de prueba:
  **no AppShell, sidebar, servicios, autorización HTTP, SQL ni catálogos reales**.
- Fonts e iconos externos se bloquean. Algunas ligaduras Material Symbols aparecen como
  texto en las capturas; esto es artefacto del arnés y NO se reporta como defecto del producto.
  Las medidas de ancho corresponden a esta fixture, no certifican todo el responsive real.
- `PreviewRouter` es mínimo y no garantiza URLs de controller/ID/query reales; ninguna
  navegación fiscal/POST ni validación server-side se prueba aquí.
- 008/096 se muestran bajo estado local ERROR con respuesta RECHAZADO. La vista comparte
  el panel entre ambos; no representa replay del pipeline ni estado persistido del cliente.
- Cascada usa ParentCodigo y códigos sintéticos: demuestra JS, no integridad de sus catálogos.
- Retorno contiene 200 opciones replicando el límite estático del controller. Buscar doc201
  no encuentra nada porque no está cargado. Es un límite reproducido del selector, NO una
  consulta de backend con 201 documentos reales.
- No se probó alta DTE/autocompletado de cliente en este arnés ni certificación del cliente,
  sesión real, tenant HTTP, PDF, JWS, emisión, reenvío, invalidez, API o APK.

Las capturas clave son login-mobile, dte-contingencia-desktop, dte-readonly-desktop,
dte-error-008/096-desktop, dte-error-before-signing-desktop, cliente-sv-desktop y
cliente-extranjero-mobile. Los navegadores se cierran en `finally`.
