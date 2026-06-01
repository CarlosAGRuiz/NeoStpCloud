namespace NeoSTP.Application.Dte.Certificacion.Dtos;

/// <summary>Resumen total de progreso de certificación de una empresa.</summary>
public class CertificacionResumenDto
{
    public int Requeridos { get; set; }
    public int Completados { get; set; }
    public int EnProgreso { get; set; }
    public int ConError { get; set; }
    public int Pendientes => Requeridos - Completados - EnProgreso - ConError;
    public decimal PorcentajeProgreso => Requeridos == 0 ? 0 : Math.Round((decimal)Completados * 100m / Requeridos, 2);
    public bool ListaParaAutorizacion => Pendientes == 0 && ConError == 0 && EnProgreso == 0;
    public int TotalTipos { get; set; }
    public int TiposCompletados { get; set; }
}

/// <summary>Progreso por tipo DTE/evento (una fila de la matriz).</summary>
public class CertificacionMatrizProgresoDto
{
    public int MatrizId { get; set; }
    public string TipoDteCodigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public int Requeridos { get; set; }
    public int Completados { get; set; }
    public int EnProgreso { get; set; }
    public int ConError { get; set; }
    public int Pendientes => Requeridos - Completados - EnProgreso - ConError;
    public decimal PorcentajeProgreso => Requeridos == 0 ? 0 : Math.Round((decimal)Completados * 100m / Requeridos, 2);
    public bool Completo => Completados == Requeridos;
}

/// <summary>Escenario con su estado actual para una empresa.</summary>
public class CertificacionEscenarioDto
{
    public int Id { get; set; }
    public int MatrizId { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public string EstadoActual { get; set; } = "PENDIENTE";
    public int? PruebaId { get; set; }
    public int? DteDocumentoId { get; set; }
    public string? NumeroControl { get; set; }
    public string? SelloRecibido { get; set; }
    public int IntentoNumero { get; set; }
    public DateTime? UltimoIntentoAt { get; set; }
}

public class CertificacionPruebaDto
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int EscenarioId { get; set; }
    public string EscenarioCodigo { get; set; } = null!;
    public string EscenarioNombre { get; set; } = null!;
    public int? DteDocumentoId { get; set; }
    public string? NumeroControl { get; set; }
    public string EstadoCodigo { get; set; } = null!;
    public string? SelloRecibido { get; set; }
    public int IntentoNumero { get; set; }
    public DateTime? ProcesadoAt { get; set; }
    public string? Notas { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CertificacionErrorDto
{
    public int Id { get; set; }
    public int PruebaId { get; set; }
    public string CodigoMh { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public string? RespuestaMhJson { get; set; }
    public DateTime OcurrioAt { get; set; }
    public string? EscenarioCodigo { get; set; }
    public int? DteDocumentoId { get; set; }
    public string? NumeroControl { get; set; }
}

public class MarcarCompletadoRequest
{
    /// <summary>ID del escenario que cubre este DTE.</summary>
    public int EscenarioId { get; set; }
    public string? Notas { get; set; }
}

public class RegistrarErrorRequest
{
    public string CodigoMh { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public string? RespuestaMhJson { get; set; }
}
