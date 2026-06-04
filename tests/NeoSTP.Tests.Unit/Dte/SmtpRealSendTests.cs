using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeoSTP.Application.Dte;
using NeoSTP.Application.Dte.Abstractions;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Infrastructure.Dte;
using NeoSTP.Infrastructure.Services;
using SkiaSharp;
using Xunit;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>
/// Envío REAL de un correo de prueba con factura PDF + JSON + cuerpo con cuadro de datos.
/// Se ejecuta solo si están definidas las variables de entorno con las credenciales SMTP
/// (NEOSTP_SMTP_USER / NEOSTP_SMTP_PASS / NEOSTP_SMTP_TO); de lo contrario se omite.
/// Las credenciales nunca se versionan: se pasan por entorno al correr el test.
/// </summary>
public class SmtpRealSendTests
{
    private static DteDocumento DemoFactura()
    {
        var d = new DteDocumento
        {
            TipoDteCodigo = TipoDteCodigos.ComprobanteCreditoFiscal,
            VersionDte = 3, AmbienteCodigo = "PRUEBAS",
            NumeroControl = "DTE-03-00010001-000000000000123",
            CodigoGeneracion = "B5F1C2A3-9D4E-4F6A-8B7C-1234567890AB",
            SelloRecibido = "2025ABCD1234EF5678901234567890ABCDEF1234",
            FechaEmision = new DateTime(2026, 6, 3), HoraEmision = new TimeSpan(10, 35, 0),
            EstadoCodigo = DteEstadoCodigos.Procesado, CondicionOperacionCodigo = "1",
            ReceptorNombre = "Comercial Los Andes, S.A. de C.V.",
            ReceptorTipoDocumento = "36", ReceptorNumeroDocumento = "0614-050505-102-3", ReceptorNrc = "987654-3",
            ReceptorCodigoActividad = "46900", ReceptorActividadEconomica = "Venta al por mayor",
            ReceptorDireccion = "Santa Tecla, La Libertad", ReceptorCorreo = "compras@losandes.com.sv",
            ReceptorTelefono = "2233-4455",
            Observaciones = "Entrega en bodega central. Pago a 30 días.",
            Empresa = new Empresa
            {
                Id = 1, RazonSocial = "Distribuidora El Salvador, S.A. de C.V.", NombreComercial = "DISAL",
                Nit = "0614-010101-001-2", Nrc = "123456-7", CodigoActividad = "47190",
                ActividadEconomica = "Venta al por menor en comercios no especializados",
                Direccion = "Col. Escalón, Av. Las Palmas #123, San Salvador", Telefono = "2222-3333",
                Correo = "ventas@disal.com.sv",
                LogoBlob = DemoImg(220, 90, "DISAL", SKColors.Transparent, SKColors.White), LogoContentType = "image/png",
                FirmaBlob = DemoImg(240, 80, "Juan Pérez", SKColors.Transparent, new SKColor(0x1E, 0x29, 0x3B), italic: true), FirmaContentType = "image/png",
                FirmaTexto = "Firma autorizada — Juan Pérez / Gerente General",
            },
        };
        d.Detalles.Add(new DteDocumentoDetalle { NumeroLinea = 1, Codigo = "PRD-001", Descripcion = "Caja de papel bond carta (10 resmas)", Cantidad = 5m, PrecioUnitario = 45.5000m });
        d.Detalles.Add(new DteDocumentoDetalle { NumeroLinea = 2, Codigo = "PRD-002", Descripcion = "Tóner negro compatible HP 26A", Cantidad = 8m, PrecioUnitario = 62.7500m, MontoDescuento = 10m });
        d.Detalles.Add(new DteDocumentoDetalle { NumeroLinea = 3, Codigo = "SRV-010", Descripcion = "Servicio de instalación y configuración", Cantidad = 1m, PrecioUnitario = 280.50m });
        new DteCalculator().Recalcular(d);
        return d;
    }

    [Fact]
    public async Task EnviaFacturaDemo_PorGmail()
    {
        var user = Environment.GetEnvironmentVariable("NEOSTP_SMTP_USER");
        var pass = Environment.GetEnvironmentVariable("NEOSTP_SMTP_PASS");
        var to = Environment.GetEnvironmentVariable("NEOSTP_SMTP_TO");
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass) || string.IsNullOrWhiteSpace(to))
            return; // omitido: sin credenciales en el entorno

        var d = DemoFactura();
        var pdf = new DtePdfService().Generar(d);
        var tieneLogo = d.Empresa!.LogoBlob is { Length: > 0 };
        var bodyHtml = DteDocumentosService.BuildBody(d, d.Empresa!.RazonSocial, tieneLogo);

        var jsonDemo = "{\"identificacion\":{\"tipoDte\":\"03\",\"numeroControl\":\"" + d.NumeroControl +
                       "\",\"codigoGeneracion\":\"" + d.CodigoGeneracion + "\"},\"resumen\":{\"totalPagar\":" +
                       d.TotalPagar.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}}";

        var options = Options.Create(new EmailOptions
        {
            Provider = "Smtp",
            From = new EmailFromOptions { Address = user, DisplayName = d.Empresa!.RazonSocial },
            Smtp = new SmtpEmailOptions { Host = "smtp.gmail.com", Port = 587, UseStartTls = true, Username = user, Password = pass },
        });
        var sender = new SmtpEmailSender(options, NullLogger<SmtpEmailSender>.Instance);

        var msg = new EmailMessage
        {
            To = to,
            Subject = $"Factura electrónica {d.NumeroControl} · {d.Empresa!.RazonSocial}",
            HtmlBody = bodyHtml,
            Attachments =
            {
                new EmailAttachment { FileName = "factura.pdf", MediaType = "application/pdf", Content = pdf },
                new EmailAttachment { FileName = "factura.json", MediaType = "application/json", Content = Encoding.UTF8.GetBytes(jsonDemo) },
            },
        };
        if (tieneLogo)
            msg.InlineImages.Add(new EmailInlineImage { ContentId = "logo", MediaType = "image/png", Content = d.Empresa!.LogoBlob! });

        var r = await sender.EnviarAsync(msg);
        r.Success.Should().BeTrue(r.Detalle);
    }

    private static byte[] DemoImg(int w, int h, string texto, SKColor fondo, SKColor color, bool italic = false)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        surface.Canvas.Clear(fondo);
        using var paint = new SKPaint
        {
            Color = color, IsAntialias = true, TextSize = h * 0.5f, TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", italic ? SKFontStyle.Italic : SKFontStyle.Bold),
        };
        surface.Canvas.DrawText(texto, w / 2f, h * 0.65f, paint);
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
