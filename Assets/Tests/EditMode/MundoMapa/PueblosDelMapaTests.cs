// Los pueblos extractores «Mejorado» (`MaquetasDelMapa.SitioDePueblo` y `GradosDe`): sobre su nodo, con el frente hacia el camino que sale de
// su huella o, sin camino, hacia su madre; en espejo según la semilla (determinista, más o menos la mitad).
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public class PueblosDelMapaTests
    {
        static readonly (string, IReadOnlyList<(double x, double y)>)[] SinCaminos = new (string, IReadOnlyList<(double x, double y)>)[0];

        [Test]
        public void VaSobreSuNodoYMiraASuCamino()
        {
            var camino = new (string, IReadOnlyList<(double x, double y)>)[] { ("camino-1", new List<(double, double)> { (10, 10), (10.3, 10), (14, 10) }) };
            var (x, y, rumbo, _) = MaquetasDelMapa.SitioDePueblo(11, 10.5, (10, 10), (10, 30), 1, camino);
            Assert.That((x, y), Is.EqualTo((10.0, 10.0)), "en el centro del nodo");
            Assert.That(rumbo, Is.EqualTo(0).Within(1e-9), "hacia el camino (al este), no hacia la madre");
        }

        [Test]
        public void SinCaminoMiraASuMadre()
        {
            var (_, _, rumbo, _) = MaquetasDelMapa.SitioDePueblo(0, 0, (10, 10), (10, 30), 1, SinCaminos);
            Assert.That(rumbo, Is.EqualTo(Math.PI / 2).Within(1e-9));
            // sin nodo, en su posición; sin madre ni camino, sin rumbo, y el giro por la semilla
            var (x, y, sin, _) = MaquetasDelMapa.SitioDePueblo(3, 4, null, null, 1, SinCaminos);
            Assert.That((x, y), Is.EqualTo((3.0, 4.0)));
            Assert.That(double.IsNaN(sin));
            float g = MaquetasDelMapa.GradosDe(sin, -Math.PI / 2, 0.37);
            Assert.That(g, Is.InRange(0f, 360f));
            Assert.That(MaquetasDelMapa.GradosDe(sin, -Math.PI / 2, 0.37), Is.EqualTo(g), "determinista");
        }

        [Test]
        public void ElGiroPoneElFrenteHaciaElRumbo()
        {
            double puerta = -Math.PI / 2;
            Assert.That(MaquetasDelMapa.GradosDe(puerta, puerta, 0), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(MaquetasDelMapa.GradosDe(puerta + Math.PI / 2, puerta, 0), Is.EqualTo(90f).Within(1e-4f));
        }

        [Test]
        public void ElEspejoEsDeLaSemillaYSaleLaMitad()
        {
            int espejados = 0, n = 0;
            for (int i = 0; i < 20; i++)
                for (int j = 0; j < 20; j++, n++)
                {
                    var a = MaquetasDelMapa.SitioDePueblo(0, 0, (i * 7.3, j * 5.1), null, 1, SinCaminos);
                    var b = MaquetasDelMapa.SitioDePueblo(1, 1, (i * 7.3, j * 5.1), null, 1, SinCaminos);
                    Assert.That(b.espejo, Is.EqualTo(a.espejo), "solo depende del sitio del nodo");
                    if (a.espejo) espejados++;
                }
            Assert.That(espejados / (double)n, Is.InRange(0.35, 0.65));
        }
    }
}
