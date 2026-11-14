using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdministracionCuentaComercial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Activa",
                table: "CuentasComerciales",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "CuentasComerciales",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RazonSocial",
                table: "CuentasComerciales",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "CuentasComerciales",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "CuentasComerciales",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            migrationBuilder.Sql("""
                UPDATE "CuentasComerciales" AS c
                SET "RazonSocial" = g."RazonSocial", "Telefono" = g."Telefono", "Email" = g."Email",
                    "Version" = gen_random_uuid()
                FROM "Comercios" AS g WHERE c."ComercioId" = g."Id";

                CREATE FUNCTION validar_cuenta_activa_envio() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE habilitada boolean;
                BEGIN
                    SELECT "Activa" INTO habilitada FROM "CuentasComerciales"
                    WHERE "Id" = NEW."CuentaComercialId" AND "TenantId" = NEW."TenantId" FOR SHARE;
                    IF habilitada IS NOT TRUE THEN
                        RAISE EXCEPTION 'La cuenta comercial no admite nuevos envíos'
                          USING ERRCODE = '23514', CONSTRAINT = 'CK_Envios_CuentaActiva';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER validar_cuenta_activa_envio BEFORE INSERT ON "Envios"
                FOR EACH ROW EXECUTE FUNCTION validar_cuenta_activa_envio();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER validar_cuenta_activa_envio ON \"Envios\"; DROP FUNCTION validar_cuenta_activa_envio();");
            migrationBuilder.DropColumn(
                name: "Activa",
                table: "CuentasComerciales");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "CuentasComerciales");

            migrationBuilder.DropColumn(
                name: "RazonSocial",
                table: "CuentasComerciales");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "CuentasComerciales");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CuentasComerciales");
        }
    }
}
