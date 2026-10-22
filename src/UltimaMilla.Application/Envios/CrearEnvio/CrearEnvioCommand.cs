using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.CrearEnvio;

public sealed class CrearEnvioCommand
{
    public Guid ComercioId { get; set; }
    public Guid CuentaComercialId { get; set; }
    public Guid ZonaId { get; set; }
    public Guid ReglasId { get; set; }
    public Guid CuadroTarifarioId { get; set; }
    public Guid FranjaHorariaId { get; set; }
    public string DestinatarioNombre { get; set; } = "";
    public string DestinatarioTelefono { get; set; } = "";
    public string DestinatarioEmail { get; set; } = "";
    public string DireccionCalle { get; set; } = "";
    public string DireccionNumero { get; set; } = "";
    public string DireccionLocalidad { get; set; } = "";
    public string DireccionCodigoPostal { get; set; } = "";
    public ModalidadServicio ModalidadServicio { get; set; }
    public List<BultoAlta> Bultos { get; set; } = [];

    // Valores que debe resolver la preparación del alta en el servidor.
    // El formulario inicial aún no calcula tarifa, compromiso ni programación.
    public PreparacionAltaEnvio? Preparacion { get; set; }
}

public sealed record BultoAlta(string Codigo, decimal Peso, decimal Alto, decimal Ancho, decimal Largo);

public sealed record PreparacionAltaEnvio(decimal TarifaCalculada, DateTime FechaComprometida,
    DateOnly FechaEntrega, DateTimeOffset InicioLocal, DateTimeOffset FinLocal);
