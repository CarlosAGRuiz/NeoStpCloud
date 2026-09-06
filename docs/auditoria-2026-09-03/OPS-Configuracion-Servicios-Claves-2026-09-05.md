# OPS — configuración, servicios y claves — 2026-09-05

Estado: incremento de código validado y paquete aislado publicado; configuración operativa e instalación pendientes. No se ha desplegado en la instalación activa, instalado servicios ni cambiado el ambiente fiscal de NEO/DANIEL.

Continuación posterior: [Windows, dominios y recuperación real](OPS-Windows-Dominios-Recuperacion-2026-09-05.md). Ese corte confirma host y dominios, añade recuperación real bajo la identidad actual y publica un candidato nuevo; la evidencia de este informe conserva su alcance histórico.

## Qué corrige este incremento

1. Production rechaza proveedores ausentes, desconocidos o Mock, incluido Hacienda y firmador. Ya no acepta el bypass PermitirMocksEnProduccion. El catálogo se contrasta con las selecciones reales del contenedor de dependencias. Esto valida selección; no acredita credenciales, disponibilidad ni aceptación de cada proveedor. No configurar nombres de proveedores sin contrato/acceso para sortear el arranque.
2. appsettings.Local.json sólo participa en Development, después de los appsettings normales y antes de secretos de usuario, entorno y argumentos. Production y Staging lo ignoran incluso si contiene JSON inválido. Los tres hosts comparten la regla.
3. Web/API comparten la política del túnel local: sólo loopback, un salto, X-Forwarded-For/Proto. Se verifica con HTTP sintético que un cliente remoto no puede falsificar IP/esquema; Host no se toma del encabezado reenviado. API incorpora HSTS fuera de Development. La API YA tenía política de proxy en ApiAuthRateLimiting; la afirmación antigua de que carecía de ella era obsoleta.
4. API/Web/Worker incorporan Windows Service de forma contextual, con content root del ejecutable cuando los inicia SCM, nombres NeoSTP.Api/NeoSTP.Web/NeoSTP.Worker y salida no exitosa al fallar el host. Esto prepara soporte; no instala servicios ni prueba arranque sin login. Worker permanece sujeto a su activación explícita.
5. Production exige directorio de claves existente, absoluto, fuera del contenido publicado y sin redirecciones de directorio; además certificado RSA accesible con clave privada utilizable y selección explícita de almacén. Usa las APIs de DataProtection para persistir y cifrar con X509. Conserva NeoSTP.Cloud y NeoSTP.DteSecrets.v1. Los errores de configuración no exponen valores ni excepciones internas de proveedor.

## Claves: alcance de la prueba

La prueba sintética protege un valor, dispone el primer proveedor, copia los XML cifrados a otro directorio e importa desde memoria un PFX sintético para un proveedor nuevo. El valor se recupera; un certificado diferente no puede descifrarlo. No se escribieron certificados al almacén del sistema ni se exportaron claves reales.

Esto NO migra las claves actuales, no prueba recuperación entre identidades/equipos de Windows ni acredita la custodia del certificado real. El perfil observado tiene seis archivos de claves; su ACL incluye CodexSandboxUsers. No se revisó su contenido ni se acredita que sean todas las claves que usan los hosts. Conservarlas y ensayar recuperación real antes de cambiar la identidad de ejecución: crear un key ring vacío dejaría ilegibles secretos existentes de NEO y del cliente.

Parámetros nuevos: DataProtection:KeyRingPath, CertificateThumbprint (40 hex), StoreName=My y StoreLocation=CurrentUser o LocalMachine. Deben corresponder a un certificado dedicado y recuperable, con ACL de directorio/clave privada para las identidades autorizadas. No reutilizar automáticamente certificado de Hacienda o TLS. Fuera de Production, ausencia de esta sección conserva el comportamiento anterior del framework.

## Publicación separada

El publicador nuevo crea un directorio UTC/GUID bajo tmp/production-candidate, con Web/API/Worker y hashes. Excluye configuración de entorno, secretos, certificados y datos persistentes; realiza validación de contenido. No ejecuta el instalador local existente, que detiene y reemplaza tareas activas.

El paquete queda sin configuración operativa y con ReadyForDeployment=false. No debe arrancarse contra NeoSTP_Cloud de 79 migraciones ni presentarse como instalado por existir archivos compilados.

Resultados finales: **build Release de solución 0 advertencias/0 errores; 2073 unitarias + 9 integraciones aprobadas, sin omisiones**. Las 119 focales son un subconjunto, no se suman al total. Revisión independiente final sin P1/P2 nuevos en el incremento.

Publicación exitosa: tmp/production-candidate/20260905T184102Z-7c558335c51647c3bb8ca49beafc271c. Web 349 archivos, API 154, Worker 144: **647 hashes verificados, cero discrepancias**. Preflight examinó 65 assets fuente antes de publicar. login-password.js está presente e idéntico a fuente. El filtro cubre ResolvedFileToPublish y StaticWebAssets; conserva licencias legítimas. Los dos intentos anteriores fallidos se conservan con sus manifiestos incompletos; se corrigieron exclusiones de licencias y normalización del marcador virtual de fingerprint del SDK.

Las pruebas generales validan el código Release final; la publicación se verifica por separado y omite símbolos de depuración. No se ejecutaron los hosts publicados, SCM ni operaciones externas. [Evidencia del corte](evidencia/production-host-2026-09-05/README.md): TRX, compilación, manifiesto del paquete y verificación, metadatos observados y huellas de fuentes. PublicationComplete=true no modifica ReadyForDeployment=false.

## Instalación observada y pasos del corte

Las tareas API/Web actuales son Interactive/AtLogOn y sus procesos siguen iniciados el 2 de septiembre; no se observó servicio NeoSTP ni Worker. Cloudflared está activo y automático; API/Web escuchan loopback. Los appsettings publicados contienen AllowedHosts=* y ApiBaseUrl de Web referencia localhost; esto describe esos archivos, no prueba toda configuración efectiva del proceso ni el HTTPS público.

1. Confirmar equipo/servidor de producción, dominio público y responsables. El usuario ya tiene credenciales de Hacienda PRODUCCION y las introducirá directamente; no pedirlas por chat. La ubicación inicial del despliegue se consultó sin interrumpir trabajo independiente.
2. Preparar cuenta de servicio de mínimo privilegio, acceso SQL runtime separado de migración, ACL de binarios/logs/storage/claves y certificado de cifrado recuperable. Ensayar la recuperación de claves y secretos EXISTENTES, además del backup SQL aprobado en el corte anterior.
3. Completar configuración efectiva de proveedores necesarios, dominio/AllowedHosts/URLs, HTTPS y correo. Funciones excluidas requieren bloqueo en backend; ocultar menús o poner un nombre de proveedor real sin credenciales no las deshabilita. Continúan pendientes límites fiscales por plan/autorización.
4. Pasar preflight del despliegue, respaldar de nuevo para el corte, aplicar las diez migraciones aprobadas y sustituir controladamente las tareas por servicios. Validar recuperación, salud, autenticación, empresa, permisos y arranque sin login; mantener Worker apagado hasta su aceptación operativa específica.
5. Introducir credenciales productivas de NEO en la configuración segura, probar autenticación/firma y correo autorizado. Primera emisión real sólo con operación legítima; DANIEL conserva su certificación independiente y acuerdo comercial CLI-23.

El ensayo SQL anterior sigue válido para su candidato identificado, pero no sustituye la aceptación operativa de estos cambios nuevos. Sus evidencias se mantienen intactas y este corte tendrá las suyas.
