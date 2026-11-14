using System.Text.Json;
using Microsoft.Playwright;

namespace UltimaMilla.E2E.Tests;

// Se saltean si no hay una Web levantada (E2E_BASE_URL), igual que los de PostgreSQL sin base.
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("E2E_BASE_URL")))
            Skip = "Definir E2E_BASE_URL con una Web en Development (ver tests/E2E/README.md).";
    }
}

public sealed record Cuentas(string Admin, string Comercio, string Contrasena,
    string SeguimientoParcial, string SeguimientoFallido);

// Un navegador y un juego de datos demo para todos los tests.
public sealed class Entorno : IAsyncLifetime
{
    public string Base { get; } = (Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "").TrimEnd('/');
    public IBrowser Navegador { get; private set; } = null!;
    public Cuentas Express { get; private set; } = null!;
    private IPlaywright? _playwright;

    public async Task InitializeAsync()
    {
        if (Base.Length == 0) return;
        _playwright = await Playwright.CreateAsync();
        Navegador = await _playwright.Chromium.LaunchAsync(new() { Headless = true });

        var api = await _playwright.APIRequest.NewContextAsync();
        var respuesta = await api.PostAsync($"{Base}/dev/datos-demo");
        if (!respuesta.Ok) throw new InvalidOperationException($"No se pudieron crear los datos demo: HTTP {respuesta.Status}");
        using var json = JsonDocument.Parse(await respuesta.TextAsync());
        var express = json.RootElement.GetProperty("operadores")[0];
        string Texto(string campo) => express.GetProperty(campo).GetString()!;
        Express = new Cuentas(Texto("emailAdministrador"), Texto("emailComercio"), Texto("contrasena"),
            Texto("seguimientoParcial"), Texto("seguimientoFallido"));
        await api.DisposeAsync();
    }

    public async Task DisposeAsync()
    {
        if (Navegador is not null) await Navegador.CloseAsync();
        _playwright?.Dispose();
    }

    public async Task<IPage> NuevaPaginaAsync()
    {
        var contexto = await Navegador.NewContextAsync(new() { BaseURL = Base, Locale = "es-UY" });
        return await contexto.NewPageAsync();
    }

    public static async Task IngresarAsync(IPage pagina, string email, string contrasena)
    {
        await pagina.GotoAsync("/login");
        await pagina.FillAsync("#Email", email);
        await pagina.FillAsync("#Contrasena", contrasena);
        await pagina.ClickAsync("button:has-text('Ingresar')");
        await pagina.WaitForURLAsync(url => !url.Contains("/login"));
    }
}

[CollectionDefinition("E2E")]
public sealed class ColeccionE2E : ICollectionFixture<Entorno>;
