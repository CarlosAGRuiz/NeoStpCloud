param([Parameter(Mandatory=$true)][int]$EmpresaId,[Parameter(Mandatory=$true)][string]$ExpectedNit)
$ErrorActionPreference='Stop'
if($EmpresaId -ne 23 -or $ExpectedNit -cne '06232705261148'){throw 'CLIENT_IDENTITY_ARGUMENT_REJECTED'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$settings=Get-Content -LiteralPath (Join-Path $repo 'src/NeoSTP.Api/appsettings.Local.json') -Raw | ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($settings.ConnectionStrings.NeoStpDb)
if($builder.InitialCatalog -cne 'NeoSTP_Cloud' -or $builder.DataSource -notin @('.','(local)','localhost',[Environment]::MachineName)){throw 'DATABASE_TARGET_REJECTED'}
$builder['Pooling']=$false
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function Rows([string]$query){
 $command=$connection.CreateCommand();$command.CommandText=$query;$command.CommandTimeout=20
 [void]$command.Parameters.AddWithValue('@tenant',$EmpresaId);[void]$command.Parameters.AddWithValue('@nit',$ExpectedNit)
 try{$reader=$command.ExecuteReader();try{while($reader.Read()){$row=[ordered]@{};for($i=0;$i-lt$reader.FieldCount;$i++){$row[$reader.GetName($i)]=$(if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)})};[pscustomobject]$row}}finally{$reader.Dispose()}}finally{$command.Dispose()}
}
try{
 $connection.Open()
 $identity=@(Rows "SELECT DB_NAME() Db,CAST(SERVERPROPERTY('MachineName') AS nvarchar(128)) Machine,CAST(SERVERPROPERTY('ProductMajorVersion') AS int) Major")[0]
 if($identity.Db -cne 'NeoSTP_Cloud' -or $identity.Machine -ine [Environment]::MachineName -or $identity.Major -ne 16){throw 'DATABASE_IDENTITY_REJECTED'}
 $company=@(Rows "SELECT e.Id FROM Core_Empresas e JOIN Dte_Configuracion c ON c.EmpresaId=e.Id WHERE e.Id=@tenant AND REPLACE(REPLACE(e.Nit,'-',''),' ','')=@nit AND c.AmbienteCodigo='PRUEBAS'")
 if($company.Count -ne 1){throw 'CLIENT_OR_ENVIRONMENT_REJECTED'}
 $report=[ordered]@{ObservedAtUtc=[DateTime]::UtcNow.ToString('O');EmpresaId=23;OnlySelect=$true;NoRawFiscalPayloadsExported=$true;TransmissionPerformed=$false}
 $report.Establishment=@(Rows 'SELECT TipoEstablecimientoCodigo,CodigoEstablecimientoMh,CodigoPuntoVentaMh FROM Dte_Configuracion WHERE EmpresaId=@tenant')
 $report.Documents=@(Rows @'
SELECT d.Id,d.TipoDteCodigo,d.EstadoCodigo,d.AmbienteCodigo,
 CASE WHEN d.EnviadoAt IS NULL THEN 0 ELSE 1 END HasSendTimestamp,
 CASE WHEN NULLIF(d.SelloRecibido,'') IS NULL THEN 0 ELSE 1 END HasSeal,
 ISJSON(j.RespuestaHacienda) ResponseIsJson,
 CASE WHEN ISJSON(j.RespuestaHacienda)=1 THEN JSON_VALUE(j.RespuestaHacienda,'$.estado') END MhStatus,
 CASE WHEN ISJSON(j.RespuestaHacienda)=1 THEN JSON_VALUE(j.RespuestaHacienda,'$.codigoMsg') END MhCode,
 CASE WHEN j.RespuestaHacienda LIKE '%municipio%' OR j.RespuestaHacienda LIKE '%departamento%' OR j.RespuestaHacienda LIKE '%distrito%' THEN 1 ELSE 0 END MentionsTerritory,
 CASE WHEN j.RespuestaHacienda LIKE '%firma%' OR j.RespuestaHacienda LIKE '%certificado%' THEN 1 ELSE 0 END MentionsSignature,
 CASE WHEN j.RespuestaHacienda LIKE '%nit%' OR j.RespuestaHacienda LIKE '%nrc%' THEN 1 ELSE 0 END MentionsTaxId,
 CASE WHEN j.RespuestaHacienda LIKE '%version%' THEN 1 ELSE 0 END MentionsVersion,
 CASE WHEN NULLIF(j.JsonFirmado,'') IS NULL THEN 0 ELSE 1 END HasSignedPayload,
 CASE WHEN ISJSON(j.JsonDte)=1 THEN JSON_VALUE(j.JsonDte,'$.identificacion.version') END GeneratedVersion,
 CASE WHEN ISJSON(j.JsonDte)=1 THEN JSON_VALUE(j.JsonDte,'$.emisor.direccion.departamento') END GeneratedDepartment,
 CASE WHEN ISJSON(j.JsonDte)=1 THEN JSON_VALUE(j.JsonDte,'$.emisor.direccion.municipio') END GeneratedMunicipality,
 CASE WHEN ISJSON(j.JsonDte)=1 THEN JSON_VALUE(j.JsonDte,'$.emisor.direccion.distrito') END GeneratedDistrict
 FROM Dte_Documentos d LEFT JOIN Dte_DocumentoJson j ON j.DocumentoId=d.Id
 WHERE d.EmpresaId=@tenant ORDER BY d.Id
'@)
 $report.TerritorialCatalog=@(Rows @'
SELECT c.EmpresaId,c.Codigo Catalog,c.Activo,i.Codigo,i.Valor,i.ParentCodigo,
 JSON_VALUE(i.MetadataJson,'$.codigoMH') MhCode
 FROM Core_Catalogos c JOIN Core_CatalogoItems i ON i.CatalogoId=c.Id
 WHERE (c.EmpresaId IS NULL OR c.EmpresaId=@tenant) AND i.Activo=1 AND
 ((c.Codigo='DEPARTAMENTO_ES' AND i.Valor LIKE '%Libertad%') OR
 (c.Codigo='MUNICIPIO_ES' AND i.Valor LIKE '%Libertad%') OR
 (c.Codigo='DISTRITO_ES' AND (i.Valor LIKE '%Opico%' OR i.ParentCodigo LIKE '%LIBERTAD%')))
 ORDER BY c.Id,i.Orden,i.Codigo
'@)
 $output=Join-Path $repo 'tmp/client-certification-2026-09-05/blockers-readonly.json'
 $report|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $output -Encoding utf8
 $report|ConvertTo-Json -Depth 8
}finally{$connection.Dispose();$settings=$null;$builder=$null}
