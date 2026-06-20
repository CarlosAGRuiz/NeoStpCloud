# Evidencia de Demo / Release

> Copiar esta plantilla por corrida fuera del repositorio cuando incluya URLs privadas, nombres de
> clientes o datos operativos. Nunca incluir passwords, tokens, certificados, connection strings,
> API keys, secretos HMAC ni payloads fiscales completos.

## Identificacion

| Campo | Valor |
|---|---|
| Fecha/hora UTC | |
| Perfil | Demo / Release |
| Decision | APTO_DEMO / APTO_RELEASE / APTO_CON_ADVERTENCIAS / NO_APTO |
| Branch | |
| Commit | |
| Ambiente | Local / Demo / Staging / Produccion |
| Responsable | |

## Servicios

| Servicio | URL sanitizada | Health live | Health ready |
|---|---|---|---|
| API | | | |
| Web | | | |
| Worker | n/a | n/a | Activo / No requerido |

## Providers

| Provider | Modo | Resultado de prueba |
|---|---|---|
| Hacienda | Mock / Http | |
| Firma DTE | Mock / Pkcs12 / HaciendaCert | |
| Email | Mock / Smtp | |
| NeoScan | Mock / Gemini | |
| Scan storage | Database / FileSystem | |
| Push | Mock / Fcm | |
| WhatsApp | Mock / Meta | |
| Cache | Memory / Redis | |
| Billing | Mock / real declarado | |

## Validacion Tecnica

| Check | Resultado | Duracion / detalle |
|---|---|---|
| Preflight JSON adjunto | | |
| Build | | |
| Tests unitarios | | |
| Tests integracion | | |
| API health/live | | |
| API health/ready | | |
| Web health/live | | |
| Web health/ready | | |
| OpenAPI | | |
| Migraciones revisadas/aplicadas | | |
| Backup verificado | | |

## Usuarios y Datos

| Rol/perfil | Empresa | Casos usados | Resultado |
|---|---|---|---|
| ADMIN | | | |
| OPERADOR/POS | | | |
| CONTADOR | | | |
| Receptor publico | | | |
| API Key NeoConnect | | | |
| Mobile | | | |

No registrar password, token JWT, token de portal ni API key raw.

## Casos Ejecutados

| ID | Ruta/pantalla | Rol | HTTP/estado | Duracion ms | TraceId si fallo | Resultado |
|---|---|---|---:|---:|---|---|
| | | | | | | |

## Hallazgos

| Severidad | Ruta/flujo | Descripcion | Responsable | Decision |
|---|---|---|---|---|
| | | | | Corregir / Aceptar / Bloquear |

## Capturas

- Dashboard:
- DTE:
- POS/caja:
- Cobros/portal:
- NeoScan:
- Tesoreria/fiscal:

Sanitizar NIT, NRC, DUI, emails, telefonos, UUID, tokens y documentos reales antes de adjuntar.

## Cierre

- Decision final:
- Advertencias aceptadas:
- Bloqueos:
- Acciones post-demo ejecutadas:
- Evidencia almacenada en:
- Aprobado por:
