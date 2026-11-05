using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MensajeProcesado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MensajesProcesados",
                columns: table => new
                {
                    MensajeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Consumidor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProcesadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajesProcesados", x => new { x.MensajeId, x.Consumidor });
                    table.ForeignKey(
                        name: "FK_MensajesProcesados_Operadores_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MensajesProcesados_TenantId",
                table: "MensajesProcesados",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MensajesProcesados");
        }
    }
}
