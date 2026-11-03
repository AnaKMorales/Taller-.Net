using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EstadosPorBulto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Estado",
                table: "Bultos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // El sistema previo solo admitía resultados completos. Se infiere la situación
            // actual de cada bulto desde su envío, sin crear eventos ni evidencias históricas.
            migrationBuilder.Sql("""
                UPDATE "Bultos" AS b
                SET "Estado" = e."Estado"
                FROM "Envios" AS e
                WHERE b."EnvioId" = e."Id" AND b."TenantId" = e."TenantId";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bultos_Estado",
                table: "Bultos",
                sql: "\"Estado\" BETWEEN 0 AND 9");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bultos_Estado",
                table: "Bultos");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Bultos");
        }
    }
}
