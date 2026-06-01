using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint15_DteEventos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dte_Eventos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    TipoEventoCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CodigoGeneracion = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    AmbienteCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FechaTransmision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SelloRecibido = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NumeroControlReferencia = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    MotivoLibre = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FinalizadoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_Eventos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_Eventos_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dte_EventoDocumentosRelacionados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    DocumentoId = table.Column<int>(type: "int", nullable: false),
                    RolCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NumeroControlSnapshot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_EventoDocumentosRelacionados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_EventoDocumentosRelacionados_Dte_Documentos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "Dte_Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dte_EventoDocumentosRelacionados_Dte_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Dte_Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Dte_EventoJson",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    JsonSinFirmar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JwsFirmado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_EventoJson", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_EventoJson_Dte_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Dte_Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Dte_EventoRespuestasHacienda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventoId = table.Column<int>(type: "int", nullable: false),
                    RespuestaCrudaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CodigoMsg = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DescripcionMsg = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SelloRecibido = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RecibidoAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dte_EventoRespuestasHacienda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dte_EventoRespuestasHacienda_Dte_Eventos_EventoId",
                        column: x => x.EventoId,
                        principalTable: "Dte_Eventos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_EventoDocumentosRelacionados_DocumentoId",
                table: "Dte_EventoDocumentosRelacionados",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_EventoDocumentosRelacionados_EventoId_DocumentoId",
                table: "Dte_EventoDocumentosRelacionados",
                columns: new[] { "EventoId", "DocumentoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_EventoJson_EventoId",
                table: "Dte_EventoJson",
                column: "EventoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_EventoRespuestasHacienda_CodigoMsg",
                table: "Dte_EventoRespuestasHacienda",
                column: "CodigoMsg");

            migrationBuilder.CreateIndex(
                name: "IX_Dte_EventoRespuestasHacienda_EventoId_RecibidoAt",
                table: "Dte_EventoRespuestasHacienda",
                columns: new[] { "EventoId", "RecibidoAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Eventos_CodigoGeneracion",
                table: "Dte_Eventos",
                column: "CodigoGeneracion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Eventos_EmpresaId_FechaTransmision",
                table: "Dte_Eventos",
                columns: new[] { "EmpresaId", "FechaTransmision" });

            migrationBuilder.CreateIndex(
                name: "IX_Dte_Eventos_EmpresaId_TipoEventoCodigo_EstadoCodigo",
                table: "Dte_Eventos",
                columns: new[] { "EmpresaId", "TipoEventoCodigo", "EstadoCodigo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dte_EventoDocumentosRelacionados");

            migrationBuilder.DropTable(
                name: "Dte_EventoJson");

            migrationBuilder.DropTable(
                name: "Dte_EventoRespuestasHacienda");

            migrationBuilder.DropTable(
                name: "Dte_Eventos");
        }
    }
}
