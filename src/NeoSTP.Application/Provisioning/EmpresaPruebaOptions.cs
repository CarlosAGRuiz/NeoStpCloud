namespace NeoSTP.Application.Provisioning;

/// <summary>
/// Configuración para el provisioning idempotente de la empresa de pruebas (Sprint 11).
/// Se lee de la sección "EmpresaPrueba" de appsettings.Local.json.
///
/// El seeder solo corre si <see cref="Enabled"/> es true y la empresa (por NIT) no existe.
/// Datos sensibles (password MH, certificado PFX) NO se siembran aquí: se cargan
/// vía la UI /DteConfiguracion para que queden cifrados con DataProtection.
/// </summary>
public class EmpresaPruebaOptions
{
    public const string SectionName = "EmpresaPrueba";

    /// <summary>Si es false (default) el seeder no hace nada.</summary>
    public bool Enabled { get; set; }

    // ----- Datos fiscales de la empresa -----
    public string Nit { get; set; } = null!;
    public string? Nrc { get; set; }
    public string RazonSocial { get; set; } = null!;
    public string? NombreComercial { get; set; }
    public string? CodigoActividad { get; set; }
    public string? ActividadEconomica { get; set; }
    /// <summary>Código MH del departamento (CAT-012), ej. "06".</summary>
    public string? Departamento { get; set; }
    /// <summary>Código MH del municipio (CAT-013).</summary>
    public string? Municipio { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }

    /// <summary>Código del plan a asignar. Default ENTERPRISE (incluye todos los módulos).</summary>
    public string PlanCodigo { get; set; } = "ENTERPRISE";

    // ----- Usuario administrador de la empresa -----
    public AdminPrueba Admin { get; set; } = new();

    // ----- Sucursal Casa Matriz -----
    public SucursalPrueba Sucursal { get; set; } = new();

    // ----- Punto de venta principal -----
    public PuntoVentaPrueba PuntoVenta { get; set; } = new();

    // ----- Configuración DTE (sin secretos) -----
    public DtePrueba Dte { get; set; } = new();

    public class AdminPrueba
    {
        public string Username { get; set; } = "admin.prueba";
        public string Email { get; set; } = "admin@empresaprueba.local";
        public string Password { get; set; } = "ChangeMe!2026";
        public string NombreCompleto { get; set; } = "Administrador de Pruebas";
    }

    public class SucursalPrueba
    {
        public string Codigo { get; set; } = "0001";
        public string Nombre { get; set; } = "Casa Matriz";
        /// <summary>Catálogo TIPO_ESTABLECIMIENTO. CASA_MATRIZ → MH "01".</summary>
        public string TipoEstablecimientoCodigo { get; set; } = "CASA_MATRIZ";
        public string? CodigoEstablecimientoMh { get; set; }
    }

    public class PuntoVentaPrueba
    {
        public string Codigo { get; set; } = "0001";
        public string Nombre { get; set; } = "Principal";
        public string? CodigoPuntoVentaMh { get; set; }
    }

    public class DtePrueba
    {
        /// <summary>PRUEBAS o PRODUCCION.</summary>
        public string AmbienteCodigo { get; set; } = "PRUEBAS";
        /// <summary>Usuario del API de Hacienda (NIT del emisor en pruebas).</summary>
        public string? UsuarioMh { get; set; }
        public string TipoEstablecimientoCodigo { get; set; } = "CASA_MATRIZ";
        public string? CodigoEstablecimientoMh { get; set; }
        public string? CodigoPuntoVentaMh { get; set; }
    }
}
