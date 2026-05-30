# NeoSTP Cloud — Diseño (fuente de verdad)

Assets de diseño de **NeoSTP Business Suite** (exportados de Stitch). Esta carpeta
es la referencia visual versionada para la modernización de la UI.

## Estructura

```
design/
  design-system/DESIGN.md      ← Sistema de diseño canónico (tokens, tipografía, componentes)
  mockups/
    dashboard-dte/             ← Centro de Control DTE (dashboard operativo)
    certificacion-dte/         ← Matriz + progreso de pruebas Hacienda (módulo Certificación DTE)
    nuevo-dte/                 ← Stepper Borrador→Validado→Firmado→Enviado→Procesado
    plan-licencia/             ← Gestión de plan y licencia SaaS
    neopos/                    ← Terminal de ventas NeoPOS
    neoprofit/                 ← Dashboard financiero NeoProfit/NeoBI
    neoscanai/                 ← Bandeja de revisión NeoScanAI
    superadmin/                ← Monitoreo global SuperAdmin
```

Cada mockup incluye `code.html` (Tailwind CSS) y `screen.png` (render de referencia).

## Sistema de diseño (resumen)

- **Marca:** Trustworthy · Sophisticated · Modular. Estilo Corporate/Modern con tarjetas.
- **Color base:** Deep Tech Blue (`#0F172A`) para navegación/acciones; **Modern Violet**
  (`#6b38d4` / `#8B5CF6`) como acento de IA (NeoScanAI) e interacción.
- **Semántica DTE:** `status-processed #10B981`, `status-rejected #EF4444`,
  `status-draft #64748B`, `status-contingency #F59E0B`.
- **Tipografía:** Hanken Grotesk (headlines) · Inter (UI/body) · JetBrains Mono (datos: UUID, montos).
- **Layout:** sidebar 260px + contenido fluido, grid 12 col, baseline 4px, radios 8/12px.

Ver `design-system/DESIGN.md` para los tokens completos.

## Plan de incorporación (decisiones acordadas)

1. **Secuencia:** primero certificación DTE backend (bloqueante), luego el design system.
2. **CSS:** Tailwind para lo nuevo + AppShell, en **coexistencia gradual** con Bootstrap
   (retiro página por página). Build con Tailwind CLI → `wwwroot/css/app.css` (no CDN en prod).
3. **Componentes** como ViewComponents/TagHelpers de Razor: AppShell, Sidebar, Navbar
   (con Environment Indicator), MetricCard, StatusBadge, DataTable, StepperDte,
   CertificationProgressBar, LicenseUsageCard, AiConfidenceBadge, IntegrationStatusCard.
4. **Primeras pantallas:** AppShell global → Dashboard DTE → Stepper Nuevo DTE →
   **Certificación DTE** (tablero de las 625 pruebas) → resto de módulos.
