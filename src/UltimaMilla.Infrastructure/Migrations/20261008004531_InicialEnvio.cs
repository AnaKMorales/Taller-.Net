using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicialEnvio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Envios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComercioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoSeguimiento = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    DestinatarioNombre = table.Column<string>(type: "text", nullable: false),
                    DestinatarioTelefono = table.Column<string>(type: "text", nullable: false),
                    DireccionCalle = table.Column<string>(type: "text", nullable: false),
                    FechaAdmision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Envios", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Envios");
        }
    }
}
