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
    public const string MunicipioEs = "MUNICIPIO_ES";

    // ---- Sprint 13.5 — catálogos MH prioritarios ----
    /// <summary>CAT-005 Tipo de Contingencia (Hacienda).</summary>
    public const string TipoContingencia = "TIPO_CONTINGENCIA";
    /// <summary>CAT-015 Tributos (Hacienda).</summary>
    public const string Tributo = "TRIBUTO";
    /// <summary>CAT-019 Actividad Económica (Hacienda).</summary>
    public const string ActividadEconomica = "ACTIVIDAD_ECONOMICA";
    /// <summary>CAT-020 País (Hacienda).</summary>
    public const string Pais = "PAIS";
    /// <summary>CAT-024 Motivo de Invalidación (Hacienda).</summary>
    public const string MotivoInvalidacion = "MOTIVO_INVALIDACION";
    /// <summary>CAT-008 Distrito (Hacienda) — populado por importación.</summary>
    public const string DistritoEs = "DISTRITO_ES";

    // ---- Sprint 13.7 — Catálogos MH oficiales (Manual v1.4) ----
    /// <summary>CAT-006 Retención IVA MH (Codigo = codigoMH).</summary>
    public const string RetencionIvaMh = "RETENCION_IVA_MH";
    /// <summary>CAT-018 Plazo (Codigo = codigoMH).</summary>
    public const string Plazo = "PLAZO";
    /// <summary>CAT-021 Otros Documentos Asociados (Codigo = codigoMH).</summary>
    public const string OtroDocAsociado = "OTRO_DOC_ASOCIADO";
    /// <summary>CAT-023 Tipo Documento en Contingencia (Codigo = codigoMH).</summary>
    public const string TipoDocContingencia = "TIPO_DOC_CONTINGENCIA";
    /// <summary>CAT-025 Título a que se Remiten los Bienes (Codigo = codigoMH).</summary>
    public const string TituloRemision = "TITULO_REMISION";
    /// <summary>CAT-026 Tipo de Donación (Codigo = codigoMH).</summary>
    public const string TipoDonacion = "TIPO_DONACION";
    /// <summary>CAT-027 Recinto Fiscal (Codigo = codigoMH).</summary>
    public const string RecintoFiscal = "RECINTO_FISCAL";
    /// <summary>CAT-029 Tipo de Persona (Codigo = codigoMH).</summary>
    public const string TipoPersona = "TIPO_PERSONA";
    /// <summary>CAT-030 Transporte (Codigo = codigoMH).</summary>
    public const string Transporte = "TRANSPORTE";
    /// <summary>CAT-031 INCOTERMS (Codigo = codigoMH).</summary>
    public const string Incoterms = "INCOTERMS";
    /// <summary>CAT-032 Domicilio Fiscal (Codigo = codigoMH).</summary>
    public const string DomicilioFiscal = "DOMICILIO_FISCAL";
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
