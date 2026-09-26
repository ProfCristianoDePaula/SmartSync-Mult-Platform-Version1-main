using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fiscal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentoFiscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documentos_fiscais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emitente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    ambiente = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    serie = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    chave_acesso = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    protocolo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cstat = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    origem_venda_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origem_pedido_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    snapshot_emitente = table.Column<string>(type: "text", nullable: false),
                    snapshot_destinatario = table.Column<string>(type: "text", nullable: false),
                    snapshot_itens = table.Column<string>(type: "text", nullable: false),
                    snapshot_totais = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentos_fiscais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "eventos_fiscais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    codigo_evento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    justificativa = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    protocolo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_fiscais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "series_numeracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    emitente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modelo = table.Column<int>(type: "integer", nullable: false),
                    serie = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ambiente = table.Column<int>(type: "integer", nullable: false),
                    ultimo_numero = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series_numeracao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documentos_emitente",
                table: "documentos_fiscais",
                column: "emitente_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_tenant_status",
                table: "documentos_fiscais",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "UX_documentos_chave",
                table: "documentos_fiscais",
                column: "chave_acesso",
                unique: true,
                filter: "\"chave_acesso\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_documentos_tenant_idempotency",
                table: "documentos_fiscais",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eventos_documento",
                table: "eventos_fiscais",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "IX_eventos_tenant_tipo",
                table: "eventos_fiscais",
                columns: new[] { "tenant_id", "tipo" });

            migrationBuilder.CreateIndex(
                name: "UX_series_emitente_modelo_serie_amb",
                table: "series_numeracao",
                columns: new[] { "emitente_id", "modelo", "serie", "ambiente" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documentos_fiscais");

            migrationBuilder.DropTable(
                name: "eventos_fiscais");

            migrationBuilder.DropTable(
                name: "series_numeracao");
        }
    }
}
