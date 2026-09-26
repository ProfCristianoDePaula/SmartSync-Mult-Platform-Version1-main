using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fiscal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "proxima_tentativa_em",
                table: "documentos_fiscais",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tentativas",
                table: "documentos_fiscais",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "xml_ultimo_envio",
                table: "documentos_fiscais",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "proxima_tentativa_em",
                table: "documentos_fiscais");

            migrationBuilder.DropColumn(
                name: "tentativas",
                table: "documentos_fiscais");

            migrationBuilder.DropColumn(
                name: "xml_ultimo_envio",
                table: "documentos_fiscais");
        }
    }
}
