using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Empresas;

namespace NeoSTP.Domain.Core.Notificaciones;

/// <summary>
/// Alerta operativa/fiscal mostrada en el centro de notificaciones y enviada por push.
/// Se deriva de datos reales (DTE rechazado, certificado por vencer, factura vencida…).
/// </summary>
public class Alerta : AuditableEntity
{
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;

    /// <summary>Destinatario específico; null = alerta para toda la empresa.</summary>
    public int? UsuarioId { get; set; }

    /// <summary>DTE_RECHAZADO, CERT_POR_VENCER, FACTURA_VENCIDA, F07_PROXIMA, etc.</summary>
    public string TipoCodigo { get; set; } = null!;

    /// <summary>INFO | ADVERTENCIA | CRITICA.</summary>
    public string Severidad { get; set; } = AlertaSeveridades.Info;

    public string Titulo { get; set; } = null!;
    public string Mensaje { get; set; } = null!;

    /// <summary>Entidad afectada para "abrir documento desde la alerta" (ej. DteDocumento).</summary>
    public string? EntidadTipo { get; set; }
    public int? EntidadId { get; set; }

    /// <summary>PENDIENTE | LEIDA | RESUELTA.</summary>
    public string EstadoCodigo { get; set; } = AlertaEstados.Pendiente;

    /// <summary>Clave de deduplicación (TipoCodigo:EntidadId). Única por empresa.</summary>
    public string Clave { get; set; } = null!;

    public DateTime? LeidaAt { get; set; }
    public DateTime? ResueltaAt { get; set; }
}

public static class AlertaTipos
{
    public const string DteRechazado = "DTE_RECHAZADO";
    public const string CertPorVencer = "CERT_POR_VENCER";
    public const string FacturaVencida = "FACTURA_VENCIDA";
    public const string F07Proxima = "F07_PROXIMA";
    public const string StockBajo = "STOCK_BAJO";
    public const string ActividadCrmVencida = "ACTIVIDAD_CRM_VENCIDA";
    public const string LotePorVencer = "LOTE_POR_VENCER";
}

public static class AlertaSeveridades
{
    public const string Info = "INFO";
    public const string Advertencia = "ADVERTENCIA";
    public const string Critica = "CRITICA";
}

public static class AlertaEstados
{
    public const string Pendiente = "PENDIENTE";
    public const string Leida = "LEIDA";
    public const string Resuelta = "RESUELTA";
}
