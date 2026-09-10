# Main: base común de Web y API

## Alcance público

La rama main es la base del producto compartido: Web MVC/Razor, API para la aplicación móvil, servicios de aplicación, dominio, infraestructura y pruebas. La app móvil se mantiene en su repositorio separado y consume los contratos de esta API. No se guardan datos de una instalación o empresa real en esta base.

Se integran autenticación y aislamiento por empresa, integridad/idempotencia fiscal, conciliación de DTE, diagnóstico, permisos/licencias, cobro calendario, selección territorial y correo automático. La disponibilidad de código no acredita habilitación productiva de todas las integraciones: pasarelas y workers sujetos a banderas continúan desactivados hasta validación específica.

Los informes de soporte y certificación, capturas de clientes, configuraciones concretas, claves y herramientas de operación por empresa son privados. Se conservan fuera de main. Las referencias históricas a esos informes se sustituyen por este documento, que no publica sus resultados particulares.

## Contratos comunes

- Filtrar cada operación por EmpresaId; conservar permisos, módulos, auditoría y contexto de empresa.
- Web usa servicios compartidos, no delega toda la emisión a HTTP API. Configurar ambos hosts de forma coherente.
- El ambiente Hacienda es por empresa e independiente de ASPNETCORE_ENVIRONMENT. No convertir documentos históricos ni cambiar un flag global para un solo contribuyente.
- Clientes: distritoCodigo pertenece al municipio y departamento seleccionados. En actualización, null/ausente preserva distrito solo con padres sin cambios; cadena vacía lo limpia. Extranjeros no guardan territorio salvadoreño.
- Correo: intento después de persistir PROCESADO con sello, adjuntos PDF/JSON y CC al correo de la empresa propietaria. SMTP fallido no revierte estado fiscal. No ofrece cola durable, reintentos automáticos ni garantía de recepción.
- Suscripción calendario: último día del mes en America/El_Salvador; cobro pendiente no equivale a pago confirmado. Cambios financieros requieren conciliación.
- Cambios compatibles con consumidores móviles; documentar nuevos campos y errores en los README de API/Web.

## Desarrollo y validación

Requisitos: SDK .NET 10. Configuración privada mediante variables o appsettings.Local.json excluido de Git. Los ejemplos no son credenciales utilizables. Para Docker Compose definir SA_PASSWORD y JWT_KEY en un .env privado; se requieren explícitamente y no tienen respaldo a una contraseña publicada. Docker Compose es para desarrollo, no una receta productiva.

Desde un checkout limpio:

```powershell
pwsh -NoProfile -File tools/RepositoryHygiene/Check-PublicTree.ps1
dotnet restore NeoSTP.slnx
dotnet build NeoSTP.slnx -c Release --no-restore
dotnet test NeoSTP.slnx -c Release --no-build
```

Para regresión aislada no configurar las variables opcionales de pruebas SMTP reales. CI usa mocks y no inicia hosts con datos reales. La suite no sustituye pruebas de entrega de correo, certificación fiscal ni restauración de respaldo; esas pruebas requieren entorno y autorización específicos.

## Reglas de publicación

Validación de esta integración (10/09/2026): compilación Release de la solución con 0 errores y 0 advertencias; 2,401 pruebas unitarias y 9 de integración aprobadas. Comprobación de higiene del árbol público y sus autopruebas aprobadas. Las variables de SMTP real se desactivaron para esta ejecución; no se iniciaron servicios ni se aplicaron migraciones. Estos resultados no equivalen a una auditoría exhaustiva de seguridad ni validan servicios externos en producción.

1. Revisar árbol e historial a publicar, no solo el último diff. Nunca empujar ramas de respaldo privado, usar push --all ni subir archivos excluidos con force-add.
2. Preservar evidencias locales fuera del repositorio público. Nombres reales, NIT/NRC, correos, documentos, sellos y snapshots de cliente no son fixtures de prueba.
3. Mantener desactivadas migraciones/seeds en despliegues hasta revisar SQL, respaldo y rollback. No cambia la base al compilar o hacer merge.
4. No publicar binarios de instalación, configuración privada, certificados, claves ni registros operativos.
5. Validar el mismo código usado por Web y API; ensayos operativos solo en empresas de prueba explícitamente verificadas.

La integración saneada se construye sobre el main remoto existente sin arrastrar los commits pendientes con informes privados. Esto no borra versiones históricas ya publicadas ni otras ramas remotas: purgar ese historial y rotar cualquier credencial expuesta requiere una operación separada y coordinada. No confundir un árbol actual saneado con la eliminación de todo dato histórico de GitHub.

## Pendientes funcionales

Cola durable de correo, nueva distinción de precios con/sin IVA, validación real de proveedores y ensayos de recuperación. No se habilitan proveedores, MFA o cambios de permisos SQL por esta limpieza. [Notas de correo/distrito](releases/2026-09-10.md).
