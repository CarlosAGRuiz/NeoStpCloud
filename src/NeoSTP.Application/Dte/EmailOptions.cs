namespace NeoSTP.Application.Dte;

public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Mock | Smtp</summary>
    public string Provider { get; set; } = "Mock";

    /// <summary>Carpeta donde MockEmailSender deja los .eml para inspección.</summary>
    public string MockOutbox { get; set; } = "logs/email-outbox";

    public SmtpEmailOptions Smtp { get; set; } = new();
    public EmailFromOptions From { get; set; } = new();
}

public class SmtpEmailOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class EmailFromOptions
{
    public string Address { get; set; } = "noreply@neostp.local";
    public string DisplayName { get; set; } = "NeoSTP Cloud";
}
