using Microsoft.EntityFrameworkCore;
using NeoSTP.Infrastructure.Persistence;

namespace NeoSTP.Infrastructure.Dte;

/// <summary>Conserva la secuencia histórica empresa/tipo en ambos ambientes; nunca la reinicia.</summary>
public static class DteCorrelativoAllocator
{
    public static async Task<int> NextAsync(NeoStpDbContext db, int empresaId, string tipoDte, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("La reserva de correlativo requiere la transacción de creación del DTE.");

        // El lock de rango protege también una fila todavía inexistente. El historial establece
        // un piso adicional si alguien bajó/eliminó el contador. No se toca ningún documento.
        var values = await db.Database.SqlQuery<int>($"""
            DECLARE @actual bigint = 0, @historico bigint = 0;
            SELECT @actual = UltimoCorrelativo FROM Dte_Correlativos WITH (UPDLOCK, HOLDLOCK)
             WHERE EmpresaId = {empresaId} AND TipoDteCodigo = {tipoDte};
            SELECT @historico = COALESCE(MAX(TRY_CONVERT(bigint, RIGHT(NumeroControl, 15))), 0)
              FROM Dte_Documentos
             WHERE EmpresaId = {empresaId} AND TipoDteCodigo = {tipoDte};
            IF @historico > @actual SET @actual = @historico;
            IF @actual < 0 OR @actual >= 2147483647
                THROW 50002, 'Correlativo agotado o fuera del rango soportado. No reiniciar.', 1;
            IF EXISTS (SELECT 1 FROM Dte_Correlativos WHERE EmpresaId = {empresaId} AND TipoDteCodigo = {tipoDte})
                UPDATE Dte_Correlativos SET UltimoCorrelativo = @actual + 1, ActualizadoAt = GETUTCDATE()
                 WHERE EmpresaId = {empresaId} AND TipoDteCodigo = {tipoDte};
            ELSE
                INSERT INTO Dte_Correlativos (EmpresaId, TipoDteCodigo, UltimoCorrelativo, ActualizadoAt)
                VALUES ({empresaId}, {tipoDte}, @actual + 1, GETUTCDATE());
            SELECT CONVERT(int, @actual + 1) AS Value;
            """).ToListAsync(ct);
        return values.Single();
    }
}
