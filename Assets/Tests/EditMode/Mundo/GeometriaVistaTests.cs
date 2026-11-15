// La cámara del mapa estratégico 2D y los sitios de la aldea (paso 9): las comprobaciones del `demo()` de BronzeAgeClient (`geometriaVista.ts`)
// y un caso pequeño de `sitiosDeAldea` (la zona, el anillo, la fertilidad y la separación).
using System.Collections.Generic;
using BronzeAge.Contract.V1;
using Google.Protobuf;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Tests
{
    public class GeometriaVistaTests
    {
        const double Lienzo_W = 1200, Lienzo_H = 800, Mundo_W = 2667, Mundo_H = 2000;

        [Test]
        public void LosTopesDelZoomYAcotar()
        {
            var min = GeometriaVista.EscalaMinima(Lienzo_W, Lienzo_H, Mundo_W, Mundo_H);
            Assert.AreEqual(1200 / 2667.0, min, 1e-9, "el mundo llena el lienzo: manda el ancho");
            Assert.AreEqual(800 / 30.0, GeometriaVista.EscalaDeVision(Lienzo_W, Lienzo_H, 15), 1e-9, "el diámetro de visión ocupa el lado corto");
            var c = GeometriaVista.Acotar(new CamaraDeVista(0.1, 0, 0), Lienzo_W, Lienzo_H, Mundo_W, Mundo_H, min, 26);
            Assert.AreEqual(min, c.Escala, 1e-12, "zoom acotado por abajo");
            Assert.AreEqual(Mundo_W / 2, c.Cx, 1e-9, "si la vista cubre el ancho, centrada en él");
            Assert.AreEqual(400 / min, c.Cy, 1e-9, "y en y pegada al borde de arriba");
        }

        [Test]
        public void ElZoomMantieneElPuntoBajoElCursorYArrastrarMueveAlReves()
        {
            var z = GeometriaVista.ZoomHacia(new CamaraDeVista(1, 1000, 1000), 2, 100, 0);
            Assert.AreEqual(1100, GeometriaVista.AMundo(z, 100, 0).x, 1e-9, "el punto bajo el cursor no se mueve");
            var vuelta = GeometriaVista.ZoomHacia(z, 0.5, 100, 0);
            Assert.AreEqual(1000, vuelta.Cx, 1e-9);
            Assert.AreEqual(1, vuelta.Escala, 1e-9, "zoom in y out al mismo foco es reversible");
            var a = GeometriaVista.Arrastrar(new CamaraDeVista(2, 500, 500), 20, -10);
            Assert.AreEqual(490, a.Cx, 1e-9);
            Assert.AreEqual(505, a.Cy, 1e-9, "arrastrar a la derecha lleva la vista a la izquierda");
            var o = GeometriaVista.Origen(new CamaraDeVista(2, 500, 500), Lienzo_W, Lienzo_H);
            Assert.AreEqual((200.0, 300.0), o, "origen = centro − mitad del lienzo / escala");
        }

        static RasterDeMundo Raster(int lado, double celda, System.Func<int, int, byte> valor)
        {
            var datos = new byte[lado * lado];
            for (int j = 0; j < lado; j++) for (int i = 0; i < lado; i++) datos[j * lado + i] = valor(i, j);
            return new RasterDeMundo(new Raster { Columnas = (uint)lado, Filas = (uint)lado, TamanoCelda = (uint)(celda * 1000), Encoding = EncodingRaster.EncodingU8, Datos = ByteString.CopyFrom(datos) });
        }

        [Test]
        public void LosSitiosDeLaAldea()
        {
            // 10 × 10 celdas de 2 u: fértil (200) la mitad este, yerma la oeste; todo llano (3).
            var fertilidad = Raster(10, 2, (i, j) => (byte)(i >= 5 ? 200 : 20));
            var terreno = Raster(10, 2, (i, j) => 3);
            var zona = new List<(double, double)> { (0, 0), (20, 0), (20, 20), (0, 20) };
            var s = SitiosDeAldea.Buscar(fertilidad, terreno, zona, (10, 10), (0, 8), new[] { (15.0, 10.0) }, 0.3, 3);
            Assert.IsTrue(s.Validos.Count > 0, "hay sitio");
            Assert.IsTrue(s.Validos.TrueForAll(p => p.x >= 10), "solo en la tierra fértil (este)");
            Assert.IsTrue(s.Validos.TrueForAll(p => (p.x - 10) * (p.x - 10) + (p.y - 10) * (p.y - 10) <= 64), "dentro de su anillo");
            Assert.IsTrue(s.Cerca.Exists(p => p.x == 15 && p.y == 10), "pegado a lo ocupado, gris");
            Assert.IsFalse(s.Validos.Exists(p => (p.x - 15) * (p.x - 15) + (p.y - 10) * (p.y - 10) < 9), "nada válido a menos de la separación");
            Assert.AreEqual(200 / 255.0, s.Mejor, 1e-9);
            // Lo que no se puede pisar no es tierra firme para el motor: fuera aunque sea fértil.
            var sinPisar = SitiosDeAldea.Buscar(fertilidad, terreno, zona, (10, 10), (0, 8), new (double, double)[0], 0.3, 3, (x, y) => y < 10);
            Assert.IsTrue(sinPisar.Validos.Count > 0 && sinPisar.Validos.TrueForAll(p => p.y < 10), "solo lo transitable");
        }
    }
}
