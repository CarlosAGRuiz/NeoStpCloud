using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint20_HardeningSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MfaConfirmadoAt",
                table: "Core_Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MfaHabilitado",
                table: "Core_Usuarios",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MfaRecoveryCodesJson",
                table: "Core_Usuarios",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MfaSecretoCifrado",
                table: "Core_Usuarios",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Core_AdminIpAllowlist",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IpCidr = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_AdminIpAllowlist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Core_ApiQuotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: true),
                    Ambito = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AmbitoRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VentanaSegundos = table.Column<int>(type: "int", nullable: false),
                    LimitePeticiones = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_ApiQuotas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Core_ApiUsageLog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    ApiKeyId = table.Column<int>(type: "int", nullable: true),
                    Metodo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Ruta = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    DuracionMs = table.Column<long>(type: "bigint", nullable: false),
                    IpOrigen = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OcurrioAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Core_ApiUsageLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ops_BackupJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: true),
                    TipoBackup = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StorageProvider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: true),
                    Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IniciadoAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinalizadoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ops_BackupJobs", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Core_Permisos",
                columns: new[] { "Id", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Modulo", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 363, "Ops.Hardening.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver backups, cuotas y lista blanca de IP", "ADMIN", null, null },
                    { 364, "Ops.Hardening.Administrar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ejecutar backups, configurar cuotas e IP allowlist", "ADMIN", null, null }
                });

            migrationBuilder.InsertData(
                table: "Core_RolPermisos",
                columns: new[] { "PermisoId", "RolId", "CreatedAt" },
                values: new object[,]
                {
                    { 363, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 364, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Core_AdminIpAllowlist_Activo",
                table: "Core_AdminIpAllowlist",
                column: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_Core_AdminIpAllowlist_IpCidr",
                table: "Core_AdminIpAllowlist",
                column: "IpCidr",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_ApiQuotas_EmpresaId_Ambito_AmbitoRef_Activo",
                table: "Core_ApiQuotas",
                columns: new[] { "EmpresaId", "Ambito", "AmbitoRef", "Activo" });

            migrationBuilder.CreateIndex(
                name: "IX_Core_ApiUsageLog_ApiKeyId_OcurrioAt",
                table: "Core_ApiUsageLog",
                columns: new[] { "ApiKeyId", "OcurrioAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Core_ApiUsageLog_EmpresaId_OcurrioAt",
                table: "Core_ApiUsageLog",
                columns: new[] { "EmpresaId", "OcurrioAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Core_ApiUsageLog_UsuarioId_OcurrioAt",
                table: "Core_ApiUsageLog",
                columns: new[] { "UsuarioId", "OcurrioAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Ops_BackupJobs_EmpresaId",
                table: "Ops_BackupJobs",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ops_BackupJobs_EstadoCodigo_IniciadoAt",
                table: "Ops_BackupJobs",
                columns: new[] { "EstadoCodigo", "IniciadoAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Core_AdminIpAllowlist");

            migrationBuilder.DropTable(
                name: "Core_ApiQuotas");

            migrationBuilder.DropTable(
                name: "Core_ApiUsageLog");

            migrationBuilder.DropTable(
                name: "Ops_BackupJobs");

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 363, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 364, 500 });

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 363);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 364);

            migrationBuilder.DropColumn(
                name: "MfaConfirmadoAt",
                table: "Core_Usuarios");

            migrationBuilder.DropColumn(
                name: "MfaHabilitado",
                table: "Core_Usuarios");

            migrationBuilder.DropColumn(
                name: "MfaRecoveryCodesJson",
                table: "Core_Usuarios");

            migrationBuilder.DropColumn(
                name: "MfaSecretoCifrado",
                table: "Core_Usuarios");
        }
    }
}
