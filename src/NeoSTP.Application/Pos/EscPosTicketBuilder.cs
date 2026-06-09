using System.Text;

namespace NeoSTP.Application.Pos;

/// <summary>
/// Construye el flujo de bytes ESC/POS de un ticket para impresoras térmicas de red.
/// Puro y testeable. Usa comandos estándar Epson/Star (init, alineación, negrita, corte).
/// </summary>
public static class EscPosTicketBuilder
{
    // Comandos ESC/POS
    private static readonly byte[] Init = [0x1B, 0x40];                 // ESC @
    private static readonly byte[] AlignLeft = [0x1B, 0x61, 0x00];
    private static readonly byte[] AlignCenter = [0x1B, 0x61, 0x01];
    private static readonly byte[] BoldOn = [0x1B, 0x45, 0x01];
    private static readonly byte[] BoldOff = [0x1B, 0x45, 0x00];
    private static readonly byte[] DoubleOn = [0x1D, 0x21, 0x11];        // GS ! (doble alto+ancho)
    private static readonly byte[] DoubleOff = [0x1D, 0x21, 0x00];
    private static readonly byte[] FullCut = [0x1D, 0x56, 0x00];        // GS V 0
    private static readonly byte[] FeedNoCut = [0x0A, 0x0A, 0x0A];

    public static byte[] Build(TicketModel t)
    {
        // Latin1 cubre acentos básicos en la mayoría de impresoras (CP1252-compatible).
        var enc = Encoding.Latin1;
        var cols = t.AnchoMm <= 58 ? 32 : 42;
        var money = t.MonedaSimbolo;
        var buf = new List<byte>();

        void Raw(byte[] b) => buf.AddRange(b);
        void Line(string s = "") { buf.AddRange(enc.GetBytes(s)); buf.Add(0x0A); }
        string Pair(string left, string right)
        {
            var space = cols - left.Length - right.Length;
            if (space < 1) space = 1;
            return left + new string(' ', space) + right;
        }
        string Sep() => new('-', cols);

        Raw(Init);

        // Encabezado
        Raw(AlignCenter);
        Raw(BoldOn); Raw(DoubleOn);
        Line(t.EmpresaNombre);
        Raw(DoubleOff); Raw(BoldOff);
        if (!string.IsNullOrWhiteSpace(t.EmpresaNit)) Line($"NIT: {t.EmpresaNit}");
        if (!string.IsNullOrWhiteSpace(t.EmpresaNrc)) Line($"NRC: {t.EmpresaNrc}");
        if (!string.IsNullOrWhiteSpace(t.Direccion)) Line(t.Direccion!);
        if (!string.IsNullOrWhiteSpace(t.Telefono)) Line($"Tel: {t.Telefono}");

        Raw(AlignLeft);
        Line(Sep());
        Line($"Ticket: {t.Numero}");
        Line($"Fecha : {t.Fecha.ToLocalTime():dd/MM/yyyy HH:mm}");
        Line($"Cliente: {t.ClienteNombre}");
        if (t.EstadoCodigo == "ANULADA") { Raw(AlignCenter); Raw(BoldOn); Line("*** ANULADA ***"); Raw(BoldOff); Raw(AlignLeft); }
        Line(Sep());

        // Ítems
        foreach (var l in t.Lineas)
        {
            Line(l.Descripcion.Length > cols ? l.Descripcion[..cols] : l.Descripcion);
            Line(Pair($"  {l.Cantidad:0.##} x {money}{l.PrecioUnitario:N2}", $"{money}{l.Total:N2}"));
        }
        Line(Sep());

        // Totales
        if (t.TotalDescuento > 0) Line(Pair("Descuento", $"{money}{t.TotalDescuento:N2}"));
        Line(Pair("Subtotal", $"{money}{t.Subtotal:N2}"));
        Line(Pair("IVA", $"{money}{t.IvaTotal:N2}"));
        Raw(BoldOn);
        Line(Pair("TOTAL", $"{money}{t.Total:N2}"));
        Raw(BoldOff);
        Line($"Pago: {t.FormaPago}");
        if (t.EfectivoRecibido is decimal rec)
        {
            Line(Pair("Efectivo", $"{money}{rec:N2}"));
            Line(Pair("Cambio", $"{money}{t.Cambio ?? 0m:N2}"));
        }
        if (!string.IsNullOrWhiteSpace(t.Nota)) { Line(Sep()); Line(t.Nota!); }

        Line(Sep());
        Raw(AlignCenter);
        Raw(BoldOn); Line(t.PieTicket); Raw(BoldOff);
        Line("Comprobante no fiscal");

        Raw(FeedNoCut);
        Raw(FullCut);
        return buf.ToArray();
    }
}
