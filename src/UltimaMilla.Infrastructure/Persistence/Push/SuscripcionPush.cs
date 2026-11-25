using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Infrastructure.Persistence.Push;

// Registro técnico: a qué dispositivo mandar los avisos push de un repartidor. No es una entidad del negocio.
// El operador (TenantId) lo estampa el interceptor de tenant. Un dispositivo (Endpoint) pertenece a un solo
// repartidor: si otro inicia sesión en el mismo celular y activa los avisos, la suscripción pasa a ser suya.
public sealed class SuscripcionPush
{
    public const int LargoMaximoEndpoint = 2048;

    private SuscripcionPush() { Endpoint = P256dh = Auth = null!; Usuario = null!; }

    internal SuscripcionPush(Usuario usuario, string endpoint, string p256dh, string auth, DateTime ahora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        Id = Guid.NewGuid();
        Endpoint = endpoint;
        CreadaEn = ahora;
        P256dh = Auth = null!; Usuario = null!; // los asigna Renovar, que también los valida
        Renovar(usuario, p256dh, auth, ahora);
    }

    public Guid Id { get; private set; }
    // La FK (UsuarioId) es una propiedad sombra, como en el resto del modelo.
    public Usuario Usuario { get; private set; }
    public string Endpoint { get; private set; }
    public string P256dh { get; private set; }
    public string Auth { get; private set; }
    public DateTime CreadaEn { get; private set; }
    public DateTime ActualizadaEn { get; private set; }

    internal void Renovar(Usuario usuario, string p256dh, string auth, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        if (usuario.Rol != RolUsuario.Repartidor) throw new ArgumentException("Solo un repartidor recibe estos avisos.", nameof(usuario));
        ArgumentException.ThrowIfNullOrWhiteSpace(p256dh);
        ArgumentException.ThrowIfNullOrWhiteSpace(auth);
        if (ahora.Kind != DateTimeKind.Utc) throw new ArgumentException("La fecha debe ser UTC.", nameof(ahora));
        Usuario = usuario;
        P256dh = p256dh;
        Auth = auth;
        ActualizadaEn = ahora;
    }
}
