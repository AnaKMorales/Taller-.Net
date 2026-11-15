using System.Globalization;
using System.Text;
using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.Importacion;

public sealed record LecturaImportacion(IReadOnlyList<FilaImportacion> Filas, IReadOnlyList<ResultadoFilaImportacion> Errores);

// CSV del portal: una línea por bulto; las líneas con la misma referencia son un mismo envío.
// Acepta ';' o ',' como separador (Excel en español guarda con ';') y coma decimal.
public static class LectorImportacion
{
    public static readonly string[] Columnas =
    [
        "referencia", "zona", "modalidad", "destinatario", "telefono", "email", "calle", "numero", "localidad",
        "codigo_postal", "fecha_entrega", "franja", "bulto", "peso_kg", "alto_cm", "ancho_cm", "largo_cm"
    ];

    public static LecturaImportacion Leer(string contenido, OpcionesAltaDto opciones)
    {
        var lineas = contenido.TrimStart('﻿').Split('\n').Select(l => l.TrimEnd('\r'))
            .Select((texto, i) => (texto, numero: i + 1)).Where(l => l.texto.Trim().Length > 0).ToList();
        if (lineas.Count < 2) throw new ArgumentException("El archivo no tiene envíos.");

        var separador = lineas[0].texto.Count(c => c == ';') >= lineas[0].texto.Count(c => c == ',') ? ';' : ',';
        var encabezado = Campos(lineas[0].texto, separador).Select(c => c.Trim().ToLowerInvariant()).ToList();
        var faltan = Columnas.Except(encabezado).ToList();
        if (faltan.Count > 0) throw new ArgumentException($"Faltan columnas: {string.Join(", ", faltan)}.");
        var indice = Columnas.ToDictionary(c => c, c => encabezado.IndexOf(c));

        var grupos = lineas.Skip(1)
            .Select(l => (l.numero, campos: Campos(l.texto, separador)))
            .Select(l => (l.numero, valor: (Func<string, string>)(c => indice[c] < l.campos.Count ? l.campos[indice[c]].Trim() : "")))
            .GroupBy(l => l.valor("referencia"), StringComparer.Ordinal);

        var filas = new List<FilaImportacion>();
        var errores = new List<ResultadoFilaImportacion>();
        foreach (var grupo in grupos)
        {
            try
            {
                if (grupo.Key.Length == 0) throw new ArgumentException("Hay líneas sin referencia.");
                filas.Add(Fila(grupo.Key, grupo.Select(g => (g.numero, g.valor)).ToList(), opciones));
            }
            catch (ArgumentException e)
            {
                errores.Add(new(grupo.Key, EstadoFilaImportacion.Error, Error: e.Message));
            }
        }
        if (filas.Count + errores.Count > 500) throw new ArgumentException("El archivo puede tener hasta 500 envíos.");
        return new(filas, errores);
    }

    private static FilaImportacion Fila(string referencia, List<(int numero, Func<string, string> valor)> lineas,
        OpcionesAltaDto opciones)
    {
        var primera = lineas[0].valor;
        // Los datos del envío salen de la primera línea; las demás tienen que coincidir
        string[] comunes = ["zona", "modalidad", "destinatario", "telefono", "email", "calle", "numero", "localidad",
            "codigo_postal", "fecha_entrega", "franja"];
        foreach (var (numero, valor) in lineas.Skip(1))
            foreach (var c in comunes)
                if (valor(c).Length > 0 && valor(c) != primera(c))
                    throw new ArgumentException($"Línea {numero}: «{c}» no coincide con la primera línea del envío.");

        var zona = opciones.Zonas.SingleOrDefault(z => Igual(z.Nombre, primera("zona")) || z.CodigoPostal == primera("zona"))
            ?? throw new ArgumentException($"Zona desconocida: «{primera("zona")}».");
        var franja = zona.Franjas.SingleOrDefault(f => Igual(f.Nombre, primera("franja")))
            ?? throw new ArgumentException($"La zona {zona.Nombre} no tiene la franja «{primera("franja")}».");
        var fecha = Fecha(primera("fecha_entrega"));

        var bultos = lineas.Select(l => new BultoAlta(
            Requerido(l.valor("bulto"), "bulto", l.numero),
            Numero(l.valor("peso_kg"), "peso_kg", l.numero),
            Numero(l.valor("alto_cm"), "alto_cm", l.numero),
            Numero(l.valor("ancho_cm"), "ancho_cm", l.numero),
            Numero(l.valor("largo_cm"), "largo_cm", l.numero))).ToList();

        var solicitud = new SolicitudAltaPortal(zona.Id, Modalidad(primera("modalidad")),
            primera("destinatario"), primera("telefono"), primera("email"),
            primera("calle"), primera("numero"), primera("localidad"), primera("codigo_postal"), bultos);
        return new FilaImportacion(referencia, solicitud, $"{fecha:yyyy-MM-dd}|{franja.Id}");
    }

    private static ModalidadServicio Modalidad(string texto) => Sin(texto) switch
    {
        "estandar" or "" => ModalidadServicio.Estandar,
        "urgente" => ModalidadServicio.Urgente,
        _ => throw new ArgumentException($"Modalidad desconocida: «{texto}» (Estandar o Urgente).")
    };

    private static DateOnly Fecha(string texto) =>
        DateOnly.TryParseExact(texto, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var fecha)
            ? fecha
            : throw new ArgumentException($"Fecha de entrega inválida: «{texto}» (usar dd/mm/aaaa).");

    private static decimal Numero(string texto, string campo, int linea) =>
        decimal.TryParse(texto.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : throw new ArgumentException($"Línea {linea}: «{campo}» no es un número.");

    private static string Requerido(string texto, string campo, int linea) =>
        texto.Length > 0 ? texto : throw new ArgumentException($"Línea {linea}: falta «{campo}».");

    private static bool Igual(string a, string b) => Sin(a) == Sin(b);

    // Sin tildes ni mayúsculas
    private static string Sin(string texto) => new(texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());

    // Separa una línea respetando comillas ("Av. Italia, esq. X")
    private static List<string> Campos(string linea, char separador)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        var comillas = false;
        for (var i = 0; i < linea.Length; i++)
        {
            var c = linea[i];
            if (c == '"')
            {
                if (comillas && i + 1 < linea.Length && linea[i + 1] == '"') { actual.Append('"'); i++; }
                else comillas = !comillas;
            }
            else if (c == separador && !comillas) { campos.Add(actual.ToString()); actual.Clear(); }
            else actual.Append(c);
        }
        campos.Add(actual.ToString());
        return campos;
    }
}
