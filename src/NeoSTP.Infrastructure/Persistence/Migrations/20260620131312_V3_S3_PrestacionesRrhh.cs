using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V3_S3_PrestacionesRrhh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Aguinaldo",
                table: "Rrhh_PlanillaDetalles",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OtrosIngresos",
                table: "Rrhh_PlanillaDetalles",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrimaVacacion",
                table: "Rrhh_PlanillaDetalles",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Rrhh_Aguinaldos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    Anio = table.Column<int>(type: "int", nullable: false),
                    FechaCorte = table.Column<DateOnly>(type: "date", nullable: false),
                    AntiguedadAnios = table.Column<int>(type: "int", nullable: false),
                    SalarioMensual = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiasCalculados = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlanillaPeriodoId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rrhh_Aguinaldos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rrhh_Aguinaldos_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rrhh_Aguinaldos_Rrhh_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Rrhh_Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rrhh_Aguinaldos_Rrhh_PlanillaPeriodos_PlanillaPeriodoId",
                        column: x => x.PlanillaPeriodoId,
                        principalTable: "Rrhh_PlanillaPeriodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Rrhh_PoliticasPrestaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    MesesParaVacacion = table.Column<int>(type: "int", nullable: false),
                    DiasVacacionAnuales = table.Column<int>(type: "int", nullable: false),
                    PrimaVacacionPorcentaje = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    AguinaldoAniosTramoMedio = table.Column<int>(type: "int", nullable: false),
                    AguinaldoAniosTramoLargo = table.Column<int>(type: "int", nullable: false),
                    AguinaldoDiasTramoCorto = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    AguinaldoDiasTramoMedio = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    AguinaldoDiasTramoLargo = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    AguinaldoMesPago = table.Column<int>(type: "int", nullable: false),
                    AguinaldoDiaPago = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rrhh_PoliticasPrestaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rrhh_PoliticasPrestaciones_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rrhh_Vacaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    Dias = table.Column<int>(type: "int", nullable: false),
                    PrimaMonto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResolucionNota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResueltaAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResueltaPor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PlanillaPeriodoId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rrhh_Vacaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rrhh_Vacaciones_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rrhh_Vacaciones_Rrhh_Empleados_EmpleadoId",
                        column: x => x.EmpleadoId,
                        principalTable: "Rrhh_Empleados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rrhh_Vacaciones_Rrhh_PlanillaPeriodos_PlanillaPeriodoId",
                        column: x => x.PlanillaPeriodoId,
                        principalTable: "Rrhh_PlanillaPeriodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rrhh_Aguinaldos_EmpleadoId",
                table: "Rrhh_Aguinaldos",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Rrhh_Aguinaldos_EmpresaId_Anio_EstadoCodigo",
                table: "Rrhh_Aguinaldos",
                columns: new[] { "EmpresaId", "Anio", "EstadoCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Rrhh_Aguinaldos_EmpresaId_EmpleadoId_Anio",
                table: "Rrhh_Aguinaldos",
                columns: new[] { "EmpresaId", "EmpleadoId", "Anio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rrhh_Aguinaldos_PlanillaPeriodoId",
                table: "Rrhh_Aguinaldos",
                column: "PlanillaPeriodoId");

            migrationBuilder.CreateIndex(
                name: "IX_Rrhh_PoliticasPrestaciones_EmpresaId",
                table: "Rrhh_PoliticasPrestaciones",
                column: "EmpresaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rrhh_Vacaciones_EmpleadoId",
                table: "Rrhh_Vacaciones",
                column: "EmpleadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Rrhh_Vacaciones_EmpresaId_EmpleadoId_FechaInicio",
                table: "Rrhh_Vacaciones",
                columns: new[] { "EmpresaId", "EmpleadoId", "FechaInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Rrhh_Vacaciones_EmpresaId_EstadoCodigo_FechaInicio",
                table: "Rrhh_Vacaciones",
                columns: new[] { "EmpresaId", "EstadoCodigo", "FechaInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Rrhh_Vacaciones_PlanillaPeriodoId",
                table: "Rrhh_Vacaciones",
                column: "PlanillaPeriodoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rrhh_Aguinaldos");

            migrationBuilder.DropTable(
                name: "Rrhh_PoliticasPrestaciones");

            migrationBuilder.DropTable(
                name: "Rrhh_Vacaciones");

            migrationBuilder.DropColumn(
                name: "Aguinaldo",
                table: "Rrhh_PlanillaDetalles");

            migrationBuilder.DropColumn(
                name: "OtrosIngresos",
                table: "Rrhh_PlanillaDetalles");

            migrationBuilder.DropColumn(
                name: "PrimaVacacion",
                table: "Rrhh_PlanillaDetalles");
        }
    }
}
