using NeoSTP.Domain.Common;

namespace NeoSTP.Domain.Core.Notificaciones;

/// <summary>Token de dispositivo (FCM) para enviar push a un usuario.</summary>
public class DispositivoNotificacion : AuditableEntity
{
    public int EmpresaId { get; set; }
    public int UsuarioId { get; set; }

    /// <summary>Token FCM del dispositivo.</summary>
    public string Token { get; set; } = null!;

    /// <summary>ANDROID | IOS | WEB.</summary>
    public string Plataforma { get; set; } = "ANDROID";

    public bool Activo { get; set; } = true;
    public DateTime UltimoUsoAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Preferencias de notificación por usuario.</summary>
public class PreferenciaNotificacion : AuditableEntity
{
    public int EmpresaId { get; set; }
    public int UsuarioId { get; set; }

    /// <summary>PUSH | CORREO | AMBOS.</summary>
    public string Canal { get; set; } = "PUSH";

    /// <summary>Modo no molestar: no enviar push fuera del horario.</summary>
    public bool NoMolestar { get; set; }
    public TimeOnly HoraInicio { get; set; } = new(7, 0);
    public TimeOnly HoraFin { get; set; } = new(21, 0);
}

public static class NotifCanales
{
    public const string Push = "PUSH";
    public const string Correo = "CORREO";
    public const string Ambos = "AMBOS";
}
