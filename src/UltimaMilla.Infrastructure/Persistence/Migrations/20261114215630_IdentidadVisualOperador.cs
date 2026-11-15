using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentidadVisualOperador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorPrimario",
                table: "Operadores",
                type: "text",
                nullable: false,
                defaultValue: "#1E40AF");

            migrationBuilder.AddColumn<string>(
                name: "ColorSecundario",
                table: "Operadores",
                type: "text",
                nullable: false,
                defaultValue: "#0F172A");

            migrationBuilder.AddColumn<string>(
                name: "EmailContacto",
                table: "Operadores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Operadores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Marca",
                table: "Operadores",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TelefonoContacto",
                table: "Operadores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VersionIdentidad",
                table: "Operadores",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            migrationBuilder.Sql("UPDATE \"Operadores\" SET \"Marca\" = \"RazonSocial\", \"VersionIdentidad\" = gen_random_uuid();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorPrimario",
                table: "Operadores");

            migrationBuilder.DropColumn(
                name: "ColorSecundario",
                table: "Operadores");

            migrationBuilder.DropColumn(
                name: "EmailContacto",
                table: "Operadores");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Operadores");

            migrationBuilder.DropColumn(
                name: "Marca",
                table: "Operadores");

            migrationBuilder.DropColumn(
                name: "TelefonoContacto",
                table: "Operadores");

            migrationBuilder.DropColumn(
                name: "VersionIdentidad",
                table: "Operadores");
        }
    }
}
