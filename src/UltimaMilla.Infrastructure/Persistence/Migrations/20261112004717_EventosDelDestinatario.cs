using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventosDelDestinatario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ResponsableId",
                table: "EventosDeEnvio",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EventosDeEnvio_ResponsableSegunOrigen",
                table: "EventosDeEnvio",
                sql: "(\"ResponsableId\" IS NULL) = (\"Origen\" = 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EventosDeEnvio_ResponsableSegunOrigen",
                table: "EventosDeEnvio");

            migrationBuilder.AlterColumn<Guid>(
                name: "ResponsableId",
                table: "EventosDeEnvio",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
