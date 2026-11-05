using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UltimaMilla.Repartidor;
using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Dispositivo;
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

// DATOS DE PRUEBA hasta que Web exponga la API del repartidor (contrato en docs/adr/005-*.md).
// Para la API real: registrar acá una implementación de IApiRepartidor basada en HttpClient.
builder.Services.AddSingleton<ApiRepartidorFalsa>();
builder.Services.AddSingleton<IApiRepartidor>(sp => sp.GetRequiredService<ApiRepartidorFalsa>());

await builder.Build().RunAsync();
