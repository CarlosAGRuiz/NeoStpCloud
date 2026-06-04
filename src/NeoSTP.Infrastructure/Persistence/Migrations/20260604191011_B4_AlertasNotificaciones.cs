using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class B4_AlertasNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notif_Alertas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    TipoCodigo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Severidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EntidadTipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EntidadId = table.Column<int>(type: "int", nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    LeidaAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResueltaAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notif_Alertas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notif_Alertas_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notif_Dispositivos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Plataforma = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    UltimoUsoAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notif_Dispositivos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notif_Preferencias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Canal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NoMolestar = table.Column<bool>(type: "bit", nullable: false),
                    HoraInicio = table.Column<TimeOnly>(type: "time", nullable: false),
                    HoraFin = table.Column<TimeOnly>(type: "time", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notif_Preferencias", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notif_Alertas_EmpresaId_Clave",
                table: "Notif_Alertas",
                columns: new[] { "EmpresaId", "Clave" });

            migrationBuilder.CreateIndex(
                name: "IX_Notif_Alertas_EmpresaId_EstadoCodigo",
                table: "Notif_Alertas",
                columns: new[] { "EmpresaId", "EstadoCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Notif_Dispositivos_EmpresaId_UsuarioId_Activo",
                table: "Notif_Dispositivos",
                columns: new[] { "EmpresaId", "UsuarioId", "Activo" });

            migrationBuilder.CreateIndex(
                name: "IX_Notif_Dispositivos_Token",
                table: "Notif_Dispositivos",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notif_Preferencias_EmpresaId_UsuarioId",
                table: "Notif_Preferencias",
                columns: new[] { "EmpresaId", "UsuarioId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notif_Alertas");

            migrationBuilder.DropTable(
                name: "Notif_Dispositivos");

            migrationBuilder.DropTable(
                name: "Notif_Preferencias");
        }
    }
}
