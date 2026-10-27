using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Architecture.Tests.Fixtures;

// Violación deliberada exclusivamente en el ensamblado de pruebas para comprobar la detección.
internal sealed class CasoDeUsoConEf
{
    public DbContext? Contexto { get; init; }
}

internal sealed class CasoDeUsoSinEf
{
    public Envio? Envio { get; init; }
}
