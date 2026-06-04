using SkiaSharp;

namespace NeoSTP.Tests.Unit.Dte;

/// <summary>Helper para generar imágenes PNG de prueba (logo/firma) en los tests de DTE.</summary>
internal static class SkiaDemo
{
    public static byte[] Imagen(int w, int h, string texto, string color = "#FFFFFF", bool italic = false)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        surface.Canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint
        {
            Color = SKColor.Parse(color), IsAntialias = true, TextSize = h * 0.5f, TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Arial", italic ? SKFontStyle.Italic : SKFontStyle.Bold),
        };
        surface.Canvas.DrawText(texto, w / 2f, h * 0.65f, paint);
        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
