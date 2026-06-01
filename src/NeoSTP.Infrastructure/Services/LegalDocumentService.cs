using Microsoft.Extensions.Options;
using NeoSTP.Application.Legal;
using NeoSTP.Domain.Core.Legal;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Services;

public class LegalDocumentService : ILegalDocumentService
{
    private readonly LegalOptions _opts;
    private readonly NeoStpDbContext _db;

    public LegalDocumentService(IOptions<LegalOptions> opts, NeoStpDbContext db)
    {
        _opts = opts.Value;
        _db   = db;
    }

    public string VersionActual => _opts.Version;

    public string? ObtenerContenido(string slug)
    {
        var raw = slug.ToLowerInvariant() switch
        {
            "terms"   => TermsHtml(),
            "privacy" => PrivacyHtml(),
            "cookies" => CookiesHtml(),
            "dpa"     => DpaHtml(),
            _         => null
        };

        return raw is null ? null : Reemplazar(raw);
    }

    public async Task RegistrarConsentimientosAsync(
        int? usuarioId, int? empresaId,
        IEnumerable<string> tipos,
        string? ip, string? userAgent,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        foreach (var tipo in tipos)
        {
            _db.UserConsents.Add(new UserConsent
            {
                UsuarioId         = usuarioId,
                EmpresaId         = empresaId,
                ConsentType       = tipo,
                Version           = _opts.Version,
                AcceptedAt        = now,
                AcceptedFromIp    = ip,
                AcceptedUserAgent = userAgent
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    // ──────────────────────────────────────────────────────────────────────────
    private string Reemplazar(string html) => html
        .Replace("{CompanyName}",  _opts.CompanyName)
        .Replace("{ContactEmail}", _opts.ContactEmail)
        .Replace("{EffectiveDate}", _opts.EffectiveDate)
        .Replace("{Version}",      _opts.Version)
        .Replace("{Country}",      _opts.Country)
        .Replace("{Website}",      _opts.Website);

    // ──────────────────────────────────────────────────────────────────────────
    // Documentos legales en línea (texto estático con placeholders)
    // ──────────────────────────────────────────────────────────────────────────

    private static string TermsHtml() => """
        <h1>Términos y Condiciones de Uso</h1>
        <p><strong>Versión {Version} · Vigente desde {EffectiveDate}</strong></p>
        <p>Bienvenido a <strong>{CompanyName}</strong>. Al acceder o usar nuestra plataforma,
        aceptas estos Términos y Condiciones. Léelos con atención.</p>

        <h2>1. Objeto del servicio</h2>
        <p>{CompanyName} es una plataforma SaaS de gestión tributaria y emisión de Documentos Tributarios
        Electrónicos (DTE). El servicio se presta en modalidad de suscripción.</p>

        <h2>2. Uso aceptable</h2>
        <p>El usuario se compromete a utilizar la plataforma únicamente para fines lícitos y conforme a
        la legislación vigente en {Country}. Está prohibido: compartir credenciales, realizar ingeniería
        inversa, intentar acceder a datos de otros tenants o utilizar la plataforma para actividades
        fraudulentas.</p>

        <h2>3. Propiedad intelectual</h2>
        <p>Todos los derechos sobre la plataforma, su código y diseño son propiedad exclusiva de
        {CompanyName}. El usuario obtiene una licencia de uso no exclusiva e intransferible.</p>

        <h2>4. Limitación de responsabilidad</h2>
        <p>{CompanyName} no se hace responsable de pérdidas derivadas del uso incorrecto de la plataforma
        ni de interrupciones ajenas a su control (fuerza mayor, fallas de terceros, etc.).</p>

        <h2>5. Modificaciones</h2>
        <p>Nos reservamos el derecho de modificar estos términos con previo aviso de 30 días. El uso
        continuado de la plataforma implica aceptación de los nuevos términos.</p>

        <h2>6. Contacto</h2>
        <p>Para consultas legales: <a href="mailto:{ContactEmail}">{ContactEmail}</a></p>
        """;

    private static string PrivacyHtml() => """
        <h1>Política de Privacidad</h1>
        <p><strong>Versión {Version} · Vigente desde {EffectiveDate}</strong></p>

        <h2>1. Responsable del tratamiento</h2>
        <p><strong>{CompanyName}</strong> es responsable del tratamiento de los datos personales
        recabados a través de la plataforma. Contacto: <a href="mailto:{ContactEmail}">{ContactEmail}</a></p>

        <h2>2. Datos que recopilamos</h2>
        <ul>
          <li>Datos de identificación: nombre, correo electrónico, NIT/DUI.</li>
          <li>Datos de empresa: razón social, NRC, dirección fiscal.</li>
          <li>Datos de uso: acciones en la plataforma, IPs de acceso, logs de auditoría.</li>
          <li>Datos fiscales: documentos DTE y sus respuestas del Ministerio de Hacienda.</li>
        </ul>

        <h2>3. Finalidad del tratamiento</h2>
        <p>Los datos se usan exclusivamente para: prestar el servicio contratado, cumplir
        obligaciones fiscales ante el Ministerio de Hacienda de {Country}, enviar notificaciones
        del servicio y mejorar la plataforma.</p>

        <h2>4. Compartición con terceros</h2>
        <p>No vendemos ni cedemos datos personales a terceros con fines comerciales. Los datos
        fiscales se transmiten únicamente al Ministerio de Hacienda de {Country} según lo exige
        la ley.</p>

        <h2>5. Derechos del titular</h2>
        <p>Puedes ejercer tus derechos de acceso, rectificación, cancelación y oposición
        escribiendo a <a href="mailto:{ContactEmail}">{ContactEmail}</a>.</p>

        <h2>6. Retención de datos</h2>
        <p>Los datos fiscales se retienen durante el período mínimo exigido por la legislación
        fiscal vigente. Los datos de cuenta se eliminan 90 días después de solicitar la baja.</p>
        """;

    private static string CookiesHtml() => """
        <h1>Política de Cookies</h1>
        <p><strong>Versión {Version} · Vigente desde {EffectiveDate}</strong></p>

        <h2>¿Qué son las cookies?</h2>
        <p>Las cookies son pequeños archivos que se almacenan en tu navegador cuando visitas un
        sitio web. {CompanyName} utiliza cookies estrictamente necesarias para el funcionamiento
        de la plataforma.</p>

        <h2>Cookies que usamos</h2>
        <table class="table table-sm">
          <thead><tr><th>Nombre</th><th>Propósito</th><th>Duración</th></tr></thead>
          <tbody>
            <tr><td>.neostp.auth</td><td>Autenticación de sesión</td><td>Sesión / 30 días (recordar)</td></tr>
            <tr><td>.neostp.empresa</td><td>Empresa activa en modo soporte</td><td>Sesión</td></tr>
            <tr><td>.neostp.antiforgery</td><td>Protección CSRF</td><td>Sesión</td></tr>
          </tbody>
        </table>

        <h2>Cookies de terceros</h2>
        <p>La plataforma no carga scripts de terceros con fines publicitarios ni de rastreo.</p>

        <h2>Control de cookies</h2>
        <p>Puedes configurar tu navegador para rechazar cookies, pero esto puede afectar la
        funcionalidad de la plataforma. Para más información: <a href="mailto:{ContactEmail}">{ContactEmail}</a></p>
        """;

    private static string DpaHtml() => """
        <h1>Acuerdo de Procesamiento de Datos (DPA)</h1>
        <p><strong>Versión {Version} · Vigente desde {EffectiveDate}</strong></p>

        <h2>1. Partes</h2>
        <p>Este acuerdo aplica entre <strong>{CompanyName}</strong> (Encargado del Tratamiento)
        y el Cliente (Responsable del Tratamiento) que contrata la plataforma.</p>

        <h2>2. Objeto</h2>
        <p>{CompanyName} procesa datos personales en nombre del Cliente exclusivamente para prestar
        el servicio SaaS contratado, conforme a las instrucciones documentadas del Cliente y a la
        legislación aplicable en {Country}.</p>

        <h2>3. Obligaciones de {CompanyName}</h2>
        <ul>
          <li>Procesar datos solo según instrucciones del Cliente.</li>
          <li>Garantizar confidencialidad del personal autorizado.</li>
          <li>Implementar medidas técnicas y organizativas adecuadas.</li>
          <li>Notificar brechas de seguridad en un plazo máximo de 72 horas.</li>
          <li>Eliminar o devolver los datos al término del contrato.</li>
        </ul>

        <h2>4. Subencargados</h2>
        <p>{CompanyName} puede utilizar subencargados (ej. proveedor de hosting, base de datos)
        bajo las mismas garantías establecidas en este acuerdo. El listado actualizado está
        disponible bajo solicitud a <a href="mailto:{ContactEmail}">{ContactEmail}</a>.</p>

        <h2>5. Auditoría</h2>
        <p>El Cliente puede solicitar auditorías o certificaciones de seguridad con preaviso
        de 30 días, a su propio costo.</p>
        """;
}
