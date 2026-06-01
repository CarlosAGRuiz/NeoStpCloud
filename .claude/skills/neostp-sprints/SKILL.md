---
name: neostp-sprints
description: Backlog operativo y guía de implementación por sprints (13–30) para NeoSTP Cloud. Use this skill cuando el usuario pida trabajar, planear, consultar o avanzar uno de los sprints 13 al 30 de NeoSTP Cloud / NeoSTP Business Suite. Triggers incluyen frases como "trabajemos sprint 14", "qué falta del sprint 13", "siguiente sprint", "implementa sprint X", "muéstrame el sprint X", "plan del sprint X", "criterios de aceptación del sprint X", o "/neostp-sprints <n>".
---

# NeoSTP — Backlog operativo por sprints (13–30)

El backlog completo está en [BACKLOG.md](BACKLOG.md). **Léelo siempre antes de proponer trabajo de un sprint**, especialmente la sección del sprint solicitado y la sección "Definición de completado global".

## Contexto base (capturado 2026-05-31)

- Proyecto: NeoSTP Cloud Web (rama `main`)
- Último sprint cerrado: **Sprint 12** (build verde, 106/106 tests).
- Stack: .NET 10, ASP.NET Core MVC/Razor, Web API, SQL Server 2022, EF Core 10, Worker, Serilog, QuestPDF, MailKit, Polly, JWT/Cookies, DataProtection.
- Lo que ya existe está enumerado en `BACKLOG.md → Estado base`.

## Cómo decidir qué hacer

1. Identifica el **número de sprint** que el usuario menciona o pide. Si no menciona, asume el siguiente del orden recomendado (`BACKLOG.md → Orden recomendado de ejecución`).
2. **Lee la sección de ese sprint en `BACKLOG.md`** antes de cualquier otra cosa. No improvises el alcance.
3. Antes de implementar, **verifica el estado real** del repo para esa área:
   - ¿Ya existen las entidades / tablas / endpoints / vistas listados?
   - ¿Las migraciones EF involucradas ya están aplicadas?
   - ¿Hay deuda parcial de sprints previos?
   Usa Grep/Glob/Read sobre `src/`, `tests/` y `Persistence/Migrations` para confirmar.
4. Si el sprint depende de algo del backlog que aún no está, **avísalo y propone un orden** antes de tocar código.
5. Si el usuario pide solo un *resumen* o el *plan* del sprint, no toques código — responde con objetivo, alcance, tareas, criterios.

## Regla operativa (impuesta por el usuario)

> "No entregar todos los sprints en un solo prompt de implementación."

Cuando el usuario te diga "implementa Sprint X", parte el trabajo en sub-entregas pequeñas siguiendo el formato del backlog:
1. Contexto corto.
2. Sprint exacto.
3. Objetivo.
4. Alcance de la sub-entrega (no del sprint entero).
5. Archivos esperados.
6. Restricciones.
7. Criterios de aceptación de esta sub-entrega.

Confirma con el usuario antes de avanzar a la siguiente sub-entrega de un mismo sprint.

## Definición de completado (global a todo sprint)

Antes de marcar cualquier sprint como completo verifica:
- `dotnet build NeoSTP.slnx` verde.
- `dotnet test NeoSTP.slnx` verde, sin regresiones.
- Nuevos tests agregados pasan.
- Migración EF aplicada si correspondía.
- **No se agregan secretos al repo.**
- No se rompe multiempresa — todo dato nuevo respeta `EmpresaId`.
- Se respeta licenciamiento por módulo (`Core_EmpresaModulos`).
- Acciones críticas quedan auditadas.
- README / `CONTEXTO-PROYECTO.md` se actualizan si cambió el estado del sistema.

Para correr build/tests/migraciones usa la skill hermana `neostp` (ver `.claude/skills/neostp/SKILL.md`) en lugar de inventar comandos.

## Mapa rápido de sprints

| Sprint | Tema | Categoría |
|---|---|---|
| 13 | Catálogos MH y mantenimiento | Crítico técnico/fiscal |
| 14 | Certificación DTE: matriz y progreso | Crítico técnico/fiscal |
| 15 | Eventos DTE persistentes + UI + PDF | Crítico técnico/fiscal |
| 16 | Contingencia avanzada y lotes | Crítico técnico/fiscal |
| 17 | Diagnóstico de errores Hacienda | Crítico técnico/fiscal |
| 18 | Legal + consentimiento | Crítico comercial |
| 19 | Billing self-service (Stripe + MercadoPago) | Crítico comercial |
| 20 | Hardening pre-producción | Crítico comercial |
| 21 | UI/UX AppShell + design system | Crítico comercial |
| 22 | NeoProfit básico | Diferenciador |
| 23 | NeoScanAI integrado | Diferenciador |
| 24 | NeoConnect API comercial | Diferenciador |
| 25 | NeoPOS básico | Operación avanzada |
| 26 | NeoPortal Clientes | Operación avanzada |
| 27 | NeoSTP Mobile API readiness | Operación avanzada |
| 28 | NeoSTP Mobile MVP | Operación avanzada |
| 29 | SuperAdmin operativo avanzado | Operación avanzada |
| 30 | Preparación comercial y documentación | Operación avanzada |

## Subcomandos sugeridos

Si el usuario escribe `/neostp-sprints <n>`:
- Sin argumento → muestra el mapa rápido y pregunta cuál sprint.
- `<n>` (13–30) → abre [BACKLOG.md](BACKLOG.md), localiza la sección "Sprint `<n>`" y responde con objetivo + alcance + tareas + criterios + dependencias detectadas.
- `<n> verify` → solo verifica estado real del repo contra los criterios de aceptación de ese sprint; **no edita código**.
- `<n> next` → propone la próxima sub-entrega del sprint en formato de prompt de implementación, sin ejecutarla.
- `next` (sin número) → busca el primer sprint no terminado en el orden recomendado y aplica el flujo `<n> next`.

## Recordatorios

- **No empezar a programar sin confirmación explícita del usuario** salvo que el usuario lo haya pedido en el mismo turno.
- Si detectas algo del backlog que ya está parcialmente hecho, dilo claramente antes de duplicar trabajo.
- Si surge un cambio de alcance respecto al backlog, actualiza `BACKLOG.md` y déjale constancia al usuario.
- Si una sub-entrega cambia el estado del sistema (entidades nuevas, módulos nuevos), actualiza también `CONTEXTO-PROYECTO.md` al cerrar el sprint.
