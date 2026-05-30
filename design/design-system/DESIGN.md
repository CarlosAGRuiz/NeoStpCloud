---
name: NeoSTP Design System
colors:
  surface: '#f7f9fb'
  surface-dim: '#d8dadc'
  surface-bright: '#f7f9fb'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f2f4f6'
  surface-container: '#eceef0'
  surface-container-high: '#e6e8ea'
  surface-container-highest: '#e0e3e5'
  on-surface: '#191c1e'
  on-surface-variant: '#45464d'
  inverse-surface: '#2d3133'
  inverse-on-surface: '#eff1f3'
  outline: '#76777d'
  outline-variant: '#c6c6cd'
  surface-tint: '#565e74'
  primary: '#000000'
  on-primary: '#ffffff'
  primary-container: '#131b2e'
  on-primary-container: '#7c839b'
  inverse-primary: '#bec6e0'
  secondary: '#6b38d4'
  on-secondary: '#ffffff'
  secondary-container: '#8455ef'
  on-secondary-container: '#fffbff'
  tertiary: '#000000'
  on-tertiary: '#ffffff'
  tertiary-container: '#1b0063'
  on-tertiary-container: '#856cff'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dae2fd'
  primary-fixed-dim: '#bec6e0'
  on-primary-fixed: '#131b2e'
  on-primary-fixed-variant: '#3f465c'
  secondary-fixed: '#e9ddff'
  secondary-fixed-dim: '#d0bcff'
  on-secondary-fixed: '#23005c'
  on-secondary-fixed-variant: '#5516be'
  tertiary-fixed: '#e6deff'
  tertiary-fixed-dim: '#c9beff'
  on-tertiary-fixed: '#1b0063'
  on-tertiary-fixed-variant: '#4618ca'
  background: '#f7f9fb'
  on-background: '#191c1e'
  surface-variant: '#e0e3e5'
  status-processed: '#10B981'
  status-rejected: '#EF4444'
  status-draft: '#64748B'
  status-contingency: '#F59E0B'
  surface-dark: '#1E293B'
  border-subtle: '#E2E8F0'
typography:
  display-lg:
    fontFamily: Hanken Grotesk
    fontSize: 48px
    fontWeight: '700'
    lineHeight: 56px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Hanken Grotesk
    fontSize: 32px
    fontWeight: '600'
    lineHeight: 40px
  headline-lg-mobile:
    fontFamily: Hanken Grotesk
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  headline-md:
    fontFamily: Hanken Grotesk
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  title-md:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 24px
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  label-md:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
    letterSpacing: 0.01em
  data-mono:
    fontFamily: JetBrains Mono
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  base: 4px
  container-margin: 24px
  gutter: 16px
  card-padding: 20px
  sidebar-width: 260px
  header-height: 64px
---

## Brand & Style

The design system is engineered for a high-performance LATAM enterprise environment, balancing operational reliability with forward-thinking automation. The brand personality is **Trustworthy, Sophisticated, and Modular**. 

The chosen style is **Corporate / Modern** with a **Tactile** influence. It utilizes a card-based architecture that organizes complex ERP and DTE (Electronic Invoicing) data into digestible, interactive modules. The interface focuses on high functional density without sacrificing the breathability required for long-form operational tasks. Visual depth is achieved through subtle tonal layering and soft shadows, creating a workspace that feels organized, secure, and physically structured.

## Colors

The palette is anchored by **Deep Tech Blue**, providing a stable, authoritative foundation for navigation and primary global actions. **Modern Violet** serves as the primary accent, specifically reserved for high-value AI interactions (NeoScanAI) and interactive highlights to draw the eye to intelligent insights.

For DTE workflows, a specialized semantic palette is utilized to provide immediate visual feedback on document states. The default background uses a very light slate to reduce eye strain, while the dark mode shifts to a deep slate palette (`#0F172A` and `#1E293B`) to maintain professional contrast and data legibility in low-light environments.

## Typography

Typography focuses on **data readability and hierarchy**. 
- **Hanken Grotesk** is used for headlines to provide a sharp, contemporary edge to the enterprise aesthetic.
- **Inter** handles all body copy and UI labels, chosen for its exceptional legibility in dense dashboard environments.
- **JetBrains Mono** is introduced for technical data strings, such as DTE UUIDs, invoice numbers, and currency values, ensuring each character is distinct and easy to audit.

Scale is strictly controlled to maintain density. Avoid using font weights below 400 for accessibility.

## Layout & Spacing

The design system employs a **Fixed Grid** model for the main content area with a responsive 12-column system. 
- **Desktop:** 260px fixed sidebar + fluid content area with 24px margins.
- **Tablet:** Sidebar collapses to an 80px icon-only rail; content margins reduce to 16px.
- **Mobile:** Sidebar becomes an off-canvas drawer; 12px horizontal margins for cards.

The rhythm is based on a **4px baseline grid**. Spacing between cards should be a consistent 16px (gutter) to maintain the "modular" feel. Large dashboards should use vertical stacks of 24px between logical sections (Operation vs Intelligence).

## Elevation & Depth

Hierarchy is established through **Tonal Layers** and **Ambient Shadows**. 
1. **Level 0 (Background):** The application background (`#F8FAFC`) acts as the canvas.
2. **Level 1 (Cards/Modules):** Pure white surfaces (`#FFFFFF`) with a subtle 1px border (`#E2E8F0`) and a soft, diffused shadow (Offset: 0, 2px; Blur: 4px; Opacity: 4%).
3. **Level 2 (Dropdowns/Modals):** Increased shadow depth (Offset: 0, 8px; Blur: 16px; Opacity: 8%) to indicate temporary interaction layers.

In Dark Mode, elevation is communicated by lightening the slate surface color rather than increasing shadow opacity, ensuring depth remains visible.

## Shapes

The shape language is **Refined and Rounded**. 
A standard radius of **8px (0.5rem)** is applied to small components like input fields and buttons. Larger modular containers and cards utilize a **12px (0.75rem)** radius to soften the high-density layout and create a more approachable, modern SaaS feel. Status badges and tags use a fully rounded (pill) style to distinguish them from interactive buttons.

## Components

### Buttons & Inputs
- **Primary Action:** Solid Deep Tech Blue (`#0F172A`) with white text. 8px radius.
- **AI Action:** Gradient or solid Modern Violet (`#8B5CF6`) for NeoScanAI features.
- **Inputs:** 1px border (`#E2E8F0`), Inter 14px text. Focus state uses a 2px violet ring with 0% offset.

### Navigation & AppShell
- **Sidebar:** Dark background (`#0F172A`). Active states use a left-edge violet indicator and a subtle background tint.
- **Top Navbar:** Semi-transparent blur or solid white. Includes the "Environment Indicator" (pill-shaped badge: Green for Prod, Yellow for Test).

### Data & Metrics
- **Metric Cards:** Large Hanken Grotesk numbers. Include a small sparkline (2px stroke) in the bottom third of the card.
- **Data Tables:** Row height of 48px for standard density. Use Zebra striping on hover only. Numeric columns must use JetBrains Mono.

### Workflow & Status
- **Steppers:** Horizontal connectors between steps. Completed steps use Modern Violet icons; current steps use a Deep Tech Blue border.
- **Status Badges:** Text in all-caps, 10px size, semi-bold. Backgrounds are low-opacity versions of the semantic colors with high-contrast text.