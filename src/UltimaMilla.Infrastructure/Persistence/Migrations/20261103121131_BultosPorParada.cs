using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BultosPorParada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Paradas_Id_EnvioId",
                table: "Paradas",
                columns: new[] { "Id", "EnvioId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Paradas_Id_EnvioId_TenantId",
                table: "Paradas",
                columns: new[] { "Id", "EnvioId", "TenantId" });

            migrationBuilder.CreateTable(
                name: "BultosParada",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParadaId = table.Column<Guid>(type: "uuid", nullable: false),
                    BultoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PesoKg = table.Column<decimal>(type: "numeric", nullable: false),
                    VolumenM3 = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BultosParada", x => x.Id);
                    table.UniqueConstraint("AK_BultosParada_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.CheckConstraint("CK_BultosParada_Medidas", "\"PesoKg\" > 0 AND \"VolumenM3\" > 0");
                    table.ForeignKey(
                        name: "FK_BultosParada_Bultos_BultoId_EnvioId_TenantId",
                        columns: x => new { x.BultoId, x.EnvioId, x.TenantId },
                        principalTable: "Bultos",
                        principalColumns: new[] { "Id", "EnvioId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BultosParada_Operadores_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BultosParada_Paradas_ParadaId_EnvioId_TenantId",
                        columns: x => new { x.ParadaId, x.EnvioId, x.TenantId },
                        principalTable: "Paradas",
                        principalColumns: new[] { "Id", "EnvioId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            // Antes de esta migración cada parada abarcaba el envío completo.
            // Las medidas históricas no se almacenaban: se inicializan con las actuales.
            migrationBuilder.Sql("""
                INSERT INTO "BultosParada" ("Id", "ParadaId", "BultoId", "EnvioId", "TenantId", "PesoKg", "VolumenM3")
                SELECT gen_random_uuid(), p."Id", b."Id", p."EnvioId", p."TenantId", b."Peso",
                       b."Alto" * b."Ancho" * b."Largo" / 1000000
                FROM "Paradas" AS p
                JOIN "Bultos" AS b ON b."EnvioId" = p."EnvioId" AND b."TenantId" = p."TenantId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BultosParada_BultoId_EnvioId_TenantId",
                table: "BultosParada",
                columns: new[] { "BultoId", "EnvioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_BultosParada_ParadaId_BultoId",
                table: "BultosParada",
                columns: new[] { "ParadaId", "BultoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BultosParada_ParadaId_EnvioId_TenantId",
                table: "BultosParada",
                columns: new[] { "ParadaId", "EnvioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_BultosParada_TenantId",
                table: "BultosParada",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BultosParada");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Paradas_Id_EnvioId",
                table: "Paradas");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Paradas_Id_EnvioId_TenantId",
                table: "Paradas");
        }
    }
}
