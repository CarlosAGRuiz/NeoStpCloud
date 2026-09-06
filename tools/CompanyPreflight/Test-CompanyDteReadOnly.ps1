param(
    [Parameter(Mandatory=$true)][int]$EmpresaId,
    [Parameter(Mandatory=$true)][string]$ExpectedNit
)
$ErrorActionPreference = 'Stop'
# Never starts Web/API, saves a DTE, reserves a number, migrates, authenticates or contacts MH.
$repoPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$localConfig = Get-Content -Raw -LiteralPath (Join-Path $repoPath 'src/NeoSTP.Api/appsettings.Local.json') | ConvertFrom-Json
$connectionString = $localConfig.ConnectionStrings.NeoStpDb
if ([string]::IsNullOrWhiteSpace($connectionString)) { throw 'Missing explicit local database configuration.' }
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
public static class CompanyCertificatePreflight {
    public static bool CheckPair(byte[] certificate) {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var stream = new MemoryStream(certificate);
        using var reader = XmlReader.Create(stream, settings);
        var root = XDocument.Load(reader).Root ?? throw new InvalidOperationException("Missing root");
        string Key(string name) => root.Elements().Single(e => e.Name.LocalName == name)
            .Elements().Single(e => e.Name.LocalName == "encodied").Value;
        var privateBytes = Convert.FromBase64String(Key("privateKey"));
        try {
            using var signing = RSA.Create();
            using var verifying = RSA.Create();
            signing.ImportPkcs8PrivateKey(privateBytes, out _);
            verifying.ImportSubjectPublicKeyInfo(Convert.FromBase64String(Key("publicKey")), out _);
            // Random local challenge, NOT a fiscal document or externally usable DTE payload.
            var challenge = RandomNumberGenerator.GetBytes(32);
            var signature = signing.SignData(challenge, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
            return verifying.VerifyData(challenge, signature, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
        } finally { CryptographicOperations.ZeroMemory(privateBytes); }
    }
}
'@
$connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
$certificateBytes = $null
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 10
    # Explicit SELECT only; no host, EF, stored procedure or generic SQL argument.
    $command.CommandText = 'SELECT e.Nit,e.RazonSocial,e.CodigoActividad,e.Departamento,e.Municipio,e.Correo,e.Telefono,c.AmbienteCodigo,c.CertificadoBlob FROM Core_Empresas e INNER JOIN Dte_Configuracion c ON c.EmpresaId=e.Id WHERE e.Id=@empresa;'
    $null = $command.Parameters.AddWithValue('@empresa',$EmpresaId)
    $reader = $command.ExecuteReader()
    try {
        if (-not $reader.Read()) { throw 'Company/configuration not found.' }
        if (($reader.GetString(0) -replace '\D','') -ne ($ExpectedNit -replace '\D','')) { throw 'Company identity mismatch; stopped.' }
        if ($reader.GetString(7) -cne 'PRUEBAS') { throw 'Only PRUEBAS is allowed; stopped.' }
        if ($reader.IsDBNull(8)) { throw 'Certificate is missing.' }
        $certificateBytes = [byte[]]$reader.GetValue(8)
        $report = [ordered]@{
            EmpresaId = $EmpresaId
            Empresa = $reader.GetString(1)
            Ambiente = 'PRUEBAS'
            ActividadFormatoCincoDigitos = (-not $reader.IsDBNull(2) -and $reader.GetString(2) -match '^\d{5}$')
            DepartamentoFormatoDosDigitos = (-not $reader.IsDBNull(3) -and $reader.GetString(3) -match '^\d{2}$')
            MunicipioFormatoDosDigitos = (-not $reader.IsDBNull(4) -and $reader.GetString(4) -match '^\d{2}$')
            CorreoPresente = (-not $reader.IsDBNull(5) -and -not [string]::IsNullOrWhiteSpace($reader.GetString(5)))
            TelefonoPresente = (-not $reader.IsDBNull(6) -and -not [string]::IsNullOrWhiteSpace($reader.GetString(6)))
        }
    } finally { $reader.Dispose() }
    $report.CertificadoParRs512Valido = [CompanyCertificatePreflight]::CheckPair($certificateBytes)
    $report.Alcance = 'Lectura SQL y desafío criptográfico local; no JSON DTE, firma fiscal, envío, credenciales MH ni escritura de datos.'
    $report.Limites = 'Formato almacenado no valida catálogo ni actividad autorizada por MH. No comprueba vigencia/revocación/autorización del certificado en Hacienda.'
    $report | ConvertTo-Json
    if (-not $report.CertificadoParRs512Valido) { throw 'Certificate key pair verification failed.' }
} catch {
    # Never print exception bodies from a SQL driver or certificate parser containing sensitive data.
    Write-Error ('Preflight failed (' + $_.Exception.GetType().Name + '). No fiscal transmission was attempted.') -ErrorAction Continue
    exit 1
} finally {
    $connection.Dispose()
    if ($null -ne $certificateBytes) { [Array]::Clear($certificateBytes,0,$certificateBytes.Length) }
    $localConfig = $null
    $connectionString = $null
}
