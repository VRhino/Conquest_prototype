// Las reglas de las miniaturas de asentamiento «Mejorado» (`MaquetasDelMapa`) que no necesitan mapa ni capturas: la variante por semilla,
// el nivel de detalle por el ancho en pantalla y la huella de las plazas (con las miniaturas, con su mínimo; sin ellas, la del prototipo).
using System;
using System.Collections.Generic;
using System.Linq;
using ConquestTactics.Mundo;
using ConquestTactics.Mundo.Mapa;
using NUnit.Framework;

namespace ConquestTactics.Mundo.UI.Tests
{
    [Category("Mapa")]
    public sealed class MaquetasDelMapaReglasTests
    {
        [Test]
        public void LaVarianteEsEstablePorSemillaYSalenTodas()
        {
            var vistas = new HashSet<int>();
            for (int i = 0; i < 200; i++)
            {
                double sem = MaquetasDelPrototipo.Semilla(100 + i * 7.3, 400 + i * 3.1);
                int v = MaquetasDelMapa.VarianteDe(sem, 2);
                Assert.That(v, Is.InRange(0, 1));
                Assert.That(MaquetasDelMapa.VarianteDe(sem, 2), Is.EqualTo(v), "la misma semilla, la misma variante");
                vistas.Add(v);
            }
            Assert.That(vistas.Count, Is.EqualTo(2), "en 200 sitios salen las dos variantes");
            // el mismo sitio, siempre la misma (la semilla sale de la posición)
            Assert.That(MaquetasDelMapa.VarianteDe(MaquetasDelPrototipo.Semilla(1596.5, 571.4), 2),
                Is.EqualTo(MaquetasDelMapa.VarianteDe(MaquetasDelPrototipo.Semilla(1596.5, 571.4), 2)));
        }

        [Test]
        public void ElRumboEsElDelCaminoDeMenorId()
        {
            IReadOnlyList<(double x, double y)> L(params (double, double)[] p) => p;
            var caminos = new List<(string, IReadOnlyList<(double x, double y)>)>
            {
                ("tramo-10", L((10, 10), (10, 20))),          // sale al norte
                ("tramo-2", L((0, 10), (9.5, 10.2), (10, 10))), // acaba en la plaza: sale al oeste (su primer punto fuera, recorrido al revés)
                ("tramo-1", L((30, 30), (40, 40))),           // no pasa por ella
            };
            Assert.That(MaquetasDelMapa.RumboDelCamino(10, 10, 1, caminos), Is.EqualTo(Math.PI).Within(1e-9), "el tramo-2 (2 < 10), hacia el oeste");
            caminos.RemoveAt(1);
            Assert.That(MaquetasDelMapa.RumboDelCamino(10, 10, 1, caminos), Is.EqualTo(Math.PI / 2).Within(1e-9), "sin él, el tramo-10, al norte");
            Assert.That(double.IsNaN(MaquetasDelMapa.RumboDelCamino(0, 0, 1, caminos)), Is.True, "sin camino, NaN (gira por su semilla)");
        }

        [Test]
        public void ElNivelDeDetalleCambiaEn80Y40Px()
        {
            var antes = (MaquetasDelMapa.AnchoCerca, MaquetasDelMapa.AnchoMedio);
            try
            {
                (MaquetasDelMapa.AnchoCerca, MaquetasDelMapa.AnchoMedio) = (80, 40);
                Assert.That(MaquetasDelMapa.NivelDeDetalle(500), Is.EqualTo(0));
                Assert.That(MaquetasDelMapa.NivelDeDetalle(80), Is.EqualTo(0));
                Assert.That(MaquetasDelMapa.NivelDeDetalle(79.9f), Is.EqualTo(1));
                Assert.That(MaquetasDelMapa.NivelDeDetalle(40), Is.EqualTo(1));
                Assert.That(MaquetasDelMapa.NivelDeDetalle(39.9f), Is.EqualTo(2));
                Assert.That(MaquetasDelMapa.NivelDeDetalle(1), Is.EqualTo(2));
            }
            finally { (MaquetasDelMapa.AnchoCerca, MaquetasDelMapa.AnchoMedio) = antes; }
        }

        [Test]
        public void LaHuellaTieneUnMinimoSoloConLasMiniaturasYNuncaEncogeAlSubir()
        {
            var antes = MaquetasDelMapa.Estilo;
            try
            {
                MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Prototipo;
                foreach (var rc in new[] { 1.2, 2.0, 3.5, 5.5 })
                    for (int n = 1; n <= 5; n++)
                        Assert.That(CosasDelMapa.HuellaDePlaza(rc, n), Is.EqualTo(MaquetasDelPrototipo.RadioDePlaza(rc, n)).Within(1e-12), $"«Prototipo»: rc {rc}, nivel {n}");
                MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
                Assert.That(MaquetasDelPrototipo.RadioDePlaza(1.2, 1), Is.EqualTo(0.5952).Within(1e-9));
                // (con lo fijo a su factor: `CosasDelMapa.Fijos(Plaza)`, lo fijo (× 2) por el de las plazas (0,5), desde el 2026-11-23)
                double f = CosasDelMapa.Fijos(ClaseDeCosa.Plaza);
                Assert.That(CosasDelMapa.HuellaDePlaza(1.2, 1), Is.EqualTo(0.9 * f).Within(1e-12), "recién fundada: de 0,60 a 0,90, por el factor");
                Assert.That(CosasDelMapa.HuellaDePlaza(2.0, 1), Is.EqualTo(MaquetasDelPrototipo.RadioDePlaza(2.0, 1) * f).Within(1e-12), "zona de 20 u: 0,99, por el factor");
                Assert.That(CosasDelMapa.HuellaDePlaza(3.5, 3), Is.EqualTo(MaquetasDelPrototipo.RadioDePlaza(3.5, 3) * f).Within(1e-12), "nivel 3, zona de 35 u: por el factor");
                Assert.That(MaquetasDelMapa.Huella(1.2, 1), Is.EqualTo(CosasDelMapa.HuellaDePlaza(1.2, 1)), "la miniatura usa la misma función");
                foreach (var rc in new[] { 1.2, 2.0, 2.5, 3.5, 4.5, 5.5 })
                    for (int n = 1; n < 5; n++)
                        Assert.That(CosasDelMapa.HuellaDePlaza(rc, n + 1), Is.GreaterThanOrEqualTo(CosasDelMapa.HuellaDePlaza(rc, n)), $"rc {rc}: del nivel {n} al {n + 1}");
                // (a la medida de la choza, 2026-11-23: la huella es el `radio_u` del modelo de su variante; nunca encoge al subir, sea cual sea la
                // variante: el mayor de un nivel, por debajo del menor del siguiente)
                double RadioU(int n, int v)
                {
                    var t = UnityEngine.Resources.Load<UnityEngine.TextAsset>($"MapaMundo/Asentamientos/plaza{n}_{v}");
                    Assert.That(t, Is.Not.Null, $"plaza{n}_{v}.json");
                    var m = System.Text.RegularExpressions.Regex.Match(t.text, "\"radio_u\"\\s*:\\s*([0-9.]+)");
                    Assert.That(m.Success, $"plaza{n}_{v}: su radio_u");
                    return double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                }
                for (int n = 1; n < 5; n++)
                    Assert.That(Math.Max(RadioU(n, 1), RadioU(n, 2)), Is.LessThan(Math.Min(RadioU(n + 1, 1), RadioU(n + 1, 2))), $"a la medida de la choza: del nivel {n} al {n + 1}");
            }
            finally { MaquetasDelMapa.Estilo = antes; }
        }
    }
}
