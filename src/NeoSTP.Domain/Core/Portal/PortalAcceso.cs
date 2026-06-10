using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Portal;

/// <summary>
/// Enlace público del portal de receptor (NEOPORTAL). El token viaja solo en la URL;
/// aquí se guarda su hash SHA-256. Expira y puede revocarse. Nunca cruza empresa:
/// el acceso resuelve al documento/cliente exacto con el que fue emitido.
/// </summary>
public class PortalAcceso : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>DOCUMENTO (un DTE) | ESTADO_CUENTA (un cliente).</summary>
    public string Tipo { get; set; } = PortalAccesoTipos.Documento;

    public int? DteDocumentoId { get; set; }
    public int? ClienteId { get; set; }

    /// <summary>SHA-256 (hex) del token público.</summary>
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiraAt { get; set; }
    public DateTime? RevocadoAt { get; set; }

    public int Accesos { get; set; }
    public DateTime? UltimoAccesoAt { get; set; }

    public string? Nota { get; set; }
}

public static class PortalAccesoTipos
{
    public const string Documento = "DOCUMENTO";
    public const string EstadoCuenta = "ESTADO_CUENTA";

    public static readonly string[] All = [Documento, EstadoCuenta];
}
