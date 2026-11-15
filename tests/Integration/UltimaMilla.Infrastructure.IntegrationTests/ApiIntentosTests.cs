using System.Net;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Envios.RegistrarIntento;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Infrastructure.Persistence.Outbox;
using UltimaMilla.Infrastructure.Persistence.Tenancy;
using UltimaMilla.Web.Desarrollo;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private const string RutaIntentosApi = "/api/repartidor/intentos";
    private const string ContrasenaApiIntentos = "Prueba-local123!";
    private const string FirmaPng = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a7o0AAAAASUVORK5CYII=";

    [PostgresFact]
    public async Task DatosDemoPwaPermiteDescargarUnaVisitaParcialPendienteConBearerReal()
    {
        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false, ambiente: "Development");
        using var cliente = servidor.Cliente();
        using var respuesta = await cliente.PostAsync("/dev/datos-demo-pwa", null);
        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync());
        var demo = (await respuesta.Content.ReadFromJsonAsync<DatosDemoCreados>())!;
        using var login = await cliente.PostAsJsonAsync("/api/repartidor/sesion",
            new SolicitudLoginRepartidor(demo.EmailRepartidor, demo.Contrasena));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var rutas = await cliente.GetFromJsonAsync<JsonObject[]>("/api/repartidor/rutas");
        var rutaId = Assert.Single(rutas!)["rutaId"]!.GetValue<Guid>();
        var hoja = (await cliente.GetFromJsonAsync<HojaRutaDto>($"/api/repartidor/rutas/{rutaId}", JsonApi))!;
        var parada = Assert.Single(hoja.Paradas);
        Assert.Equal(EstadoEnvio.EnTransito, parada.EstadoEnvio);
        Assert.True(parada.Reglas.PermiteEntregaParcial);
        Assert.Equal(new[] { "PARCIAL-01", "PARCIAL-02" }, parada.Bultos.Select(b => b.Codigo));

        await using var db = Context(demo.OperadorId);
        var avisos = await db.Set<EntregaAviso>().OrderBy(a => a.Exitoso).ToListAsync();
        Assert.Collection(avisos, aviso => Assert.False(aviso.Exitoso), aviso => Assert.True(aviso.Exitoso));
    }

    [PostgresFact]
    public async Task ApiIntentosRegistraParcialRepiteYTraduceConflictosConBearerReal()
    {
        var e = await PrepararVisitaParcial();
        await AsignarContrasenaIntentosAsync(e.Operador.Id, e.Repartidor.Id);
        var comando = await ComandoRegistro(e);
        var prueba = comando.Evidencias[0];
        var firma = new EvidenciaIntentoCommand(Guid.NewGuid(), TipoPrueba.Firma, prueba.Hora, prueba.Posicion, Archivo: FirmaPng);
        comando = comando with
        {
            Evidencias = [.. comando.Evidencias, firma],
            Bultos = comando.Bultos.Select(bulto => bulto.Resultado == ResultadoIntento.Entregado
                ? bulto with { Evidencias = [.. bulto.Evidencias, firma.Referencia] }
                : bulto).ToArray()
        };
        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false);
        using var cliente = servidor.Cliente();
        cliente.DefaultRequestHeaders.Add("Origin", "https://repartidor.example");
        const string traceId = "0123456789abcdef0123456789abcdef";
        cliente.DefaultRequestHeaders.Add("traceparent", $"00-{traceId}-0123456789abcdef-01");
        cliente.DefaultRequestHeaders.Add("tracestate", "prueba=api");
        await IngresarIntentosAsync(cliente, e.Repartidor.Email);

        using var obsoleta = await cliente.PostAsJsonAsync(RutaIntentosApi,
            comando with { VersionEsperada = Guid.NewGuid() }, JsonApi);
        Assert.Equal(HttpStatusCode.Conflict, obsoleta.StatusCode);
        var activa = (await obsoleta.Content.ReadFromJsonAsync<JsonObject>())!["conflicto"]!;
        Assert.True(activa["paradaActiva"]!.GetValue<bool>());
        Assert.All(activa["bultos"]!.AsArray(), b => Assert.Equal("SinCambioDeEstado", b!["motivo"]!.GetValue<string>()));

        using var nueva = await cliente.PostAsJsonAsync(RutaIntentosApi, comando, JsonApi);
        Assert.Equal(HttpStatusCode.OK, nueva.StatusCode);
        Assert.Equal("https://repartidor.example", Assert.Single(nueva.Headers.GetValues("Access-Control-Allow-Origin")));
        var registrada = (await nueva.Content.ReadFromJsonAsync<IntentoRegistradoDto>(JsonApi))!;
        Assert.False(registrada.Repetido);
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, registrada.EstadoActualEnvio);
        Assert.Equal(3, registrada.Bultos.Count);
        Assert.NotEqual(comando.VersionEsperada, registrada.VersionActualEnvio);

        using var reenvio = await cliente.PostAsJsonAsync(RutaIntentosApi, comando, JsonApi);
        Assert.Equal(HttpStatusCode.OK, reenvio.StatusCode);
        var repetida = (await reenvio.Content.ReadFromJsonAsync<IntentoRegistradoDto>(JsonApi))!;
        Assert.True(repetida.Repetido);
        Assert.Equal(registrada.IntentoId, repetida.IntentoId);
        Assert.Equal(registrada.VersionActualEnvio, repetida.VersionActualEnvio);

        var bultoFueraDeParada = Guid.NewGuid();
        using var atrasada = await cliente.PostAsJsonAsync(RutaIntentosApi, comando with
        {
            IdOperacion = Guid.NewGuid(),
            Bultos = [.. comando.Bultos, new(bultoFueraDeParada, ResultadoIntento.Fallido, null, [])]
        }, JsonApi);
        Assert.Equal(HttpStatusCode.Conflict, atrasada.StatusCode);
        var cerrada = (await atrasada.Content.ReadFromJsonAsync<JsonObject>())!["conflicto"]!;
        Assert.False(cerrada["paradaActiva"]!.GetValue<bool>());
        Assert.Equal(comando.ParadaId, cerrada["paradaId"]!.GetValue<Guid>());
        Assert.Equal(registrada.VersionActualEnvio, cerrada["versionActualEnvio"]!.GetValue<Guid>());
        var bultos = cerrada["bultos"]!.AsArray();
        Assert.Equal(comando.Bultos.Count + 1, bultos.Count);
        foreach (var resultado in comando.Bultos)
        {
            var bulto = Assert.Single(bultos, b => b!["bultoId"]!.GetValue<Guid>() == resultado.BultoId)!;
            Assert.Equal("EstadoCambio", bulto["motivo"]!.GetValue<string>());
            Assert.Equal(resultado.Resultado == ResultadoIntento.Entregado ? "Entregado" : "NoEntregado",
                bulto["estadoActual"]!.GetValue<string>());
        }
        var ajeno = Assert.Single(bultos, b => b!["bultoId"]!.GetValue<Guid>() == bultoFueraDeParada)!;
        Assert.Equal("FueraDeParada", ajeno["motivo"]!.GetValue<string>());
        Assert.Null(ajeno["codigo"]);
        Assert.Null(ajeno["estadoActual"]);

        var distinto = comando with { OcurridoEn = comando.OcurridoEn.AddSeconds(1) };
        using var reutilizado = await cliente.PostAsJsonAsync(RutaIntentosApi, distinto, JsonApi);
        Assert.Equal(HttpStatusCode.Conflict, reutilizado.StatusCode);

        await using var lectura = Context(e.Operador.Id);
        Assert.Single(await lectura.Set<IntentoEntrega>().ToListAsync());
        var pruebas = await lectura.Set<PruebaEntrega>().Where(p => p.TipoPrueba == TipoPrueba.Firma).ToListAsync();
        Assert.Equal(FirmaPng, Assert.Single(pruebas).ArchivoRef!.Ubicacion);
        Assert.Equal(e.Repartidor.Id, (await lectura.Set<EventoDeEnvio>().Include(x => x.Responsable)
            .SingleAsync(x => x.IdOperacion == comando.IdOperacion)).ResponsableId);
        var outbox = Assert.Single(await lectura.Set<MensajeOutbox>().Where(m => m.Tipo == "envio.evento.v2").ToListAsync());
        Assert.True(ActivityContext.TryParse(outbox.TraceParent, outbox.TraceState, out var contexto));
        Assert.Equal(traceId, contexto.TraceId.ToString());
        Assert.Equal("prueba=api", outbox.TraceState);
    }

    [PostgresFact]
    public async Task ApiIntentosRechazaEnvioAjenoCredencialesInvalidasYDatosIncompletos()
    {
        var e = await PrepararVisitaParcial();
        await AsignarContrasenaIntentosAsync(e.Operador.Id, e.Repartidor.Id);
        var comando = await ComandoRegistro(e);
        var otroEmail = $"otro-{Guid.NewGuid():N}@example.test";
        await using (var db = Context(e.Operador.Id))
        {
            var otro = new Repartidor(await db.Set<Operador>().SingleAsync(), otroEmail);
            db.Add(otro);
            await FabricaCuentasAcceso.Predeterminada.CrearAsync(db, otro, ContrasenaApiIntentos);
            await db.SaveChangesAsync();
        }

        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false);
        using var cliente = servidor.Cliente();
        using var anonima = await cliente.PostAsJsonAsync(RutaIntentosApi, comando, JsonApi);
        Assert.Equal(HttpStatusCode.Unauthorized, anonima.StatusCode);

        await IngresarIntentosAsync(cliente, otroEmail);
        using var ajena = await cliente.PostAsJsonAsync(RutaIntentosApi, comando, JsonApi);
        Assert.Equal(HttpStatusCode.Forbidden, ajena.StatusCode);

        cliente.DefaultRequestHeaders.Authorization = null;
        await IngresarIntentosAsync(cliente, e.Repartidor.Email);
        var coordenadaInvalida = JsonSerializer.SerializeToNode(comando, JsonApi)!;
        coordenadaInvalida["evidencias"]![0]!["posicion"]!["latitud"] = 91;
        using var invalida = await cliente.PostAsJsonAsync(RutaIntentosApi, coordenadaInvalida, JsonApi);
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);

        var sinResultado = JsonSerializer.SerializeToNode(comando, JsonApi)!;
        Assert.True(sinResultado["bultos"]![0]!.AsObject().Remove("resultado"));
        using var incompleta = await cliente.PostAsJsonAsync(RutaIntentosApi, sinResultado, JsonApi);
        Assert.Equal(HttpStatusCode.BadRequest, incompleta.StatusCode);

        var archivoInvalido = JsonSerializer.SerializeToNode(comando, JsonApi)!;
        archivoInvalido["evidencias"]![0]!["tipo"] = "Foto";
        archivoInvalido["evidencias"]![0]!["archivo"] = "data:image/jpeg;base64,no-es-base64";
        using var imagenInvalida = await cliente.PostAsJsonAsync(RutaIntentosApi, archivoInvalido, JsonApi);
        Assert.Equal(HttpStatusCode.BadRequest, imagenInvalida.StatusCode);

        await using var lectura = Context(e.Operador.Id);
        Assert.Empty(await lectura.Set<IntentoEntrega>().ToListAsync());
        Assert.Empty(await lectura.Set<MensajeOutbox>().Where(m => m.Tipo == "envio.evento.v2").ToListAsync());
    }

    private async Task AsignarContrasenaIntentosAsync(Guid operadorId, Guid repartidorId)
    {
        await using var db = Context(operadorId);
        var repartidor = await db.Set<Repartidor>().SingleAsync(r => r.Id == repartidorId);
        await FabricaCuentasAcceso.Predeterminada.CrearAsync(db, repartidor, ContrasenaApiIntentos);
        await db.SaveChangesAsync();
    }

    private static async Task IngresarIntentosAsync(HttpClient cliente, string email)
    {
        using var login = await cliente.PostAsJsonAsync("/api/repartidor/sesion",
            new SolicitudLoginRepartidor(email, ContrasenaApiIntentos));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
