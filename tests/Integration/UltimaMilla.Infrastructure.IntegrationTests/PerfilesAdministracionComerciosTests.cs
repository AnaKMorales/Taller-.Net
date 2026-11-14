using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Application.Administracion;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task A1SoloAdministradorPuedeGestionarComerciosYCuentasYUsuarios()
    {
        var e = await PrepararAdministracionFlota();
        var despacho = new Usuario(e.Operador, "despacho-permisos@example.com", RolUsuario.Despachador);
        var deposito = new Usuario(e.Operador, "deposito-permisos@example.com", RolUsuario.OperarioDeposito);
        await using (var db = Context())
        {
            db.Attach(e.Operador); db.AddRange(despacho, deposito); await db.SaveChangesAsync();
        }
        using var servidor = new ServidorApi(_testConnection);
        var comercio = e.Envio.CuentaComercial.Usuarios.Single();
        var cuentaId = e.Envio.CuentaComercial.Id;
        const string url = "/api/administracion/comercios";
        var operaciones = new (HttpMethod Metodo, string Ruta, object? Cuerpo)[] {
            (HttpMethod.Get, url, null),
            (HttpMethod.Post, url, new CrearComercio("No permitido", "123", "x@example.com")),
            (HttpMethod.Put, $"{url}/{cuentaId}", new ModificarCuentaComercial("No permitido", "123", "x@example.com", e.Envio.CuentaComercial.Version)),
            (HttpMethod.Post, $"{url}/{cuentaId}/baja", new BajaCuentaComercial(e.Envio.CuentaComercial.Version)),
            (HttpMethod.Get, $"{url}/{cuentaId}/usuarios", null),
            (HttpMethod.Post, $"{url}/{cuentaId}/usuarios", new CrearUsuarioComercio("x@example.com", ClaveApiComercio)),
            (HttpMethod.Put, $"{url}/{cuentaId}/usuarios/{comercio.Id}", new ModificarUsuarioComercio("x@example.com", new(comercio.Email, true))),
            (HttpMethod.Post, $"{url}/{cuentaId}/usuarios/{comercio.Id}/baja", new EstadoUsuarioEsperado(comercio.Email, true)) };
        // Comercio se verifica con Bearer real en el caso siguiente; estos son perfiles internos.
        foreach (var usuario in new[] { despacho, deposito, e.Repartidor })
        {
            using var cliente = servidor.Cliente(e, usuario);
            await Antiforgery(cliente);
            foreach (var op in operaciones)
            {
                using var solicitud = new HttpRequestMessage(op.Metodo, op.Ruta);
                if (op.Cuerpo is not null) solicitud.Content = JsonContent.Create(op.Cuerpo, op.Cuerpo.GetType());
                using var respuesta = await cliente.SendAsync(solicitud);
                Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
            }
        }
        using var admin = servidor.Cliente(e, e.Admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
        await using var lectura = Context();
        var cuenta = await lectura.Set<CuentaComercial>().SingleAsync();
        Assert.True(cuenta.Activa); Assert.Equal("Comercio", cuenta.RazonSocial);
        var guardado = await lectura.Set<Usuario>().SingleAsync(u => u.Id == comercio.Id);
        Assert.True(guardado.Activo); Assert.Equal(comercio.Email, guardado.Email);
        Assert.False(await lectura.Set<Usuario>().AnyAsync(u => u.Email == "x@example.com"));
    }

    [PostgresFact]
    public async Task A1PerfilComercioSeAsignaEnServidorYSuBearerNoPuedeElevarPermisos()
    {
        var e = await PrepararAdministracionFlota();
        using var servidor = new ServidorApi(_testConnection); using var admin = servidor.Cliente(e, e.Admin);
        await Antiforgery(admin);
        var cuenta = e.Envio.CuentaComercial.Id;
        var url = $"/api/administracion/comercios/{cuenta}/usuarios";
        using var alta = await admin.PostAsJsonAsync(url, new {
            email = "perfil@example.com", contrasena = ClaveApiComercio,
            rol = "Administrador", permisos = new[] { "AdministrarComercios" },
            operadorId = Guid.NewGuid(), cuentaComercialId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var usuario = (await alta.Content.ReadFromJsonAsync<UsuarioComercioAdministracionDto>())!;
        using var cambio = await admin.PutAsJsonAsync($"{url}/{usuario.Id}", new {
            email = "perfil-nuevo@example.com", esperado = new { email = usuario.Email, activo = true },
            rol = "Administrador", cuentaComercialId = Guid.NewGuid(), permisos = new[] { "AdministrarComercios" } });
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);
        await using (var db = Context())
        {
            var guardado = await db.Set<Usuario>().Include(u => u.CuentasComerciales).SingleAsync(u => u.Id == usuario.Id);
            Assert.Equal(RolUsuario.Comercio, guardado.Rol);
            Assert.Equal(cuenta, Assert.Single(guardado.CuentasComerciales).Id);
        }
        using var real = ServidorComercios(new()); using var cliente = real.Cliente();
        await LoginApiComercio(cliente, "perfil-nuevo@example.com");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/comercios/envios/opciones")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/administracion/comercios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync($"/api/administracion/comercios/{cuenta}/baja",
            new BajaCuentaComercial(e.Envio.CuentaComercial.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/despacho/recursos")).StatusCode);
    }
}
