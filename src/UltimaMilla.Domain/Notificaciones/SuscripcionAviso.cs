using System.Collections.Frozen;

namespace UltimaMilla.Domain.Notificaciones;

public sealed class SuscripcionAviso : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private SuscripcionAviso()
  {
    CuentaComercial = null!;
    TiposEvento = null!;
    SecretoFirma = null!;
    UrlDestino = null!;
  }

  private readonly List<EntregaAviso> _entregas = [];
  public CuentaComercial CuentaComercial { get; private set; }
  public Guid CuentaComercialId => CuentaComercial.Id;
  public Guid OperadorId => CuentaComercial.OperadorId;
  public Guid ComercioId => CuentaComercial.ComercioId;
  public IReadOnlySet<TipoEvento> TiposEvento { get; private set; }
  public string SecretoFirma { get; private set; }
  public Uri UrlDestino { get; private set; }
  public bool Activa { get; private set; } = true;
  public IReadOnlyCollection<EntregaAviso> Entregas => _entregas.AsReadOnly();

  public SuscripcionAviso(CuentaComercial cuentaComercial, IEnumerable<TipoEvento> tiposEvento, string secretoFirma, Uri urlDestino)
  {
    CuentaComercial = Validacion.Requerido(cuentaComercial, nameof(cuentaComercial));
    ArgumentNullException.ThrowIfNull(tiposEvento);
    TiposEvento = tiposEvento.Select(t => Validacion.Enumeracion(t, nameof(tiposEvento))).ToFrozenSet();
    if (TiposEvento.Count == 0) throw new ArgumentException("Debe seleccionar al menos un evento.", nameof(tiposEvento));
    SecretoFirma = Validacion.Texto(secretoFirma, nameof(secretoFirma));
    UrlDestino = Validacion.Requerido(urlDestino, nameof(urlDestino));
    if (!urlDestino.IsAbsoluteUri || (urlDestino.Scheme != Uri.UriSchemeHttp && urlDestino.Scheme != Uri.UriSchemeHttps))
      throw new ArgumentException("El destino debe ser una URL HTTP o HTTPS absoluta.", nameof(urlDestino));
    cuentaComercial.Registrar(this);
  }

  public void Desactivar() => Activa = false;

  internal void Registrar(EntregaAviso entrega)
  {
    if (_entregas.Any(e => e.EventoId == entrega.EventoId))
      throw new InvalidOperationException("El evento ya tiene una entrega para esta suscripcion.");
    _entregas.Add(entrega);
  }
}
