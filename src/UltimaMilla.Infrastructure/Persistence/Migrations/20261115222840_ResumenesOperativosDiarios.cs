using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResumenesOperativosDiarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResumenesOperativosDiarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Dia = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CerradoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Recalculos = table.Column<int>(type: "integer", nullable: false),
                    VersionCalculo = table.Column<int>(type: "integer", nullable: false),
                    ActualJson = table.Column<string>(type: "jsonb", nullable: false),
                    AlCierreJson = table.Column<string>(type: "jsonb", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumenesOperativosDiarios", x => x.Id);
                    table.UniqueConstraint("AK_ResumenesOperativosDiarios_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.CheckConstraint("CK_ResumenOperativo_Cierre", "(\"CerradoEn\" IS NULL) = (\"AlCierreJson\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ResumenesOperativosDiarios_Operadores_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResumenesOperativosDiarios_TenantId",
                table: "ResumenesOperativosDiarios",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumenesOperativosDiarios_TenantId_Dia",
                table: "ResumenesOperativosDiarios",
                columns: new[] { "TenantId", "Dia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResumenesOperativosDiarios");
        }
    }
}
