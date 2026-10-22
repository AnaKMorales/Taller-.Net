using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task HandlerCreaEnvioConElModeloCompletoYSuAmbitoAutenticado()
    {
        var e = await GuardarEscenario();
        var tenant = TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Administrador);
        Guid id;
        await using (var db = Scoped(tenant))
        {
            var handler = new CrearEnvioHandler(new AltaEnvioRepository(db), new RelojAlta());
            var envio = await handler.Handle(new CrearEnvioCommand
            {
                ComercioId = e.Envio.ComercioId,
                CuentaComercialId = e.Envio.CuentaComercialId,
                ZonaId = e.Zona.Id,
                ReglasId = e.Reglas.Id,
                CuadroTarifarioId = e.Envio.CuadroTarifarioId,
                FranjaHorariaId = e.Envio.FranjaHorariaId,
                DestinatarioNombre = "Nuevo destinatario",
                DestinatarioTelefono = "099000099",
                DestinatarioEmail = "nuevo@example.com",
                DireccionCalle = "Otra calle",
                DireccionNumero = "123",
                DireccionLocalidad = "Montevideo",
                DireccionCodigoPostal = "11000",
                ModalidadServicio = ModalidadServicio.Estandar,
                Bultos = [new("NUEVO", 1, 10, 10, 10)],
                Preparacion = new(150, e.Envio.FechaComprometida, e.Envio.Programacion.FechaEntrega,
                    e.Envio.Programacion.InicioLocal, e.Envio.Programacion.FinLocal)
            });
            Assert.Equal(e.Admin.Id, envio.Eventos.Single().ResponsableId);
            id = envio.Id;
        }
        await using var reader = Scoped(tenant);
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync(e => e.Id == id);
        Assert.Equal("Nuevo destinatario", actual.Destinatario.Nombre);
        Assert.Equal("Otra calle", actual.Destinatario.Direccion.Calle);
        Assert.Single(actual.Bultos);
        Assert.Single(actual.Eventos);
        Assert.Equal(EstadoEnvio.Admitido, actual.Estado);
        Assert.Equal(e.Envio.CuentaComercialId, actual.CuentaComercialId);
        Assert.NotEqual(Guid.Empty, reader.Entry(actual).Property<Guid>("RowVersion").CurrentValue);
    }

    private sealed class RelojAlta : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(EscenarioPersistencia.Fecha.AddMinutes(5));
    }
}
