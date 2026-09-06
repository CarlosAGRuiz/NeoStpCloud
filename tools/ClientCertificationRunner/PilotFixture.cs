using NeoSTP.Domain.Core.Dte;

namespace ClientCertificationRunner;

public static class PilotFixture
{
    // Deliberately synthetic recipients; approval of a fixture is separate from official scenario coverage.
    public const string Version = "CLIENT23-PILOT-FIXTURE-V2";
    public static int ExpectedVersion(string type) => type switch { "01" => 2, "03" => 4, "11" => 3, "14" => 2, _ => throw new RunnerRejected("FIXTURE_TYPE_REJECTED") };
    public static DteDocumento Create(string type, string block, DateTime localDate)
    {
        RunnerPolicy.Require(RunnerPolicy.Types.Contains(type), "FIXTURE_TYPE_REJECTED");
        var gross = type == "01" ? 1.13m : 1m;
        return new DteDocumento {
            EmpresaId = 23, TipoDteCodigo = type, VersionDte = ExpectedVersion(type),
            AmbienteCodigo = "PRUEBAS", EstadoCodigo = "BORRADOR",
            NumeroControl = $"DTE-{type}-{block}-000000000000001", CodigoGeneracion = Guid.NewGuid().ToString("D").ToUpperInvariant(),
            FechaEmision = localDate.Date, HoraEmision = localDate.TimeOfDay, CondicionOperacionCodigo = "1", FormaPagoCodigo = "01",
            ReceptorTipoDocumento = type == "01" ? null : type is "11" or "14" ? "37" : "36",
            ReceptorNumeroDocumento = type == "01" ? null : type == "03" ? "06140000000000" : type == "11" ? "SYNTHETIC-PILOT-001" : "CERT-SE-PILOT-001",
            ReceptorNrc = type == "03" ? "7654321" : null, ReceptorNombre = "RECEPTOR SINTETICO PRUEBA CERTIFICACION",
            ReceptorCodigoActividad = "62010", ReceptorActividadEconomica = "PROGRAMACION INFORMATICA",
            ReceptorDepartamentoCodigo = "05", ReceptorMunicipioCodigo = "24", ReceptorDistritoCodigo = "15",
            ReceptorDireccion = "DIRECCION SINTETICA PARA CERTIFICACION EN PRUEBAS",
            ReceptorCorreo = "certification@example.invalid", ReceptorTelefono = "22000001",
            ReceptorPaisCodigo = "US", ReceptorPaisNombre = "Estados Unidos", ReceptorTipoPersona = 2,
            Observaciones = "PRUEBA SINTETICA DE CERTIFICACION. SIN OPERACION COMERCIAL REAL.",
            Detalles = [new DteDocumentoDetalle { NumeroLinea = 1, TipoItem = 2, Codigo = "CERT-PILOT-" + type,
                Descripcion = "SERVICIO SINTETICO DE CERTIFICACION", UnidadMedidaCodigo = "59", Cantidad = 1,
                PrecioUnitario = gross, VentaGravada = gross, IvaItem = type is "01" or "03" ? .13m : 0m }]
        };
    }
}
