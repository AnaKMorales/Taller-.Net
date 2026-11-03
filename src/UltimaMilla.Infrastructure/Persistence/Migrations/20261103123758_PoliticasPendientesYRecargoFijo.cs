using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PoliticasPendientesYRecargoFijo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ImporteRecargoNuevaVisita",
                table: "ReglasOperativas",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RecargoPrevisto",
                table: "Paradas",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Devoluciones_Id_EnvioId",
                table: "Devoluciones",
                columns: new[] { "Id", "EnvioId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Devoluciones_Id_EnvioId_TenantId",
                table: "Devoluciones",
                columns: new[] { "Id", "EnvioId", "TenantId" });

            migrationBuilder.CreateTable(
                name: "BultosDevolucion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DevolucionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BultoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BultosDevolucion", x => x.Id);
                    table.UniqueConstraint("AK_BultosDevolucion_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_BultosDevolucion_Bultos_BultoId_EnvioId_TenantId",
                        columns: x => new { x.BultoId, x.EnvioId, x.TenantId },
                        principalTable: "Bultos",
                        principalColumns: new[] { "Id", "EnvioId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BultosDevolucion_Devoluciones_DevolucionId_EnvioId_TenantId",
                        columns: x => new { x.DevolucionId, x.EnvioId, x.TenantId },
                        principalTable: "Devoluciones",
                        principalColumns: new[] { "Id", "EnvioId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BultosDevolucion_Operadores_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecargosVisita",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParadaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Importe = table.Column<decimal>(type: "numeric", nullable: false),
                    RegistradoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecargosVisita", x => x.Id);
                    table.UniqueConstraint("AK_RecargosVisita_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.CheckConstraint("CK_RecargosVisita_Importe", "\"Importe\" > 0 AND round(\"Importe\", 2) = \"Importe\"");
                    table.ForeignKey(
                        name: "FK_RecargosVisita_Envios_EnvioId_TenantId",
                        columns: x => new { x.EnvioId, x.TenantId },
                        principalTable: "Envios",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecargosVisita_Operadores_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecargosVisita_Paradas_ParadaId_EnvioId_TenantId",
                        columns: x => new { x.ParadaId, x.EnvioId, x.TenantId },
                        principalTable: "Paradas",
                        principalColumns: new[] { "Id", "EnvioId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            // Las devoluciones anteriores abarcaban el envío completo; no se inventan recargos previos.
            migrationBuilder.Sql("""
                INSERT INTO "BultosDevolucion" ("Id", "DevolucionId", "BultoId", "EnvioId", "TenantId")
                SELECT gen_random_uuid(), d."Id", b."Id", d."EnvioId", d."TenantId"
                FROM "Devoluciones" AS d
                JOIN "Bultos" AS b ON b."EnvioId" = d."EnvioId" AND b."TenantId" = d."TenantId";
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReglasOperativas_ImporteRecargo",
                table: "ReglasOperativas",
                sql: "\"ImporteRecargoNuevaVisita\" IS NULL OR (\"AccionPendientes\" = 1 AND \"ImporteRecargoNuevaVisita\" > 0 AND round(\"ImporteRecargoNuevaVisita\", 2) = \"ImporteRecargoNuevaVisita\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Paradas_RecargoPrevisto",
                table: "Paradas",
                sql: "\"RecargoPrevisto\" >= 0 AND round(\"RecargoPrevisto\", 2) = \"RecargoPrevisto\"");

            migrationBuilder.CreateIndex(
                name: "IX_BultosDevolucion_BultoId_EnvioId_TenantId",
                table: "BultosDevolucion",
                columns: new[] { "BultoId", "EnvioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_BultosDevolucion_DevolucionId_BultoId",
                table: "BultosDevolucion",
                columns: new[] { "DevolucionId", "BultoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BultosDevolucion_DevolucionId_EnvioId_TenantId",
                table: "BultosDevolucion",
                columns: new[] { "DevolucionId", "EnvioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_BultosDevolucion_TenantId",
                table: "BultosDevolucion",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RecargosVisita_EnvioId_TenantId",
                table: "RecargosVisita",
                columns: new[] { "EnvioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecargosVisita_ParadaId",
                table: "RecargosVisita",
                column: "ParadaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecargosVisita_ParadaId_EnvioId_TenantId",
                table: "RecargosVisita",
                columns: new[] { "ParadaId", "EnvioId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecargosVisita_TenantId",
                table: "RecargosVisita",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BultosDevolucion");

            migrationBuilder.DropTable(
                name: "RecargosVisita");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReglasOperativas_ImporteRecargo",
                table: "ReglasOperativas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Paradas_RecargoPrevisto",
                table: "Paradas");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Devoluciones_Id_EnvioId",
                table: "Devoluciones");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Devoluciones_Id_EnvioId_TenantId",
                table: "Devoluciones");

            migrationBuilder.DropColumn(
                name: "ImporteRecargoNuevaVisita",
                table: "ReglasOperativas");

            migrationBuilder.DropColumn(
                name: "RecargoPrevisto",
                table: "Paradas");
        }
    }
}
