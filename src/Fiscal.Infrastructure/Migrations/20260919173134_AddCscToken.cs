using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fiscal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCscToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "csc_token_cifrado",
                table: "configuracoes_documento",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "csc_token_key_id",
                table: "configuracoes_documento",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "csc_token_cifrado",
                table: "configuracoes_documento");

            migrationBuilder.DropColumn(
                name: "csc_token_key_id",
                table: "configuracoes_documento");
        }
    }
}
