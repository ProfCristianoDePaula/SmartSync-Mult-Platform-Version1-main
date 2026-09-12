using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estoque.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPedidosVendas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cupom_produtos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_cupom = table.Column<Guid>(type: "uuid", nullable: false),
                    id_produto = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cupom_produtos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cupons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    valor_desconto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    perc_desconto = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    valor_minimo_compra = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    id_categoria = table.Column<Guid>(type: "uuid", nullable: true),
                    data_validade = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_criacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    is_cupom_produto = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cupons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "formas_pagto",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    qtd_maxima_parcelas = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_formas_pagto", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pedidos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_abertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    id_cliente = table.Column<Guid>(type: "uuid", nullable: true),
                    id_cupom = table.Column<Guid>(type: "uuid", nullable: true),
                    id_unidade = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    data_fechamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    valor_total = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedidos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "produtos_pedido",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_pedido = table.Column<Guid>(type: "uuid", nullable: false),
                    id_produto = table.Column<Guid>(type: "uuid", nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produtos_pedido", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "produtos_venda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_venda = table.Column<Guid>(type: "uuid", nullable: false),
                    id_produto = table.Column<Guid>(type: "uuid", nullable: false),
                    quantidade = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_produtos_venda", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vendas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_pedido = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_venda = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    id_forma_pagto = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_bruto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    valor_desconto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    valor_liquido_pedido = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    valor_frete = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    valor_final = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_pago = table.Column<bool>(type: "boolean", nullable: false),
                    nr_pedido = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quantidade_parcelar = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cupom_produtos_id_cupom",
                table: "cupom_produtos",
                column: "id_cupom");

            migrationBuilder.CreateIndex(
                name: "IX_cupom_produtos_id_produto",
                table: "cupom_produtos",
                column: "id_produto");

            migrationBuilder.CreateIndex(
                name: "UX_cupom_produtos_cupom_produto",
                table: "cupom_produtos",
                columns: new[] { "id_cupom", "id_produto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cupons_data_validade",
                table: "cupons",
                column: "data_validade");

            migrationBuilder.CreateIndex(
                name: "IX_cupons_id_categoria",
                table: "cupons",
                column: "id_categoria");

            migrationBuilder.CreateIndex(
                name: "IX_cupons_tenant_id",
                table: "cupons",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_cupons_tenant_validade",
                table: "cupons",
                columns: new[] { "tenant_id", "data_validade" });

            migrationBuilder.CreateIndex(
                name: "UX_formas_pagto_descricao",
                table: "formas_pagto",
                column: "descricao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_id_cliente",
                table: "pedidos",
                column: "id_cliente");

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_id_cupom",
                table: "pedidos",
                column: "id_cupom");

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_id_unidade",
                table: "pedidos",
                column: "id_unidade");

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_status",
                table: "pedidos",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_tenant_cliente",
                table: "pedidos",
                columns: new[] { "tenant_id", "id_cliente" });

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_tenant_id",
                table: "pedidos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_tenant_status",
                table: "pedidos",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_produtos_pedido_id_pedido",
                table: "produtos_pedido",
                column: "id_pedido");

            migrationBuilder.CreateIndex(
                name: "IX_produtos_pedido_id_produto",
                table: "produtos_pedido",
                column: "id_produto");

            migrationBuilder.CreateIndex(
                name: "UX_produtos_pedido_pedido_produto",
                table: "produtos_pedido",
                columns: new[] { "id_pedido", "id_produto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_produtos_venda_id_produto",
                table: "produtos_venda",
                column: "id_produto");

            migrationBuilder.CreateIndex(
                name: "IX_produtos_venda_id_venda",
                table: "produtos_venda",
                column: "id_venda");

            migrationBuilder.CreateIndex(
                name: "UX_produtos_venda_venda_produto",
                table: "produtos_venda",
                columns: new[] { "id_venda", "id_produto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vendas_data_venda",
                table: "vendas",
                column: "data_venda");

            migrationBuilder.CreateIndex(
                name: "IX_vendas_id_forma_pagto",
                table: "vendas",
                column: "id_forma_pagto");

            migrationBuilder.CreateIndex(
                name: "IX_vendas_tenant_data_venda",
                table: "vendas",
                columns: new[] { "tenant_id", "data_venda" });

            migrationBuilder.CreateIndex(
                name: "IX_vendas_tenant_id",
                table: "vendas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UX_vendas_id_pedido",
                table: "vendas",
                column: "id_pedido",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cupom_produtos");

            migrationBuilder.DropTable(
                name: "cupons");

            migrationBuilder.DropTable(
                name: "formas_pagto");

            migrationBuilder.DropTable(
                name: "pedidos");

            migrationBuilder.DropTable(
                name: "produtos_pedido");

            migrationBuilder.DropTable(
                name: "produtos_venda");

            migrationBuilder.DropTable(
                name: "vendas");
        }
    }
}
