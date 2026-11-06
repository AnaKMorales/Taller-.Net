using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Envios.RegistrarIntento;

public interface IRegistroIntentoRepository
{
    // Una unidad dedicada: autoriza la identidad y carga el agregado con una lectura coherente.
    Task<IOperacionRegistroIntento> AbrirAsync(Guid envioId, Guid paradaId, Guid idOperacion, CancellationToken ct);
}

public interface IOperacionRegistroIntento : IAsyncDisposable
{
    Envio Envio { get; }
    Repartidor Responsable { get; }
    Guid VersionActual { get; }
    Task GuardarAsync(CancellationToken ct);
}
