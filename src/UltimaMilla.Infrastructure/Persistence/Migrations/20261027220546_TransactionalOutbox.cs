using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransactionalOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MensajesOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Contenido = table.Column<string>(type: "jsonb", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublicadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensajesOutbox", x => x.Id);
                    table.UniqueConstraint("AK_MensajesOutbox_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_MensajesOutbox_EventosDeEnvio_EventoId_TenantId",
                        columns: x => new { x.EventoId, x.TenantId },
                        principalTable: "EventosDeEnvio",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MensajesOutbox_Operadores_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MensajesOutbox_CreadoEn_Id",
                table: "MensajesOutbox",
                columns: new[] { "CreadoEn", "Id" },
                filter: "\"PublicadoEn\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MensajesOutbox_EventoId",
                table: "MensajesOutbox",
                column: "EventoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MensajesOutbox_EventoId_TenantId",
                table: "MensajesOutbox",
                columns: new[] { "EventoId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_MensajesOutbox_TenantId",
                table: "MensajesOutbox",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MensajesOutbox");
        }
    }
}
