using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fiscal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogoUfSefaz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sefaz_endpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    autorizador = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    modelo = table.Column<int>(type: "integer", nullable: false),
                    servico = table.Column<int>(type: "integer", nullable: false),
                    ambiente = table.Column<int>(type: "integer", nullable: false),
                    versao_servico = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    vigencia_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    vigencia_fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fonte_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verificado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verificado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sefaz_endpoints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ufs_fiscais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sigla = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    codigo_ibge = table.Column<int>(type: "integer", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    autorizador_nfe = table.Column<int>(type: "integer", nullable: false),
                    autorizador_nfce = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ufs_fiscais", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sefaz_endpoints_autorizador",
                table: "sefaz_endpoints",
                column: "autorizador");

            migrationBuilder.CreateIndex(
                name: "IX_sefaz_endpoints_autorizador_ambiente",
                table: "sefaz_endpoints",
                columns: new[] { "autorizador", "ambiente" });

            migrationBuilder.CreateIndex(
                name: "UX_sefaz_endpoints_natural_active",
                table: "sefaz_endpoints",
                columns: new[] { "autorizador", "modelo", "servico", "ambiente", "versao_servico" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "UX_ufs_fiscais_cuf_active",
                table: "ufs_fiscais",
                column: "codigo_ibge",
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "UX_ufs_fiscais_sigla_active",
                table: "ufs_fiscais",
                column: "sigla",
                unique: true,
                filter: "\"is_active\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sefaz_endpoints");

            migrationBuilder.DropTable(
                name: "ufs_fiscais");
        }
    }
}
