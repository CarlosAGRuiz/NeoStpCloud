param([int]$EmpresaId=23,[string]$ExpectedNamePrefix='DANIEL IMPORTADORA Y DISTRIBUIDORA',[string]$OutputPath='tmp/client-audit-2026-09-05/configuracion-sanitizada.json')
$ErrorActionPreference='Stop'
$repoPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$config=Get-Content -LiteralPath (Join-Path $repoPath 'src/NeoSTP.Api/appsettings.Local.json') -Raw | ConvertFrom-Json
$connection=[System.Data.SqlClient.SqlConnection]::new($config.ConnectionStrings.NeoStpDb)
function Read-CompanyRows([string]$sql) {
 if($sql -notmatch '^\s*(SELECT|WITH)\b'){throw 'Only fixed SELECT queries permitted.'}
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=15;$cmd.CommandText=$sql
 [void]$cmd.Parameters.AddWithValue('@empresa',$EmpresaId)
 $reader=$cmd.ExecuteReader()
 try { while($reader.Read()) { $row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){ $row[$reader.GetName($i)]=$(if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)}) };[PSCustomObject]$row } }
 finally{$reader.Dispose();$cmd.Dispose()}
}
$queries=[ordered]@{
 Empresa=@'
SELECT Id,RazonSocial,NombreComercial,EstadoCodigo,CodigoActividad,ActividadEconomica,Departamento,Municipio,Distrito,
 CASE WHEN NULLIF(LTRIM(RTRIM(Nit)),'') IS NOT NULL THEN 1 ELSE 0 END NitPresente,
 CASE WHEN NULLIF(LTRIM(RTRIM(Nrc)),'') IS NOT NULL THEN 1 ELSE 0 END NrcPresente,
 CASE WHEN NULLIF(LTRIM(RTRIM(Direccion)),'') IS NOT NULL THEN 1 ELSE 0 END DireccionPresente,
 CASE WHEN NULLIF(LTRIM(RTRIM(Correo)),'') IS NOT NULL THEN 1 ELSE 0 END CorreoContactoPresente,
 CASE WHEN NULLIF(LTRIM(RTRIM(Telefono)),'') IS NOT NULL THEN 1 ELSE 0 END TelefonoPresente,
 CASE WHEN LogoBlob IS NOT NULL OR NULLIF(LogoUrl,'') IS NOT NULL THEN 1 ELSE 0 END LogoPresente
 FROM Core_Empresas WHERE Id=@empresa
'@
 Planes=@'
SELECT ep.Id LicenciaId,ep.PlanId,p.Codigo,p.Nombre,p.PrecioMensual,p.MonedaCodigo,p.LimiteUsuarios,p.LimiteSucursales,p.LimitePuntosVenta,p.LimiteDteMensual,p.Activo CatalogoActivo,ep.EstadoCodigo,ep.FechaInicio,ep.FechaFin,
 CASE WHEN ep.EstadoCodigo='ACTIVO' AND ep.FechaInicio<=GETUTCDATE() AND (ep.FechaFin IS NULL OR ep.FechaFin>GETUTCDATE()) THEN 1 ELSE 0 END VigentePorFecha
 FROM Core_EmpresaPlan ep JOIN Core_Planes p ON p.Id=ep.PlanId WHERE ep.EmpresaId=@empresa ORDER BY ep.FechaInicio DESC
'@
 Modulos=@'
SELECT m.Id,m.Codigo,m.Nombre,m.Activo GlobalActivo,em.Activo AsignacionActiva,em.FechaInactivacion,
 CASE WHEN EXISTS(SELECT 1 FROM Core_PlanModulos pm JOIN Core_EmpresaPlan ep ON ep.PlanId=pm.PlanId WHERE ep.EmpresaId=@empresa AND ep.EstadoCodigo='ACTIVO' AND ep.FechaInicio<=GETUTCDATE() AND (ep.FechaFin IS NULL OR ep.FechaFin>GETUTCDATE()) AND pm.ModuloId=m.Id AND pm.Activo=1) THEN 1 ELSE 0 END IncluidoEnPlanVigente
 FROM Core_Modulos m LEFT JOIN Core_EmpresaModulos em ON em.ModuloId=m.Id AND em.EmpresaId=@empresa ORDER BY m.Orden,m.Codigo
'@
 Usuarios=@'
SELECT u.Id,u.Username,u.TipoUsuarioCodigo,u.EstadoCodigo,u.UltimoLogin,u.IntentosFallidos,u.BloqueadoHasta,u.MfaHabilitado,u.MfaConfirmadoAt,
 CASE WHEN u.EmpresaId=@empresa THEN 1 ELSE 0 END EmpresaPrincipalEsCliente,
 CASE WHEN NULLIF(u.Email,'') IS NOT NULL THEN 1 ELSE 0 END EmailPresente,
 CASE WHEN NULLIF(u.PasswordHash,'') IS NOT NULL THEN 1 ELSE 0 END PasswordHashPresente,
 CASE WHEN NULLIF(u.MfaSecretoCifrado,'') IS NOT NULL THEN 1 ELSE 0 END MfaSecretoPresente,
 CASE WHEN NULLIF(u.SsoProveedor,'') IS NOT NULL THEN 1 ELSE 0 END SsoVinculado
 FROM Core_Usuarios u WHERE u.EmpresaId=@empresa OR EXISTS(SELECT 1 FROM Core_UsuarioEmpresas ue WHERE ue.UsuarioId=u.Id AND ue.EmpresaId=@empresa) ORDER BY u.Id
'@
 Membresias=@'
SELECT ue.UsuarioId,ue.EstadoCodigo,r.Codigo Rol,r.Activo RolActivo,r.EmpresaId EmpresaDelRol FROM Core_UsuarioEmpresas ue LEFT JOIN Core_Roles r ON r.Id=ue.RolId WHERE ue.EmpresaId=@empresa
'@
 RolesUsuario=@'
SELECT ur.UsuarioId,r.Id RolId,r.Codigo,r.EmpresaId EmpresaDelRol,r.EsSistema,r.Activo FROM Core_UsuarioRoles ur JOIN Core_Roles r ON r.Id=ur.RolId WHERE EXISTS(SELECT 1 FROM Core_Usuarios u WHERE u.Id=ur.UsuarioId AND (u.EmpresaId=@empresa OR EXISTS(SELECT 1 FROM Core_UsuarioEmpresas ue WHERE ue.UsuarioId=u.Id AND ue.EmpresaId=@empresa)))
'@
 PermisosUsuario=@'
SELECT DISTINCT ur.UsuarioId,r.Codigo Rol,r.Activo RolActivo,p.Codigo,p.Modulo FROM Core_UsuarioRoles ur JOIN Core_Roles r ON r.Id=ur.RolId JOIN Core_RolPermisos rp ON rp.RolId=r.Id JOIN Core_Permisos p ON p.Id=rp.PermisoId WHERE EXISTS(SELECT 1 FROM Core_Usuarios u WHERE u.Id=ur.UsuarioId AND (u.EmpresaId=@empresa OR EXISTS(SELECT 1 FROM Core_UsuarioEmpresas ue WHERE ue.UsuarioId=u.Id AND ue.EmpresaId=@empresa))) ORDER BY ur.UsuarioId,p.Modulo,p.Codigo
'@
 Correo=@'
SELECT Activo,Host,Puerto,UsarStartTls,CASE WHEN NULLIF(Usuario,'') IS NOT NULL THEN 1 ELSE 0 END UsuarioPresente,CASE WHEN NULLIF(PasswordProtegida,'') IS NOT NULL THEN 1 ELSE 0 END PasswordProtegidaPresente,CASE WHEN NULLIF(FromEmail,'') IS NOT NULL THEN 1 ELSE 0 END RemitentePresente,CASE WHEN NULLIF(FromNombre,'') IS NOT NULL THEN 1 ELSE 0 END NombreRemitentePresente FROM Com_ConfiguracionCorreo WHERE EmpresaId=@empresa
'@
 CuentasCobro=@'
SELECT Id,Tipo,Nombre,Banco,CASE WHEN NULLIF(NumeroCuenta,'') IS NOT NULL THEN 1 ELSE 0 END NumeroCuentaPresente,CASE WHEN NULLIF(Titular,'') IS NOT NULL THEN 1 ELSE 0 END TitularPresente,CASE WHEN NULLIF(UrlPago,'') IS NOT NULL THEN 1 ELSE 0 END UrlPagoPresente,EstadoCodigo FROM Cobros_CuentasCobro WHERE EmpresaId=@empresa
'@
 CuentasTesoreria=@'
SELECT Id,Codigo,Nombre,TipoCuenta,Banco,MonedaCodigo,EstadoCodigo,CASE WHEN NULLIF(NumeroCuenta,'') IS NOT NULL THEN 1 ELSE 0 END NumeroCuentaPresente FROM Tes_Cuentas WHERE EmpresaId=@empresa
'@
 DteConfiguracion=@'
SELECT AmbienteCodigo,TipoEstablecimientoCodigo,CodigoEstablecimientoMh,CodigoPuntoVentaMh,CertificadoEmitido,CertificadoVence,UltimaPruebaAt,UltimaPruebaResultado,
 CASE WHEN NULLIF(UsuarioMh,'') IS NOT NULL THEN 1 ELSE 0 END UsuarioMhPresente,
 CASE WHEN NULLIF(PasswordMhCifrado,'') IS NOT NULL THEN 1 ELSE 0 END PasswordMhProtegidaPresente,
 CASE WHEN CertificadoBlob IS NOT NULL AND DATALENGTH(CertificadoBlob)>0 THEN 1 ELSE 0 END CertificadoPresente,
 CASE WHEN NULLIF(PasswordCertificadoCifrado,'') IS NOT NULL THEN 1 ELSE 0 END PasswordCertificadoProtegidaPresente
 FROM Dte_Configuracion WHERE EmpresaId=@empresa
'@
 Sucursales=@'
SELECT Id,Codigo,Nombre,EstadoCodigo,CodigoEstablecimientoMh,TipoEstablecimientoCodigo,Departamento,Municipio,CASE WHEN NULLIF(Direccion,'') IS NOT NULL THEN 1 ELSE 0 END DireccionPresente FROM Core_Sucursales WHERE EmpresaId=@empresa
'@
 PuntosVenta=@'
SELECT pv.Id,pv.SucursalId,pv.Codigo,pv.Nombre,pv.EstadoCodigo,pv.CodigoPuntoVentaMh FROM Core_PuntosVenta pv JOIN Core_Sucursales s ON s.Id=pv.SucursalId WHERE s.EmpresaId=@empresa
'@
 Suscripciones=@'
SELECT s.Id,s.PlanId,s.Status,s.TrialStart,s.TrialEnd,s.CurrentPeriodStart,s.CurrentPeriodEnd,s.CancelAtPeriodEnd,c.Provider,CASE WHEN NULLIF(s.ExternalSubscriptionId,'') IS NOT NULL THEN 1 ELSE 0 END SuscripcionRemotaPresente FROM Billing_Subscriptions s JOIN Billing_Customers c ON c.Id=s.BillingCustomerId WHERE c.EmpresaId=@empresa
'@
 PagosSuscripcion=@'
SELECT p.Status,p.Metodo,COUNT(*) Cantidad FROM Billing_Payments p JOIN Billing_Subscriptions s ON s.Id=p.BillingSubscriptionId JOIN Billing_Customers c ON c.Id=s.BillingCustomerId WHERE c.EmpresaId=@empresa GROUP BY p.Status,p.Metodo
'@
 Sso=@'
SELECT ProveedorCodigo,Habilitado,AutoProvisionar,CASE WHEN NULLIF(DominioCorreo,'') IS NOT NULL THEN 1 ELSE 0 END DominioPresente,CASE WHEN NULLIF(TenantIdExterno,'') IS NOT NULL THEN 1 ELSE 0 END TenantExternoPresente FROM Core_EmpresaSso WHERE EmpresaId=@empresa
'@
 ApiKeys=@'
SELECT Id,Nombre,Scopes,Activo,ExpiresAt,UltimoUsoAt,RevokedAt FROM Connect_ApiKeys WHERE EmpresaId=@empresa
'@
 CuotasApi=@'
SELECT Ambito,VentanaSegundos,LimitePeticiones,Activo FROM Core_ApiQuotas WHERE EmpresaId=@empresa
'@
 VersionEsquema=@'
SELECT COUNT(*) Migraciones,MAX(MigrationId) UltimaMigracion FROM __EFMigrationsHistory
'@
 EsquemaAdicional=@'
SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME IN ('Dte_Documentos','Dte_Clientes','Dte_Productos') OR TABLE_NAME LIKE 'Cobros_%' OR TABLE_NAME LIKE '%AuthSession%' OR TABLE_NAME LIKE '%PaymentApplication%' ORDER BY TABLE_NAME,ORDINAL_POSITION
'@
}
try{
 $connection.Open()
 $identity=@(Read-CompanyRows 'SELECT RazonSocial FROM Core_Empresas WHERE Id=@empresa')
 if($identity.Count -ne 1 -or -not $identity[0].RazonSocial.StartsWith($ExpectedNamePrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Identity mismatch.'}
 $report=[ordered]@{GeneratedAtUtc=[DateTime]::UtcNow.ToString('O');EmpresaId=$EmpresaId;Source='Base configurada en API appsettings.Local.json; SELECT únicamente; no certifica despliegue remoto';Secrets='Solo indicadores de presencia; sin descifrar, autenticar, enviar, firmar ni modificar'}
 foreach($key in $queries.Keys){$report[$key]=@(Read-CompanyRows $queries[$key])}
 $target=[IO.Path]::GetFullPath((Join-Path $repoPath $OutputPath))
 if(-not $target.StartsWith($repoPath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output outside workspace.'}
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
 $report|ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $target -Encoding utf8
 'Audit saved: '+$OutputPath
}catch{Write-Output ('READ_ONLY_AUDIT_FAILED section='+$key+' type='+$_.Exception.GetType().Name);exit 1}
finally{$connection.Dispose();$config=$null}
