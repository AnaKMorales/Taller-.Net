using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CuarentenaOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MensajesOutbox_CreadoEn_Id",
                table: "MensajesOutbox");

            migrationBuilder.AddColumn<DateTime>(
                name: "CuarentenaEn",
                table: "MensajesOutbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntentosFallidos",
                table: "MensajesOutbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MensajesOutbox_CreadoEn_Id",
                table: "MensajesOutbox",
                columns: new[] { "CreadoEn", "Id" },
                filter: "\"PublicadoEn\" IS NULL AND \"CuarentenaEn\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MensajesOutbox_Cuarentena",
                table: "MensajesOutbox",
                sql: "\"IntentosFallidos\" >= 0 AND (\"CuarentenaEn\" IS NULL OR (\"PublicadoEn\" IS NULL AND \"IntentosFallidos\" > 0))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MensajesOutbox_CreadoEn_Id",
                table: "MensajesOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MensajesOutbox_Cuarentena",
                table: "MensajesOutbox");

            migrationBuilder.DropColumn(
                name: "CuarentenaEn",
                table: "MensajesOutbox");

            migrationBuilder.DropColumn(
                name: "IntentosFallidos",
                table: "MensajesOutbox");

            migrationBuilder.CreateIndex(
                name: "IX_MensajesOutbox_CreadoEn_Id",
                table: "MensajesOutbox",
                columns: new[] { "CreadoEn", "Id" },
                filter: "\"PublicadoEn\" IS NULL");
        }
    }
}
