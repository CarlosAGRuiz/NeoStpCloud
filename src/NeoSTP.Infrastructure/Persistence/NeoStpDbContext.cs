using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Core.Auditoria;
using NeoSTP.Domain.Core.Catalogos;
using NeoSTP.Domain.Core.Clientes;
using NeoSTP.Domain.Core.Cobranza;
using NeoSTP.Domain.Core.Connect;
using NeoSTP.Domain.Core.Crm;
using NeoSTP.Domain.Core.Notificaciones;
using NeoSTP.Domain.Core.Profit;
using NeoSTP.Domain.Core.Rrhh;
using NeoSTP.Domain.Core.Scan;
using NeoSTP.Domain.Core.Dte;
using NeoSTP.Domain.Core.Dte.Certificacion;
using NeoSTP.Domain.Core.Dte.Contingencia;
using NeoSTP.Domain.Core.Dte.Diagnostico;
using NeoSTP.Domain.Core.Dte.Eventos;
using NeoSTP.Domain.Core.Empresas;
using NeoSTP.Domain.Core.Billing;
using NeoSTP.Domain.Core.Legal;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Ops;
using NeoSTP.Domain.Core.Productos;
using NeoSTP.Domain.Core.Seguridad;
using NeoSTP.Infrastructure.Persistence.Seed;

namespace NeoSTP.Infrastructure.Persistence;

public class NeoStpDbContext : DbContext
{
    public NeoStpDbContext(DbContextOptions<NeoStpDbContext> options) : base(options)
    {
    }

    // CatÃ¡logos
    public DbSet<Catalogo> Catalogos => Set<Catalogo>();
    public DbSet<CatalogoItem> CatalogoItems => Set<CatalogoItem>();

    // Empresas
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<PuntoVenta> PuntosVenta => Set<PuntoVenta>();

    // Seguridad
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Licenciamiento
    public DbSet<Modulo> Modulos => Set<Modulo>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<PlanModulo> PlanModulos => Set<PlanModulo>();
    public DbSet<EmpresaPlan> EmpresaPlanes => Set<EmpresaPlan>();
    public DbSet<EmpresaModulo> EmpresaModulos => Set<EmpresaModulo>();

    // DTE - Clientes, Productos y ConfiguraciÃ³n
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<DteConfiguracion> DteConfiguracion => Set<DteConfiguracion>();

    // DTE - Documentos
    public DbSet<DteCorrelativo> DteCorrelativos => Set<DteCorrelativo>();
    public DbSet<DteDocumento> DteDocumentos => Set<DteDocumento>();
    public DbSet<DteDocumentoDetalle> DteDocumentoDetalles => Set<DteDocumentoDetalle>();
    public DbSet<DteDocumentoJson> DteDocumentoJson => Set<DteDocumentoJson>();

    // DTE - CertificaciÃ³n (Sprint 14)
    public DbSet<CertificacionMatriz> CertificacionMatriz => Set<CertificacionMatriz>();
    public DbSet<CertificacionEscenario> CertificacionEscenarios => Set<CertificacionEscenario>();
    public DbSet<CertificacionPrueba> CertificacionPruebas => Set<CertificacionPrueba>();
    public DbSet<CertificacionError> CertificacionErrores => Set<CertificacionError>();

    // DTE - Eventos persistentes (Sprint 15)
    public DbSet<DteEvento> DteEventos => Set<DteEvento>();
    public DbSet<DteEventoJson> DteEventoJson => Set<DteEventoJson>();
    public DbSet<DteEventoRespuestaHacienda> DteEventoRespuestas => Set<DteEventoRespuestaHacienda>();
    public DbSet<DteEventoDocumentoRelacionado> DteEventoDocumentosRelacionados => Set<DteEventoDocumentoRelacionado>();

    // DTE - Lotes de contingencia (Sprint 16)
    public DbSet<DteContingenciaLote> DteContingenciaLotes => Set<DteContingenciaLote>();
    public DbSet<DteContingenciaLoteDetalle> DteContingenciaLoteDetalles => Set<DteContingenciaLoteDetalle>();

    // DTE - DiagnÃ³stico de errores (Sprint 17)
    public DbSet<DteErrorCatalogo> DteErrorCatalogo => Set<DteErrorCatalogo>();
    public DbSet<DteErrorOcurrencia> DteErrorOcurrencias => Set<DteErrorOcurrencia>();

    // Legal â€” consentimiento (Sprint 18)
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();

    // Billing â€” self-service (Sprint 19)
    public DbSet<BillingCustomer> BillingCustomers => Set<BillingCustomer>();
    public DbSet<BillingSubscription> BillingSubscriptions => Set<BillingSubscription>();
    public DbSet<BillingPayment> BillingPayments => Set<BillingPayment>();
    public DbSet<BillingInvoice> BillingInvoices => Set<BillingInvoice>();
    public DbSet<BillingWebhookEvent> BillingWebhookEvents => Set<BillingWebhookEvent>();
    public DbSet<BillingPlanProviderMapping> BillingPlanProviderMappings => Set<BillingPlanProviderMapping>();

    // Hardening / OperaciÃ³n (Sprint 20)
    public DbSet<BackupJob> BackupJobs => Set<BackupJob>();
    public DbSet<ApiUsageLog> ApiUsageLogs => Set<ApiUsageLog>();
    public DbSet<ApiQuota> ApiQuotas => Set<ApiQuota>();
    public DbSet<AdminIpAllowlistEntry> AdminIpAllowlist => Set<AdminIpAllowlistEntry>();

    // NeoConnect API (Sprint 24)
    public DbSet<ConnectApiKey> ConnectApiKeys => Set<ConnectApiKey>();
    public DbSet<ConnectWebhook> ConnectWebhooks => Set<ConnectWebhook>();
    public DbSet<ConnectWebhookDelivery> ConnectWebhookDeliveries => Set<ConnectWebhookDelivery>();

    // NeoProfit (Sprint 22)
    public DbSet<ProfitGasto> ProfitGastos => Set<ProfitGasto>();
    public DbSet<ProfitCompra> ProfitCompras => Set<ProfitCompra>();

    // Cobranza / Cuentas por cobrar (B-2 NeoCloud Mobile)
    public DbSet<PagoCliente> PagosCliente => Set<PagoCliente>();
    public DbSet<CuentaCobro> CuentasCobro => Set<CuentaCobro>();
    public DbSet<RecordatorioCobro> RecordatoriosCobro => Set<RecordatorioCobro>();
    public DbSet<ConfigRecordatorioCobro> ConfigRecordatoriosCobro => Set<ConfigRecordatorioCobro>();

    // NEOCRM (V2-C1)
    public DbSet<ContactoCrm> ContactosCrm => Set<ContactoCrm>();
    public DbSet<EtapaPipelineCrm> EtapasPipelineCrm => Set<EtapaPipelineCrm>();
    public DbSet<OportunidadCrm> OportunidadesCrm => Set<OportunidadCrm>();
    public DbSet<ActividadCrm> ActividadesCrm => Set<ActividadCrm>();
    public DbSet<CotizacionCrm> CotizacionesCrm => Set<CotizacionCrm>();
    public DbSet<CotizacionCrmLinea> CotizacionLineasCrm => Set<CotizacionCrmLinea>();

    // NeoScanAI (Sprint 23 / B-3 NeoCloud Mobile)
    public DbSet<ScanDocumento> ScanDocumentos => Set<ScanDocumento>();
    public DbSet<DteDocumentoRecibido> DteDocumentosRecibidos => Set<DteDocumentoRecibido>();

    // NeoPortal receptor (V2-C2)
    public DbSet<NeoSTP.Domain.Core.Portal.PortalAcceso> PortalAccesos => Set<NeoSTP.Domain.Core.Portal.PortalAcceso>();

    // NEOCONTA (V2-D2)
    public DbSet<NeoSTP.Domain.Core.Conta.CuentaContable> CuentasContables => Set<NeoSTP.Domain.Core.Conta.CuentaContable>();
    public DbSet<NeoSTP.Domain.Core.Conta.AsientoContable> AsientosContables => Set<NeoSTP.Domain.Core.Conta.AsientoContable>();
    public DbSet<NeoSTP.Domain.Core.Conta.AsientoContableLinea> AsientoContableLineas => Set<NeoSTP.Domain.Core.Conta.AsientoContableLinea>();

    // Alertas y notificaciones (B-4 NeoCloud Mobile)
    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<DispositivoNotificacion> DispositivosNotificacion => Set<DispositivoNotificacion>();
    public DbSet<PreferenciaNotificacion> PreferenciasNotificacion => Set<PreferenciaNotificacion>();

    // RRHH / NÃ³mina (NEORRHH â€” V2)
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<ContratoLaboral> ContratosLaborales => Set<ContratoLaboral>();
    public DbSet<PlanillaPeriodo> PlanillaPeriodos => Set<PlanillaPeriodo>();
    public DbSet<PlanillaDetalle> PlanillaDetalles => Set<PlanillaDetalle>();
    public DbSet<PoliticaPrestaciones> PoliticasPrestaciones => Set<PoliticaPrestaciones>();
    public DbSet<SolicitudVacacion> SolicitudesVacacion => Set<SolicitudVacacion>();
    public DbSet<AguinaldoCalculo> AguinaldosCalculados => Set<AguinaldoCalculo>();

    // TesorerÃ­a (NEOTESORERIA â€” V2)
    public DbSet<NeoSTP.Domain.Core.Tesoreria.CuentaTesoreria> CuentasTesoreria => Set<NeoSTP.Domain.Core.Tesoreria.CuentaTesoreria>();
    public DbSet<NeoSTP.Domain.Core.Tesoreria.MovimientoTesoreria> MovimientosTesoreria => Set<NeoSTP.Domain.Core.Tesoreria.MovimientoTesoreria>();
    public DbSet<NeoSTP.Domain.Core.Tesoreria.MovimientoBancario> MovimientosBancarios => Set<NeoSTP.Domain.Core.Tesoreria.MovimientoBancario>();
    public DbSet<NeoSTP.Domain.Core.Tesoreria.ConciliacionDetalle> ConciliacionDetalles => Set<NeoSTP.Domain.Core.Tesoreria.ConciliacionDetalle>();

    // Compras / Cuentas por pagar (NEOCOMPRAS â€” V2)
    public DbSet<NeoSTP.Domain.Core.Compras.Proveedor> Proveedores => Set<NeoSTP.Domain.Core.Compras.Proveedor>();
    public DbSet<NeoSTP.Domain.Core.Compras.FacturaCompra> FacturasCompra => Set<NeoSTP.Domain.Core.Compras.FacturaCompra>();
    public DbSet<NeoSTP.Domain.Core.Compras.PagoProveedor> PagosProveedor => Set<NeoSTP.Domain.Core.Compras.PagoProveedor>();
    public DbSet<NeoSTP.Domain.Core.Compras.OrdenCompra> OrdenesCompra => Set<NeoSTP.Domain.Core.Compras.OrdenCompra>();
    public DbSet<NeoSTP.Domain.Core.Compras.OrdenCompraLinea> OrdenCompraLineas => Set<NeoSTP.Domain.Core.Compras.OrdenCompraLinea>();
    public DbSet<NeoSTP.Domain.Core.Compras.OrdenCompraRecepcion> OrdenCompraRecepciones => Set<NeoSTP.Domain.Core.Compras.OrdenCompraRecepcion>();
    public DbSet<NeoSTP.Domain.Core.Compras.OrdenCompraRecepcionLinea> OrdenCompraRecepcionLineas => Set<NeoSTP.Domain.Core.Compras.OrdenCompraRecepcionLinea>();

    // Punto de venta (NEOPOS â€” V2)
    public DbSet<NeoSTP.Domain.Core.Pos.VentaPos> VentasPos => Set<NeoSTP.Domain.Core.Pos.VentaPos>();
    public DbSet<NeoSTP.Domain.Core.Pos.VentaPosLinea> VentaPosLineas => Set<NeoSTP.Domain.Core.Pos.VentaPosLinea>();
    public DbSet<NeoSTP.Domain.Core.Pos.ImpresoraPos> ImpresorasPos => Set<NeoSTP.Domain.Core.Pos.ImpresoraPos>();
    public DbSet<NeoSTP.Domain.Core.Pos.SesionCaja> SesionesCaja => Set<NeoSTP.Domain.Core.Pos.SesionCaja>();

    // Comunicaciones (correo por empresa â€” V2)
    public DbSet<NeoSTP.Domain.Core.Comunicaciones.ConfiguracionCorreo> ConfiguracionesCorreo => Set<NeoSTP.Domain.Core.Comunicaciones.ConfiguracionCorreo>();

    // Inventario (INVENTARIO â€” V2)
    public DbSet<NeoSTP.Domain.Core.Inventario.ExistenciaProducto> ExistenciasProducto => Set<NeoSTP.Domain.Core.Inventario.ExistenciaProducto>();
    public DbSet<NeoSTP.Domain.Core.Inventario.MovimientoInventario> MovimientosInventario => Set<NeoSTP.Domain.Core.Inventario.MovimientoInventario>();

    // AuditorÃ­a
    public DbSet<Auditoria> Auditoria => Set<Auditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NeoStpDbContext).Assembly);
        SeedData.Apply(modelBuilder);
    }
}
