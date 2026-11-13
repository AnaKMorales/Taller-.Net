using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImportacionIdempotente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AltasImportadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaComercialKey = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Referencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Huella = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Resultado = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AltasImportadas", x => x.Id);
                    table.UniqueConstraint("AK_AltasImportadas_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_AltasImportadas_CuentasComerciales_CuentaComercialKey_Tenan~",
                        columns: x => new { x.CuentaComercialKey, x.TenantId },
                        principalTable: "CuentasComerciales",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AltasImportadas_Envios_EnvioKey_TenantId",
                        columns: x => new { x.EnvioKey, x.TenantId },
                        principalTable: "Envios",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AltasImportadas_Operadores_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AltasImportadas_CuentaComercialKey_TenantId",
                table: "AltasImportadas",
                columns: new[] { "CuentaComercialKey", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AltasImportadas_EnvioKey_TenantId",
                table: "AltasImportadas",
                columns: new[] { "EnvioKey", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AltasImportadas_TenantId",
                table: "AltasImportadas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AltasImportadas_TenantId_CuentaComercialKey_Referencia",
                table: "AltasImportadas",
                columns: new[] { "TenantId", "CuentaComercialKey", "Referencia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AltasImportadas");
        }
    }
}
