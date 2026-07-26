using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Dashboard;

/// <summary>
/// Consolidado de grupo (E5): el alcance sale de la empresa principal + membresías (E1),
/// y las métricas se agregan por empresa. InMemory + datos mínimos por caso.
/// </summary>
public class GrupoDashboardServiceTests
{
    private const int Contador = 7;   // usuario con varias empresas
    private const int Ajeno = 8;      // usuario de otra empresa
    private const int EmpA = 1;
    private const int EmpB = 2;
    private const int EmpFuera = 3;   // empresa a la que nadie del grupo pertenece
    private const int RolContador = 503;

    private static NeoStpDbContext NewDb()
    {
        var db = new NeoStpDbContext(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"grupo-{Guid.NewGuid()}").Options);

        db.Empresas.AddRange(
            new Empresa { Id = EmpA, Nit = "E1", RazonSocial = "Alfa S.A.", EstadoCodigo = EmpresaEstados.Activa },
            new Empresa { Id = EmpB, Nit = "E2", RazonSocial = "Beta S.A.", NombreComercial = "Beta", EstadoCodigo = EmpresaEstados.Activa },
            new Empresa { Id = EmpFuera, Nit = "E3", RazonSocial = "Gamma S.A.", EstadoCodigo = EmpresaEstados.Activa });
        db.Roles.Add(new Rol { Id = RolContador, Codigo = "CONTADOR", Nombre = "Contador" });
        db.Usuarios.AddRange(
            new Usuario
            {
                Id = Contador, EmpresaId = EmpA, Username = "conta", Email = "conta@x.com",
                NombreCompleto = "Contador", PasswordHash = "x", EstadoCodigo = EstadoCodes.Activo,
            },
            new Usuario
            {
                Id = Ajeno, EmpresaId = EmpFuera, Username = "ajeno", Email = "ajeno@x.com",
                NombreCompleto = "Ajeno", PasswordHash = "x", EstadoCodigo = EstadoCodes.Activo,
            });
        // El contador es miembro externo de Beta (E1).
        db.UsuarioEmpresas.Add(new UsuarioEmpresa
        {
            UsuarioId = Contador, EmpresaId = EmpB, RolId = RolContador, EstadoCodigo = "ACTIVO",
        });
        db.SaveChanges();
        return db;
    }

    private static DteDocumento Doc(
        int empresaId, string estado, decimal total, decimal iva = 0m,
        DateTime? fecha = null, string tipo = TipoDteCodigos.FacturaConsumidorFinal,
        string condicion = "1", int? plazo = null) => new()
        {
            EmpresaId = empresaId,
            TipoDteCodigo = tipo,
            NumeroControl = $"DTE-{Guid.NewGuid():N}",
            CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            EstadoCodigo = estado,
            FechaEmision = (fecha ?? DateTime.UtcNow).Date,
            CondicionOperacionCodigo = condicion,
            PlazoDias = plazo,
            TotalPagar = total,
            IvaTotal = iva,
        };

    [Fact]
    public async Task Alcance_IncluyePrincipalYMembresias_YExcluyeAjenas()
    {
        await using var db = NewDb();
        var svc = new GrupoDashboardService(db);

        var r = await svc.GetAsync(Contador);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Empresas.Should().HaveCount(2);
        r.Value.Empresas.Select(e => e.EmpresaId).Should().BeEquivalentTo([EmpA, EmpB]);
        r.Value.Empresas.Single(e => e.EmpresaId == EmpA).EsPrincipal.Should().BeTrue();
        r.Value.Empresas.Single(e => e.EmpresaId == EmpB).RolNombre.Should().Be("Contador");
        // Usa el nombre comercial cuando existe.
        r.Value.Empresas.Single(e => e.EmpresaId == EmpB).Nombre.Should().Be("Beta");
    }

    [Fact]
    public async Task UsuarioSinMembresias_SoloVeSuEmpresa()
    {
        await using var db = NewDb();
        var svc = new GrupoDashboardService(db);

        var r = await svc.GetAsync(Ajeno);

        r.Value!.Empresas.Should().ContainSingle().Which.EmpresaId.Should().Be(EmpFuera);
    }

    [Fact]
    public async Task VentasEIva_SoloProcesadosDelPeriodo_YTotalizaGrupo()
    {
        await using var db = NewDb();
        db.DteDocumentos.AddRange(
            Doc(EmpA, DteEstadoCodigos.Procesado, 113m, 13m),
            Doc(EmpA, DteEstadoCodigos.Rechazado, 500m, 50m),          // no suma a ventas
            Doc(EmpA, DteEstadoCodigos.Borrador, 200m, 20m),           // pendiente, no suma
            Doc(EmpB, DteEstadoCodigos.Procesado, 226m, 26m),
            Doc(EmpFuera, DteEstadoCodigos.Procesado, 999m, 99m));     // fuera del alcance
        await db.SaveChangesAsync();
        var svc = new GrupoDashboardService(db);

        var r = await svc.GetAsync(Contador);

        var alfa = r.Value!.Empresas.Single(e => e.EmpresaId == EmpA);
        alfa.DteMes.Should().Be(3);
        alfa.VentasMes.Should().Be(113m);
        alfa.IvaDebitoMes.Should().Be(13m);
        alfa.Rechazados.Should().Be(1);
        alfa.Pendientes.Should().Be(1);

        r.Value.VentasMes.Should().Be(339m);      // 113 + 226, sin Gamma
        r.Value.IvaDebitoMes.Should().Be(39m);
        r.Value.DteMes.Should().Be(4);
    }

    [Fact]
    public async Task DocumentosDeOtroMes_NoCuentanEnElPeriodo()
    {
        await using var db = NewDb();
        var mesPasado = DateTime.UtcNow.Date.AddMonths(-1);
        db.DteDocumentos.Add(Doc(EmpA, DteEstadoCodigos.Procesado, 100m, 11m, mesPasado));
        await db.SaveChangesAsync();
        var svc = new GrupoDashboardService(db);

        var actual = await svc.GetAsync(Contador);
        var anterior = await svc.GetAsync(Contador, mesPasado.Year, mesPasado.Month);

        actual.Value!.VentasMes.Should().Be(0m);
        anterior.Value!.VentasMes.Should().Be(100m);
        anterior.Value.Anio.Should().Be(mesPasado.Year);
        anterior.Value.Mes.Should().Be(mesPasado.Month);
    }

    [Fact]
    public async Task Cartera_SumaSaldoPendienteYMarcaVencido()
    {
        await using var db = NewDb();
        var hace10 = DateTime.UtcNow.Date.AddDays(-10);
        // Crédito a 5 días emitido hace 10 → vencida. Total 100, pagado 40 → saldo 60.
        var vencida = Doc(EmpA, DteEstadoCodigos.Procesado, 100m, 0m, hace10,
            TipoDteCodigos.ComprobanteCreditoFiscal, condicion: "2", plazo: 5);
        // Crédito a 60 días → aún vigente, saldo 200.
        var vigente = Doc(EmpA, DteEstadoCodigos.Procesado, 200m, 0m, hace10,
            TipoDteCodigos.ComprobanteCreditoFiscal, condicion: "2", plazo: 60);
        // Contado → no genera cartera.
        var contado = Doc(EmpA, DteEstadoCodigos.Procesado, 300m, 0m, hace10, condicion: "1");
        db.DteDocumentos.AddRange(vencida, vigente, contado);
        await db.SaveChangesAsync();

        db.Set<PagoCliente>().Add(new PagoCliente
        {
            EmpresaId = EmpA, DteDocumentoId = vencida.Id, Monto = 40m,
            EstadoCodigo = PagoEstados.Confirmado, Fecha = DateOnly.FromDateTime(hace10),
        });
        await db.SaveChangesAsync();
        var svc = new GrupoDashboardService(db);

        var r = await svc.GetAsync(Contador);

        var alfa = r.Value!.Empresas.Single(e => e.EmpresaId == EmpA);
        alfa.PorCobrar.Should().Be(260m);   // 60 + 200
        alfa.Vencido.Should().Be(60m);
        alfa.FacturasVencidas.Should().Be(1);
        alfa.RequiereAtencion.Should().BeTrue();
    }

    [Fact]
    public async Task Alertas_CuentaSoloLasNoResueltas()
    {
        await using var db = NewDb();
        db.Alertas.AddRange(
            new Alerta { EmpresaId = EmpA, Clave = "k1", TipoCodigo = AlertaTipos.DteRechazado, Severidad = AlertaSeveridades.Critica, Titulo = "t", Mensaje = "m", EstadoCodigo = AlertaEstados.Pendiente },
            new Alerta { EmpresaId = EmpA, Clave = "k2", TipoCodigo = AlertaTipos.StockBajo, Severidad = AlertaSeveridades.Advertencia, Titulo = "t", Mensaje = "m", EstadoCodigo = AlertaEstados.Leida },
            new Alerta { EmpresaId = EmpA, Clave = "k3", TipoCodigo = AlertaTipos.StockBajo, Severidad = AlertaSeveridades.Advertencia, Titulo = "t", Mensaje = "m", EstadoCodigo = AlertaEstados.Resuelta });
        await db.SaveChangesAsync();
        var svc = new GrupoDashboardService(db);

        var r = await svc.GetAsync(Contador);

        r.Value!.Empresas.Single(e => e.EmpresaId == EmpA).AlertasActivas.Should().Be(2);
        r.Value.AlertasActivas.Should().Be(2);
    }

    [Fact]
    public async Task EmpresaSuspendida_SeMarcaYCuentaComoPorAtender()
    {
        await using var db = NewDb();
        db.Empresas.Single(e => e.Id == EmpB).EstadoCodigo = EmpresaEstados.Suspendida;
        await db.SaveChangesAsync();
        var svc = new GrupoDashboardService(db);

        var r = await svc.GetAsync(Contador);

        var beta = r.Value!.Empresas.Single(e => e.EmpresaId == EmpB);
        beta.Activa.Should().BeFalse();
        beta.RequiereAtencion.Should().BeTrue();
        r.Value.EmpresasSuspendidas.Should().Be(1);
        r.Value.EmpresasConPendientes.Should().Be(1);
    }

    [Fact]
    public async Task MembresiaInactiva_NoEntraAlAlcance()
    {
        await using var db = NewDb();
        db.UsuarioEmpresas.Single().EstadoCodigo = "INACTIVO";
        await db.SaveChangesAsync();
        var svc = new GrupoDashboardService(db);

        var r = await svc.GetAsync(Contador);

        r.Value!.Empresas.Should().ContainSingle().Which.EmpresaId.Should().Be(EmpA);
    }

    [Fact]
    public async Task PeriodoInvalido_Falla()
    {
        await using var db = NewDb();
        var svc = new GrupoDashboardService(db);

        (await svc.GetAsync(Contador, 2026, 13)).ErrorCode.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task UsuarioInexistente_Falla()
    {
        await using var db = NewDb();
        var svc = new GrupoDashboardService(db);

        (await svc.GetAsync(999)).ErrorCode.Should().Be("USER_NOT_FOUND");
    }

    /// <summary>
    /// Regresión: el estado de empresa es "ACTIVA" (femenino), no "ACTIVO". Una empresa
    /// creada con el default de la entidad debe quedar operativa — con el valor equivocado
    /// el enforcement de la Entrega 7 la trataba como suspendida.
    /// </summary>
    [Fact]
    public async Task EmpresaConEstadoPorDefecto_QuedaActiva()
    {
        await using var db = NewDb();
        db.Empresas.Add(new Empresa { Id = 90, Nit = "E90", RazonSocial = "Nueva S.A." });
        db.UsuarioEmpresas.Add(new UsuarioEmpresa
        {
            UsuarioId = Contador, EmpresaId = 90, RolId = RolContador, EstadoCodigo = "ACTIVO",
        });
        await db.SaveChangesAsync();
        var svc = new GrupoDashboardService(db);

        var r = await svc.GetAsync(Contador);

        db.Empresas.Single(e => e.Id == 90).EstadoCodigo.Should().Be(EmpresaEstados.Activa);
        r.Value!.Empresas.Single(e => e.EmpresaId == 90).Activa.Should().BeTrue();
    }
}
