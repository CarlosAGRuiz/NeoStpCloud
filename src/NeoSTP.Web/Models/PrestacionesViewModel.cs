using NeoSTP.Application.Rrhh.Dtos;

namespace NeoSTP.Web.Models;

public sealed class PrestacionesViewModel
{
    public int Anio { get; init; }
    public bool PuedeGestionar { get; init; }
    public PoliticaPrestacionesDto Politica { get; init; } = new();
    public IReadOnlyList<EmpleadoDto> Empleados { get; init; } = [];
    public IReadOnlyList<SolicitudVacacionDto> Vacaciones { get; init; } = [];
    public IReadOnlyList<AguinaldoCalculoDto> Aguinaldos { get; init; } = [];
    public CrearSolicitudVacacionRequest NuevaVacacion { get; init; } = new();
}
