using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResultadosEntregaPorBulto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HuellaSolicitud",
                table: "IntentosEntrega",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PruebasEntrega_Id_IntentoId",
                table: "PruebasEntrega",
                columns: new[] { "Id", "IntentoId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PruebasEntrega_Id_IntentoId_TenantId",
                table: "PruebasEntrega",
                columns: new[] { "Id", "IntentoId", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_IntentosEntrega_Id_EnvioId",
                table: "IntentosEntrega",
                columns: new[] { "Id", "EnvioId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_IntentosEntrega_Id_EnvioId_TenantId",
                table: "IntentosEntrega",
                columns: new[] { "Id", "EnvioId", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Bultos_Id_EnvioId",
                table: "Bultos",
                columns: new[] { "Id", "EnvioId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Bultos_Id_EnvioId_TenantId",
                table: "Bultos",
                columns: new[] { "Id", "EnvioId", "TenantId" });

            migrationBuilder.CreateTable(
                name: "DetallesIntentoBulto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IntentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    BultoId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Resultado = table.Column<int>(type: "integer", nullable: false),
                    MotivoId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesIntentoBulto", x => x.Id);
                    table.UniqueConstraint("AK_DetallesIntentoBulto_Id_IntentoId", x => new { x.Id, x.IntentoId });
                    table.UniqueConstraint("AK_DetallesIntentoBulto_Id_IntentoId_TenantId", x => new { x.Id, x.IntentoId, x.TenantId });
                    table.UniqueConstraint("AK_DetallesIntentoBulto_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.CheckConstraint("CK_DetalleIntento_ResultadoMotivo", "(\"Resultado\" = 0 AND \"MotivoId\" IS NULL) OR (\"Resultado\" = 1 AND \"MotivoId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_DetallesIntentoBulto_Bultos_BultoId_EnvioId_TenantId",
                        columns: x => new { x.BultoId, x.EnvioId, x.TenantId },
                        principalTable: "Bultos",
                        principalColumns: new[] { "Id", "EnvioId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetallesIntentoBulto_IntentosEntrega_IntentoId_EnvioId_Tena~",
                        columns: x => new { x.IntentoId, x.EnvioId, x.TenantId },
                        principalTable: "IntentosEntrega",
                        principalColumns: new[] { "Id", "EnvioId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetallesIntentoBulto_MotivosNoEntrega_MotivoId_TenantId",
                        columns: x => new { x.MotivoId, x.TenantId },
                        principalTable: "MotivosNoEntrega",
                        principalColumns: new[] { "Id", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetallesIntentoBulto_Operadores_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PruebasBulto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DetalleId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PruebaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PruebasBulto", x => x.Id);
                    table.UniqueConstraint("AK_PruebasBulto_Id_TenantId", x => new { x.Id, x.TenantId });
                    table.ForeignKey(
                        name: "FK_PruebasBulto_DetallesIntentoBulto_DetalleId_IntentoId_Tenan~",
                        columns: x => new { x.DetalleId, x.IntentoId, x.TenantId },
                        principalTable: "DetallesIntentoBulto",
                        principalColumns: new[] { "Id", "IntentoId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PruebasBulto_Operadores_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PruebasBulto_PruebasEntrega_PruebaId_IntentoId_TenantId",
                        columns: x => new { x.PruebaId, x.IntentoId, x.TenantId },
                        principalTable: "PruebasEntrega",
                        principalColumns: new[] { "Id", "IntentoId", "TenantId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesIntentoBulto_BultoId_EnvioId_TenantId",
                table: "DetallesIntentoBulto",
                columns: new[] { "BultoId", "EnvioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesIntentoBulto_IntentoId_BultoId",
                table: "DetallesIntentoBulto",
                columns: new[] { "IntentoId", "BultoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetallesIntentoBulto_IntentoId_EnvioId_TenantId",
                table: "DetallesIntentoBulto",
                columns: new[] { "IntentoId", "EnvioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesIntentoBulto_MotivoId_TenantId",
                table: "DetallesIntentoBulto",
                columns: new[] { "MotivoId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesIntentoBulto_TenantId",
                table: "DetallesIntentoBulto",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PruebasBulto_DetalleId_IntentoId_TenantId",
                table: "PruebasBulto",
                columns: new[] { "DetalleId", "IntentoId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PruebasBulto_DetalleId_PruebaId",
                table: "PruebasBulto",
                columns: new[] { "DetalleId", "PruebaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PruebasBulto_PruebaId_IntentoId_TenantId",
                table: "PruebasBulto",
                columns: new[] { "PruebaId", "IntentoId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PruebasBulto_TenantId",
                table: "PruebasBulto",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PruebasBulto");

            migrationBuilder.DropTable(
                name: "DetallesIntentoBulto");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PruebasEntrega_Id_IntentoId",
                table: "PruebasEntrega");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PruebasEntrega_Id_IntentoId_TenantId",
                table: "PruebasEntrega");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_IntentosEntrega_Id_EnvioId",
                table: "IntentosEntrega");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_IntentosEntrega_Id_EnvioId_TenantId",
                table: "IntentosEntrega");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Bultos_Id_EnvioId",
                table: "Bultos");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Bultos_Id_EnvioId_TenantId",
                table: "Bultos");

            migrationBuilder.DropColumn(
                name: "HuellaSolicitud",
                table: "IntentosEntrega");
        }
    }
}
