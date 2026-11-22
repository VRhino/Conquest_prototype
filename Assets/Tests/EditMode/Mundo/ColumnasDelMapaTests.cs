// Las reglas puras de las figuras de una columna en el mapa (tema 7): los bloques de tropa, la disposición de la cabeza a la cola, lo que se saca
// de la proyección (tropa, carros, caravana suelta) y cómo se pone cada figura por el camino de la cabeza.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Tests
{
    public class ColumnasDelMapaTests
    {
        static TropaDeColumna Tropa(bool ejercito, params string[][] porHeroe) =>
            new TropaDeColumna { Ejercito = ejercito, PorHeroe = porHeroe.Select(x => x.ToList()).ToList() };

        static string[] N(int n, string tropa = "t") => Enumerable.Repeat(tropa, n).ToArray();

        [Test]
        public void EnUnaColumnaPersonalCadaEscuadraEsUnBloqueDeDosFilasDeTresYComoMuchoSeis()
        {
            var b = ColumnasDelMapa.BloquesDeTropa(Tropa(false, new[] { "lanceros", "lenadores" }, N(6, "mineros")));
            Assert.AreEqual(6, b.Count);
            Assert.That(b.All(x => x.filas == 2 && x.ancho == 3));
            CollectionAssert.AreEqual(new[] { "lanceros", "lanceros" }, b[0].tropaPorFila);
            Assert.AreEqual("lenadores", b[1].tropaPorFila[1]);
            Assert.AreEqual("mineros", b[5].tropaPorFila[0]);
        }

        [Test]
        public void EnUnEjercitoCadaHeroeEsUnBloqueConUnaFilaDeCuatroPorEscuadraYComoMuchoDoce()
        {
            var b = ColumnasDelMapa.BloquesDeTropa(Tropa(true, new[] { "a", "b", "c" }, N(0), N(8, "d"), N(5, "e")));
            CollectionAssert.AreEqual(new[] { 3, 8, 1 }, b.Select(x => x.filas));
            Assert.That(b.All(x => x.ancho == 4));
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, b[0].tropaPorFila);
        }

        [Test]
        public void DeLaCabezaALaColaHeroesTropaConSuEstandarteCarritoYCarrosDeDosEnDos()
        {
            var carros = new CarrosDeColumna { Carga = 2, Fundacion = true, Propio = true,
                Animales = { AnimalDeTiro.Camello, AnimalDeTiro.Caballo, AnimalDeTiro.Buey } };
            var f = ColumnasDelMapa.Disposicion(2, Tropa(false, new[] { "lanceros" }, new[] { "mineros" }), carros, "ej-1");
            // dos héroes a 0 y 0,15
            CollectionAssert.AreEqual(new[] { 0.0, 0.15 }, f.Where(x => x.Figura == FiguraDeColumna.Heroe).Select(x => Math.Round(x.Atras, 6)));
            // dos bloques de 2×3: filas a 0,30 y 0,45; el segundo, con el hueco de 0,08 de más
            var s = f.Where(x => x.Figura == FiguraDeColumna.Soldado).ToList();
            Assert.AreEqual(12, s.Count);
            CollectionAssert.AreEqual(new[] { 0.30, 0.45, 0.68, 0.83 }, s.Select(x => Math.Round(x.Atras, 6)).Distinct());
            CollectionAssert.AreEqual(new[] { -1.0, 0.0, 1.0 }, s.Take(3).Select(x => x.Lado));
            Assert.IsTrue(s[0].Estandarte);
            Assert.AreEqual(1, f.Count(x => x.Estandarte));
            Assert.AreEqual("lanceros", s[0].TropaId);
            Assert.AreEqual("mineros", s[11].TropaId);
            // el carrito detrás de la última fila: 0,83 + 0,1 + 0,13
            var carrito = f.Single(x => x.Figura == FiguraDeColumna.Carrito);
            Assert.AreEqual(0.83 + 0.1 + 0.13, carrito.Atras, 1e-9);
            // los dos de carga en pareja, a la escala de todo (×1) y a ±1,3, cada uno con su animal; el de fundación, el último y en el centro
            var c = f.Where(x => x.Figura == FiguraDeColumna.Carro).ToList();
            Assert.AreEqual(2, c.Count);
            Assert.AreEqual(c[0].Atras, c[1].Atras);
            CollectionAssert.AreEqual(new[] { -1.3, 1.3 }, c.Select(x => x.Lado));
            Assert.That(c.All(x => x.Escala == 1));
            CollectionAssert.AreEqual(new[] { AnimalDeTiro.Camello, AnimalDeTiro.Caballo }, c.Select(x => x.Animal));
            double tras = carrito.Atras + 0.045 + 0.04 + 0.325;
            Assert.AreEqual(tras, c[0].Atras, 1e-9);
            var fun = f.Last();
            Assert.AreEqual(FiguraDeColumna.CarroDeFundacion, fun.Figura);
            Assert.AreEqual(0, fun.Lado);
            Assert.AreEqual(tras + 0.085 + 0.04 + 0.325, fun.Atras, 1e-9);
        }

        [Test]
        public void SinNadieDelanteElPrimerCarroVaEnLaCabezaYComoMuchoCuatroJinetes()
        {
            var sola = ColumnasDelMapa.Disposicion(0, new TropaDeColumna(), new CarrosDeColumna { Carga = 1 }, "car-1");
            Assert.AreEqual(1, sola.Count);
            Assert.AreEqual(0, sola[0].Atras);
            Assert.AreEqual(AnimalDeTiro.Buey, sola[0].Animal);
            Assert.AreEqual(4, ColumnasDelMapa.Disposicion(7, new TropaDeColumna(), new CarrosDeColumna(), "x").Count);
        }

        [Test]
        public void ElAedaVaDelanteYSuBurroAUnLadoUnPasoDetras()
        {
            var f = ColumnasDelMapa.DeAeda();
            Assert.AreEqual(FiguraDeColumna.Aeda, f[0].Figura);
            Assert.AreEqual(FiguraDeColumna.Burro, f[1].Figura);
            Assert.AreEqual(0.1, f[1].Atras);
            Assert.AreEqual(0.9, f[1].Lado);
        }

        [Test]
        public void LaVarianteEsFijaPorSemillaYSitioYReparteLasCuatro()
        {
            Assert.AreEqual(ColumnasDelMapa.Variante("ej-7", 3), ColumnasDelMapa.Variante("ej-7", 3));
            var v = Enumerable.Range(0, 200).Select(i => ColumnasDelMapa.Variante("ej-7", i)).ToList();
            Assert.That(v.All(x => x >= 0 && x <= 3));
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, v.Distinct());
        }

        [Test]
        public void LaTropaSaleDeLosEscuadronesQueSeVenOSiNoDeLaFichaPublicaYLoQueFaltaVaConElUltimo()
        {
            var c = new Ejercito { Id = "ej-1", Tipo = TipoColumna.ColumnaEjercito, Participantes = { new Participante { HeroeId = "h-1" }, new Participante { HeroeId = "h-2" } },
                EscuadronIds = { "s-1", "s-2", "s-3", "s-4" } };
            var vistos = new[]
            {
                new Escuadron { Id = "s-1", HeroeId = "h-1", TropaId = "milicia_lanceros", Cantidad = 10, Contenedor = new ContenedorEscuadron { EjercitoId = "ej-1" } },
                new Escuadron { Id = "s-2", HeroeId = "h-1", TropaId = "lenadores", Cantidad = 10, Contenedor = new ContenedorEscuadron { EjercitoId = "ej-1" } },
                new Escuadron { Id = "s-9", HeroeId = "h-1", TropaId = "otra", Cantidad = 10, Contenedor = new ContenedorEscuadron { EjercitoId = "ej-2" } },
                new Escuadron { Id = "s-3", HeroeId = "h-1", TropaId = "muertos", Cantidad = 0, Contenedor = new ContenedorEscuadron { EjercitoId = "ej-1" } },
            };
            var heroes = new Dictionary<string, Heroe> { ["h-2"] = new Heroe { EscuadrasQueLleva = { new EscuadraQueLleva { TropaId = "mineros" } } } };
            var t = ColumnasDelMapa.TropaDe(c, vistos, id => heroes.TryGetValue(id, out var h) ? h : null);
            Assert.IsTrue(t.Ejercito);
            CollectionAssert.AreEqual(new[] { "milicia_lanceros", "lenadores" }, t.PorHeroe[0]);
            CollectionAssert.AreEqual(new[] { "mineros", null }, t.PorHeroe[1]);
            Assert.AreEqual(2, ColumnasDelMapa.Jinetes(c));
        }

        [Test]
        public void LosCarrosSonDosDeCargaComoMuchoElDeFundacionConBueyYElCarritoSiLlevaSuministro()
        {
            var caravanas = new Dictionary<string, Caravana>
            {
                ["k-1"] = new Caravana { Id = "k-1", Tipo = TipoCaravana.CaravanaComercial, Carros = { new CarroCaravana(), new CarroCaravana { Animal = "camello" } } },
                ["k-2"] = new Caravana { Id = "k-2", Tipo = TipoCaravana.CaravanaComercial },
                ["k-3"] = new Caravana { Id = "k-3", Tipo = TipoCaravana.CaravanaComercial, Carros = { new CarroCaravana { Animal = "caballo" } } },
            };
            var c = new Ejercito { Id = "ej-1", CaravanasAdjuntasIds = { "k-1", "k-2", "k-3" }, CaravanaFundacionId = "k-f",
                Suministro = new Cantidades { PorId = { ["trigo"] = 500 } } };
            var r = ColumnasDelMapa.CarrosDe(c, id => caravanas.TryGetValue(id, out var k) ? k : null);
            Assert.AreEqual(2, r.Carga);
            Assert.IsTrue(r.Fundacion);
            Assert.IsTrue(r.Propio);
            CollectionAssert.AreEqual(new[] { AnimalDeTiro.Camello, AnimalDeTiro.Buey, AnimalDeTiro.Buey }, r.Animales);
            Assert.IsFalse(ColumnasDelMapa.CarrosDe(new Ejercito { Suministro = new Cantidades { PorId = { ["trigo"] = 0 } } }, _ => null).Propio);
        }

        [Test]
        public void UnaCaravanaSueltaLlevaSuEscoltaYUnCarroPorCarroComoMuchoTres()
        {
            var k = new Caravana { Tipo = TipoCaravana.CaravanaComercial, EscoltaIds = { "a", "b", "c", "d" },
                Carros = { new CarroCaravana { Animal = "caballo" }, new CarroCaravana(), new CarroCaravana { Animal = "camello" }, new CarroCaravana() } };
            var (t, c) = ColumnasDelMapa.DeCaravana(k);
            Assert.AreEqual(3, t.PorHeroe[0].Count);
            Assert.AreEqual(3, c.Carga);
            CollectionAssert.AreEqual(new[] { AnimalDeTiro.Caballo, AnimalDeTiro.Buey, AnimalDeTiro.Camello }, c.Animales);
            var (ta, ca) = ColumnasDelMapa.DeCaravana(new Caravana { Escoltada = true });
            Assert.AreEqual(1, ta.PorHeroe[0].Count);
            Assert.AreEqual(1, ca.Carga);
            var (_, cf) = ColumnasDelMapa.DeCaravana(new Caravana { Tipo = TipoCaravana.CaravanaDeFundacion });
            Assert.IsTrue(cf.Fundacion);
            Assert.AreEqual(0, cf.Carga);
        }

        [Test]
        public void CadaFiguraSeColocaPorLongitudDeArcoHaciaAtrasConElRumboDeSuTramo()
        {
            // la cabeza en (10, 0), venía de (0, 0) y antes de (0, -10): un recodo
            var camino = new List<(double x, double y)> { (10, 0), (0, 0), (0, -10) };
            var a = ColumnasDelMapa.Colocar(camino, 4, 0);
            Assert.AreEqual(6, a.x, 1e-9); Assert.AreEqual(0, a.y, 1e-9); Assert.AreEqual(0, a.rumbo, 1e-9);
            var b = ColumnasDelMapa.Colocar(camino, 13, 0);
            Assert.AreEqual(0, b.x, 1e-9); Assert.AreEqual(-3, b.y, 1e-9); Assert.AreEqual(Math.PI / 2, b.rumbo, 1e-9);
            // más allá del final, recto por el último tramo
            var c = ColumnasDelMapa.Colocar(camino, 25, 0);
            Assert.AreEqual(0, c.x, 1e-9); Assert.AreEqual(-15, c.y, 1e-9);
            // el lado: a la izquierda de la marcha (como el prototipo: −sin, +cos)
            var d = ColumnasDelMapa.Colocar(camino, 4, 1);
            Assert.AreEqual(6, d.x, 1e-9); Assert.AreEqual(1, d.y, 1e-9);
        }

        [Test]
        public void ElRastroSeFormaHaciaElSurLaPrimeraVezYTrasUnSaltoYElPlanManda()
        {
            var r = new RastroDeColumna();
            r.Mover(5, 5);
            Assert.AreEqual(30, r.Cuantos);
            var atras = r.HaciaAtras(5, 5);
            Assert.AreEqual(5 + 29 * 0.3, atras.Last().y, 1e-9);
            r.Mover(5.01, 5);
            Assert.AreEqual(30, r.Cuantos);
            r.Mover(5.5, 5);
            Assert.AreEqual(31, r.Cuantos);
            r.Mover(50, 50);
            Assert.AreEqual(30, r.Cuantos);
            // con plan que llega a toda la columna, se va por el plan; si no, por el rastro
            var plan = new Plan { Vertices = { new Vertice { EnMs = 0, P = new Punto { X = 0, Y = 0 } }, new Vertice { EnMs = 10_000, P = new Punto { X = 10_000, Y = 0 } } } };
            var porPlan = new RastroDeColumna().Camino(plan, null, 5_000, 3);
            CollectionAssert.AreEqual(new[] { (5.0, 0.0), (0.0, 0.0) }, porPlan);
            var porRastro = new RastroDeColumna().Camino(plan, null, 5_000, 8);
            Assert.AreEqual(31, porRastro.Count);
        }
    }
}
