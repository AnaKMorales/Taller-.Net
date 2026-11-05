using UltimaMilla.Repartidor.Contratos;

namespace UltimaMilla.Repartidor.Sincronizacion;

// Repite en el dispositivo las reglas de Envio.RegistrarIntento que se pueden comprobar sin conexión,
// para que el repartidor corrija en el momento y no se entere horas después por un rechazo.
// Las reglas que dependen del estado actual del servidor (intentos agotados, plazo entre intentos,
// cambios hechos por el despachador) las decide el servidor al sincronizar.
public static class ValidadorAccion
{
    public static IReadOnlyList<string> Validar(AccionEntregaDto accion, HojaDeRutaDto hoja)
    {
        var errores = new List<string>();
        if (accion.IdOperacion == Guid.Empty) errores.Add("La acción no tiene identificador de operación.");

        var parada = hoja.Paradas.SingleOrDefault(p => p.EnvioId == accion.EnvioId);
        if (parada is null)
            errores.Add("El envío no está en la hoja de ruta.");
        else if (parada.Estado != EstadoEnvio.EnTransito)
            errores.Add($"El envío está {parada.Estado}: solo se registran intentos de envíos en tránsito.");

        if (accion.Pruebas.Any(p => string.IsNullOrWhiteSpace(p.Valor)))
            errores.Add("Las pruebas no pueden estar vacías.");
        if (accion.Pruebas.GroupBy(p => p.Tipo).Any(g => g.Count() > 1))
            errores.Add("No se puede repetir un tipo de prueba.");
        if (accion.Pruebas.Any(p => p.Posicion is null))
            errores.Add("Las pruebas necesitan la ubicación del dispositivo. Activá el GPS e intentá de nuevo.");

        if (accion.Resultado == ResultadoIntento.Entregado)
        {
            if (accion.MotivoId is not null) errores.Add("Una entrega exitosa no lleva motivo.");
            foreach (var tipo in hoja.PruebasExigidas.Where(t => accion.Pruebas.All(p => p.Tipo != t)))
                errores.Add($"Falta la prueba exigida por el operador: {tipo}.");
        }
        else
        {
            var motivo = hoja.Motivos.SingleOrDefault(m => m.Id == accion.MotivoId);
            if (motivo is null)
                errores.Add("Elegí un motivo del catálogo.");
            else if (motivo.RequiereEvidencia && accion.Pruebas.Count == 0)
                errores.Add($"El motivo \"{motivo.Nombre}\" exige evidencia.");
        }

        return errores;
    }
}
