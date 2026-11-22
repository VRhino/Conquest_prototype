// Lo menudo «Mejorado» del mapa (parte 5, `MaquetasDelMapa`): qué modelo toca a cada avanzada por su etapa y su estado (y con su mejora en
// marcha), la ciudadela por la semilla, la tela y el edificio militar de los mercenarios, el campamento de bandidos, el alijo y su mercancía, la
// obra genérica; dónde va el kit de obra (por fuera de los muros y lejos de la puerta); las huellas por etapa (`CosasDelMapa`); y el eje de los
// modelos importados (x de Blender → −x de Unity, y → −z).
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public class MenudoDelMapaTests
    {
        [Test]
        public void LaAvanzadaPorEtapaYEstado()
        {
            for (int i = 0; i < 40; i++)
            {
                double s = i * 0.731;
                string c = "ciudadela_" + MaquetasDelMapa.VarianteDeCiudadela(s);
                Assert.That(MaquetasDelMapa.ModeloDeAvanzada(1, false, false, s), Is.EqualTo("atalaya"));
                Assert.That(MaquetasDelMapa.ModeloDeAvanzada(2, false, false, s), Is.EqualTo("fuerte"));
                Assert.That(MaquetasDelMapa.ModeloDeAvanzada(3, false, false, s), Is.EqualTo(c));
                // en ruinas, sin atalaya (vencida, desaparece)
                Assert.That(MaquetasDelMapa.ModeloDeAvanzada(1, true, false, s), Is.Null);
                Assert.That(MaquetasDelMapa.ModeloDeAvanzada(2, true, false, s), Is.EqualTo("fuerte_ruina"));
                Assert.That(MaquetasDelMapa.ModeloDeAvanzada(3, true, false, s), Is.EqualTo(c + "_ruina"), "la misma ciudadela entera y en ruina");
                // con la mejora en marcha, la etapa siguiente en obras (la ciudadela, la suya); una ruina no se mejora
                Assert.That(MaquetasDelMapa.ModeloDeAvanzada(1, false, true, s), Is.EqualTo("fuerte_obras"));
                Assert.That(MaquetasDelMapa.ModeloDeAvanzada(2, false, true, s), Is.EqualTo(c + "_obras"), "la misma ciudadela en obras que entera");
                Assert.That(MaquetasDelMapa.ModeloDeAvanzada(3, false, true, s), Is.EqualTo(c));
                Assert.That(MaquetasDelMapa.ModeloDeAvanzada(2, true, true, s), Is.EqualTo("fuerte_ruina"));
            }
            // (la etapa fuera de 1 a 3, a la más cercana)
            Assert.That(MaquetasDelMapa.ModeloDeAvanzada(0, false, false, 0.3), Is.EqualTo("atalaya"));
            Assert.That(MaquetasDelMapa.ModeloDeAvanzada(7, false, false, 0.3), Does.StartWith("ciudadela_"));
        }

        [Test]
        public void LaCiudadelaEsDeLaSemillaYSalenLasDos()
        {
            int unos = 0, n = 400;
            for (int i = 0; i < n; i++)
            {
                double s = MaquetasDelPrototipo.Semilla(i * 3.7, i * 1.9 + 11);
                int v = MaquetasDelMapa.VarianteDeCiudadela(s);
                Assert.That(v, Is.EqualTo(MaquetasDelMapa.VarianteDeCiudadela(s)), "determinista");
                if (v == 1) unos++;
            }
            Assert.That(unos / (double)n, Is.InRange(0.35, 0.65));
        }

        [Test]
        public void LosMercenariosPorOrigenYEdificios()
        {
            Assert.That(MaquetasDelMapa.ModeloDeMercenarios(0), Is.EqualTo("mercenarios"));
            Assert.That(MaquetasDelMapa.ModeloDeMercenarios(1), Is.EqualTo("mercenarios_rayado"));
            Assert.That(MaquetasDelMapa.ModeloDeMercenarios(2), Is.EqualTo("mercenarios_rubia"));
            Assert.That(MaquetasDelMapa.ModeloDeMercenarios(4), Is.EqualTo("mercenarios_rayado"), "los demás orígenes, en vuelta");
            Assert.That(MaquetasDelMapa.PiezaMilitar(new[] { "taberna", "vivienda", "mercado", "barracon" }), Is.EqualTo("barracon"));
            Assert.That(MaquetasDelMapa.PiezaMilitar(new[] { "taberna", "galeriaDeTiro" }), Is.EqualTo("tiro"));
            Assert.That(MaquetasDelMapa.PiezaMilitar(new[] { "caballerizas", "mercado" }), Is.EqualTo("cuadras"));
            Assert.That(MaquetasDelMapa.PiezaMilitar(new[] { "taberna", "algoNuevo" }), Is.EqualTo("barracon"), "si no se reconoce, barracón");
            Assert.That(MaquetasDelMapa.PiezaMilitar(null), Is.EqualTo("barracon"));
        }

        [Test]
        public void LosBandidosPorVetaBosqueOSemilla()
        {
            Assert.That(MaquetasDelMapa.ModeloDeBandidos("veta-3", "bosque-1", 2, 0.4), Is.EqualTo("bandidos_2_2"), "con veta, la peña");
            Assert.That(MaquetasDelMapa.ModeloDeBandidos(null, "bosque-1", 3, 0.4), Is.EqualTo("bandidos_1_3"), "en un bosque, el claro");
            Assert.That(MaquetasDelMapa.ModeloDeBandidos("", "", 0, 0.4), Does.Match("^bandidos_[12]_1$"), "nivel 0 → 1");
            Assert.That(MaquetasDelMapa.ModeloDeBandidos(null, null, 9, 0.4), Does.EndWith("_3"), "nivel por encima de 3 → 3");
            int peñas = 0, n = 300;
            for (int i = 0; i < n; i++)
                if (MaquetasDelMapa.ModeloDeBandidos(null, null, 1, MaquetasDelPrototipo.Semilla(i * 2.3, i * 0.7)).StartsWith("bandidos_2")) peñas++;
            Assert.That(peñas / (double)n, Is.InRange(0.35, 0.65), "sin veta ni bosque, las dos por la semilla");
        }

        [Test]
        public void ElAlijoYSuMercanciaYLaObra()
        {
            Assert.That(new[] { "trigo", "madera", "piedra", "oro" }.Select(MaquetasDelMapa.PiezaDeMercancia), Is.EqualTo(new[] { "trigo", "madera", "piedra", "oro" }));
            Assert.That(MaquetasDelMapa.PiezaDeMercancia("cobre"), Is.EqualTo("trigo"));
            int unos = 0, n = 300;
            for (int i = 0; i < n; i++) if (MaquetasDelMapa.ModeloDeAlijo(MaquetasDelPrototipo.Semilla(i * 1.3, i * 4.1)) == "alijo_1") unos++;
            Assert.That(unos / (double)n, Is.InRange(0.35, 0.65));
            Assert.That(MaquetasDelMapa.ModeloDeObra(false), Is.EqualTo("obra_estacas"));
            Assert.That(MaquetasDelMapa.ModeloDeObra(true), Is.EqualTo("obra_mitad"));
        }

        [Test]
        public void ElKitVaPorFueraDeLosMurosYLejosDeLaPuerta()
        {
            foreach (var modelo in new[] { "fuerte_ruina", "ciudadela_1_ruina", "ciudadela_2_ruina", "atalaya", "fuerte", "ciudadela_2" })
                for (int i = 0; i < 60; i++)
                {
                    double s = i * 0.377, rm = MaquetasDelMapa.RadioDeMuro(modelo);
                    var completo = MaquetasDelMapa.SitiosDelKit(rm, s, true);
                    var pendiente = MaquetasDelMapa.SitiosDelKit(rm, s, false);
                    Assert.That(completo.Select(k => k.pieza), Is.EquivalentTo(new[] { "andamio", "andamio", "cabria", "sombrajo", "adobes", "sillares" }));
                    Assert.That(pendiente.Select(k => k.pieza), Is.EquivalentTo(new[] { "adobes", "sillares" }), "pendiente de materiales: los montones");
                    foreach (var (pieza, x, y, _) in completo)
                    {
                        double r = Math.Sqrt(x * x + y * y), a = Math.Atan2(y, x);
                        Assert.That(r, Is.GreaterThanOrEqualTo(rm), $"{modelo}: {pieza} pisa el muro");
                        Assert.That(Math.Abs(Math.IEEERemainder(a + Math.PI / 2, Math.PI * 2)), Is.GreaterThanOrEqualTo(0.74), $"{modelo}: {pieza} en la puerta");
                    }
                    Assert.That(MaquetasDelMapa.SitiosDelKit(rm, s, true), Is.EqualTo(completo), "determinista");
                }
        }

        [Test]
        public void LaHuellaDeLaAvanzadaPorEtapa()
        {
            var antes = (CosasDelMapa.HuellaMejorada, CosasDelMapa.HuellaDeAvanzada);
            try
            {
                CosasDelMapa.HuellaMejorada = true;
                CosasDelMapa.HuellaDeAvanzada = e => e == 1 ? 0.25 : e == 2 ? 0.48 : 0.72;
                Assert.That(CosasDelMapa.HuellaDeAvanzadaEn(1, false), Is.EqualTo(0.25));
                Assert.That(CosasDelMapa.HuellaDeAvanzadaEn(1, true), Is.EqualTo(0.48), "con la mejora en marcha, la de la etapa siguiente");
                Assert.That(CosasDelMapa.HuellaDeAvanzadaEn(3, true), Is.EqualTo(0.72));
                CosasDelMapa.HuellaMejorada = false;
                Assert.That(CosasDelMapa.HuellaDeAvanzadaEn(2, false), Is.EqualTo(0.8), "sin miniaturas, la del prototipo (0,8 u)");
            }
            finally { (CosasDelMapa.HuellaMejorada, CosasDelMapa.HuellaDeAvanzada) = antes; }
        }

        [Test]
        public void LosModelosDeCadaEtapa()
        {
            Assert.That(MaquetasDelMapa.ModelosDeEtapa(1), Is.EqualTo(new[] { "atalaya" }));
            Assert.That(MaquetasDelMapa.ModelosDeEtapa(2), Does.Contain("fuerte_ruina").And.Contain("fuerte_obras"));
            Assert.That(MaquetasDelMapa.ModelosDeEtapa(3).Length, Is.EqualTo(6), "las dos ciudadelas en sus tres estados");
            // todos los de las etapas están entre los que se cargan
            foreach (var e in new[] { 1, 2, 3 }) Assert.That(MaquetasDelMapa.NombresMenudos, Is.SupersetOf(MaquetasDelMapa.ModelosDeEtapa(e)));
        }

        [Test]
        public void ElEjeDeLosModelos()
        {
            // el gallardete de los mercenarios (6b) está en x = 0,06, y = −0,43 de Blender, lo más alto del modelo (0,42)
            var raiz = Resources.Load<GameObject>("MapaMundo/Asentamientos/mercenarios");
            if (raiz == null) Assert.Ignore("sin el modelo de los mercenarios");
            var malla = raiz.transform.Find("mercenarios_cerca").GetComponent<MeshFilter>().sharedMesh;
            // (la malla tal cual: `MaquetasDelMapa` la dibuja con su matriz, sin la del objeto)
            var v = malla.vertices.OrderByDescending(q => q.y).First();
            var esperado = MaquetasDelMapa.DeBlender(0.06, -0.43, 0.42);
            Assert.That(v.x, Is.EqualTo(esperado.x).Within(0.03), $"x: {v}");
            Assert.That(v.z, Is.EqualTo(esperado.z).Within(0.03), $"z: {v}");
            Assert.That(v.y, Is.EqualTo(esperado.y).Within(0.03), $"alto: {v}");
        }
    }
}
