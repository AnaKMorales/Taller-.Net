namespace UltimaMilla.Domain.Envios;

public class Envio
{
    public Guid Id { get; set; }
    public Guid OperadorId { get; set; }
    public Guid ComercioId { get; set; }
    public string CodigoSeguimiento { get; set; }
    public EstadoEnvio Estado { get; set; }
    public string DestinatarioNombre { get; set; }
    public string DestinatarioTelefono { get; set; }
    public string DireccionCalle { get; set; }
    public DateTime FechaAdmision { get; set; }

    public Envio() { }

    public Envio(Guid operadorId, Guid comercioId, string nombre, string telefono, string direccion)
    {
        Id = Guid.NewGuid();
        OperadorId = operadorId;
        ComercioId = comercioId;
        CodigoSeguimiento = GenerarCodigo();
        Estado = EstadoEnvio.Admitido;
        DestinatarioNombre = nombre;
        DestinatarioTelefono = telefono;
        DireccionCalle = direccion;
        FechaAdmision = DateTime.Now;
    }

    private string GenerarCodigo()
    {
        // hay que mejorarlo
        var guid = Guid.NewGuid().ToString();
        return guid.Substring(0, 8).ToUpper();
    }
}

public enum EstadoEnvio
{
    Admitido,
    EnDeposito,
    AsignadoARuta,
    EnTransito,
    Entregado,
    NoEntregado,
    Reprogramado,
    EnDevolucion,
    Devuelto,
    Extraviado
}
