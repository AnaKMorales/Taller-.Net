using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Application.Envios.Importacion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tests;

public sealed class LectorImportacionTests
{
    private static readonly Guid Manana = Guid.NewGuid();
    private static readonly ZonaAltaDto Centro = new(Guid.NewGuid(), "Centro", "11000",
        [new(Manana, "Mañana", new(8, 0), new(12, 0))]);
    private static readonly OpcionesAltaDto Opciones = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), [Centro], []);

    private const string Encabezado = "referencia;zona;modalidad;destinatario;telefono;email;calle;numero;localidad;" +
        "codigo_postal;fecha_entrega;franja;bulto;peso_kg;alto_cm;ancho_cm;largo_cm";

    [Fact]
    public void AgrupaLasLineasDeUnaMismaReferenciaEnUnEnvio()
    {
        var lectura = LectorImportacion.Leer($"""
            {Encabezado}
            P-1;centro;Urgente;"Pérez; Ana";099;a@x.com;Av. Italia;1;Montevideo;11000;20/11/2026;manana;B1;2,5;10;20;30
            P-1;;;;;;;;;;;;B2;1;10;10;10
            P-2;11000;;Juan;098;j@x.com;Colonia;2;Montevideo;11000;2026-11-21;Mañana;C1;3;10;10;10
            """, Opciones);

        Assert.Empty(lectura.Errores);
        Assert.Equal(2, lectura.Filas.Count);
        var p1 = lectura.Filas[0];
        Assert.Equal("Pérez; Ana", p1.Solicitud.DestinatarioNombre);
        Assert.Equal(ModalidadServicio.Urgente, p1.Solicitud.Modalidad);
        Assert.Equal(["B1", "B2"], p1.Solicitud.Bultos.Select(b => b.Codigo));
        Assert.Equal(2.5m, p1.Solicitud.Bultos[0].Peso);
        Assert.Equal($"2026-11-20|{Manana}", p1.ClaveEntrega);
        Assert.Equal(ModalidadServicio.Estandar, lectura.Filas[1].Solicitud.Modalidad);
    }

    [Fact]
    public void UnEnvioConErrorNoFrenaAlResto()
    {
        var lectura = LectorImportacion.Leer($"""
            {Encabezado}
            P-1;Marte;;Ana;099;a@x.com;C;1;M;11000;20/11/2026;Mañana;B1;1;1;1;1
            P-2;Centro;;Ana;099;a@x.com;C;1;M;11000;20/11/2026;Mañana;B1;uno;1;1;1
            P-3;Centro;;Ana;099;a@x.com;C;1;M;11000;20/11/2026;Mañana;B1;1;1;1;1
            P-3;Centro;;Otra;099;a@x.com;C;1;M;11000;20/11/2026;Mañana;B2;1;1;1;1
            P-4;Centro;;Ana;099;a@x.com;C;1;M;11000;20/11/2026;Mañana;B1;1;1;1;1
            """, Opciones);

        Assert.Equal("P-4", Assert.Single(lectura.Filas).Referencia);
        Assert.Equal(["P-1", "P-2", "P-3"], lectura.Errores.Select(e => e.Referencia));
        Assert.Contains("Marte", lectura.Errores[0].Error);
        Assert.Contains("peso_kg", lectura.Errores[1].Error);
        Assert.Contains("destinatario", lectura.Errores[2].Error);
    }

    [Fact]
    public void RechazaArchivosSinColumnasOVacios()
    {
        Assert.Throws<ArgumentException>(() => LectorImportacion.Leer("referencia;zona\nP-1;Centro", Opciones));
        Assert.Throws<ArgumentException>(() => LectorImportacion.Leer(Encabezado, Opciones));
    }

    [Fact]
    public void AceptaComaComoSeparador()
    {
        var lectura = LectorImportacion.Leer(Encabezado.Replace(';', ',') + "\n" +
            "P-1,Centro,,Ana,099,a@x.com,C,1,M,11000,20/11/2026,Mañana,B1,\"2,5\",1,1,1", Opciones);
        Assert.Equal(2.5m, Assert.Single(lectura.Filas).Solicitud.Bultos[0].Peso);
    }
}
