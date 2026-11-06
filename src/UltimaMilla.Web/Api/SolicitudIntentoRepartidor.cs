using System.Text.Json.Serialization;
using UltimaMilla.Application.Envios.RegistrarIntento;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Web.Api;

internal sealed record SolicitudIntentoRepartidor(Guid EnvioId, Guid ParadaId, Guid VersionEsperada,
    Guid IdOperacion, DateTime OcurridoEn, IReadOnlyList<BultoIntentoSolicitud?>? Bultos,
    IReadOnlyList<EvidenciaIntentoSolicitud?>? Evidencias, PosicionIntentoSolicitud? Posicion = null)
{
    internal RegistrarIntentoCommand AComando()
    {
        if (Bultos is null || Evidencias is null)
            throw new ArgumentException("Se requieren resultados por bulto y una lista de evidencias.");

        var bultos = Bultos.Select(bulto => bulto?.AComando()
            ?? throw new ArgumentException("Los resultados por bulto no pueden ser nulos.")).ToArray();
        var evidencias = Evidencias.Select(evidencia => evidencia?.AComando()
            ?? throw new ArgumentException("Las evidencias no pueden ser nulas.")).ToArray();
        if (evidencias.Where(e => EvidenciaIntentoSolicitud.EsImagenIncluida(e.Archivo))
            .Sum(e => (long)e.Archivo!.Length) > 4 * 1024 * 1024)
            throw new ArgumentException("Las fotos y firmas de la visita superan el tamaño máximo.");
        return new(EnvioId, ParadaId, VersionEsperada, IdOperacion, OcurridoEn, bultos, evidencias, Posicion?.ADominio());
    }
}

internal sealed record BultoIntentoSolicitud(Guid BultoId, [property: JsonRequired] ResultadoIntento Resultado,
    Guid? MotivoId, IReadOnlyList<Guid> Evidencias)
{
    internal ResultadoBultoCommand AComando() => new(BultoId, Resultado, MotivoId, Evidencias);
}

internal sealed record EvidenciaIntentoSolicitud(Guid Referencia, [property: JsonRequired] TipoPrueba Tipo,
    DateTime Hora, PosicionIntentoSolicitud? Posicion, string? Archivo = null, string? Texto = null)
{
    private const int MaxImagenBytes = 256 * 1024;
    private const string Jpeg = "data:image/jpeg;base64,";
    private const string Png = "data:image/png;base64,";

    internal EvidenciaIntentoCommand AComando()
    {
        if (Archivo is not null && EsImagenIncluida(Archivo))
            ValidarImagenIncluida(Archivo);
        return new(Referencia, Tipo, Hora, Posicion?.ADominio(), Archivo, Texto);
    }

    internal static bool EsImagenIncluida(string? archivo) =>
        archivo?.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase) == true;

    private void ValidarImagenIncluida(string archivo)
    {
        var prefijo = Tipo switch
        {
            TipoPrueba.Foto => Jpeg,
            TipoPrueba.Firma => Png,
            _ => ""
        };
        if (prefijo.Length == 0 || !archivo.StartsWith(prefijo, StringComparison.Ordinal) ||
            archivo.Length - prefijo.Length > (MaxImagenBytes + 2) / 3 * 4)
            throw new ArgumentException("Se espera una foto JPEG o firma PNG de hasta 256 KiB.");

        byte[] imagen;
        try
        {
            imagen = Convert.FromBase64String(archivo[prefijo.Length..]);
        }
        catch (FormatException)
        {
            throw new ArgumentException("La imagen de evidencia no es válida.");
        }

        ReadOnlySpan<byte> encabezado = Tipo == TipoPrueba.Foto
            ? [0xff, 0xd8, 0xff]
            : [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        if (imagen.Length > MaxImagenBytes || !imagen.AsSpan().StartsWith(encabezado))
            throw new ArgumentException("La imagen de evidencia no es válida o supera 256 KiB.");
    }
}

// Construir GeoPunto dentro del endpoint permite devolver 400 si la coordenada viola sus límites.
internal sealed record PosicionIntentoSolicitud([property: JsonRequired] decimal Latitud,
    [property: JsonRequired] decimal Longitud)
{
    internal GeoPunto ADominio() => new(Latitud, Longitud);
}
