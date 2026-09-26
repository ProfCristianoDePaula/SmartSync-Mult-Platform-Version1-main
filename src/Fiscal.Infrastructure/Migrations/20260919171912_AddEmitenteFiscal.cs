using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fiscal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmitenteFiscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "concessoes_unidade",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    papel = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_concessoes_unidade", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "configuracoes_documento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    emitente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    ambiente = table.Column<int>(type: "integer", nullable: false),
                    habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    serie = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    modo_integracao = table.Column<int>(type: "integer", nullable: false),
                    referencia_certificado = table.Column<Guid>(type: "uuid", nullable: true),
                    csc_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracoes_documento", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "emitentes_fiscais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    cnpj_alfanumerico = table.Column<bool>(type: "boolean", nullable: false),
                    razao_social = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    fantasia = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    inscricao_estadual = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    inscricao_municipal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cnae = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    crt = table.Column<int>(type: "integer", nullable: false),
                    end_logradouro = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    end_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    end_complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    end_bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    end_cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    end_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    end_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    end_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    telefone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emitentes_fiscais", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_concessoes_tenant_user",
                table: "concessoes_unidade",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "UX_concessoes_tenant_branch_user_papel_active",
                table: "concessoes_unidade",
                columns: new[] { "tenant_id", "branch_id", "user_id", "papel" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_config_doc_emitente",
                table: "configuracoes_documento",
                column: "emitente_id");

            migrationBuilder.CreateIndex(
                name: "UX_config_doc_emitente_tipo_amb_active",
                table: "configuracoes_documento",
                columns: new[] { "emitente_id", "tipo", "ambiente" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_emitentes_tenant_id",
                table: "emitentes_fiscais",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UX_emitentes_tenant_branch_active",
                table: "emitentes_fiscais",
                columns: new[] { "tenant_id", "branch_id" },
                unique: true,
                filter: "\"is_active\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "concessoes_unidade");

            migrationBuilder.DropTable(
                name: "configuracoes_documento");

            migrationBuilder.DropTable(
                name: "emitentes_fiscais");
        }
    }
}
