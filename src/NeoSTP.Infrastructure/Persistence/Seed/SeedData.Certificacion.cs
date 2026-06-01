using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Dte.Certificacion;

namespace NeoSTP.Infrastructure.Persistence.Seed;

/// <summary>
/// Sprint 14.1 — Seed de la matriz oficial de certificación de Hacienda.
///
/// 15 cabeceras (CertificacionMatriz) + 625 escenarios numerados (FACTURA-01,
/// CCF-01, etc.) con nombre y descripción genéricos. El detalle real de cada
/// escenario se completa via UI/API cuando esté disponible el manual oficial.
///
/// IDs:
///   - CertificacionMatriz: 1-15
///   - CertificacionEscenario: 10001-10625 (reservados altos para no colisionar)
/// </summary>
internal static partial class SeedData
{
    private record CertMatrizSeed(int Id, string TipoDteCodigo, string Nombre, string Descripcion, int Requeridos, int Orden);

    private static readonly CertMatrizSeed[] CertMatrices =
    {
        new(1,  "01",                    "Factura",                          "Factura electrónica (Consumidor Final)",          90, 1),
        new(2,  "03",                    "CCF",                              "Comprobante de Crédito Fiscal",                   75, 2),
        new(3,  "04",                    "Nota de Remisión",                 "Nota de Remisión electrónica",                    50, 3),
        new(4,  "05",                    "Nota de Crédito",                  "Nota de Crédito electrónica",                     50, 4),
        new(5,  "06",                    "Nota de Débito",                   "Nota de Débito electrónica",                      25, 5),
        new(6,  "07",                    "Comprobante de Retención",         "Comprobante de Retención de IVA",                 50, 6),
        new(7,  "08",                    "Comprobante de Liquidación",       "Comprobante de Liquidación",                      75, 7),
        new(8,  "09",                    "Documento Contable de Liquidación","Documento Contable de Liquidación",               50, 8),
        new(9,  "11",                    "Factura de Exportación",           "Factura de Exportación",                          90, 9),
        new(10, "14",                    "Sujeto Excluido",                  "Factura Sujeto Excluido",                         25, 10),
        new(11, "15",                    "Donación",                         "Comprobante de Donación",                         25, 11),
        new(12, "INVALIDACION",          "Invalidación",                     "Evento de invalidación",                          5,  12),
        new(13, "CONTINGENCIA",          "Contingencia",                     "Evento de contingencia",                          5,  13),
        new(14, "RETORNO",               "Retorno",                          "Evento de retorno",                               5,  14),
        new(15, "OPERACIONES_ESPECIALES","Operaciones Especiales",           "Evento de operaciones especiales",                5,  15),
    };

    public static void ApplyCertificacion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CertificacionMatriz>().HasData(
            CertMatrices.Select(m => new CertificacionMatriz
            {
                Id = m.Id,
                TipoDteCodigo = m.TipoDteCodigo,
                Nombre = m.Nombre,
                Descripcion = m.Descripcion,
                EscenariosRequeridos = m.Requeridos,
                Orden = m.Orden,
                Activo = true,
                CreatedAt = SeededAt,
                CreatedBy = SeededBy,
            }).ToArray());

        // 625 escenarios numerados.
        var escenarios = new List<CertificacionEscenario>(625);
        var nextId = 10001;
        foreach (var m in CertMatrices)
        {
            var prefijo = ShortPrefix(m.TipoDteCodigo, m.Nombre);
            for (var n = 1; n <= m.Requeridos; n++)
            {
                escenarios.Add(new CertificacionEscenario
                {
                    Id = nextId++,
                    MatrizId = m.Id,
                    Codigo = $"{prefijo}-{n:00}",
                    Nombre = $"{m.Nombre} — escenario {n:00}",
                    Descripcion = $"Escenario {n:00} de la matriz oficial Hacienda para {m.Nombre}. " +
                                  "Detalle pendiente — editar via /Catalogos o admin de certificación.",
                    Orden = n,
                    Activo = true,
                    CreatedAt = SeededAt,
                    CreatedBy = SeededBy,
                });
            }
        }

        modelBuilder.Entity<CertificacionEscenario>().HasData(escenarios);
    }

    /// <summary>
    /// Prefijo corto para el código del escenario. Para DTE numéricos usa el nombre
    /// (FACTURA, CCF, NR, NC, ND, RET, LIQ, DCL, EXP, FSE, DON); para eventos usa
    /// el código en sí pero acortado.
    /// </summary>
    private static string ShortPrefix(string tipo, string nombre) => tipo switch
    {
        "01" => "FACTURA",
        "03" => "CCF",
        "04" => "NR",
        "05" => "NC",
        "06" => "ND",
        "07" => "RET",
        "08" => "LIQ",
        "09" => "DCL",
        "11" => "EXP",
        "14" => "FSE",
        "15" => "DON",
        "INVALIDACION" => "INV",
        "CONTINGENCIA" => "CONT",
        "RETORNO" => "RETOR",
        "OPERACIONES_ESPECIALES" => "OPESP",
        _ => nombre.ToUpperInvariant().Substring(0, Math.Min(6, nombre.Length)),
    };
}
