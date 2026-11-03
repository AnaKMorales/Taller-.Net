using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UltimaMilla.Domain.Envios;

public sealed partial class Envio
{
  public IntentoEntrega RegistrarIntentoPorBulto(Guid paradaId, IEnumerable<ResultadoBultoEntrega> resultados,
    Usuario responsable, DateTime ocurridoEn, Guid idOperacion, GeoPunto? posicion = null)
  {
    ValidarResponsable(responsable, ocurridoEn);
    if (idOperacion == Guid.Empty) throw new ArgumentException("La operación requiere un identificador.", nameof(idOperacion));
    if (paradaId == Guid.Empty) throw new ArgumentException("La operación requiere la parada de origen.", nameof(paradaId));
    if (ocurridoEn.Kind != DateTimeKind.Utc) throw new ArgumentException("El instante del intento debe ser UTC.", nameof(ocurridoEn));
    ArgumentNullException.ThrowIfNull(resultados);
    var datos = resultados.Select(r => r is null || r.Pruebas is null ?
      throw new ArgumentException("Faltan resultados o evidencias.", nameof(resultados)) :
      r with { Pruebas = r.Pruebas.ToArray() }).ToArray();
    if (datos.Length == 0 || datos.Any(r => r.Pruebas.Any(p => p is null)))
      throw new ArgumentException("Se requieren resultados y evidencias válidos.", nameof(resultados));
    if (datos.Select(r => r.BultoId).Distinct().Count() != datos.Length ||
        datos.Any(r => r.Resultado is not (ResultadoIntento.Entregado or ResultadoIntento.Fallido)))
      throw new ArgumentException("Cada bulto debe aparecer una vez con resultado entregado o fallido.", nameof(resultados));
    var huella = HuellaIntento(datos, paradaId, responsable.Id, ocurridoEn, posicion);
    var anterior = _intentos.SingleOrDefault(i => i.IdOperacion == idOperacion);
    if (anterior is not null)
    {
      if (anterior.HuellaSolicitud != huella)
        throw new InvalidOperationException("El identificador ya se utilizó con otro contenido.");
      return anterior;
    }

    if (Estado != EstadoEnvio.EnTransito || Parada is null || Parada.Id != paradaId || responsable.Id != Parada.Ruta.RepartidorId)
      throw new InvalidOperationException("El intento requiere el repartidor asignado y un envío en tránsito.");
    if (_intentos.Count >= Reglas.MaxIntentosEntrega || (_intentos.Count > 0 &&
        ocurridoEn < UltimoIntento!.OcurridoEn.AddHours(Reglas.PlazoEntreIntentosHoras)))
      throw new InvalidOperationException("No corresponde registrar otro intento en este momento.");
    if (!datos.Select(r => r.BultoId).ToHashSet().SetEquals(_bultos.Select(b => b.Id)) ||
        _bultos.Any(b => b.Estado != EstadoBulto.EnTransito))
      throw new InvalidOperationException("El intento debe incluir todos y solo los bultos en tránsito de la visita actual.");

    var entregados = datos.Count(r => r.Resultado == ResultadoIntento.Entregado);
    var parcial = entregados > 0 && entregados < datos.Length;
    if (parcial && !Reglas.PermiteEntregaParcial)
      throw new InvalidOperationException("La versión de reglas del envío no permite entrega parcial.");

    var pruebas = datos.SelectMany(r => r.Pruebas).DistinctBy(p => p.Id).ToList();
    if (pruebas.Any(p => p.Intento is not null || p.Posicion is null || p.Hora.Kind != DateTimeKind.Utc) ||
        datos.SelectMany(r => r.Pruebas).GroupBy(p => p.Id).Any(g => g.Any(p => !ReferenceEquals(p, g.First()))))
      throw new ArgumentException("Las evidencias requieren posición y fecha UTC y no deben pertenecer a otro intento ni repetir identidad con otro objeto.");

    foreach (var dato in datos)
    {
      if (dato.Pruebas.Select(p => p.Id).Distinct().Count() != dato.Pruebas.Count)
        throw new ArgumentException("Una evidencia no puede repetirse en el mismo resultado.");
      if (dato.Resultado == ResultadoIntento.Entregado)
      {
        if (dato.Motivo is not null || Reglas.TiposPrueba.Any(t => !dato.Pruebas.Any(p => p.TipoPrueba == t)))
          throw new InvalidOperationException("Cada bulto entregado requiere las pruebas configuradas y no lleva motivo de no entrega.");
      }
      else if (dato.Motivo is not { Activo: true } motivo || motivo.ReglasId != ReglasId ||
               (motivo.RequiereEvidencia && dato.Pruebas.Count == 0))
        throw new InvalidOperationException("Cada bulto no entregado requiere motivo activo del catálogo del envío y su evidencia exigida.");
    }
    // Una prueba de entrega no puede acreditar a la vez un bulto que no se entregó.
    if (datos.SelectMany(r => r.Pruebas.Select(p => (p.Id, r.Resultado))).GroupBy(x => x.Id)
        .Any(g => g.Select(x => x.Resultado).Distinct().Count() > 1))
      throw new InvalidOperationException("No se puede compartir evidencia entre entregados y no entregados.");

    var resultado = parcial ? ResultadoIntento.Parcial : entregados == datos.Length ? ResultadoIntento.Entregado : ResultadoIntento.Fallido;
    var tipo = parcial ? TipoEvento.EntregaParcial : entregados == datos.Length ? TipoEvento.EntregaExitosa : TipoEvento.FalloEntrega;
    var esperado = TransicionesEnvio.Resolver(Estado, tipo);
    var cambios = datos.Select(d => (Bulto: _bultos.Single(b => b.Id == d.BultoId), Dato: d,
      Estado: TransicionesBulto.Resolver(EstadoBulto.EnTransito,
        d.Resultado == ResultadoIntento.Entregado ? TipoEvento.EntregaExitosa : TipoEvento.FalloEntrega))).ToArray();
    var nuevo = EstadoConjuntoEnvio.Calcular(cambios.Select(c => c.Estado));
    if (nuevo != esperado) throw new InvalidOperationException("El resumen no coincide con los resultados.");
    var motivoComun = entregados == 0 && datos.Select(d => d.Motivo!.Id).Distinct().Count() == 1 ? datos[0].Motivo : null;
    var intento = new IntentoEntrega(this, Parada, _intentos.Count + 1, resultado, motivoComun, pruebas, idOperacion, ocurridoEn);
    intento.FijarHuella(huella);
    foreach (var c in cambios) intento.RegistrarDetalle(c.Bulto, c.Dato.Resultado, c.Dato.Motivo, c.Dato.Pruebas);
    var evento = new EventoDeEnvio(this, tipo, OrigenEvento.AppRepartidor, Estado, nuevo, responsable, ocurridoEn, posicion, null, idOperacion);
    _intentos.Add(intento);
    Parada.Registrar(intento);
    foreach (var c in cambios) c.Bulto.ActualizarEstado(c.Estado);
    Estado = nuevo;
    _eventos.Add(evento);
    LiberarParada();
    return intento;
  }

  private static string HuellaIntento(IEnumerable<ResultadoBultoEntrega> datos, Guid parada, Guid responsable,
    DateTime fecha, GeoPunto? posicion)
  {
    // Los Id de evidencias se generan al reconstruirlas: se compara contenido y asociación.
    var json = JsonSerializer.Serialize(new
    {
      parada, responsable, fecha, posicion,
      resultados = datos.OrderBy(d => d.BultoId).Select(d => new
      {
        d.BultoId, d.Resultado, Motivo = d.Motivo?.Id,
        Pruebas = d.Pruebas.Select(p => JsonSerializer.Serialize(new
          { p.TipoPrueba, p.Hora, p.Posicion, p.ArchivoRef, p.ValorTexto })).Order(StringComparer.Ordinal).ToArray()
      })
    });
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
  }
}
