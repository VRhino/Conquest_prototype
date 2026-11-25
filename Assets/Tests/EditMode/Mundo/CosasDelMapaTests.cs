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

        /// <summary>«Mejorado»: un clic en los campos de una aldea no la elige (la columna marcha allí); en su núcleo, sí. El aro, 0,5 u; el clic,
        /// 0,62 u.</summary>
        [Test]
        public void UnClicEnLosCamposDeUnaAldeaMarcha()
        {
            var antes = (CosasDelMapa.HuellaMejorada, CosasDelMapa.HuellaDePueblo);
            try
            {
                CosasDelMapa.HuellaMejorada = true;
                CosasDelMapa.HuellaDePueblo = (tipo, nivel) => 0.5;
                var a = new Almacen();
                var f = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 1 };
                f.Entidades.Add(new Entidad { Pueblo = new Pueblo { Id = "al-1", AsentamientoMadreId = "as-1", TipoExtractor = "aldeaAgricola", NivelAldea = 3, Cultivo = "trigo",
                    Posicion = new Punto { X = 50_000, Y = 50_000 } } });
                a.Aplicar(new MensajeDeServidor { Foto = f });
                var cosas = CosasDelMapa.Reunir(a, null, 0, new NodoRecurso[0], 12);
                var aldea = cosas.Single(c => c.Clase == ClaseDeCosa.Aldea);
                Assert.AreEqual(0.5 * CosasDelMapa.FactorDeFijos, aldea.Radio, 1e-9, "el aro, solo el núcleo (lo fijo, al doble)");
                Assert.AreEqual(0.62 * CosasDelMapa.FactorDeFijos, aldea.RadioClic, 1e-9);
                Assert.AreEqual(new[] { aldea }, CosasDelMapa.BajoElClic(cosas, 50.3, 50, 60).ToArray(), "en el núcleo, la aldea");
                Assert.IsEmpty(CosasDelMapa.BajoElClic(cosas, 50 + 0.62 * CosasDelMapa.FactorDeFijos + 0.6, 50, 60), "en los campos, más allá del clic del núcleo, nada: la columna marcha");
            }
            finally { (CosasDelMapa.HuellaMejorada, CosasDelMapa.HuellaDePueblo) = antes; }
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

        /// <summary>Las etiquetas del mapa (tema 10, tanda 1, como el prototipo): la plaza en dos líneas (nombre; «Nivel N» con los avisos de Armería
        /// y de Aeda parado), la recordada tenue y solo con su nivel, los campamentos, las columnas (héroe y Facción; el ejército, su Facción; sin
        /// Facción, «Sin Facción»), la caravana suelta de una Facción, el Aeda de camino (el parado no), las Miradas (sus minutos; caducada, tenue) y
        /// el territorio bajo tu columna. Cuáles se pulsan.</summary>
        [Test]
        public void LasEtiquetasDelMapa()
        {
            var a = new Almacen();
            var f = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 1 };
            f.Entidades.Add(new Entidad { Faccion = new Faccion { Id = "f-1", Nombre = "Los Atridas" } });
            f.Entidades.Add(new Entidad { Faccion = new Faccion { Id = "f-2", Nombre = "Los de Tebas" } });
            f.Entidades.Add(new Entidad { Asentamiento = new Asentamiento { Id = "as-1", Nombre = "Micenas", FaccionId = "f-1", Nivel = 3, ArmeriaActiva = true, Posicion = new Punto { X = 100_000, Y = 50_000 } } });
            f.Entidades.Add(new Entidad { Asentamiento = new Asentamiento { Id = "as-2", Nombre = "Tebas", FaccionId = "f-2", Nivel = 2, Posicion = new Punto { X = 200_000, Y = 50_000 } } });
            f.Entidades.Add(new Entidad { FichaConocida = new FichaConocida { AsentamientoId = "as-3", Nombre = "Pilos", FaccionId = "f-2", Nivel = 4, Posicion = new Punto { X = 300_000, Y = 50_000 } } });
            f.Entidades.Add(new Entidad { AedaItinerante = new AedaItinerante { Id = "aeda-1", EnAsentamientoId = "as-2" } });
            f.Entidades.Add(new Entidad { AedaItinerante = new AedaItinerante { Id = "aeda-2", Posicion = new Punto { X = 50_000, Y = 50_000 } } });
            f.Entidades.Add(new Entidad { CampamentoMercenarios = new CampamentoMercenarios { Id = "camp-1", Posicion = new Punto { X = 10_000, Y = 10_000 } } });
            f.Entidades.Add(new Entidad { CampamentoBandidos = new CampamentoBandidos { Id = "bandidos-1", Nivel = 2, Posicion = new Punto { X = 20_000, Y = 10_000 } } });
            f.Entidades.Add(new Entidad { Heroe = new Heroe { Id = "heroe-2", DisplayName = "Aquiles", FaccionId = "f-2" } });
            var personal = new Ejercito { Id = "ej-2", FaccionId = "f-2", Tipo = TipoColumna.ColumnaPersonal, Posicion = new Punto { X = 30_000, Y = 10_000 } };
            personal.Participantes.Add(new Participante { HeroeId = "heroe-2" });
            f.Entidades.Add(new Entidad { Ejercito = personal });
            f.Entidades.Add(new Entidad { Ejercito = new Ejercito { Id = "ej-3", FaccionId = "f-2", Tipo = TipoColumna.ColumnaEjercito, Posicion = new Punto { X = 40_000, Y = 10_000 } } });
            f.Entidades.Add(new Entidad { Ejercito = new Ejercito { Id = "ej-4", Tipo = TipoColumna.ColumnaEjercito, Posicion = new Punto { X = 45_000, Y = 10_000 } } });
            f.Entidades.Add(new Entidad { Caravana = new Caravana { Id = "car-1", FaccionId = "f-2", Posicion = new Punto { X = 60_000, Y = 10_000 } } });
            f.Entidades.Add(new Entidad { Caravana = new Caravana { Id = "car-2", Posicion = new Punto { X = 65_000, Y = 10_000 } } });
            f.Entidades.Add(new Entidad { Mirada = new MiradaIntel { Id = "mir-1", Centro = new Punto { X = 70_000, Y = 10_000 }, Radio = 8000, ExpiraEnMs = 1000 + 5 * 60_000 - 1 } });
            f.Entidades.Add(new Entidad { Mirada = new MiradaIntel { Id = "mir-2", Centro = new Punto { X = 80_000, Y = 10_000 }, Radio = 8000, ExpiraEnMs = 500, LibreEnMs = 1000 + 2 * 60_000 } });
            a.Aplicar(new MensajeDeServidor { Foto = f });
            var cosas = CosasDelMapa.Reunir(a, null, 1000, new NodoRecurso[0], 12);
            // tu columna, en tierra de Tebas
            var mia = new Ejercito { Id = "ej-1", FaccionId = "f-1", Tipo = TipoColumna.ColumnaPersonal, TerritorioFaccionId = "f-2", Posicion = new Punto { X = 0, Y = 0 } };
            var miCosa = new CosaDelMapa { Clase = ClaseDeCosa.Ejercito, Id = "ej-1", X = 0, Y = 0, FaccionId = "f-1", Propia = true };
            var et = CosasDelMapa.Etiquetas(a, cosas, "f-1", mia, miCosa, 1000).ToDictionary(e => e.Id);
            var micenas = et["plaza:as-1"];
            Assert.AreEqual("Micenas", micenas.Texto);
            Assert.AreEqual("Nivel 3", micenas.Subtexto);
            CollectionAssert.AreEqual(new[] { "Armería" }, micenas.Avisos.Select(x => x.palabra), "la Armería activa, aviso de la plaza");
            Assert.AreEqual(40, micenas.AnchoSigilo);
            Assert.AreEqual("f-1", micenas.FaccionSigilo);
            CollectionAssert.AreEqual(new[] { "Aeda" }, et["plaza:as-2"].Avisos.Select(x => x.palabra), "el Aeda parado en Tebas, aviso de Tebas");
            Assert.IsFalse(et.ContainsKey("aeda:aeda-1"), "el Aeda parado no lleva etiqueta propia");
            var pilos = et["plaza:as-3"];
            Assert.IsTrue(pilos.Tenue, "la recordada, tenue");
            Assert.AreEqual("Nivel 4", pilos.Subtexto);
            Assert.IsEmpty(pilos.Avisos);
            Assert.AreEqual("Mercenarios", et["mercenarios:camp-1"].Texto);
            Assert.AreEqual("Bandidos · nivel 2", et["bandidos:bandidos-1"].Texto);
            Assert.AreEqual("Aquiles", et["columna:ej-2"].Texto);
            Assert.AreEqual("Los de Tebas", et["columna:ej-2"].Subtexto, "la Facción, debajo del héroe");
            Assert.AreEqual("Los de Tebas", et["columna:ej-3"].Texto, "el ejército, su Facción");
            Assert.AreEqual("Sin Facción", et["columna:ej-4"].Texto);
            Assert.AreEqual("Los de Tebas", et["caravana:car-1"].Texto);
            Assert.AreEqual(26, et["caravana:car-1"].AnchoSigilo);
            Assert.IsFalse(et.ContainsKey("caravana:car-2"), "la caravana sin Facción, sin etiqueta");
            Assert.AreEqual(CosasDelMapa.NombreDeAeda("aeda-2"), et["aeda:aeda-2"].Texto);
            Assert.AreEqual("Aeda", et["aeda:aeda-2"].Subtexto);
            Assert.AreEqual("5 min", et["mirada:mir-1"].Texto);
            Assert.IsFalse(et["mirada:mir-1"].Tenue);
            Assert.AreEqual("2 min", et["mirada:mir-2"].Texto, "caducada: hasta que la zona queda libre");
            Assert.IsTrue(et["mirada:mir-2"].Tenue);
            Assert.AreEqual("Los de Tebas", et["territorio:ej-1"].Texto, "bajo tu columna, de quién es la tierra");
            Assert.IsFalse(et["columna:ej-1"].Pulsable, "tu columna no se elige con un clic (como en el mapa)");
            CollectionAssert.AreEquivalent(new[] { "plaza:as-1", "plaza:as-2", "plaza:as-3", "mercenarios:camp-1", "bandidos:bandidos-1", "columna:ej-2", "columna:ej-3", "columna:ej-4",
                "caravana:car-1", "aeda:aeda-2" }, et.Values.Where(e => e.Pulsable).Select(e => e.Id), "las que se pulsan");
            // en tu tierra (o en tierra de nadie), sin aviso
            mia.TerritorioFaccionId = "f-1";
            Assert.IsFalse(CosasDelMapa.Etiquetas(a, cosas, "f-1", mia, miCosa, 1000).Any(e => e.Tipo == "territorio"));
        }

        /// <summary>Tema 10, punto 10: la batalla es tuya si combate en ella tu héroe o alguien de tu Facción (de cualquier lado); entonces lleva
        /// tu Facción y su color (el de su estandarte). La ajena, como siempre. Ninguna batalla lleva etiqueta.</summary>
        [Test]
        public void LaBatallaPropia()
        {
            var a = new Almacen();
            var f = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 1 };
            f.Entidades.Add(new Entidad { Faccion = new Faccion { Id = "f-1", Nombre = "Los Atridas" } });
            f.Entidades.Add(new Entidad { Heroe = new Heroe { Id = "heroe-1", FaccionId = "f-1" } });
            // (id, atacante, su Facción, defensor, su Facción)
            foreach (var (id, at, fat, de, fde) in new[] { ("bt-1", "heroe-9", "f-2", "heroe-1", "f-1"), ("bt-2", "heroe-7", "f-1", "heroe-8", "f-2"), ("bt-3", "heroe-9", "f-2", "heroe-8", (string)null) })
            {
                var t = new BattleTicket { Atacante = new BattleSide(), Defensor = new BattleSide() };
                t.Atacante.Participantes.Add(new BattleParticipantSnapshot { HeroeId = at, FaccionId = fat });
                var p = new BattleParticipantSnapshot { HeroeId = de };
                if (fde != null) p.FaccionId = fde;
                t.Defensor.Participantes.Add(p);
                f.Entidades.Add(new Entidad { Batalla = new Batalla { Id = id, Estado = EstadoBatalla.BatallaEnCurso, Punto = new Punto { X = 10_000, Y = 10_000 }, Ticket = t } });
            }
            a.Aplicar(new MensajeDeServidor { Foto = f });
            var cosas = CosasDelMapa.Reunir(a, "heroe-1", 1000, new NodoRecurso[0], 12).Where(c => c.Clase == ClaseDeCosa.Batalla).ToDictionary(c => c.Id);
            Assert.IsTrue(cosas["bt-1"].Propia, "defiendes en ella");
            Assert.IsTrue(cosas["bt-2"].Propia, "ataca alguien de tu Facción");
            Assert.IsFalse(cosas["bt-3"].Propia);
            Assert.AreEqual("f-1", cosas["bt-1"].FaccionId);
            Assert.AreEqual(CosasDelMapa.ColorDeFaccion(a, "f-1"), cosas["bt-1"].ColorDeBando);
            Assert.IsNull(cosas["bt-3"].FaccionId, "la ajena, como siempre");
            Assert.AreEqual(CosasDelMapa.BatallaColor, cosas["bt-1"].Color, "su color (el de las cajas) no cambia");
            Assert.IsFalse(CosasDelMapa.Etiquetas(a, cosas.Values, "f-1", null, null, 1000).Any(), "las batallas, sin etiqueta");
            Assert.IsFalse(CosasDelMapa.EsBatallaPropia(new Batalla(), "heroe-1", "f-1"), "sin ticket, no es tuya");
        }
    }
}
