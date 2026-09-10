using System.Buffers.Binary;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace NeoSTP.Infrastructure.Branding;

/// <summary>Bounded, local-only raster validation shared by uploads and legacy PDF branding.</summary>
public static class BrandingImageValidator
{
    public const int MaxBytes = 1_048_576;
    public const int MaxDimension = 4096;
    public const long MaxPixels = 4_000_000;

    public static bool TryValidate(byte[]? bytes, string? declaredType, out string contentType, out string error)
    {
        contentType = "";
        error = "El archivo está vacío.";
        if (bytes is not { Length: > 0 }) return false;
        error = "La imagen excede el máximo de 1024 KB.";
        if (bytes.Length > MaxBytes) return false;
        error = "La imagen no es un PNG, JPG o WEBP estático válido, o supera 4096 píxeles por lado / 4 megapíxeles.";
        if (!ReadDimensions(bytes, out var width, out var height, out contentType)
            || !AllowedSize(width, height)) return false;
        var mime = declaredType?.Trim().ToLowerInvariant();
        if (mime == "image/jpg") mime = "image/jpeg";
        if (mime is not null && mime != contentType)
        {
            error = "El formato real de la imagen no coincide con el tipo enviado. Use PNG, JPG o WEBP.";
            return false;
        }
        try
        {
            // Header bounds are checked BEFORE invoking the native decoder. The same decoder
            // and drawing engine used by the invoice must be able to rasterize the complete image.
            QuestPDF.Settings.License ??= LicenseType.Community;
            using var image = Image.FromBinaryData(bytes);
            _ = Document.Create(c => c.Page(p => {
                p.Size(64, 64); p.Margin(0); p.Content().Image(image).FitArea();
            })).GenerateImages().Single();
            error = "";
            return true;
        }
        catch (Exception ex) when (ex is DocumentComposeException or DocumentDrawingException
            or ArgumentException or InvalidOperationException)
        {
            error = "No se pudo decodificar la imagen. El archivo está dañado o incompleto.";
            return false;
        }
    }

    public static byte[]? SafeForPdf(byte[]? bytes)
        => TryValidate(bytes, null, out _, out _) ? bytes : null;

    private static bool AllowedSize(int width, int height)
        => width > 0 && height > 0 && width <= MaxDimension && height <= MaxDimension
            && (long)width * height <= MaxPixels;

    private static bool ReadDimensions(ReadOnlySpan<byte> data, out int width, out int height, out string type)
    {
        width = height = 0; type = "";
        if (data.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            type = "image/png";
            if (data.Length < 33 || BinaryPrimitives.ReadUInt32BigEndian(data[8..]) != 13
                || !data.Slice(12, 4).SequenceEqual("IHDR"u8)) return false;
            width = unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(data[16..]));
            height = unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(data[20..]));
            var offset = 8;
            var imageData = false;
            while (offset <= data.Length - 12)
            {
                var length = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
                if (length > data.Length - offset - 12) return false;
                var kind = data.Slice(offset + 4, 4);
                if (kind.SequenceEqual("acTL"u8)) return false; // APNG is not an invoice logo/signature.
                if (kind.SequenceEqual("IDAT"u8)) imageData = true;
                offset += checked((int)length + 12);
                if (kind.SequenceEqual("IEND"u8)) return length == 0 && imageData && offset == data.Length;
            }
            return false;
        }
        if (data.Length >= 4 && data[0] == 0xff && data[1] == 0xd8)
        {
            type = "image/jpeg";
            if (data[^2] != 0xff || data[^1] != 0xd9) return false;
            var offset = 2;
            while (offset < data.Length - 1)
            {
                if (data[offset++] != 0xff) return false;
                while (offset < data.Length && data[offset] == 0xff) offset++;
                if (offset >= data.Length) return false;
                var marker = data[offset++];
                if (marker is 0xda or 0xd9) break; // SOS/EOI, dimensions must already be available.
                if (marker is 0x01 or >= 0xd0 and <= 0xd7) continue;
                if (offset > data.Length - 2) return false;
                var length = BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);
                if (length < 2 || length > data.Length - offset) return false;
                if (marker is >= 0xc0 and <= 0xcf && marker is not (0xc4 or 0xc8 or 0xcc))
                {
                    if (length < 8) return false;
                    var h = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 3)..]);
                    var w = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 5)..]);
                    if (!AllowedSize(w, h)) return false;
                    width = w; height = h;
                }
                offset += length;
            }
            return width > 0;
        }
        if (data.Length < 20 || !data[..4].SequenceEqual("RIFF"u8)
            || !data.Slice(8, 4).SequenceEqual("WEBP"u8)) return false;
        type = "image/webp";
        if (BinaryPrimitives.ReadUInt32LittleEndian(data[4..]) != data.Length - 8) return false;
        var position = 12;
        var hasImage = false;
        while (position <= data.Length - 8)
        {
            var kind = data.Slice(position, 4);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(data[(position + 4)..]);
            if (length > data.Length - position - 8) return false;
            var chunk = data.Slice(position + 8, (int)length);
            var w = 0; var h = 0;
            if (kind.SequenceEqual("ANIM"u8) || kind.SequenceEqual("ANMF"u8)) return false;
            if (kind.SequenceEqual("VP8X"u8))
            {
                if (length != 10 || (chunk[0] & 2) != 0) return false;
                w = 1 + U24(chunk[4..]); h = 1 + U24(chunk[7..]);
            }
            else if (kind.SequenceEqual("VP8 "u8))
            {
                if (hasImage || length < 10 || !chunk.Slice(3, 3).SequenceEqual(new byte[] { 0x9d, 1, 0x2a })) return false;
                w = BinaryPrimitives.ReadUInt16LittleEndian(chunk[6..]) & 0x3fff;
                h = BinaryPrimitives.ReadUInt16LittleEndian(chunk[8..]) & 0x3fff;
                hasImage = true;
            }
            else if (kind.SequenceEqual("VP8L"u8))
            {
                if (hasImage || length < 5 || chunk[0] != 0x2f) return false;
                var bits = BinaryPrimitives.ReadUInt32LittleEndian(chunk[1..]);
                w = (int)(bits & 0x3fff) + 1; h = (int)((bits >> 14) & 0x3fff) + 1;
                hasImage = true;
            }
            if (w > 0 || h > 0)
            {
                if (!AllowedSize(w, h)) return false;
                width = w; height = h;
            }
            position += checked(8 + (int)length + (int)(length & 1));
        }
        return hasImage && position == data.Length;
    }

    private static int U24(ReadOnlySpan<byte> data) => data[0] | data[1] << 8 | data[2] << 16;
}
