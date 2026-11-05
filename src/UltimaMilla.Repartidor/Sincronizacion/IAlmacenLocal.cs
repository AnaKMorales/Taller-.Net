namespace UltimaMilla.Repartidor.Sincronizacion;

// Almacén clave/valor del dispositivo. En el navegador es IndexedDB (Dispositivo/Navegador.cs);
// en las pruebas, un diccionario. Así la lógica de cola y sincronización se prueba sin navegador.
public interface IAlmacenLocal
{
    Task<string?> LeerAsync(string clave);
    Task GuardarAsync(string clave, string valor);
    Task BorrarAsync(string clave);
}
