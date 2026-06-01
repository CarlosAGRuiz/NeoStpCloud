using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NeoSTP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint17_SeedErrorCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Dte_ErrorCatalogo",
                columns: new[] { "Id", "AccionSugerida", "Activo", "CausaProbable", "Codigo", "CreatedAt", "CreatedBy", "Descripcion", "MensajeTecnico", "Severidad", "Tipo", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "No requiere acción. El sello llegará en el campo selloRecibido.", true, "El documento fue transmitido y recibido correctamente.", "001", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Documento recibido por Hacienda", "001 - RECIBIDO", "INFO", "HACIENDA", null, null },
                    { 2, "Revisar el campo observaciones en la respuesta y corregir en el próximo documento.", true, "El documento fue aceptado pero Hacienda reportó observaciones menores.", "002", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Documento recibido con observaciones", "002 - OBSERVACIONES", "WARNING", "HACIENDA", null, null },
                    { 3, "Revisar el JSON enviado contra el esquema MH vigente. Corregir los campos señalados y retransmitir.", true, "El JSON enviado no cumple con el esquema de validación de Hacienda.", "006", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Documento rechazado por Hacienda", "006 - RECHAZADO", "ERROR", "HACIENDA", null, null },
                    { 4, "Regenerar el token de autenticación. Verificar NIT y credenciales en Config DTE. Contactar a Hacienda si persiste.", true, "El token de autenticación expiró, es inválido o la cuenta no tiene los permisos requeridos.", "095", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Error de autenticación con Hacienda", "095 - Error de autenticación", "ERROR", "HACIENDA", null, null },
                    { 5, "Solicitar habilitación del tipo de documento ante el Ministerio de Hacienda.", true, "La cuenta de pruebas o producción no tiene habilitado el tipo de DTE o la operación solicitada.", "096", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "La cuenta no está autorizada para este tipo de operación", "096 - Error de autorización", "ERROR", "HACIENDA", null, null },
                    { 6, "Verificar si el documento ya fue procesado. Si es un reintento, usar el mismo codigoGeneracion y no generar uno nuevo.", true, "Se intentó enviar un DTE con un UUID (codigoGeneracion) que ya fue procesado anteriormente.", "802", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "CodigoGeneracion ya existe en Hacienda", "802 - Documento duplicado", "WARNING", "HACIENDA", null, null },
                    { 7, "Verificar el certificado en Config DTE: cargarlo nuevamente con la contraseña correcta y asegurarse que no esté vencido.", true, "El archivo PFX/P12 es incorrecto, la contraseña es inválida, o el certificado está vencido.", "FIRMA_FAILED", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "Fallo en la firma RS512 del DTE", "Error al firmar el documento", "ERROR", "FIRMA", null, null },
                    { 8, "Verificar las credenciales en Config DTE (usuario/contraseña Hacienda). Revisar conectividad con apitest.dtes.mh.gob.sv.", true, "El NIT, usuario o contraseña configurados para Hacienda son incorrectos, o el servicio de autenticación no está disponible.", "HACIENDA_AUTH_FAILED", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "No se pudo obtener token JWT de Hacienda", "Error al obtener token de autenticación", "ERROR", "INTERNO", null, null },
                    { 9, "Cambiar el toggle de firma a REAL en Config DTE y regenerar el documento.", true, "El toggle de firma está en modo simulado (Mock). Los documentos generados en modo Mock no tienen firma real.", "FIRMA_MOCK_NO_ENVIABLE", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "El documento fue firmado en modo MOCK y no puede transmitirse a Hacienda real", "Documento firmado con mock no es enviable", "WARNING", "INTERNO", null, null },
                    { 10, "Revisar la respuesta raw del lote en el detalle. Verificar conectividad y reintentar desde la pantalla de Contingencia.", true, "El servicio /fesv/recepcionlote de Hacienda no está disponible o devolvió un error de validación.", "LOTE_ENVIO_FAILED", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "El endpoint de recepción de lote respondió con error o no fue alcanzable", "Error al enviar lote de contingencia", "ERROR", "INTERNO", null, null },
                    { 11, "Reintentar la consulta del lote desde la pantalla de Detalle de Lote.", true, "El servicio /fesv/recepcion/consultadtelote de Hacienda no está disponible.", "LOTE_CONSULTA_FAILED", new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", "El endpoint de consulta de lote no fue alcanzable o devolvió error", "Error al consultar lote de contingencia", "WARNING", "INTERNO", null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Dte_ErrorCatalogo",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Dte_ErrorCatalogo",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Dte_ErrorCatalogo",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Dte_ErrorCatalogo",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Dte_ErrorCatalogo",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Dte_ErrorCatalogo",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Dte_ErrorCatalogo",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Dte_ErrorCatalogo",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Dte_ErrorCatalogo",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Dte_ErrorCatalogo",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Dte_ErrorCatalogo",
                keyColumn: "Id",
                keyValue: 11);
        }
    }
}
