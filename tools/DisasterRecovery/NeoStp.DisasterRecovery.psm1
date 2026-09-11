Set-StrictMode -Version Latest

function Get-NeoStpAdminConnectionString {
    param([Parameter(Mandatory)][string]$EnvironmentVariable)

    $value = [Environment]::GetEnvironmentVariable($EnvironmentVariable)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "$EnvironmentVariable is required and must contain an administrative SQL Server connection string."
    }

    return $value
}

function Assert-NeoStpSqlIdentifier {
    param(
        [Parameter(Mandatory)][string]$Value,
        [string]$ParameterName = 'Database'
    )

    if ($Value -notmatch '^[A-Za-z][A-Za-z0-9_]{0,127}$') {
        throw "$ParameterName must start with a letter and contain only letters, numbers, or underscores."
    }
}

function ConvertTo-NeoStpSqlIdentifier {
    param([Parameter(Mandatory)][string]$Value)
    return '[' + $Value.Replace(']', ']]') + ']'
}

function ConvertTo-NeoStpSqlLiteral {
    param([Parameter(Mandatory)][string]$Value)
    return "N'" + $Value.Replace("'", "''") + "'"
}

function Open-NeoStpSqlConnection {
    param([Parameter(Mandatory)][string]$ConnectionString)

    Add-Type -AssemblyName System.Data
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($ConnectionString)
    $builder['Initial Catalog'] = 'master'
    $builder['Persist Security Info'] = $false
    $connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
    $connection.Open()
    return $connection
}

function Invoke-NeoStpSqlNonQuery {
    param(
        [Parameter(Mandatory)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory)][string]$Sql,
        [int]$CommandTimeoutSeconds = 0
    )

    $command = $Connection.CreateCommand()
    try {
        $command.CommandText = $Sql
        $command.CommandTimeout = $CommandTimeoutSeconds
        return $command.ExecuteNonQuery()
    }
    finally {
        $command.Dispose()
    }
}

function Invoke-NeoStpSqlTable {
    param(
        [Parameter(Mandatory)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory)][string]$Sql,
        [int]$CommandTimeoutSeconds = 0
    )

    $command = $Connection.CreateCommand()
    $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($command)
    try {
        $command.CommandText = $Sql
        $command.CommandTimeout = $CommandTimeoutSeconds
        $table = [System.Data.DataTable]::new()
        $null = $adapter.Fill($table)
        return ,$table
    }
    finally {
        $adapter.Dispose()
        $command.Dispose()
    }
}

function Resolve-NeoStpAbsolutePath {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Write-NeoStpJsonEvidence {
    param(
        [Parameter(Mandatory)]$Value,
        [Parameter(Mandatory)][string]$Path
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        $null = New-Item -ItemType Directory -Force -Path $directory
    }

    $Value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Path -Encoding utf8
}

Export-ModuleMember -Function @(
    'Get-NeoStpAdminConnectionString',
    'Assert-NeoStpSqlIdentifier',
    'ConvertTo-NeoStpSqlIdentifier',
    'ConvertTo-NeoStpSqlLiteral',
    'Open-NeoStpSqlConnection',
    'Invoke-NeoStpSqlNonQuery',
    'Invoke-NeoStpSqlTable',
    'Resolve-NeoStpAbsolutePath',
    'Write-NeoStpJsonEvidence'
)
