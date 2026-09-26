using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fiscal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNfseMunicipios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "municipios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_municipios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nfse_ambientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    modo = table.Column<int>(type: "integer", nullable: false),
                    ambiente = table.Column<int>(type: "integer", nullable: false),
                    base_url_sefin = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    base_url_adn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    base_url_parametros = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verificado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nfse_ambientes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nfse_import_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    arquivo_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    criados = table.Column<int>(type: "integer", nullable: false),
                    atualizados = table.Column<int>(type: "integer", nullable: false),
                    ignorados_manuais = table.Column<int>(type: "integer", nullable: false),
                    rejeitados = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nfse_import_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nfse_municipio_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    modo = table.Column<int>(type: "integer", nullable: false),
                    fonte = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sobrescrito_manual = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nfse_municipio_configs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_municipios_uf",
                table: "municipios",
                column: "uf");

            migrationBuilder.CreateIndex(
                name: "UX_municipios_ibge_active",
                table: "municipios",
                column: "codigo_ibge",
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "UX_nfse_ambientes_ibge_modo_amb_active",
                table: "nfse_ambientes",
                columns: new[] { "codigo_ibge", "modo", "ambiente" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_nfse_import_runs_ocorrido",
                table: "nfse_import_runs",
                column: "ocorrido_em");

            migrationBuilder.CreateIndex(
                name: "UX_nfse_municipio_configs_ibge_active",
                table: "nfse_municipio_configs",
                column: "codigo_ibge",
                unique: true,
                filter: "\"is_active\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "municipios");

            migrationBuilder.DropTable(
                name: "nfse_ambientes");

            migrationBuilder.DropTable(
                name: "nfse_import_runs");

            migrationBuilder.DropTable(
                name: "nfse_municipio_configs");
        }
    }
}
