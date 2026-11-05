using System.Xml.Linq;

namespace UltimaMilla.Architecture.Tests;

// Complementa el análisis de tipos: detecta también referencias declaradas pero aún no utilizadas.
public sealed class ProyectosTests
{
    private static readonly IReadOnlyDictionary<string, string[]> ReferenciasPermitidas = new Dictionary<string, string[]>
    {
        ["UltimaMilla.Domain"] = [],
        ["UltimaMilla.Application"] = ["UltimaMilla.Domain"],
        ["UltimaMilla.Infrastructure"] = ["UltimaMilla.Domain", "UltimaMilla.Application"],
        ["UltimaMilla.Web"] = ["UltimaMilla.Domain", "UltimaMilla.Application", "UltimaMilla.Infrastructure"],
        // La PWA corre en el navegador: solo conoce el contrato JSON de la API, no las capas del servidor.
        ["UltimaMilla.Repartidor"] = []
    };

    [Theory]
    [InlineData("UltimaMilla.Domain")]
    [InlineData("UltimaMilla.Application")]
    [InlineData("UltimaMilla.Infrastructure")]
    [InlineData("UltimaMilla.Web")]
    [InlineData("UltimaMilla.Repartidor")]
    public void ReferenciasDeclaradasRespetanLaDireccionDeLasCapas(string proyecto)
    {
        var path = Path.Combine(RaizRepositorio(), "src", proyecto, proyecto + ".csproj");
        var xml = XDocument.Load(path);
        foreach (var reference in xml.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
        {
            var include = reference.Attribute("Include")?.Value;
            Assert.False(string.IsNullOrWhiteSpace(include), $"Referencia no reconocida en {path}.");
            var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!,
                include!.Replace('\\', Path.DirectorySeparatorChar)));
            var allowed = ReferenciasPermitidas[proyecto].Select(name =>
                Path.GetFullPath(Path.Combine(RaizRepositorio(), "src", name, name + ".csproj")));
            Assert.Contains(target, allowed, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        }

        if (proyecto is "UltimaMilla.Domain" or "UltimaMilla.Application")
        {
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName is "PackageReference" or "FrameworkReference" or "Import");
        }
    }

    [Fact]
    public void TodoProyectoProductivoEstaContempladoEnLasReglas()
    {
        var src = Path.Combine(RaizRepositorio(), "src");
        var projects = Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(src, path).Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .Select(path => Path.GetFileNameWithoutExtension(path)).Order().ToArray();
        Assert.True(projects.SequenceEqual(ReferenciasPermitidas.Keys.Order()),
            "Hay proyectos nuevos o renombrados. Actualiza la matriz, las referencias de este proyecto de pruebas y las reglas de tipos; revisa ADR-001.");
    }

    private static string RaizRepositorio()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UltimaMilla.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException(
            "Ejecuta estas pruebas desde el repositorio: necesitan leer los .csproj y UltimaMilla.slnx.");
    }
}
