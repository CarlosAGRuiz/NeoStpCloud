[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d{8}T\d{6}Z-[a-f0-9]{32}$')][string]$CandidateId,
    [Parameter(Mandatory)][ValidatePattern('^[A-F0-9]{64}$')][string]$ExpectedManifestSha256,
    [ValidatePattern('^\d{8}T\d{6}Z-[a-f0-9]{32}$')][string]$SourceReleaseId,
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'

# Reviewed candidate identity is explicit. Preparation starts no hosts and issues no SQL.
$expectedManifestHash = $ExpectedManifestSha256
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$candidate = Join-Path $repo ('tmp/production-candidate/' + $candidateId)
$destinationParent = Join-Path $repo 'out/client-certification-release'
$destination = Join-Path $destinationParent $candidateId

function Assert-NoReparse([string]$path) {
    $cursor = [IO.Path]::GetFullPath($path)
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'STAGING_PATH_REJECTED'
            }
        }
        $parent = [IO.Path]::GetDirectoryName($cursor)
        if ($parent -eq $cursor) { break }
        $cursor = $parent
    }
}

function Resolve-Child([string]$root, [string]$relative) {
    if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[/\\])\.\.([/\\]|$)') {
        throw 'STAGING_PATH_REJECTED'
    }
    $full = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $full.StartsWith($root.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'STAGING_PATH_REJECTED'
    }
    Assert-NoReparse $full
    return $full
}

function New-PrivateDirectory([string]$path, $sid) {
    Assert-NoReparse $path
    [void][IO.Directory]::CreateDirectory($path)
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetOwner($sid)
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($allowedSid in @($sid, [Security.Principal.SecurityIdentifier]::new('S-1-5-18'), [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new($allowedSid,
            [Security.AccessControl.FileSystemRights]::FullControl,
            [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit',
            [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Allow)
        [void]$acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $path -AclObject $acl
    $actual = Get-Acl -LiteralPath $path
    $allowed = @($sid.Value, 'S-1-5-18', 'S-1-5-32-544')
    if (-not $actual.AreAccessRulesProtected) { throw 'STAGING_ACL_REJECTED' }
    $rules = @($actual.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier]))
    if ($rules.Count -ne 3 -or @($rules | Where-Object {
        $_.IsInherited -or $_.IdentityReference.Value -notin $allowed -or $_.AccessControlType -ne 'Allow'
    }).Count -gt 0) { throw 'STAGING_ACL_REJECTED' }
}

function Get-ConfigValue($node, [string]$key) {
    foreach ($part in $key.Split(':')) {
        if ($null -eq $node -or $null -eq $node.PSObject.Properties[$part]) { return $null }
        $node = $node.$part
    }
    return $node
}

function Set-ConfigValue($node, [string]$key, $value) {
    $parts = $key.Split(':')
    for ($index = 0; $index -lt $parts.Length - 1; $index++) {
        $name = $parts[$index]
        if ($null -eq $node.PSObject.Properties[$name]) { $node | Add-Member -NotePropertyName $name -NotePropertyValue ([pscustomobject]@{}) }
        if ($node.$name -isnot [pscustomobject]) { throw 'STAGING_CONFIGURATION_REJECTED' }
        $node = $node.$name
    }
    $name = $parts[-1]
    if ($null -eq $node.PSObject.Properties[$name]) { $node | Add-Member -NotePropertyName $name -NotePropertyValue $value }
    else { $node.$name = $value }
}

try {
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'STAGING_WINDOWS_REQUIRED' }
    Assert-NoReparse $candidate
    Assert-NoReparse $destination
    if (Test-Path -LiteralPath $destination) { throw 'STAGING_DESTINATION_EXISTS' }
    $manifestPath = Join-Path $candidate 'candidate-manifest.json'
    if ((Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash -cne $expectedManifestHash) { throw 'STAGING_MANIFEST_MISMATCH' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.CandidateId -cne $candidateId -or $manifest.PublicationComplete -ne $true -or $manifest.EnvironmentConfigurationIncluded -ne $false) {
        throw 'STAGING_CANDIDATE_REJECTED'
    }
    # Verify all candidate roots, including the worker that is deliberately NOT staged.
    foreach ($root in @('web','api','worker')) {
        $rootPath = Resolve-Child $candidate $root
        $entries = @($manifest.Files | Where-Object { $_.Root -ceq $root })
        if ($entries.Count -eq 0 -or @($entries.Path | Sort-Object -Unique).Count -ne $entries.Count) { throw 'STAGING_MANIFEST_REJECTED' }
        $actual = @(Get-ChildItem -LiteralPath $rootPath -Recurse -File -Force)
        if ($actual.Count -ne $entries.Count) { throw 'STAGING_CANDIDATE_FILESET_MISMATCH' }
        foreach ($entry in $entries) {
            $file = Resolve-Child $rootPath $entry.Path
            if (-not (Test-Path -LiteralPath $file -PathType Leaf) -or (Get-Item -LiteralPath $file).Length -ne $entry.Bytes -or
                (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -cne $entry.Sha256) { throw 'STAGING_CANDIDATE_HASH_MISMATCH' }
        }
    }
    $sourceFiles = @('appsettings.json','appsettings.Development.json','appsettings.Local.json')
    $sourceNodes = @{}
    $sourceRoots = @{}
    foreach ($app in @('Web','Api')) {
        $root = if ($SourceReleaseId) { Join-Path $destinationParent ($SourceReleaseId + '/' + $app.ToLowerInvariant()) } else { Join-Path $repo ('src/NeoSTP.' + $app) }
        Assert-NoReparse $root
        $sourceRoots[$app] = $root
        $nodes = @()
        foreach ($name in $sourceFiles) {
            $file = Resolve-Child $root $name
            if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw 'STAGING_SOURCE_CONFIGURATION_MISSING' }
            $node = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
            if ($node -isnot [pscustomobject]) { throw 'STAGING_CONFIGURATION_REJECTED' }
            $nodes += $node
        }
        $sourceNodes[$app] = $nodes
    }
    if ($ValidateOnly) { Write-Output 'STAGING_PREFLIGHT_PASSED: candidate hashes and source configuration parsed; no files copied or runtime changed.'; return }

    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
    New-PrivateDirectory $destination $sid
    $report = [ordered]@{
        SchemaVersion = 1; CandidateId = $candidateId; CandidateManifestSha256 = $expectedManifestHash
        PreparedAtUtc = [DateTime]::UtcNow.ToString('O'); Environment = 'Development'; TemporaryCertificationRelease = $true
        ReadyForProduction = $false; HostsStarted = $false; TasksChanged = $false; DatabaseAccessed = $false; WorkerStaged = $false
        PrivateDirectoryAclVerifiedBeforeConfigurationCopy = $true; EffectiveProcessEnvironmentVerified = $false
        RequiresSameWindowsIdentityAndExistingDataProtectionKeys = $true; SourceConfigurationPreserved = $true
        RequiredStartArguments = '--environment Development --Ops:Database:ApplyMigrationsOnStartup false --Ops:Database:SeedOnStartup false --SuperAdmin:BootstrapEnabled false --EmpresaPrueba:Enabled false --DemoComercial:Enabled false'
        Roots = @()
    }
    foreach ($app in @('Web','Api')) {
        $rootName = $app.ToLowerInvariant()
        $appRoot = Resolve-Child $destination $rootName
        New-PrivateDirectory $appRoot $sid
        $entries = @($manifest.Files | Where-Object { $_.Root -ceq $rootName })
        foreach ($entry in $entries) {
            $from = Resolve-Child (Join-Path $candidate $rootName) $entry.Path
            $to = Resolve-Child $appRoot $entry.Path
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to))
            [IO.File]::Copy($from, $to, $false)
            if ((Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash -cne $entry.Sha256) { throw 'STAGING_COPY_HASH_MISMATCH' }
        }
        $sourceRoot = $sourceRoots[$app]
        foreach ($name in $sourceFiles) { [IO.File]::Copy((Resolve-Child $sourceRoot $name), (Resolve-Child $appRoot $name), $false) }
        $nodes = $sourceNodes[$app]
        $local = $nodes[2]
        foreach ($key in @('Ops:Database:ApplyMigrationsOnStartup','Ops:Database:SeedOnStartup','SuperAdmin:BootstrapEnabled','EmpresaPrueba:Enabled','DemoComercial:Enabled')) {
            Set-ConfigValue $local $key $false
        }
        # One owner for the authorized month-end billing scheduler: API only.
        # This does not enable checkout, automatic payment capture or fiscal workers.
        Set-ConfigValue $local 'Billing:Calendar:Enabled' ($app -ceq 'Api')
        # Operational schema rollout for the verified client in the test environment.
        # Global selection and other tenants are preserved; no fiscal credentials are changed.
        Set-ConfigValue $local 'Dte:TenantSchemas:23:Nit' '06232705261148'
        Set-ConfigValue $local 'Dte:TenantSchemas:23:Ambiente' 'PRUEBAS'
        Set-ConfigValue $local 'Dte:TenantSchemas:23:Profile' 'MH_20260825'
        # Existing source hosts run bin/Debug/net10.0. Storage services resolve relative paths
        # against AppContext.BaseDirectory, not ContentRootPath; preserve that actual base.
        $sourceRuntimeBase = if ($SourceReleaseId) { $sourceRoot } else { Join-Path $sourceRoot 'bin/Debug/net10.0' }
        $scanProvider = $null; $scanRoot = $null; $backupPath = 'backups'
        foreach ($node in $nodes) {
            $v = Get-ConfigValue $node 'Scan:Storage:Provider'; if ($null -ne $v) { $scanProvider = $v }
            $v = Get-ConfigValue $node 'Scan:Storage:Root'; if ($null -ne $v) { $scanRoot = $v }
            $v = Get-ConfigValue $node 'Hardening:Backup:LocalPath'; if ($null -ne $v) { $backupPath = $v }
        }
        $scanAdjusted = $false; $backupAdjusted = $false
        if ($scanProvider -ieq 'FileSystem') {
            if ($null -eq $scanRoot) { $scanRoot = 'scan-blobs' }
            if ([string]::IsNullOrWhiteSpace($scanRoot)) { throw 'STAGING_STORAGE_REJECTED' }
            if (-not [IO.Path]::IsPathRooted($scanRoot)) {
                $scanRoot = [IO.Path]::GetFullPath((Join-Path $sourceRuntimeBase $scanRoot)); Assert-NoReparse $scanRoot
                Set-ConfigValue $local 'Scan:Storage:Root' $scanRoot; $scanAdjusted = $true
            }
        }
        if ([string]::IsNullOrWhiteSpace($backupPath)) { throw 'STAGING_STORAGE_REJECTED' }
        if (-not [IO.Path]::IsPathRooted($backupPath)) {
            $backupPath = [IO.Path]::GetFullPath((Join-Path $sourceRuntimeBase $backupPath)); Assert-NoReparse $backupPath
            Set-ConfigValue $local 'Hardening:Backup:LocalPath' $backupPath; $backupAdjusted = $true
        }
        # Private configuration never enters tmp/evidence, stdout or the sanitized manifest.
        [IO.File]::WriteAllText((Join-Path $appRoot 'appsettings.Local.json'), ($local | ConvertTo-Json -Depth 100), [Text.UTF8Encoding]::new($false))
        $report.Roots += @{ Name = $rootName; RuntimeFilesVerified = $entries.Count; SourceConfigFiles = 3
            ScanStorageAbsolutized = $scanAdjusted; BackupPathAbsolutized = $backupAdjusted; DataProtectionUnchanged = $true
            DteSchemaVersionFlagUnchanged = $true; TenantSchemaPolicy = '23/PRUEBAS/MH_20260825'; WorkerEnabledByThisTool = $false
            CalendarBillingEnabled = ($app -ceq 'Api') }
    }
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $destination 'staging-manifest.json') -Encoding utf8
    Write-Output ('STAGING_PREPARED_ONLY: ' + $destination)
}
catch {
    # Do not relay exception messages that may contain configuration fragments or private paths.
    throw [InvalidOperationException]::new('CLIENT_RELEASE_STAGING_FAILED: preparation stopped. No hosts, tasks or SQL operations were requested. A partial protected destination, if created, must be reviewed before retrying.')
}
