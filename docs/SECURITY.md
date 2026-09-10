# Seguridad de NeoSTP Cloud

Este documento define los controles públicos de seguridad de la plataforma. No contiene secretos, datos de clientes ni procedimientos particulares de una instalación.

## Identidad y autorización

- Web usa cookie segura; API usa JWT Bearer y NeoConnect usa API keys con hash, prefijo visible, scopes, expiración y revocación.
- RBAC, módulo contratado y empresa actual son controles independientes; todos deben cumplirse.
- Toda operación de negocio se filtra por `EmpresaId`. SuperAdmin opera soporte mediante una empresa seleccionada cuando accede a datos tenant.
- MFA TOTP, recovery codes, lockout y allowlist administrativa se mantienen como controles de cuenta privilegiada.

## Secretos y DataProtection

- Secretos locales viven en variables de entorno, un secret store o `appsettings.Local.json` ignorado por Git.
- Nunca se publican JWT, passwords, tokens, certificados, llaves privadas, cadenas de conexión ni payloads DTE sensibles completos.
- DataProtection debe usar key rings y certificados distintos para STAGING y PROD. Las llaves históricas necesarias para descifrar datos no se eliminan.
- La configuración `Production` debe deshabilitar bootstrap, demo, seed y migrations automáticas.

## Auditoría de repositorio — 2026-09-10

- `Check-PublicTree.ps1 -SelfTest`: aprobado sobre el conjunto publicado.
- GitHub Secret Scanning: 0 alertas abiertas.
- Gitleaks v8.30.1 sobre `main`: 205 commits, 0 hallazgos después de tres excepciones exactas para placeholders y vectores de prueba publicados.
- Ramas remotas `codex/gl0a-auth-security`, `feature/sprint5-dte-documentos`, `feature/sprint6-firma-transmision` y `feature/sprint8-dashboard`: 0 hallazgos.
- Una referencia local privada prepublicación contiene 130 coincidencias de la regla genérica en inventarios SHA-256 y evidencias; el stash local contiene otras 130 coincidencias equivalentes. No se confirmó una credencial activa. Estas referencias no deben publicarse y requieren revisión manual antes de cualquier traslado.

Las excepciones de [`.gitleaks.toml`](../.gitleaks.toml) comparan valores sintéticos exactos. No se ignoran carpetas, extensiones ni reglas completas.

## CI y rama principal

- `main` exige PR, una aprobación, conversación resuelta, historial lineal y rama actualizada.
- Administradores también están sujetos a la protección.
- Force-push y borrado de `main` están bloqueados.
- Checks requeridos: Build and tests, SQL Server 2022 migrations y Git history secret scan.
- Al 2026-09-10 GitHub no inicia runners por un bloqueo de facturación de la cuenta. Ningún fallo de ese periodo debe interpretarse como ejecución de los tests.

## Logging e incidentes

- Logs pueden incluir TraceId, EventId, EmpresaId y UsuarioId cuando sea seguro y necesario.
- Logs nunca incluyen passwords, JWT completos, llaves, certificados, secretos de webhook ni documentos fiscales completos.
- Ante un secreto confirmado: invalidarlo primero, rotarlo en cada ambiente, revisar uso, preservar evidencia saneada y purgar historial solo mediante un procedimiento coordinado.

## Gate de release

Los criterios obligatorios de seguridad y producción están en [RELEASE.md](RELEASE.md). Un árbol limpio no sustituye ZAP, revisión de dependencias, prueba de restauración ni smoke fiscal en STAGING.
