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
        if (accion.Intento is { } intento) return ValidarPorBulto(accion, intento, hoja);

        var errores = new List<string>();
        if (accion.IdOperacion == Guid.Empty) errores.Add("La acción no tiene identificador de operación.");

        var parada = hoja.Paradas.SingleOrDefault(p => p.EnvioId == accion.EnvioId);
        if (parada is null)
            errores.Add("El envío no está en la hoja de ruta.");
        else if (parada.ParadaId is not null)
            errores.Add("Registrá el resultado de cada bulto de la visita.");
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

    private static IReadOnlyList<string> ValidarPorBulto(AccionEntregaDto accion, IntentoPorBultoDto intento, HojaDeRutaDto hoja)
    {
        var parada = hoja.Paradas.SingleOrDefault(p => p.EnvioId == intento.EnvioId && p.ParadaId == intento.ParadaId);
        if (parada is not { AdmiteRegistro: true, Reglas: { } reglas, Bultos: { } bultos } ||
            parada.VersionEsperadaEnvio != intento.VersionEsperada || intento.EnvioId != accion.EnvioId)
            return ["La parada o su versión no coincide con la hoja guardada. Actualizá la hoja."];

        var resultados = intento.Bultos;
        if (resultados.Count != bultos.Count ||
            !resultados.Select(r => r.BultoId).ToHashSet().SetEquals(bultos.Select(b => b.BultoId)) ||
            resultados.Any(r => r.Resultado is not (ResultadoIntento.Entregado or ResultadoIntento.Fallido)))
            return ["Elegí un resultado para cada bulto de la visita."];

        var errores = new List<string>();
        if (intento.IdOperacion == Guid.Empty || intento.IdOperacion != accion.IdOperacion)
            errores.Add("La visita no tiene un identificador de operación válido.");
        if (!reglas.PermiteEntregaParcial && resultados.Select(r => r.Resultado).Distinct().Count() > 1)
            errores.Add("La configuración de este envío no permite entregas parciales.");
        if (intento.Evidencias.Any(e => e.Posicion is null))
            errores.Add("Las evidencias necesitan la ubicación del dispositivo. Activá el GPS e intentá de nuevo.");
        if (intento.Evidencias.Sum(e => (long)(e.Archivo?.Length ?? 0)) > 4 * 1024 * 1024)
            errores.Add("Las fotos y firmas de esta visita superan el tamaño máximo. Reducí el tamaño de las imágenes.");

        foreach (var resultado in resultados)
        {
            var codigo = bultos.Single(b => b.BultoId == resultado.BultoId).Codigo;
            if (resultado.Resultado == ResultadoIntento.Entregado)
            {
                foreach (var tipo in reglas.PruebasExigidas.Where(tipo =>
                    !intento.Evidencias.Any(e => e.Tipo == tipo && resultado.Evidencias.Contains(e.Referencia))))
                    errores.Add($"{codigo}: falta la prueba de {tipo}.");
            }
            else
            {
                var motivo = reglas.Motivos.SingleOrDefault(m => m.Id == resultado.MotivoId);
                if (motivo is null)
                    errores.Add($"{codigo}: elegí un motivo del catálogo.");
                else if (motivo.RequiereEvidencia && resultado.Evidencias.Count == 0)
                    errores.Add($"{codigo}: el motivo \"{motivo.Nombre}\" exige evidencia.");
            }
        }

        return errores;
    }
}
