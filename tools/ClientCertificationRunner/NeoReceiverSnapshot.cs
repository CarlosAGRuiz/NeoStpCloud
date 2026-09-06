using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Infrastructure.Persistence;

namespace ClientCertificationRunner;

/// <summary>Reviewed public fiscal data only. Never loads keys or tracks/mutates NEO.</summary>
public sealed record NeoReceiverSnapshot(int EmpresaId, string Nit, string? Nrc, string RazonSocial, string? NombreComercial,
    string? CodigoActividad, string? ActividadEconomica, string? Departamento, string? Municipio, string? Distrito,
    string? Direccion, string? Telefono, string? Correo, string EstadoCodigo, string? AmbienteCodigo)
{
    public const string ExpectedHash = "317182F1ABD7FE71963FF846D03E7F529E130A163059CFCAEC07687D729AE677";
    public static readonly NeoReceiverSnapshot Expected = new(2, "06231111251090", "3755868",
        "NEO SOFTWARE TECH PRO, SOCIEDAD POR ACCIONES SIMPLIFICADA DE CAPITAL VARIABLE", "NeoSTP", "62010",
        "Actividades de programación informática", "06", "23", "03", "San Salvador, Ayutuxtepeque", "72847720",
        "Carlosgruiz34@gmail.com", "ACTIVA", "PRUEBAS");
    public string Hash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this,
        new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))));
    public static async Task<NeoReceiverSnapshot> ReadAndVerifyAsync(NeoStpDbContext db, CancellationToken ct)
    {
        var snapshot = await (from company in db.Empresas.AsNoTracking()
                              join fiscal in db.DteConfiguracion.AsNoTracking() on company.Id equals fiscal.EmpresaId
                              where company.Id == 2
                              select new NeoReceiverSnapshot(company.Id, company.Nit, company.Nrc, company.RazonSocial,
                                  company.NombreComercial, company.CodigoActividad, company.ActividadEconomica, company.Departamento,
                                  company.Municipio, company.Distrito, company.Direccion, company.Telefono, company.Correo,
                                  company.EstadoCodigo, fiscal.AmbienteCodigo)).SingleAsync(ct);
        RunnerPolicy.Require(snapshot == Expected && snapshot.Hash() == ExpectedHash, "REVIEWED_NEO_PUBLIC_SNAPSHOT_CHANGED");
        return snapshot;
    }
    public void ApplyToReviewedDocument(DteDocumento document)
    {
        RunnerPolicy.Require(document.Id == 1017 && document.EmpresaId == 23 && document.AmbienteCodigo == "PRUEBAS"
            && document.TipoDteCodigo == "03" && this == Expected && Hash() == ExpectedHash, "REVIEWED_NEO_SNAPSHOT_SCOPE_REJECTED");
        document.ReceptorTipoDocumento = "36"; document.ReceptorNumeroDocumento = Nit; document.ReceptorNrc = Nrc;
        document.ReceptorNombre = RazonSocial; document.ReceptorCodigoActividad = CodigoActividad; document.ReceptorActividadEconomica = ActividadEconomica;
        document.ReceptorDepartamentoCodigo = Departamento; document.ReceptorMunicipioCodigo = Municipio; document.ReceptorDistritoCodigo = Distrito;
        document.ReceptorDireccion = Direccion; document.ReceptorTelefono = Telefono; document.ReceptorCorreo = Correo;
    }
}
