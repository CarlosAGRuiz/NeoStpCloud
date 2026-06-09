# Contratos API V2-C0

Fecha de corte: 2026-06-09.

Este documento congela las reglas minimas para ampliar la API sin romper mobile, Web, NeoConnect ni futuros modulos como NEOCRM y NeoPortal.

## Reglas obligatorias

- Todo endpoint interno protegido usa JWT Bearer y hereda respuesta `ApiResponse<T>`.
- Todo endpoint operativo resuelve `EmpresaId` desde `ICurrentUser.EmpresaId`; SuperAdmin debe enviar `empresaId`.
- Todo controller de modulo licenciable usa `[RequireModule("CODIGO")]`.
- Toda accion protegida usa `[RequirePermiso("Modulo.Accion")]`.
- Los errores de negocio salen con `Result`/`Result<T>` y codigo estable.
- Los secretos nunca se devuelven completos; solo estado, prefijo o metadatos.
- Los listados paginados usan `PagedQuery` y `PagedResult<T>`.
- Los DTOs de request llevan validaciones `DataAnnotations` cuando aplica.
- Las acciones de negocio relevantes registran auditoria.
- `src/NeoSTP.Api/README.md` debe actualizarse en el mismo cambio del controller.

## Superficies revisadas en C0

| Area | Estado |
|---|---|
| Cobros/CxC | Documentado, incluye recordatorios V2-D3. |
| Compras/CxP | Documentado como API-first. |
| Tesoreria | Documentado. |
| Inventario | Documentado. |
| POS/caja | Documentado. |
| RRHH | Documentado. |
| NeoConnect v1 | Documentado. |
| NEOCRM | Nuevo contrato API-first documentado y protegido por tests. |

## Checklist para nuevas APIs

1. Agregar DTOs en `NeoSTP.Application`.
2. Agregar interfaz de caso de uso en `NeoSTP.Application`.
3. Implementar servicio en `NeoSTP.Infrastructure`.
4. Exponer controller delgado en `NeoSTP.Api`.
5. Agregar permisos y modulo si aplica.
6. Agregar migracion EF si hay persistencia.
7. Agregar tests unitarios de servicio y metadata de controller.
8. Actualizar `src/NeoSTP.Api/README.md`.
9. Ejecutar build y tests relevantes.
