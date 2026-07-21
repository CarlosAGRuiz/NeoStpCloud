# SSO corporativo (OIDC) — Microsoft Entra ID / Google Workspace

**Estado:** E3 del roadmap enterprise. Deshabilitado por defecto (`Sso:Enabled = false`).

El SSO permite que los usuarios de un cliente Enterprise inicien sesión con su cuenta
corporativa (Microsoft Entra ID o Google Workspace) en lugar de una contraseña propia.
No reemplaza el modelo de permisos: el rol sigue definiendo qué puede hacer el usuario;
el proveedor OIDC solo sustituye la autenticación.

## Modelo

- El SaaS registra **una** app multi-tenant en Microsoft Entra y **un** cliente OAuth en
  Google (credenciales en `appsettings`, sección `Sso`).
- Cada empresa declara en **Ajustes → SSO corporativo** (`/Sso`) su **dominio de correo**
  (p. ej. `contoso.com`) y, opcionalmente para Entra, su **Tenant ID** para restringir la
  validación a su directorio.
- Al iniciar sesión por SSO, el sistema resuelve la cuenta en este orden:
  1. **Sujeto vinculado**: si esa identidad federada (`oid`/`sub`) ya está atada a una
     cuenta local, entra directo.
  2. **Vinculación por correo**: si existe un usuario local con ese correo, se vincula la
     identidad federada y entra.
  3. **Auto-aprovisionamiento**: si la empresa dueña del dominio lo habilita, se crea la
     cuenta con el **rol por defecto** configurado. Si no, se rechaza el acceso.

## Configuración de plataforma (`appsettings` / secrets)

```json
"Sso": {
  "Enabled": true,
  "Microsoft": {
    "Authority": "https://login.microsoftonline.com/organizations/v2.0",
    "ClientId": "<app registration client id>",
    "ClientSecret": "<secret>"
  },
  "Google": {
    "Authority": "https://accounts.google.com",
    "ClientId": "<oauth client id>",
    "ClientSecret": "<secret>"
  }
}
```

Los secretos van en `dotnet user-secrets` (dev) o en el gestor de secretos del ambiente.
Si `Enabled` es `true` pero un proveedor no tiene `ClientId`/`ClientSecret`, ese proveedor
simplemente no se ofrece (no rompe el arranque).

### Redirect URIs a registrar en el IdP

- Microsoft Entra: `https://<host>/signin-microsoft`
- Google: `https://<host>/signin-google`

Entra: registrar la app como **multi-tenant** (`organizations`), scopes `openid profile
email`. Google: pantalla de consentimiento + credenciales OAuth 2.0 de tipo *Web
application*.

## Configuración por empresa

En `/Sso` (permiso `Seguridad.Sso.Gestionar`, incluido en ADMIN):

| Campo | Descripción |
|---|---|
| Proveedor | ENTRA o GOOGLE |
| Dominio de correo | Dominio corporativo que mapea a la empresa (único en la plataforma) |
| Tenant ID (Entra) | Opcional; restringe a un directorio de Entra |
| SSO habilitado | Activa/desactiva el flujo para la empresa |
| Auto-aprovisionar | Crea usuarios en el primer login |
| Rol por defecto | Rol asignado a las cuentas auto-creadas |

## API

- `GET /api/sso/config` · `PUT /api/sso/config` — administra la configuración por empresa
  (permiso `Seguridad.Sso.Gestionar`). El flujo de login federado es interactivo (web).

## Notas de seguridad

- Las cuentas creadas por SSO reciben una contraseña aleatoria inutilizable: solo entran
  por SSO.
- La vinculación por correo confía en el correo verificado por el IdP.
- El cookie intermedio del retorno OIDC usa `SameSite=None; Secure` (requerido para el
  round-trip cross-site) y expira en 10 minutos.
- Una empresa suspendida no permite auto-aprovisionar ni operar (enforcement de Entrega 7).

## Pendiente (follow-up)

- SSO para móvil/API (intercambio de `id_token` validado) — hoy el login federado es web.
- SLO / logout federado end-to-end.
