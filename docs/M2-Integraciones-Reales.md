# M2 — Encender integraciones reales (OCR + FCM)

Las integraciones externas son **pluggable** y vienen en **Mock** por defecto. Para activarlas se
cambia el toggle por configuración y se aportan los secretos en `appsettings.Local.json`
(gitignored) o variables de entorno. **Nunca** se commitean credenciales reales.

## M2.1 — OCR/IA real de NeoScan (Gemini Flash)

Proveedor: **Google Generative Language API (Gemini)**, multimodal (imagen/PDF → JSON de campos DTE).

Implementación: `GeminiScanExtractionService` (toggle `Scan:Provider=Gemini`). Es resiliente:
ante cualquier fallo (HTTP, JSON inválido, sin API key) devuelve `Confianza = 0` y el documento
cae a `REQUIERE_REVISION` (captura/corrección manual), nunca lanza.

```jsonc
// appsettings.Local.json
"Scan": {
  "Provider": "Gemini",
  "Gemini": {
    "ApiKey": "<API key de Google AI Studio>",   // secreto
    "Model": "gemini-2.0-flash"                    // opcional (default)
  }
}
```

1. Obtén una API key en https://aistudio.google.com/ (Generative Language API).
2. Pon `Scan:Provider=Gemini` y la `ApiKey`.
3. Sube un documento en `/Scan`: si la extracción es buena, los campos vienen prellenados.

## M2.2 — Push FCM real (Firebase Cloud Messaging HTTP v1)

Implementación: `FcmPushSender` (toggle `Push:Provider=Fcm`) + `ServiceAccountTokenProvider`
(OAuth2 JWT-bearer firmado RS256, con caché del access token). Envía un mensaje por token
(FCM v1 no acepta multicast) y reporta tokens inválidos/no registrados; `AlertaService` los
**desactiva** automáticamente (`DispositivoNotificacion.Activo = false`).

```jsonc
// appsettings.Local.json
"Push": {
  "Provider": "Fcm",
  "Fcm": {
    "ProjectId": "<project_id>",                  // del service account
    "ClientEmail": "<client_email>",              // secreto
    "PrivateKey": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----\n"  // secreto, \n escapados
  }
}
```

1. En Firebase Console → Configuración → Cuentas de servicio → genera una clave privada (JSON).
2. Copia `project_id`, `client_email` y `private_key` del JSON a la config (la `private_key`
   conserva los `\n` escapados).
3. Pon `Push:Provider=Fcm`. Las alertas (`AlertaService.CrearAsync`) enviarán push a los
   dispositivos activos de la empresa/usuario.

## M2.3 — Verificación NIT en línea (bloqueado)

Pendiente hasta que el Ministerio de Hacienda publique el servicio. Hoy `INitVerificationService`
valida formato + lookup local (Mobile B-6); el hook online queda listo para `Fuente=MH`.

## Notas

- Los toggles se evalúan en `Infrastructure/DependencyInjection.cs`. Si el provider no es el real,
  se registra el Mock — el sistema funciona idéntico sin credenciales.
- Tests: `GeminiScanExtractionServiceTests`, `FcmPushSenderTests`, `ServiceAccountTokenProviderTests`
  (HTTP simulado, sin contactar servicios reales) + cobertura de desactivación de tokens en
  `AlertaServiceTests`.
