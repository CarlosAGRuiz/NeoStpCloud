using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint1_CoreCatalogosYSeguridad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Core_Auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Modulo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Entidad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntidadId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DatosAntes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DatosDespues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Resultado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TraceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Core_Catalogos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EsSistema = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_Catalogos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Core_Empresas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nrc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RazonSocial = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    CodigoActividad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ActividadEconomica = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Departamento = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Municipio = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Core_Modulos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Icono = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_Modulos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Core_Permisos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_Permisos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Core_Planes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PrecioMensual = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MonedaCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LimiteUsuarios = table.Column<int>(type: "int", nullable: true),
                    LimiteSucursales = table.Column<int>(type: "int", nullable: true),
                    LimitePuntosVenta = table.Column<int>(type: "int", nullable: true),
                    LimiteDteMensual = table.Column<int>(type: "int", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_Planes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Core_CatalogoItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CatalogoId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    EsSistema = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_CatalogoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Core_CatalogoItems_Core_Catalogos_CatalogoId",
                        column: x => x.CatalogoId,
                        principalTable: "Core_Catalogos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Core_Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EsSistema = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Core_Roles_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Core_Sucursales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CodigoEstablecimientoMh = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TipoEstablecimientoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Departamento = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Municipio = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_Sucursales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Core_Sucursales_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Core_Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TipoUsuarioCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UltimoLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IntentosFallidos = table.Column<int>(type: "int", nullable: false),
                    BloqueadoHasta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Core_Usuarios_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Core_EmpresaModulos",
                columns: table => new
                {
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ModuloId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaActivacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaInactivacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_EmpresaModulos", x => new { x.EmpresaId, x.ModuloId });
                    table.ForeignKey(
                        name: "FK_Core_EmpresaModulos_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Core_EmpresaModulos_Core_Modulos_ModuloId",
                        column: x => x.ModuloId,
                        principalTable: "Core_Modulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Core_EmpresaPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_EmpresaPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Core_EmpresaPlan_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Core_EmpresaPlan_Core_Planes_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Core_Planes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Core_PlanModulos",
                columns: table => new
                {
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    ModuloId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_PlanModulos", x => new { x.PlanId, x.ModuloId });
                    table.ForeignKey(
                        name: "FK_Core_PlanModulos_Core_Modulos_ModuloId",
                        column: x => x.ModuloId,
                        principalTable: "Core_Modulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Core_PlanModulos_Core_Planes_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Core_Planes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Core_RolPermisos",
                columns: table => new
                {
                    RolId = table.Column<int>(type: "int", nullable: false),
                    PermisoId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_RolPermisos", x => new { x.RolId, x.PermisoId });
                    table.ForeignKey(
                        name: "FK_Core_RolPermisos_Core_Permisos_PermisoId",
                        column: x => x.PermisoId,
                        principalTable: "Core_Permisos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Core_RolPermisos_Core_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Core_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Core_PuntosVenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SucursalId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CodigoPuntoVentaMh = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_PuntosVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Core_PuntosVenta_Core_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Core_Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Core_RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacedByToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedByIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    RevokedByIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Core_RefreshTokens_Core_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Core_Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Core_UsuarioRoles",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    RolId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_UsuarioRoles", x => new { x.UsuarioId, x.RolId });
                    table.ForeignKey(
                        name: "FK_Core_UsuarioRoles_Core_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Core_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Core_UsuarioRoles_Core_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Core_Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Core_Catalogos",
                columns: new[] { "Id", "Activo", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EmpresaId", "EsSistema", "Nombre", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, true, "ESTADO_GENERICO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Activo / Inactivo para uso general", null, true, "Estado genérico", null, null },
                    { 2, true, "ESTADO_USUARIO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Estados que puede tener un usuario", null, true, "Estado de usuario", null, null },
                    { 3, true, "ESTADO_EMPRESA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Estados del ciclo de vida de una empresa", null, true, "Estado de empresa", null, null },
                    { 4, true, "ESTADO_FACTURA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Estados del ciclo de vida de un DTE", null, true, "Estado de factura/DTE", null, null },
                    { 5, true, "ESTADO_PLAN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Estados de la suscripción de empresa a un plan", null, true, "Estado de plan", null, null },
                    { 6, true, "TIPO_FACTURA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Tipos de documentos tributarios electrónicos", null, true, "Tipo de factura/DTE", null, null },
                    { 7, true, "TIPO_DOC_IDENTIDAD", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "DUI, NIT, Pasaporte, etc.", null, true, "Tipo de documento de identidad", null, null },
                    { 8, true, "TIPO_CONTRIBUYENTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Consumidor final, contribuyente, gran contribuyente", null, true, "Tipo de contribuyente", null, null },
                    { 9, true, "TIPO_ESTABLECIMIENTO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Casa matriz, sucursal, bodega, etc.", null, true, "Tipo de establecimiento", null, null },
                    { 10, true, "TIPO_USUARIO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "SuperAdmin, Admin, Operador, Contador, ReadOnly", null, true, "Tipo de usuario", null, null },
                    { 11, true, "TIPO_MOVIMIENTO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Venta, compra, ajuste, transferencia, devolución", null, true, "Tipo de movimiento", null, null },
                    { 12, true, "MONEDA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Monedas habilitadas para emisión y cobro", null, true, "Moneda", null, null },
                    { 13, true, "FORMA_PAGO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Formas de pago permitidas en documentos", null, true, "Forma de pago", null, null },
                    { 14, true, "CONDICION_OPERACION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Contado, crédito u otro", null, true, "Condición de operación", null, null },
                    { 15, true, "UNIDAD_MEDIDA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Unidades de medida para productos y servicios", null, true, "Unidad de medida", null, null },
                    { 16, true, "CANAL_VENTA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Origen del documento: POS, web, móvil, API, manual", null, true, "Canal de venta", null, null },
                    { 17, true, "AMBIENTE_DTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ambientes Hacienda: pruebas y producción", null, true, "Ambiente DTE", null, null }
                });

            migrationBuilder.InsertData(
                table: "Core_Modulos",
                columns: new[] { "Id", "Activo", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Icono", "Nombre", "Orden", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 100, true, "CORE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Núcleo: empresas, usuarios, roles, permisos", "shield-check", "NeoSTP Core", 1, null, null },
                    { 101, true, "NEODTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Emisión de Documentos Tributarios Electrónicos", "receipt", "NeoDTE", 2, null, null },
                    { 102, true, "NEOPOS", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Punto de venta", "shopping-cart", "NeoPOS", 3, null, null },
                    { 103, true, "NEOSCANAI", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Captura/escaneo asistido por IA", "scan-line", "NeoScanAI", 4, null, null },
                    { 104, true, "NEOPROFIT", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Indicadores y rentabilidad", "trending-up", "NeoProfit", 5, null, null },
                    { 105, true, "NEOBI", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Inteligencia de negocio / reportes", "bar-chart-3", "NeoBI", 6, null, null },
                    { 106, true, "NEOCONNECT", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Integraciones y conectores", "plug", "NeoConnect", 7, null, null },
                    { 107, true, "NEOPORTAL", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Portal de receptor / cliente", "globe", "NeoPortal", 8, null, null },
                    { 108, true, "CONTINGENCIA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Modo contingencia y reenvíos", "alert-triangle", "Contingencia DTE", 9, null, null },
                    { 109, true, "EVENTOSDTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Eventos de invalidación y contingencia", "calendar-clock", "Eventos DTE", 10, null, null },
                    { 110, true, "INVENTARIO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Stock, movimientos, kardex", "boxes", "Inventario", 11, null, null },
                    { 111, true, "COMPRAS", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Compras y proveedores", "truck", "Compras", 12, null, null },
                    { 112, true, "GASTOS", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Control de gastos", "wallet", "Gastos", 13, null, null }
                });

            migrationBuilder.InsertData(
                table: "Core_Permisos",
                columns: new[] { "Id", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Modulo", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 300, "Core.Empresa.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver datos de la empresa", "CORE", null, null },
                    { 301, "Core.Empresa.Editar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Editar datos de la empresa", "CORE", null, null },
                    { 302, "Core.Usuarios.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver usuarios de la empresa", "CORE", null, null },
                    { 303, "Core.Usuarios.Crear", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Crear usuarios", "CORE", null, null },
                    { 304, "Core.Usuarios.Editar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Editar usuarios", "CORE", null, null },
                    { 305, "Core.Usuarios.Bloquear", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Bloquear / desbloquear usuarios", "CORE", null, null },
                    { 306, "Core.Roles.Administrar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Administrar roles y permisos", "CORE", null, null },
                    { 307, "Core.Sucursales.Administrar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Crear, editar e inactivar sucursales", "CORE", null, null },
                    { 308, "Core.PuntosVenta.Administrar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Crear y editar puntos de venta", "CORE", null, null },
                    { 309, "Core.Auditoria.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Consultar auditoría", "CORE", null, null },
                    { 310, "Core.Catalogos.Administrar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Administrar catálogos de empresa", "CORE", null, null },
                    { 320, "DTE.Configurar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Configurar emisor, ambiente y credenciales DTE", "NEODTE", null, null },
                    { 321, "DTE.Emitir", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Emitir DTE", "NEODTE", null, null },
                    { 322, "DTE.Consultar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Consultar DTE emitidos", "NEODTE", null, null },
                    { 323, "DTE.Reenviar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Reenviar DTE por correo", "NEODTE", null, null },
                    { 324, "DTE.Invalidar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Invalidar DTE", "NEODTE", null, null },
                    { 325, "DTE.Contingencia", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Operar en modo contingencia", "NEODTE", null, null },
                    { 330, "Clientes.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver clientes", "CORE", null, null },
                    { 331, "Clientes.Crear", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Crear clientes", "CORE", null, null },
                    { 332, "Clientes.Editar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Editar clientes", "CORE", null, null },
                    { 335, "Productos.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver productos", "CORE", null, null },
                    { 336, "Productos.Crear", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Crear productos", "CORE", null, null },
                    { 337, "Productos.Editar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Editar productos", "CORE", null, null },
                    { 340, "Reportes.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver reportes", "NEOBI", null, null },
                    { 345, "ScanAI.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver capturas de ScanAI", "NEOSCANAI", null, null },
                    { 346, "ScanAI.Confirmar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Confirmar capturas de ScanAI", "NEOSCANAI", null, null },
                    { 350, "API.Configurar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Configurar credenciales y endpoints de integración", "NEOCONNECT", null, null },
                    { 360, "SuperAdmin.Empresas.Administrar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Administrar empresas globalmente", "ADMIN", null, null },
                    { 361, "SuperAdmin.Planes.Administrar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Administrar planes y módulos", "ADMIN", null, null },
                    { 362, "SuperAdmin.Soporte.Entrar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Entrar en modo soporte a una empresa", "ADMIN", null, null }
                });

            migrationBuilder.InsertData(
                table: "Core_Planes",
                columns: new[] { "Id", "Activo", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "LimiteDteMensual", "LimitePuntosVenta", "LimiteSucursales", "LimiteUsuarios", "MonedaCodigo", "Nombre", "PrecioMensual", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 200, true, "STARTER", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Empresas que inician con emisión básica", 100, 2, 1, 1, "USD", "Starter", 15m, null, null },
                    { 201, true, "PYME", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Pyme con varios usuarios y un punto de venta", 500, 3, 1, 3, "USD", "Pyme", 35m, null, null },
                    { 202, true, "PRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Pyme con sucursal y reportes", 2000, 8, 3, 8, "USD", "Pro", 75m, null, null },
                    { 203, true, "BUSINESSFULL", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Cadena con módulos avanzados", 10000, 25, 10, 25, "USD", "Business Full", 150m, null, null },
                    { 204, true, "ENTERPRISE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Operación grande con soporte dedicado", 50000, 100, 50, 100, "USD", "Enterprise", 400m, null, null },
                    { 205, true, "INTEGRADORAPI", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Para empresas que integran vía API", 30000, 10, 5, 10, "USD", "Integrador API", 250m, null, null },
                    { 206, true, "CONTADOR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Plan para contadores con múltiples clientes", 5000, 20, 10, 25, "USD", "Contador", 120m, null, null }
                });

            migrationBuilder.InsertData(
                table: "Core_Roles",
                columns: new[] { "Id", "Activo", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EmpresaId", "EsSistema", "Nombre", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 500, true, "SUPERADMIN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Acceso total a la plataforma", null, true, "SuperAdmin NeoSTP", null, null },
                    { 501, true, "ADMIN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Administrador de empresa", null, true, "Administrador", null, null },
                    { 502, true, "OPERADOR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Operador de punto de venta y emisión", null, true, "Operador", null, null },
                    { 503, true, "CONTADOR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Acceso a reportes, conciliación y auditoría", null, true, "Contador", null, null },
                    { 504, true, "READONLY", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Solo consulta", null, true, "Solo lectura", null, null }
                });

            migrationBuilder.InsertData(
                table: "Core_CatalogoItems",
                columns: new[] { "Id", "Activo", "CatalogoId", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "EsSistema", "MetadataJson", "Orden", "UpdatedAt", "UpdatedBy", "Valor" },
                values: new object[,]
                {
                    { 1, true, 1, "ACTIVO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 1, null, null, "Activo" },
                    { 2, true, 1, "INACTIVO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 2, null, null, "Inactivo" },
                    { 3, true, 2, "ACTIVO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 1, null, null, "Activo" },
                    { 4, true, 2, "INACTIVO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 2, null, null, "Inactivo" },
                    { 5, true, 2, "BLOQUEADO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 3, null, null, "Bloqueado" },
                    { 6, true, 2, "PENDIENTE_ACTIVACION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 4, null, null, "Pendiente activación" },
                    { 7, true, 2, "ELIMINADO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 5, null, null, "Eliminado" },
                    { 8, true, 3, "ACTIVA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 1, null, null, "Activa" },
                    { 9, true, 3, "INACTIVA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 2, null, null, "Inactiva" },
                    { 10, true, 3, "SUSPENDIDA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 3, null, null, "Suspendida" },
                    { 11, true, 3, "VENCIDA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 4, null, null, "Vencida" },
                    { 12, true, 3, "ELIMINADA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 5, null, null, "Eliminada" },
                    { 13, true, 4, "BORRADOR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 1, null, null, "Borrador" },
                    { 14, true, 4, "GENERADO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 2, null, null, "Generado" },
                    { 15, true, 4, "VALIDADO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 3, null, null, "Validado" },
                    { 16, true, 4, "FIRMADO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 4, null, null, "Firmado" },
                    { 17, true, 4, "ENVIADO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 5, null, null, "Enviado" },
                    { 18, true, 4, "PROCESADO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 6, null, null, "Procesado" },
                    { 19, true, 4, "RECHAZADO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 7, null, null, "Rechazado" },
                    { 20, true, 4, "CONTINGENCIA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 8, null, null, "Contingencia" },
                    { 21, true, 4, "INVALIDADO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 9, null, null, "Invalidado" },
                    { 22, true, 4, "ERROR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 10, null, null, "Error" },
                    { 23, true, 5, "ACTIVO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 1, null, null, "Activo" },
                    { 24, true, 5, "VENCIDO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 2, null, null, "Vencido" },
                    { 25, true, 5, "SUSPENDIDO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 3, null, null, "Suspendido" },
                    { 26, true, 5, "CANCELADO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 4, null, null, "Cancelado" },
                    { 27, true, 6, "FACTURA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"01\"}", 1, null, null, "Factura electrónica" },
                    { 28, true, 6, "CCF", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"03\"}", 2, null, null, "Comprobante de Crédito Fiscal" },
                    { 29, true, 6, "NOTA_REMISION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"04\"}", 3, null, null, "Nota de Remisión" },
                    { 30, true, 6, "NOTA_CREDITO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"05\"}", 4, null, null, "Nota de Crédito" },
                    { 31, true, 6, "NOTA_DEBITO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"06\"}", 5, null, null, "Nota de Débito" },
                    { 32, true, 6, "COMPROBANTE_RETENCION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"07\"}", 6, null, null, "Comprobante de Retención" },
                    { 33, true, 6, "COMPROBANTE_LIQUIDACION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"08\"}", 7, null, null, "Comprobante de Liquidación" },
                    { 34, true, 6, "DOCUMENTO_CONTABLE_LIQUIDACION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"09\"}", 8, null, null, "Documento Contable de Liquidación" },
                    { 35, true, 6, "FACTURA_EXPORTACION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"11\"}", 9, null, null, "Factura de Exportación" },
                    { 36, true, 6, "SUJETO_EXCLUIDO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"14\"}", 10, null, null, "Factura Sujeto Excluido" },
                    { 37, true, 6, "COMPROBANTE_DONACION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"15\"}", 11, null, null, "Comprobante de Donación" },
                    { 38, true, 7, "DUI", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 1, null, null, "DUI" },
                    { 39, true, 7, "NIT", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 2, null, null, "NIT" },
                    { 40, true, 7, "PASAPORTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 3, null, null, "Pasaporte" },
                    { 41, true, 7, "CARNET_RESIDENTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 4, null, null, "Carnet de Residente" },
                    { 42, true, 7, "OTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 5, null, null, "Otro documento" },
                    { 43, true, 8, "CONSUMIDOR_FINAL", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 1, null, null, "Consumidor Final" },
                    { 44, true, 8, "CONTRIBUYENTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 2, null, null, "Contribuyente" },
                    { 45, true, 8, "GRAN_CONTRIBUYENTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 3, null, null, "Gran Contribuyente" },
                    { 46, true, 9, "CASA_MATRIZ", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"01\"}", 1, null, null, "Casa Matriz" },
                    { 47, true, 9, "SUCURSAL", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"02\"}", 2, null, null, "Sucursal" },
                    { 48, true, 9, "BODEGA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"04\"}", 3, null, null, "Bodega" },
                    { 49, true, 9, "PATIO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"07\"}", 4, null, null, "Patio o Predio" },
                    { 50, true, 9, "OFICINA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 5, null, null, "Oficina" },
                    { 51, true, 9, "OTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"20\"}", 6, null, null, "Otro" },
                    { 52, true, 10, "SUPERADMIN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 1, null, null, "SuperAdmin NeoSTP" },
                    { 53, true, 10, "ADMIN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 2, null, null, "Administrador" },
                    { 54, true, 10, "OPERADOR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 3, null, null, "Operador" },
                    { 55, true, 10, "CONTADOR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 4, null, null, "Contador" },
                    { 56, true, 10, "READONLY", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 5, null, null, "Solo lectura" },
                    { 57, true, 11, "VENTA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 1, null, null, "Venta" },
                    { 58, true, 11, "COMPRA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 2, null, null, "Compra" },
                    { 59, true, 11, "AJUSTE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 3, null, null, "Ajuste" },
                    { 60, true, 11, "TRANSFERENCIA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 4, null, null, "Transferencia" },
                    { 61, true, 11, "DEVOLUCION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 5, null, null, "Devolución" },
                    { 62, true, 12, "USD", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"simbolo\":\"$\"}", 1, null, null, "Dólar estadounidense" },
                    { 63, true, 12, "EUR", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"simbolo\":\"€\"}", 2, null, null, "Euro" },
                    { 64, true, 12, "MXN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"simbolo\":\"$\"}", 3, null, null, "Peso mexicano" },
                    { 65, true, 12, "GTQ", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"simbolo\":\"Q\"}", 4, null, null, "Quetzal guatemalteco" },
                    { 66, true, 12, "HNL", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"simbolo\":\"L\"}", 5, null, null, "Lempira hondureño" },
                    { 67, true, 12, "NIO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"simbolo\":\"C$\"}", 6, null, null, "Córdoba nicaragüense" },
                    { 68, true, 12, "CRC", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"simbolo\":\"₡\"}", 7, null, null, "Colón costarricense" },
                    { 69, true, 12, "PAB", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"simbolo\":\"B/.\"}", 8, null, null, "Balboa panameño" },
                    { 70, true, 12, "DOP", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"simbolo\":\"RD$\"}", 9, null, null, "Peso dominicano" },
                    { 71, true, 12, "COP", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"simbolo\":\"$\"}", 10, null, null, "Peso colombiano" },
                    { 72, true, 13, "EFECTIVO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"01\"}", 1, null, null, "Efectivo" },
                    { 73, true, 13, "TARJETA_DEBITO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"02\"}", 2, null, null, "Tarjeta de Débito" },
                    { 74, true, 13, "TARJETA_CREDITO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"03\"}", 3, null, null, "Tarjeta de Crédito" },
                    { 75, true, 13, "CHEQUE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"04\"}", 4, null, null, "Cheque" },
                    { 76, true, 13, "TRANSFERENCIA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"05\"}", 5, null, null, "Transferencia bancaria" },
                    { 77, true, 13, "VALE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"06\"}", 6, null, null, "Vale o cupón" },
                    { 78, true, 13, "BITCOIN", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"07\"}", 7, null, null, "Bitcoin" },
                    { 79, true, 13, "OTRAS_CRIPTO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"08\"}", 8, null, null, "Otras criptomonedas" },
                    { 80, true, 13, "CREDITO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"99\"}", 9, null, null, "A crédito" },
                    { 81, true, 13, "OTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 10, null, null, "Otro" },
                    { 82, true, 14, "CONTADO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"1\"}", 1, null, null, "Contado" },
                    { 83, true, 14, "CREDITO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"2\"}", 2, null, null, "Crédito" },
                    { 84, true, 14, "OTRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"3\"}", 3, null, null, "Otro" },
                    { 85, true, 15, "UNIDAD", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"59\"}", 1, null, null, "Unidad" },
                    { 86, true, 15, "CAJA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"43\"}", 2, null, null, "Caja" },
                    { 87, true, 15, "DOCENA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"44\"}", 3, null, null, "Docena" },
                    { 88, true, 15, "LITRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"22\"}", 4, null, null, "Litro" },
                    { 89, true, 15, "GALON", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"23\"}", 5, null, null, "Galón" },
                    { 90, true, 15, "KILOGRAMO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"10\"}", 6, null, null, "Kilogramo" },
                    { 91, true, 15, "LIBRA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"15\"}", 7, null, null, "Libra" },
                    { 92, true, 15, "METRO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"32\"}", 8, null, null, "Metro" },
                    { 93, true, 15, "SERVICIO", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"58\"}", 9, null, null, "Servicio" },
                    { 94, true, 15, "HORA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"56\"}", 10, null, null, "Hora" },
                    { 95, true, 15, "DIA", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"55\"}", 11, null, null, "Día" },
                    { 96, true, 15, "PAQUETE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"49\"}", 12, null, null, "Paquete" },
                    { 97, true, 16, "POS", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 1, null, null, "Punto de Venta" },
                    { 98, true, 16, "WEB", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 2, null, null, "Web" },
                    { 99, true, 16, "MOBILE", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 3, null, null, "Móvil" },
                    { 100, true, 16, "API", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 4, null, null, "API/Integración" },
                    { 101, true, 16, "MANUAL", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, null, 5, null, null, "Manual" },
                    { 102, true, 17, "PRUEBAS", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"00\"}", 1, null, null, "Pruebas" },
                    { 103, true, 17, "PRODUCCION", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", null, true, "{\"codigoMH\":\"01\"}", 2, null, null, "Producción" }
                });

            migrationBuilder.InsertData(
                table: "Core_PlanModulos",
                columns: new[] { "ModuloId", "PlanId", "Activo", "CreatedAt" },
                values: new object[,]
                {
                    { 100, 200, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 101, 200, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 100, 201, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 101, 201, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 102, 201, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 100, 202, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 101, 202, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 102, 202, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 103, 202, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 108, 202, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 110, 202, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 100, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 101, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 102, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 103, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 104, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 105, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 108, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 109, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 110, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 111, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 112, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 100, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 101, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 102, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 103, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 104, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 105, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 106, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 107, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 108, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 109, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 110, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 111, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 112, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 100, 205, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 101, 205, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 106, 205, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 100, 206, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 101, 206, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 105, 206, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 110, 206, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Core_RolPermisos",
                columns: new[] { "PermisoId", "RolId", "CreatedAt" },
                values: new object[,]
                {
                    { 300, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 301, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 302, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 303, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 304, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 305, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 306, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 307, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 308, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 309, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 310, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 320, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 321, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 322, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 323, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 324, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 325, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 330, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 331, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 332, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 335, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 336, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 337, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 340, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 345, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 346, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 350, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 360, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 361, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 362, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 300, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 301, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 302, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 303, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 304, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 305, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 306, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 307, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 308, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 309, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 310, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 320, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 321, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 322, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 323, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 324, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 325, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 330, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 331, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 332, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 335, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 336, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 337, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 340, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 345, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 346, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 350, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 300, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 302, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 307, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 308, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 321, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 322, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 323, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 325, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 330, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 331, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 332, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 335, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 336, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 337, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 345, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 346, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 300, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 302, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 307, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 308, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 309, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 322, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 324, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 330, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 335, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 340, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 300, 504, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 302, 504, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 309, 504, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 322, 504, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 330, 504, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 335, 504, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 340, 504, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Core_Auditoria_EmpresaId_CreatedAt",
                table: "Core_Auditoria",
                columns: new[] { "EmpresaId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Core_Auditoria_Modulo_Accion",
                table: "Core_Auditoria",
                columns: new[] { "Modulo", "Accion" });

            migrationBuilder.CreateIndex(
                name: "IX_Core_Auditoria_TraceId",
                table: "Core_Auditoria",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Auditoria_UsuarioId_CreatedAt",
                table: "Core_Auditoria",
                columns: new[] { "UsuarioId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Core_CatalogoItems_CatalogoId_Activo_Orden",
                table: "Core_CatalogoItems",
                columns: new[] { "CatalogoId", "Activo", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_Core_CatalogoItems_CatalogoId_Codigo",
                table: "Core_CatalogoItems",
                columns: new[] { "CatalogoId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_Catalogos_Codigo_EmpresaId",
                table: "Core_Catalogos",
                columns: new[] { "Codigo", "EmpresaId" },
                unique: true,
                filter: "[EmpresaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Catalogos_EmpresaId",
                table: "Core_Catalogos",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_EmpresaModulos_ModuloId",
                table: "Core_EmpresaModulos",
                column: "ModuloId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_EmpresaPlan_EmpresaId_EstadoCodigo",
                table: "Core_EmpresaPlan",
                columns: new[] { "EmpresaId", "EstadoCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Core_EmpresaPlan_FechaFin",
                table: "Core_EmpresaPlan",
                column: "FechaFin");

            migrationBuilder.CreateIndex(
                name: "IX_Core_EmpresaPlan_PlanId",
                table: "Core_EmpresaPlan",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Empresas_EstadoCodigo",
                table: "Core_Empresas",
                column: "EstadoCodigo");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Empresas_Nit",
                table: "Core_Empresas",
                column: "Nit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_Modulos_Codigo",
                table: "Core_Modulos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_Permisos_Codigo",
                table: "Core_Permisos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_Permisos_Modulo",
                table: "Core_Permisos",
                column: "Modulo");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Planes_Codigo",
                table: "Core_Planes",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_PlanModulos_ModuloId",
                table: "Core_PlanModulos",
                column: "ModuloId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_PuntosVenta_SucursalId_Codigo",
                table: "Core_PuntosVenta",
                columns: new[] { "SucursalId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_RefreshTokens_Token",
                table: "Core_RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_RefreshTokens_UsuarioId_RevokedAt",
                table: "Core_RefreshTokens",
                columns: new[] { "UsuarioId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Core_Roles_EmpresaId_Codigo",
                table: "Core_Roles",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true,
                filter: "[EmpresaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Core_RolPermisos_PermisoId",
                table: "Core_RolPermisos",
                column: "PermisoId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Sucursales_EmpresaId_Codigo",
                table: "Core_Sucursales",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_UsuarioRoles_RolId",
                table: "Core_UsuarioRoles",
                column: "RolId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Usuarios_EmpresaId_Email",
                table: "Core_Usuarios",
                columns: new[] { "EmpresaId", "Email" },
                unique: true,
                filter: "[EmpresaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Usuarios_EmpresaId_Username",
                table: "Core_Usuarios",
                columns: new[] { "EmpresaId", "Username" },
                unique: true,
                filter: "[EmpresaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Usuarios_EstadoCodigo",
                table: "Core_Usuarios",
                column: "EstadoCodigo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Core_Auditoria");

            migrationBuilder.DropTable(
                name: "Core_CatalogoItems");

            migrationBuilder.DropTable(
                name: "Core_EmpresaModulos");

            migrationBuilder.DropTable(
                name: "Core_EmpresaPlan");

            migrationBuilder.DropTable(
                name: "Core_PlanModulos");

            migrationBuilder.DropTable(
                name: "Core_PuntosVenta");

            migrationBuilder.DropTable(
                name: "Core_RefreshTokens");

            migrationBuilder.DropTable(
                name: "Core_RolPermisos");

            migrationBuilder.DropTable(
                name: "Core_UsuarioRoles");

            migrationBuilder.DropTable(
                name: "Core_Catalogos");

            migrationBuilder.DropTable(
                name: "Core_Modulos");

            migrationBuilder.DropTable(
                name: "Core_Planes");

            migrationBuilder.DropTable(
                name: "Core_Sucursales");

            migrationBuilder.DropTable(
                name: "Core_Permisos");

            migrationBuilder.DropTable(
                name: "Core_Roles");

            migrationBuilder.DropTable(
                name: "Core_Usuarios");

            migrationBuilder.DropTable(
                name: "Core_Empresas");
        }
    }
}
