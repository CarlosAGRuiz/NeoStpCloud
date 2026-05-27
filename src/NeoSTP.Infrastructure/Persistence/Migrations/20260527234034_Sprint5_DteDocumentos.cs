using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint5_DteDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dte_Documentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    SucursalId = table.Column<int>(type: "int", nullable: true),
                    PuntoVentaId = table.Column<int>(type: "int", nullable: true),
                    TipoDteCodigo = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    VersionDte = table.Column<int>(type: "int", nullable: false),
                    AmbienteCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NumeroControl = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CodigoGeneracion = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    SelloRecibido = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModeloFacturacion = table.Column<int>(type: "int", nullable: false),
                    TipoTransmision = table.Column<int>(type: "int", nullable: false),
                    TipoContingenciaCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    MotivoContingencia = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaEmision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HoraEmision = table.Column<TimeSpan>(type: "time", nullable: false),
                    TipoMonedaCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    ReceptorTipoDocumento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ReceptorNumeroDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReceptorNrc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReceptorNombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ReceptorTipoContribuyente = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ReceptorCodigoActividad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReceptorActividadEconomica = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ReceptorDepartamentoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReceptorMunicipioCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ReceptorDireccion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceptorCorreo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReceptorTelefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CondicionOperacionCodigo = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    FormaPagoCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PlazoDias = table.Column<int>(type: "int", nullable: true),
                    Periodo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DocumentoRelacionadoId = table.Column<int>(type: "int", nullable: true),
                    NumeroDocumentoRelacionado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    TipoDteRelacionado = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    TipoGeneracionRelacionado = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    VentaTerceroNit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VentaTerceroNombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TotalNoSujeto = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalExenta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalGravada = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SubTotalVentas = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DescuentoNoSujeto = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DescuentoExenta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DescuentoGravada = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PorcentajeDescuento = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalDescuento = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IvaTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IvaRetenido = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReteRenta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MontoTotalOperacion = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalNoGravado = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalPagar = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalLetras = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GeneradoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidadoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EnviadoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcesadoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_Documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_Documentos_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dte_Documentos_Dte_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Dte_Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dte_Documentos_Dte_Documentos_DocumentoRelacionadoId",
                        column: x => x.DocumentoRelacionadoId,
                        principalTable: "Dte_Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dte_DocumentoDetalles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    NumeroLinea = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    TipoItem = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UnidadMedidaCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    MontoDescuento = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VentaNoSujeta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VentaExenta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VentaGravada = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IvaItem = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NoGravado = table.Column<bool>(type: "bit", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_DocumentoDetalles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_DocumentoDetalles_Dte_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Dte_Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Dte_DocumentoDetalles_Dte_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Dte_Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dte_DocumentoJson",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    JsonDte = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JsonFirmado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RespuestaHacienda = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneradoAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FirmadoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespuestaAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_DocumentoJson", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_DocumentoJson_Dte_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Dte_Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_DocumentoDetalles_DocumentoId_NumeroLinea",
                table: "Dte_DocumentoDetalles",
                columns: new[] { "DocumentoId", "NumeroLinea" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_DocumentoDetalles_ProductoId",
                table: "Dte_DocumentoDetalles",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_DocumentoJson_DocumentoId",
                table: "Dte_DocumentoJson",
                column: "DocumentoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Documentos_ClienteId",
                table: "Dte_Documentos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Documentos_CodigoGeneracion",
                table: "Dte_Documentos",
                column: "CodigoGeneracion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Documentos_DocumentoRelacionadoId",
                table: "Dte_Documentos",
                column: "DocumentoRelacionadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Documentos_EmpresaId_EstadoCodigo",
                table: "Dte_Documentos",
                columns: new[] { "EmpresaId", "EstadoCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Documentos_EmpresaId_NumeroControl",
                table: "Dte_Documentos",
                columns: new[] { "EmpresaId", "NumeroControl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Documentos_EmpresaId_TipoDteCodigo_FechaEmision",
                table: "Dte_Documentos",
                columns: new[] { "EmpresaId", "TipoDteCodigo", "FechaEmision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dte_DocumentoDetalles");

            migrationBuilder.DropTable(
                name: "Dte_DocumentoJson");

            migrationBuilder.DropTable(
                name: "Dte_Documentos");
        }
    }
}
