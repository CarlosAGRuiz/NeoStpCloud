param([string]$ManifestPath='tmp/neo-production/clone-manifest.json')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$configuration=Get-Content -LiteralPath (Join-Path $repo 'src/NeoSTP.Api/appsettings.Local.json') -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($configuration.ConnectionStrings.NeoStpDb)
if($builder.InitialCatalog -ne 'NeoSTP_Cloud'){throw 'Unexpected source database.'}
$source=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$masterBuilder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($builder.ConnectionString);$masterBuilder['Initial Catalog']='master'
$master=[System.Data.SqlClient.SqlConnection]::new($masterBuilder.ConnectionString)
$clone=$null
$runId=[Guid]::NewGuid().ToString('N')
$targetName='NeoProductionAudit_'+$runId
$allowedBackupRoot='C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup'
$protectedDirectory=[IO.Path]::GetFullPath((Join-Path $allowedBackupRoot $targetName))
if(-not $protectedDirectory.StartsWith($allowedBackupRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or $targetName -notmatch '^NeoProductionAudit_[a-f0-9]{32}$'){throw 'Unexpected owned output identity.'}
$backupFile=Join-Path $protectedDirectory 'source-copy-only.bak'
$dataFile=Join-Path $protectedDirectory ($targetName+'.mdf')
$logFile=Join-Path $protectedDirectory ($targetName+'.ldf')
$manifest=[ordered]@{GeneratedAtUtc=[DateTime]::UtcNow.ToString('O');RunId=$runId;SourceDatabase='NeoSTP_Cloud';TargetDatabase=$targetName;ProtectedDirectory=$protectedDirectory;BackupFile=$backupFile;DataFile=$dataFile;LogFile=$logFile;BackupCompleted=$false;Verified=$false;Restored=$false;CloneRetained=$true;RawRowsExported=$false}
function Rows($connection,[string]$sql,$parameters=@{}) {
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=120;$cmd.CommandText=$sql
 foreach($key in $parameters.Keys){[void]$cmd.Parameters.AddWithValue($key,$parameters[$key])}
 try{$reader=$cmd.ExecuteReader();try{while($reader.Read()){$row=[ordered]@{};for($i=0;$i-lt$reader.FieldCount;$i++){$row[$reader.GetName($i)]=$(if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)})};[pscustomobject]$row}}finally{$reader.Dispose()}}finally{$cmd.Dispose()}
}
function Execute($connection,[string]$sql,$parameters=@{}) {
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=180
 $cmd.CommandText=$sql;foreach($key in $parameters.Keys){[void]$cmd.Parameters.AddWithValue($key,$parameters[$key])}
 try{[void]$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}
}
function Identifier([string]$name){return '['+$name.Replace(']',']]')+']'}
function Fingerprints($connection,$definitions) {
 foreach($definition in $definitions){
  $table=(Identifier $definition.SchemaName)+'.'+(Identifier $definition.TableName)
  $columns=($definition.Columns|ForEach-Object{Identifier $_})-join ','
  $order=($definition.KeyColumns|ForEach-Object{Identifier $_})-join ','
  if([string]::IsNullOrWhiteSpace($order)){throw 'Fingerprint table lacks stable key.'}
  foreach($tenant in @(2,23)){
   $sql="SELECT COUNT_BIG(*) [RowCount],CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT $columns FROM $table WHERE EmpresaId=@tenant ORDER BY $order FOR JSON PATH,INCLUDE_NULL_VALUES)),2) Digest FROM $table WHERE EmpresaId=@tenant"
   $fingerprint=@(Rows $connection $sql @{'@tenant'=$tenant})[0]
   [pscustomobject]@{SchemaName=$definition.SchemaName;TableName=$definition.TableName;EmpresaId=$tenant;RowCount=$fingerprint.RowCount;Digest=$fingerprint.Digest}
  }
 }
}
try{
 $source.Open();$master.Open()
 $identity=@(Rows $source "SELECT DB_NAME() DatabaseName,CAST(SERVERPROPERTY('ProductMajorVersion') AS int) MajorVersion,IS_SRVROLEMEMBER('sysadmin') IsSysadmin,HAS_PERMS_BY_NAME('master','DATABASE','CREATE DATABASE') CanCreateDatabase")[0]
 if($identity.DatabaseName-ne'NeoSTP_Cloud'-or$identity.MajorVersion-ne16-or($identity.IsSysadmin-ne1-and$identity.CanCreateDatabase-ne1)){throw 'Source identity or clone permission mismatch.'}
 if(@(Rows $master 'SELECT name FROM sys.databases WHERE name=@name' @{'@name'=$targetName}).Count-ne0){throw 'Target database already exists.'}
 if(Test-Path -LiteralPath $protectedDirectory){throw 'Protected target directory already exists.'}
 $columns=@(Rows $source @'
SELECT SCHEMA_NAME(t.schema_id) SchemaName,t.name TableName,c.name ColumnName,
 ISNULL((SELECT TOP(1) ic.key_ordinal FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
 WHERE i.object_id=t.object_id AND i.is_primary_key=1 AND ic.column_id=c.column_id),0) KeyOrdinal
 FROM sys.tables t JOIN sys.columns c ON t.object_id=c.object_id
 WHERE EXISTS(SELECT 1 FROM sys.columns tenant WHERE tenant.object_id=t.object_id AND tenant.name='EmpresaId')
 AND c.system_type_id<>189 ORDER BY SchemaName,TableName,c.column_id
'@)
 $definitions=@($columns|Group-Object SchemaName,TableName|ForEach-Object{[pscustomobject]@{SchemaName=$_.Group[0].SchemaName;TableName=$_.Group[0].TableName;Columns=@($_.Group.ColumnName);KeyColumns=@($_.Group|Where-Object{$_.KeyOrdinal-gt0}|Sort-Object KeyOrdinal|ForEach-Object{$_.ColumnName})}})
 $manifest.SourceEngineMajor=16;$manifest.TableDefinitions=$definitions
 $manifest.SourceBefore=@(Fingerprints $source $definitions)
 $manifest.SourceMigrations=@(Rows $source 'SELECT COUNT(*) MigrationCount,MAX(MigrationId) LastMigration FROM __EFMigrationsHistory')[0]
 if($manifest.SourceMigrations.MigrationCount-ne79){throw 'Source migration baseline changed.'}
 [void][IO.Directory]::CreateDirectory($protectedDirectory)
 # The server-owned Backup parent already grants only SYSTEM/admins/service/current-user access.
 Execute $source 'BACKUP DATABASE [NeoSTP_Cloud] TO DISK=@backup WITH COPY_ONLY,CHECKSUM' @{'@backup'=$backupFile}
 $manifest.BackupCompleted=$true;Write-Output 'COPY_ONLY backup with CHECKSUM completed.'
 Execute $master 'RESTORE VERIFYONLY FROM DISK=@backup WITH CHECKSUM' @{'@backup'=$backupFile}
 $manifest.Verified=$true;Write-Output 'VERIFYONLY with CHECKSUM completed.'
 $fileList=@(Rows $master 'RESTORE FILELISTONLY FROM DISK=@backup' @{'@backup'=$backupFile})
 if($fileList.Count-ne2-or@($fileList|Where-Object{$_.Type-eq'D'}).Count-ne1-or@($fileList|Where-Object{$_.Type-eq'L'}).Count-ne1){throw 'Unexpected backup file layout; manual review required.'}
 $dataLogical=($fileList|Where-Object{$_.Type-eq'D'}).LogicalName;$logLogical=($fileList|Where-Object{$_.Type-eq'L'}).LogicalName
 $restore='RESTORE DATABASE '+(Identifier $targetName)+' FROM DISK=@backup WITH MOVE @dataLogical TO @dataFile,MOVE @logLogical TO @logFile,RECOVERY,CHECKSUM'
 Execute $master $restore @{'@backup'=$backupFile;'@dataLogical'=$dataLogical;'@dataFile'=$dataFile;'@logLogical'=$logLogical;'@logFile'=$logFile}
 $manifest.Restored=$true
 $cloneBuilder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($builder.ConnectionString);$cloneBuilder['Initial Catalog']=$targetName
 $clone=[System.Data.SqlClient.SqlConnection]::new($cloneBuilder.ConnectionString);$clone.Open()
 $actual=@(Rows $clone 'SELECT DB_NAME() DatabaseName,CAST(SERVERPROPERTY(''ProductMajorVersion'') AS int) MajorVersion')[0]
 if($actual.DatabaseName-ne$targetName-or$actual.MajorVersion-ne16){throw 'Restored clone identity mismatch.'}
 $manifest.CloneBeforeMigrations=@(Fingerprints $clone $definitions)
 $manifest.CloneMigrations=@(Rows $clone 'SELECT COUNT(*) MigrationCount,MAX(MigrationId) LastMigration FROM __EFMigrationsHistory')[0]
 $manifest.SourceAfter=@(Fingerprints $source $definitions)
 $manifest.SourceUnchanged=(ConvertTo-Json -InputObject $manifest.SourceBefore -Depth 5 -Compress)-ceq(ConvertTo-Json -InputObject $manifest.SourceAfter -Depth 5 -Compress)
 $manifest.CloneMatchesSourceBefore=(ConvertTo-Json -InputObject $manifest.SourceBefore -Depth 5 -Compress)-ceq(ConvertTo-Json -InputObject $manifest.CloneBeforeMigrations -Depth 5 -Compress)
 if(-not $manifest.SourceUnchanged -or -not $manifest.CloneMatchesSourceBefore){throw 'Source or restored clone fingerprint baseline mismatch.'}
 $manifest.ProtectedDirectoryAcl=@((Get-Acl -LiteralPath $protectedDirectory).Access|ForEach-Object{[pscustomobject]@{Identity=$_.IdentityReference.Value;Rights=$_.FileSystemRights.ToString();Type=$_.AccessControlType.ToString();Inherited=$_.IsInherited}})
 $manifest.BackupSha256=(Get-FileHash -LiteralPath $backupFile -Algorithm SHA256).Hash
 $manifest.BackupBytes=(Get-Item -LiteralPath $backupFile).Length
 Write-Output ('Protected clone ready: '+$targetName+'; source unchanged='+$manifest.SourceUnchanged+'; clone matches baseline='+$manifest.CloneMatchesSourceBefore)
}catch{
 $manifest.FailureType=$_.Exception.GetType().Name; $manifest.FailureNumber=$_.Exception.GetBaseException().Number
 Write-Output ('CLONE_OPERATION_FAILED type='+$manifest.FailureType+'; clone identity='+$targetName)
 throw [InvalidOperationException]::new('Protected clone operation failed; inspect sanitized manifest and exact owned resources.')
}finally{
 if($clone){$clone.Dispose()};$master.Dispose();$source.Dispose();$configuration=$null
 $output=[IO.Path]::GetFullPath((Join-Path $repo $ManifestPath))
 if(-not$output.StartsWith($repo+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Manifest outside repository.'}
 [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output))
 $manifest|ConvertTo-Json -Depth 9|Set-Content -LiteralPath $output -Encoding utf8
}
