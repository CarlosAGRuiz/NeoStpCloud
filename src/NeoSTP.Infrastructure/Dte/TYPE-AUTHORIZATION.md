# Tipos DTE por empresa

`Dte_Configuracion.TiposDteAutorizadosCsv` contiene una restricción fiscal local, independiente del plan, la licencia y los permisos de usuario. El código no consulta ni certifica autorizaciones de Hacienda.

- `null`: compatibilidad heredada con los tipos implementados; no acredita autorización MH.
- Lista explícita canónica, por ejemplo `01,03,11,14`: sólo esos tipos están habilitados.
- Vacío, duplicados, espacios, tipos desconocidos o formato inválido: ningún tipo habilitado.
- Configuración DTE inexistente: no se ofrecen tipos para emitir; los controles fiscales existentes también requieren dicha configuración.

El campo es de lectura en el DTO de configuración. No forma parte de `SaveDteConfiguracionRequest`, ni del modelo de creación del DTE, ni de la huella de idempotencia. Sólo debe provisionarse mediante una operación restringida de soporte, con comprobación de empresa/NIT, evidencia MH y auditoría; esta entrega no ejecuta esa operación ni modifica filas existentes.

`DteTypeAuthorization` se consulta sin caché antes de crear un documento nuevo y antes de avanzar generación, validación, firma o envío. Los servicios Web/API/Connect/POS/retransmisión comparten estos controles; el servicio de lotes comprueba los tipos antes de autenticar o reclamar envíos. Una repetición de creación devuelve su documento histórico sin generar otro; no permite retransmitir un tipo revocado.

`GET /api/dte/tipos`, los formularios Web, el catálogo operativo `TIPO_FACTURA` y la matriz de certificación reflejan la restricción. El lookup fiscal evita caché para observar revocaciones. El catálogo global no se modifica. Las consultas históricas conservan acceso con el ámbito y permisos existentes; el filtro Web añade los tipos históricos presentes en la empresa, etiquetados como históricos.

Se requiere desplegar el esquema y el código juntos antes de provisionar la restricción. No hay activación de campaña, ampliación de cuota, envío a Hacienda ni cambio global de catálogo en esta implementación.
