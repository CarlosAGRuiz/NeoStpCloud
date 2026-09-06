#requires -Version 7.0
[CmdletBinding()]
param(
 [ValidateSet('Preview','Rehearse','Apply')][string]$Mode='Preview',
 [Parameter(Mandatory)][string]$SqlPath,
 [Parameter(Mandatory)][ValidatePattern('^[A-F0-9]{64}$')][string]$SqlSha256,
 [Parameter(Mandatory)][ValidatePattern('^\d{14}_CLI23_CalendarBillingAndCompanyModuleGrants$')][string]$MigrationId,
 [string]$RehearsalManifestPath,
 [ValidatePattern('^[A-F0-9]{64}$')][string]$RehearsalManifestSha256
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$run=[Guid]::NewGuid().ToString('N')
$report=[ordered]@{AtUtc=[DateTime]::UtcNow.ToString('o');RunId=$run;Mode=$Mode;Passed=$false;Applied=$false;SourceDatabase='NeoSTP_Cloud';ExpectedMigration=$MigrationId;ExpectedMigrationCount=92;SqlSha256=$SqlSha256;HostsChanged=$false;ProviderCalls=0;RawRowsExported=$false;AutomaticRollback=$false;Old91BinaryCompatibleAfterApply=$false}
$source=$null;$master=$null;$clone=$null
function Require([bool]$ok,[string]$code){if(-not$ok){throw $code}}
function RepoFile([string]$path){
 $full=[IO.Path]::GetFullPath((Join-Path $repo $path))
 Require ($full.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) 'FILE_OUTSIDE_REPOSITORY'
 $cursor=$full
 while($cursor-and$cursor.Length-ge$repo.Length){Require (((Get-Item -LiteralPath $cursor -Force).Attributes-band[IO.FileAttributes]::ReparsePoint)-eq0) 'REPARSE_REJECTED';$cursor=[IO.Path]::GetDirectoryName($cursor)}
 return $full
}
function Rows($connection,[string]$sql,$parameters=@{}){
 $command=$connection.CreateCommand();$command.CommandTimeout=300;$command.CommandText=$sql
 foreach($key in $parameters.Keys){[void]$command.Parameters.AddWithValue($key,$parameters[$key])}
 try{$reader=$command.ExecuteReader();try{do{while($reader.Read()){$row=[ordered]@{};for($i=0;$i-lt$reader.FieldCount;$i++){$row[$reader.GetName($i)]=if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)}};[pscustomobject]$row}}while($reader.NextResult())}finally{$reader.Dispose()}}finally{$command.Dispose()}
}
function Execute($connection,[string]$sql,$parameters=@{}){
 $command=$connection.CreateCommand();$command.CommandTimeout=300;$command.CommandText=$sql
 foreach($key in $parameters.Keys){[void]$command.Parameters.AddWithValue($key,$parameters[$key])}
 try{[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}
}
function Quote([string]$name){'['+$name.Replace(']',']]')+']'}
function Migrations($connection){@(Rows $connection 'SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId').MigrationId}
function AssertBaseline($connection){$actual=@(Migrations $connection);Require ($actual.Count-eq91-and($actual-join'|')-ceq($expected[0..90]-join'|')) 'EXACT_91_PREFIX_REQUIRED'}
function WindowsGuard([string]$guardMode,[string]$id=''){
 $exe=Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
 $text=& $exe -NoProfile -NonInteractive -File (Join-Path $repo 'tools/ClientCertificationSchemaUpgrade/WindowsGuard.ps1') -Mode $guardMode -RunId $id
 Require ($LASTEXITCODE-eq0) 'WINDOWS_GUARD_FAILED'
 $proof=($text-join[Environment]::NewLine)|ConvertFrom-Json
 Require ($proof.Passed-eq$true) 'WINDOWS_GUARD_REJECTED'
 return $proof
}
function Definitions($connection){
 $columns=@(Rows $connection @'
SELECT SCHEMA_NAME(t.schema_id) SchemaName,t.name TableName,c.name ColumnName,
 ISNULL((SELECT TOP(1) ic.key_ordinal FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
 WHERE i.object_id=t.object_id AND i.is_primary_key=1 AND ic.column_id=c.column_id),0) KeyOrdinal
FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
WHERE t.is_ms_shipped=0 AND t.name<>'__EFMigrationsHistory' AND c.system_type_id<>189
ORDER BY SchemaName,TableName,c.column_id;
'@)
 $definitions=@($columns|Group-Object SchemaName,TableName|ForEach-Object{[pscustomobject]@{Schema=$_.Group[0].SchemaName;Table=$_.Group[0].TableName;Columns=@($_.Group.ColumnName);Keys=@($_.Group|Where-Object KeyOrdinal -gt 0|Sort-Object KeyOrdinal|ForEach-Object ColumnName)}})
 Require ($definitions.Count-gt0-and@($definitions|Where-Object {$_.Keys.Count-eq0}).Count-eq0) 'STABLE_PRIMARY_KEYS_REQUIRED'
 return $definitions
}
function Fingerprints($connection,$definitions){
 foreach($definition in $definitions){
  $table=(Quote $definition.Schema)+'.'+(Quote $definition.Table);$columns=($definition.Columns|ForEach-Object{Quote $_})-join',';$order=($definition.Keys|ForEach-Object{Quote $_})-join','
  $row=@(Rows $connection "SELECT COUNT_BIG(*) [RowCount],CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT $columns FROM $table ORDER BY $order FOR JSON PATH,INCLUDE_NULL_VALUES)),2) Digest FROM $table")[0]
  [pscustomobject]@{Schema=$definition.Schema;Table=$definition.Table;Rows=$row.RowCount;Sha256=$row.Digest}
 }
}
function Backup($connection){
 $directory=(WindowsGuard 'PrepareDirectory' $run).Directory
 $backup=Join-Path $directory 'source-copy-only.bak'
 Require (-not(Test-Path -LiteralPath $backup)) 'BACKUP_ALREADY_EXISTS'
 Execute $connection 'BACKUP DATABASE [NeoSTP_Cloud] TO DISK=@path WITH COPY_ONLY,CHECKSUM' @{'@path'=$backup}
 $report.BackupCompleted=$true
 Execute $connection 'RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM' @{'@path'=$backup}
 $null=WindowsGuard 'VerifyDirectory' $run
 $report.BackupVerified=$true;$report.BackupFile=$backup;$report.BackupSha256=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
 return $directory
}
function ApplyDelta($connection){
 AssertBaseline $connection
 Require ((Get-FileHash -LiteralPath $sqlFile -Algorithm SHA256).Hash-ceq$SqlSha256) 'SQL_CHANGED_BEFORE_APPLY'
 foreach($batch in [regex]::Split($sqlText,'(?im)^\s*GO\s*(?:--[^\r\n]*)?$')){if(-not[string]::IsNullOrWhiteSpace($batch)){Execute $connection $batch}}
 Require ((@(Migrations $connection)-join'|')-ceq($expected-join'|')) 'EXACT_92_HISTORY_REQUIRED'
}
try{
 Require ($SqlSha256-ceq'5B426111333F4BEB28B6054843DD8A842251D1EC61FB1DB1044A35B9026FE74C'-and$MigrationId-ceq'20260906152921_CLI23_CalendarBillingAndCompanyModuleGrants') 'REVIEWED_FIXED_DELTA92_REQUIRED'
 $sqlFile=RepoFile $SqlPath
 Require ((Get-FileHash -LiteralPath $sqlFile -Algorithm SHA256).Hash-ceq$SqlSha256) 'SQL_HASH_REJECTED'
 $sqlText=Get-Content -LiteralPath $sqlFile -Raw
 # This file accepts only the reviewed offline EF delta; its supplied hash is the
 # approval boundary. Reject destructive/DML/provider/server switching syntax too.
 $statementCheck=[regex]::Replace($sqlText,'(?i)\bON\s+DELETE\s+NO\s+ACTION\b','')
 Require ($statementCheck-notmatch'(?i)\b(DROP|DELETE|UPDATE|TRUNCATE|MERGE|EXEC(?:UTE)?|USE|BACKUP|RESTORE|DBCC|GRANT|DENY|REVOKE)\b') 'SQL_NON_ADDITIVE_OPERATION_REJECTED'
 foreach($match in [regex]::Matches($sqlText,'(?i)ALTER\s+TABLE\s+\[([^\]]+)\]\s+(\w+)')){Require ($match.Groups[1].Value-ceq'Core_EmpresaModulos'-and$match.Groups[2].Value-ieq'ADD') 'UNEXPECTED_ALTER_TABLE'}
 $created=@([regex]::Matches($sqlText,'(?i)CREATE\s+TABLE\s+\[([^\]]+)\]')|ForEach-Object {$_.Groups[1].Value})
 Require (($created|Sort-Object)-join'|'-ceq'Billing_CalendarAgreements|Billing_CalendarPeriods') 'EXACT_TWO_NEW_CALENDAR_TABLES_REQUIRED'
 foreach($match in [regex]::Matches($sqlText,'(?i)INSERT\s+INTO\s+\[([^\]]+)\]')){Require ($match.Groups[1].Value-ceq'__EFMigrationsHistory') 'SQL_DATA_INSERT_REJECTED'}
 Require ($sqlText.Contains($MigrationId)) 'SQL_MIGRATION_ID_REQUIRED'
 $expected=@(Get-ChildItem -LiteralPath (Join-Path $repo 'src/NeoSTP.Infrastructure/Persistence/Migrations') -File -Filter '*.cs'|Where-Object {$_.Name-match'^\d{14}_.+(?<!\.Designer)\.cs$'}|Sort-Object Name|ForEach-Object BaseName)
 Require ($expected.Count-eq92-and$expected[90]-ceq'20260905221056_CERT2_TenantDteTypeAuthorization'-and$expected[91]-ceq$MigrationId) 'EXACT_SOURCE_91_TO_92_REQUIRED'
 $report.SourceMigrationSetSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($expected-join'|')))
 if($Mode-eq'Apply'){
  $proofPath=RepoFile $RehearsalManifestPath
  Require ($RehearsalManifestSha256-and(Get-FileHash -LiteralPath $proofPath -Algorithm SHA256).Hash-ceq$RehearsalManifestSha256) 'REHEARSAL_HASH_REJECTED'
  $proof=Get-Content -LiteralPath $proofPath -Raw|ConvertFrom-Json
  Require ($proof.Mode-ceq'Rehearse'-and$proof.Passed-and$proof.SqlSha256-ceq$SqlSha256-and$proof.ExpectedMigration-ceq$MigrationId-and$proof.SourceMigrationSetSha256-ceq$report.SourceMigrationSetSha256-and$proof.BackupVerified-and$proof.CloneRetained-and$proof.OriginalRowsPreserved-and$proof.DbccErrors-eq0-and[DateTime]::Parse($proof.AtUtc).ToUniversalTime()-gt[DateTime]::UtcNow.AddHours(-48)) 'SUCCESSFUL_RECENT_REHEARSAL_REQUIRED'
  $report.RehearsalManifestSha256=$RehearsalManifestSha256
  $null=WindowsGuard 'Check'
 }
 $local=Get-Content -LiteralPath (Join-Path $repo 'out/client-certification-release/20260905T232433Z-9062dc25354442429d2ef07f76f24d94/api/appsettings.Local.json') -Raw|ConvertFrom-Json
 $builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($local.ConnectionStrings.NeoStpDb)
 Require ($builder.InitialCatalog-ceq'NeoSTP_Cloud'-and$builder.AttachDBFilename.Length-eq0-and$builder.DataSource-in@('.','(local)','localhost','127.0.0.1',[Environment]::MachineName)) 'LOCAL_DATABASE_REQUIRED'
 $source=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$source.Open()
 $identity=@(Rows $source "SELECT DB_NAME() DatabaseName,CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')) MachineName,CONVERT(int,SERVERPROPERTY('ProductMajorVersion')) MajorVersion,SERVERPROPERTY('InstanceName') InstanceName")[0]
 Require ($identity.DatabaseName-ceq'NeoSTP_Cloud'-and$identity.MachineName-ieq[Environment]::MachineName-and$identity.MajorVersion-eq16-and$null-eq$identity.InstanceName) 'SQL_SERVER_IDENTITY_REJECTED'
 AssertBaseline $source
 $report.SourceIdentityVerified=$true
 if($Mode-eq'Preview'){$report.Passed=$true;$report.SqlSchemaChangesStarted=$false;return}
 $directory=Backup $source
 if($Mode-eq'Rehearse'){
  $target='NeoClientBilling92_'+$run;$report.TargetDatabase=$target
  $builder['Initial Catalog']='master';$master=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$master.Open()
  Require (@(Rows $master 'SELECT name FROM sys.databases WHERE name=@name' @{'@name'=$target}).Count-eq0) 'CLONE_ALREADY_EXISTS'
  $fileList=@(Rows $master 'RESTORE FILELISTONLY FROM DISK=@path' @{'@path'=$report.BackupFile})
  Require ($fileList.Count-eq2-and@($fileList|Where-Object Type -eq 'D').Count-eq1-and@($fileList|Where-Object Type -eq 'L').Count-eq1) 'TWO_FILE_BACKUP_REQUIRED'
  $data=Join-Path $directory ($target+'.mdf');$log=Join-Path $directory ($target+'.ldf')
  Require (-not(Test-Path -LiteralPath $data)-and-not(Test-Path -LiteralPath $log)) 'CLONE_FILES_ALREADY_EXIST'
  Execute $master ('RESTORE DATABASE '+(Quote $target)+' FROM DISK=@backup WITH MOVE @dataLogical TO @data,MOVE @logLogical TO @log,RECOVERY,CHECKSUM') @{'@backup'=$report.BackupFile;'@dataLogical'=($fileList|Where-Object Type -eq 'D').LogicalName;'@logLogical'=($fileList|Where-Object Type -eq 'L').LogicalName;'@data'=$data;'@log'=$log}
  $builder['Initial Catalog']=$target;$clone=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$clone.Open();$work=$clone
  Require (@(Rows $clone 'SELECT DB_NAME() Name')[0].Name-ceq$target) 'CLONE_IDENTITY_REJECTED'
  $report.CloneRetained=$true
 }else{$work=$source;$report.TargetDatabase='NeoSTP_Cloud';$null=WindowsGuard 'Check'}
 $definitions=@(Definitions $work);$before=@(Fingerprints $work $definitions)
 $report.BeforeFingerprints=$before
 $report.SqlSchemaChangesStarted=$true
 ApplyDelta $work
 $report.Applied=($Mode-eq'Apply')
 $after=@(Fingerprints $work $definitions)
 $report.AfterFingerprints=$after
 $report.OriginalRowsPreserved=(ConvertTo-Json -InputObject $before -Compress -Depth 4)-ceq(ConvertTo-Json -InputObject $after -Compress -Depth 4)
 Require $report.OriginalRowsPreserved 'ORIGINAL_ROWS_CHANGED'
 $report.OriginalTableCount=$definitions.Count
 $integrity=@(Rows $work ('DBCC CHECKDB ('+(Quote $report.TargetDatabase)+') WITH TABLERESULTS,NO_INFOMSGS,ALL_ERRORMSGS'))
 $report.DbccErrors=@($integrity|Where-Object {$null-eq$_.Level-or$_.Level-ge11}).Count
 Require ($report.DbccErrors-eq0) 'DBCC_ERRORS'
 if($Mode-eq'Rehearse'){AssertBaseline $source;$report.SourceSchemaUnchanged=$true}else{$null=WindowsGuard 'Check'}
 $report.Passed=$true
}catch{$report.FailureType=$_.Exception.GetType().Name;$report.FailureCode=if($_.Exception.Message-cmatch'\A[A-Z0-9_]+\z'){$_.Exception.Message}else{'SCHEMA_OPERATION_REVIEW_REQUIRED'};if($_.Exception.GetBaseException()-is[System.Data.SqlClient.SqlException]){$report.SqlErrorNumber=$_.Exception.GetBaseException().Number}}
finally{
 if($clone){$clone.Dispose()};if($master){$master.Dispose()};if($source){$source.Dispose()};$local=$null;$builder=$null
 $outputDirectory=Join-Path $repo ('tmp/client-commercial-schema92/'+$run);[void][IO.Directory]::CreateDirectory($outputDirectory)
 $output=Join-Path $outputDirectory 'results.json';$report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $output -Encoding utf8
 $report|ConvertTo-Json -Depth 6;Write-Output ('Evidence: '+$output)
}
if(-not$report.Passed){exit 1}
