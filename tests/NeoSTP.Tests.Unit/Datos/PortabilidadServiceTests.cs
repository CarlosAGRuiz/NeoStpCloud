using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Application.Auth.Abstractions;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Infrastructure.Persistence;
using NeoSTP.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace NeoSTP.Tests.Unit.Datos;

/// <summary>
/// Portabilidad (E8). Lo crítico: que el ZIP traiga los datos de la empresa que pide
/// y de ninguna otra — es un archivo que el cliente se lleva fuera de la plataforma.
/// </summary>
public class PortabilidadServiceTests
{
    private const int Empresa = 1;
    private const int Ajena = 2;

    private static NeoStpDbContext NewDb()
    {
        var db = new NeoStpDbContext(new DbContextOptionsBuilder<NeoStpDbContext>()
            .UseInMemoryDatabase($"portab-{Guid.NewGuid()}").Options);

        db.Empresas.AddRange(
            new Empresa { Id = Empresa, Nit = "06140101011001", RazonSocial = "Mi Empresa, S.A.", EstadoCodigo = EmpresaEstados.Activa },
            new Empresa { Id = Ajena, Nit = "06140101011999", RazonSocial = "Empresa Ajena", EstadoCodigo = EmpresaEstados.Activa });

        db.Clientes.AddRange(
            new Cliente { EmpresaId = Empresa, TipoDocumentoCodigo = "DUI", NumeroDocumento = "01", Nombre = "Cliente Propio" },
            new Cliente { EmpresaId = Ajena, TipoDocumentoCodigo = "DUI", NumeroDocumento = "99", Nombre = "Cliente Ajeno" });

        db.Productos.AddRange(
            new Producto
            {
                EmpresaId = Empresa, CodigoInterno = "P-1", Nombre = "Producto Propio",
                TipoItem = "BIEN", UnidadMedidaCodigo = "59", PrecioUnitario = 10m, EstadoCodigo = EstadoCodes.Activo,
            },
            new Producto
            {
                EmpresaId = Ajena, CodigoInterno = "P-9", Nombre = "Producto Ajeno",
                TipoItem = "BIEN", UnidadMedidaCodigo = "59", PrecioUnitario = 5m, EstadoCodigo = EstadoCodes.Activo,
            });

        db.SaveChanges();
        return db;
    }

    private static PortabilidadService NewService(NeoStpDbContext db)
        => new(db, Substitute.For<IAuditoriaService>());

    private static Dictionary<string, string> LeerZip(byte[] contenido)
    {
        using var ms = new MemoryStream(contenido);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var archivos = new Dictionary<string, string>();
        foreach (var e in zip.Entries)
        {
            using var s = e.Open();
            using var r = new StreamReader(s, Encoding.UTF8);
            archivos[e.Name] = r.ReadToEnd();
        }
        return archivos;
    }

    [Fact]
    public async Task Exportar_IncluyeLosArchivosEsperadosYElLeeme()
    {
        await using var db = NewDb();

        var r = await NewService(db).ExportarAsync(Empresa, "admin");

        r.IsSuccess.Should().BeTrue();
        var archivos = LeerZip(r.Value!.Contenido);
        archivos.Keys.Should().Contain(
            "empresa.csv", "clientes.csv", "productos.csv", "dte_documentos.csv",
            "dte_detalle.csv", "inventario_existencias.csv", "inventario_movimientos.csv",
            "cobros_pagos.csv", "proveedores.csv", "compras_facturas.csv", "LEEME.txt");
    }

    [Fact]
    public async Task Exportar_NoFiltraDatosDeOtraEmpresa()
    {
        await using var db = NewDb();

        var r = await NewService(db).ExportarAsync(Empresa, "admin");

        var archivos = LeerZip(r.Value!.Contenido);
        archivos["clientes.csv"].Should().Contain("Cliente Propio");
        archivos["clientes.csv"].Should().NotContain("Cliente Ajeno");
        archivos["productos.csv"].Should().Contain("Producto Propio");
        archivos["productos.csv"].Should().NotContain("Producto Ajeno");
        archivos["empresa.csv"].Should().NotContain("Empresa Ajena");
    }

    [Fact]
    public async Task Exportar_ResumenCuentaLasFilasReales()
    {
        await using var db = NewDb();

        var r = await NewService(db).ExportarAsync(Empresa, "admin");

        r.Value!.Resumen["clientes"].Should().Be(1);
        r.Value.Resumen["productos"].Should().Be(1);
        r.Value.Resumen["dte_documentos"].Should().Be(0);
    }

    [Fact]
    public async Task Exportar_NombreDeArchivoLlevaNitYFecha()
    {
        await using var db = NewDb();

        var r = await NewService(db).ExportarAsync(Empresa, "admin");

        r.Value!.NombreArchivo.Should().Be($"neostp_06140101011001_{DateTime.UtcNow:yyyyMMdd}.zip");
    }

    [Fact]
    public async Task Exportar_IncluyeLosDteConSuDetalle()
    {
        await using var db = NewDb();
        var producto = await db.Productos.FirstAsync(p => p.EmpresaId == Empresa);
        var dte = new DteDocumento
        {
            EmpresaId = Empresa, TipoDteCodigo = TipoDteCodigos.FacturaConsumidorFinal,
            NumeroControl = "DTE-01-PRUEBA-0001", CodigoGeneracion = Guid.NewGuid().ToString().ToUpperInvariant(),
            EstadoCodigo = DteEstadoCodigos.Procesado, FechaEmision = DateTime.UtcNow.Date,
            CondicionOperacionCodigo = "1", TotalPagar = 11.30m, IvaTotal = 1.30m,
        };
        dte.Detalles.Add(new DteDocumentoDetalle
        {
            NumeroLinea = 1, ProductoId = producto.Id, TipoItem = 1,
            Codigo = producto.CodigoInterno, Descripcion = producto.Nombre,
            UnidadMedidaCodigo = "59", Cantidad = 1m, PrecioUnitario = 11.30m,
        });
        db.DteDocumentos.Add(dte);
        await db.SaveChangesAsync();

        var r = await NewService(db).ExportarAsync(Empresa, "admin");

        var archivos = LeerZip(r.Value!.Contenido);
        archivos["dte_documentos.csv"].Should().Contain("DTE-01-PRUEBA-0001");
        archivos["dte_detalle.csv"].Should().Contain("Producto Propio");
        r.Value.Resumen["dte_documentos"].Should().Be(1);
        r.Value.Resumen["dte_detalle"].Should().Be(1);
    }

    [Fact]
    public async Task Exportar_EmpresaInexistente_Falla()
    {
        await using var db = NewDb();

        var r = await NewService(db).ExportarAsync(999, "admin");

        r.ErrorCode.Should().Be("EMPRESA_NOT_FOUND");
    }

    [Fact]
    public async Task Exportar_LeemeExplicaComoCruzarLosArchivos()
    {
        await using var db = NewDb();

        var r = await NewService(db).ExportarAsync(Empresa, "admin");

        var leeme = LeerZip(r.Value!.Contenido)["LEEME.txt"];
        leeme.Should().Contain("Mi Empresa, S.A.");
        leeme.Should().Contain("clientes.csv");
        leeme.Should().Contain("dte_documentos.Id");
    }
}
