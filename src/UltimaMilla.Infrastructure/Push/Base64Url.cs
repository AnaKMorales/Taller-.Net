namespace UltimaMilla.Infrastructure.Push;

// Base64 "URL safe" sin relleno: el formato de las claves de Web Push (navegador y VAPID).
internal static class Base64Url
{
    public static string Codificar(ReadOnlySpan<byte> datos) =>
        Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decodificar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        var b64 = texto.Trim().Replace('-', '+').Replace('_', '/');
        b64 += (b64.Length % 4) switch { 2 => "==", 3 => "=", 0 => "", _ => throw new FormatException("Base64url inválido.") };
        return Convert.FromBase64String(b64);
    }

    public static bool Intentar(string? texto, out byte[] datos)
    {
        datos = [];
        if (string.IsNullOrWhiteSpace(texto)) return false;
        try { datos = Decodificar(texto); return true; }
        catch (FormatException) { return false; }
    }
}
