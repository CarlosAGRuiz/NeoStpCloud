$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$old=Get-Content -LiteralPath (Join-Path $repo 'tmp/neo-production/logs/attempt2/clone-manifest.json') -Raw|ConvertFrom-Json
$current=Get-Content -LiteralPath (Join-Path $repo 'tmp/neo-production/clone-manifest.json') -Raw|ConvertFrom-Json
$result=Get-Content -LiteralPath (Join-Path $repo 'tmp/neo-production/rehearsal-results.json') -Raw|ConvertFrom-Json
if($result.Failed-ne0-or$result.Passed-ne22-or$result.TargetDatabase-ne$current.TargetDatabase){throw 'Successful final rehearsal required before owned cleanup.'}
$config=Get-Content -LiteralPath (Join-Path $repo 'src/NeoSTP.Api/appsettings.Local.json') -Raw|ConvertFrom-Json
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($config.ConnectionStrings.NeoStpDb)
if($builder.InitialCatalog-ne'NeoSTP_Cloud'){throw 'Source identity changed.'}
$builder['Initial Catalog']='master'
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function Rows([string]$sql,$parameters=@{}){
 $command=$connection.CreateCommand();$command.CommandTimeout=60;$command.CommandText=$sql
 foreach($key in $parameters.Keys){[void]$command.Parameters.AddWithValue($key,$parameters[$key])}
 try{$reader=$command.ExecuteReader();try{while($reader.Read()){$row=[ordered]@{};for($i=0;$i-lt$reader.FieldCount;$i++){$row[$reader.GetName($i)]=$reader.GetValue($i)};[pscustomobject]$row}}finally{$reader.Dispose()}}finally{$command.Dispose()}
}
$evidence=[ordered]@{GeneratedAtUtc=[DateTime]::UtcNow.ToString('O');SourceDatabase='NeoSTP_Cloud';SourceWritesIssued=$false;Clones=@();BackupRetained=$false;AllClonesAbsent=$false}
try{
 $connection.Open()
 $major=@(Rows "SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int) MajorVersion")[0].MajorVersion
 if($major-ne16){throw 'Wrong server version.'}
 $evidence.SourceEngineMajor=$major
 foreach($manifest in @($old,$current)){
  $target=$manifest.TargetDatabase
  if($target-notmatch'^NeoProductionAudit_[a-f0-9]{32}$'-or$target-ne('NeoProductionAudit_'+$manifest.RunId)-or$manifest.SourceDatabase-ne'NeoSTP_Cloud'){throw 'Owned clone name mismatch.'}
  $expectedRoot=Join-Path 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup' $target
  if([IO.Path]::GetFullPath($manifest.ProtectedDirectory)-ne$expectedRoot-or[IO.Path]::GetFullPath($manifest.DataFile)-ne(Join-Path $expectedRoot ($target+'.mdf'))-or[IO.Path]::GetFullPath($manifest.LogFile)-ne(Join-Path $expectedRoot ($target+'.ldf'))){throw 'Owned clone path mismatch.'}
  $files=@(Rows 'SELECT physical_name FROM sys.master_files WHERE database_id=DB_ID(@target)' @{'@target'=$target})
  if($files.Count-ne2-or$files.physical_name-notcontains$manifest.DataFile-or$files.physical_name-notcontains$manifest.LogFile){throw 'Live database physical identity mismatch.'}
  $acl=@((Get-Acl -LiteralPath $expectedRoot).Access|ForEach-Object{[pscustomobject]@{Identity=$_.IdentityReference.Value;Rights=$_.FileSystemRights.ToString();Type=$_.AccessControlType.ToString();Inherited=$_.IsInherited}})
  $command=$connection.CreateCommand();$command.CommandTimeout=60;$command.CommandText='DROP DATABASE ['+$target+']'
  try{[void]$command.ExecuteNonQuery()}finally{$command.Dispose()}
  $absent=@(Rows 'SELECT name FROM sys.databases WHERE name=@target' @{'@target'=$target}).Count-eq0
  $filesAbsent=(-not(Test-Path -LiteralPath $manifest.DataFile))-and(-not(Test-Path -LiteralPath $manifest.LogFile))
  $evidence.Clones+=@{TargetDatabase=$target;IdentityAndPathsVerified=$true;DatabaseAbsent=$absent;DataAndLogAbsent=$filesAbsent;ProtectedDirectoryAcl=$acl}
  if(-not$absent-or-not$filesAbsent){throw 'Owned clone cleanup did not verify.'}
  Write-Output ('Exact owned clone removed: '+$target)
 }
 $backupHash=(Get-FileHash -LiteralPath $old.BackupFile -Algorithm SHA256).Hash
 if($backupHash-ne$old.BackupSha256){throw 'Retained backup digest mismatch.'}
 $evidence.BackupRetained=$true;$evidence.BackupSha256=$backupHash;$evidence.BackupBytes=(Get-Item -LiteralPath $old.BackupFile).Length
 $evidence.SourceMigrationCount=@(Rows 'SELECT COUNT(*) MigrationCount FROM [NeoSTP_Cloud].[dbo].[__EFMigrationsHistory]')[0].MigrationCount
 $evidence.AllClonesAbsent=$true
 if($evidence.SourceMigrationCount-ne79){throw 'Source migration baseline changed.'}
 Write-Output 'Both owned clones absent; protected verified backup retained; source migrations=79.'
}finally{
 $connection.Dispose();$config=$null
 $evidence|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $repo 'tmp/neo-production/cleanup-results.json') -Encoding utf8
}