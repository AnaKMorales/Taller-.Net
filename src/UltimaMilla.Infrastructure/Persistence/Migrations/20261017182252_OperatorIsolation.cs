using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperatorIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AjustesTarifarios_LineasTarifa_LineaId",
                table: "AjustesTarifarios");

            migrationBuilder.DropForeignKey(
                name: "FK_Bultos_Envios_EnvioId",
                table: "Bultos");

            migrationBuilder.DropForeignKey(
                name: "FK_Destinatarios_Direcciones_DireccionId",
                table: "Destinatarios");

            migrationBuilder.DropForeignKey(
                name: "FK_Devoluciones_Envios_EnvioId",
                table: "Devoluciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Discrepancias_Bultos_BultoId",
                table: "Discrepancias");

            migrationBuilder.DropForeignKey(
                name: "FK_Discrepancias_Envios_EnvioId",
                table: "Discrepancias");

            migrationBuilder.DropForeignKey(
                name: "FK_EntregasAviso_EventosDeEnvio_EventoId",
                table: "EntregasAviso");

            migrationBuilder.DropForeignKey(
                name: "FK_EntregasAviso_SuscripcionesAviso_SuscripcionId",
                table: "EntregasAviso");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_CuadrosTarifarios_CuadroTarifarioId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_CuentasComerciales_CuentaComercialId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_Destinatarios_DestinatarioId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_Paradas_ParadaId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_ProgramacionesEntrega_ProgramacionId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_ReglasOperativas_ReglasId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_ZonasCobertura_ZonaId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_EscaneosBulto_Bultos_BultoId",
                table: "EscaneosBulto");

            migrationBuilder.DropForeignKey(
                name: "FK_EscaneosBulto_Devoluciones_DevolucionId",
                table: "EscaneosBulto");

            migrationBuilder.DropForeignKey(
                name: "FK_EscaneosBulto_Rutas_RutaId",
                table: "EscaneosBulto");

            migrationBuilder.DropForeignKey(
                name: "FK_EscaneosBulto_Usuarios_ResponsableId",
                table: "EscaneosBulto");

            migrationBuilder.DropForeignKey(
                name: "FK_EventosDeEnvio_Envios_EnvioId",
                table: "EventosDeEnvio");

            migrationBuilder.DropForeignKey(
                name: "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionAnteriorId",
                table: "EventosDeEnvio");

            migrationBuilder.DropForeignKey(
                name: "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionNuevaId",
                table: "EventosDeEnvio");

            migrationBuilder.DropForeignKey(
                name: "FK_EventosDeEnvio_Usuarios_ResponsableId",
                table: "EventosDeEnvio");

            migrationBuilder.DropForeignKey(
                name: "FK_FranjasHorarias_ZonasCobertura_ZonaId",
                table: "FranjasHorarias");

            migrationBuilder.DropForeignKey(
                name: "FK_IntentosEntrega_Envios_EnvioId",
                table: "IntentosEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_IntentosEntrega_MotivosNoEntrega_MotivoNoEntregaId",
                table: "IntentosEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_IntentosEntrega_Paradas_ParadaId",
                table: "IntentosEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_LineasTarifa_CuadrosTarifarios_CuadroId",
                table: "LineasTarifa");

            migrationBuilder.DropForeignKey(
                name: "FK_LineasTarifa_ZonasCobertura_ZonaId",
                table: "LineasTarifa");

            migrationBuilder.DropForeignKey(
                name: "FK_MotivosNoEntrega_ReglasOperativas_ReglasId",
                table: "MotivosNoEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_Notificaciones_Destinatarios_DestinatarioId",
                table: "Notificaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Paradas_Envios_EnvioId",
                table: "Paradas");

            migrationBuilder.DropForeignKey(
                name: "FK_Paradas_Rutas_RutaId",
                table: "Paradas");

            migrationBuilder.DropForeignKey(
                name: "FK_PlazosServicio_ReglasOperativas_ReglasId",
                table: "PlazosServicio");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgramacionesEntrega_FranjasHorarias_FranjaId",
                table: "ProgramacionesEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_PruebasEntrega_IntentosEntrega_IntentoId",
                table: "PruebasEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_ReglasOperativas_PoliticasDevolucion_PoliticaDevolucionId",
                table: "ReglasOperativas");

            migrationBuilder.DropForeignKey(
                name: "FK_Rendiciones_Rutas_RutaId",
                table: "Rendiciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Rutas_Usuarios_RepartidorId",
                table: "Rutas");

            migrationBuilder.DropForeignKey(
                name: "FK_Rutas_Vehiculos_VehiculoId",
                table: "Rutas");

            migrationBuilder.DropForeignKey(
                name: "FK_SuscripcionesAviso_CuentasComerciales_CuentaComercialId",
                table: "SuscripcionesAviso");

            migrationBuilder.DropForeignKey(
                name: "FK_UsuariosCuentasComerciales_CuentasComerciales_CuentaComerci~",
                table: "UsuariosCuentasComerciales");

            migrationBuilder.DropForeignKey(
                name: "FK_UsuariosCuentasComerciales_Usuarios_UsuarioId",
                table: "UsuariosCuentasComerciales");

            migrationBuilder.DropIndex(
                name: "IX_UsuariosCuentasComerciales_CuentaComercialId",
                table: "UsuariosCuentasComerciales");

            migrationBuilder.DropIndex(
                name: "IX_SuscripcionesAviso_CuentaComercialId",
                table: "SuscripcionesAviso");

            migrationBuilder.DropIndex(
                name: "IX_Rutas_RepartidorId",
                table: "Rutas");

            migrationBuilder.DropIndex(
                name: "IX_Rutas_VehiculoId",
                table: "Rutas");

            migrationBuilder.DropIndex(
                name: "IX_Rendiciones_RutaId",
                table: "Rendiciones");

            migrationBuilder.DropIndex(
                name: "IX_ReglasOperativas_PoliticaDevolucionId",
                table: "ReglasOperativas");

            migrationBuilder.DropIndex(
                name: "IX_PruebasEntrega_IntentoId",
                table: "PruebasEntrega");

            migrationBuilder.DropIndex(
                name: "IX_ProgramacionesEntrega_FranjaId",
                table: "ProgramacionesEntrega");

            migrationBuilder.DropIndex(
                name: "IX_Notificaciones_DestinatarioId",
                table: "Notificaciones");

            migrationBuilder.DropIndex(
                name: "IX_MotivosNoEntrega_ReglasId",
                table: "MotivosNoEntrega");

            migrationBuilder.DropIndex(
                name: "IX_LineasTarifa_CuadroId",
                table: "LineasTarifa");

            migrationBuilder.DropIndex(
                name: "IX_LineasTarifa_ZonaId",
                table: "LineasTarifa");

            migrationBuilder.DropIndex(
                name: "IX_IntentosEntrega_MotivoNoEntregaId",
                table: "IntentosEntrega");

            migrationBuilder.DropIndex(
                name: "IX_IntentosEntrega_ParadaId",
                table: "IntentosEntrega");

            migrationBuilder.DropIndex(
                name: "IX_FranjasHorarias_ZonaId",
                table: "FranjasHorarias");

            migrationBuilder.DropIndex(
                name: "IX_EventosDeEnvio_ProgramacionAnteriorId",
                table: "EventosDeEnvio");

            migrationBuilder.DropIndex(
                name: "IX_EventosDeEnvio_ProgramacionNuevaId",
                table: "EventosDeEnvio");

            migrationBuilder.DropIndex(
                name: "IX_EventosDeEnvio_ResponsableId",
                table: "EventosDeEnvio");

            migrationBuilder.DropIndex(
                name: "IX_EscaneosBulto_BultoId",
                table: "EscaneosBulto");

            migrationBuilder.DropIndex(
                name: "IX_EscaneosBulto_DevolucionId",
                table: "EscaneosBulto");

            migrationBuilder.DropIndex(
                name: "IX_EscaneosBulto_ResponsableId",
                table: "EscaneosBulto");

            migrationBuilder.DropIndex(
                name: "IX_EscaneosBulto_RutaId",
                table: "EscaneosBulto");

            migrationBuilder.DropIndex(
                name: "IX_Envios_CuadroTarifarioId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_CuentaComercialId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_DestinatarioId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_ParadaId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_ProgramacionId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_ReglasId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_ZonaId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_EntregasAviso_EventoId",
                table: "EntregasAviso");

            migrationBuilder.DropIndex(
                name: "IX_Discrepancias_BultoId",
                table: "Discrepancias");

            migrationBuilder.DropIndex(
                name: "IX_Discrepancias_EnvioId",
                table: "Discrepancias");

            migrationBuilder.DropIndex(
                name: "IX_Devoluciones_EnvioId",
                table: "Devoluciones");

            migrationBuilder.DropIndex(
                name: "IX_Destinatarios_DireccionId",
                table: "Destinatarios");

            migrationBuilder.DropIndex(
                name: "IX_AjustesTarifarios_LineaId",
                table: "AjustesTarifarios");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ZonasCobertura",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Vehiculos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UsuariosCuentasComerciales",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Usuarios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "SuscripcionesAviso",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Rutas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Rendiciones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ReglasOperativas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PruebasEntrega",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProgramacionesEntrega",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PoliticasDevolucion",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PlazosServicio",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Paradas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Notificaciones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "MotivosNoEntrega",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "LineasTarifa",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "IntentosEntrega",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "FranjasHorarias",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "EventosDeEnvio",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "EscaneosBulto",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Envios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "EntregasAviso",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Discrepancias",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Direcciones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Devoluciones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Destinatarios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CuentasComerciales",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CuadrosTarifarios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Bultos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AjustesTarifarios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Recuperar el operador de los datos existentes antes de exigir las nuevas FK.
            // Un registro sin propietario o inconsistente hace fallar la migración; no se borra.
            migrationBuilder.Sql("""
                UPDATE "CuentasComerciales" SET "TenantId" = "OperadorId";
                UPDATE "Usuarios" SET "TenantId" = "OperadorId";
                UPDATE "Vehiculos" SET "TenantId" = "OperadorId";
                UPDATE "ZonasCobertura" SET "TenantId" = "OperadorId";
                UPDATE "ReglasOperativas" SET "TenantId" = "OperadorId";
                UPDATE "CuadrosTarifarios" SET "TenantId" = "OperadorId";
                UPDATE "FranjasHorarias" AS d SET "TenantId" = p."TenantId" FROM "ZonasCobertura" AS p WHERE d."ZonaId" = p."Id";
                UPDATE "PlazosServicio" AS d SET "TenantId" = p."TenantId" FROM "ReglasOperativas" AS p WHERE d."ReglasId" = p."Id";
                UPDATE "MotivosNoEntrega" AS d SET "TenantId" = p."TenantId" FROM "ReglasOperativas" AS p WHERE d."ReglasId" = p."Id";
                UPDATE "LineasTarifa" AS d SET "TenantId" = p."TenantId" FROM "CuadrosTarifarios" AS p WHERE d."CuadroId" = p."Id";
                UPDATE "AjustesTarifarios" AS d SET "TenantId" = p."TenantId" FROM "LineasTarifa" AS p WHERE d."LineaId" = p."Id";
                UPDATE "Envios" AS d SET "TenantId" = p."TenantId" FROM "CuentasComerciales" AS p WHERE d."CuentaComercialId" = p."Id";
                UPDATE "ProgramacionesEntrega" AS d SET "TenantId" = p."TenantId" FROM "FranjasHorarias" AS p WHERE d."FranjaId" = p."Id";
                UPDATE "Rutas" AS d SET "TenantId" = p."TenantId" FROM "Vehiculos" AS p WHERE d."VehiculoId" = p."Id";
                UPDATE "Paradas" AS d SET "TenantId" = p."TenantId" FROM "Rutas" AS p WHERE d."RutaId" = p."Id";
                UPDATE "IntentosEntrega" AS d SET "TenantId" = p."TenantId" FROM "Envios" AS p WHERE d."EnvioId" = p."Id";
                UPDATE "PruebasEntrega" AS d SET "TenantId" = p."TenantId" FROM "IntentosEntrega" AS p WHERE d."IntentoId" = p."Id";
                UPDATE "Bultos" AS d SET "TenantId" = p."TenantId" FROM "Envios" AS p WHERE d."EnvioId" = p."Id";
                UPDATE "EventosDeEnvio" AS d SET "TenantId" = p."TenantId" FROM "Envios" AS p WHERE d."EnvioId" = p."Id";
                UPDATE "Devoluciones" AS d SET "TenantId" = p."TenantId" FROM "Envios" AS p WHERE d."EnvioId" = p."Id";
                UPDATE "Discrepancias" AS d SET "TenantId" = p."TenantId" FROM "Envios" AS p WHERE d."EnvioId" = p."Id";
                UPDATE "EscaneosBulto" AS d SET "TenantId" = p."TenantId" FROM "Bultos" AS p WHERE d."BultoId" = p."Id";
                UPDATE "Rendiciones" AS d SET "TenantId" = p."TenantId" FROM "Rutas" AS p WHERE d."RutaId" = p."Id";
                UPDATE "SuscripcionesAviso" AS d SET "TenantId" = p."TenantId" FROM "CuentasComerciales" AS p WHERE d."CuentaComercialId" = p."Id";
                UPDATE "EntregasAviso" AS d SET "TenantId" = p."TenantId" FROM "SuscripcionesAviso" AS p WHERE d."SuscripcionId" = p."Id";
                UPDATE "UsuariosCuentasComerciales" AS d SET "TenantId" = p."TenantId" FROM "Usuarios" AS p WHERE d."UsuarioId" = p."Id";
                UPDATE "PoliticasDevolucion" AS d SET "TenantId" = p."TenantId" FROM "ReglasOperativas" AS p WHERE p."PoliticaDevolucionId" = d."Id";
                UPDATE "Destinatarios" AS d SET "TenantId" = p."TenantId" FROM "Envios" AS p WHERE p."DestinatarioId" = d."Id";
                UPDATE "Direcciones" AS d SET "TenantId" = p."TenantId" FROM "Destinatarios" AS p WHERE p."DireccionId" = d."Id";
                UPDATE "Notificaciones" AS d SET "TenantId" = p."TenantId" FROM "Destinatarios" AS p WHERE d."DestinatarioId" = p."Id";
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ZonasCobertura_Id_TenantId",
                table: "ZonasCobertura",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Vehiculos_Id_TenantId",
                table: "Vehiculos",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Usuarios_Id_TenantId",
                table: "Usuarios",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SuscripcionesAviso_Id_TenantId",
                table: "SuscripcionesAviso",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Rutas_Id_TenantId",
                table: "Rutas",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Rendiciones_Id_TenantId",
                table: "Rendiciones",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ReglasOperativas_Id_TenantId",
                table: "ReglasOperativas",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PruebasEntrega_Id_TenantId",
                table: "PruebasEntrega",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ProgramacionesEntrega_Id_TenantId",
                table: "ProgramacionesEntrega",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PoliticasDevolucion_Id_TenantId",
                table: "PoliticasDevolucion",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PlazosServicio_Id_TenantId",
                table: "PlazosServicio",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Paradas_Id_TenantId",
                table: "Paradas",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Notificaciones_Id_TenantId",
                table: "Notificaciones",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MotivosNoEntrega_Id_TenantId",
                table: "MotivosNoEntrega",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_LineasTarifa_Id_TenantId",
                table: "LineasTarifa",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_IntentosEntrega_Id_TenantId",
                table: "IntentosEntrega",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_FranjasHorarias_Id_TenantId",
                table: "FranjasHorarias",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EventosDeEnvio_Id_TenantId",
                table: "EventosDeEnvio",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EscaneosBulto_Id_TenantId",
                table: "EscaneosBulto",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Envios_Id_TenantId",
                table: "Envios",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_EntregasAviso_Id_TenantId",
                table: "EntregasAviso",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Discrepancias_Id_TenantId",
                table: "Discrepancias",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Direcciones_Id_TenantId",
                table: "Direcciones",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Devoluciones_Id_TenantId",
                table: "Devoluciones",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Destinatarios_Id_TenantId",
                table: "Destinatarios",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CuentasComerciales_Id_TenantId",
                table: "CuentasComerciales",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CuadrosTarifarios_Id_TenantId",
                table: "CuadrosTarifarios",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Bultos_Id_TenantId",
                table: "Bultos",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AjustesTarifarios_Id_TenantId",
                table: "AjustesTarifarios",
                columns: new[] { "Id", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ZonasCobertura_TenantId",
                table: "ZonasCobertura",
                column: "TenantId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ZonasCobertura_OperadorTenant",
                table: "ZonasCobertura",
                sql: "\"OperadorId\" = \"TenantId\"");

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_TenantId",
                table: "Vehiculos",
                column: "TenantId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehiculos_OperadorTenant",
                table: "Vehiculos",
                sql: "\"OperadorId\" = \"TenantId\"");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosCuentasComerciales_CuentaComercialId_TenantId",
                table: "UsuariosCuentasComerciales",
                columns: new[] { "CuentaComercialId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosCuentasComerciales_TenantId",
                table: "UsuariosCuentasComerciales",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosCuentasComerciales_UsuarioId_TenantId",
                table: "UsuariosCuentasComerciales",
                columns: new[] { "UsuarioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_TenantId",
                table: "Usuarios",
                column: "TenantId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Usuarios_OperadorTenant",
                table: "Usuarios",
                sql: "\"OperadorId\" = \"TenantId\"");

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesAviso_CuentaComercialId_TenantId",
                table: "SuscripcionesAviso",
                columns: new[] { "CuentaComercialId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesAviso_TenantId",
                table: "SuscripcionesAviso",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Rutas_RepartidorId_TenantId",
                table: "Rutas",
                columns: new[] { "RepartidorId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Rutas_TenantId",
                table: "Rutas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Rutas_VehiculoId_TenantId",
                table: "Rutas",
                columns: new[] { "VehiculoId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Rendiciones_RutaId_TenantId",
                table: "Rendiciones",
                columns: new[] { "RutaId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rendiciones_TenantId",
                table: "Rendiciones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReglasOperativas_PoliticaDevolucionId_TenantId",
                table: "ReglasOperativas",
                columns: new[] { "PoliticaDevolucionId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReglasOperativas_TenantId",
                table: "ReglasOperativas",
                column: "TenantId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReglasOperativas_OperadorTenant",
                table: "ReglasOperativas",
                sql: "\"OperadorId\" = \"TenantId\"");

            migrationBuilder.CreateIndex(
                name: "IX_PruebasEntrega_IntentoId_TenantId",
                table: "PruebasEntrega",
                columns: new[] { "IntentoId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PruebasEntrega_TenantId",
                table: "PruebasEntrega",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramacionesEntrega_FranjaId_TenantId",
                table: "ProgramacionesEntrega",
                columns: new[] { "FranjaId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramacionesEntrega_TenantId",
                table: "ProgramacionesEntrega",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PoliticasDevolucion_TenantId",
                table: "PoliticasDevolucion",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PlazosServicio_ReglasId_TenantId",
                table: "PlazosServicio",
                columns: new[] { "ReglasId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlazosServicio_TenantId",
                table: "PlazosServicio",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Paradas_EnvioId_TenantId",
                table: "Paradas",
                columns: new[] { "EnvioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Paradas_RutaId_TenantId",
                table: "Paradas",
                columns: new[] { "RutaId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Paradas_TenantId",
                table: "Paradas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_DestinatarioId_TenantId",
                table: "Notificaciones",
                columns: new[] { "DestinatarioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_TenantId",
                table: "Notificaciones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MotivosNoEntrega_ReglasId_TenantId",
                table: "MotivosNoEntrega",
                columns: new[] { "ReglasId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_MotivosNoEntrega_TenantId",
                table: "MotivosNoEntrega",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasTarifa_CuadroId_TenantId",
                table: "LineasTarifa",
                columns: new[] { "CuadroId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_LineasTarifa_TenantId",
                table: "LineasTarifa",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasTarifa_ZonaId_TenantId",
                table: "LineasTarifa",
                columns: new[] { "ZonaId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntentosEntrega_EnvioId_TenantId",
                table: "IntentosEntrega",
                columns: new[] { "EnvioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntentosEntrega_MotivoNoEntregaId_TenantId",
                table: "IntentosEntrega",
                columns: new[] { "MotivoNoEntregaId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntentosEntrega_ParadaId_TenantId",
                table: "IntentosEntrega",
                columns: new[] { "ParadaId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntentosEntrega_TenantId",
                table: "IntentosEntrega",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FranjasHorarias_TenantId",
                table: "FranjasHorarias",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FranjasHorarias_ZonaId_TenantId",
                table: "FranjasHorarias",
                columns: new[] { "ZonaId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosDeEnvio_EnvioId_TenantId",
                table: "EventosDeEnvio",
                columns: new[] { "EnvioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosDeEnvio_ProgramacionAnteriorId_TenantId",
                table: "EventosDeEnvio",
                columns: new[] { "ProgramacionAnteriorId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosDeEnvio_ProgramacionNuevaId_TenantId",
                table: "EventosDeEnvio",
                columns: new[] { "ProgramacionNuevaId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosDeEnvio_ResponsableId_TenantId",
                table: "EventosDeEnvio",
                columns: new[] { "ResponsableId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosDeEnvio_TenantId",
                table: "EventosDeEnvio",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EscaneosBulto_BultoId_TenantId",
                table: "EscaneosBulto",
                columns: new[] { "BultoId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EscaneosBulto_DevolucionId_TenantId",
                table: "EscaneosBulto",
                columns: new[] { "DevolucionId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EscaneosBulto_ResponsableId_TenantId",
                table: "EscaneosBulto",
                columns: new[] { "ResponsableId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EscaneosBulto_RutaId_TenantId",
                table: "EscaneosBulto",
                columns: new[] { "RutaId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EscaneosBulto_TenantId",
                table: "EscaneosBulto",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Envios_CuadroTarifarioId_TenantId",
                table: "Envios",
                columns: new[] { "CuadroTarifarioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Envios_CuentaComercialId_TenantId",
                table: "Envios",
                columns: new[] { "CuentaComercialId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Envios_DestinatarioId_TenantId",
                table: "Envios",
                columns: new[] { "DestinatarioId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Envios_ParadaId_TenantId",
                table: "Envios",
                columns: new[] { "ParadaId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Envios_ProgramacionId_TenantId",
                table: "Envios",
                columns: new[] { "ProgramacionId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Envios_ReglasId_TenantId",
                table: "Envios",
                columns: new[] { "ReglasId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Envios_TenantId",
                table: "Envios",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Envios_ZonaId_TenantId",
                table: "Envios",
                columns: new[] { "ZonaId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EntregasAviso_EventoId_TenantId",
                table: "EntregasAviso",
                columns: new[] { "EventoId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EntregasAviso_SuscripcionId_TenantId",
                table: "EntregasAviso",
                columns: new[] { "SuscripcionId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_EntregasAviso_TenantId",
                table: "EntregasAviso",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancias_BultoId_TenantId",
                table: "Discrepancias",
                columns: new[] { "BultoId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancias_EnvioId_TenantId",
                table: "Discrepancias",
                columns: new[] { "EnvioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancias_TenantId",
                table: "Discrepancias",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Direcciones_TenantId",
                table: "Direcciones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Devoluciones_EnvioId_TenantId",
                table: "Devoluciones",
                columns: new[] { "EnvioId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devoluciones_TenantId",
                table: "Devoluciones",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Destinatarios_DireccionId_TenantId",
                table: "Destinatarios",
                columns: new[] { "DireccionId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Destinatarios_TenantId",
                table: "Destinatarios",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasComerciales_TenantId",
                table: "CuentasComerciales",
                column: "TenantId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CuentasComerciales_OperadorTenant",
                table: "CuentasComerciales",
                sql: "\"OperadorId\" = \"TenantId\"");

            migrationBuilder.CreateIndex(
                name: "IX_CuadrosTarifarios_TenantId",
                table: "CuadrosTarifarios",
                column: "TenantId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CuadrosTarifarios_OperadorTenant",
                table: "CuadrosTarifarios",
                sql: "\"OperadorId\" = \"TenantId\"");

            migrationBuilder.CreateIndex(
                name: "IX_Bultos_EnvioId_TenantId",
                table: "Bultos",
                columns: new[] { "EnvioId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bultos_TenantId",
                table: "Bultos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AjustesTarifarios_LineaId_TenantId",
                table: "AjustesTarifarios",
                columns: new[] { "LineaId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AjustesTarifarios_TenantId",
                table: "AjustesTarifarios",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AjustesTarifarios_LineasTarifa_LineaId_TenantId",
                table: "AjustesTarifarios",
                columns: new[] { "LineaId", "TenantId" },
                principalTable: "LineasTarifa",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AjustesTarifarios_Operadores_TenantId",
                table: "AjustesTarifarios",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bultos_Envios_EnvioId_TenantId",
                table: "Bultos",
                columns: new[] { "EnvioId", "TenantId" },
                principalTable: "Envios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bultos_Operadores_TenantId",
                table: "Bultos",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CuadrosTarifarios_Operadores_TenantId",
                table: "CuadrosTarifarios",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CuentasComerciales_Operadores_TenantId",
                table: "CuentasComerciales",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Destinatarios_Direcciones_DireccionId_TenantId",
                table: "Destinatarios",
                columns: new[] { "DireccionId", "TenantId" },
                principalTable: "Direcciones",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Destinatarios_Operadores_TenantId",
                table: "Destinatarios",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Devoluciones_Envios_EnvioId_TenantId",
                table: "Devoluciones",
                columns: new[] { "EnvioId", "TenantId" },
                principalTable: "Envios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Devoluciones_Operadores_TenantId",
                table: "Devoluciones",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Direcciones_Operadores_TenantId",
                table: "Direcciones",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Discrepancias_Bultos_BultoId_TenantId",
                table: "Discrepancias",
                columns: new[] { "BultoId", "TenantId" },
                principalTable: "Bultos",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Discrepancias_Envios_EnvioId_TenantId",
                table: "Discrepancias",
                columns: new[] { "EnvioId", "TenantId" },
                principalTable: "Envios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Discrepancias_Operadores_TenantId",
                table: "Discrepancias",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EntregasAviso_EventosDeEnvio_EventoId_TenantId",
                table: "EntregasAviso",
                columns: new[] { "EventoId", "TenantId" },
                principalTable: "EventosDeEnvio",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EntregasAviso_Operadores_TenantId",
                table: "EntregasAviso",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EntregasAviso_SuscripcionesAviso_SuscripcionId_TenantId",
                table: "EntregasAviso",
                columns: new[] { "SuscripcionId", "TenantId" },
                principalTable: "SuscripcionesAviso",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_CuadrosTarifarios_CuadroTarifarioId_TenantId",
                table: "Envios",
                columns: new[] { "CuadroTarifarioId", "TenantId" },
                principalTable: "CuadrosTarifarios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_CuentasComerciales_CuentaComercialId_TenantId",
                table: "Envios",
                columns: new[] { "CuentaComercialId", "TenantId" },
                principalTable: "CuentasComerciales",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_Destinatarios_DestinatarioId_TenantId",
                table: "Envios",
                columns: new[] { "DestinatarioId", "TenantId" },
                principalTable: "Destinatarios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_Operadores_TenantId",
                table: "Envios",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_Paradas_ParadaId_TenantId",
                table: "Envios",
                columns: new[] { "ParadaId", "TenantId" },
                principalTable: "Paradas",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_ProgramacionesEntrega_ProgramacionId_TenantId",
                table: "Envios",
                columns: new[] { "ProgramacionId", "TenantId" },
                principalTable: "ProgramacionesEntrega",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_ReglasOperativas_ReglasId_TenantId",
                table: "Envios",
                columns: new[] { "ReglasId", "TenantId" },
                principalTable: "ReglasOperativas",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_ZonasCobertura_ZonaId_TenantId",
                table: "Envios",
                columns: new[] { "ZonaId", "TenantId" },
                principalTable: "ZonasCobertura",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscaneosBulto_Bultos_BultoId_TenantId",
                table: "EscaneosBulto",
                columns: new[] { "BultoId", "TenantId" },
                principalTable: "Bultos",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscaneosBulto_Devoluciones_DevolucionId_TenantId",
                table: "EscaneosBulto",
                columns: new[] { "DevolucionId", "TenantId" },
                principalTable: "Devoluciones",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscaneosBulto_Operadores_TenantId",
                table: "EscaneosBulto",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscaneosBulto_Rutas_RutaId_TenantId",
                table: "EscaneosBulto",
                columns: new[] { "RutaId", "TenantId" },
                principalTable: "Rutas",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscaneosBulto_Usuarios_ResponsableId_TenantId",
                table: "EscaneosBulto",
                columns: new[] { "ResponsableId", "TenantId" },
                principalTable: "Usuarios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventosDeEnvio_Envios_EnvioId_TenantId",
                table: "EventosDeEnvio",
                columns: new[] { "EnvioId", "TenantId" },
                principalTable: "Envios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventosDeEnvio_Operadores_TenantId",
                table: "EventosDeEnvio",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionAnteriorId~",
                table: "EventosDeEnvio",
                columns: new[] { "ProgramacionAnteriorId", "TenantId" },
                principalTable: "ProgramacionesEntrega",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionNuevaId_Te~",
                table: "EventosDeEnvio",
                columns: new[] { "ProgramacionNuevaId", "TenantId" },
                principalTable: "ProgramacionesEntrega",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventosDeEnvio_Usuarios_ResponsableId_TenantId",
                table: "EventosDeEnvio",
                columns: new[] { "ResponsableId", "TenantId" },
                principalTable: "Usuarios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FranjasHorarias_Operadores_TenantId",
                table: "FranjasHorarias",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FranjasHorarias_ZonasCobertura_ZonaId_TenantId",
                table: "FranjasHorarias",
                columns: new[] { "ZonaId", "TenantId" },
                principalTable: "ZonasCobertura",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntentosEntrega_Envios_EnvioId_TenantId",
                table: "IntentosEntrega",
                columns: new[] { "EnvioId", "TenantId" },
                principalTable: "Envios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntentosEntrega_MotivosNoEntrega_MotivoNoEntregaId_TenantId",
                table: "IntentosEntrega",
                columns: new[] { "MotivoNoEntregaId", "TenantId" },
                principalTable: "MotivosNoEntrega",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntentosEntrega_Operadores_TenantId",
                table: "IntentosEntrega",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntentosEntrega_Paradas_ParadaId_TenantId",
                table: "IntentosEntrega",
                columns: new[] { "ParadaId", "TenantId" },
                principalTable: "Paradas",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LineasTarifa_CuadrosTarifarios_CuadroId_TenantId",
                table: "LineasTarifa",
                columns: new[] { "CuadroId", "TenantId" },
                principalTable: "CuadrosTarifarios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LineasTarifa_Operadores_TenantId",
                table: "LineasTarifa",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LineasTarifa_ZonasCobertura_ZonaId_TenantId",
                table: "LineasTarifa",
                columns: new[] { "ZonaId", "TenantId" },
                principalTable: "ZonasCobertura",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MotivosNoEntrega_Operadores_TenantId",
                table: "MotivosNoEntrega",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MotivosNoEntrega_ReglasOperativas_ReglasId_TenantId",
                table: "MotivosNoEntrega",
                columns: new[] { "ReglasId", "TenantId" },
                principalTable: "ReglasOperativas",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notificaciones_Destinatarios_DestinatarioId_TenantId",
                table: "Notificaciones",
                columns: new[] { "DestinatarioId", "TenantId" },
                principalTable: "Destinatarios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notificaciones_Operadores_TenantId",
                table: "Notificaciones",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Paradas_Envios_EnvioId_TenantId",
                table: "Paradas",
                columns: new[] { "EnvioId", "TenantId" },
                principalTable: "Envios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Paradas_Operadores_TenantId",
                table: "Paradas",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Paradas_Rutas_RutaId_TenantId",
                table: "Paradas",
                columns: new[] { "RutaId", "TenantId" },
                principalTable: "Rutas",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlazosServicio_Operadores_TenantId",
                table: "PlazosServicio",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlazosServicio_ReglasOperativas_ReglasId_TenantId",
                table: "PlazosServicio",
                columns: new[] { "ReglasId", "TenantId" },
                principalTable: "ReglasOperativas",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PoliticasDevolucion_Operadores_TenantId",
                table: "PoliticasDevolucion",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramacionesEntrega_FranjasHorarias_FranjaId_TenantId",
                table: "ProgramacionesEntrega",
                columns: new[] { "FranjaId", "TenantId" },
                principalTable: "FranjasHorarias",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramacionesEntrega_Operadores_TenantId",
                table: "ProgramacionesEntrega",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PruebasEntrega_IntentosEntrega_IntentoId_TenantId",
                table: "PruebasEntrega",
                columns: new[] { "IntentoId", "TenantId" },
                principalTable: "IntentosEntrega",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PruebasEntrega_Operadores_TenantId",
                table: "PruebasEntrega",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReglasOperativas_Operadores_TenantId",
                table: "ReglasOperativas",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReglasOperativas_PoliticasDevolucion_PoliticaDevolucionId_T~",
                table: "ReglasOperativas",
                columns: new[] { "PoliticaDevolucionId", "TenantId" },
                principalTable: "PoliticasDevolucion",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rendiciones_Operadores_TenantId",
                table: "Rendiciones",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rendiciones_Rutas_RutaId_TenantId",
                table: "Rendiciones",
                columns: new[] { "RutaId", "TenantId" },
                principalTable: "Rutas",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rutas_Operadores_TenantId",
                table: "Rutas",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rutas_Usuarios_RepartidorId_TenantId",
                table: "Rutas",
                columns: new[] { "RepartidorId", "TenantId" },
                principalTable: "Usuarios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rutas_Vehiculos_VehiculoId_TenantId",
                table: "Rutas",
                columns: new[] { "VehiculoId", "TenantId" },
                principalTable: "Vehiculos",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SuscripcionesAviso_CuentasComerciales_CuentaComercialId_Ten~",
                table: "SuscripcionesAviso",
                columns: new[] { "CuentaComercialId", "TenantId" },
                principalTable: "CuentasComerciales",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SuscripcionesAviso_Operadores_TenantId",
                table: "SuscripcionesAviso",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Operadores_TenantId",
                table: "Usuarios",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UsuariosCuentasComerciales_CuentasComerciales_CuentaComerci~",
                table: "UsuariosCuentasComerciales",
                columns: new[] { "CuentaComercialId", "TenantId" },
                principalTable: "CuentasComerciales",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UsuariosCuentasComerciales_Operadores_TenantId",
                table: "UsuariosCuentasComerciales",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UsuariosCuentasComerciales_Usuarios_UsuarioId_TenantId",
                table: "UsuariosCuentasComerciales",
                columns: new[] { "UsuarioId", "TenantId" },
                principalTable: "Usuarios",
                principalColumns: new[] { "Id", "TenantId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehiculos_Operadores_TenantId",
                table: "Vehiculos",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ZonasCobertura_Operadores_TenantId",
                table: "ZonasCobertura",
                column: "TenantId",
                principalTable: "Operadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AjustesTarifarios_LineasTarifa_LineaId_TenantId",
                table: "AjustesTarifarios");

            migrationBuilder.DropForeignKey(
                name: "FK_AjustesTarifarios_Operadores_TenantId",
                table: "AjustesTarifarios");

            migrationBuilder.DropForeignKey(
                name: "FK_Bultos_Envios_EnvioId_TenantId",
                table: "Bultos");

            migrationBuilder.DropForeignKey(
                name: "FK_Bultos_Operadores_TenantId",
                table: "Bultos");

            migrationBuilder.DropForeignKey(
                name: "FK_CuadrosTarifarios_Operadores_TenantId",
                table: "CuadrosTarifarios");

            migrationBuilder.DropForeignKey(
                name: "FK_CuentasComerciales_Operadores_TenantId",
                table: "CuentasComerciales");

            migrationBuilder.DropForeignKey(
                name: "FK_Destinatarios_Direcciones_DireccionId_TenantId",
                table: "Destinatarios");

            migrationBuilder.DropForeignKey(
                name: "FK_Destinatarios_Operadores_TenantId",
                table: "Destinatarios");

            migrationBuilder.DropForeignKey(
                name: "FK_Devoluciones_Envios_EnvioId_TenantId",
                table: "Devoluciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Devoluciones_Operadores_TenantId",
                table: "Devoluciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Direcciones_Operadores_TenantId",
                table: "Direcciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Discrepancias_Bultos_BultoId_TenantId",
                table: "Discrepancias");

            migrationBuilder.DropForeignKey(
                name: "FK_Discrepancias_Envios_EnvioId_TenantId",
                table: "Discrepancias");

            migrationBuilder.DropForeignKey(
                name: "FK_Discrepancias_Operadores_TenantId",
                table: "Discrepancias");

            migrationBuilder.DropForeignKey(
                name: "FK_EntregasAviso_EventosDeEnvio_EventoId_TenantId",
                table: "EntregasAviso");

            migrationBuilder.DropForeignKey(
                name: "FK_EntregasAviso_Operadores_TenantId",
                table: "EntregasAviso");

            migrationBuilder.DropForeignKey(
                name: "FK_EntregasAviso_SuscripcionesAviso_SuscripcionId_TenantId",
                table: "EntregasAviso");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_CuadrosTarifarios_CuadroTarifarioId_TenantId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_CuentasComerciales_CuentaComercialId_TenantId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_Destinatarios_DestinatarioId_TenantId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_Operadores_TenantId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_Paradas_ParadaId_TenantId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_ProgramacionesEntrega_ProgramacionId_TenantId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_ReglasOperativas_ReglasId_TenantId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_Envios_ZonasCobertura_ZonaId_TenantId",
                table: "Envios");

            migrationBuilder.DropForeignKey(
                name: "FK_EscaneosBulto_Bultos_BultoId_TenantId",
                table: "EscaneosBulto");

            migrationBuilder.DropForeignKey(
                name: "FK_EscaneosBulto_Devoluciones_DevolucionId_TenantId",
                table: "EscaneosBulto");

            migrationBuilder.DropForeignKey(
                name: "FK_EscaneosBulto_Operadores_TenantId",
                table: "EscaneosBulto");

            migrationBuilder.DropForeignKey(
                name: "FK_EscaneosBulto_Rutas_RutaId_TenantId",
                table: "EscaneosBulto");

            migrationBuilder.DropForeignKey(
                name: "FK_EscaneosBulto_Usuarios_ResponsableId_TenantId",
                table: "EscaneosBulto");

            migrationBuilder.DropForeignKey(
                name: "FK_EventosDeEnvio_Envios_EnvioId_TenantId",
                table: "EventosDeEnvio");

            migrationBuilder.DropForeignKey(
                name: "FK_EventosDeEnvio_Operadores_TenantId",
                table: "EventosDeEnvio");

            migrationBuilder.DropForeignKey(
                name: "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionAnteriorId~",
                table: "EventosDeEnvio");

            migrationBuilder.DropForeignKey(
                name: "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionNuevaId_Te~",
                table: "EventosDeEnvio");

            migrationBuilder.DropForeignKey(
                name: "FK_EventosDeEnvio_Usuarios_ResponsableId_TenantId",
                table: "EventosDeEnvio");

            migrationBuilder.DropForeignKey(
                name: "FK_FranjasHorarias_Operadores_TenantId",
                table: "FranjasHorarias");

            migrationBuilder.DropForeignKey(
                name: "FK_FranjasHorarias_ZonasCobertura_ZonaId_TenantId",
                table: "FranjasHorarias");

            migrationBuilder.DropForeignKey(
                name: "FK_IntentosEntrega_Envios_EnvioId_TenantId",
                table: "IntentosEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_IntentosEntrega_MotivosNoEntrega_MotivoNoEntregaId_TenantId",
                table: "IntentosEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_IntentosEntrega_Operadores_TenantId",
                table: "IntentosEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_IntentosEntrega_Paradas_ParadaId_TenantId",
                table: "IntentosEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_LineasTarifa_CuadrosTarifarios_CuadroId_TenantId",
                table: "LineasTarifa");

            migrationBuilder.DropForeignKey(
                name: "FK_LineasTarifa_Operadores_TenantId",
                table: "LineasTarifa");

            migrationBuilder.DropForeignKey(
                name: "FK_LineasTarifa_ZonasCobertura_ZonaId_TenantId",
                table: "LineasTarifa");

            migrationBuilder.DropForeignKey(
                name: "FK_MotivosNoEntrega_Operadores_TenantId",
                table: "MotivosNoEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_MotivosNoEntrega_ReglasOperativas_ReglasId_TenantId",
                table: "MotivosNoEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_Notificaciones_Destinatarios_DestinatarioId_TenantId",
                table: "Notificaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Notificaciones_Operadores_TenantId",
                table: "Notificaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Paradas_Envios_EnvioId_TenantId",
                table: "Paradas");

            migrationBuilder.DropForeignKey(
                name: "FK_Paradas_Operadores_TenantId",
                table: "Paradas");

            migrationBuilder.DropForeignKey(
                name: "FK_Paradas_Rutas_RutaId_TenantId",
                table: "Paradas");

            migrationBuilder.DropForeignKey(
                name: "FK_PlazosServicio_Operadores_TenantId",
                table: "PlazosServicio");

            migrationBuilder.DropForeignKey(
                name: "FK_PlazosServicio_ReglasOperativas_ReglasId_TenantId",
                table: "PlazosServicio");

            migrationBuilder.DropForeignKey(
                name: "FK_PoliticasDevolucion_Operadores_TenantId",
                table: "PoliticasDevolucion");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgramacionesEntrega_FranjasHorarias_FranjaId_TenantId",
                table: "ProgramacionesEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgramacionesEntrega_Operadores_TenantId",
                table: "ProgramacionesEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_PruebasEntrega_IntentosEntrega_IntentoId_TenantId",
                table: "PruebasEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_PruebasEntrega_Operadores_TenantId",
                table: "PruebasEntrega");

            migrationBuilder.DropForeignKey(
                name: "FK_ReglasOperativas_Operadores_TenantId",
                table: "ReglasOperativas");

            migrationBuilder.DropForeignKey(
                name: "FK_ReglasOperativas_PoliticasDevolucion_PoliticaDevolucionId_T~",
                table: "ReglasOperativas");

            migrationBuilder.DropForeignKey(
                name: "FK_Rendiciones_Operadores_TenantId",
                table: "Rendiciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Rendiciones_Rutas_RutaId_TenantId",
                table: "Rendiciones");

            migrationBuilder.DropForeignKey(
                name: "FK_Rutas_Operadores_TenantId",
                table: "Rutas");

            migrationBuilder.DropForeignKey(
                name: "FK_Rutas_Usuarios_RepartidorId_TenantId",
                table: "Rutas");

            migrationBuilder.DropForeignKey(
                name: "FK_Rutas_Vehiculos_VehiculoId_TenantId",
                table: "Rutas");

            migrationBuilder.DropForeignKey(
                name: "FK_SuscripcionesAviso_CuentasComerciales_CuentaComercialId_Ten~",
                table: "SuscripcionesAviso");

            migrationBuilder.DropForeignKey(
                name: "FK_SuscripcionesAviso_Operadores_TenantId",
                table: "SuscripcionesAviso");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Operadores_TenantId",
                table: "Usuarios");

            migrationBuilder.DropForeignKey(
                name: "FK_UsuariosCuentasComerciales_CuentasComerciales_CuentaComerci~",
                table: "UsuariosCuentasComerciales");

            migrationBuilder.DropForeignKey(
                name: "FK_UsuariosCuentasComerciales_Operadores_TenantId",
                table: "UsuariosCuentasComerciales");

            migrationBuilder.DropForeignKey(
                name: "FK_UsuariosCuentasComerciales_Usuarios_UsuarioId_TenantId",
                table: "UsuariosCuentasComerciales");

            migrationBuilder.DropForeignKey(
                name: "FK_Vehiculos_Operadores_TenantId",
                table: "Vehiculos");

            migrationBuilder.DropForeignKey(
                name: "FK_ZonasCobertura_Operadores_TenantId",
                table: "ZonasCobertura");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ZonasCobertura_Id_TenantId",
                table: "ZonasCobertura");

            migrationBuilder.DropIndex(
                name: "IX_ZonasCobertura_TenantId",
                table: "ZonasCobertura");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ZonasCobertura_OperadorTenant",
                table: "ZonasCobertura");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Vehiculos_Id_TenantId",
                table: "Vehiculos");

            migrationBuilder.DropIndex(
                name: "IX_Vehiculos_TenantId",
                table: "Vehiculos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehiculos_OperadorTenant",
                table: "Vehiculos");

            migrationBuilder.DropIndex(
                name: "IX_UsuariosCuentasComerciales_CuentaComercialId_TenantId",
                table: "UsuariosCuentasComerciales");

            migrationBuilder.DropIndex(
                name: "IX_UsuariosCuentasComerciales_TenantId",
                table: "UsuariosCuentasComerciales");

            migrationBuilder.DropIndex(
                name: "IX_UsuariosCuentasComerciales_UsuarioId_TenantId",
                table: "UsuariosCuentasComerciales");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Usuarios_Id_TenantId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_TenantId",
                table: "Usuarios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Usuarios_OperadorTenant",
                table: "Usuarios");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SuscripcionesAviso_Id_TenantId",
                table: "SuscripcionesAviso");

            migrationBuilder.DropIndex(
                name: "IX_SuscripcionesAviso_CuentaComercialId_TenantId",
                table: "SuscripcionesAviso");

            migrationBuilder.DropIndex(
                name: "IX_SuscripcionesAviso_TenantId",
                table: "SuscripcionesAviso");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Rutas_Id_TenantId",
                table: "Rutas");

            migrationBuilder.DropIndex(
                name: "IX_Rutas_RepartidorId_TenantId",
                table: "Rutas");

            migrationBuilder.DropIndex(
                name: "IX_Rutas_TenantId",
                table: "Rutas");

            migrationBuilder.DropIndex(
                name: "IX_Rutas_VehiculoId_TenantId",
                table: "Rutas");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Rendiciones_Id_TenantId",
                table: "Rendiciones");

            migrationBuilder.DropIndex(
                name: "IX_Rendiciones_RutaId_TenantId",
                table: "Rendiciones");

            migrationBuilder.DropIndex(
                name: "IX_Rendiciones_TenantId",
                table: "Rendiciones");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ReglasOperativas_Id_TenantId",
                table: "ReglasOperativas");

            migrationBuilder.DropIndex(
                name: "IX_ReglasOperativas_PoliticaDevolucionId_TenantId",
                table: "ReglasOperativas");

            migrationBuilder.DropIndex(
                name: "IX_ReglasOperativas_TenantId",
                table: "ReglasOperativas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReglasOperativas_OperadorTenant",
                table: "ReglasOperativas");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PruebasEntrega_Id_TenantId",
                table: "PruebasEntrega");

            migrationBuilder.DropIndex(
                name: "IX_PruebasEntrega_IntentoId_TenantId",
                table: "PruebasEntrega");

            migrationBuilder.DropIndex(
                name: "IX_PruebasEntrega_TenantId",
                table: "PruebasEntrega");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ProgramacionesEntrega_Id_TenantId",
                table: "ProgramacionesEntrega");

            migrationBuilder.DropIndex(
                name: "IX_ProgramacionesEntrega_FranjaId_TenantId",
                table: "ProgramacionesEntrega");

            migrationBuilder.DropIndex(
                name: "IX_ProgramacionesEntrega_TenantId",
                table: "ProgramacionesEntrega");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PoliticasDevolucion_Id_TenantId",
                table: "PoliticasDevolucion");

            migrationBuilder.DropIndex(
                name: "IX_PoliticasDevolucion_TenantId",
                table: "PoliticasDevolucion");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PlazosServicio_Id_TenantId",
                table: "PlazosServicio");

            migrationBuilder.DropIndex(
                name: "IX_PlazosServicio_ReglasId_TenantId",
                table: "PlazosServicio");

            migrationBuilder.DropIndex(
                name: "IX_PlazosServicio_TenantId",
                table: "PlazosServicio");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Paradas_Id_TenantId",
                table: "Paradas");

            migrationBuilder.DropIndex(
                name: "IX_Paradas_EnvioId_TenantId",
                table: "Paradas");

            migrationBuilder.DropIndex(
                name: "IX_Paradas_RutaId_TenantId",
                table: "Paradas");

            migrationBuilder.DropIndex(
                name: "IX_Paradas_TenantId",
                table: "Paradas");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Notificaciones_Id_TenantId",
                table: "Notificaciones");

            migrationBuilder.DropIndex(
                name: "IX_Notificaciones_DestinatarioId_TenantId",
                table: "Notificaciones");

            migrationBuilder.DropIndex(
                name: "IX_Notificaciones_TenantId",
                table: "Notificaciones");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MotivosNoEntrega_Id_TenantId",
                table: "MotivosNoEntrega");

            migrationBuilder.DropIndex(
                name: "IX_MotivosNoEntrega_ReglasId_TenantId",
                table: "MotivosNoEntrega");

            migrationBuilder.DropIndex(
                name: "IX_MotivosNoEntrega_TenantId",
                table: "MotivosNoEntrega");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_LineasTarifa_Id_TenantId",
                table: "LineasTarifa");

            migrationBuilder.DropIndex(
                name: "IX_LineasTarifa_CuadroId_TenantId",
                table: "LineasTarifa");

            migrationBuilder.DropIndex(
                name: "IX_LineasTarifa_TenantId",
                table: "LineasTarifa");

            migrationBuilder.DropIndex(
                name: "IX_LineasTarifa_ZonaId_TenantId",
                table: "LineasTarifa");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_IntentosEntrega_Id_TenantId",
                table: "IntentosEntrega");

            migrationBuilder.DropIndex(
                name: "IX_IntentosEntrega_EnvioId_TenantId",
                table: "IntentosEntrega");

            migrationBuilder.DropIndex(
                name: "IX_IntentosEntrega_MotivoNoEntregaId_TenantId",
                table: "IntentosEntrega");

            migrationBuilder.DropIndex(
                name: "IX_IntentosEntrega_ParadaId_TenantId",
                table: "IntentosEntrega");

            migrationBuilder.DropIndex(
                name: "IX_IntentosEntrega_TenantId",
                table: "IntentosEntrega");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FranjasHorarias_Id_TenantId",
                table: "FranjasHorarias");

            migrationBuilder.DropIndex(
                name: "IX_FranjasHorarias_TenantId",
                table: "FranjasHorarias");

            migrationBuilder.DropIndex(
                name: "IX_FranjasHorarias_ZonaId_TenantId",
                table: "FranjasHorarias");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EventosDeEnvio_Id_TenantId",
                table: "EventosDeEnvio");

            migrationBuilder.DropIndex(
                name: "IX_EventosDeEnvio_EnvioId_TenantId",
                table: "EventosDeEnvio");

            migrationBuilder.DropIndex(
                name: "IX_EventosDeEnvio_ProgramacionAnteriorId_TenantId",
                table: "EventosDeEnvio");

            migrationBuilder.DropIndex(
                name: "IX_EventosDeEnvio_ProgramacionNuevaId_TenantId",
                table: "EventosDeEnvio");

            migrationBuilder.DropIndex(
                name: "IX_EventosDeEnvio_ResponsableId_TenantId",
                table: "EventosDeEnvio");

            migrationBuilder.DropIndex(
                name: "IX_EventosDeEnvio_TenantId",
                table: "EventosDeEnvio");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EscaneosBulto_Id_TenantId",
                table: "EscaneosBulto");

            migrationBuilder.DropIndex(
                name: "IX_EscaneosBulto_BultoId_TenantId",
                table: "EscaneosBulto");

            migrationBuilder.DropIndex(
                name: "IX_EscaneosBulto_DevolucionId_TenantId",
                table: "EscaneosBulto");

            migrationBuilder.DropIndex(
                name: "IX_EscaneosBulto_ResponsableId_TenantId",
                table: "EscaneosBulto");

            migrationBuilder.DropIndex(
                name: "IX_EscaneosBulto_RutaId_TenantId",
                table: "EscaneosBulto");

            migrationBuilder.DropIndex(
                name: "IX_EscaneosBulto_TenantId",
                table: "EscaneosBulto");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Envios_Id_TenantId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_CuadroTarifarioId_TenantId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_CuentaComercialId_TenantId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_DestinatarioId_TenantId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_ParadaId_TenantId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_ProgramacionId_TenantId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_ReglasId_TenantId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_TenantId",
                table: "Envios");

            migrationBuilder.DropIndex(
                name: "IX_Envios_ZonaId_TenantId",
                table: "Envios");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_EntregasAviso_Id_TenantId",
                table: "EntregasAviso");

            migrationBuilder.DropIndex(
                name: "IX_EntregasAviso_EventoId_TenantId",
                table: "EntregasAviso");

            migrationBuilder.DropIndex(
                name: "IX_EntregasAviso_SuscripcionId_TenantId",
                table: "EntregasAviso");

            migrationBuilder.DropIndex(
                name: "IX_EntregasAviso_TenantId",
                table: "EntregasAviso");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Discrepancias_Id_TenantId",
                table: "Discrepancias");

            migrationBuilder.DropIndex(
                name: "IX_Discrepancias_BultoId_TenantId",
                table: "Discrepancias");

            migrationBuilder.DropIndex(
                name: "IX_Discrepancias_EnvioId_TenantId",
                table: "Discrepancias");

            migrationBuilder.DropIndex(
                name: "IX_Discrepancias_TenantId",
                table: "Discrepancias");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Direcciones_Id_TenantId",
                table: "Direcciones");

            migrationBuilder.DropIndex(
                name: "IX_Direcciones_TenantId",
                table: "Direcciones");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Devoluciones_Id_TenantId",
                table: "Devoluciones");

            migrationBuilder.DropIndex(
                name: "IX_Devoluciones_EnvioId_TenantId",
                table: "Devoluciones");

            migrationBuilder.DropIndex(
                name: "IX_Devoluciones_TenantId",
                table: "Devoluciones");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Destinatarios_Id_TenantId",
                table: "Destinatarios");

            migrationBuilder.DropIndex(
                name: "IX_Destinatarios_DireccionId_TenantId",
                table: "Destinatarios");

            migrationBuilder.DropIndex(
                name: "IX_Destinatarios_TenantId",
                table: "Destinatarios");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CuentasComerciales_Id_TenantId",
                table: "CuentasComerciales");

            migrationBuilder.DropIndex(
                name: "IX_CuentasComerciales_TenantId",
                table: "CuentasComerciales");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CuentasComerciales_OperadorTenant",
                table: "CuentasComerciales");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CuadrosTarifarios_Id_TenantId",
                table: "CuadrosTarifarios");

            migrationBuilder.DropIndex(
                name: "IX_CuadrosTarifarios_TenantId",
                table: "CuadrosTarifarios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CuadrosTarifarios_OperadorTenant",
                table: "CuadrosTarifarios");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Bultos_Id_TenantId",
                table: "Bultos");

            migrationBuilder.DropIndex(
                name: "IX_Bultos_EnvioId_TenantId",
                table: "Bultos");

            migrationBuilder.DropIndex(
                name: "IX_Bultos_TenantId",
                table: "Bultos");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AjustesTarifarios_Id_TenantId",
                table: "AjustesTarifarios");

            migrationBuilder.DropIndex(
                name: "IX_AjustesTarifarios_LineaId_TenantId",
                table: "AjustesTarifarios");

            migrationBuilder.DropIndex(
                name: "IX_AjustesTarifarios_TenantId",
                table: "AjustesTarifarios");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ZonasCobertura");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Vehiculos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UsuariosCuentasComerciales");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SuscripcionesAviso");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Rutas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Rendiciones");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ReglasOperativas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PruebasEntrega");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProgramacionesEntrega");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PoliticasDevolucion");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PlazosServicio");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Paradas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MotivosNoEntrega");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LineasTarifa");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "IntentosEntrega");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "FranjasHorarias");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EventosDeEnvio");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EscaneosBulto");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Envios");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EntregasAviso");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Discrepancias");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Direcciones");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Devoluciones");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Destinatarios");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CuentasComerciales");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CuadrosTarifarios");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Bultos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AjustesTarifarios");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosCuentasComerciales_CuentaComercialId",
                table: "UsuariosCuentasComerciales",
                column: "CuentaComercialId");

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesAviso_CuentaComercialId",
                table: "SuscripcionesAviso",
                column: "CuentaComercialId");

            migrationBuilder.CreateIndex(
                name: "IX_Rutas_RepartidorId",
                table: "Rutas",
                column: "RepartidorId");

            migrationBuilder.CreateIndex(
                name: "IX_Rutas_VehiculoId",
                table: "Rutas",
                column: "VehiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_Rendiciones_RutaId",
                table: "Rendiciones",
                column: "RutaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReglasOperativas_PoliticaDevolucionId",
                table: "ReglasOperativas",
                column: "PoliticaDevolucionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PruebasEntrega_IntentoId",
                table: "PruebasEntrega",
                column: "IntentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramacionesEntrega_FranjaId",
                table: "ProgramacionesEntrega",
                column: "FranjaId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_DestinatarioId",
                table: "Notificaciones",
                column: "DestinatarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MotivosNoEntrega_ReglasId",
                table: "MotivosNoEntrega",
                column: "ReglasId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasTarifa_CuadroId",
                table: "LineasTarifa",
                column: "CuadroId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasTarifa_ZonaId",
                table: "LineasTarifa",
                column: "ZonaId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosEntrega_MotivoNoEntregaId",
                table: "IntentosEntrega",
                column: "MotivoNoEntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosEntrega_ParadaId",
                table: "IntentosEntrega",
                column: "ParadaId");

            migrationBuilder.CreateIndex(
                name: "IX_FranjasHorarias_ZonaId",
                table: "FranjasHorarias",
                column: "ZonaId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosDeEnvio_ProgramacionAnteriorId",
                table: "EventosDeEnvio",
                column: "ProgramacionAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosDeEnvio_ProgramacionNuevaId",
                table: "EventosDeEnvio",
                column: "ProgramacionNuevaId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosDeEnvio_ResponsableId",
                table: "EventosDeEnvio",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_EscaneosBulto_BultoId",
                table: "EscaneosBulto",
                column: "BultoId");

            migrationBuilder.CreateIndex(
                name: "IX_EscaneosBulto_DevolucionId",
                table: "EscaneosBulto",
                column: "DevolucionId");

            migrationBuilder.CreateIndex(
                name: "IX_EscaneosBulto_ResponsableId",
                table: "EscaneosBulto",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_EscaneosBulto_RutaId",
                table: "EscaneosBulto",
                column: "RutaId");

            migrationBuilder.CreateIndex(
                name: "IX_Envios_CuadroTarifarioId",
                table: "Envios",
                column: "CuadroTarifarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Envios_CuentaComercialId",
                table: "Envios",
                column: "CuentaComercialId");

            migrationBuilder.CreateIndex(
                name: "IX_Envios_DestinatarioId",
                table: "Envios",
                column: "DestinatarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Envios_ParadaId",
                table: "Envios",
                column: "ParadaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Envios_ProgramacionId",
                table: "Envios",
                column: "ProgramacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Envios_ReglasId",
                table: "Envios",
                column: "ReglasId");

            migrationBuilder.CreateIndex(
                name: "IX_Envios_ZonaId",
                table: "Envios",
                column: "ZonaId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregasAviso_EventoId",
                table: "EntregasAviso",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancias_BultoId",
                table: "Discrepancias",
                column: "BultoId");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancias_EnvioId",
                table: "Discrepancias",
                column: "EnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_Devoluciones_EnvioId",
                table: "Devoluciones",
                column: "EnvioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Destinatarios_DireccionId",
                table: "Destinatarios",
                column: "DireccionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AjustesTarifarios_LineaId",
                table: "AjustesTarifarios",
                column: "LineaId");

            migrationBuilder.AddForeignKey(
                name: "FK_AjustesTarifarios_LineasTarifa_LineaId",
                table: "AjustesTarifarios",
                column: "LineaId",
                principalTable: "LineasTarifa",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bultos_Envios_EnvioId",
                table: "Bultos",
                column: "EnvioId",
                principalTable: "Envios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Destinatarios_Direcciones_DireccionId",
                table: "Destinatarios",
                column: "DireccionId",
                principalTable: "Direcciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Devoluciones_Envios_EnvioId",
                table: "Devoluciones",
                column: "EnvioId",
                principalTable: "Envios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Discrepancias_Bultos_BultoId",
                table: "Discrepancias",
                column: "BultoId",
                principalTable: "Bultos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Discrepancias_Envios_EnvioId",
                table: "Discrepancias",
                column: "EnvioId",
                principalTable: "Envios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EntregasAviso_EventosDeEnvio_EventoId",
                table: "EntregasAviso",
                column: "EventoId",
                principalTable: "EventosDeEnvio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EntregasAviso_SuscripcionesAviso_SuscripcionId",
                table: "EntregasAviso",
                column: "SuscripcionId",
                principalTable: "SuscripcionesAviso",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_CuadrosTarifarios_CuadroTarifarioId",
                table: "Envios",
                column: "CuadroTarifarioId",
                principalTable: "CuadrosTarifarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_CuentasComerciales_CuentaComercialId",
                table: "Envios",
                column: "CuentaComercialId",
                principalTable: "CuentasComerciales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_Destinatarios_DestinatarioId",
                table: "Envios",
                column: "DestinatarioId",
                principalTable: "Destinatarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_Paradas_ParadaId",
                table: "Envios",
                column: "ParadaId",
                principalTable: "Paradas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_ProgramacionesEntrega_ProgramacionId",
                table: "Envios",
                column: "ProgramacionId",
                principalTable: "ProgramacionesEntrega",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_ReglasOperativas_ReglasId",
                table: "Envios",
                column: "ReglasId",
                principalTable: "ReglasOperativas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Envios_ZonasCobertura_ZonaId",
                table: "Envios",
                column: "ZonaId",
                principalTable: "ZonasCobertura",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscaneosBulto_Bultos_BultoId",
                table: "EscaneosBulto",
                column: "BultoId",
                principalTable: "Bultos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscaneosBulto_Devoluciones_DevolucionId",
                table: "EscaneosBulto",
                column: "DevolucionId",
                principalTable: "Devoluciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscaneosBulto_Rutas_RutaId",
                table: "EscaneosBulto",
                column: "RutaId",
                principalTable: "Rutas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EscaneosBulto_Usuarios_ResponsableId",
                table: "EscaneosBulto",
                column: "ResponsableId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventosDeEnvio_Envios_EnvioId",
                table: "EventosDeEnvio",
                column: "EnvioId",
                principalTable: "Envios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionAnteriorId",
                table: "EventosDeEnvio",
                column: "ProgramacionAnteriorId",
                principalTable: "ProgramacionesEntrega",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionNuevaId",
                table: "EventosDeEnvio",
                column: "ProgramacionNuevaId",
                principalTable: "ProgramacionesEntrega",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EventosDeEnvio_Usuarios_ResponsableId",
                table: "EventosDeEnvio",
                column: "ResponsableId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FranjasHorarias_ZonasCobertura_ZonaId",
                table: "FranjasHorarias",
                column: "ZonaId",
                principalTable: "ZonasCobertura",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntentosEntrega_Envios_EnvioId",
                table: "IntentosEntrega",
                column: "EnvioId",
                principalTable: "Envios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntentosEntrega_MotivosNoEntrega_MotivoNoEntregaId",
                table: "IntentosEntrega",
                column: "MotivoNoEntregaId",
                principalTable: "MotivosNoEntrega",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntentosEntrega_Paradas_ParadaId",
                table: "IntentosEntrega",
                column: "ParadaId",
                principalTable: "Paradas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LineasTarifa_CuadrosTarifarios_CuadroId",
                table: "LineasTarifa",
                column: "CuadroId",
                principalTable: "CuadrosTarifarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LineasTarifa_ZonasCobertura_ZonaId",
                table: "LineasTarifa",
                column: "ZonaId",
                principalTable: "ZonasCobertura",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MotivosNoEntrega_ReglasOperativas_ReglasId",
                table: "MotivosNoEntrega",
                column: "ReglasId",
                principalTable: "ReglasOperativas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notificaciones_Destinatarios_DestinatarioId",
                table: "Notificaciones",
                column: "DestinatarioId",
                principalTable: "Destinatarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Paradas_Envios_EnvioId",
                table: "Paradas",
                column: "EnvioId",
                principalTable: "Envios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Paradas_Rutas_RutaId",
                table: "Paradas",
                column: "RutaId",
                principalTable: "Rutas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlazosServicio_ReglasOperativas_ReglasId",
                table: "PlazosServicio",
                column: "ReglasId",
                principalTable: "ReglasOperativas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramacionesEntrega_FranjasHorarias_FranjaId",
                table: "ProgramacionesEntrega",
                column: "FranjaId",
                principalTable: "FranjasHorarias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PruebasEntrega_IntentosEntrega_IntentoId",
                table: "PruebasEntrega",
                column: "IntentoId",
                principalTable: "IntentosEntrega",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReglasOperativas_PoliticasDevolucion_PoliticaDevolucionId",
                table: "ReglasOperativas",
                column: "PoliticaDevolucionId",
                principalTable: "PoliticasDevolucion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rendiciones_Rutas_RutaId",
                table: "Rendiciones",
                column: "RutaId",
                principalTable: "Rutas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rutas_Usuarios_RepartidorId",
                table: "Rutas",
                column: "RepartidorId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rutas_Vehiculos_VehiculoId",
                table: "Rutas",
                column: "VehiculoId",
                principalTable: "Vehiculos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SuscripcionesAviso_CuentasComerciales_CuentaComercialId",
                table: "SuscripcionesAviso",
                column: "CuentaComercialId",
                principalTable: "CuentasComerciales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UsuariosCuentasComerciales_CuentasComerciales_CuentaComerci~",
                table: "UsuariosCuentasComerciales",
                column: "CuentaComercialId",
                principalTable: "CuentasComerciales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UsuariosCuentasComerciales_Usuarios_UsuarioId",
                table: "UsuariosCuentasComerciales",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
