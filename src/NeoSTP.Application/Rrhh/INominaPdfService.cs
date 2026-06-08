using NeoSTP.Application.Rrhh.Dtos;

namespace NeoSTP.Application.Rrhh;

/// <summary>Renderiza el recibo/boleta de pago de nómina a PDF.</summary>
public interface INominaPdfService
{
    byte[] GenerarRecibo(ReciboNominaModel recibo);
}
