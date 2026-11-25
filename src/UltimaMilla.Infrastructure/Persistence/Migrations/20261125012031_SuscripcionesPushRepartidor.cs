using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SuscripcionesPushRepartidor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SuscripcionesPush",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    P256dh = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Auth = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualizadaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuscripcionesPush", x => x.Id);
                    table.UniqueConstraint("AK_SuscripcionesPush_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_SuscripcionesPush_Operadores_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SuscripcionesPush_Usuarios_UsuarioId_TenantId",
                        columns: x => new { x.UsuarioId, x.TenantId },
                        principalTable: "Usuarios",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesPush_Endpoint_TenantId",
                table: "SuscripcionesPush",
                columns: new[] { "Endpoint", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesPush_TenantId",
                table: "SuscripcionesPush",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesPush_UsuarioId_TenantId",
                table: "SuscripcionesPush",
                columns: new[] { "UsuarioId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuscripcionesPush");
        }
    }
}
