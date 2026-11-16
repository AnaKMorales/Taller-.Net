using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PosicionesVehiculo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PosicionesVehiculo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RutaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdLectura = table.Column<Guid>(type: "uuid", nullable: false),
                    Punto = table.Column<string>(type: "jsonb", nullable: false),
                    PrecisionMetros = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    RegistradaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecibidaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PosicionesVehiculo", x => x.Id);
                    table.UniqueConstraint("AK_PosicionesVehiculo_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_PosicionesVehiculo_Operadores_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PosicionesVehiculo_Rutas_RutaId_TenantId",
                        columns: x => new { x.RutaId, x.TenantId },
                        principalTable: "Rutas",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PosicionesVehiculo_RegistradaEn",
                table: "PosicionesVehiculo",
                column: "RegistradaEn");

            migrationBuilder.CreateIndex(
                name: "IX_PosicionesVehiculo_RutaId_IdLectura",
                table: "PosicionesVehiculo",
                columns: new[] { "RutaId", "IdLectura" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PosicionesVehiculo_RutaId_TenantId",
                table: "PosicionesVehiculo",
                columns: new[] { "RutaId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PosicionesVehiculo_TenantId",
                table: "PosicionesVehiculo",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PosicionesVehiculo");
        }
    }
}
