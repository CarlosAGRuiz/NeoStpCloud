using Microsoft.EntityFrameworkCore;
using NeoSTP.Domain.Common;
using NeoSTP.Domain.Core.Catalogos;
using NeoSTP.Domain.Core.Licenciamiento;
using NeoSTP.Domain.Core.Seguridad;

namespace NeoSTP.Infrastructure.Persistence.Seed;

/// <summary>
/// Datos semilla aplicados vía HasData en cada migración. Si modificas un valor existente
/// EF Core generará un UPDATE en la siguiente migración. Si agregas filas, generará INSERTs.
///
/// IDs reservados por bloque (no reusar):
///   Catalogos:           1 - 99
///   CatalogoItems:       1 - 999
///   Modulos:           100 - 199
///   Planes:            200 - 299
///   Permisos:          300 - 499
///   Roles del sistema: 500 - 599
/// </summary>
internal static class SeedData
{
    private static readonly DateTime SeededAt = new(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc);
    private const string SeededBy = "SYSTEM";

    public static void Apply(ModelBuilder modelBuilder)
    {
        SeedCatalogos(modelBuilder);
        SeedModulos(modelBuilder);
        SeedPlanes(modelBuilder);
        SeedPlanModulos(modelBuilder);
        SeedPermisos(modelBuilder);
        SeedRoles(modelBuilder);
        SeedRolPermisos(modelBuilder);
    }

    private static void SeedCatalogos(ModelBuilder modelBuilder)
    {
        var catalogos = new[]
        {
            Cat(1,  CatalogCodes.EstadoGenerico,        "Estado genérico",              "Activo / Inactivo para uso general"),
            Cat(2,  CatalogCodes.EstadoUsuario,         "Estado de usuario",            "Estados que puede tener un usuario"),
            Cat(3,  CatalogCodes.EstadoEmpresa,         "Estado de empresa",            "Estados del ciclo de vida de una empresa"),
            Cat(4,  CatalogCodes.EstadoFactura,         "Estado de factura/DTE",        "Estados del ciclo de vida de un DTE"),
            Cat(5,  CatalogCodes.EstadoPlan,            "Estado de plan",               "Estados de la suscripción de empresa a un plan"),
            Cat(6,  CatalogCodes.TipoFactura,           "Tipo de factura/DTE",          "Tipos de documentos tributarios electrónicos"),
            Cat(7,  CatalogCodes.TipoDocumentoIdentidad,"Tipo de documento de identidad","DUI, NIT, Pasaporte, etc."),
            Cat(8,  CatalogCodes.TipoContribuyente,     "Tipo de contribuyente",        "Consumidor final, contribuyente, gran contribuyente"),
            Cat(9,  CatalogCodes.TipoEstablecimiento,   "Tipo de establecimiento",      "Casa matriz, sucursal, bodega, etc."),
            Cat(10, CatalogCodes.TipoUsuario,           "Tipo de usuario",              "SuperAdmin, Admin, Operador, Contador, ReadOnly"),
            Cat(11, CatalogCodes.TipoMovimiento,        "Tipo de movimiento",           "Venta, compra, ajuste, transferencia, devolución"),
            Cat(12, CatalogCodes.Moneda,                "Moneda",                       "Monedas habilitadas para emisión y cobro"),
            Cat(13, CatalogCodes.FormaPago,             "Forma de pago",                "Formas de pago permitidas en documentos"),
            Cat(14, CatalogCodes.CondicionOperacion,    "Condición de operación",       "Contado, crédito u otro"),
            Cat(15, CatalogCodes.UnidadMedida,          "Unidad de medida",             "Unidades de medida para productos y servicios"),
            Cat(16, CatalogCodes.CanalVenta,            "Canal de venta",               "Origen del documento: POS, web, móvil, API, manual"),
            Cat(17, CatalogCodes.AmbienteDte,           "Ambiente DTE",                 "Ambientes Hacienda: pruebas y producción"),
            Cat(18, CatalogCodes.DepartamentoEs,        "Departamento (El Salvador)",   "14 departamentos de El Salvador con codigos MH CAT-012"),
            Cat(19, CatalogCodes.MunicipioEs,           "Municipio / Zona (El Salvador)","42 municipios post-reforma territorial 2024 (Decreto 290). Cada item lleva departamento padre + zona en metadata. Distribución base; ajustar contra CAT-013 final de Hacienda"),
        };

        modelBuilder.Entity<Catalogo>().HasData(catalogos);

        var items = new List<CatalogoItem>();
        int id = 1;

        // ESTADO_GENERICO (1)
        items.Add(Item(id++, 1, "ACTIVO", "Activo", 1));
        items.Add(Item(id++, 1, "INACTIVO", "Inactivo", 2));

        // ESTADO_USUARIO (2)
        items.Add(Item(id++, 2, "ACTIVO",                "Activo",                1));
        items.Add(Item(id++, 2, "INACTIVO",              "Inactivo",              2));
        items.Add(Item(id++, 2, "BLOQUEADO",             "Bloqueado",             3));
        items.Add(Item(id++, 2, "PENDIENTE_ACTIVACION",  "Pendiente activación",  4));
        items.Add(Item(id++, 2, "ELIMINADO",             "Eliminado",             5));

        // ESTADO_EMPRESA (3)
        items.Add(Item(id++, 3, "ACTIVA",     "Activa",     1));
        items.Add(Item(id++, 3, "INACTIVA",   "Inactiva",   2));
        items.Add(Item(id++, 3, "SUSPENDIDA", "Suspendida", 3));
        items.Add(Item(id++, 3, "VENCIDA",    "Vencida",    4));
        items.Add(Item(id++, 3, "ELIMINADA",  "Eliminada",  5));

        // ESTADO_FACTURA (4)
        items.Add(Item(id++, 4, "BORRADOR",     "Borrador",     1));
        items.Add(Item(id++, 4, "GENERADO",     "Generado",     2));
        items.Add(Item(id++, 4, "VALIDADO",     "Validado",     3));
        items.Add(Item(id++, 4, "FIRMADO",      "Firmado",      4));
        items.Add(Item(id++, 4, "ENVIADO",      "Enviado",      5));
        items.Add(Item(id++, 4, "PROCESADO",    "Procesado",    6));
        items.Add(Item(id++, 4, "RECHAZADO",    "Rechazado",    7));
        items.Add(Item(id++, 4, "CONTINGENCIA", "Contingencia", 8));
        items.Add(Item(id++, 4, "INVALIDADO",   "Invalidado",   9));
        items.Add(Item(id++, 4, "ERROR",        "Error",        10));

        // ESTADO_PLAN (5)
        items.Add(Item(id++, 5, "ACTIVO",     "Activo",     1));
        items.Add(Item(id++, 5, "VENCIDO",    "Vencido",    2));
        items.Add(Item(id++, 5, "SUSPENDIDO", "Suspendido", 3));
        items.Add(Item(id++, 5, "CANCELADO",  "Cancelado",  4));

        // TIPO_FACTURA (6) — códigos según catálogo MH CAT-002
        items.Add(Item(id++, 6, "FACTURA",                       "Factura electrónica",                        1, metadata: "{\"codigoMH\":\"01\"}"));
        items.Add(Item(id++, 6, "CCF",                           "Comprobante de Crédito Fiscal",              2, metadata: "{\"codigoMH\":\"03\"}"));
        items.Add(Item(id++, 6, "NOTA_REMISION",                 "Nota de Remisión",                           3, metadata: "{\"codigoMH\":\"04\"}"));
        items.Add(Item(id++, 6, "NOTA_CREDITO",                  "Nota de Crédito",                            4, metadata: "{\"codigoMH\":\"05\"}"));
        items.Add(Item(id++, 6, "NOTA_DEBITO",                   "Nota de Débito",                             5, metadata: "{\"codigoMH\":\"06\"}"));
        items.Add(Item(id++, 6, "COMPROBANTE_RETENCION",         "Comprobante de Retención",                   6, metadata: "{\"codigoMH\":\"07\"}"));
        items.Add(Item(id++, 6, "COMPROBANTE_LIQUIDACION",       "Comprobante de Liquidación",                 7, metadata: "{\"codigoMH\":\"08\"}"));
        items.Add(Item(id++, 6, "DOCUMENTO_CONTABLE_LIQUIDACION","Documento Contable de Liquidación",          8, metadata: "{\"codigoMH\":\"09\"}"));
        items.Add(Item(id++, 6, "FACTURA_EXPORTACION",           "Factura de Exportación",                     9, metadata: "{\"codigoMH\":\"11\"}"));
        items.Add(Item(id++, 6, "SUJETO_EXCLUIDO",               "Factura Sujeto Excluido",                    10, metadata: "{\"codigoMH\":\"14\"}"));
        items.Add(Item(id++, 6, "COMPROBANTE_DONACION",          "Comprobante de Donación",                    11, metadata: "{\"codigoMH\":\"15\"}"));

        // TIPO_DOC_IDENTIDAD (7)
        items.Add(Item(id++, 7, "DUI",              "DUI",                       1));
        items.Add(Item(id++, 7, "NIT",              "NIT",                       2));
        items.Add(Item(id++, 7, "PASAPORTE",        "Pasaporte",                 3));
        items.Add(Item(id++, 7, "CARNET_RESIDENTE", "Carnet de Residente",       4));
        items.Add(Item(id++, 7, "OTRO",             "Otro documento",            5));

        // TIPO_CONTRIBUYENTE (8)
        items.Add(Item(id++, 8, "CONSUMIDOR_FINAL",   "Consumidor Final",     1));
        items.Add(Item(id++, 8, "CONTRIBUYENTE",      "Contribuyente",        2));
        items.Add(Item(id++, 8, "GRAN_CONTRIBUYENTE", "Gran Contribuyente",   3));

        // TIPO_ESTABLECIMIENTO (9)
        items.Add(Item(id++, 9, "CASA_MATRIZ",   "Casa Matriz",   1, metadata: "{\"codigoMH\":\"01\"}"));
        items.Add(Item(id++, 9, "SUCURSAL",      "Sucursal",      2, metadata: "{\"codigoMH\":\"02\"}"));
        items.Add(Item(id++, 9, "BODEGA",        "Bodega",        3, metadata: "{\"codigoMH\":\"04\"}"));
        items.Add(Item(id++, 9, "PATIO",         "Patio o Predio",4, metadata: "{\"codigoMH\":\"07\"}"));
        items.Add(Item(id++, 9, "OFICINA",       "Oficina",       5));
        items.Add(Item(id++, 9, "OTRO",          "Otro",          6, metadata: "{\"codigoMH\":\"20\"}"));

        // TIPO_USUARIO (10)
        items.Add(Item(id++, 10, "SUPERADMIN", "SuperAdmin NeoSTP",   1));
        items.Add(Item(id++, 10, "ADMIN",      "Administrador",       2));
        items.Add(Item(id++, 10, "OPERADOR",   "Operador",            3));
        items.Add(Item(id++, 10, "CONTADOR",   "Contador",            4));
        items.Add(Item(id++, 10, "READONLY",   "Solo lectura",        5));

        // TIPO_MOVIMIENTO (11)
        items.Add(Item(id++, 11, "VENTA",         "Venta",            1));
        items.Add(Item(id++, 11, "COMPRA",        "Compra",           2));
        items.Add(Item(id++, 11, "AJUSTE",        "Ajuste",           3));
        items.Add(Item(id++, 11, "TRANSFERENCIA", "Transferencia",    4));
        items.Add(Item(id++, 11, "DEVOLUCION",    "Devolución",       5));

        // MONEDA (12)
        items.Add(Item(id++, 12, "USD", "Dólar estadounidense", 1, metadata: "{\"simbolo\":\"$\"}"));
        items.Add(Item(id++, 12, "EUR", "Euro",                 2, metadata: "{\"simbolo\":\"€\"}"));
        items.Add(Item(id++, 12, "MXN", "Peso mexicano",        3, metadata: "{\"simbolo\":\"$\"}"));
        items.Add(Item(id++, 12, "GTQ", "Quetzal guatemalteco", 4, metadata: "{\"simbolo\":\"Q\"}"));
        items.Add(Item(id++, 12, "HNL", "Lempira hondureño",    5, metadata: "{\"simbolo\":\"L\"}"));
        items.Add(Item(id++, 12, "NIO", "Córdoba nicaragüense", 6, metadata: "{\"simbolo\":\"C$\"}"));
        items.Add(Item(id++, 12, "CRC", "Colón costarricense",  7, metadata: "{\"simbolo\":\"₡\"}"));
        items.Add(Item(id++, 12, "PAB", "Balboa panameño",      8, metadata: "{\"simbolo\":\"B/.\"}"));
        items.Add(Item(id++, 12, "DOP", "Peso dominicano",      9, metadata: "{\"simbolo\":\"RD$\"}"));
        items.Add(Item(id++, 12, "COP", "Peso colombiano",      10, metadata: "{\"simbolo\":\"$\"}"));

        // FORMA_PAGO (13) — códigos Hacienda CAT-017
        items.Add(Item(id++, 13, "EFECTIVO",         "Efectivo",                1, metadata: "{\"codigoMH\":\"01\"}"));
        items.Add(Item(id++, 13, "TARJETA_DEBITO",   "Tarjeta de Débito",       2, metadata: "{\"codigoMH\":\"02\"}"));
        items.Add(Item(id++, 13, "TARJETA_CREDITO",  "Tarjeta de Crédito",      3, metadata: "{\"codigoMH\":\"03\"}"));
        items.Add(Item(id++, 13, "CHEQUE",           "Cheque",                  4, metadata: "{\"codigoMH\":\"04\"}"));
        items.Add(Item(id++, 13, "TRANSFERENCIA",    "Transferencia bancaria",  5, metadata: "{\"codigoMH\":\"05\"}"));
        items.Add(Item(id++, 13, "VALE",             "Vale o cupón",            6, metadata: "{\"codigoMH\":\"06\"}"));
        items.Add(Item(id++, 13, "BITCOIN",          "Bitcoin",                 7, metadata: "{\"codigoMH\":\"07\"}"));
        items.Add(Item(id++, 13, "OTRAS_CRIPTO",     "Otras criptomonedas",     8, metadata: "{\"codigoMH\":\"08\"}"));
        items.Add(Item(id++, 13, "CREDITO",          "A crédito",               9, metadata: "{\"codigoMH\":\"99\"}"));
        items.Add(Item(id++, 13, "OTRO",             "Otro",                    10));

        // CONDICION_OPERACION (14) — CAT-016 Hacienda
        items.Add(Item(id++, 14, "CONTADO", "Contado", 1, metadata: "{\"codigoMH\":\"1\"}"));
        items.Add(Item(id++, 14, "CREDITO", "Crédito", 2, metadata: "{\"codigoMH\":\"2\"}"));
        items.Add(Item(id++, 14, "OTRO",    "Otro",    3, metadata: "{\"codigoMH\":\"3\"}"));

        // UNIDAD_MEDIDA (15) — subset CAT-014 Hacienda
        items.Add(Item(id++, 15, "UNIDAD",    "Unidad",          1, metadata: "{\"codigoMH\":\"59\"}"));
        items.Add(Item(id++, 15, "CAJA",      "Caja",            2, metadata: "{\"codigoMH\":\"43\"}"));
        items.Add(Item(id++, 15, "DOCENA",    "Docena",          3, metadata: "{\"codigoMH\":\"44\"}"));
        items.Add(Item(id++, 15, "LITRO",     "Litro",           4, metadata: "{\"codigoMH\":\"22\"}"));
        items.Add(Item(id++, 15, "GALON",     "Galón",           5, metadata: "{\"codigoMH\":\"23\"}"));
        items.Add(Item(id++, 15, "KILOGRAMO", "Kilogramo",       6, metadata: "{\"codigoMH\":\"10\"}"));
        items.Add(Item(id++, 15, "LIBRA",     "Libra",           7, metadata: "{\"codigoMH\":\"15\"}"));
        items.Add(Item(id++, 15, "METRO",     "Metro",           8, metadata: "{\"codigoMH\":\"32\"}"));
        items.Add(Item(id++, 15, "SERVICIO",  "Servicio",        9, metadata: "{\"codigoMH\":\"58\"}"));
        items.Add(Item(id++, 15, "HORA",      "Hora",            10, metadata: "{\"codigoMH\":\"56\"}"));
        items.Add(Item(id++, 15, "DIA",       "Día",             11, metadata: "{\"codigoMH\":\"55\"}"));
        items.Add(Item(id++, 15, "PAQUETE",   "Paquete",         12, metadata: "{\"codigoMH\":\"49\"}"));

        // CANAL_VENTA (16)
        items.Add(Item(id++, 16, "POS",    "Punto de Venta", 1));
        items.Add(Item(id++, 16, "WEB",    "Web",            2));
        items.Add(Item(id++, 16, "MOBILE", "Móvil",          3));
        items.Add(Item(id++, 16, "API",    "API/Integración",4));
        items.Add(Item(id++, 16, "MANUAL", "Manual",         5));

        // AMBIENTE_DTE (17)
        items.Add(Item(id++, 17, "PRUEBAS",    "Pruebas",    1, metadata: "{\"codigoMH\":\"00\"}"));
        items.Add(Item(id++, 17, "PRODUCCION", "Producción", 2, metadata: "{\"codigoMH\":\"01\"}"));

        // DEPARTAMENTO_ES (18) — CAT-012 Hacienda
        items.Add(Item(id++, 18, "AHUACHAPAN",   "Ahuachapán",   1,  metadata: "{\"codigoMH\":\"01\"}"));
        items.Add(Item(id++, 18, "SANTA_ANA",    "Santa Ana",    2,  metadata: "{\"codigoMH\":\"02\"}"));
        items.Add(Item(id++, 18, "SONSONATE",    "Sonsonate",    3,  metadata: "{\"codigoMH\":\"03\"}"));
        items.Add(Item(id++, 18, "CHALATENANGO", "Chalatenango", 4,  metadata: "{\"codigoMH\":\"04\"}"));
        items.Add(Item(id++, 18, "LA_LIBERTAD",  "La Libertad",  5,  metadata: "{\"codigoMH\":\"05\"}"));
        items.Add(Item(id++, 18, "SAN_SALVADOR", "San Salvador", 6,  metadata: "{\"codigoMH\":\"06\"}"));
        items.Add(Item(id++, 18, "CUSCATLAN",    "Cuscatlán",    7,  metadata: "{\"codigoMH\":\"07\"}"));
        items.Add(Item(id++, 18, "LA_PAZ",       "La Paz",       8,  metadata: "{\"codigoMH\":\"08\"}"));
        items.Add(Item(id++, 18, "CABANAS",      "Cabañas",      9,  metadata: "{\"codigoMH\":\"09\"}"));
        items.Add(Item(id++, 18, "SAN_VICENTE",  "San Vicente",  10, metadata: "{\"codigoMH\":\"10\"}"));
        items.Add(Item(id++, 18, "USULUTAN",     "Usulután",     11, metadata: "{\"codigoMH\":\"11\"}"));
        items.Add(Item(id++, 18, "SAN_MIGUEL",   "San Miguel",   12, metadata: "{\"codigoMH\":\"12\"}"));
        items.Add(Item(id++, 18, "MORAZAN",      "Morazán",      13, metadata: "{\"codigoMH\":\"13\"}"));
        items.Add(Item(id++, 18, "LA_UNION",     "La Unión",     14, metadata: "{\"codigoMH\":\"14\"}"));

        // MUNICIPIO_ES (19) — Municipios post-reforma territorial 2024 (Decreto 290 / Mayo 2024)
        // 42 zonas distribuidas en los 14 departamentos. La distribución exacta puede
        // ajustarse contra el CAT-013 oficial de Hacienda cuando se publique.
        // Cada item lleva metadata {"departamento":"CODIGO","zona":"ZONA"} para cascada UI.
        AddMun(items, ref id, "AHUACHAPAN",   new[] { "NORTE", "CENTRO", "SUR" });
        AddMun(items, ref id, "SANTA_ANA",    new[] { "NORTE", "CENTRO", "ESTE" });
        AddMun(items, ref id, "SONSONATE",    new[] { "NORTE", "CENTRO", "ESTE" });
        AddMun(items, ref id, "CHALATENANGO", new[] { "NORTE", "CENTRO", "SUR" });
        AddMun(items, ref id, "LA_LIBERTAD",  new[] { "NORTE", "CENTRO", "SUR", "ESTE", "OESTE", "COSTA" });
        AddMun(items, ref id, "SAN_SALVADOR", new[] { "NORTE", "CENTRO", "ESTE", "OESTE", "SUR" });
        AddMun(items, ref id, "CUSCATLAN",    new[] { "NORTE", "SUR" });
        AddMun(items, ref id, "LA_PAZ",       new[] { "CENTRO", "ESTE", "OESTE" });
        AddMun(items, ref id, "CABANAS",      new[] { "ESTE", "OESTE" });
        AddMun(items, ref id, "SAN_VICENTE",  new[] { "NORTE", "SUR" });
        AddMun(items, ref id, "USULUTAN",     new[] { "NORTE", "ESTE", "OESTE" });
        AddMun(items, ref id, "SAN_MIGUEL",   new[] { "NORTE", "CENTRO", "OESTE" });
        AddMun(items, ref id, "MORAZAN",      new[] { "NORTE", "SUR" });
        AddMun(items, ref id, "LA_UNION",     new[] { "NORTE", "SUR" });

        modelBuilder.Entity<CatalogoItem>().HasData(items);
    }

    private static readonly Dictionary<string, string> DepartamentoLabels = new()
    {
        ["AHUACHAPAN"] = "Ahuachapán",
        ["SANTA_ANA"] = "Santa Ana",
        ["SONSONATE"] = "Sonsonate",
        ["CHALATENANGO"] = "Chalatenango",
        ["LA_LIBERTAD"] = "La Libertad",
        ["SAN_SALVADOR"] = "San Salvador",
        ["CUSCATLAN"] = "Cuscatlán",
        ["LA_PAZ"] = "La Paz",
        ["CABANAS"] = "Cabañas",
        ["SAN_VICENTE"] = "San Vicente",
        ["USULUTAN"] = "Usulután",
        ["SAN_MIGUEL"] = "San Miguel",
        ["MORAZAN"] = "Morazán",
        ["LA_UNION"] = "La Unión",
    };

    private static readonly Dictionary<string, string> ZonaLabels = new()
    {
        ["NORTE"] = "Norte",
        ["CENTRO"] = "Centro",
        ["SUR"] = "Sur",
        ["ESTE"] = "Este",
        ["OESTE"] = "Oeste",
        ["COSTA"] = "Costa",
    };

    /// <summary>Agrega N municipios de un departamento al seed (catalogo 19). Mantiene orden continuo global.</summary>
    private static void AddMun(List<CatalogoItem> items, ref int id, string deptoCodigo, string[] zonas)
    {
        var deptoLabel = DepartamentoLabels[deptoCodigo];
        var orden = 1;
        foreach (var zona in zonas)
        {
            var codigo = $"{deptoCodigo}_{zona}";
            var valor = $"{deptoLabel} {ZonaLabels[zona]}";
            var metadata = $"{{\"departamento\":\"{deptoCodigo}\",\"zona\":\"{zona}\"}}";
            items.Add(Item(id++, 19, codigo, valor, orden++, metadata: metadata));
        }
    }

    private static void SeedModulos(ModelBuilder modelBuilder)
    {
        var modulos = new[]
        {
            Mod(100, "CORE",         "NeoSTP Core",       "Núcleo: empresas, usuarios, roles, permisos",        "shield-check",   1),
            Mod(101, "NEODTE",       "NeoDTE",            "Emisión de Documentos Tributarios Electrónicos",     "receipt",        2),
            Mod(102, "NEOPOS",       "NeoPOS",            "Punto de venta",                                     "shopping-cart",  3),
            Mod(103, "NEOSCANAI",    "NeoScanAI",         "Captura/escaneo asistido por IA",                    "scan-line",      4),
            Mod(104, "NEOPROFIT",    "NeoProfit",         "Indicadores y rentabilidad",                         "trending-up",    5),
            Mod(105, "NEOBI",        "NeoBI",             "Inteligencia de negocio / reportes",                 "bar-chart-3",    6),
            Mod(106, "NEOCONNECT",   "NeoConnect",        "Integraciones y conectores",                         "plug",           7),
            Mod(107, "NEOPORTAL",    "NeoPortal",         "Portal de receptor / cliente",                       "globe",          8),
            Mod(108, "CONTINGENCIA", "Contingencia DTE",  "Modo contingencia y reenvíos",                       "alert-triangle", 9),
            Mod(109, "EVENTOSDTE",   "Eventos DTE",       "Eventos de invalidación y contingencia",             "calendar-clock", 10),
            Mod(110, "INVENTARIO",   "Inventario",        "Stock, movimientos, kardex",                         "boxes",          11),
            Mod(111, "COMPRAS",      "Compras",           "Compras y proveedores",                              "truck",          12),
            Mod(112, "GASTOS",       "Gastos",            "Control de gastos",                                  "wallet",         13),
        };

        modelBuilder.Entity<Modulo>().HasData(modulos);
    }

    private static void SeedPlanes(ModelBuilder modelBuilder)
    {
        var planes = new[]
        {
            Plan(200, "STARTER",        "Starter",         "Empresas que inician con emisión básica",          15m,    1,   1,   2,   100),
            Plan(201, "PYME",           "Pyme",            "Pyme con varios usuarios y un punto de venta",     35m,    3,   1,   3,   500),
            Plan(202, "PRO",            "Pro",             "Pyme con sucursal y reportes",                     75m,    8,   3,   8,  2000),
            Plan(203, "BUSINESSFULL",   "Business Full",   "Cadena con módulos avanzados",                    150m,   25,  10,  25, 10000),
            Plan(204, "ENTERPRISE",     "Enterprise",      "Operación grande con soporte dedicado",           400m,  100,  50, 100, 50000),
            Plan(205, "INTEGRADORAPI",  "Integrador API",  "Para empresas que integran vía API",              250m,   10,   5,  10, 30000),
            Plan(206, "CONTADOR",       "Contador",        "Plan para contadores con múltiples clientes",     120m,   25,  10,  20,  5000),
        };

        modelBuilder.Entity<Plan>().HasData(planes);
    }

    private static void SeedPlanModulos(ModelBuilder modelBuilder)
    {
        // PlanId 200-206, ModuloId 100-112
        // CORE (100) → todos los planes
        // NEODTE (101) → todos los planes
        // Pyme(201)+: NEOPOS (102)
        // Pro(202)+: NEOSCANAI (103), CONTINGENCIA (108), INVENTARIO (110)
        // BusinessFull(203)+: NEOPROFIT (104), NEOBI (105), EVENTOSDTE (109), COMPRAS (111), GASTOS (112)
        // Enterprise(204): NEOCONNECT (106), NEOPORTAL (107)
        // IntegradorApi(205): CORE + DTE + NEOCONNECT
        // Contador(206): CORE + DTE + INVENTARIO + reportes (NEOBI)

        var planModulos = new[]
        {
            // Starter
            Pm(200, 100), Pm(200, 101),
            // Pyme
            Pm(201, 100), Pm(201, 101), Pm(201, 102),
            // Pro
            Pm(202, 100), Pm(202, 101), Pm(202, 102), Pm(202, 103), Pm(202, 108), Pm(202, 110),
            // BusinessFull
            Pm(203, 100), Pm(203, 101), Pm(203, 102), Pm(203, 103), Pm(203, 104), Pm(203, 105),
            Pm(203, 108), Pm(203, 109), Pm(203, 110), Pm(203, 111), Pm(203, 112),
            // Enterprise (todos)
            Pm(204, 100), Pm(204, 101), Pm(204, 102), Pm(204, 103), Pm(204, 104), Pm(204, 105),
            Pm(204, 106), Pm(204, 107), Pm(204, 108), Pm(204, 109), Pm(204, 110), Pm(204, 111), Pm(204, 112),
            // IntegradorAPI
            Pm(205, 100), Pm(205, 101), Pm(205, 106),
            // Contador
            Pm(206, 100), Pm(206, 101), Pm(206, 105), Pm(206, 110),
        };

        modelBuilder.Entity<PlanModulo>().HasData(planModulos);
    }

    private static void SeedPermisos(ModelBuilder modelBuilder)
    {
        var permisos = new[]
        {
            // Core
            Perm(300, "Core.Empresa.Ver",          "CORE",      "Ver datos de la empresa"),
            Perm(301, "Core.Empresa.Editar",       "CORE",      "Editar datos de la empresa"),
            Perm(302, "Core.Usuarios.Ver",         "CORE",      "Ver usuarios de la empresa"),
            Perm(303, "Core.Usuarios.Crear",       "CORE",      "Crear usuarios"),
            Perm(304, "Core.Usuarios.Editar",      "CORE",      "Editar usuarios"),
            Perm(305, "Core.Usuarios.Bloquear",    "CORE",      "Bloquear / desbloquear usuarios"),
            Perm(306, "Core.Roles.Administrar",    "CORE",      "Administrar roles y permisos"),
            Perm(307, "Core.Sucursales.Administrar","CORE",     "Crear, editar e inactivar sucursales"),
            Perm(308, "Core.PuntosVenta.Administrar","CORE",    "Crear y editar puntos de venta"),
            Perm(309, "Core.Auditoria.Ver",        "CORE",      "Consultar auditoría"),
            Perm(310, "Core.Catalogos.Administrar","CORE",      "Administrar catálogos de empresa"),

            // DTE
            Perm(320, "DTE.Configurar",   "NEODTE", "Configurar emisor, ambiente y credenciales DTE"),
            Perm(321, "DTE.Emitir",       "NEODTE", "Emitir DTE"),
            Perm(322, "DTE.Consultar",    "NEODTE", "Consultar DTE emitidos"),
            Perm(323, "DTE.Reenviar",     "NEODTE", "Reenviar DTE por correo"),
            Perm(324, "DTE.Invalidar",    "NEODTE", "Invalidar DTE"),
            Perm(325, "DTE.Contingencia", "NEODTE", "Operar en modo contingencia"),

            // Clientes / Productos
            Perm(330, "Clientes.Ver",     "CORE",   "Ver clientes"),
            Perm(331, "Clientes.Crear",   "CORE",   "Crear clientes"),
            Perm(332, "Clientes.Editar",  "CORE",   "Editar clientes"),
            Perm(335, "Productos.Ver",    "CORE",   "Ver productos"),
            Perm(336, "Productos.Crear",  "CORE",   "Crear productos"),
            Perm(337, "Productos.Editar", "CORE",   "Editar productos"),

            // Reportes y otros
            Perm(340, "Reportes.Ver",     "NEOBI",     "Ver reportes"),
            Perm(345, "ScanAI.Ver",       "NEOSCANAI", "Ver capturas de ScanAI"),
            Perm(346, "ScanAI.Confirmar", "NEOSCANAI", "Confirmar capturas de ScanAI"),
            Perm(350, "API.Configurar",   "NEOCONNECT","Configurar credenciales y endpoints de integración"),

            // SuperAdmin
            Perm(360, "SuperAdmin.Empresas.Administrar", "ADMIN", "Administrar empresas globalmente"),
            Perm(361, "SuperAdmin.Planes.Administrar",   "ADMIN", "Administrar planes y módulos"),
            Perm(362, "SuperAdmin.Soporte.Entrar",       "ADMIN", "Entrar en modo soporte a una empresa"),
        };

        modelBuilder.Entity<Permiso>().HasData(permisos);
    }

    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        var roles = new[]
        {
            Rol(500, "SUPERADMIN", "SuperAdmin NeoSTP",    "Acceso total a la plataforma",                   esSistema: true),
            Rol(501, "ADMIN",      "Administrador",        "Administrador de empresa",                        esSistema: true),
            Rol(502, "OPERADOR",   "Operador",             "Operador de punto de venta y emisión",            esSistema: true),
            Rol(503, "CONTADOR",   "Contador",             "Acceso a reportes, conciliación y auditoría",     esSistema: true),
            Rol(504, "READONLY",   "Solo lectura",         "Solo consulta",                                   esSistema: true),
        };

        modelBuilder.Entity<Rol>().HasData(roles);
    }

    private static void SeedRolPermisos(ModelBuilder modelBuilder)
    {
        // SUPERADMIN (500) → todos los permisos
        var superAdmin = new[]
        {
            300, 301, 302, 303, 304, 305, 306, 307, 308, 309, 310,
            320, 321, 322, 323, 324, 325,
            330, 331, 332, 335, 336, 337,
            340, 345, 346, 350,
            360, 361, 362,
        };

        // ADMIN (501) → todo lo de empresa, sin permisos SuperAdmin
        var admin = new[]
        {
            300, 301, 302, 303, 304, 305, 306, 307, 308, 309, 310,
            320, 321, 322, 323, 324, 325,
            330, 331, 332, 335, 336, 337,
            340, 345, 346, 350,
        };

        // OPERADOR (502) → operación de venta
        var operador = new[]
        {
            300, 302, 307, 308,
            321, 322, 323, 325,
            330, 331, 332, 335, 336, 337,
            345, 346,
        };

        // CONTADOR (503) → consulta y reportes
        var contador = new[]
        {
            300, 302, 307, 308, 309,
            322, 324,
            330, 335,
            340,
        };

        // READONLY (504) → solo ver
        var readOnly = new[]
        {
            300, 302, 309,
            322,
            330, 335,
            340,
        };

        var rolPermisos = new List<object>();
        foreach (var p in superAdmin) rolPermisos.Add(new { RolId = 500, PermisoId = p, CreatedAt = SeededAt });
        foreach (var p in admin)      rolPermisos.Add(new { RolId = 501, PermisoId = p, CreatedAt = SeededAt });
        foreach (var p in operador)   rolPermisos.Add(new { RolId = 502, PermisoId = p, CreatedAt = SeededAt });
        foreach (var p in contador)   rolPermisos.Add(new { RolId = 503, PermisoId = p, CreatedAt = SeededAt });
        foreach (var p in readOnly)   rolPermisos.Add(new { RolId = 504, PermisoId = p, CreatedAt = SeededAt });

        modelBuilder.Entity<RolPermiso>().HasData(rolPermisos);
    }

    // -- builders --------------------------------------------------------

    private static Catalogo Cat(int id, string codigo, string nombre, string descripcion) => new()
    {
        Id = id,
        Codigo = codigo,
        Nombre = nombre,
        Descripcion = descripcion,
        EsSistema = true,
        Activo = true,
        EmpresaId = null,
        CreatedAt = SeededAt,
        CreatedBy = SeededBy,
    };

    private static CatalogoItem Item(int id, int catalogoId, string codigo, string valor, int orden, string? descripcion = null, string? metadata = null) => new()
    {
        Id = id,
        CatalogoId = catalogoId,
        Codigo = codigo,
        Valor = valor,
        Descripcion = descripcion,
        Orden = orden,
        EsSistema = true,
        Activo = true,
        MetadataJson = metadata,
        CreatedAt = SeededAt,
        CreatedBy = SeededBy,
    };

    private static Modulo Mod(int id, string codigo, string nombre, string descripcion, string icono, int orden) => new()
    {
        Id = id,
        Codigo = codigo,
        Nombre = nombre,
        Descripcion = descripcion,
        Icono = icono,
        Orden = orden,
        Activo = true,
        CreatedAt = SeededAt,
        CreatedBy = SeededBy,
    };

    private static Plan Plan(int id, string codigo, string nombre, string descripcion, decimal precio, int? maxUsr, int? maxSuc, int? maxPv, int? maxDte) => new()
    {
        Id = id,
        Codigo = codigo,
        Nombre = nombre,
        Descripcion = descripcion,
        PrecioMensual = precio,
        MonedaCodigo = "USD",
        LimiteUsuarios = maxUsr,
        LimiteSucursales = maxSuc,
        LimitePuntosVenta = maxPv,
        LimiteDteMensual = maxDte,
        Activo = true,
        CreatedAt = SeededAt,
        CreatedBy = SeededBy,
    };

    private static PlanModulo Pm(int planId, int moduloId) => new()
    {
        PlanId = planId,
        ModuloId = moduloId,
        Activo = true,
        CreatedAt = SeededAt,
    };

    private static Permiso Perm(int id, string codigo, string modulo, string descripcion) => new()
    {
        Id = id,
        Codigo = codigo,
        Modulo = modulo,
        Descripcion = descripcion,
        CreatedAt = SeededAt,
        CreatedBy = SeededBy,
    };

    private static Rol Rol(int id, string codigo, string nombre, string descripcion, bool esSistema) => new()
    {
        Id = id,
        Codigo = codigo,
        Nombre = nombre,
        Descripcion = descripcion,
        EsSistema = esSistema,
        Activo = true,
        EmpresaId = null,
        CreatedAt = SeededAt,
        CreatedBy = SeededBy,
    };
}
