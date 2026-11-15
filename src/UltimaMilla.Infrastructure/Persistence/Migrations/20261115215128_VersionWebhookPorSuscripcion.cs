using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VersionWebhookPorSuscripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VersionContrato",
                table: "SuscripcionesAviso",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SuscripcionesAviso_VersionContrato",
                table: "SuscripcionesAviso",
                sql: "\"VersionContrato\" IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SuscripcionesAviso_VersionContrato",
                table: "SuscripcionesAviso");

            migrationBuilder.DropColumn(
                name: "VersionContrato",
                table: "SuscripcionesAviso");
        }
    }
}
