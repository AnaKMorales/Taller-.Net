CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "Comercios" (
    "Id" uuid NOT NULL,
    "RazonSocial" text NOT NULL,
    "Telefono" text NOT NULL,
    "Email" text NOT NULL,
    CONSTRAINT "PK_Comercios" PRIMARY KEY ("Id")
);

CREATE TABLE "Direcciones" (
    "Id" uuid NOT NULL,
    "Calle" text NOT NULL,
    "Numero" text NOT NULL,
    "Localidad" text NOT NULL,
    "CodigoPostal" text NOT NULL,
    CONSTRAINT "PK_Direcciones" PRIMARY KEY ("Id")
);

CREATE TABLE "Operadores" (
    "Id" uuid NOT NULL,
    "RazonSocial" text NOT NULL,
    CONSTRAINT "PK_Operadores" PRIMARY KEY ("Id")
);

CREATE TABLE "PoliticasDevolucion" (
    "Id" uuid NOT NULL,
    "DiasEnDeposito" integer NOT NULL,
    "PlazoMaximoDevolucionDias" integer NOT NULL,
    "DevolverAlAgotarIntentos" boolean NOT NULL,
    CONSTRAINT "PK_PoliticasDevolucion" PRIMARY KEY ("Id")
);

CREATE TABLE "Destinatarios" (
    "Id" uuid NOT NULL,
    "Nombre" text NOT NULL,
    "Telefono" text NOT NULL,
    "Email" text NOT NULL,
    "DireccionId" uuid NOT NULL,
    CONSTRAINT "PK_Destinatarios" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Destinatarios_Direcciones_DireccionId" FOREIGN KEY ("DireccionId") REFERENCES "Direcciones" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CuadrosTarifarios" (
    "Id" uuid NOT NULL,
    "OperadorId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "VigenteDesde" timestamp with time zone NOT NULL,
    "EnUso" boolean NOT NULL,
    CONSTRAINT "PK_CuadrosTarifarios" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CuadrosTarifarios_Operadores_OperadorId" FOREIGN KEY ("OperadorId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "CuentasComerciales" (
    "Id" uuid NOT NULL,
    "OperadorId" uuid NOT NULL,
    "ComercioId" uuid NOT NULL,
    CONSTRAINT "PK_CuentasComerciales" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CuentasComerciales_Comercios_ComercioId" FOREIGN KEY ("ComercioId") REFERENCES "Comercios" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_CuentasComerciales_Operadores_OperadorId" FOREIGN KEY ("OperadorId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Usuarios" (
    "Id" uuid NOT NULL,
    "OperadorId" uuid NOT NULL,
    "Email" text NOT NULL,
    "Rol" integer NOT NULL,
    "Activo" boolean NOT NULL,
    "EmailNormalizado" text GENERATED ALWAYS AS (lower("Email")) STORED,
    "TipoUsuario" character varying(13) NOT NULL,
    CONSTRAINT "PK_Usuarios" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Usuarios_Operadores_OperadorId" FOREIGN KEY ("OperadorId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Vehiculos" (
    "Id" uuid NOT NULL,
    "OperadorId" uuid NOT NULL,
    "Matricula" text NOT NULL,
    "CapacidadPesoKg" numeric NOT NULL,
    "CapacidadVolumenM3" numeric NOT NULL,
    "MatriculaNormalizada" text GENERATED ALWAYS AS (lower("Matricula")) STORED,
    CONSTRAINT "PK_Vehiculos" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Vehiculos_Operadores_OperadorId" FOREIGN KEY ("OperadorId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ZonasCobertura" (
    "Id" uuid NOT NULL,
    "OperadorId" uuid NOT NULL,
    "Nombre" text NOT NULL,
    "CodigoPostal" text NOT NULL,
    CONSTRAINT "PK_ZonasCobertura" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ZonasCobertura_Operadores_OperadorId" FOREIGN KEY ("OperadorId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ReglasOperativas" (
    "Id" uuid NOT NULL,
    "OperadorId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "VigenteDesde" timestamp with time zone NOT NULL,
    "MaxIntentosEntrega" integer NOT NULL,
    "PlazoEntreIntentosHoras" integer NOT NULL,
    "TiposPrueba" integer[] NOT NULL,
    "PoliticaDevolucionId" uuid NOT NULL,
    "EnUso" boolean NOT NULL,
    CONSTRAINT "PK_ReglasOperativas" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ReglasOperativas_Operadores_OperadorId" FOREIGN KEY ("OperadorId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ReglasOperativas_PoliticasDevolucion_PoliticaDevolucionId" FOREIGN KEY ("PoliticaDevolucionId") REFERENCES "PoliticasDevolucion" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Notificaciones" (
    "Id" uuid NOT NULL,
    "DestinatarioId" uuid NOT NULL,
    "Canal" integer NOT NULL,
    "Mensaje" text NOT NULL,
    "EnviadaEn" timestamp with time zone,
    CONSTRAINT "PK_Notificaciones" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Notificaciones_Destinatarios_DestinatarioId" FOREIGN KEY ("DestinatarioId") REFERENCES "Destinatarios" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "SuscripcionesAviso" (
    "Id" uuid NOT NULL,
    "CuentaComercialId" uuid NOT NULL,
    "TiposEvento" integer[] NOT NULL,
    "SecretoFirma" text NOT NULL,
    "UrlDestino" text NOT NULL,
    "Activa" boolean NOT NULL,
    CONSTRAINT "PK_SuscripcionesAviso" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SuscripcionesAviso_CuentasComerciales_CuentaComercialId" FOREIGN KEY ("CuentaComercialId") REFERENCES "CuentasComerciales" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "UsuariosCuentasComerciales" (
    "UsuarioId" uuid NOT NULL,
    "CuentaComercialId" uuid NOT NULL,
    CONSTRAINT "PK_UsuariosCuentasComerciales" PRIMARY KEY ("UsuarioId", "CuentaComercialId"),
    CONSTRAINT "FK_UsuariosCuentasComerciales_CuentasComerciales_CuentaComerci~" FOREIGN KEY ("CuentaComercialId") REFERENCES "CuentasComerciales" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_UsuariosCuentasComerciales_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Rutas" (
    "Id" uuid NOT NULL,
    "RepartidorId" uuid NOT NULL,
    "VehiculoId" uuid NOT NULL,
    "MaxParadas" integer NOT NULL,
    "DespachadaEn" timestamp with time zone,
    CONSTRAINT "PK_Rutas" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Rutas_Usuarios_RepartidorId" FOREIGN KEY ("RepartidorId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Rutas_Vehiculos_VehiculoId" FOREIGN KEY ("VehiculoId") REFERENCES "Vehiculos" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "FranjasHorarias" (
    "Id" uuid NOT NULL,
    "Nombre" text NOT NULL,
    "Desde" time without time zone NOT NULL,
    "Hasta" time without time zone NOT NULL,
    "ZonaId" uuid NOT NULL,
    CONSTRAINT "PK_FranjasHorarias" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_FranjasHorarias_ZonasCobertura_ZonaId" FOREIGN KEY ("ZonaId") REFERENCES "ZonasCobertura" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "LineasTarifa" (
    "Id" uuid NOT NULL,
    "CuadroId" uuid NOT NULL,
    "ZonaId" uuid NOT NULL,
    "ModalidadServicio" integer NOT NULL,
    "PesoDesde" numeric NOT NULL,
    "PesoHasta" numeric NOT NULL,
    "VolumenHasta" numeric NOT NULL,
    "Precio" numeric NOT NULL,
    CONSTRAINT "PK_LineasTarifa" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_LineasTarifa_CuadrosTarifarios_CuadroId" FOREIGN KEY ("CuadroId") REFERENCES "CuadrosTarifarios" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_LineasTarifa_ZonasCobertura_ZonaId" FOREIGN KEY ("ZonaId") REFERENCES "ZonasCobertura" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "MotivosNoEntrega" (
    "Id" uuid NOT NULL,
    "ReglasId" uuid NOT NULL,
    "Nombre" text NOT NULL,
    "Descripcion" text NOT NULL,
    "Responsable" integer NOT NULL,
    "RequiereEvidencia" boolean NOT NULL,
    "EsReintentable" boolean NOT NULL,
    "Activo" boolean NOT NULL,
    CONSTRAINT "PK_MotivosNoEntrega" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MotivosNoEntrega_ReglasOperativas_ReglasId" FOREIGN KEY ("ReglasId") REFERENCES "ReglasOperativas" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "PlazosServicio" (
    "Id" uuid NOT NULL,
    "ReglasId" uuid NOT NULL,
    "ModalidadServicio" integer NOT NULL,
    "PlazoComprometidoHoras" integer NOT NULL,
    "HoraCorte" time without time zone NOT NULL,
    CONSTRAINT "PK_PlazosServicio" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PlazosServicio_ReglasOperativas_ReglasId" FOREIGN KEY ("ReglasId") REFERENCES "ReglasOperativas" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Rendiciones" (
    "Id" uuid NOT NULL,
    "RutaId" uuid NOT NULL,
    "FechaHora" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Rendiciones" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Rendiciones_Rutas_RutaId" FOREIGN KEY ("RutaId") REFERENCES "Rutas" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "ProgramacionesEntrega" (
    "Id" uuid NOT NULL,
    "FranjaId" uuid NOT NULL,
    "FechaEntrega" date NOT NULL,
    "InicioLocal" character varying(40) NOT NULL,
    "FinLocal" character varying(40) NOT NULL,
    CONSTRAINT "PK_ProgramacionesEntrega" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProgramacionesEntrega_FranjasHorarias_FranjaId" FOREIGN KEY ("FranjaId") REFERENCES "FranjasHorarias" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "AjustesTarifarios" (
    "Id" uuid NOT NULL,
    "LineaId" uuid NOT NULL,
    "Tipo" integer NOT NULL,
    "ValorPorcentaje" numeric NOT NULL,
    CONSTRAINT "PK_AjustesTarifarios" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AjustesTarifarios_LineasTarifa_LineaId" FOREIGN KEY ("LineaId") REFERENCES "LineasTarifa" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Bultos" (
    "Id" uuid NOT NULL,
    "Codigo" text NOT NULL,
    "Peso" numeric NOT NULL,
    "Alto" numeric NOT NULL,
    "Ancho" numeric NOT NULL,
    "Largo" numeric NOT NULL,
    "EnvioId" uuid NOT NULL,
    "CodigoNormalizado" text GENERATED ALWAYS AS (lower("Codigo")) STORED,
    CONSTRAINT "PK_Bultos" PRIMARY KEY ("Id")
);

CREATE TABLE "Devoluciones" (
    "Id" uuid NOT NULL,
    "EnvioId" uuid NOT NULL,
    "FechaInicio" timestamp with time zone NOT NULL,
    "FechaCierre" timestamp with time zone,
    "Estado" integer NOT NULL,
    "PlazoLimite" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Devoluciones" PRIMARY KEY ("Id")
);

CREATE TABLE "EscaneosBulto" (
    "Id" uuid NOT NULL,
    "RutaId" uuid,
    "DevolucionId" uuid,
    "BultoId" uuid NOT NULL,
    "Tipo" integer NOT NULL,
    "FechaHora" timestamp with time zone NOT NULL,
    "ResponsableId" uuid NOT NULL,
    CONSTRAINT "PK_EscaneosBulto" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EscaneosBulto_Bultos_BultoId" FOREIGN KEY ("BultoId") REFERENCES "Bultos" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EscaneosBulto_Devoluciones_DevolucionId" FOREIGN KEY ("DevolucionId") REFERENCES "Devoluciones" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EscaneosBulto_Rutas_RutaId" FOREIGN KEY ("RutaId") REFERENCES "Rutas" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EscaneosBulto_Usuarios_ResponsableId" FOREIGN KEY ("ResponsableId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Discrepancias" (
    "Id" uuid NOT NULL,
    "EnvioId" uuid NOT NULL,
    "BultoId" uuid,
    "Tipo" integer NOT NULL,
    "Detalle" text NOT NULL,
    "DetalleDeclarado" text NOT NULL,
    "DetalleRecibido" text NOT NULL,
    "FechaHora" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Discrepancias" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Discrepancias_Bultos_BultoId" FOREIGN KEY ("BultoId") REFERENCES "Bultos" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "EntregasAviso" (
    "Id" uuid NOT NULL,
    "SuscripcionId" uuid NOT NULL,
    "EventoId" uuid NOT NULL,
    "Exitoso" boolean NOT NULL,
    "Intentos" integer NOT NULL,
    "UltimoIntentoEn" timestamp with time zone,
    CONSTRAINT "PK_EntregasAviso" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EntregasAviso_SuscripcionesAviso_SuscripcionId" FOREIGN KEY ("SuscripcionId") REFERENCES "SuscripcionesAviso" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Envios" (
    "Id" uuid NOT NULL,
    "CuentaComercialId" uuid NOT NULL,
    "DestinatarioId" uuid NOT NULL,
    "ZonaId" uuid NOT NULL,
    "ReglasId" uuid NOT NULL,
    "CuadroTarifarioId" uuid NOT NULL,
    "CodigoSeguimiento" text NOT NULL,
    "Estado" integer NOT NULL,
    "ModalidadServicio" integer NOT NULL,
    "FechaAdmision" timestamp with time zone NOT NULL,
    "FechaComprometida" timestamp with time zone NOT NULL,
    "ProgramacionId" uuid NOT NULL,
    "TarifaCalculada" numeric NOT NULL,
    "ParadaId" uuid,
    CONSTRAINT "PK_Envios" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Envios_CuadrosTarifarios_CuadroTarifarioId" FOREIGN KEY ("CuadroTarifarioId") REFERENCES "CuadrosTarifarios" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Envios_CuentasComerciales_CuentaComercialId" FOREIGN KEY ("CuentaComercialId") REFERENCES "CuentasComerciales" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Envios_Destinatarios_DestinatarioId" FOREIGN KEY ("DestinatarioId") REFERENCES "Destinatarios" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Envios_ProgramacionesEntrega_ProgramacionId" FOREIGN KEY ("ProgramacionId") REFERENCES "ProgramacionesEntrega" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Envios_ReglasOperativas_ReglasId" FOREIGN KEY ("ReglasId") REFERENCES "ReglasOperativas" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Envios_ZonasCobertura_ZonaId" FOREIGN KEY ("ZonaId") REFERENCES "ZonasCobertura" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "EventosDeEnvio" (
    "Id" uuid NOT NULL,
    "EnvioId" uuid NOT NULL,
    "Tipo" integer NOT NULL,
    "Origen" integer NOT NULL,
    "EstadoAnterior" integer,
    "EstadoNuevo" integer NOT NULL,
    "ResponsableId" uuid NOT NULL,
    "OcurridoEn" timestamp with time zone NOT NULL,
    "Posicion" jsonb,
    "Detalle" text,
    "IdOperacion" uuid,
    "ProgramacionAnteriorId" uuid,
    "ProgramacionNuevaId" uuid,
    CONSTRAINT "PK_EventosDeEnvio" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_EventosDeEnvio_Envios_EnvioId" FOREIGN KEY ("EnvioId") REFERENCES "Envios" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionAnteriorId" FOREIGN KEY ("ProgramacionAnteriorId") REFERENCES "ProgramacionesEntrega" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionNuevaId" FOREIGN KEY ("ProgramacionNuevaId") REFERENCES "ProgramacionesEntrega" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_EventosDeEnvio_Usuarios_ResponsableId" FOREIGN KEY ("ResponsableId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Paradas" (
    "Id" uuid NOT NULL,
    "RutaId" uuid NOT NULL,
    "EnvioId" uuid NOT NULL,
    "Cod" integer NOT NULL,
    "HoraEstimadaLlegada" timestamp with time zone NOT NULL,
    "Activa" boolean NOT NULL,
    CONSTRAINT "PK_Paradas" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Paradas_Envios_EnvioId" FOREIGN KEY ("EnvioId") REFERENCES "Envios" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Paradas_Rutas_RutaId" FOREIGN KEY ("RutaId") REFERENCES "Rutas" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "IntentosEntrega" (
    "Id" uuid NOT NULL,
    "EnvioId" uuid NOT NULL,
    "ParadaId" uuid NOT NULL,
    "Numero" integer NOT NULL,
    "Resultado" integer NOT NULL,
    "MotivoNoEntregaId" uuid,
    "IdOperacion" uuid NOT NULL,
    "OcurridoEn" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_IntentosEntrega" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_IntentosEntrega_Envios_EnvioId" FOREIGN KEY ("EnvioId") REFERENCES "Envios" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_IntentosEntrega_MotivosNoEntrega_MotivoNoEntregaId" FOREIGN KEY ("MotivoNoEntregaId") REFERENCES "MotivosNoEntrega" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_IntentosEntrega_Paradas_ParadaId" FOREIGN KEY ("ParadaId") REFERENCES "Paradas" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "PruebasEntrega" (
    "Id" uuid NOT NULL,
    "TipoPrueba" integer NOT NULL,
    "Posicion" jsonb,
    "Hora" timestamp with time zone NOT NULL,
    "ArchivoRef" text,
    "ValorTexto" text,
    "IntentoId" uuid NOT NULL,
    CONSTRAINT "PK_PruebasEntrega" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PruebasEntrega_IntentosEntrega_IntentoId" FOREIGN KEY ("IntentoId") REFERENCES "IntentosEntrega" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_AjustesTarifarios_LineaId" ON "AjustesTarifarios" ("LineaId");

CREATE UNIQUE INDEX "IX_Bultos_EnvioId_CodigoNormalizado" ON "Bultos" ("EnvioId", "CodigoNormalizado");

CREATE UNIQUE INDEX "IX_CuadrosTarifarios_OperadorId_Version" ON "CuadrosTarifarios" ("OperadorId", "Version");

CREATE INDEX "IX_CuentasComerciales_ComercioId" ON "CuentasComerciales" ("ComercioId");

CREATE UNIQUE INDEX "IX_CuentasComerciales_OperadorId_ComercioId" ON "CuentasComerciales" ("OperadorId", "ComercioId");

CREATE UNIQUE INDEX "IX_Destinatarios_DireccionId" ON "Destinatarios" ("DireccionId");

CREATE UNIQUE INDEX "IX_Devoluciones_EnvioId" ON "Devoluciones" ("EnvioId");

CREATE INDEX "IX_Discrepancias_BultoId" ON "Discrepancias" ("BultoId");

CREATE INDEX "IX_Discrepancias_EnvioId" ON "Discrepancias" ("EnvioId");

CREATE INDEX "IX_EntregasAviso_EventoId" ON "EntregasAviso" ("EventoId");

CREATE UNIQUE INDEX "IX_EntregasAviso_SuscripcionId_EventoId" ON "EntregasAviso" ("SuscripcionId", "EventoId");

CREATE UNIQUE INDEX "IX_Envios_CodigoSeguimiento" ON "Envios" ("CodigoSeguimiento");

CREATE INDEX "IX_Envios_CuadroTarifarioId" ON "Envios" ("CuadroTarifarioId");

CREATE INDEX "IX_Envios_CuentaComercialId" ON "Envios" ("CuentaComercialId");

CREATE UNIQUE INDEX "IX_Envios_DestinatarioId" ON "Envios" ("DestinatarioId");

CREATE UNIQUE INDEX "IX_Envios_ParadaId" ON "Envios" ("ParadaId");

CREATE INDEX "IX_Envios_ProgramacionId" ON "Envios" ("ProgramacionId");

CREATE INDEX "IX_Envios_ReglasId" ON "Envios" ("ReglasId");

CREATE INDEX "IX_Envios_ZonaId" ON "Envios" ("ZonaId");

CREATE INDEX "IX_EscaneosBulto_BultoId" ON "EscaneosBulto" ("BultoId");

CREATE INDEX "IX_EscaneosBulto_DevolucionId" ON "EscaneosBulto" ("DevolucionId");

CREATE INDEX "IX_EscaneosBulto_ResponsableId" ON "EscaneosBulto" ("ResponsableId");

CREATE INDEX "IX_EscaneosBulto_RutaId" ON "EscaneosBulto" ("RutaId");

CREATE UNIQUE INDEX "IX_EventosDeEnvio_EnvioId_IdOperacion" ON "EventosDeEnvio" ("EnvioId", "IdOperacion") WHERE "IdOperacion" IS NOT NULL;

CREATE INDEX "IX_EventosDeEnvio_EnvioId_OcurridoEn" ON "EventosDeEnvio" ("EnvioId", "OcurridoEn");

CREATE INDEX "IX_EventosDeEnvio_ProgramacionAnteriorId" ON "EventosDeEnvio" ("ProgramacionAnteriorId");

CREATE INDEX "IX_EventosDeEnvio_ProgramacionNuevaId" ON "EventosDeEnvio" ("ProgramacionNuevaId");

CREATE INDEX "IX_EventosDeEnvio_ResponsableId" ON "EventosDeEnvio" ("ResponsableId");

CREATE INDEX "IX_FranjasHorarias_ZonaId" ON "FranjasHorarias" ("ZonaId");

CREATE UNIQUE INDEX "IX_IntentosEntrega_EnvioId_IdOperacion" ON "IntentosEntrega" ("EnvioId", "IdOperacion");

CREATE UNIQUE INDEX "IX_IntentosEntrega_EnvioId_Numero" ON "IntentosEntrega" ("EnvioId", "Numero");

CREATE INDEX "IX_IntentosEntrega_MotivoNoEntregaId" ON "IntentosEntrega" ("MotivoNoEntregaId");

CREATE INDEX "IX_IntentosEntrega_ParadaId" ON "IntentosEntrega" ("ParadaId");

CREATE INDEX "IX_LineasTarifa_CuadroId" ON "LineasTarifa" ("CuadroId");

CREATE INDEX "IX_LineasTarifa_ZonaId" ON "LineasTarifa" ("ZonaId");

CREATE INDEX "IX_MotivosNoEntrega_ReglasId" ON "MotivosNoEntrega" ("ReglasId");

CREATE INDEX "IX_Notificaciones_DestinatarioId" ON "Notificaciones" ("DestinatarioId");

CREATE UNIQUE INDEX "IX_Paradas_EnvioId" ON "Paradas" ("EnvioId") WHERE "Activa" = TRUE;

CREATE UNIQUE INDEX "IX_Paradas_RutaId_Cod" ON "Paradas" ("RutaId", "Cod");

CREATE UNIQUE INDEX "IX_PlazosServicio_ReglasId_ModalidadServicio" ON "PlazosServicio" ("ReglasId", "ModalidadServicio");

CREATE INDEX "IX_ProgramacionesEntrega_FranjaId" ON "ProgramacionesEntrega" ("FranjaId");

CREATE INDEX "IX_PruebasEntrega_IntentoId" ON "PruebasEntrega" ("IntentoId");

CREATE UNIQUE INDEX "IX_ReglasOperativas_OperadorId_Version" ON "ReglasOperativas" ("OperadorId", "Version");

CREATE UNIQUE INDEX "IX_ReglasOperativas_PoliticaDevolucionId" ON "ReglasOperativas" ("PoliticaDevolucionId");

CREATE UNIQUE INDEX "IX_Rendiciones_RutaId" ON "Rendiciones" ("RutaId");

CREATE INDEX "IX_Rutas_RepartidorId" ON "Rutas" ("RepartidorId");

CREATE INDEX "IX_Rutas_VehiculoId" ON "Rutas" ("VehiculoId");

CREATE INDEX "IX_SuscripcionesAviso_CuentaComercialId" ON "SuscripcionesAviso" ("CuentaComercialId");

CREATE UNIQUE INDEX "IX_Usuarios_OperadorId_EmailNormalizado" ON "Usuarios" ("OperadorId", "EmailNormalizado");

CREATE INDEX "IX_UsuariosCuentasComerciales_CuentaComercialId" ON "UsuariosCuentasComerciales" ("CuentaComercialId");

CREATE UNIQUE INDEX "IX_Vehiculos_OperadorId_MatriculaNormalizada" ON "Vehiculos" ("OperadorId", "MatriculaNormalizada");

CREATE INDEX "IX_ZonasCobertura_OperadorId" ON "ZonasCobertura" ("OperadorId");

ALTER TABLE "Bultos" ADD CONSTRAINT "FK_Bultos_Envios_EnvioId" FOREIGN KEY ("EnvioId") REFERENCES "Envios" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Devoluciones" ADD CONSTRAINT "FK_Devoluciones_Envios_EnvioId" FOREIGN KEY ("EnvioId") REFERENCES "Envios" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Discrepancias" ADD CONSTRAINT "FK_Discrepancias_Envios_EnvioId" FOREIGN KEY ("EnvioId") REFERENCES "Envios" ("Id") ON DELETE RESTRICT;

ALTER TABLE "EntregasAviso" ADD CONSTRAINT "FK_EntregasAviso_EventosDeEnvio_EventoId" FOREIGN KEY ("EventoId") REFERENCES "EventosDeEnvio" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Envios" ADD CONSTRAINT "FK_Envios_Paradas_ParadaId" FOREIGN KEY ("ParadaId") REFERENCES "Paradas" ("Id") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261014230137_InitialCreate', '10.0.12');

COMMIT;

START TRANSACTION;
ALTER TABLE "AjustesTarifarios" DROP CONSTRAINT "FK_AjustesTarifarios_LineasTarifa_LineaId";

ALTER TABLE "Bultos" DROP CONSTRAINT "FK_Bultos_Envios_EnvioId";

ALTER TABLE "Destinatarios" DROP CONSTRAINT "FK_Destinatarios_Direcciones_DireccionId";

ALTER TABLE "Devoluciones" DROP CONSTRAINT "FK_Devoluciones_Envios_EnvioId";

ALTER TABLE "Discrepancias" DROP CONSTRAINT "FK_Discrepancias_Bultos_BultoId";

ALTER TABLE "Discrepancias" DROP CONSTRAINT "FK_Discrepancias_Envios_EnvioId";

ALTER TABLE "EntregasAviso" DROP CONSTRAINT "FK_EntregasAviso_EventosDeEnvio_EventoId";

ALTER TABLE "EntregasAviso" DROP CONSTRAINT "FK_EntregasAviso_SuscripcionesAviso_SuscripcionId";

ALTER TABLE "Envios" DROP CONSTRAINT "FK_Envios_CuadrosTarifarios_CuadroTarifarioId";

ALTER TABLE "Envios" DROP CONSTRAINT "FK_Envios_CuentasComerciales_CuentaComercialId";

ALTER TABLE "Envios" DROP CONSTRAINT "FK_Envios_Destinatarios_DestinatarioId";

ALTER TABLE "Envios" DROP CONSTRAINT "FK_Envios_Paradas_ParadaId";

ALTER TABLE "Envios" DROP CONSTRAINT "FK_Envios_ProgramacionesEntrega_ProgramacionId";

ALTER TABLE "Envios" DROP CONSTRAINT "FK_Envios_ReglasOperativas_ReglasId";

ALTER TABLE "Envios" DROP CONSTRAINT "FK_Envios_ZonasCobertura_ZonaId";

ALTER TABLE "EscaneosBulto" DROP CONSTRAINT "FK_EscaneosBulto_Bultos_BultoId";

ALTER TABLE "EscaneosBulto" DROP CONSTRAINT "FK_EscaneosBulto_Devoluciones_DevolucionId";

ALTER TABLE "EscaneosBulto" DROP CONSTRAINT "FK_EscaneosBulto_Rutas_RutaId";

ALTER TABLE "EscaneosBulto" DROP CONSTRAINT "FK_EscaneosBulto_Usuarios_ResponsableId";

ALTER TABLE "EventosDeEnvio" DROP CONSTRAINT "FK_EventosDeEnvio_Envios_EnvioId";

ALTER TABLE "EventosDeEnvio" DROP CONSTRAINT "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionAnteriorId";

ALTER TABLE "EventosDeEnvio" DROP CONSTRAINT "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionNuevaId";

ALTER TABLE "EventosDeEnvio" DROP CONSTRAINT "FK_EventosDeEnvio_Usuarios_ResponsableId";

ALTER TABLE "FranjasHorarias" DROP CONSTRAINT "FK_FranjasHorarias_ZonasCobertura_ZonaId";

ALTER TABLE "IntentosEntrega" DROP CONSTRAINT "FK_IntentosEntrega_Envios_EnvioId";

ALTER TABLE "IntentosEntrega" DROP CONSTRAINT "FK_IntentosEntrega_MotivosNoEntrega_MotivoNoEntregaId";

ALTER TABLE "IntentosEntrega" DROP CONSTRAINT "FK_IntentosEntrega_Paradas_ParadaId";

ALTER TABLE "LineasTarifa" DROP CONSTRAINT "FK_LineasTarifa_CuadrosTarifarios_CuadroId";

ALTER TABLE "LineasTarifa" DROP CONSTRAINT "FK_LineasTarifa_ZonasCobertura_ZonaId";

ALTER TABLE "MotivosNoEntrega" DROP CONSTRAINT "FK_MotivosNoEntrega_ReglasOperativas_ReglasId";

ALTER TABLE "Notificaciones" DROP CONSTRAINT "FK_Notificaciones_Destinatarios_DestinatarioId";

ALTER TABLE "Paradas" DROP CONSTRAINT "FK_Paradas_Envios_EnvioId";

ALTER TABLE "Paradas" DROP CONSTRAINT "FK_Paradas_Rutas_RutaId";

ALTER TABLE "PlazosServicio" DROP CONSTRAINT "FK_PlazosServicio_ReglasOperativas_ReglasId";

ALTER TABLE "ProgramacionesEntrega" DROP CONSTRAINT "FK_ProgramacionesEntrega_FranjasHorarias_FranjaId";

ALTER TABLE "PruebasEntrega" DROP CONSTRAINT "FK_PruebasEntrega_IntentosEntrega_IntentoId";

ALTER TABLE "ReglasOperativas" DROP CONSTRAINT "FK_ReglasOperativas_PoliticasDevolucion_PoliticaDevolucionId";

ALTER TABLE "Rendiciones" DROP CONSTRAINT "FK_Rendiciones_Rutas_RutaId";

ALTER TABLE "Rutas" DROP CONSTRAINT "FK_Rutas_Usuarios_RepartidorId";

ALTER TABLE "Rutas" DROP CONSTRAINT "FK_Rutas_Vehiculos_VehiculoId";

ALTER TABLE "SuscripcionesAviso" DROP CONSTRAINT "FK_SuscripcionesAviso_CuentasComerciales_CuentaComercialId";

ALTER TABLE "UsuariosCuentasComerciales" DROP CONSTRAINT "FK_UsuariosCuentasComerciales_CuentasComerciales_CuentaComerci~";

ALTER TABLE "UsuariosCuentasComerciales" DROP CONSTRAINT "FK_UsuariosCuentasComerciales_Usuarios_UsuarioId";

DROP INDEX "IX_UsuariosCuentasComerciales_CuentaComercialId";

DROP INDEX "IX_SuscripcionesAviso_CuentaComercialId";

DROP INDEX "IX_Rutas_RepartidorId";

DROP INDEX "IX_Rutas_VehiculoId";

DROP INDEX "IX_Rendiciones_RutaId";

DROP INDEX "IX_ReglasOperativas_PoliticaDevolucionId";

DROP INDEX "IX_PruebasEntrega_IntentoId";

DROP INDEX "IX_ProgramacionesEntrega_FranjaId";

DROP INDEX "IX_Notificaciones_DestinatarioId";

DROP INDEX "IX_MotivosNoEntrega_ReglasId";

DROP INDEX "IX_LineasTarifa_CuadroId";

DROP INDEX "IX_LineasTarifa_ZonaId";

DROP INDEX "IX_IntentosEntrega_MotivoNoEntregaId";

DROP INDEX "IX_IntentosEntrega_ParadaId";

DROP INDEX "IX_FranjasHorarias_ZonaId";

DROP INDEX "IX_EventosDeEnvio_ProgramacionAnteriorId";

DROP INDEX "IX_EventosDeEnvio_ProgramacionNuevaId";

DROP INDEX "IX_EventosDeEnvio_ResponsableId";

DROP INDEX "IX_EscaneosBulto_BultoId";

DROP INDEX "IX_EscaneosBulto_DevolucionId";

DROP INDEX "IX_EscaneosBulto_ResponsableId";

DROP INDEX "IX_EscaneosBulto_RutaId";

DROP INDEX "IX_Envios_CuadroTarifarioId";

DROP INDEX "IX_Envios_CuentaComercialId";

DROP INDEX "IX_Envios_DestinatarioId";

DROP INDEX "IX_Envios_ParadaId";

DROP INDEX "IX_Envios_ProgramacionId";

DROP INDEX "IX_Envios_ReglasId";

DROP INDEX "IX_Envios_ZonaId";

DROP INDEX "IX_EntregasAviso_EventoId";

DROP INDEX "IX_Discrepancias_BultoId";

DROP INDEX "IX_Discrepancias_EnvioId";

DROP INDEX "IX_Devoluciones_EnvioId";

DROP INDEX "IX_Destinatarios_DireccionId";

DROP INDEX "IX_AjustesTarifarios_LineaId";

ALTER TABLE "ZonasCobertura" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Vehiculos" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "UsuariosCuentasComerciales" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Usuarios" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "SuscripcionesAviso" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Rutas" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Rendiciones" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "ReglasOperativas" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "PruebasEntrega" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "ProgramacionesEntrega" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "PoliticasDevolucion" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "PlazosServicio" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Paradas" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Notificaciones" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "MotivosNoEntrega" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "LineasTarifa" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "IntentosEntrega" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "FranjasHorarias" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "EventosDeEnvio" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "EscaneosBulto" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Envios" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "EntregasAviso" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Discrepancias" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Direcciones" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Devoluciones" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Destinatarios" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "CuentasComerciales" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "CuadrosTarifarios" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Bultos" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "AjustesTarifarios" ADD "TenantId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

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

ALTER TABLE "ZonasCobertura" ADD CONSTRAINT "AK_ZonasCobertura_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "Vehiculos" ADD CONSTRAINT "AK_Vehiculos_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "Usuarios" ADD CONSTRAINT "AK_Usuarios_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "SuscripcionesAviso" ADD CONSTRAINT "AK_SuscripcionesAviso_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "Rutas" ADD CONSTRAINT "AK_Rutas_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "Rendiciones" ADD CONSTRAINT "AK_Rendiciones_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "ReglasOperativas" ADD CONSTRAINT "AK_ReglasOperativas_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "PruebasEntrega" ADD CONSTRAINT "AK_PruebasEntrega_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "ProgramacionesEntrega" ADD CONSTRAINT "AK_ProgramacionesEntrega_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "PoliticasDevolucion" ADD CONSTRAINT "AK_PoliticasDevolucion_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "PlazosServicio" ADD CONSTRAINT "AK_PlazosServicio_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "Paradas" ADD CONSTRAINT "AK_Paradas_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "Notificaciones" ADD CONSTRAINT "AK_Notificaciones_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "MotivosNoEntrega" ADD CONSTRAINT "AK_MotivosNoEntrega_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "LineasTarifa" ADD CONSTRAINT "AK_LineasTarifa_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "IntentosEntrega" ADD CONSTRAINT "AK_IntentosEntrega_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "FranjasHorarias" ADD CONSTRAINT "AK_FranjasHorarias_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "EventosDeEnvio" ADD CONSTRAINT "AK_EventosDeEnvio_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "EscaneosBulto" ADD CONSTRAINT "AK_EscaneosBulto_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "Envios" ADD CONSTRAINT "AK_Envios_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "EntregasAviso" ADD CONSTRAINT "AK_EntregasAviso_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "Discrepancias" ADD CONSTRAINT "AK_Discrepancias_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "Direcciones" ADD CONSTRAINT "AK_Direcciones_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "Devoluciones" ADD CONSTRAINT "AK_Devoluciones_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "Destinatarios" ADD CONSTRAINT "AK_Destinatarios_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "CuentasComerciales" ADD CONSTRAINT "AK_CuentasComerciales_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "CuadrosTarifarios" ADD CONSTRAINT "AK_CuadrosTarifarios_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "Bultos" ADD CONSTRAINT "AK_Bultos_Id_TenantId" UNIQUE ("Id", "TenantId");

ALTER TABLE "AjustesTarifarios" ADD CONSTRAINT "AK_AjustesTarifarios_Id_TenantId" UNIQUE ("Id", "TenantId");

CREATE INDEX "IX_ZonasCobertura_TenantId" ON "ZonasCobertura" ("TenantId");

ALTER TABLE "ZonasCobertura" ADD CONSTRAINT "CK_ZonasCobertura_OperadorTenant" CHECK ("OperadorId" = "TenantId");

CREATE INDEX "IX_Vehiculos_TenantId" ON "Vehiculos" ("TenantId");

ALTER TABLE "Vehiculos" ADD CONSTRAINT "CK_Vehiculos_OperadorTenant" CHECK ("OperadorId" = "TenantId");

CREATE INDEX "IX_UsuariosCuentasComerciales_CuentaComercialId_TenantId" ON "UsuariosCuentasComerciales" ("CuentaComercialId", "TenantId");

CREATE INDEX "IX_UsuariosCuentasComerciales_TenantId" ON "UsuariosCuentasComerciales" ("TenantId");

CREATE INDEX "IX_UsuariosCuentasComerciales_UsuarioId_TenantId" ON "UsuariosCuentasComerciales" ("UsuarioId", "TenantId");

CREATE INDEX "IX_Usuarios_TenantId" ON "Usuarios" ("TenantId");

ALTER TABLE "Usuarios" ADD CONSTRAINT "CK_Usuarios_OperadorTenant" CHECK ("OperadorId" = "TenantId");

CREATE INDEX "IX_SuscripcionesAviso_CuentaComercialId_TenantId" ON "SuscripcionesAviso" ("CuentaComercialId", "TenantId");

CREATE INDEX "IX_SuscripcionesAviso_TenantId" ON "SuscripcionesAviso" ("TenantId");

CREATE INDEX "IX_Rutas_RepartidorId_TenantId" ON "Rutas" ("RepartidorId", "TenantId");

CREATE INDEX "IX_Rutas_TenantId" ON "Rutas" ("TenantId");

CREATE INDEX "IX_Rutas_VehiculoId_TenantId" ON "Rutas" ("VehiculoId", "TenantId");

CREATE UNIQUE INDEX "IX_Rendiciones_RutaId_TenantId" ON "Rendiciones" ("RutaId", "TenantId");

CREATE INDEX "IX_Rendiciones_TenantId" ON "Rendiciones" ("TenantId");

CREATE UNIQUE INDEX "IX_ReglasOperativas_PoliticaDevolucionId_TenantId" ON "ReglasOperativas" ("PoliticaDevolucionId", "TenantId");

CREATE INDEX "IX_ReglasOperativas_TenantId" ON "ReglasOperativas" ("TenantId");

ALTER TABLE "ReglasOperativas" ADD CONSTRAINT "CK_ReglasOperativas_OperadorTenant" CHECK ("OperadorId" = "TenantId");

CREATE INDEX "IX_PruebasEntrega_IntentoId_TenantId" ON "PruebasEntrega" ("IntentoId", "TenantId");

CREATE INDEX "IX_PruebasEntrega_TenantId" ON "PruebasEntrega" ("TenantId");

CREATE INDEX "IX_ProgramacionesEntrega_FranjaId_TenantId" ON "ProgramacionesEntrega" ("FranjaId", "TenantId");

CREATE INDEX "IX_ProgramacionesEntrega_TenantId" ON "ProgramacionesEntrega" ("TenantId");

CREATE INDEX "IX_PoliticasDevolucion_TenantId" ON "PoliticasDevolucion" ("TenantId");

CREATE INDEX "IX_PlazosServicio_ReglasId_TenantId" ON "PlazosServicio" ("ReglasId", "TenantId");

CREATE INDEX "IX_PlazosServicio_TenantId" ON "PlazosServicio" ("TenantId");

CREATE INDEX "IX_Paradas_EnvioId_TenantId" ON "Paradas" ("EnvioId", "TenantId");

CREATE INDEX "IX_Paradas_RutaId_TenantId" ON "Paradas" ("RutaId", "TenantId");

CREATE INDEX "IX_Paradas_TenantId" ON "Paradas" ("TenantId");

CREATE INDEX "IX_Notificaciones_DestinatarioId_TenantId" ON "Notificaciones" ("DestinatarioId", "TenantId");

CREATE INDEX "IX_Notificaciones_TenantId" ON "Notificaciones" ("TenantId");

CREATE INDEX "IX_MotivosNoEntrega_ReglasId_TenantId" ON "MotivosNoEntrega" ("ReglasId", "TenantId");

CREATE INDEX "IX_MotivosNoEntrega_TenantId" ON "MotivosNoEntrega" ("TenantId");

CREATE INDEX "IX_LineasTarifa_CuadroId_TenantId" ON "LineasTarifa" ("CuadroId", "TenantId");

CREATE INDEX "IX_LineasTarifa_TenantId" ON "LineasTarifa" ("TenantId");

CREATE INDEX "IX_LineasTarifa_ZonaId_TenantId" ON "LineasTarifa" ("ZonaId", "TenantId");

CREATE INDEX "IX_IntentosEntrega_EnvioId_TenantId" ON "IntentosEntrega" ("EnvioId", "TenantId");

CREATE INDEX "IX_IntentosEntrega_MotivoNoEntregaId_TenantId" ON "IntentosEntrega" ("MotivoNoEntregaId", "TenantId");

CREATE INDEX "IX_IntentosEntrega_ParadaId_TenantId" ON "IntentosEntrega" ("ParadaId", "TenantId");

CREATE INDEX "IX_IntentosEntrega_TenantId" ON "IntentosEntrega" ("TenantId");

CREATE INDEX "IX_FranjasHorarias_TenantId" ON "FranjasHorarias" ("TenantId");

CREATE INDEX "IX_FranjasHorarias_ZonaId_TenantId" ON "FranjasHorarias" ("ZonaId", "TenantId");

CREATE INDEX "IX_EventosDeEnvio_EnvioId_TenantId" ON "EventosDeEnvio" ("EnvioId", "TenantId");

CREATE INDEX "IX_EventosDeEnvio_ProgramacionAnteriorId_TenantId" ON "EventosDeEnvio" ("ProgramacionAnteriorId", "TenantId");

CREATE INDEX "IX_EventosDeEnvio_ProgramacionNuevaId_TenantId" ON "EventosDeEnvio" ("ProgramacionNuevaId", "TenantId");

CREATE INDEX "IX_EventosDeEnvio_ResponsableId_TenantId" ON "EventosDeEnvio" ("ResponsableId", "TenantId");

CREATE INDEX "IX_EventosDeEnvio_TenantId" ON "EventosDeEnvio" ("TenantId");

CREATE INDEX "IX_EscaneosBulto_BultoId_TenantId" ON "EscaneosBulto" ("BultoId", "TenantId");

CREATE INDEX "IX_EscaneosBulto_DevolucionId_TenantId" ON "EscaneosBulto" ("DevolucionId", "TenantId");

CREATE INDEX "IX_EscaneosBulto_ResponsableId_TenantId" ON "EscaneosBulto" ("ResponsableId", "TenantId");

CREATE INDEX "IX_EscaneosBulto_RutaId_TenantId" ON "EscaneosBulto" ("RutaId", "TenantId");

CREATE INDEX "IX_EscaneosBulto_TenantId" ON "EscaneosBulto" ("TenantId");

CREATE INDEX "IX_Envios_CuadroTarifarioId_TenantId" ON "Envios" ("CuadroTarifarioId", "TenantId");

CREATE INDEX "IX_Envios_CuentaComercialId_TenantId" ON "Envios" ("CuentaComercialId", "TenantId");

CREATE UNIQUE INDEX "IX_Envios_DestinatarioId_TenantId" ON "Envios" ("DestinatarioId", "TenantId");

CREATE UNIQUE INDEX "IX_Envios_ParadaId_TenantId" ON "Envios" ("ParadaId", "TenantId");

CREATE INDEX "IX_Envios_ProgramacionId_TenantId" ON "Envios" ("ProgramacionId", "TenantId");

CREATE INDEX "IX_Envios_ReglasId_TenantId" ON "Envios" ("ReglasId", "TenantId");

CREATE INDEX "IX_Envios_TenantId" ON "Envios" ("TenantId");

CREATE INDEX "IX_Envios_ZonaId_TenantId" ON "Envios" ("ZonaId", "TenantId");

CREATE INDEX "IX_EntregasAviso_EventoId_TenantId" ON "EntregasAviso" ("EventoId", "TenantId");

CREATE INDEX "IX_EntregasAviso_SuscripcionId_TenantId" ON "EntregasAviso" ("SuscripcionId", "TenantId");

CREATE INDEX "IX_EntregasAviso_TenantId" ON "EntregasAviso" ("TenantId");

CREATE INDEX "IX_Discrepancias_BultoId_TenantId" ON "Discrepancias" ("BultoId", "TenantId");

CREATE INDEX "IX_Discrepancias_EnvioId_TenantId" ON "Discrepancias" ("EnvioId", "TenantId");

CREATE INDEX "IX_Discrepancias_TenantId" ON "Discrepancias" ("TenantId");

CREATE INDEX "IX_Direcciones_TenantId" ON "Direcciones" ("TenantId");

CREATE UNIQUE INDEX "IX_Devoluciones_EnvioId_TenantId" ON "Devoluciones" ("EnvioId", "TenantId");

CREATE INDEX "IX_Devoluciones_TenantId" ON "Devoluciones" ("TenantId");

CREATE UNIQUE INDEX "IX_Destinatarios_DireccionId_TenantId" ON "Destinatarios" ("DireccionId", "TenantId");

CREATE INDEX "IX_Destinatarios_TenantId" ON "Destinatarios" ("TenantId");

CREATE INDEX "IX_CuentasComerciales_TenantId" ON "CuentasComerciales" ("TenantId");

ALTER TABLE "CuentasComerciales" ADD CONSTRAINT "CK_CuentasComerciales_OperadorTenant" CHECK ("OperadorId" = "TenantId");

CREATE INDEX "IX_CuadrosTarifarios_TenantId" ON "CuadrosTarifarios" ("TenantId");

ALTER TABLE "CuadrosTarifarios" ADD CONSTRAINT "CK_CuadrosTarifarios_OperadorTenant" CHECK ("OperadorId" = "TenantId");

CREATE INDEX "IX_Bultos_EnvioId_TenantId" ON "Bultos" ("EnvioId", "TenantId");

CREATE INDEX "IX_Bultos_TenantId" ON "Bultos" ("TenantId");

CREATE INDEX "IX_AjustesTarifarios_LineaId_TenantId" ON "AjustesTarifarios" ("LineaId", "TenantId");

CREATE INDEX "IX_AjustesTarifarios_TenantId" ON "AjustesTarifarios" ("TenantId");

ALTER TABLE "AjustesTarifarios" ADD CONSTRAINT "FK_AjustesTarifarios_LineasTarifa_LineaId_TenantId" FOREIGN KEY ("LineaId", "TenantId") REFERENCES "LineasTarifa" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "AjustesTarifarios" ADD CONSTRAINT "FK_AjustesTarifarios_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Bultos" ADD CONSTRAINT "FK_Bultos_Envios_EnvioId_TenantId" FOREIGN KEY ("EnvioId", "TenantId") REFERENCES "Envios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Bultos" ADD CONSTRAINT "FK_Bultos_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "CuadrosTarifarios" ADD CONSTRAINT "FK_CuadrosTarifarios_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "CuentasComerciales" ADD CONSTRAINT "FK_CuentasComerciales_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Destinatarios" ADD CONSTRAINT "FK_Destinatarios_Direcciones_DireccionId_TenantId" FOREIGN KEY ("DireccionId", "TenantId") REFERENCES "Direcciones" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Destinatarios" ADD CONSTRAINT "FK_Destinatarios_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Devoluciones" ADD CONSTRAINT "FK_Devoluciones_Envios_EnvioId_TenantId" FOREIGN KEY ("EnvioId", "TenantId") REFERENCES "Envios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Devoluciones" ADD CONSTRAINT "FK_Devoluciones_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Direcciones" ADD CONSTRAINT "FK_Direcciones_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Discrepancias" ADD CONSTRAINT "FK_Discrepancias_Bultos_BultoId_TenantId" FOREIGN KEY ("BultoId", "TenantId") REFERENCES "Bultos" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Discrepancias" ADD CONSTRAINT "FK_Discrepancias_Envios_EnvioId_TenantId" FOREIGN KEY ("EnvioId", "TenantId") REFERENCES "Envios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Discrepancias" ADD CONSTRAINT "FK_Discrepancias_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "EntregasAviso" ADD CONSTRAINT "FK_EntregasAviso_EventosDeEnvio_EventoId_TenantId" FOREIGN KEY ("EventoId", "TenantId") REFERENCES "EventosDeEnvio" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "EntregasAviso" ADD CONSTRAINT "FK_EntregasAviso_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "EntregasAviso" ADD CONSTRAINT "FK_EntregasAviso_SuscripcionesAviso_SuscripcionId_TenantId" FOREIGN KEY ("SuscripcionId", "TenantId") REFERENCES "SuscripcionesAviso" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Envios" ADD CONSTRAINT "FK_Envios_CuadrosTarifarios_CuadroTarifarioId_TenantId" FOREIGN KEY ("CuadroTarifarioId", "TenantId") REFERENCES "CuadrosTarifarios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Envios" ADD CONSTRAINT "FK_Envios_CuentasComerciales_CuentaComercialId_TenantId" FOREIGN KEY ("CuentaComercialId", "TenantId") REFERENCES "CuentasComerciales" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Envios" ADD CONSTRAINT "FK_Envios_Destinatarios_DestinatarioId_TenantId" FOREIGN KEY ("DestinatarioId", "TenantId") REFERENCES "Destinatarios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Envios" ADD CONSTRAINT "FK_Envios_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Envios" ADD CONSTRAINT "FK_Envios_Paradas_ParadaId_TenantId" FOREIGN KEY ("ParadaId", "TenantId") REFERENCES "Paradas" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Envios" ADD CONSTRAINT "FK_Envios_ProgramacionesEntrega_ProgramacionId_TenantId" FOREIGN KEY ("ProgramacionId", "TenantId") REFERENCES "ProgramacionesEntrega" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Envios" ADD CONSTRAINT "FK_Envios_ReglasOperativas_ReglasId_TenantId" FOREIGN KEY ("ReglasId", "TenantId") REFERENCES "ReglasOperativas" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Envios" ADD CONSTRAINT "FK_Envios_ZonasCobertura_ZonaId_TenantId" FOREIGN KEY ("ZonaId", "TenantId") REFERENCES "ZonasCobertura" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "EscaneosBulto" ADD CONSTRAINT "FK_EscaneosBulto_Bultos_BultoId_TenantId" FOREIGN KEY ("BultoId", "TenantId") REFERENCES "Bultos" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "EscaneosBulto" ADD CONSTRAINT "FK_EscaneosBulto_Devoluciones_DevolucionId_TenantId" FOREIGN KEY ("DevolucionId", "TenantId") REFERENCES "Devoluciones" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "EscaneosBulto" ADD CONSTRAINT "FK_EscaneosBulto_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "EscaneosBulto" ADD CONSTRAINT "FK_EscaneosBulto_Rutas_RutaId_TenantId" FOREIGN KEY ("RutaId", "TenantId") REFERENCES "Rutas" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "EscaneosBulto" ADD CONSTRAINT "FK_EscaneosBulto_Usuarios_ResponsableId_TenantId" FOREIGN KEY ("ResponsableId", "TenantId") REFERENCES "Usuarios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "EventosDeEnvio" ADD CONSTRAINT "FK_EventosDeEnvio_Envios_EnvioId_TenantId" FOREIGN KEY ("EnvioId", "TenantId") REFERENCES "Envios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "EventosDeEnvio" ADD CONSTRAINT "FK_EventosDeEnvio_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "EventosDeEnvio" ADD CONSTRAINT "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionAnteriorId~" FOREIGN KEY ("ProgramacionAnteriorId", "TenantId") REFERENCES "ProgramacionesEntrega" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "EventosDeEnvio" ADD CONSTRAINT "FK_EventosDeEnvio_ProgramacionesEntrega_ProgramacionNuevaId_Te~" FOREIGN KEY ("ProgramacionNuevaId", "TenantId") REFERENCES "ProgramacionesEntrega" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "EventosDeEnvio" ADD CONSTRAINT "FK_EventosDeEnvio_Usuarios_ResponsableId_TenantId" FOREIGN KEY ("ResponsableId", "TenantId") REFERENCES "Usuarios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "FranjasHorarias" ADD CONSTRAINT "FK_FranjasHorarias_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "FranjasHorarias" ADD CONSTRAINT "FK_FranjasHorarias_ZonasCobertura_ZonaId_TenantId" FOREIGN KEY ("ZonaId", "TenantId") REFERENCES "ZonasCobertura" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "IntentosEntrega" ADD CONSTRAINT "FK_IntentosEntrega_Envios_EnvioId_TenantId" FOREIGN KEY ("EnvioId", "TenantId") REFERENCES "Envios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "IntentosEntrega" ADD CONSTRAINT "FK_IntentosEntrega_MotivosNoEntrega_MotivoNoEntregaId_TenantId" FOREIGN KEY ("MotivoNoEntregaId", "TenantId") REFERENCES "MotivosNoEntrega" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "IntentosEntrega" ADD CONSTRAINT "FK_IntentosEntrega_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "IntentosEntrega" ADD CONSTRAINT "FK_IntentosEntrega_Paradas_ParadaId_TenantId" FOREIGN KEY ("ParadaId", "TenantId") REFERENCES "Paradas" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "LineasTarifa" ADD CONSTRAINT "FK_LineasTarifa_CuadrosTarifarios_CuadroId_TenantId" FOREIGN KEY ("CuadroId", "TenantId") REFERENCES "CuadrosTarifarios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "LineasTarifa" ADD CONSTRAINT "FK_LineasTarifa_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "LineasTarifa" ADD CONSTRAINT "FK_LineasTarifa_ZonasCobertura_ZonaId_TenantId" FOREIGN KEY ("ZonaId", "TenantId") REFERENCES "ZonasCobertura" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "MotivosNoEntrega" ADD CONSTRAINT "FK_MotivosNoEntrega_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "MotivosNoEntrega" ADD CONSTRAINT "FK_MotivosNoEntrega_ReglasOperativas_ReglasId_TenantId" FOREIGN KEY ("ReglasId", "TenantId") REFERENCES "ReglasOperativas" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Notificaciones" ADD CONSTRAINT "FK_Notificaciones_Destinatarios_DestinatarioId_TenantId" FOREIGN KEY ("DestinatarioId", "TenantId") REFERENCES "Destinatarios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Notificaciones" ADD CONSTRAINT "FK_Notificaciones_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Paradas" ADD CONSTRAINT "FK_Paradas_Envios_EnvioId_TenantId" FOREIGN KEY ("EnvioId", "TenantId") REFERENCES "Envios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Paradas" ADD CONSTRAINT "FK_Paradas_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Paradas" ADD CONSTRAINT "FK_Paradas_Rutas_RutaId_TenantId" FOREIGN KEY ("RutaId", "TenantId") REFERENCES "Rutas" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "PlazosServicio" ADD CONSTRAINT "FK_PlazosServicio_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "PlazosServicio" ADD CONSTRAINT "FK_PlazosServicio_ReglasOperativas_ReglasId_TenantId" FOREIGN KEY ("ReglasId", "TenantId") REFERENCES "ReglasOperativas" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "PoliticasDevolucion" ADD CONSTRAINT "FK_PoliticasDevolucion_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "ProgramacionesEntrega" ADD CONSTRAINT "FK_ProgramacionesEntrega_FranjasHorarias_FranjaId_TenantId" FOREIGN KEY ("FranjaId", "TenantId") REFERENCES "FranjasHorarias" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "ProgramacionesEntrega" ADD CONSTRAINT "FK_ProgramacionesEntrega_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "PruebasEntrega" ADD CONSTRAINT "FK_PruebasEntrega_IntentosEntrega_IntentoId_TenantId" FOREIGN KEY ("IntentoId", "TenantId") REFERENCES "IntentosEntrega" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "PruebasEntrega" ADD CONSTRAINT "FK_PruebasEntrega_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "ReglasOperativas" ADD CONSTRAINT "FK_ReglasOperativas_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "ReglasOperativas" ADD CONSTRAINT "FK_ReglasOperativas_PoliticasDevolucion_PoliticaDevolucionId_T~" FOREIGN KEY ("PoliticaDevolucionId", "TenantId") REFERENCES "PoliticasDevolucion" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Rendiciones" ADD CONSTRAINT "FK_Rendiciones_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Rendiciones" ADD CONSTRAINT "FK_Rendiciones_Rutas_RutaId_TenantId" FOREIGN KEY ("RutaId", "TenantId") REFERENCES "Rutas" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Rutas" ADD CONSTRAINT "FK_Rutas_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Rutas" ADD CONSTRAINT "FK_Rutas_Usuarios_RepartidorId_TenantId" FOREIGN KEY ("RepartidorId", "TenantId") REFERENCES "Usuarios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Rutas" ADD CONSTRAINT "FK_Rutas_Vehiculos_VehiculoId_TenantId" FOREIGN KEY ("VehiculoId", "TenantId") REFERENCES "Vehiculos" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "SuscripcionesAviso" ADD CONSTRAINT "FK_SuscripcionesAviso_CuentasComerciales_CuentaComercialId_Ten~" FOREIGN KEY ("CuentaComercialId", "TenantId") REFERENCES "CuentasComerciales" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "SuscripcionesAviso" ADD CONSTRAINT "FK_SuscripcionesAviso_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "Usuarios" ADD CONSTRAINT "FK_Usuarios_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "UsuariosCuentasComerciales" ADD CONSTRAINT "FK_UsuariosCuentasComerciales_CuentasComerciales_CuentaComerci~" FOREIGN KEY ("CuentaComercialId", "TenantId") REFERENCES "CuentasComerciales" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "UsuariosCuentasComerciales" ADD CONSTRAINT "FK_UsuariosCuentasComerciales_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "UsuariosCuentasComerciales" ADD CONSTRAINT "FK_UsuariosCuentasComerciales_Usuarios_UsuarioId_TenantId" FOREIGN KEY ("UsuarioId", "TenantId") REFERENCES "Usuarios" ("Id", "TenantId") ON DELETE RESTRICT;

ALTER TABLE "Vehiculos" ADD CONSTRAINT "FK_Vehiculos_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

ALTER TABLE "ZonasCobertura" ADD CONSTRAINT "FK_ZonasCobertura_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261017182252_OperatorIsolation', '10.0.12');

COMMIT;

START TRANSACTION;
ALTER TABLE "Rutas" ADD "RowVersion" uuid NOT NULL DEFAULT (gen_random_uuid());

ALTER TABLE "Envios" ADD "RowVersion" uuid NOT NULL DEFAULT (gen_random_uuid());

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261021210217_OptimisticConcurrency', '10.0.12');

COMMIT;

START TRANSACTION;
CREATE TABLE "MensajesOutbox" (
    "Id" uuid NOT NULL,
    "EventoId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Tipo" character varying(100) NOT NULL,
    "Contenido" jsonb NOT NULL,
    "CreadoEn" timestamp with time zone NOT NULL,
    "PublicadoEn" timestamp with time zone,
    CONSTRAINT "PK_MensajesOutbox" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_MensajesOutbox_Id_TenantId" UNIQUE ("Id", "TenantId"),
    CONSTRAINT "FK_MensajesOutbox_EventosDeEnvio_EventoId_TenantId" FOREIGN KEY ("EventoId", "TenantId") REFERENCES "EventosDeEnvio" ("Id", "TenantId") ON DELETE RESTRICT,
    CONSTRAINT "FK_MensajesOutbox_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_MensajesOutbox_CreadoEn_Id" ON "MensajesOutbox" ("CreadoEn", "Id") WHERE "PublicadoEn" IS NULL;

CREATE UNIQUE INDEX "IX_MensajesOutbox_EventoId" ON "MensajesOutbox" ("EventoId");

CREATE INDEX "IX_MensajesOutbox_EventoId_TenantId" ON "MensajesOutbox" ("EventoId", "TenantId");

CREATE INDEX "IX_MensajesOutbox_TenantId" ON "MensajesOutbox" ("TenantId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261027220546_TransactionalOutbox', '10.0.12');

COMMIT;

START TRANSACTION;
ALTER TABLE "Usuarios" ADD "ContrasenaHash" character varying(256);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261030191957_CredencialesLogin', '10.0.12');

COMMIT;

START TRANSACTION;
ALTER TABLE "ReglasOperativas" ADD "AccionPendientes" integer NOT NULL DEFAULT 0;

ALTER TABLE "ReglasOperativas" ADD "PermiteEntregaParcial" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "ReglasOperativas" ADD CONSTRAINT "CK_ReglasOperativas_AccionPendientes" CHECK ("AccionPendientes" IN (0, 1, 2));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261101185636_ReglasEntregaParcial', '10.0.12');

COMMIT;

START TRANSACTION;
ALTER TABLE "Bultos" ADD "Estado" integer NOT NULL DEFAULT 0;

UPDATE "Bultos" AS b
SET "Estado" = e."Estado"
FROM "Envios" AS e
WHERE b."EnvioId" = e."Id" AND b."TenantId" = e."TenantId";

ALTER TABLE "Bultos" ADD CONSTRAINT "CK_Bultos_Estado" CHECK ("Estado" BETWEEN 0 AND 9);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261101195520_EstadosPorBulto', '10.0.12');

COMMIT;

START TRANSACTION;
ALTER TABLE "IntentosEntrega" ADD "HuellaSolicitud" character varying(64);

ALTER TABLE "PruebasEntrega" ADD CONSTRAINT "AK_PruebasEntrega_Id_IntentoId" UNIQUE ("Id", "IntentoId");

ALTER TABLE "PruebasEntrega" ADD CONSTRAINT "AK_PruebasEntrega_Id_IntentoId_TenantId" UNIQUE ("Id", "IntentoId", "TenantId");

ALTER TABLE "IntentosEntrega" ADD CONSTRAINT "AK_IntentosEntrega_Id_EnvioId" UNIQUE ("Id", "EnvioId");

ALTER TABLE "IntentosEntrega" ADD CONSTRAINT "AK_IntentosEntrega_Id_EnvioId_TenantId" UNIQUE ("Id", "EnvioId", "TenantId");

ALTER TABLE "Bultos" ADD CONSTRAINT "AK_Bultos_Id_EnvioId" UNIQUE ("Id", "EnvioId");

ALTER TABLE "Bultos" ADD CONSTRAINT "AK_Bultos_Id_EnvioId_TenantId" UNIQUE ("Id", "EnvioId", "TenantId");

CREATE TABLE "DetallesIntentoBulto" (
    "Id" uuid NOT NULL,
    "IntentoId" uuid NOT NULL,
    "BultoId" uuid NOT NULL,
    "EnvioId" uuid NOT NULL,
    "Resultado" integer NOT NULL,
    "MotivoId" uuid,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_DetallesIntentoBulto" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_DetallesIntentoBulto_Id_IntentoId" UNIQUE ("Id", "IntentoId"),
    CONSTRAINT "AK_DetallesIntentoBulto_Id_IntentoId_TenantId" UNIQUE ("Id", "IntentoId", "TenantId"),
    CONSTRAINT "AK_DetallesIntentoBulto_Id_TenantId" UNIQUE ("Id", "TenantId"),
    CONSTRAINT "CK_DetalleIntento_ResultadoMotivo" CHECK (("Resultado" = 0 AND "MotivoId" IS NULL) OR ("Resultado" = 1 AND "MotivoId" IS NOT NULL)),
    CONSTRAINT "FK_DetallesIntentoBulto_Bultos_BultoId_EnvioId_TenantId" FOREIGN KEY ("BultoId", "EnvioId", "TenantId") REFERENCES "Bultos" ("Id", "EnvioId", "TenantId") ON DELETE RESTRICT,
    CONSTRAINT "FK_DetallesIntentoBulto_IntentosEntrega_IntentoId_EnvioId_Tena~" FOREIGN KEY ("IntentoId", "EnvioId", "TenantId") REFERENCES "IntentosEntrega" ("Id", "EnvioId", "TenantId") ON DELETE RESTRICT,
    CONSTRAINT "FK_DetallesIntentoBulto_MotivosNoEntrega_MotivoId_TenantId" FOREIGN KEY ("MotivoId", "TenantId") REFERENCES "MotivosNoEntrega" ("Id", "TenantId") ON DELETE RESTRICT,
    CONSTRAINT "FK_DetallesIntentoBulto_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "PruebasBulto" (
    "Id" uuid NOT NULL,
    "DetalleId" uuid NOT NULL,
    "IntentoId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PruebaId" uuid NOT NULL,
    CONSTRAINT "PK_PruebasBulto" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_PruebasBulto_Id_TenantId" UNIQUE ("Id", "TenantId"),
    CONSTRAINT "FK_PruebasBulto_DetallesIntentoBulto_DetalleId_IntentoId_Tenan~" FOREIGN KEY ("DetalleId", "IntentoId", "TenantId") REFERENCES "DetallesIntentoBulto" ("Id", "IntentoId", "TenantId") ON DELETE RESTRICT,
    CONSTRAINT "FK_PruebasBulto_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PruebasBulto_PruebasEntrega_PruebaId_IntentoId_TenantId" FOREIGN KEY ("PruebaId", "IntentoId", "TenantId") REFERENCES "PruebasEntrega" ("Id", "IntentoId", "TenantId") ON DELETE RESTRICT
);

CREATE INDEX "IX_DetallesIntentoBulto_BultoId_EnvioId_TenantId" ON "DetallesIntentoBulto" ("BultoId", "EnvioId", "TenantId");

CREATE UNIQUE INDEX "IX_DetallesIntentoBulto_IntentoId_BultoId" ON "DetallesIntentoBulto" ("IntentoId", "BultoId");

CREATE INDEX "IX_DetallesIntentoBulto_IntentoId_EnvioId_TenantId" ON "DetallesIntentoBulto" ("IntentoId", "EnvioId", "TenantId");

CREATE INDEX "IX_DetallesIntentoBulto_MotivoId_TenantId" ON "DetallesIntentoBulto" ("MotivoId", "TenantId");

CREATE INDEX "IX_DetallesIntentoBulto_TenantId" ON "DetallesIntentoBulto" ("TenantId");

CREATE INDEX "IX_PruebasBulto_DetalleId_IntentoId_TenantId" ON "PruebasBulto" ("DetalleId", "IntentoId", "TenantId");

CREATE UNIQUE INDEX "IX_PruebasBulto_DetalleId_PruebaId" ON "PruebasBulto" ("DetalleId", "PruebaId");

CREATE INDEX "IX_PruebasBulto_PruebaId_IntentoId_TenantId" ON "PruebasBulto" ("PruebaId", "IntentoId", "TenantId");

CREATE INDEX "IX_PruebasBulto_TenantId" ON "PruebasBulto" ("TenantId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261101201848_ResultadosEntregaPorBulto', '10.0.12');

COMMIT;

START TRANSACTION;
ALTER TABLE "Paradas" ADD CONSTRAINT "AK_Paradas_Id_EnvioId" UNIQUE ("Id", "EnvioId");

ALTER TABLE "Paradas" ADD CONSTRAINT "AK_Paradas_Id_EnvioId_TenantId" UNIQUE ("Id", "EnvioId", "TenantId");

CREATE TABLE "BultosParada" (
    "Id" uuid NOT NULL,
    "ParadaId" uuid NOT NULL,
    "BultoId" uuid NOT NULL,
    "EnvioId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PesoKg" numeric NOT NULL,
    "VolumenM3" numeric NOT NULL,
    CONSTRAINT "PK_BultosParada" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_BultosParada_Id_TenantId" UNIQUE ("Id", "TenantId"),
    CONSTRAINT "CK_BultosParada_Medidas" CHECK ("PesoKg" > 0 AND "VolumenM3" > 0),
    CONSTRAINT "FK_BultosParada_Bultos_BultoId_EnvioId_TenantId" FOREIGN KEY ("BultoId", "EnvioId", "TenantId") REFERENCES "Bultos" ("Id", "EnvioId", "TenantId") ON DELETE RESTRICT,
    CONSTRAINT "FK_BultosParada_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_BultosParada_Paradas_ParadaId_EnvioId_TenantId" FOREIGN KEY ("ParadaId", "EnvioId", "TenantId") REFERENCES "Paradas" ("Id", "EnvioId", "TenantId") ON DELETE RESTRICT
);

INSERT INTO "BultosParada" ("Id", "ParadaId", "BultoId", "EnvioId", "TenantId", "PesoKg", "VolumenM3")
SELECT gen_random_uuid(), p."Id", b."Id", p."EnvioId", p."TenantId", b."Peso",
       b."Alto" * b."Ancho" * b."Largo" / 1000000
FROM "Paradas" AS p
JOIN "Bultos" AS b ON b."EnvioId" = p."EnvioId" AND b."TenantId" = p."TenantId";

CREATE INDEX "IX_BultosParada_BultoId_EnvioId_TenantId" ON "BultosParada" ("BultoId", "EnvioId", "TenantId");

CREATE UNIQUE INDEX "IX_BultosParada_ParadaId_BultoId" ON "BultosParada" ("ParadaId", "BultoId");

CREATE INDEX "IX_BultosParada_ParadaId_EnvioId_TenantId" ON "BultosParada" ("ParadaId", "EnvioId", "TenantId");

CREATE INDEX "IX_BultosParada_TenantId" ON "BultosParada" ("TenantId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261103121131_BultosPorParada', '10.0.12');

COMMIT;

START TRANSACTION;
ALTER TABLE "ReglasOperativas" ADD "ImporteRecargoNuevaVisita" numeric;

ALTER TABLE "Paradas" ADD "RecargoPrevisto" numeric NOT NULL DEFAULT 0.0;

ALTER TABLE "Devoluciones" ADD CONSTRAINT "AK_Devoluciones_Id_EnvioId" UNIQUE ("Id", "EnvioId");

ALTER TABLE "Devoluciones" ADD CONSTRAINT "AK_Devoluciones_Id_EnvioId_TenantId" UNIQUE ("Id", "EnvioId", "TenantId");

CREATE TABLE "BultosDevolucion" (
    "Id" uuid NOT NULL,
    "DevolucionId" uuid NOT NULL,
    "BultoId" uuid NOT NULL,
    "EnvioId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_BultosDevolucion" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_BultosDevolucion_Id_TenantId" UNIQUE ("Id", "TenantId"),
    CONSTRAINT "FK_BultosDevolucion_Bultos_BultoId_EnvioId_TenantId" FOREIGN KEY ("BultoId", "EnvioId", "TenantId") REFERENCES "Bultos" ("Id", "EnvioId", "TenantId") ON DELETE RESTRICT,
    CONSTRAINT "FK_BultosDevolucion_Devoluciones_DevolucionId_EnvioId_TenantId" FOREIGN KEY ("DevolucionId", "EnvioId", "TenantId") REFERENCES "Devoluciones" ("Id", "EnvioId", "TenantId") ON DELETE RESTRICT,
    CONSTRAINT "FK_BultosDevolucion_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "RecargosVisita" (
    "Id" uuid NOT NULL,
    "EnvioId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ParadaId" uuid NOT NULL,
    "Importe" numeric NOT NULL,
    "RegistradoEn" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_RecargosVisita" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_RecargosVisita_Id_TenantId" UNIQUE ("Id", "TenantId"),
    CONSTRAINT "CK_RecargosVisita_Importe" CHECK ("Importe" > 0 AND round("Importe", 2) = "Importe"),
    CONSTRAINT "FK_RecargosVisita_Envios_EnvioId_TenantId" FOREIGN KEY ("EnvioId", "TenantId") REFERENCES "Envios" ("Id", "TenantId") ON DELETE RESTRICT,
    CONSTRAINT "FK_RecargosVisita_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RecargosVisita_Paradas_ParadaId_EnvioId_TenantId" FOREIGN KEY ("ParadaId", "EnvioId", "TenantId") REFERENCES "Paradas" ("Id", "EnvioId", "TenantId") ON DELETE RESTRICT
);

INSERT INTO "BultosDevolucion" ("Id", "DevolucionId", "BultoId", "EnvioId", "TenantId")
SELECT gen_random_uuid(), d."Id", b."Id", d."EnvioId", d."TenantId"
FROM "Devoluciones" AS d
JOIN "Bultos" AS b ON b."EnvioId" = d."EnvioId" AND b."TenantId" = d."TenantId";

ALTER TABLE "ReglasOperativas" ADD CONSTRAINT "CK_ReglasOperativas_ImporteRecargo" CHECK ("ImporteRecargoNuevaVisita" IS NULL OR ("AccionPendientes" = 1 AND "ImporteRecargoNuevaVisita" > 0 AND round("ImporteRecargoNuevaVisita", 2) = "ImporteRecargoNuevaVisita"));

ALTER TABLE "Paradas" ADD CONSTRAINT "CK_Paradas_RecargoPrevisto" CHECK ("RecargoPrevisto" >= 0 AND round("RecargoPrevisto", 2) = "RecargoPrevisto");

CREATE INDEX "IX_BultosDevolucion_BultoId_EnvioId_TenantId" ON "BultosDevolucion" ("BultoId", "EnvioId", "TenantId");

CREATE UNIQUE INDEX "IX_BultosDevolucion_DevolucionId_BultoId" ON "BultosDevolucion" ("DevolucionId", "BultoId");

CREATE INDEX "IX_BultosDevolucion_DevolucionId_EnvioId_TenantId" ON "BultosDevolucion" ("DevolucionId", "EnvioId", "TenantId");

CREATE INDEX "IX_BultosDevolucion_TenantId" ON "BultosDevolucion" ("TenantId");

CREATE INDEX "IX_RecargosVisita_EnvioId_TenantId" ON "RecargosVisita" ("EnvioId", "TenantId");

CREATE UNIQUE INDEX "IX_RecargosVisita_ParadaId" ON "RecargosVisita" ("ParadaId");

CREATE UNIQUE INDEX "IX_RecargosVisita_ParadaId_EnvioId_TenantId" ON "RecargosVisita" ("ParadaId", "EnvioId", "TenantId");

CREATE INDEX "IX_RecargosVisita_TenantId" ON "RecargosVisita" ("TenantId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261103123758_PoliticasPendientesYRecargoFijo', '10.0.12');
CREATE TABLE "MensajesProcesados" (
    "MensajeId" uuid NOT NULL,
    "Consumidor" character varying(100) NOT NULL,
    "ProcesadoEn" timestamp with time zone NOT NULL,
    "TenantId" uuid NOT NULL,
    CONSTRAINT "PK_MensajesProcesados" PRIMARY KEY ("MensajeId", "Consumidor"),
    CONSTRAINT "FK_MensajesProcesados_Operadores_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Operadores" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_MensajesProcesados_TenantId" ON "MensajesProcesados" ("TenantId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261105204809_MensajeProcesado', '10.0.12');

COMMIT;

