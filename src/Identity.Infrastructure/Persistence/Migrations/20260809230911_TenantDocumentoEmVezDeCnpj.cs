using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantDocumentoEmVezDeCnpj : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "cnpj",
                table: "tenants",
                newName: "documento");

            // Etapa 17: o campo de documento passa a ter tipo (tipo_pessoa).
            // Adicionado como nullable primeiro para permitir o backfill.
            migrationBuilder.AddColumn<int>(
                name: "tipo_pessoa",
                table: "tenants",
                type: "integer",
                nullable: true);

            // Backfill: todos os tenants existentes nasceram como pessoa
            // jurídica (o campo era CNPJ). Sem isso o tipo ficaria 0 (inválido).
            migrationBuilder.Sql("UPDATE \"tenants\" SET \"tipo_pessoa\" = 2");

            migrationBuilder.AlterColumn<int>(
                name: "tipo_pessoa",
                table: "tenants",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.RenameIndex(
                name: "IX_tenants_cnpj",
                table: "tenants",
                newName: "IX_tenants_documento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo_pessoa",
                table: "tenants");

            migrationBuilder.RenameColumn(
                name: "documento",
                table: "tenants",
                newName: "cnpj");

            migrationBuilder.RenameIndex(
                name: "IX_tenants_documento",
                table: "tenants",
                newName: "IX_tenants_cnpj");
        }
    }
}
