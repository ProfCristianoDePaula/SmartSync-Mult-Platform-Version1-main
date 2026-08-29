using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniquenessRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_document",
                table: "users",
                columns: new[] { "tenant_id", "document" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_email",
                table: "users",
                columns: new[] { "tenant_id", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_email",
                table: "tenants",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_tenant_document",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_tenant_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_tenants_email",
                table: "tenants");
        }
    }
}
