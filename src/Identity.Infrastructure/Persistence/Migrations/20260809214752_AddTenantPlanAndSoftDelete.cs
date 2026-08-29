using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPlanAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tenants_cnpj",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_email",
                table: "tenants");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at_utc",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "plan_id",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_cnpj",
                table: "tenants",
                column: "cnpj",
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_email",
                table: "tenants",
                column: "email",
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_plan_id",
                table: "tenants",
                column: "plan_id");

            migrationBuilder.AddForeignKey(
                name: "FK_tenants_plans_plan_id",
                table: "tenants",
                column: "plan_id",
                principalTable: "plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenants_plans_plan_id",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_cnpj",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_email",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_plan_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "deleted_at_utc",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "plan_id",
                table: "tenants");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_cnpj",
                table: "tenants",
                column: "cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_email",
                table: "tenants",
                column: "email",
                unique: true);
        }
    }
}
