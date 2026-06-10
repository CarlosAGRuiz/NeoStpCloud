using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V2_D3_ConfigRecordatorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cobros_ConfigRecordatorios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    DiasVencidoMinimo = table.Column<int>(type: "int", nullable: false),
                    FrecuenciaDias = table.Column<int>(type: "int", nullable: false),
                    MaximoPorEjecucion = table.Column<int>(type: "int", nullable: false),
                    EnviarEmail = table.Column<bool>(type: "bit", nullable: false),
                    EnviarWhatsApp = table.Column<bool>(type: "bit", nullable: false),
                    AsuntoPlantilla = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    MensajePlantilla = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cobros_ConfigRecordatorios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cobros_ConfigRecordatorios_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cobros_ConfigRecordatorios_EmpresaId",
                table: "Cobros_ConfigRecordatorios",
                column: "EmpresaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cobros_ConfigRecordatorios");
        }
    }
}
