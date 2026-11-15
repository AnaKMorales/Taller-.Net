using UltimaMilla.Repartidor.Contratos;

namespace UltimaMilla.Repartidor.Dispositivo;

// Lógica pura del escaneo: qué bulto de la visita corresponde a un código leído (cámara, lector o teclado) y qué
// decirle al repartidor si la cámara no arranca. Sin JS, para poder probarla.
public static class LecturaCodigo
{
    // Los lectores suelen agregar espacios o un salto de línea al final; el código del bulto se compara exacto.
    public static BultoParadaDto? Buscar(IEnumerable<BultoParadaDto> bultos, string? leido)
    {
        ArgumentNullException.ThrowIfNull(bultos);
        var codigo = leido?.Trim();
        return string.IsNullOrEmpty(codigo) ? null : bultos.FirstOrDefault(b => string.Equals(b.Codigo, codigo, StringComparison.Ordinal));
    }

    public static string MotivoSinCamara(string? motivo) => motivo switch
    {
        "permiso" => "No hay permiso para usar la cámara. Habilitalo en el navegador o ingresá el código.",
        "no-disponible" => "Este navegador no puede leer códigos con la cámara. Usá un lector o ingresá el código.",
        _ => "No se pudo abrir la cámara. Ingresá el código a mano."
    };
}
