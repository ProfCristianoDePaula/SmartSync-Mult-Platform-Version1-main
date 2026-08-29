using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estoque.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    generated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    acknowledged_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    acknowledged_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "brands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    parent_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "models",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_models", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outlet_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<int>(type: "integer", nullable: false),
                    suggested_discount_pct = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outlet_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    barcode = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: true),
                    model_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_of_measure = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_suggestions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    suggested_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    observed_balance = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_suggestions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_balances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    average_unit_cost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_balances", x => x.id);
                    table.CheckConstraint("ck_stock_balances_quantity_non_negative", "\"quantity\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origin_document_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    performed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    counterparty_branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    minimum_quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    reorder_point = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    lead_time_days = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo_pessoa = table.Column<int>(type: "integer", nullable: false),
                    document_number = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    address_street = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    address_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    address_complement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    address_postal_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    contact_secondary_phone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "xml_imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    total_items = table.Column<int>(type: "integer", nullable: false),
                    processed_items = table.Column<int>(type: "integer", nullable: false),
                    skipped_items = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_xml_imports", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_tenant_created_at",
                table: "alerts",
                columns: new[] { "tenant_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "UX_alerts_dedupe",
                table: "alerts",
                columns: new[] { "tenant_id", "type", "product_id", "branch_id", "generated_on" },
                unique: true,
                filter: "\"product_id\" IS NOT NULL AND \"branch_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_brands_tenant_id",
                table: "brands",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UX_brands_tenant_name_active",
                table: "brands",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_categories_tenant_id",
                table: "categories",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UX_categories_tenant_name_active",
                table: "categories",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_lots_tenant_expires_on",
                table: "lots",
                columns: new[] { "tenant_id", "expires_on" });

            migrationBuilder.CreateIndex(
                name: "UX_lots_tenant_branch_product_number_active",
                table: "lots",
                columns: new[] { "tenant_id", "branch_id", "product_id", "number" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_models_tenant_id",
                table: "models",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UX_models_tenant_brand_name_active",
                table: "models",
                columns: new[] { "tenant_id", "brand_id", "name" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_pending",
                table: "outbox_messages",
                column: "published_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_outlet_items_tenant_branch_product",
                table: "outlet_items",
                columns: new[] { "tenant_id", "branch_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_outlet_items_tenant_id",
                table: "outlet_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_id",
                table: "products",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UX_products_tenant_barcode_active",
                table: "products",
                columns: new[] { "tenant_id", "barcode" },
                unique: true,
                filter: "\"is_active\" AND \"barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_products_tenant_sku_active",
                table: "products",
                columns: new[] { "tenant_id", "sku" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_suggestions_tenant_id",
                table: "purchase_suggestions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UX_purchase_suggestions_open",
                table: "purchase_suggestions",
                columns: new[] { "tenant_id", "product_id", "branch_id" },
                unique: true,
                filter: "\"status\" = 1");

            migrationBuilder.CreateIndex(
                name: "UX_stock_balances_tenant_product_branch",
                table: "stock_balances",
                columns: new[] { "tenant_id", "product_id", "branch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_tenant_branch_product",
                table: "stock_movements",
                columns: new[] { "tenant_id", "branch_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_tenant_created_at",
                table: "stock_movements",
                columns: new[] { "tenant_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_rules_tenant_id",
                table: "stock_rules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UX_stock_rules_tenant_product_branch_active",
                table: "stock_rules",
                columns: new[] { "tenant_id", "product_id", "branch_id" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_tenant_id",
                table: "suppliers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UX_suppliers_tenant_document_active",
                table: "suppliers",
                columns: new[] { "tenant_id", "document_number" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_xml_imports_status",
                table: "xml_imports",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_xml_imports_tenant_id",
                table: "xml_imports",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "brands");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "lots");

            migrationBuilder.DropTable(
                name: "models");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "outlet_items");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "purchase_suggestions");

            migrationBuilder.DropTable(
                name: "stock_balances");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "stock_rules");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "xml_imports");
        }
    }
}
