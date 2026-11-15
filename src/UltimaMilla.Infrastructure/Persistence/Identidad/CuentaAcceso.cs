using Microsoft.AspNetCore.Identity;

namespace UltimaMilla.Infrastructure.Persistence.Identidad;

// Usuario de ASP.NET Identity (tabla AspNetUsers): credenciales, bloqueo y stamps.
// Tiene el mismo Id que el Usuario del dominio (FK 1 a 1); el operador, el rol y las cuentas siguen en Usuario.
// Vive en Infrastructure para que Domain no dependa de Identity.
public sealed class CuentaAcceso : IdentityUser<Guid>;
