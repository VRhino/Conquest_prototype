// Lo de la tanda 2B que no necesita Unity: qué hay bajo el clic, las acciones a la puerta (solo las que se pueden hacer), el tiempo de marcha por
// el grafo y lo que se reúne del almacén.
using System.Linq;
using BronzeAge.Contract.V1;
using NUnit.Framework;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.Tests
{
    public class CosasDelMapaTests
    {
        static CosaDelMapa Cosa(ClaseDeCosa clase, string id, double x, double y, double radioClic, string faccion = null) =>
            new CosaDelMapa { Clase = clase, Id = id, X = x, Y = y, RadioClic = radioClic, FaccionId = faccion };

        [Test]
        public void ElClicCogeLoQueTieneElPuntoEnSuZonaEnElOrdenDeLaLista()
        {
            var cosas = new[]
            {
                Cosa(ClaseDeCosa.Plaza, "as-1", 10, 10, 1.0),
                Cosa(ClaseDeCosa.Aeda, "aeda-1", 10.2, 10, 0.5),
                Cosa(ClaseDeCosa.Mercenarios, "camp-0", 30, 30, 1.1),
                new CosaDelMapa { Clase = ClaseDeCosa.Nodo, Id = "nodo-1", X = 10, Y = 10, RadioClic = 5 },
            };
            var bajo = CosasDelMapa.BajoElClic(cosas, 10.1, 10, 30);
            CollectionAssert.AreEqual(new[] { "aeda-1", "as-1" }, bajo.Select(c => c.Id), "el Aeda va antes que la plaza; el nodo no se elige");
            Assert.IsEmpty(CosasDelMapa.BajoElClic(cosas, 20, 20, 30));
            // Muy lejos (pocos píxeles por unidad), la zona mínima de 13 px agranda la del campamento.
            Assert.AreEqual(1, CosasDelMapa.BajoElClic(cosas, 33, 30, 3).Count);
        }

        static CosasDelMapa.Contexto Jugador(double x, double y, bool soldados = true, TipoColumna tipo = TipoColumna.ColumnaEjercito) => new CosasDelMapa.Contexto
        {
            HeroeId = "heroe-1", FaccionId = "f-1", X = x, Y = y, Lider = true, ConSoldados = soldados,
            Columna = new Ejercito { Id = "ej-1", Tipo = tipo, LiderId = "heroe-1" },
            RadioPuerta = 2, RadioEncuentro = 1, RadioInspeccion = 8, RadioProteccion = 10,
        };

        [Test]
        public void SoloSeEnsenaLoQueSePuedeHacer()
        {
            var propia = Cosa(ClaseDeCosa.Plaza, "as-1", 0, 0, 1, "f-1");
            var ajena = Cosa(ClaseDeCosa.Plaza, "as-2", 0, 0, 1, "f-2");
            // Con la columna personal se entra; un ejército entra a guarnecerla (paso 11: `entrarEnAsentamiento` lo rechaza con `columna.es_un_ejercito`).
            CollectionAssert.AreEqual(new[] { "Entrar" }, CosasDelMapa.Acciones(propia, Jugador(1.5, 0, tipo: TipoColumna.ColumnaPersonal)).Select(a => a.Texto));
            CollectionAssert.AreEqual(new[] { "Entrar con el ejército" }, CosasDelMapa.Acciones(propia, Jugador(1.5, 0)).Select(a => a.Texto));
            Assert.IsEmpty(CosasDelMapa.Acciones(propia, Jugador(5, 0)), "lejos de la puerta no se entra");
            var marchando = Jugador(1.5, 0);
            marchando.EnMarcha = true;
            Assert.IsEmpty(CosasDelMapa.Acciones(propia, marchando), "con la columna aún en marcha, nada a la puerta");
            var campamento = Cosa(ClaseDeCosa.Mercenarios, "camp-1", 0, 0, 1, null);
            CollectionAssert.Contains(CosasDelMapa.Acciones(campamento, Jugador(1.5, 0)).Select(a => a.Texto), "Entrar", "a la puerta de un campamento, parada, se entra");
            Assert.IsFalse(CosasDelMapa.Acciones(campamento, marchando).Any(a => a.Texto == "Entrar"), "llegando, aún no");
            CollectionAssert.AreEqual(new[] { "Atacar", "Inspeccionar" }, CosasDelMapa.Acciones(ajena, Jugador(0.5, 0)).Select(a => a.Texto));
            CollectionAssert.AreEqual(new[] { "Inspeccionar" }, CosasDelMapa.Acciones(ajena, Jugador(0.5, 0, tipo: TipoColumna.ColumnaPersonal)).Select(a => a.Texto),
                "un asedio solo lo abre un ejército");
            var j = Jugador(0.5, 0);
            j.CampamentosDeMercenarios.Add((5, 0));
            CollectionAssert.AreEqual(new[] { "Inspeccionar" }, CosasDelMapa.Acciones(ajena, j).Select(a => a.Texto), "junto a un campamento de mercenarios nadie ataca");
            var sinColumna = Jugador(0, 0);
            sinColumna.Columna = null;
            Assert.IsEmpty(CosasDelMapa.Acciones(ajena, sinColumna));

            var orden = CosasDelMapa.Acciones(Cosa(ClaseDeCosa.Bandidos, "bandidos-3", 0, 0, 1), Jugador(0.5, 0)).Single().Orden;
            Assert.AreEqual("bandidos-3", orden.Atacar.Objetivo.CampamentoId);
            var columna = Cosa(ClaseDeCosa.EjercitoAjeno, "ej-9", 0, 0, 0.8, "f-2");
            CollectionAssert.AreEqual(new[] { "Inspeccionar", "Perseguir", "Atacar" }, CosasDelMapa.Acciones(columna, Jugador(0.5, 0)).Select(a => a.Texto));
            var persiguiendo = Jugador(5, 0);
            persiguiendo.Persiguiendo = "ej-9";
            CollectionAssert.AreEqual(new[] { "Inspeccionar", "Dejar de perseguir" }, CosasDelMapa.Acciones(columna, persiguiendo).Select(a => a.Texto));
        }

        [Test]
        public void ElTiempoDeMarchaVaPorElGrafo()
        {
            // Tres nodos en L: de (0,0) a (10,0) llano y de (10,0) a (10,10) a doble coste. No hay arista directa en diagonal.
            var g = new GrafoNavegacion();
            g.Nodos.Add(new NodoGrafo { Id = 0, Posicion = new Punto { X = 0, Y = 0 } });
            g.Nodos.Add(new NodoGrafo { Id = 1, Posicion = new Punto { X = 10_000, Y = 0 } });
            g.Nodos.Add(new NodoGrafo { Id = 2, Posicion = new Punto { X = 10_000, Y = 10_000 } });
            g.Aristas.Add(new AristaGrafo { A = 0, B = 1, CosteMilesimas = 1000 });
            g.Aristas.Add(new AristaGrafo { A = 1, B = 2, CosteMilesimas = 2000 });
            var t = new TiempoDeMarcha(g);
            Assert.AreEqual(30, t.Coste(0, 0, 10, 10).Value, 1e-9);
            Assert.AreEqual(31, t.Coste(0, 1, 10, 10).Value, 1e-9, "más el tramo recto hasta el nodo más cercano");
            Assert.AreEqual("~30 s", TiempoDeMarcha.Texto(30, 60, 1), "30 u a 60 u/min con el reloj a ×1");
            Assert.AreEqual("~2 min", TiempoDeMarcha.Texto(30, 15, 1));
        }

        [Test]
        public void ElColorDeUnaFaccionEsElPrimarioDeSuSigilo()
        {
            var raiz = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Resources/MundoUI/Sigilo");
            string Leer(string n) => System.IO.File.ReadAllText(System.IO.Path.Combine(raiz, n));
            ConquestTactics.Mundo.Sigilos.Cargar(Leer("catalogoSigilos.json"), Leer("emblemas.json"), Leer("insignias.json"));
            var a = new Almacen();
            var f = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 1 };
            var dir = new DirectorioFacciones();
            dir.Facciones.Add(new FaccionDelDirectorio { Id = "f-7", Nombre = "Argos", Sigilo = new Sigilo { FormaId = "aspis", CampoId = "liso", EmblemaId = "toro", ColorPrimarioId = "azul",
                ColorSecundarioId = "oro", ColorEmblemaId = "blanco", OrlaId = "ninguna", ColorOrlaId = "oro" } });
            f.Entidades.Add(new Entidad { DirectorioFacciones = dir });
            a.Aplicar(new MensajeDeServidor { Foto = f });
            Assert.AreEqual(0x1f4e9cu, CosasDelMapa.ColorDeFaccion(a, "f-7"), "el azul del catálogo");
            Assert.AreNotEqual(0x1f4e9cu, CosasDelMapa.ColorDeFaccion(a, "f-8"), "sin sigilo, la paleta de reserva");
            StringAssert.Contains("clip-path", ConquestTactics.Mundo.Sigilos.Svg(null, 48), "sin sigilo, el neutro");
        }

        [Test]
        public void SeReunenLasCosasDelAlmacen()
        {
            var a = new Almacen();
            var f = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 1 };
            f.Entidades.Add(new Entidad { Asentamiento = new Asentamiento { Id = "as-1", Nombre = "Micenas", FaccionId = "f-2", Nivel = 1, Posicion = new Punto { X = 100_000, Y = 50_000 } } });
            f.Entidades.Add(new Entidad { CampamentoBandidos = new CampamentoBandidos { Id = "bandidos-1", Nivel = 2, Posicion = new Punto { X = 10_000, Y = 10_000 } } });
            var plan = new Plan();
            plan.Vertices.Add(new Vertice { P = new Punto { X = 0, Y = 0 }, EnMs = 0 });
            plan.Vertices.Add(new Vertice { P = new Punto { X = 10_000, Y = 0 }, EnMs = 1000 });
            f.Entidades.Add(new Entidad { Ejercito = new Ejercito { Id = "ej-2", FaccionId = "f-3", Plan = plan } });
            a.Aplicar(new MensajeDeServidor { Foto = f });
            var cosas = CosasDelMapa.Reunir(a, null, 500, new[] { new NodoRecurso { Id = "nodo-1", Tipo = "cobre", Posicion = new Punto { X = 1, Y = 1 } } }, 12);
            var plaza = cosas.Single(c => c.Clase == ClaseDeCosa.Plaza);
            Assert.AreEqual("Micenas", plaza.Nombre);
            Assert.AreEqual(12 * 0.1 * 0.8 * 0.62, plaza.Radio, 1e-9, "sin zona, la inicial de las reglas; nivel 1 a 0,62");
            Assert.AreEqual(CosasDelMapa.ColorDeFaccion(a, "f-2"), plaza.Color);
            Assert.AreEqual("Nivel 2", cosas.Single(c => c.Clase == ClaseDeCosa.Bandidos).Detalle);
            var columna = cosas.Single(c => c.Clase == ClaseDeCosa.EjercitoAjeno);
            Assert.AreEqual(5, columna.X, 1e-9, "la columna ajena, a mitad de su plan en el instante");
            Assert.IsTrue(cosas.Single(c => c.Clase == ClaseDeCosa.Nodo).BajoNiebla);
        }
    }
}
