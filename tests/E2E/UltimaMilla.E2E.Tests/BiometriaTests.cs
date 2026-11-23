using System.Text.Json;

namespace UltimaMilla.E2E.Tests;

[Collection("E2E")]
public sealed class BiometriaTests(Entorno entorno)
{
    [E2EFact]
    public async Task ElRepartidorSoloCambiaSuContrasenaConUnaConfirmacionValidaYDeUnSoloUso()
    {
        var pagina = await entorno.NuevaPaginaAsync();
        var datos = await pagina.APIRequest.PostAsync("/dev/datos-demo-pwa");
        Assert.True(datos.Ok);
        using var json = JsonDocument.Parse(await datos.TextAsync());
        var email = json.RootElement.GetProperty("emailRepartidor").GetString()!;
        var actual = json.RootElement.GetProperty("contrasena").GetString()!;

        // Chromium requiere un nombre de dominio como RP ID, incluso en el entorno local.
        var origen = new UriBuilder(entorno.Base) { Path = "/login" };
        if (origen.Host == "127.0.0.1") origen.Host = "localhost";
        await pagina.GotoAsync(origen.Uri.AbsoluteUri);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("WebAuthn.enable");
        await cdp.SendAsync("WebAuthn.addVirtualAuthenticator", new Dictionary<string, object>
        {
            ["options"] = new
            {
                protocol = "ctap2", transport = "internal", hasResidentKey = true, hasUserVerification = true,
                isUserVerified = true, automaticPresenceSimulation = true
            }
        });

        var estadosJson = await pagina.EvaluateAsync<string>(
            """
            async ({ email, actual, nueva }) => {
                const api = '/api/repartidor/';
                const post = (ruta, datos, token) => fetch(api + ruta, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', ...(token && { Authorization: `Bearer ${token}` }) },
                    body: JSON.stringify(datos)
                });
                const abrirSesion = contrasena => post('sesion', { email, contrasena });
                const login = await abrirSesion(actual);
                if (!login.ok) throw new Error(`Inicio de sesión: HTTP ${login.status}`);
                const { accessToken } = await login.json();
                const pedir = (ruta, datos) => post('biometria/' + ruta, datos, accessToken);
                const leerOpciones = async (ruta, datos) => {
                    const respuesta = await pedir(ruta, datos);
                    if (!respuesta.ok) throw new Error(`${ruta}: HTTP ${respuesta.status}`);
                    const opciones = await respuesta.json();
                    return [opciones.desafioId, JSON.parse(opciones.opcionesJson)];
                };

                const sinPasskey = await pedir('contrasena/opciones');
                const [idRegistro, opcionesRegistro] = await leerOpciones('registro/opciones', { contrasena: actual });
                const registro = await navigator.credentials.create({
                    publicKey: PublicKeyCredential.parseCreationOptionsFromJSON(opcionesRegistro)
                });
                const alta = await pedir('registro', { desafioId: idRegistro, credencial: registro });
                const repetirAlta = await pedir('registro', { desafioId: idRegistro, credencial: registro });
                const segundaPasskey = await pedir('registro/opciones', { contrasena: actual });

                const [idInvalido] = await leerOpciones('contrasena/opciones');
                const sinFirma = await pedir('contrasena', { desafioId: idInvalido, credencial: {}, actual, nueva });
                const [desafioId, opcionesCambio] = await leerOpciones('contrasena/opciones');
                const firma = await navigator.credentials.get({
                    publicKey: PublicKeyCredential.parseRequestOptionsFromJSON(opcionesCambio)
                });
                const solicitud = { desafioId, credencial: firma, actual, nueva };
                const confirmacion = await pedir('contrasena', solicitud);
                const repetida = await pedir('contrasena', solicitud);
                return JSON.stringify({
                    sinPasskey: sinPasskey.status, alta: alta.status, repetirAlta: repetirAlta.status,
                    segundaPasskey: segundaPasskey.status,
                    sinFirma: sinFirma.status, confirmacion: confirmacion.status, repetida: repetida.status,
                    anterior: (await abrirSesion(actual)).status, actualizada: (await abrirSesion(nueva)).status
                });
            }
            """, new { email, actual, nueva = $"Nueva!{Guid.NewGuid():N}" });

        using var estados = JsonDocument.Parse(estadosJson);
        int Estado(string etapa) => estados.RootElement.GetProperty(etapa).GetInt32();
        Assert.Equal(400, Estado("sinPasskey"));
        Assert.Equal(204, Estado("alta"));
        Assert.Equal(400, Estado("repetirAlta"));
        Assert.Equal(400, Estado("segundaPasskey"));
        Assert.Equal(400, Estado("sinFirma"));
        Assert.Equal(204, Estado("confirmacion"));
        Assert.Equal(400, Estado("repetida"));
        Assert.Equal(401, Estado("anterior"));
        Assert.Equal(200, Estado("actualizada"));
    }
}
