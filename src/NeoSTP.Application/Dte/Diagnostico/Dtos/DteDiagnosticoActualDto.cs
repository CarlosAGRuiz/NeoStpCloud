namespace NeoSTP.Application.Dte.Diagnostico.Dtos;

/// <summary>Guía informativa del DTE existente; no autoriza ni ejecuta una retransmisión.</summary>
public record DteDiagnosticoActualDto(
    string Codigo,
    string Mensaje,
    string? CodigoHacienda,
    string? MensajeTecnico,
    IReadOnlyList<string> Observaciones,
    IReadOnlyList<DteProblemaCampoDto> Campos,
    string SiguientePaso,
    string AccionSugerida,
    bool RequiereConsultaHacienda)
{
    public bool ReintentoAutomatico => false;
}

public record DteProblemaCampoDto(string Campo, string Seccion, string Mensaje, string AccionSugerida);
