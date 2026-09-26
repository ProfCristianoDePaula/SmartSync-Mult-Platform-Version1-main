using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fiscal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCadastrosFiscais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clientes_fiscais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_pessoa = table.Column<int>(type: "integer", nullable: false),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ind_ie = table.Column<int>(type: "integer", nullable: false),
                    inscricao_estadual = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    end_logradouro = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    end_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    end_complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    end_bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    end_cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    end_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    end_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    end_ibge = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    consumidor_final = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes_fiscais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "naturezas_operacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descricao = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo_operacao = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_naturezas_operacao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "produtos_fiscais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    ncm = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    cest = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    origem = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    un_comercial = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    un_tributavel = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    fator_conversao = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    gtin = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    cfop_dentro_uf = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    cfop_fora_uf = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    cst_icms = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    csosn = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    aliq_icms = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    cst_pis = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    cst_cofins = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    aliq_pis = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    aliq_cofins = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    cst_ipi = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    aliq_ipi = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    cclasstrib = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    cst_ibscbs = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    item_lc116 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    nbs = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    cod_trib_nacional = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    aliq_iss = table.Column<decimal>(type: "numeric(7,4)", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produtos_fiscais", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clientes_fiscais_tenant",
                table: "clientes_fiscais",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UX_clientes_fiscais_tenant_cliente_active",
                table: "clientes_fiscais",
                columns: new[] { "tenant_id", "cliente_id" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "UX_naturezas_tenant_codigo_active",
                table: "naturezas_operacao",
                columns: new[] { "tenant_id", "codigo" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_produtos_fiscais_tenant",
                table: "produtos_fiscais",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UX_produtos_fiscais_tenant_produto_active",
                table: "produtos_fiscais",
                columns: new[] { "tenant_id", "produto_id" },
                unique: true,
                filter: "\"is_active\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clientes_fiscais");

            migrationBuilder.DropTable(
                name: "naturezas_operacao");

            migrationBuilder.DropTable(
                name: "produtos_fiscais");
        }
    }
}
