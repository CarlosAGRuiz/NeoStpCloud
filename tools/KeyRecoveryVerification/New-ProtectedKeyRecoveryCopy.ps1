$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourcePath=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'ASP.NET/DataProtection-Keys'))
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if($identity.Name -ne 'AzureAD\CarlosAntonioGarciaR'){throw 'Unexpected recovery identity.'}
$previous=Get-Content -LiteralPath (Join-Path $repo 'tmp/neo-production/key-recovery-existing.json') -Raw|ConvertFrom-Json
if(-not $previous.Passed -or -not $previous.OriginalRingUnchanged -or -not $previous.AllStoredKeyElementsEncrypted -or $previous.KeyWriteAttempts -ne 0 -or $previous.KeyDirectory -ne $sourcePath){throw 'Successful unchanged encrypted-ring verification required.'}
$manifest=Join-Path $repo 'tmp/neo-production/key-recovery-copy.json'
if(Test-Path -LiteralPath $manifest){throw 'Recovery copy manifest already exists; inspect and reuse the exact owned copy.'}
$runId=[Guid]::NewGuid().ToString('N')
$programData=[IO.Path]::GetFullPath([Environment]::GetFolderPath('CommonApplicationData'))
$targetPath=[IO.Path]::GetFullPath((Join-Path $programData ('NeoSTP-KeyRecovery-'+$runId)))
if([IO.Path]::GetDirectoryName($targetPath) -ne $programData -or [IO.Path]::GetFileName($targetPath) -notmatch '^NeoSTP-KeyRecovery-[a-f0-9]{32}$' -or (Test-Path -LiteralPath $targetPath)){throw 'Unexpected recovery output identity.'}
$allowedSids=@($identity.User.Value,'S-1-5-18','S-1-5-32-544')
$result=[ordered]@{GeneratedAtUtc=[DateTime]::UtcNow.ToString('O');RunId=$runId;SourceDirectory=$sourcePath;ProtectedDirectory=$targetPath;RawKeyMaterialExportedToEvidence=$false;OriginalRingWritten=$false;CopyComplete=$false;AclRestricted=$false;SourceUnchanged=$false;CopyMatchesSource=$false;EncryptedFilesOnly=$false}
function AssertDirectory([string]$path){
 $directory=[IO.DirectoryInfo]::new($path)
 while($directory){if($directory.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Recovery path cannot contain a reparse point.'};$directory=$directory.Parent}
}
function Fingerprints([string]$path){
 $values=@{}
 foreach($file in Get-ChildItem -LiteralPath $path -File -Filter '*.xml'){
  if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Recovery file cannot be a reparse point.'}
  $values[$file.Name]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
 }
 return $values
}
function EqualHashes($left,$right){
 if($left.Count -ne $right.Count){return $false}
 foreach($key in $left.Keys){if(-not $right.ContainsKey($key) -or $left[$key] -cne $right[$key]){return $false}}
 return $true
}
function AssertAcl([string]$path,[bool]$mustBeProtected){
 $acl=Get-Acl -LiteralPath $path
 if($mustBeProtected -and -not $acl.AreAccessRulesProtected){throw 'Recovery ACL inheritance must be disabled.'}
 $rules=@($acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]))
 if($rules.Count -ne 3){throw 'Unexpected recovery ACL rule count.'}
 foreach($rule in $rules){if($rule.IdentityReference.Value -notin $allowedSids -or $rule.AccessControlType -ne 'Allow' -or $rule.FileSystemRights -ne [Security.AccessControl.FileSystemRights]::FullControl){throw 'Unexpected recovery ACL grant.'}}
}
try{
 AssertDirectory $sourcePath
 AssertDirectory $programData
 $before=Fingerprints $sourcePath
 if($before.Count -ne $previous.KeyFileCount -or $before.Count -eq 0){throw 'Source key inventory changed.'}
 foreach($file in Get-ChildItem -LiteralPath $sourcePath -File -Filter '*.xml'){
  $settings=[Xml.XmlReaderSettings]::new();$settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit
  $reader=[Xml.XmlReader]::Create($file.FullName,$settings)
  $isKey=$false;$isEncrypted=$false
  try{while($reader.Read()){if($reader.NodeType -eq 'Element' -and $reader.LocalName -eq 'key'){$isKey=$true};if($reader.NodeType -eq 'Element' -and $reader.LocalName -eq 'encryptedSecret'){$isEncrypted=$true}}}finally{$reader.Dispose()}
  if(-not $isKey -or -not $isEncrypted){throw 'Only encrypted key XML is approved for this copy.'}
 }
 $result.EncryptedFilesOnly=$true
 [void][IO.Directory]::CreateDirectory($targetPath)
 $security=[Security.AccessControl.DirectorySecurity]::new()
 $security.SetAccessRuleProtection($true,$false)
 $security.SetOwner($identity.User)
 foreach($sid in $allowedSids){
  $rule=[Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),[Security.AccessControl.FileSystemRights]::FullControl,[Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit',[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow)
  [void]$security.AddAccessRule($rule)
 }
 Set-Acl -LiteralPath $targetPath -AclObject $security
 AssertDirectory $targetPath
 AssertAcl $targetPath $true
 $result.AclRestricted=$true
 foreach($file in Get-ChildItem -LiteralPath $sourcePath -File -Filter '*.xml'){
  Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $targetPath $file.Name)
  AssertAcl (Join-Path $targetPath $file.Name) $false
 }
 $after=Fingerprints $sourcePath
 $copy=Fingerprints $targetPath
 $result.SourceUnchanged=EqualHashes $before $after
 $result.CopyMatchesSource=EqualHashes $before $copy
 $result.KeyFileCount=$copy.Count
 if(-not $result.SourceUnchanged -or -not $result.CopyMatchesSource){throw 'Recovery copy hash comparison failed.'}
 $result.CopyComplete=$true
 $result.AclPrincipals=$allowedSids
}catch{
 $result.FailureType=$_.Exception.GetType().Name
 throw [InvalidOperationException]::new('Protected key recovery copy failed; inspect sanitized metadata only.')
}finally{
 $result|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $manifest -Encoding utf8
 $result|ConvertTo-Json -Depth 4|Write-Output
}
