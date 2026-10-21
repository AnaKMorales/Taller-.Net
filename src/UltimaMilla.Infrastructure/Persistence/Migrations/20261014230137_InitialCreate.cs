using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimaMilla.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Comercios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RazonSocial = table.Column<string>(type: "text", nullable: false),
                    Telefono = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comercios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Direcciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Calle = table.Column<string>(type: "text", nullable: false),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    Localidad = table.Column<string>(type: "text", nullable: false),
                    CodigoPostal = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Direcciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Operadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RazonSocial = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operadores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PoliticasDevolucion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiasEnDeposito = table.Column<int>(type: "integer", nullable: false),
                    PlazoMaximoDevolucionDias = table.Column<int>(type: "integer", nullable: false),
                    DevolverAlAgotarIntentos = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoliticasDevolucion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Destinatarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Telefono = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    DireccionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Destinatarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Destinatarios_Direcciones_DireccionId",
                        column: x => x.DireccionId,
                        principalTable: "Direcciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CuadrosTarifarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    VigenteDesde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnUso = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuadrosTarifarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CuadrosTarifarios_Operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CuentasComerciales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComercioId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentasComerciales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CuentasComerciales_Comercios_ComercioId",
                        column: x => x.ComercioId,
                        principalTable: "Comercios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuentasComerciales_Operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Rol = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    EmailNormalizado = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(\"Email\")", stored: true),
                    TipoUsuario = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vehiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Matricula = table.Column<string>(type: "text", nullable: false),
                    CapacidadPesoKg = table.Column<decimal>(type: "numeric", nullable: false),
                    CapacidadVolumenM3 = table.Column<decimal>(type: "numeric", nullable: false),
                    MatriculaNormalizada = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(\"Matricula\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehiculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehiculos_Operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ZonasCobertura",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    CodigoPostal = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZonasCobertura", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZonasCobertura_Operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReglasOperativas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    VigenteDesde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaxIntentosEntrega = table.Column<int>(type: "integer", nullable: false),
                    PlazoEntreIntentosHoras = table.Column<int>(type: "integer", nullable: false),
                    TiposPrueba = table.Column<int[]>(type: "integer[]", nullable: false),
                    PoliticaDevolucionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnUso = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglasOperativas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReglasOperativas_Operadores_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "Operadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReglasOperativas_PoliticasDevolucion_PoliticaDevolucionId",
                        column: x => x.PoliticaDevolucionId,
                        principalTable: "PoliticasDevolucion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notificaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinatarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Canal = table.Column<int>(type: "integer", nullable: false),
                    Mensaje = table.Column<string>(type: "text", nullable: false),
                    EnviadaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificaciones_Destinatarios_DestinatarioId",
                        column: x => x.DestinatarioId,
                        principalTable: "Destinatarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SuscripcionesAviso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaComercialId = table.Column<Guid>(type: "uuid", nullable: false),
                    TiposEvento = table.Column<int[]>(type: "integer[]", nullable: false),
                    SecretoFirma = table.Column<string>(type: "text", nullable: false),
                    UrlDestino = table.Column<string>(type: "text", nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuscripcionesAviso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuscripcionesAviso_CuentasComerciales_CuentaComercialId",
                        column: x => x.CuentaComercialId,
                        principalTable: "CuentasComerciales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosCuentasComerciales",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaComercialId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosCuentasComerciales", x => new { x.UsuarioId, x.CuentaComercialId });
                    table.ForeignKey(
                        name: "FK_UsuariosCuentasComerciales_CuentasComerciales_CuentaComerci~",
                        column: x => x.CuentaComercialId,
                        principalTable: "CuentasComerciales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuariosCuentasComerciales_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rutas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepartidorId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehiculoId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxParadas = table.Column<int>(type: "integer", nullable: false),
                    DespachadaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rutas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rutas_Usuarios_RepartidorId",
                        column: x => x.RepartidorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rutas_Vehiculos_VehiculoId",
                        column: x => x.VehiculoId,
                        principalTable: "Vehiculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FranjasHorarias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Desde = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Hasta = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ZonaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FranjasHorarias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FranjasHorarias_ZonasCobertura_ZonaId",
                        column: x => x.ZonaId,
                        principalTable: "ZonasCobertura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LineasTarifa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuadroId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZonaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModalidadServicio = table.Column<int>(type: "integer", nullable: false),
                    PesoDesde = table.Column<decimal>(type: "numeric", nullable: false),
                    PesoHasta = table.Column<decimal>(type: "numeric", nullable: false),
                    VolumenHasta = table.Column<decimal>(type: "numeric", nullable: false),
                    Precio = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasTarifa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasTarifa_CuadrosTarifarios_CuadroId",
                        column: x => x.CuadroId,
                        principalTable: "CuadrosTarifarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LineasTarifa_ZonasCobertura_ZonaId",
                        column: x => x.ZonaId,
                        principalTable: "ZonasCobertura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MotivosNoEntrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReglasId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Responsable = table.Column<int>(type: "integer", nullable: false),
                    RequiereEvidencia = table.Column<bool>(type: "boolean", nullable: false),
                    EsReintentable = table.Column<bool>(type: "boolean", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivosNoEntrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MotivosNoEntrega_ReglasOperativas_ReglasId",
                        column: x => x.ReglasId,
                        principalTable: "ReglasOperativas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlazosServicio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReglasId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModalidadServicio = table.Column<int>(type: "integer", nullable: false),
                    PlazoComprometidoHoras = table.Column<int>(type: "integer", nullable: false),
                    HoraCorte = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlazosServicio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlazosServicio_ReglasOperativas_ReglasId",
                        column: x => x.ReglasId,
                        principalTable: "ReglasOperativas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rendiciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RutaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rendiciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rendiciones_Rutas_RutaId",
                        column: x => x.RutaId,
                        principalTable: "Rutas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramacionesEntrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FranjaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaEntrega = table.Column<DateOnly>(type: "date", nullable: false),
                    InicioLocal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FinLocal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramacionesEntrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramacionesEntrega_FranjasHorarias_FranjaId",
                        column: x => x.FranjaId,
                        principalTable: "FranjasHorarias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AjustesTarifarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    ValorPorcentaje = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AjustesTarifarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AjustesTarifarios_LineasTarifa_LineaId",
                        column: x => x.LineaId,
                        principalTable: "LineasTarifa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bultos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Peso = table.Column<decimal>(type: "numeric", nullable: false),
                    Alto = table.Column<decimal>(type: "numeric", nullable: false),
                    Ancho = table.Column<decimal>(type: "numeric", nullable: false),
                    Largo = table.Column<decimal>(type: "numeric", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoNormalizado = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(\"Codigo\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bultos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devoluciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    PlazoLimite = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devoluciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscaneosBulto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RutaId = table.Column<Guid>(type: "uuid", nullable: true),
                    DevolucionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BultoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResponsableId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscaneosBulto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscaneosBulto_Bultos_BultoId",
                        column: x => x.BultoId,
                        principalTable: "Bultos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscaneosBulto_Devoluciones_DevolucionId",
                        column: x => x.DevolucionId,
                        principalTable: "Devoluciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscaneosBulto_Rutas_RutaId",
                        column: x => x.RutaId,
                        principalTable: "Rutas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscaneosBulto_Usuarios_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Discrepancias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    BultoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Detalle = table.Column<string>(type: "text", nullable: false),
                    DetalleDeclarado = table.Column<string>(type: "text", nullable: false),
                    DetalleRecibido = table.Column<string>(type: "text", nullable: false),
                    FechaHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discrepancias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Discrepancias_Bultos_BultoId",
                        column: x => x.BultoId,
                        principalTable: "Bultos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EntregasAviso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SuscripcionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Exitoso = table.Column<bool>(type: "boolean", nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    UltimoIntentoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntregasAviso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntregasAviso_SuscripcionesAviso_SuscripcionId",
                        column: x => x.SuscripcionId,
                        principalTable: "SuscripcionesAviso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Envios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaComercialId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinatarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZonaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReglasId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuadroTarifarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodigoSeguimiento = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    ModalidadServicio = table.Column<int>(type: "integer", nullable: false),
                    FechaAdmision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaComprometida = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProgramacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TarifaCalculada = table.Column<decimal>(type: "numeric", nullable: false),
                    ParadaId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Envios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Envios_CuadrosTarifarios_CuadroTarifarioId",
                        column: x => x.CuadroTarifarioId,
                        principalTable: "CuadrosTarifarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Envios_CuentasComerciales_CuentaComercialId",
                        column: x => x.CuentaComercialId,
                        principalTable: "CuentasComerciales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Envios_Destinatarios_DestinatarioId",
                        column: x => x.DestinatarioId,
                        principalTable: "Destinatarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Envios_ProgramacionesEntrega_ProgramacionId",
                        column: x => x.ProgramacionId,
                        principalTable: "ProgramacionesEntrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Envios_ReglasOperativas_ReglasId",
                        column: x => x.ReglasId,
                        principalTable: "ReglasOperativas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Envios_ZonasCobertura_ZonaId",
                        column: x => x.ZonaId,
                        principalTable: "ZonasCobertura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventosDeEnvio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Origen = table.Column<int>(type: "integer", nullable: false),
                    EstadoAnterior = table.Column<int>(type: "integer", nullable: true),
                    EstadoNuevo = table.Column<int>(type: "integer", nullable: false),
                    ResponsableId = table.Column<Guid>(type: "uuid", nullable: false),
                    OcurridoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Posicion = table.Column<string>(type: "jsonb", nullable: true),
                    Detalle = table.Column<string>(type: "text", nullable: true),
                    IdOperacion = table.Column<Guid>(type: "uuid", nullable: true),
                    ProgramacionAnteriorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProgramacionNuevaId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosDeEnvio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosDeEnvio_Envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "Envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionAnteriorId",
                        column: x => x.ProgramacionAnteriorId,
                        principalTable: "ProgramacionesEntrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionNuevaId",
                        column: x => x.ProgramacionNuevaId,
                        principalTable: "ProgramacionesEntrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosDeEnvio_Usuarios_ResponsableId",
                        column: x => x.ResponsableId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Paradas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RutaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Cod = table.Column<int>(type: "integer", nullable: false),
                    HoraEstimadaLlegada = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paradas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Paradas_Envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "Envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Paradas_Rutas_RutaId",
                        column: x => x.RutaId,
                        principalTable: "Rutas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntentosEntrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParadaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Resultado = table.Column<int>(type: "integer", nullable: false),
                    MotivoNoEntregaId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdOperacion = table.Column<Guid>(type: "uuid", nullable: false),
                    OcurridoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentosEntrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntentosEntrega_Envios_EnvioId",
                        column: x => x.EnvioId,
                        principalTable: "Envios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntentosEntrega_MotivosNoEntrega_MotivoNoEntregaId",
                        column: x => x.MotivoNoEntregaId,
                        principalTable: "MotivosNoEntrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntentosEntrega_Paradas_ParadaId",
                        column: x => x.ParadaId,
                        principalTable: "Paradas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PruebasEntrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoPrueba = table.Column<int>(type: "integer", nullable: false),
                    Posicion = table.Column<string>(type: "jsonb", nullable: true),
                    Hora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchivoRef = table.Column<string>(type: "text", nullable: true),
                    ValorTexto = table.Column<string>(type: "text", nullable: true),
                    IntentoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PruebasEntrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PruebasEntrega_IntentosEntrega_IntentoId",
                        column: x => x.IntentoId,
                        principalTable: "IntentosEntrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AjustesTarifarios_LineaId",
                table: "AjustesTarifarios",
                column: "LineaId");

            migrationBuilder.CreateIndex(
                name: "IX_Bultos_EnvioId_CodigoNormalizado",
                table: "Bultos",
                columns: new[] { "EnvioId", "CodigoNormalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CuadrosTarifarios_OperadorId_Version",
                table: "CuadrosTarifarios",
                columns: new[] { "OperadorId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CuentasComerciales_ComercioId",
                table: "CuentasComerciales",
                column: "ComercioId");

            migrationBuilder.CreateIndex(
                name: "IX_CuentasComerciales_OperadorId_ComercioId",
                table: "CuentasComerciales",
                columns: new[] { "OperadorId", "ComercioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Destinatarios_DireccionId",
                table: "Destinatarios",
                column: "DireccionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devoluciones_EnvioId",
                table: "Devoluciones",
                column: "EnvioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancias_BultoId",
                table: "Discrepancias",
                column: "BultoId");

            migrationBuilder.CreateIndex(
                name: "IX_Discrepancias_EnvioId",
                table: "Discrepancias",
                column: "EnvioId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregasAviso_EventoId",
                table: "EntregasAviso",
                column: "EventoId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregasAviso_SuscripcionId_EventoId",
                table: "EntregasAviso",
                columns: new[] { "SuscripcionId", "EventoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Envios_CodigoSeguimiento",
                table: "Envios",
                column: "CodigoSeguimiento",
                unique: true);

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
                name: "IX_EventosDeEnvio_EnvioId_IdOperacion",
                table: "EventosDeEnvio",
                columns: new[] { "EnvioId", "IdOperacion" },
                unique: true,
                filter: "\"IdOperacion\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EventosDeEnvio_EnvioId_OcurridoEn",
                table: "EventosDeEnvio",
                columns: new[] { "EnvioId", "OcurridoEn" });

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
                name: "IX_FranjasHorarias_ZonaId",
                table: "FranjasHorarias",
                column: "ZonaId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosEntrega_EnvioId_IdOperacion",
                table: "IntentosEntrega",
                columns: new[] { "EnvioId", "IdOperacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntentosEntrega_EnvioId_Numero",
                table: "IntentosEntrega",
                columns: new[] { "EnvioId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntentosEntrega_MotivoNoEntregaId",
                table: "IntentosEntrega",
                column: "MotivoNoEntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosEntrega_ParadaId",
                table: "IntentosEntrega",
                column: "ParadaId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasTarifa_CuadroId",
                table: "LineasTarifa",
                column: "CuadroId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasTarifa_ZonaId",
                table: "LineasTarifa",
                column: "ZonaId");

            migrationBuilder.CreateIndex(
                name: "IX_MotivosNoEntrega_ReglasId",
                table: "MotivosNoEntrega",
                column: "ReglasId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_DestinatarioId",
                table: "Notificaciones",
                column: "DestinatarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Paradas_EnvioId",
                table: "Paradas",
                column: "EnvioId",
                unique: true,
                filter: "\"Activa\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Paradas_RutaId_Cod",
                table: "Paradas",
                columns: new[] { "RutaId", "Cod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlazosServicio_ReglasId_ModalidadServicio",
                table: "PlazosServicio",
                columns: new[] { "ReglasId", "ModalidadServicio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramacionesEntrega_FranjaId",
                table: "ProgramacionesEntrega",
                column: "FranjaId");

            migrationBuilder.CreateIndex(
                name: "IX_PruebasEntrega_IntentoId",
                table: "PruebasEntrega",
                column: "IntentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReglasOperativas_OperadorId_Version",
                table: "ReglasOperativas",
                columns: new[] { "OperadorId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReglasOperativas_PoliticaDevolucionId",
                table: "ReglasOperativas",
                column: "PoliticaDevolucionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rendiciones_RutaId",
                table: "Rendiciones",
                column: "RutaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rutas_RepartidorId",
                table: "Rutas",
                column: "RepartidorId");

            migrationBuilder.CreateIndex(
                name: "IX_Rutas_VehiculoId",
                table: "Rutas",
                column: "VehiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_SuscripcionesAviso_CuentaComercialId",
                table: "SuscripcionesAviso",
                column: "CuentaComercialId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_OperadorId_EmailNormalizado",
                table: "Usuarios",
                columns: new[] { "OperadorId", "EmailNormalizado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosCuentasComerciales_CuentaComercialId",
                table: "UsuariosCuentasComerciales",
                column: "CuentaComercialId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehiculos_OperadorId_MatriculaNormalizada",
                table: "Vehiculos",
                columns: new[] { "OperadorId", "MatriculaNormalizada" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZonasCobertura_OperadorId",
                table: "ZonasCobertura",
                column: "OperadorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bultos_Envios_EnvioId",
                table: "Bultos",
                column: "EnvioId",
                principalTable: "Envios",
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
                name: "FK_Envios_Paradas_ParadaId",
                table: "Envios",
                column: "ParadaId",
                principalTable: "Paradas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Paradas_Envios_EnvioId",
                table: "Paradas");

            migrationBuilder.DropTable(
                name: "AjustesTarifarios");

            migrationBuilder.DropTable(
                name: "Discrepancias");

            migrationBuilder.DropTable(
                name: "EntregasAviso");

            migrationBuilder.DropTable(
                name: "EscaneosBulto");

            migrationBuilder.DropTable(
                name: "Notificaciones");

            migrationBuilder.DropTable(
                name: "PlazosServicio");

            migrationBuilder.DropTable(
                name: "PruebasEntrega");

            migrationBuilder.DropTable(
                name: "Rendiciones");

            migrationBuilder.DropTable(
                name: "UsuariosCuentasComerciales");

            migrationBuilder.DropTable(
                name: "LineasTarifa");

            migrationBuilder.DropTable(
                name: "EventosDeEnvio");

            migrationBuilder.DropTable(
                name: "SuscripcionesAviso");

            migrationBuilder.DropTable(
                name: "Bultos");

            migrationBuilder.DropTable(
                name: "Devoluciones");

            migrationBuilder.DropTable(
                name: "IntentosEntrega");

            migrationBuilder.DropTable(
                name: "MotivosNoEntrega");

            migrationBuilder.DropTable(
                name: "Envios");

            migrationBuilder.DropTable(
                name: "CuadrosTarifarios");

            migrationBuilder.DropTable(
                name: "CuentasComerciales");

            migrationBuilder.DropTable(
                name: "Destinatarios");

            migrationBuilder.DropTable(
                name: "Paradas");

            migrationBuilder.DropTable(
                name: "ProgramacionesEntrega");

            migrationBuilder.DropTable(
                name: "ReglasOperativas");

            migrationBuilder.DropTable(
                name: "Comercios");

            migrationBuilder.DropTable(
                name: "Direcciones");

            migrationBuilder.DropTable(
                name: "Rutas");

            migrationBuilder.DropTable(
                name: "FranjasHorarias");

            migrationBuilder.DropTable(
                name: "PoliticasDevolucion");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Vehiculos");

            migrationBuilder.DropTable(
                name: "ZonasCobertura");

            migrationBuilder.DropTable(
                name: "Operadores");
        }
    }
}
