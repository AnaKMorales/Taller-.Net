using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace UltimaMilla.Web.Seguridad;

/// <summary>Cómo se sirven los archivos publicados de la PWA del repartidor (wwwroot/repartidor).</summary>
public static class StaticFilesPwa
{
    public static StaticFileOptions Opciones()
    {
        // Los .dat de ICU (globalización) de Blazor WebAssembly no tienen tipo conocido y, sin esto, darían 404.
        var tipos = new FileExtensionContentTypeProvider();
        tipos.Mappings[".dat"] = "application/octet-stream";

        return new StaticFileOptions
        {
            ContentTypeProvider = tipos,
            OnPrepareResponse = ctx =>
            {
                // El service worker y su manifiesto de versión se revalidan siempre: si el navegador los guarda,
                // los repartidores quedan con una versión vieja de la app.
                if (ctx.File.Name is "service-worker.js" or "service-worker-assets.js")
                    ctx.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache";
            },
        };
    }
}
