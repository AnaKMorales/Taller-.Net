using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UltimaMilla.Repartidor;
using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Dispositivo;
using UltimaMilla.Repartidor.Servidor;
using UltimaMilla.Repartidor.Simulacion;
using UltimaMilla.Repartidor.Sincronizacion;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// En WebAssembly hay un solo usuario por pestaña: los singletons comparten la misma cola y estado de red.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Navegador>();
builder.Services.AddSingleton<IAlmacenLocal>(sp => sp.GetRequiredService<Navegador>());
builder.Services.AddSingleton<EstadoConexion>();
builder.Services.AddSingleton<DatosLocales>();
builder.Services.AddSingleton<ColaAcciones>();
builder.Services.AddSingleton<Sincronizador>();
builder.Services.AddSingleton<SincronizacionAutomatica>();

// Dirección de la API de Web (wwwroot/appsettings.json). No es un secreto: el acceso lo da el token.
var api = new Uri(builder.Configuration["ApiUrl"] ?? builder.HostEnvironment.BaseAddress);
builder.Services.AddSingleton(new HttpClient { BaseAddress = api });

// Login real; hoja de ruta e intentos siguen simulados hasta que Web exponga sus endpoints (ADR-005).
builder.Services.AddSingleton<ApiRepartidorFalsa>();
builder.Services.AddSingleton<IApiRepartidor, ApiRepartidorHttp>();

await builder.Build().RunAsync();
