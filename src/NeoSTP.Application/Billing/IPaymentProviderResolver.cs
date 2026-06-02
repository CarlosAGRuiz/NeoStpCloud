namespace NeoSTP.Application.Billing;

/// <summary>
/// Resuelve el <see cref="IPaymentProvider"/> concreto por nombre de método de pago.
/// Permite que el cliente elija método en el checkout (Stripe, MercadoPago, Wompi,
/// PayPal, Transferencia, …) en lugar de un único proveedor global.
/// </summary>
public interface IPaymentProviderResolver
{
    /// <summary>Proveedor por defecto (sección Billing:Provider).</summary>
    string DefaultProvider { get; }

    /// <summary>Nombres de los métodos de pago disponibles (proveedores registrados).</summary>
    IReadOnlyList<string> Disponibles { get; }

    /// <summary>
    /// Devuelve el proveedor para el método indicado; si es null/desconocido cae al
    /// proveedor por defecto, y si tampoco existe al primero registrado (Mock).
    /// </summary>
    IPaymentProvider Resolve(string? metodo = null);
}
