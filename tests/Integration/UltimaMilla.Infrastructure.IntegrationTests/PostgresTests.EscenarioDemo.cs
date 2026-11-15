using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Web.Desarrollo;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task DatosDemoCreaDosOperadoresConConfiguracionDistintaYSuHojaDeRuta()
    {
        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false, ambiente: "Development");
        using var cliente = servidor.Cliente();
        using var respuesta = await cliente.PostAsync("/dev/datos-demo", null);
        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync());
        var escenario = (await respuesta.Content.ReadFromJsonAsync<EscenarioDemoCreado>())!;

        var (a, b) = (escenario.Operadores[0], escenario.Operadores[1]);
        Assert.Equal(2, escenario.Operadores.Count);
        Assert.NotEqual(a.OperadorId, b.OperadorId);
        Assert.All(escenario.Operadores, c => Assert.Equal("Demo1234!", c.Contrasena));

        var configuracion = new List<(int Intentos, AccionBultosPendientes Accion, string[] Zonas, string[] Motivos, string[] Pruebas)>();
        foreach (var cuenta in escenario.Operadores)
        {
            await using var db = Context(cuenta.OperadorId);

            // Configuración propia de cada operador (los filtros por tenant dejan ver solo lo suyo).
            var reglas = await db.Set<ReglasOperativas>().Include(r => r.MotivosNoEntrega).SingleAsync();
            Assert.True(reglas.PermiteEntregaParcial);
            var zonas = await db.Set<ZonaCobertura>().Select(z => z.Nombre).OrderBy(n => n).ToArrayAsync();
            Assert.Equal(2, zonas.Length);
            Assert.Equal(1, await db.Set<CuadroTarifario>().CountAsync());
            configuracion.Add((reglas.MaxIntentosEntrega, reglas.AccionPendientes, zonas,
                reglas.MotivosNoEntrega.Select(m => m.Nombre).Order().ToArray(),
                reglas.TiposPrueba.Select(t => t.ToString()).Order().ToArray()));

            // Un repartidor con una hoja de ruta despachada con cuatro paradas.
            var ruta = await db.Set<Ruta>().Include(r => r.Paradas).ThenInclude(p => p.Envio).ThenInclude(e => e.Intentos)
                .ThenInclude(i => i.Detalles).SingleAsync();
            Assert.NotNull(ruta.DespachadaEn);
            Assert.Equal(4, ruta.Paradas.Count);
            var envios = ruta.Paradas.Select(p => p.Envio).ToList();

            // Visita pendiente: en tránsito y sin intentos.
            var pendiente = Assert.Single(envios, e => e.Estado == EstadoEnvio.EnTransito);
            Assert.Empty(pendiente.Intentos);
            Assert.Equal(pendiente.CodigoSeguimiento, cuenta.SeguimientoPendiente);

            // Visita con entrega parcial por bulto: un intento Parcial con bultos entregados y no entregados.
            var parcial = Assert.Single(envios, e => e.Estado == EstadoEnvio.EntregadoParcialmente);
            var intento = Assert.Single(parcial.Intentos);
            Assert.Equal(ResultadoIntento.Parcial, intento.Resultado);
            Assert.Contains(intento.Detalles, d => d.Resultado == ResultadoIntento.Entregado);
            Assert.Contains(intento.Detalles, d => d.Resultado == ResultadoIntento.Fallido);
            Assert.Equal(parcial.CodigoSeguimiento, cuenta.SeguimientoParcial);

            // Envío no entregado con un aviso fallido, más un envío entregado.
            var fallido = Assert.Single(envios, e => e.Estado == EstadoEnvio.NoEntregado);
            Assert.Equal(fallido.CodigoSeguimiento, cuenta.SeguimientoFallido);
            Assert.Contains(envios, e => e.Estado == EstadoEnvio.Entregado);
            var avisos = await db.Set<EntregaAviso>().Include(x => x.Evento).ToListAsync();
            var avisoFallido = Assert.Single(avisos, x => !x.Exitoso);
            Assert.Equal(fallido.Id, avisoFallido.Evento.EnvioId);
            Assert.True(avisoFallido.Intentos >= 2);
        }

        // Los dos operadores se tratan distinto.
        Assert.NotEqual(configuracion[0].Intentos, configuracion[1].Intentos);
        Assert.NotEqual(configuracion[0].Accion, configuracion[1].Accion);
        Assert.Empty(configuracion[0].Zonas.Intersect(configuracion[1].Zonas));
        Assert.Empty(configuracion[0].Motivos.Intersect(configuracion[1].Motivos));
        Assert.NotEqual(configuracion[0].Pruebas, configuracion[1].Pruebas);
    }

    [PostgresFact]
    public async Task DatosDemoConContrasenaPropiaLaUsaNoLaDevuelveYRechazaUnaCorta()
    {
        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false, ambiente: "Development");
        using var cliente = servidor.Cliente();
        const string clave = "x7Kp2mQw9Zr4TbN8vLc1";

        using var corta = await cliente.PostAsJsonAsync("/dev/datos-demo", new SolicitudDatosDemo("corta"));
        Assert.Equal(HttpStatusCode.BadRequest, corta.StatusCode);

        using var respuesta = await cliente.PostAsJsonAsync("/dev/datos-demo", new SolicitudDatosDemo(clave));
        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync());
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(clave, texto); // el servidor no devuelve la clave que recibió
        Assert.DoesNotContain("Demo1234!", texto);
        var escenario = System.Text.Json.JsonSerializer.Deserialize<EscenarioDemoCreado>(texto,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.All(escenario.Operadores, c => Assert.Null(c.Contrasena));

        foreach (var cuenta in escenario.Operadores)
        {
            using var conLaPropia = await cliente.PostAsJsonAsync("/api/repartidor/sesion",
                new SolicitudLoginRepartidor(cuenta.EmailRepartidor, clave));
            Assert.True(conLaPropia.IsSuccessStatusCode);
            using var conLaDeDesarrollo = await cliente.PostAsJsonAsync("/api/repartidor/sesion",
                new SolicitudLoginRepartidor(cuenta.EmailRepartidor, "Demo1234!"));
            Assert.False(conLaDeDesarrollo.IsSuccessStatusCode);
        }
    }
}
