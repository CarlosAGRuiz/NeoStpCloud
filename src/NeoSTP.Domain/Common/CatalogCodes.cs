namespace NeoSTP.Domain.Common;

/// <summary>
/// Códigos de catálogos del sistema. Estos códigos identifican el catálogo
/// dentro de Core_Catalogos y permiten consultar sus items por código.
/// </summary>
public static class CatalogCodes
{
    public const string EstadoGenerico = "ESTADO_GENERICO";
    public const string EstadoUsuario = "ESTADO_USUARIO";
    public const string EstadoEmpresa = "ESTADO_EMPRESA";
    public const string EstadoFactura = "ESTADO_FACTURA";
    public const string EstadoPlan = "ESTADO_PLAN";

    public const string TipoFactura = "TIPO_FACTURA";
    public const string TipoDocumentoIdentidad = "TIPO_DOC_IDENTIDAD";
    public const string TipoContribuyente = "TIPO_CONTRIBUYENTE";
    public const string TipoEstablecimiento = "TIPO_ESTABLECIMIENTO";
    public const string TipoUsuario = "TIPO_USUARIO";
    public const string TipoMovimiento = "TIPO_MOVIMIENTO";

    public const string Moneda = "MONEDA";
    public const string FormaPago = "FORMA_PAGO";
    public const string CondicionOperacion = "CONDICION_OPERACION";
    public const string UnidadMedida = "UNIDAD_MEDIDA";
    public const string CanalVenta = "CANAL_VENTA";
    public const string AmbienteDte = "AMBIENTE_DTE";
    public const string DepartamentoEs = "DEPARTAMENTO_ES";
}

/// <summary>
/// Valores estándar de estados que se repiten en varios catálogos.
/// Usarlos como string permite ampliar sin migraciones.
/// </summary>
public static class EstadoCodes
{
    public const string Activo = "ACTIVO";
    public const string Inactivo = "INACTIVO";
    public const string Bloqueado = "BLOQUEADO";
    public const string PendienteActivacion = "PENDIENTE_ACTIVACION";
    public const string Suspendido = "SUSPENDIDO";
    public const string Vencido = "VENCIDO";
    public const string Eliminado = "ELIMINADO";
}
