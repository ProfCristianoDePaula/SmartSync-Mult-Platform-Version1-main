using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModulesAndTenantModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenants_plans_plan_id",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_tenants_plan_id",
                table: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_plans_name_active",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "plan_id",
                table: "tenants");

            // Defensivo (Etapa 15): module_id nasce NULLABLE para permitir o
            // backfill abaixo. Em bancos já populados com planos (sem módulo),
            // um módulo "Legado" é criado e os planos órfãos são vinculados a
            // ele — caso contrário a FK plans→modules quebraria. Em bancos
            // vazios (como o do compose), o WHERE EXISTS não encontra nada e
            // nada é inserido.
            migrationBuilder.AddColumn<Guid>(
                name: "module_id",
                table: "plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "modules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_modules", x => x.id);
                });

            migrationBuilder.Sql("""
                INSERT INTO modules (id, name, slug, description, is_active, created_at_utc)
                SELECT gen_random_uuid(),
                       'Legado',
                       'legado',
                       'Módulo criado automaticamente para planos existentes antes da Etapa 15.',
                       true,
                       now()
                WHERE EXISTS (SELECT 1 FROM plans WHERE module_id IS NULL);

                UPDATE plans
                SET module_id = (SELECT id FROM modules WHERE slug = 'legado' LIMIT 1)
                WHERE module_id IS NULL;
                """);

            // Após o backfill, module_id volta a ser obrigatório.
            migrationBuilder.AlterColumn<Guid>(
                name: "module_id",
                table: "plans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "tenant_modules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    start_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_modules", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenant_modules_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_modules_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_modules_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plans_module_id",
                table: "plans",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "IX_plans_module_id_name_active",
                table: "plans",
                columns: new[] { "module_id", "name" },
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_modules_name",
                table: "modules",
                column: "name",
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_modules_slug",
                table: "modules",
                column: "slug",
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_modules_module_id",
                table: "tenant_modules",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_modules_plan_id",
                table: "tenant_modules",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_modules_tenant_id",
                table: "tenant_modules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "UQ_tenant_modules_tenant_module_active",
                table: "tenant_modules",
                columns: new[] { "tenant_id", "module_id" },
                unique: true,
                filter: "\"status\" = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_plans_modules_module_id",
                table: "plans",
                column: "module_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_plans_modules_module_id",
                table: "plans");

            migrationBuilder.DropTable(
                name: "tenant_modules");

            migrationBuilder.DropTable(
                name: "modules");

            migrationBuilder.DropIndex(
                name: "IX_plans_module_id",
                table: "plans");

            migrationBuilder.DropIndex(
                name: "IX_plans_module_id_name_active",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "module_id",
                table: "plans");

            migrationBuilder.AddColumn<Guid>(
                name: "plan_id",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_plan_id",
                table: "tenants",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_plans_name_active",
                table: "plans",
                column: "name",
                unique: true,
                filter: "\"is_active\"");

            migrationBuilder.AddForeignKey(
                name: "FK_tenants_plans_plan_id",
                table: "tenants",
                column: "plan_id",
                principalTable: "plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
