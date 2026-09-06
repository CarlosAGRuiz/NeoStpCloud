# Continuación — certificación DANIEL, lote automático y consumo

## Corte vigente: 2026-09-06, activación comercial y release desplegado

Consultar primero [CIERRE-CLI23-2026-09-06.md](CIERRE-CLI23-2026-09-06.md). Suscripción 2 ACTIVE, cobro mensual a fin de mes (primer vencimiento 30/09/2026 por USD15, OPEN), licencia29 sin corte automático y complementos EVENTOSDTE/CONTINGENCIA activos para empresa23. Se conservan los tipos01/03/11/14.

SQL92 aplicado con respaldo y ensayo; release20260906T153647Z-8691442423df464a9110277a78b50232 arrancado en API/Web y health local/público200. Correcciones de consumo/eventos ya desplegadas. Suite2373unit+9integración; ensayo SQL de concurrencia/replay/rollback correcto. Código consolidado en f74fff5. El documento de cierre detalla el push, evidencias y límites del corte.

La cohorte fiscal mantiene90/75/90/25 y hay15 documentos fuera de campaña que consumen cuota. La certificación local pasó; producción fiscal sigue pendiente de contraste/autorización del portal y credenciales. También siguen pendientes servicios sin sesión, identidad/recuperación de claves, mínimo privilegio SQL y MFA. Los apartados inferiores son históricos y no deben ejecutarse como instrucciones actuales.

**Corte: 05/09/2026, 17:52 El Salvador / 23:52 UTC.** Este documento registra hechos y pendientes; no acredita autorización productiva de Hacienda.

## 0. Actualización 2026-09-06 00:50 UTC (hechos verificados)

- **Lote 275 COMPLETO.** Orquestador `tools/ClientCertificationBatch/Run-BatchAuto.ps1` (reutiliza el
  DLL congelado en bucle; no recompila; aborta ante cualquier exit≠0/STOP/blocker/no-monotónico).
  226 casos aceptados esta corrida (49→275). Cero STOP/ABORT. Log `tmp/client-certification-batch/orchestrator-run-full.log`.
- **Cierre SQL verificado:** consumptions campaña `7bc8a3fb` = 88/74/89/24 = 275; pilotos (campaña `5f681bfb`) = 4 intactos;
  documentos empresa 23 PROCESADO por tipo = **01:90 · 03:75 · 11:90 · 14:25** (totales objetivo 90/75/90/25);
  histórico previo intacto (01: 2 BORRADOR + 8 ERROR); **consumo comercial del mes = 0**.
- **NO acredita producción:** falta contraste del portal MH, eventos, autorización y credenciales productivas.
- **Fix de consumo aplicado (código, aún sin desplegar):** `EmpresasService.GetLicenciaAsync` y
  `DashboardService` ahora usan `CertificationCampaignAccess.CountCommercialDocumentsAsync` (excluye
  certificación). Nuevo `DashboardEmpresaDto.DteMesComercial`; `PorcentajeUsoDte` y el texto de cupo en
  `Home/Index.cshtml` usan el consumo comercial; `DteMes` se conserva como actividad total. Test nuevo
  `tests/.../Dashboard/ConsumoComercialTests.cs`. Suite: **2355 unitarias verdes**. Requiere re-stage+reinicio
  (coordinar con el release, junto con los fixes de eventos) para verse en el contador visual.
- **Fixes de eventos aplicados y validados (código, aún sin desplegar ni transmitir):**
  `DteDocumentosService.Eventos.cs` — Invalidación ahora `version=3` fija (ya no depende del flag
  global; el corte 2026-08-25 rige, igual que contingencia v4); `documento.fecEmi` = emisión ORIGINAL
  del DTE invalidado (antes usaba la fecha del evento); `identificacion.fecEmi/horEmi` = evento (correcto);
  guard nuevo: si `codEstableMH`/`codPuntoVentaMH` no son 4 chars → error claro (v3 los exige no-null).
  Contingencia — `tipoEstablecimiento` resuelto a código MH (CAT-009) vía MapCodigoMhAsync (antes crudo).
  **Validación objetiva con ajv contra esquemas oficiales locales:** invalidacion-schema-v3 → payload VÁLIDO;
  contingencia-schema-v4 → compatible. Suite 2355 verde. Revisión SQL independiente (subagente) = cierre ÍNTEGRO.
  (El subagente de revisión backend cayó por límite de sesión; la revisión de payloads se hizo con ajv.)
- **EVENTOS DE DANIEL COMPLETADOS (2026-09-06):** 5 invalidación (tipo 2) + 5 contingencia (tipos 1-5),
  todos **PROCESADO con sello** en apitest (verificado en `Dte_Eventos`: INVALIDACION PROCESADO=5,
  CONTINGENCIA PROCESADO=5; hay 2 CONTINGENCIA RECHAZADO archivados de los intentos previos). Se ejecutó
  con un comando NUEVO del arnés: `dotnet tools/CertHarness/bin/Debug/net10.0/CertHarness.dll cert-cliente 23 [inv|cont] [ids]`
  (Program.cs; parametrizado por empresa, no toca el EmpresaId=2 de NEO; stub ICurrentUser para el DI de la
  rama Auth). Bugs de eventos hallados AL EJECUTAR y corregidos en `DteDocumentosService.Eventos.cs`:
  (1) los eventos mandaban `emisor.nit` CON guiones → `[emisor.nit] NO CORRESPONDE A USUARIO`; fix:
  `ClienteValidator.StripToDigits(empresa.Nit)` en TODOS los eventos. (2) la contingencia NO puede
  referenciar un CCF ya PROCESADO (MH: "codigo generacion ya existe"); el flujo correcto es emitir el CCF
  en modo contingencia (TipoTransmision=2, generar+firmar SIN enviar) y luego el evento — implementado en
  `EmitirCcfContingenciaAsync`. Invalidación tipo 2 va sobre CCF PROCESADO (#1295-1299).
- **NOTA de consumo:** los ~10 CCF auxiliares se emitieron SIN campaña de certificación (enfoque simple tipo
  NEO), así que SÍ consumen cuota comercial de 23 este mes (~10/100, resetea mensual). Si se requiere que
  no cuenten, habría que emitirlos dentro de una campaña auxiliar (pendiente, opcional).
- **PENDIENTE real:** deploy de los fixes (consumo + eventos) a los servicios staged del cliente (re-stage +
  reinicio); contraste del portal MH + autorización + certificado productivo para acreditar PRODUCCIÓN.

## Prompt listo para copiar

```text
Continúa en C:\Neo\NeoSTPBusinnesSuite\NeoStpCloud.
Lee primero .agents/skills/neostp-context/SKILL.md y
docs/auditoria-2026-09-03/CONTINUAR-AQUI-2026-09-05.md.
Después consulta las evidencias y el plan enlazados dentro del documento.

Mi prioridad es terminar las pruebas de DANIEL y dejarlo preparado para producción.
Quiero un lote automático rápido desde código, sin avanzar manualmente en grupos de diez.
Corrige también el consumo de facturas mostrado: las pruebas de certificación no deben
consumir las 100 facturas comerciales de su plan. Conserva documentos, sellos y auditoría.

Ya hay cuatro pilotos aceptados y 46 de 275 documentos adicionales aceptados.
Quedan 229 del lote existente. No crees otra campaña para repetirlos ni vuelvas a emitir
documentos aceptados. Comprueba primero el estado real y si queda algún ejecutor activo.
Automatiza la continuación con las mismas reservas, casos y límites, deteniendo nuevos
envíos ante rechazo o incertidumbre. Si mejoras el ejecutor, conserva la trazabilidad
del plan y revisa explícitamente la transición de binarios; no desactives sus controles.

DANIEL sólo debe ver y emitir los tipos 01, 03, 11 y 14. Eso ya está desplegado.
El consumo comercial real es cero; el contador visual todavía cuenta pruebas por error.
Corrige esa diferencia sin borrar documentos ni ocultar la actividad histórica.

Completa también las cinco pruebas de contingencia y cinco de invalidación necesarias.
Autorizo a Carlos Garcia como responsable y solicitante. Su documento ya está guardado
cifrado en out/client-certification-events/identities.dpapi; no lo copies al chat,
al código ni a los informes. No invalides los pilotos ni los documentos del lote275:
prepara cinco documentos auxiliares de PRUEBAS y conserva los objetivos originales.

Usa subagentes para revisión de backend, integridad SQL y pruebas independientes.
Avanza con lo ya autorizado, actualiza el plan y deja evidencias verificables.
No des por cerrada producción sólo por obtener sellos: contrasta el portal, los eventos,
la autorización y las credenciales productivas. No cambies NEO2 usando datos de DANIEL23.
```

## 1. Estado confirmado al detener el turno

- Empresa cliente: **23**, DANIEL; NIT fiscal normalizado **06232705261148**; **PRUEBAS**.
- Empresa propia: **NEO2**, independiente. Su configuración fiscal no fue cambiada.
- Esquema activo: **91 migraciones**, SQL Server16 local, base `NeoSTP_Cloud`.
- No había procesos de envío del lote activos en la comprobación de las 23:52 UTC. Revalidarlo antes de arrancar otro.
- Último resultado del lote: `tmp/client-certification-batch/20260905T234722Z-a701ccce7b9a403abbd9b08108e93195.json`.
- Campaña preparada, **46 aceptados / 46 reservados / 0 bloqueos**. Siguiente caso: `batch275-v1-01-047`.
- Último documento aceptado: **1065**. No hay un intento incierto que deba reenviarse según ese corte.
- Lectura independiente SQL 23:51 UTC: **50 documentos nuevos de septiembre = 4 pilotos + 46 del lote**, todos con consumo de certificación coherente; **consumo comercial = 0** y ningún STOP.

Los siguientes son totales de documentos aceptados en la base local, incluyendo la Factura1015 anterior al trabajo; no son una lectura reciente del portal:

| Tipo | Objetivo de la captura | Aceptados locales | Restantes |
|---|---:|---:|---:|
| 01 Factura | 90 | 48 | 42 |
| 03 Crédito Fiscal | 75 | 1 | 74 |
| 11 Exportación | 90 | 1 | 89 |
| 14 Sujeto Excluido | 25 | 1 | 24 |
| Total | 280 | 51 | **229** |

La captura original mostraba un aprobado y 279 pendientes. Ya se aceptaron 50 documentos nuevos; no reiniciar el conteo desde279.

## 2. Lote existente: continuar, no duplicar

- Campaña piloto: `5f681bfb-149d-4650-9a1b-5723feeebef3`, cuatro cupos, uno por tipo; agotada correctamente.
- Campaña adicional: **`7bc8a3fb-08d6-4c8d-974a-01cde7fd702a`**.
- Referencia: `CLIENT23-BATCH275-V1`.
- Presupuestos originales de la campaña adicional: **88/74/89/24**, total275. Ya se consumieron46 del primer tipo.
- PlanHash: **`FD21C5AC3D9BC27A03693299DE54D083D25CCFCA68C646D069C77680D14C160E`**.
- SHA256 del ejecutor congelado `ClientCertificationBatch.dll`: **`6F87264293D852FDCE6EAEE69E192F86CF298D2786A9BCB86F0564BA28A65809`**.

Herramientas existentes:

- `tools/ClientCertificationBatch/README.md`: contrato del lote.
- `tools/ClientCertificationBatch/Program.cs`: preview, preparación y un caso por ejecución.
- `tools/ClientCertificationBatch/BatchCampaign.cs`: reserva, intento previo al POST, cierre y STOP durable.
- `tools/ClientCertificationBatch/Run-BatchWindow.ps1`: envoltorio actual limitado a10; **el usuario pidió reemplazar esta supervisión por una continuación automática más rápida**.
- `tools/CompanyPreflight/Get-ClientCertificationBatchClosure.ps1`: auditoría exclusivamente SELECT para el cierre.

Comando disponible de inventario, sin firmas ni transmisiones:

```powershell
dotnet tools/ClientCertificationBatch/bin/Release/net10.0/ClientCertificationBatch.dll --preview
```

Cada recepción ya se hacía desde código; no se estaban llenando facturas a mano. La lentitud viene de arrancar/revalidar por documento y de la supervisión en ventanas pequeñas. **La mejora de velocidad todavía NO está implementada.** Evitar concurrencia sobre el ejecutor actual: encontrar un intento previo IN_PROGRESS genera STOP. Tampoco enviar documentos normales a la ruta de lotes de contingencia sin comprobar su contrato.

El plan fija binarios, dependencias, esquemas, catálogos, datos fiscales y pilotos. Recompilar o desplegar mientras continúa el lote puede cambiar esas huellas y bloquearlo. Opciones a revisar: orquestador persistente que reutilice los componentes congelados, o transición auditada del ejecutor manteniendo casos, consumos y resultados. No eludir comparaciones de hashes ni ampliar presupuestos para sortear un bloqueo.

No hay Worker del lote, correo ni webhooks externos. Un rechazo o respuesta incierta detiene el proceso; no existe reintento automático. El nuevo orquestador pasó32 comprobaciones y275 validaciones de esquema, más revisión independiente y ejecución real secuencial. No tiene un ensayo SQL específico de carrera/rollback: no atribuírselo.

## 3. Consumo: defecto de presentación localizado

**No hay una tabla de saldo que se deba poner a cero.** El guard de licencia usa `CertificationCampaignAccess.CountCommercialDocumentsAsync` y da0. Los consumos del piloto/lote están correctamente excluidos de la cuota comercial.

Hallazgo pendiente de corregir:

- `src/NeoSTP.Infrastructure/Services/EmpresasService.cs`, `GetLicenciaAsync`: usa COUNT de documentos por `CreatedAt`, sin excluir certificación.
- `src/NeoSTP.Infrastructure/Services/DashboardService.cs`: cuenta actividad por `FechaEmision`; Home reutiliza `DteMes` para la barra de cuota.
- Corrección propuesta: misma función de consumo comercial en licencia y un campo separado de consumo comercial para la barra/porcentaje. Mantener `DteMes` como actividad, con su significado explícito.

No borrar DTE, sellos, errores, correlativos, campañas ni claims. No cambiar el plan global ni añadir cuotas comerciales. Añadir pruebas que comparen actividad total con consumo comercial, incluida certificación coherente y marcas CERT falsas. Coordinar el despliegue con el lote congelado.

## 4. Pilotos y versión activa

| Documento | Tipo | Versión | Resultado |
|---|---|---:|---|
| 1016 | 01 | 2 | PROCESADO con sello |
| 1017 | 03 | 4 | PROCESADO con sello |
| 1018 | 11 | 3 | PROCESADO con sello |
| 1019 | 14 | 2 | PROCESADO con sello |

1016 recibió inicialmente096 por versión incoherente JSON/sobre; se corrigió el producto y se recuperó el mismo documento. 1017 recibió009 por NIT sintético inexistente; se recuperó usando NEO como receptor de PRUEBAS, sin modificar NEO. Los dos rechazos originales permanecen archivados. No repetir sus modos de recuperación.

Correcciones ya desplegadas:

- Sólo01/03/11/14 autorizados en API/Web para23; creación directa05 devuelve403.
- Dirección confirmada: La Libertad/Centro/San Juan Opico, códigos05/24/15.
- Versión persistida tomada del JSON; sobre y JWS deben coincidir antes de HTTP.
- Política `Dte:TenantSchemas:23`: NIT del cliente, ambiente PRUEBAS, perfil `MH_20260825`. Globalfalse y otras empresas preservados.
- Referencia global Ayutuxtepeque06/23/03 añadida desde catálogo oficial para el receptorNEO.

Release activo: **`20260905T232433Z-9062dc25354442429d2ef07f76f24d94`**.

- Raíz privada: `out/client-certification-release/<release>/{api,web}`.
- SHA manifiesto: `98722A2B38A922BBC389FDF7274136F0A729F4EB0382867B03911FC9E75C92D8`.
- SHA Infrastructure: `8F5E0D1DAA3837775E161630ED4C519367430192BE79F6BC05C6FBB7F4786C95`.
- Wrapper: `tools/ClientCertificationDeployment/Start-StagedHost.ps1`.
- Tareas Windows `NeoSTP API` / `NeoSTP Web`; API5058 / Web5031. Revalidar PID; no usar identificadores de procesos históricos para detenerlos.
- Dominios: https://app.neostp.com y https://api.neostp.com.
- Suite del producto: **2,354 unitarias + 9 integraciones**, cero errores/advertencias. Focales incluidas, no sumar cortes anteriores.
- Ambiente del host todavía **Development temporal**, cinco flags de migración/semillas/bootstrap apagados; esto no acredita cierre Production general.

Evidencia formal: `docs/auditoria-2026-09-03/evidencia/client-four-types-2026-09-05/`, con22 archivos hasheados. Informes completos: `CERT2-Cliente-Cuatro-Tipos-2026-09-05.md` y `Plan-Continuidad-Auditado-2026-09-04.md`.

## 5. Eventos: autorización recibida, ejecución pendiente

El usuario autorizó **Carlos Garcia** como responsable y solicitante y proporcionó su DUI. Se guardó posteriormente para la continuidad en:

`out/client-certification-events/identities.dpapi`

- Cifrado Windows DPAPI `CurrentUser`, ACL protegida: usuario actual, SYSTEM y Administradores.
- SHA256 del archivo cifrado: `476889490DC95A5CCEE763558F30F73B7316EC8B3AE02670A2A83AB734D8DA31`.
- Entropía UTF8: `NeoSTP:Client23:CertificationEvents:2026-09-05:v1`.
- JSON cifrado: Version1, EmpresaId23, AmbientePRUEBAS, objetos Responsable/Solicitante con Nombre/TipoDocumento/NumeroDocumento.
- Descifrar únicamente en memoria bajo la misma identidad Windows; no imprimir ni guardar en claro. El roundtrip del archivo se verificó. No confundir esta identidad con credenciales de Hacienda o con un certificado.

Propuesta de recorrido: **campaña auxiliar nueva de cinco CF de PRUEBAS**, uno por evento de contingencia; confirmar evento, enviar/consultar lote y obtener sello individual; después invalidación tipo2 sin reemplazo. Mantener intactos los cuatro pilotos y los275 documentos objetivo. No hay campaña auxiliar ni eventos ejecutados todavía.

Hallazgos del código actual que deben corregirse o cubrirse con un ejecutor durable revisado antes de esos envíos:

- `DteDocumentosService.Eventos.cs` persiste eventos best-effort después del POST y genera UUID nuevo en cada llamada: no usarlo en un bucle sin reserva/intento durable anterior a la red.
- Invalidación usa el flag global: false genera v2; esquema oficial local disponiblev3. La política por tenant añadida para documentos no modifica eventos.
- `documento.fecEmi` en invalidación usa fecha del evento; debe ser la emisión original del documento.
- Contingencia v4 toma `tipoEstablecimiento` de configuración raw; requiere código MH normalizado y establecimiento/punto coherentes.
- Confirmar estado terminal y sello antes de dar por aceptado un evento o cambiar un documento a INVALIDADO.

Corrección de nomenclatura detectada: **CAT-022:36=NIT,13=DUI,37=Otro,03=Pasaporte,02=Carnet de Residente**. Los pilotos11/14 con37 usan documento Otro; algunas etiquetas históricas del runner dicen pasaporte por error. No cambiar ahora los documentos aceptados ni sus huellas. CAT-024:1=error de información,2=rescindir,3=otro. CAT-005 tiene etiquetas UI antiguas para2–4; contrastar el Excel oficial antes de elegir motivos.

Fuente oficial consultada: [Pasos para ser emisor de DTE](https://factura.gob.sv/2020/09/02/ent1/), especialmente pasos3–5. Contempla invalidación y contingencia para Sistema de Transmisión, seguimiento en portal, solicitud de autorización y certificado productivo. La captura marca0/5 en ambos eventos. No afirmar producción lista por completar sólo los cuatro tipos.

## 6. Comercial y otros pendientes de producción

- SMTP Hostinger configurado; el usuario confirmó recepción del correo de prueba. No reenviar para repetir la comprobación.
- StarterFE, plan207, USD15/mes; implementación pagada según usuario. Septiembre vence30/09 y NO está pagado.
- CLI23 aún pendiente: licencia29 ACTIVO hasta20/09; suscripción2/customer2 Mock/TRIALING, sin factura/pago de septiembre. No se aplicó un reset comercial.
- `CLI23-Preparacion-Comercial-2026-09-05.md` y `tools/ClientCommercial/Preview-Client23Commercial.ps1` contienen el diagnóstico/propuesta. El esquema permite facturaOPEN, pero falta acuerdo mensual durable/idempotencia por período y separar vencimiento de acceso; el flujo actual usa `AddMonths(1)` desde pago.
- MFA corresponde al titular; datos bancarios y cuenta de cobro pendientes.
- Windows definitivo confirmado; HTTPS308/HSTS86400 activos. Servicios sin sesión, recuperación fuera del equipo, identidad/SQL mínimo privilegio y proveedores reales siguen abiertos.
- Wompi seleccionado, sin cuenta/aplicativo todavía. N1co sólo fue solicitado para evaluación: no incorporar.
- NEO productivo: usuario dispone de credenciales/certificado según conversación; no afirmar que ya se introdujeron ni usar su certificado para DANIEL.
- Conservar backup y copia SQL ensayada; no limpiar ahora. Base activa migrada79→91 con preservación de103 tablas originales comprobada.

## 7. Coordinación y cierre

Subagentes existentes en esta tarea: `pruebas_backend`, `revisor_backend`, `sql_certificacion`. Sus últimas entregas fueron revisión del lote, preparación comercial, hallazgos de eventos y auditoría SQL. Si se continúa en una tarea nueva, crear subagentes sólo para subtareas concretas que puedan ejecutarse junto a trabajo útil del coordinador.

No hay commit/push de este trabajo. Rama `codex/gl0a-auth-security`; árbol compartido con más de300 cambios previos. Preservarlos. Leer las skills/contexto antes de editar y no reiniciar auditorías ya documentadas.

Al completar el lote, ejecutar:

```powershell
./tools/CompanyPreflight/Get-ClientCertificationBatchClosure.ps1
```

Su salida intermedia `Passed=false` puede deberse a cantidades pendientes: leer todos los checks. El cierre exige275 casos/claims/intentos aceptados, pilotos e histórico intactos, cuotas coherentes y totales locales90/75/90/25. Luego cerrar por separado los eventos y contrastar el portal/autorización productiva. Actualizar este MD y el plan con hechos, no con ejecuciones propuestas.
