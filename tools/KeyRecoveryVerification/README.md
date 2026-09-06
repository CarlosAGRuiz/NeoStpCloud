# Verificación acotada de recuperación de claves

Arnés operativo de solo lectura para este Windows, la identidad que ejecuta actualmente Web/API y las empresas 2 y 23. No arranca hosts, no registra servicios, no usa EF ni ejecuta migraciones. La cadena SQL se obtiene en memoria de la configuración publicada de API; nunca se imprime. El programa valida base `NeoSTP_Cloud`, equipo local y SQL Server 16, y consulta exclusivamente los tres campos cifrados de `dbo.Dte_Configuracion` para esas empresas.

DataProtection conserva `NeoSTP.Cloud` y `NeoSTP.DteSecrets.v1`. Usa `DisableAutomaticKeyGeneration()` y un `IXmlRepository` que rechaza toda escritura. Los resultados contienen únicamente presencia, descifrado y contenido no vacío, además de metadatos de ejecución. No contienen valores SQL cifrados o descifrados, longitudes, hashes de secretos ni XML de claves. Los hashes de archivos se comparan exclusivamente en memoria.

## Ejecución

Ejecutar desde la raíz del repositorio, bajo la identidad autorizada. Los comandos no cambian los procesos publicados. Coordinar el uso exclusivo de dotnet con las demás verificaciones.

```powershell
dotnet build tools/KeyRecoveryVerification/KeyRecoveryVerification.csproj -c Release
dotnet tools/KeyRecoveryVerification/bin/Release/net10.0/KeyRecoveryVerification.dll
```

La primera fase usa el repositorio existente del perfil. Su evidencia se guarda en `tmp/neo-production/key-recovery-existing.json`.

`New-ProtectedKeyRecoveryCopy.ps1` exige una primera fase satisfactoria. Crea una carpeta nueva `C:\ProgramData\NeoSTP-KeyRecovery-<GUID>` y fija/valida su ACL **antes de copiar claves**: herencia deshabilitada y acceso únicamente a la identidad actual, SYSTEM y Administradores. Comprueba que los XML contienen claves cifradas, rechaza reparse points y verifica igualdad de hashes sin exportarlos. No genera, descifra, reprotege ni modifica claves. Una copia previa con manifiesto impide crear otra accidentalmente.

```powershell
./tools/KeyRecoveryVerification/New-ProtectedKeyRecoveryCopy.ps1
$recoveryCopy = Get-Content tmp/neo-production/key-recovery-copy.json -Raw | ConvertFrom-Json
dotnet tools/KeyRecoveryVerification/bin/Release/net10.0/KeyRecoveryVerification.dll --restored-directory $recoveryCopy.ProtectedDirectory
```

La segunda instancia valida nuevamente la ACL del directorio y de cada archivo, usa exclusivamente la copia como repositorio y guarda `tmp/neo-production/key-recovery-restored.json`. Los XML permanecen fuera del repositorio y de `tmp`; allí solo quedan metadatos y resultados. El arnés puede leer el ring original para comparar hashes, pero no lo usa como repositorio en esta fase.

## Resultado acreditado el 5 de septiembre de 2026

- Compilación final del arnés: cero errores y cero advertencias.
- Seis XML cifrados copiados y comparados; ACL restringida verificada.
- Cuatro campos presentes recuperados en las dos fases: password MH y token MH de las empresas 2 y 23.
- Password de certificado ausente en ambas configuraciones; no se puede acreditar su recuperación.
- Cero intentos de escritura en el ring; original y copia sin cambios. Solo consultas SQL de lectura.
- Copia retenida: `C:\ProgramData\NeoSTP-KeyRecovery-ec58523371504459bc195d0dc2e929af`.

La evidencia formal sanitizada está en `docs/auditoria-2026-09-03/evidencia/ops-key-recovery-2026-09-05/`.

## Límites

La prueba acredita recuperación desde una copia bajo **la misma identidad y el mismo Windows**. No acredita una identidad de servicio nueva, otro equipo, recuperación tras pérdida del perfil, disponibilidad de credenciales ante Hacienda, descifrado de todos los secretos de la base, ni respaldo fuera del host. No transforma las claves antiguas para el nuevo certificado X509 de despliegue. La copia conserva su protección original; no debe confundirse con una migración de claves ni con una configuración productiva aplicada.

No borrar ni trasladar el ring activo al limpiar el ensayo. La copia retenida solo debe eliminarse después de comprobar su ruta absoluta y el GUID exacto del manifiesto, conforme a la decisión de retención del operador.
