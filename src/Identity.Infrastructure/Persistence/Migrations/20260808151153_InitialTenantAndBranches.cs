using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenantAndBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    address_street = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    address_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    address_complement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_state = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    address_postal_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    contact_secondary_phone = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.id);
                    table.ForeignKey(
                        name: "FK_branches_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_branches_tenant_id",
                table: "branches",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_cnpj",
                table: "tenants",
                column: "cnpj",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
