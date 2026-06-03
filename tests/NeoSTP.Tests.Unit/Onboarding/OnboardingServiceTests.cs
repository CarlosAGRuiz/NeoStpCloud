using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Onboarding;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using Xunit;

namespace NeoSTP.Tests.Unit.Onboarding;

/// <summary>
/// Onboarding self-service — verifica que el estado de activación se deriva de datos reales,
/// aísla por empresa y avanza el porcentaje a medida que se completan los pasos.
/// </summary>
public class OnboardingServiceTests
{
    private const int EmpresaA = 10;

    private static NeoStpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"onboarding-{Guid.NewGuid()}")
            .Options;
        return new NeoStpDbContext(options);
    }

    private static void AddEmpresa(NeoStpDbContext db, int id, bool perfilCompleto)
    {
        db.Empresas.Add(new Empresa
        {
            Id = id,
            Nit = "0614-010101-001-0",
            RazonSocial = "Demo SA",
            EstadoCodigo = "ACTIVA",
            Nrc = perfilCompleto ? "123456-7" : null,
            CodigoActividad = perfilCompleto ? "62010" : null,
            Departamento = perfilCompleto ? "06" : null,
            Municipio = perfilCompleto ? "23" : null,
            Direccion = perfilCompleto ? "San Salvador" : null,
        });
    }

    [Fact]
    public async Task EmpresaReciénCreada_TodosLosPasosPendientes()
    {
        var db = NewDb();
        AddEmpresa(db, EmpresaA, perfilCompleto: false);
        await db.SaveChangesAsync();
        var svc = new OnboardingService(db);

        var estado = await svc.GetEstadoAsync(EmpresaA);

        estado.PasosTotal.Should().Be(5);
        estado.PasosCompletados.Should().Be(0);
        estado.PorcentajeCompletado.Should().Be(0);
        estado.Completo.Should().BeFalse();
        estado.SiguientePaso!.Codigo.Should().Be(OnboardingPasos.PerfilEmpresa);
    }

    [Fact]
    public async Task PerfilCompleto_MarcaSoloEsePaso()
    {
        var db = NewDb();
        AddEmpresa(db, EmpresaA, perfilCompleto: true);
        await db.SaveChangesAsync();
        var svc = new OnboardingService(db);

        var estado = await svc.GetEstadoAsync(EmpresaA);

        estado.Pasos.Single(p => p.Codigo == OnboardingPasos.PerfilEmpresa).Completado.Should().BeTrue();
        estado.PasosCompletados.Should().Be(1);
        estado.PorcentajeCompletado.Should().Be(20);
        estado.SiguientePaso!.Codigo.Should().Be(OnboardingPasos.ConfigDte);
    }

    [Fact]
    public async Task CredencialesYCertificado_MarcanSusPasos()
    {
        var db = NewDb();
        AddEmpresa(db, EmpresaA, perfilCompleto: true);
        db.DteConfiguracion.Add(new DteConfiguracion
        {
            EmpresaId = EmpresaA,
            AmbienteCodigo = "PRUEBAS",
            UsuarioMh = "user",
            PasswordMhCifrado = "cifrado",
            TipoEstablecimientoCodigo = "CASA_MATRIZ",
            CodigoEstablecimientoMh = "0001",
            CodigoPuntoVentaMh = "P001",
            CertificadoBlob = new byte[] { 1, 2, 3 },
            CertificadoNombre = "cert.crt",
        });
        await db.SaveChangesAsync();
        var svc = new OnboardingService(db);

        var estado = await svc.GetEstadoAsync(EmpresaA);

        estado.Pasos.Single(p => p.Codigo == OnboardingPasos.ConfigDte).Completado.Should().BeTrue();
        estado.Pasos.Single(p => p.Codigo == OnboardingPasos.Certificado).Completado.Should().BeTrue();
        estado.PasosCompletados.Should().Be(3);
    }

    [Fact]
    public async Task CatalogoBase_RequiereClienteYProductoActivos()
    {
        var db = NewDb();
        AddEmpresa(db, EmpresaA, perfilCompleto: true);
        db.Clientes.Add(new Cliente
        {
            EmpresaId = EmpresaA, TipoDocumentoCodigo = "DUI", NumeroDocumento = "1-1",
            Nombre = "C", TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", EstadoCodigo = "ACTIVO",
        });
        // Producto inactivo => no debería contar
        db.Productos.Add(new Producto
        {
            EmpresaId = EmpresaA, CodigoInterno = "P-1", Nombre = "P", TipoItem = "BIEN", EstadoCodigo = "INACTIVO",
        });
        await db.SaveChangesAsync();
        var svc = new OnboardingService(db);

        var estado = await svc.GetEstadoAsync(EmpresaA);
        estado.Pasos.Single(p => p.Codigo == OnboardingPasos.CatalogoBase).Completado.Should().BeFalse();

        db.Productos.Add(new Producto
        {
            EmpresaId = EmpresaA, CodigoInterno = "P-2", Nombre = "P2", TipoItem = "BIEN", EstadoCodigo = "ACTIVO",
        });
        await db.SaveChangesAsync();

        var estado2 = await svc.GetEstadoAsync(EmpresaA);
        estado2.Pasos.Single(p => p.Codigo == OnboardingPasos.CatalogoBase).Completado.Should().BeTrue();
    }

    [Fact]
    public async Task PrimerDte_SoloCuentaProcesado()
    {
        var db = NewDb();
        AddEmpresa(db, EmpresaA, perfilCompleto: true);
        db.DteDocumentos.Add(new DteDocumento
        {
            EmpresaId = EmpresaA, TipoDteCodigo = "01", EstadoCodigo = DteEstadoCodigos.Borrador,
            NumeroControl = "DTE-01-0001-000000000000001", CodigoGeneracion = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();
        var svc = new OnboardingService(db);

        (await svc.GetEstadoAsync(EmpresaA)).Pasos
            .Single(p => p.Codigo == OnboardingPasos.PrimerDte).Completado.Should().BeFalse();

        db.DteDocumentos.Add(new DteDocumento
        {
            EmpresaId = EmpresaA, TipoDteCodigo = "01", EstadoCodigo = DteEstadoCodigos.Procesado,
            NumeroControl = "DTE-01-0001-000000000000002", CodigoGeneracion = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();

        (await svc.GetEstadoAsync(EmpresaA)).Pasos
            .Single(p => p.Codigo == OnboardingPasos.PrimerDte).Completado.Should().BeTrue();
    }

    [Fact]
    public async Task DatosDeOtraEmpresa_NoCuentan()
    {
        var db = NewDb();
        AddEmpresa(db, EmpresaA, perfilCompleto: true);
        AddEmpresa(db, 99, perfilCompleto: true);
        db.Clientes.Add(new Cliente
        {
            EmpresaId = 99, TipoDocumentoCodigo = "DUI", NumeroDocumento = "9-9",
            Nombre = "Ajeno", TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", EstadoCodigo = "ACTIVO",
        });
        db.Productos.Add(new Producto
        {
            EmpresaId = 99, CodigoInterno = "X", Nombre = "X", TipoItem = "BIEN", EstadoCodigo = "ACTIVO",
        });
        await db.SaveChangesAsync();
        var svc = new OnboardingService(db);

        var estado = await svc.GetEstadoAsync(EmpresaA);
        estado.Pasos.Single(p => p.Codigo == OnboardingPasos.CatalogoBase).Completado.Should().BeFalse();
    }

    [Fact]
    public async Task TodoCompleto_PorcentajeCien()
    {
        var db = NewDb();
        AddEmpresa(db, EmpresaA, perfilCompleto: true);
        db.DteConfiguracion.Add(new DteConfiguracion
        {
            EmpresaId = EmpresaA, AmbienteCodigo = "PRUEBAS", UsuarioMh = "u", PasswordMhCifrado = "c",
            TipoEstablecimientoCodigo = "CASA_MATRIZ", CodigoEstablecimientoMh = "0001", CodigoPuntoVentaMh = "P001",
            CertificadoBlob = new byte[] { 1 }, CertificadoNombre = "c.crt",
        });
        db.Clientes.Add(new Cliente
        {
            EmpresaId = EmpresaA, TipoDocumentoCodigo = "DUI", NumeroDocumento = "1-1",
            Nombre = "C", TipoContribuyenteCodigo = "CONSUMIDOR_FINAL", EstadoCodigo = "ACTIVO",
        });
        db.Productos.Add(new Producto
        {
            EmpresaId = EmpresaA, CodigoInterno = "P-1", Nombre = "P", TipoItem = "BIEN", EstadoCodigo = "ACTIVO",
        });
        db.DteDocumentos.Add(new DteDocumento
        {
            EmpresaId = EmpresaA, TipoDteCodigo = "01", EstadoCodigo = DteEstadoCodigos.Procesado,
            NumeroControl = "DTE-01-0001-000000000000002", CodigoGeneracion = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();
        var svc = new OnboardingService(db);

        var estado = await svc.GetEstadoAsync(EmpresaA);

        estado.Completo.Should().BeTrue();
        estado.PorcentajeCompletado.Should().Be(100);
        estado.SiguientePaso.Should().BeNull();
    }
}
