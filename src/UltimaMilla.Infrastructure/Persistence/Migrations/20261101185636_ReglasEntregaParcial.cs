using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReglasEntregaParcial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccionPendientes",
                table: "ReglasOperativas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PermiteEntregaParcial",
                table: "ReglasOperativas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReglasOperativas_AccionPendientes",
                table: "ReglasOperativas",
                sql: "\"AccionPendientes\" IN (0, 1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ReglasOperativas_AccionPendientes",
                table: "ReglasOperativas");

            migrationBuilder.DropColumn(
                name: "AccionPendientes",
                table: "ReglasOperativas");

            migrationBuilder.DropColumn(
                name: "PermiteEntregaParcial",
                table: "ReglasOperativas");
        }
    }
}
