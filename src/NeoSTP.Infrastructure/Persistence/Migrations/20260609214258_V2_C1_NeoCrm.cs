using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class V2_C1_NeoCrm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Crm_Contactos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crm_Contactos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Crm_Contactos_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Contactos_Dte_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Dte_Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Crm_EtapasPipeline",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    ProbabilidadDefault = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    EsCierreGanado = table.Column<bool>(type: "bit", nullable: false),
                    EsCierrePerdido = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crm_EtapasPipeline", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Crm_EtapasPipeline_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Crm_Oportunidades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    ContactoCrmId = table.Column<int>(type: "int", nullable: true),
                    EtapaPipelineCrmId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MontoEstimado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Probabilidad = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    FechaApertura = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaCierreEstimada = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaCierreReal = table.Column<DateOnly>(type: "date", nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MotivoPerdida = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    DteDocumentoId = table.Column<int>(type: "int", nullable: true),
                    CuentaCobroId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crm_Oportunidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Crm_Oportunidades_Cobros_CuentasCobro_CuentaCobroId",
                        column: x => x.CuentaCobroId,
                        principalTable: "Cobros_CuentasCobro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Oportunidades_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Oportunidades_Crm_Contactos_ContactoCrmId",
                        column: x => x.ContactoCrmId,
                        principalTable: "Crm_Contactos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Oportunidades_Crm_EtapasPipeline_EtapaPipelineCrmId",
                        column: x => x.EtapaPipelineCrmId,
                        principalTable: "Crm_EtapasPipeline",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Oportunidades_Dte_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Dte_Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Oportunidades_Dte_Documentos_DteDocumentoId",
                        column: x => x.DteDocumentoId,
                        principalTable: "Dte_Documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Crm_Actividades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmpresaId = table.Column<int>(type: "int", nullable: false),
                    OportunidadCrmId = table.Column<int>(type: "int", nullable: true),
                    ContactoCrmId = table.Column<int>(type: "int", nullable: true),
                    ClienteId = table.Column<int>(type: "int", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Asunto = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FechaProgramada = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaRealizada = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecordatorioAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstadoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crm_Actividades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Crm_Actividades_Core_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Core_Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Actividades_Crm_Contactos_ContactoCrmId",
                        column: x => x.ContactoCrmId,
                        principalTable: "Crm_Contactos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Actividades_Crm_Oportunidades_OportunidadCrmId",
                        column: x => x.OportunidadCrmId,
                        principalTable: "Crm_Oportunidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Crm_Actividades_Dte_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Dte_Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Core_Modulos",
                columns: new[] { "Id", "Activo", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Icono", "Nombre", "Orden", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 114, true, "NEOCRM", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Contactos, oportunidades, pipeline y actividades", "handshake", "NeoCRM", 16, null, null });

            migrationBuilder.InsertData(
                table: "Core_Permisos",
                columns: new[] { "Id", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "Modulo", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 408, "Crm.Contactos.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver contactos CRM", "NEOCRM", null, null },
                    { 409, "Crm.Contactos.Gestionar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Crear, editar e inactivar contactos CRM", "NEOCRM", null, null },
                    { 410, "Crm.Oportunidades.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver oportunidades, pipeline y resumen CRM", "NEOCRM", null, null },
                    { 411, "Crm.Oportunidades.Gestionar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Crear oportunidades y mover etapas", "NEOCRM", null, null },
                    { 412, "Crm.Actividades.Ver", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Ver actividades CRM", "NEOCRM", null, null },
                    { 413, "Crm.Actividades.Gestionar", new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Crear, completar y cancelar actividades CRM", "NEOCRM", null, null }
                });

            migrationBuilder.InsertData(
                table: "Core_PlanModulos",
                columns: new[] { "ModuloId", "PlanId", "Activo", "CreatedAt" },
                values: new object[,]
                {
                    { 114, 202, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 114, 203, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 114, 204, true, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "Core_RolPermisos",
                columns: new[] { "PermisoId", "RolId", "CreatedAt" },
                values: new object[,]
                {
                    { 408, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 409, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 410, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 411, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 412, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 413, 500, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 408, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 409, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 410, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 411, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 412, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 413, 501, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 408, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 409, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 410, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 411, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 412, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 413, 502, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 408, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 410, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 412, 503, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 408, 504, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 410, 504, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 412, 504, new DateTime(2026, 5, 26, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Actividades_ClienteId",
                table: "Crm_Actividades",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Actividades_ContactoCrmId",
                table: "Crm_Actividades",
                column: "ContactoCrmId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Actividades_EmpresaId_EstadoCodigo_FechaProgramada",
                table: "Crm_Actividades",
                columns: new[] { "EmpresaId", "EstadoCodigo", "FechaProgramada" });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Actividades_EmpresaId_OportunidadCrmId",
                table: "Crm_Actividades",
                columns: new[] { "EmpresaId", "OportunidadCrmId" });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Actividades_OportunidadCrmId",
                table: "Crm_Actividades",
                column: "OportunidadCrmId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Contactos_ClienteId",
                table: "Crm_Contactos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Contactos_EmpresaId_EstadoCodigo",
                table: "Crm_Contactos",
                columns: new[] { "EmpresaId", "EstadoCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Contactos_EmpresaId_Nombre",
                table: "Crm_Contactos",
                columns: new[] { "EmpresaId", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_EtapasPipeline_EmpresaId_Codigo",
                table: "Crm_EtapasPipeline",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Crm_EtapasPipeline_EmpresaId_Orden",
                table: "Crm_EtapasPipeline",
                columns: new[] { "EmpresaId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Oportunidades_ClienteId",
                table: "Crm_Oportunidades",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Oportunidades_ContactoCrmId",
                table: "Crm_Oportunidades",
                column: "ContactoCrmId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Oportunidades_CuentaCobroId",
                table: "Crm_Oportunidades",
                column: "CuentaCobroId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Oportunidades_DteDocumentoId",
                table: "Crm_Oportunidades",
                column: "DteDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Oportunidades_EmpresaId_ClienteId",
                table: "Crm_Oportunidades",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Oportunidades_EmpresaId_EstadoCodigo",
                table: "Crm_Oportunidades",
                columns: new[] { "EmpresaId", "EstadoCodigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Oportunidades_EmpresaId_EtapaPipelineCrmId",
                table: "Crm_Oportunidades",
                columns: new[] { "EmpresaId", "EtapaPipelineCrmId" });

            migrationBuilder.CreateIndex(
                name: "IX_Crm_Oportunidades_EtapaPipelineCrmId",
                table: "Crm_Oportunidades",
                column: "EtapaPipelineCrmId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Crm_Actividades");

            migrationBuilder.DropTable(
                name: "Crm_Oportunidades");

            migrationBuilder.DropTable(
                name: "Crm_Contactos");

            migrationBuilder.DropTable(
                name: "Crm_EtapasPipeline");

            migrationBuilder.DeleteData(
                table: "Core_PlanModulos",
                keyColumns: new[] { "ModuloId", "PlanId" },
                keyValues: new object[] { 114, 202 });

            migrationBuilder.DeleteData(
                table: "Core_PlanModulos",
                keyColumns: new[] { "ModuloId", "PlanId" },
                keyValues: new object[] { 114, 203 });

            migrationBuilder.DeleteData(
                table: "Core_PlanModulos",
                keyColumns: new[] { "ModuloId", "PlanId" },
                keyValues: new object[] { 114, 204 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 408, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 409, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 410, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 411, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 412, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 413, 500 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 408, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 409, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 410, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 411, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 412, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 413, 501 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 408, 502 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 409, 502 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 410, 502 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 411, 502 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 412, 502 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 413, 502 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 408, 503 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 410, 503 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 412, 503 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 408, 504 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 410, 504 });

            migrationBuilder.DeleteData(
                table: "Core_RolPermisos",
                keyColumns: new[] { "PermisoId", "RolId" },
                keyValues: new object[] { 412, 504 });

            migrationBuilder.DeleteData(
                table: "Core_Modulos",
                keyColumn: "Id",
                keyValue: 114);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 408);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 409);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 410);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 411);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 412);

            migrationBuilder.DeleteData(
                table: "Core_Permisos",
                keyColumn: "Id",
                keyValue: 413);
        }
    }
}
