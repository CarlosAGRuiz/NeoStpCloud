# Verificación aislada de login y MFA

Renderiza la vista Razor real sin ejecutar el Program de la Web, sin registrar Infrastructure,
sin servidor HTTP, SQL, seed, login ni datos del cliente. DataProtection es efímero.

~~~powershell
dotnet restore tools/LoginPreview/LoginPreview.csproj --source https://api.nuget.org/v3/index.json
dotnet run --project tools/LoginPreview/LoginPreview.csproj -c Release --no-restore -- C:/Neo/NeoSTPBusinnesSuite/NeoStpCloud
~~~

Con Node, Playwright y Edge instalados, ejecutar:

~~~powershell
node tools/LoginPreview/verify-login.cjs C:/Neo/NeoSTPBusinnesSuite/NeoStpCloud
node tools/LoginPreview/verify-mfa.cjs C:/Neo/NeoSTPBusinnesSuite/NeoStpCloud
~~~

Si Playwright no está en las dependencias locales, configurar NODE_PATH solo para ese proceso.
El navegador intercepta todas las peticiones: sirve únicamente HTML/archivos locales y bloquea la red.
Las fuentes externas no aparecen en las capturas; el icono nuevo del visor es SVG local.

Resultado observado: 13 comprobaciones aprobadas (clic, teclado, accesibilidad, contenido intacto,
ocultación en submit/visibilitychange/pagehide, MFA independiente, móvil y ausencia de errores JS).
Archivos generados en tmp/login-preview (ignorados por git): HTML y capturas escritorio/móvil.

MFA: 14 comprobaciones de cuatro variantes Razor en escritorio/móvil, sin menú operativo ni overflow,
clave solo en configuración iniciada, sin devolver secreto en inputs, código oculto y antiforgery.
Todos los códigos y secretos usados para renderizar son sintéticos e inutilizables en el sistema.

Este visor no prueba autenticación HTTP. Esos recorridos se verifican por separado en
AuthHostSessionIntegrationTests mediante TestServer y EF InMemory, sin iniciar hosts reales.
