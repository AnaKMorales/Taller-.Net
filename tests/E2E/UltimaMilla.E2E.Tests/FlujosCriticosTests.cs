using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace UltimaMilla.E2E.Tests;

[Collection("E2E")]
public sealed class FlujosCriticosTests(Entorno entorno)
{
    [E2EFact]
    public async Task ElAdministradorPublicaUnaNuevaVersionDelTarifario()
    {
        var pagina = await entorno.NuevaPaginaAsync();
        await Entorno.IngresarAsync(pagina, entorno.Express.Admin, entorno.Express.Contrasena);

        await pagina.GotoAsync("/backoffice/configuracion/tarifas");
        await pagina.FillAsync("input[name='Lineas[0].Precio']", "155");
        await pagina.ClickAsync("button:has-text('Publicar nueva versión')");

        await Expect(pagina.Locator(".exito")).ToContainTextAsync("Se publicó la versión");
        await Expect(pagina.Locator("input[name='Lineas[0].Precio']")).ToHaveValueAsync("155");
    }

    [E2EFact]
    public async Task ElComercioDaDeAltaUnEnvioConLaTarifaCalculada()
    {
        var pagina = await entorno.NuevaPaginaAsync();
        await Entorno.IngresarAsync(pagina, entorno.Express.Comercio, entorno.Express.Contrasena);

        await pagina.GotoAsync("/portal/envios/nuevo");
        await pagina.FillAsync("input[name='Nombre']", "Destinataria E2E");
        await pagina.FillAsync("input[name='Telefono']", "099111222");
        await pagina.FillAsync("input[name='Email']", "e2e@example.com");
        await pagina.FillAsync("input[name='Calle']", "Av. Italia");
        await pagina.FillAsync("input[name='Numero']", "2500");
        await pagina.FillAsync("input[name='Localidad']", "Montevideo");
        await pagina.FillAsync("input[name='CodigoPostal']", "11000");
        await pagina.FillAsync("input[name='Bultos[0].Codigo']", "E2E-1");
        await pagina.FillAsync("input[name='Bultos[0].Peso']", "2,5");
        await pagina.FillAsync("input[name='Bultos[0].Alto']", "20");
        await pagina.FillAsync("input[name='Bultos[0].Ancho']", "30");
        await pagina.FillAsync("input[name='Bultos[0].Largo']", "40");

        await pagina.ClickAsync("button:has-text('Calcular tarifa y ver fechas')");
        await Expect(pagina.GetByText(new Regex(@"Tarifa: \$"))).ToBeVisibleAsync();

        await pagina.ClickAsync("button:has-text('Confirmar alta')");
        await Expect(pagina.Locator(".exito")).ToContainTextAsync("Envío creado");
    }

    [E2EFact]
    public async Task ElDepositoRecibeAsignaCargaYDespacha()
    {
        // El comercio da de alta un envío con un bulto
        var comercio = await entorno.NuevaPaginaAsync();
        await Entorno.IngresarAsync(comercio, entorno.Express.Comercio, entorno.Express.Contrasena);
        var bulto = $"DEP-{Guid.NewGuid():N}"[..12];
        await comercio.GotoAsync("/portal/envios/nuevo");
        foreach (var (campo, valor) in new[] { ("Nombre", "Depósito E2E"), ("Telefono", "099111222"), ("Email", "dep@example.com"),
                     ("Calle", "Rivera"), ("Numero", "100"), ("Localidad", "Montevideo"), ("CodigoPostal", "11000"),
                     ("Bultos[0].Codigo", bulto), ("Bultos[0].Peso", "1"), ("Bultos[0].Alto", "10"),
                     ("Bultos[0].Ancho", "10"), ("Bultos[0].Largo", "10") })
            await comercio.FillAsync($"input[name='{campo}']", valor);
        await comercio.ClickAsync("button:has-text('Calcular tarifa y ver fechas')");
        await comercio.ClickAsync("button:has-text('Confirmar alta')");
        var codigo = await comercio.Locator(".exito code").InnerTextAsync();

        var admin = await entorno.NuevaPaginaAsync();
        await Entorno.IngresarAsync(admin, entorno.Express.Admin, entorno.Express.Contrasena);

        // Recepción
        await admin.GotoAsync("/backoffice/deposito");
        await admin.Locator($"tr:has-text('{codigo}')").GetByText("Recibir").ClickAsync();
        await admin.FillAsync("input[name='Codigo']", bulto);
        await admin.ClickAsync("button:has-text('Registrar bulto')");
        await Expect(admin.Locator(".exito")).ToContainTextAsync("1 de 1");
        await admin.ClickAsync("button:has-text('Confirmar ingreso al depósito')");
        await Expect(admin.Locator(".exito")).ToContainTextAsync("ingresado al depósito");

        // Ruta nueva y asignación
        await admin.ClickAsync("button:has-text('Crear ruta')");
        await Expect(admin.Locator(".exito")).ToContainTextAsync("Ruta creada");
        var fila = admin.Locator($"tr:has-text('{codigo}')");
        var opcion = fila.Locator("select[name='Ruta'] option", new() { HasTextString = "(0/" }).First;
        var ruta = (await opcion.GetAttributeAsync("value"))!.Split('|')[0];
        await fila.Locator("select[name='Ruta']").SelectOptionAsync(await opcion.GetAttributeAsync("value"));
        await fila.GetByText("Asignar").ClickAsync();
        await Expect(admin.Locator(".exito")).ToContainTextAsync("asignado");

        // Carga y despacho
        await admin.GotoAsync($"/backoffice/deposito/rutas/{ruta}");
        await admin.FillAsync("input[name='Codigo']", bulto);
        await admin.ClickAsync("button:has-text('Registrar carga')");
        await Expect(admin.Locator(".exito")).ToContainTextAsync("1 de 1");
        await admin.ClickAsync("button:has-text('Despachar ruta')");
        await Expect(admin.Locator(".exito")).ToContainTextAsync("Ruta despachada");
    }

    [E2EFact]
    public async Task ElComercioImportaUnArchivoSinDuplicarAlReimportar()
    {
        var pagina = await entorno.NuevaPaginaAsync();
        await Entorno.IngresarAsync(pagina, entorno.Express.Comercio, entorno.Express.Contrasena);
        var referencia = $"E2E-{Guid.NewGuid():N}"[..16];
        var fecha = DateTime.Today.AddDays(3).ToString("dd/MM/yyyy");
        var csv = "referencia;zona;modalidad;destinatario;telefono;email;calle;numero;localidad;codigo_postal;" +
            "fecha_entrega;franja;bulto;peso_kg;alto_cm;ancho_cm;largo_cm\n" +
            $"{referencia};Centro;Estandar;Ana E2E;099111222;e2e@example.com;Av. Italia;2500;Montevideo;11000;{fecha};Tarde;{referencia}-1;2,5;20;30;40\n" +
            $"{referencia};;;;;;;;;;;;{referencia}-2;1;10;10;10\n";
        var archivo = new FilePayload { Name = "envios.csv", MimeType = "text/csv", Buffer = System.Text.Encoding.UTF8.GetBytes(csv) };

        await pagina.GotoAsync("/portal/envios/importar");
        await pagina.SetInputFilesAsync("input[name='archivo']", archivo);
        await pagina.ClickAsync("button:has-text('Importar')");
        await Expect(pagina.Locator("tbody tr")).ToContainTextAsync("Creado");

        await pagina.SetInputFilesAsync("input[name='archivo']", archivo);
        await pagina.ClickAsync("button:has-text('Importar')");
        await Expect(pagina.Locator("tbody tr")).ToContainTextAsync("Ya importado");
    }

    [E2EFact]
    public async Task ElComercioImprimeUnaEtiquetaPorBulto()
    {
        var pagina = await entorno.NuevaPaginaAsync();
        await Entorno.IngresarAsync(pagina, entorno.Express.Comercio, entorno.Express.Contrasena);

        await pagina.GotoAsync($"/portal/envios/{entorno.Express.SeguimientoParcial}/etiqueta");

        await Expect(pagina.Locator(".etiqueta")).ToHaveCountAsync(3);
        await Expect(pagina.Locator(".etiqueta").Last).ToContainTextAsync("3 de 3");
    }

    [E2EFact]
    public async Task ElDestinatarioVeElAvanceDeUnaEntregaParcial()
    {
        var pagina = await entorno.NuevaPaginaAsync();
        await pagina.GotoAsync($"/seguimiento/{entorno.Express.SeguimientoParcial}");

        await Expect(pagina.Locator(".estado")).ToContainTextAsync("Entregado en parte");
        await Expect(pagina.GetByText("2 de 3 bultos entregados")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task ElDestinatarioReprogramaTrasUnIntentoFallido()
    {
        var pagina = await entorno.NuevaPaginaAsync();
        await pagina.GotoAsync($"/seguimiento/{entorno.Express.SeguimientoFallido}");

        await pagina.ClickAsync("text=¿No vas a estar? Elegí otro día");
        await pagina.ClickAsync("button:has-text('Reprogramar')");

        await Expect(pagina.GetByText("Listo, reprogramaste la entrega.")).ToBeVisibleAsync();
        await Expect(pagina.Locator(".estado").First).ToContainTextAsync("Entrega reprogramada");
    }

    [E2EFact]
    public async Task UnTokenInexistenteNoRevelaNada()
    {
        var pagina = await entorno.NuevaPaginaAsync();
        var respuesta = await pagina.GotoAsync($"/seguimiento/{Guid.NewGuid():N}");

        Assert.Equal(404, respuesta!.Status);
        await Expect(pagina.GetByText("No encontramos el envío")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task ElBackofficeMuestraElDetallePorBultoDeLaEntregaParcial()
    {
        var pagina = await entorno.NuevaPaginaAsync();
        await Entorno.IngresarAsync(pagina, entorno.Express.Admin, entorno.Express.Contrasena);

        // El tablero enlaza los envíos de los últimos cambios; se busca el de la entrega parcial.
        await pagina.GotoAsync("/backoffice/tablero");
        var enlaces = await pagina.Locator("a[href^='/backoffice/envios/']")
            .EvaluateAllAsync<string[]>("els => [...new Set(els.map(e => e.getAttribute('href')))]");
        var encontrado = false;
        foreach (var enlace in enlaces)
        {
            await pagina.GotoAsync(enlace);
            if (await pagina.Locator("code", new() { HasText = entorno.Express.SeguimientoParcial }).CountAsync() == 0) continue;
            encontrado = true;
            await Expect(pagina.Locator(".estado-grande")).ToHaveTextAsync("Entregado parcialmente");
            await Expect(pagina.GetByText("No entregado (pendiente)")).ToBeVisibleAsync();
            await Expect(pagina.GetByText("Bultos pendientes de la entrega parcial")).ToBeVisibleAsync();
            break;
        }
        Assert.True(encontrado, "El envío parcial no aparece en el tablero.");
    }

    [E2EFact]
    public async Task UnComercioNoEntraAlBackoffice()
    {
        var pagina = await entorno.NuevaPaginaAsync();
        await Entorno.IngresarAsync(pagina, entorno.Express.Comercio, entorno.Express.Contrasena);

        await pagina.GotoAsync("/backoffice/configuracion/tarifas");
        await Expect(pagina).ToHaveURLAsync(new Regex("/acceso-denegado"));
    }
}
