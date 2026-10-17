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

