namespace NeoSTP.Application.Notificaciones;

public sealed record DteCorreoOutboxPayload(
    int EmpresaId,
    int DteDocumentoId,
    string Finalidad,
    bool Automatico,
    string? Actor);
