namespace NeoSTP.Application.Workers;

/// <summary>
/// Servicio de limpieza periódica de refresh tokens vencidos o revocados.
/// Ejecutado por <c>LimpiezaTokensWorker</c>.
/// </summary>
public interface ILimpiezaTokensService
{
    /// <summary>
    /// Elimina tokens cuya expiración o revocación supera el período de retención configurado.
    /// </summary>
    /// <returns>Número de tokens eliminados.</returns>
    Task<int> LimpiarTokensVencidosAsync(CancellationToken ct = default);
}
