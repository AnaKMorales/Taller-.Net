using UltimaMilla.Application.Backoffice;
using UltimaMilla.Infrastructure.Persistence.Backoffice;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task ElDetalleMuestraElEstadoDeCadaBultoYElResultadoPorBultoDelIntento()
    {
        await PrepararVisitaParcial();
        Guid envioId;
        await using (var db = Context())
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            envioId = envio.Id;
            envio.RegistrarIntentoPorBulto(envio.Parada!.Id, SolicitudParcial(envio), await db.Set<Repartidor>().SingleAsync(),
                EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid());
            await db.SaveChangesAsync();
        }

        await using (var db = Context())
        {
            var detalle = await new DetalleEnvio(db).ObtenerAsync(envioId);
            Assert.NotNull(detalle);
            Assert.Equal(EstadoEnvio.EntregadoParcialmente, detalle.Estado);
            Assert.Equal([EstadoBulto.Entregado, EstadoBulto.Entregado, EstadoBulto.NoEntregado],
                detalle.Bultos.Select(b => b.Estado));

            var intento = Assert.Single(detalle.Intentos);
            Assert.Equal(3, intento.Bultos.Count);
            var fallido = Assert.Single(intento.Bultos, b => b.Resultado == ResultadoIntento.Fallido);
            Assert.Equal(detalle.Bultos[2].Codigo, fallido.Codigo);
            Assert.NotNull(fallido.Motivo);
            Assert.All(intento.Bultos, b => Assert.Equal(1, b.Evidencias));

            // Sin otra visita ni devolución, la pantalla ofrece gestionar el bulto pendiente.
            Assert.True(detalle.AdmiteGestionDePendientes);
            Assert.Equal(detalle.Bultos[2].Codigo, Assert.Single(detalle.Pendientes).Codigo);
            Assert.Equal(EstadoEnvio.EntregadoParcialmente, detalle.Eventos[0].EstadoNuevo); // el más reciente primero
        }

        // Otro operador no encuentra el envío, ni siquiera conociendo su id.
        await using (var otro = Context(Guid.NewGuid()))
            Assert.Null(await new DetalleEnvio(otro).ObtenerAsync(envioId));
    }
}
