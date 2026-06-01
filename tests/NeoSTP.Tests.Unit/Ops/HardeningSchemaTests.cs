using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Ops;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;
using Xunit;

namespace NeoSTP.Tests.Unit.Ops;

/// <summary>
/// Sprint 20.1 — contrato del esquema de hardening: defaults de las entidades
/// (BackupJob, ApiUsageLog, ApiQuota, AdminIpAllowlistEntry), columnas MFA en
/// Usuario, multi-tenant y permisos de operación sembrados.
/// </summary>
public class HardeningSchemaTests
{
    private const int EmpresaA = 10;
    private const int EmpresaB = 20;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"ops-{Guid.NewGuid()}")
            .Options;
        var db = new NeoStpDbContext(options);
        db.Empresas.Add(new Empresa { Id = EmpresaA, Nit = "0001-A", RazonSocial = "A", EstadoCodigo = "ACTIVA" });
        db.Empresas.Add(new Empresa { Id = EmpresaB, Nit = "0002-B", RazonSocial = "B", EstadoCodigo = "ACTIVA" });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public void BackupJob_New_HasPendienteEstadoManualLocalDefaults()
    {
        var b = new BackupJob();
        b.EstadoCodigo.Should().Be(BackupEstados.Pendiente);
        b.TipoBackup.Should().Be(BackupTipos.Full);
        b.Origen.Should().Be(BackupOrigen.Manual);
        b.StorageProvider.Should().Be(StorageProviders.Local);
        b.FinalizadoAt.Should().BeNull();
    }

    [Fact]
    public void ApiQuota_New_HasGlobalAmbitoSesentaSegundosActivo()
    {
        var q = new ApiQuota();
        q.Ambito.Should().Be(ApiQuotaAmbito.Global);
        q.VentanaSegundos.Should().Be(60);
        q.Activo.Should().BeTrue();
    }

    [Fact]
    public void AdminIpAllowlistEntry_New_IsActivoByDefault()
    {
        new AdminIpAllowlistEntry().Activo.Should().BeTrue();
    }

    [Fact]
    public void Usuario_New_HasMfaDisabledByDefault()
    {
        var u = new Usuario();
        u.MfaHabilitado.Should().BeFalse();
        u.MfaSecretoCifrado.Should().BeNull();
        u.MfaConfirmadoAt.Should().BeNull();
    }

    [Fact]
    public async Task BackupJob_SistemaUsaEmpresaIdNull()
    {
        await using var db = NewDb();
        db.BackupJobs.Add(new BackupJob { EmpresaId = null, TipoBackup = BackupTipos.Full });
        db.BackupJobs.Add(new BackupJob { EmpresaId = EmpresaA, TipoBackup = BackupTipos.Logico });
        await db.SaveChangesAsync();

        (await db.BackupJobs.CountAsync(b => b.EmpresaId == null)).Should().Be(1);
        (await db.BackupJobs.CountAsync(b => b.EmpresaId == EmpresaA)).Should().Be(1);
    }

    [Fact]
    public async Task ApiUsageLog_RespetaScopeEmpresa()
    {
        await using var db = NewDb();
        db.ApiUsageLogs.Add(new ApiUsageLog { EmpresaId = EmpresaA, Metodo = "GET", Ruta = "/a", StatusCode = 200 });
        db.ApiUsageLogs.Add(new ApiUsageLog { EmpresaId = EmpresaB, Metodo = "GET", Ruta = "/b", StatusCode = 200 });
        db.ApiUsageLogs.Add(new ApiUsageLog { EmpresaId = EmpresaA, Metodo = "POST", Ruta = "/a", StatusCode = 429 });
        await db.SaveChangesAsync();

        (await db.ApiUsageLogs.CountAsync(l => l.EmpresaId == EmpresaA)).Should().Be(2);
        (await db.ApiUsageLogs.CountAsync(l => l.StatusCode == 429)).Should().Be(1);
    }

    [Fact]
    public async Task ApiQuota_RoundTrip_ConservaAmbitoYLimite()
    {
        await using var db = NewDb();
        db.ApiQuotas.Add(new ApiQuota
        {
            EmpresaId = EmpresaA,
            Ambito = ApiQuotaAmbito.Modulo,
            AmbitoRef = "NEODTE",
            VentanaSegundos = 60,
            LimitePeticiones = 100,
            Descripcion = "Tope NEODTE",
        });
        await db.SaveChangesAsync();

        var loaded = await db.ApiQuotas.AsNoTracking().SingleAsync();
        loaded.Ambito.Should().Be(ApiQuotaAmbito.Modulo);
        loaded.AmbitoRef.Should().Be("NEODTE");
        loaded.LimitePeticiones.Should().Be(100);
    }

    [Fact]
    public async Task SeedData_DefinePermisosDeHardeningParaSuperAdmin()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"ops-seed-{Guid.NewGuid()}")
            .Options;
        using var db = new NeoStpDbContext(options);
        db.Database.EnsureCreated(); // aplica el seed HasData al store InMemory

        var permisos = await db.Permisos.AsNoTracking()
            .Where(p => p.Codigo == "Ops.Hardening.Ver" || p.Codigo == "Ops.Hardening.Administrar")
            .Select(p => p.Id)
            .ToListAsync();
        permisos.Should().BeEquivalentTo(new[] { 363, 364 });

        var superAdminTiene = await db.RolPermisos.AsNoTracking()
            .Where(rp => rp.RolId == 500 && (rp.PermisoId == 363 || rp.PermisoId == 364))
            .CountAsync();
        superAdminTiene.Should().Be(2, "SUPERADMIN debe tener ambos permisos de hardening");
    }
}
