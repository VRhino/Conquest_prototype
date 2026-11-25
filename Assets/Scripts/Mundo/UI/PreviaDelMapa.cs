// La previa del mapa (escena `Assets/Scenes/BronzeAge/PreviaDelMapa.unity`, menú `Conquest/Mundo/Previa del mapa`), como `DemoAnimaciones` para
// las animaciones: todo lo que se dibuja en el mapa de mundo sobre un trozo de terreno real, sin servidor ni cuenta, con el mismo `MapaDeMundo`
// (sombreadores, luz, cielo y cámara), el estilo «Mejorado» y sin niebla; y un panel para subir o bajar los tamaños en vivo.
//  * El terreno: el mundo en disco (el que bajó la última partida; `BancoDelMapa.DelDisco`), en un llano abierto con agua (río o costa) y
//    bosque a unas 20-35 u; un camino por donde anda lo que se mueve.
//  * Lo que se pone, cada cosa con su rótulo: lo fijo (plazas de nivel 1 a 5, un pueblo de cada recurso, aldeas con sus campos, avanzadas en
//    sus tres etapas, campamentos, obra, alijo y batalla), un nodo de cada recurso, una «familia real» (una plaza con una aldea y un pueblo a
//    3 u y una avanzada a su distancia mínima) y lo que se mueve (columna personal, ejército, una caravana de cada animal y la de fundación, el
//    Aeda con su burro y una columna ajena), andando en bucle por el camino; y dos columnas con su ruta y su destino como la tuya
//    (`MapaDeMundo.RutasComoLaMia`): una a campo abierto y otra contra la plaza de nivel 5 (la ruta, cortada en el borde de su huella).
//  * La cámara: la del juego (arrastrar gira, la rueda acerca); WASD o las flechas la llevan por la escena (no hay héroe al que seguir).
//  * El panel: «Lo que se mueve» (`ColumnasDelMapa.FactorDeFiguras`), «Lo fijo» (`CosasDelMapa.FactorDeFijos`) y su afinado por clase, y
//    «Nodos» (`NodosDelMapa.FactorDeNodos`), el ancho de la cinta de la ruta y el tamaño de la marca del destino (`RutasDelMapa`); el alto
//    del soldado y el radio de la plaza por nivel; «Restablecer», «Copiar valores» y andar o parar. Todo vuelve a los valores del código al salir: en el juego mandan ellos.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.UI
{
    public sealed class PreviaDelMapa : MonoBehaviour
    {
        /// <summary>Los prefabs de las tropas y los héroes (los modelos de las figuras se hornean de ellos).</summary>
        public VisualPrefabConfiguration Prefabs;

        public static PreviaDelMapa Viva { get; private set; }
        public bool Lista { get; private set; }
        public MapaDeMundo Mapa { get; private set; }
        public BancoDelMapa Banco { get; private set; }
        /// <summary>Lo que se mueve (sus ids) y si anda.</summary>
        public readonly List<string> Moviles = new List<string>();
        public bool Andando { get; private set; } = true;
        public Almacen Almacen => almacen;
        /// <summary>Los dos extremos del camino (u).</summary>
        public (double x, double y) CaminoA => caminoA;
        public (double x, double y) CaminoB => caminoB;

        /// <summary>Lleva la cámara a (x, y) (u).</summary>
        public void Mirar(double x, double y)
        {
            mira = (x, y);
            almacen.Buscar(TipoEntidad.Ejercito, "ej-banco").Ejercito.Posicion = BancoDelMapa.Pu(x, y);
        }

        static readonly string[] Tropas = { "milicia_lanceros", "lenadores", "mineros", "granjeros" };
        /// <summary>Los factores, con su nombre en el panel, y cómo se leen y se ponen.</summary>
        static readonly (string nombre, string clave, float min, float max, Func<double> leer, Action<double> poner)[] Factores =
        {
            ("Lo que se mueve", "figuras", 0.5f, 3, () => ColumnasDelMapa.FactorDeFiguras, v => ColumnasDelMapa.FactorDeFiguras = v),
            ("Lo fijo", "fijos", 0.5f, 3, () => CosasDelMapa.FactorDeFijos, v => CosasDelMapa.FactorDeFijos = v),
            ("  plazas", "plazas", 0.5f, 3, () => CosasDelMapa.FactorPlazas, v => CosasDelMapa.FactorPlazas = v),
            ("  pueblos y aldeas", "pueblos", 0.5f, 3, () => CosasDelMapa.FactorPueblos, v => CosasDelMapa.FactorPueblos = v),
            ("  avanzadas", "avanzadas", 0.5f, 3, () => CosasDelMapa.FactorAvanzadas, v => CosasDelMapa.FactorAvanzadas = v),
            ("  campamentos", "campamentos", 0.5f, 3, () => CosasDelMapa.FactorCampamentos, v => CosasDelMapa.FactorCampamentos = v),
            ("  obras, alijos y batalla", "menudo", 0.5f, 3, () => CosasDelMapa.FactorMenudo, v => CosasDelMapa.FactorMenudo = v),
            ("Nodos", "nodos", 0.5f, 3, () => NodosDelMapa.FactorDeNodos, v => NodosDelMapa.FactorDeNodos = v),
            // (los dos de la ruta, antes del factor de lo que se mueve)
            ("Ancho de la cinta (u)", "cinta", 0.04f, 0.4f, () => RutasDelMapa.AnchoDeCinta, v => RutasDelMapa.AnchoDeCinta = v),
            ("Tamaño de la marca", "marca", 0.5f, 3, () => RutasDelMapa.TamanoDeMarca, v => RutasDelMapa.TamanoDeMarca = v),
        };
        /// <summary>Las dos columnas con ruta y destino como la tuya: a campo abierto y contra la plaza de nivel 5.</summary>
        public const string RutaCampo = "previa-ruta-campo", RutaPlaza = "previa-ruta-plaza";
        /// <summary>De dónde salen y adónde van (u), por id; y la plaza contra la que va la segunda (centro y huella, u).</summary>
        public readonly Dictionary<string, ((double x, double y) desde, (double x, double y) hasta)> Rutas = new Dictionary<string, ((double, double), (double, double))>();
        double[] delCodigo;
        EstiloDeAsentamientos estiloAntes;
        bool pintarMiColumnaAntes;

        Almacen almacen;
        Sesion sesion;
        (double x, double y) mira, caminoA, caminoB;
        const double Velocidad = 0.2;
        readonly Dictionary<string, bool> haciaB = new Dictionary<string, bool>();
        readonly List<(string nombre, double x, double y, Label rotulo)> rotulos = new List<(string, double, double, Label)>();
        readonly Dictionary<string, Slider> deslizadores = new Dictionary<string, Slider>();
        Label lectura;
        float proximosAltos;
        readonly Dictionary<(double, double), double> altos = new Dictionary<(double, double), double>();
        Button botonAndar;
        VisualElement capaRotulos;

        void Awake()
        {
            Viva = this;
            delCodigo = Factores.Select(f => f.leer()).ToArray();
        }

        IEnumerator Start()
        {
            // (un fotograma: al entrar en Play, el menú de los asentamientos pone el estilo de su casilla; aquí manda «Mejorado»)
            yield return null;
            estiloAntes = MaquetasDelMapa.Estilo;
            pintarMiColumnaAntes = MapaDeMundo.PintarMiColumna;
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
            MapaDeMundo.PintarMiColumna = false;
            try { Montar(); }
            catch (Exception e) { Debug.LogError($"[Previa del mapa] no se pudo montar: {e.Message}"); yield break; }
            Panel();
            Lista = true;
            Debug.Log($"[Previa del mapa] mundo {Banco.Hash} ({Banco.OrigenDelMundo}); sitio: {Sitio}");
            Debug.Log($"[Previa del mapa] camino {caminoA.x:0.0},{caminoA.y:0.0} → {caminoB.x:0.0},{caminoB.y:0.0}; " + string.Join("; ", rotulos.Select(r => $"{r.nombre} ({r.x:0.0}, {r.y:0.0})")));
            Debug.Log($"[Previa del mapa] {rotulos.Count} cosas; WASD o las flechas llevan la cámara, arrastrar gira, la rueda acerca.");
        }

        void OnDestroy()
        {
            if (Viva == this) Viva = null;
            for (int i = 0; i < Factores.Length; i++) Factores[i].poner(delCodigo[i]);
            MaquetasDelMapa.Estilo = estiloAntes;
            MapaDeMundo.PintarMiColumna = pintarMiColumnaAntes;
            MapaDeMundo.CamaraFija = null;
        }

        // ------------------------------------------------------------------ el sitio y lo que se pone

        /// <summary>Lo que cumple el sitio elegido.</summary>
        public string Sitio { get; private set; } = "";

        /// <summary>El camino: 75 u desde el sitio, hacia donde no hay agua y hay menos bosque y cuesta.</summary>
        void Camino((double x, double y) o)
        {
            var b = Banco;
            double mejor = double.MaxValue, rumbo = 0;
            for (int i = 0; i < 16; i++)
            {
                double a = i * Math.PI / 8, coste = 0, h0 = b.Relieve.AlturaEn(o.x, o.y);
                for (double t = -5; t <= 70; t += 2.5)
                {
                    double x = o.x + t * Math.Cos(a), y = o.y + t * Math.Sin(a);
                    if (b.Relieve.AguaEn(x, y) || b.Cauces.ACauce(x, y, 3) < 2) coste += 1000;
                    coste += b.Bosque.En(x, y) / 10.0 + Math.Abs(b.Relieve.AlturaEn(x, y) - h0) / 5;
                }
                if (coste < mejor) { mejor = coste; rumbo = a; }
            }
            caminoA = (o.x - 5 * Math.Cos(rumbo), o.y - 5 * Math.Sin(rumbo));
            caminoB = (o.x + 70 * Math.Cos(rumbo), o.y + 70 * Math.Sin(rumbo));
            for (double t = -5; t <= 70; t += 5) b.Ocupar(o.x + t * Math.Cos(rumbo), o.y + t * Math.Sin(rumbo), 2.5);
        }

        (double x, double y) Junto(double t, double lado)
        {
            double dx = caminoB.x - caminoA.x, dy = caminoB.y - caminoA.y, l = Math.Sqrt(dx * dx + dy * dy);
            return (caminoA.x + dx * t + -dy / l * lado, caminoA.y + dy * t + dx / l * lado);
        }

        void Montar()
        {
            Banco = BancoDelMapa.DelDisco("previa");
            var b = Banco;
            var o = b.SitioDeHierba(60, out var nota);
            Sitio = nota;
            b.Origen = o;
            b.DesdeElOrigen = true;
            b.BosqueMaximo = 140;
            Camino(o);
            double f = CosasDelMapa.FactorDeFijos;
            // junto al camino, para comparar tamaños con lo que anda: un campamento de mercenarios y un nodo de piedra
            // (en tierra y sin pisar nada: si junto al camino no cabe, donde quepa)
            (double x, double y)? JuntoLibre(double t, double lado, double r)
            {
                foreach (var dt in new[] { 0.0, 0.04, -0.04, 0.08, -0.08 })
                {
                    var (x, y) = Junto(t + dt, lado);
                    if (b.Libre(x, y, r)) { b.Ocupar(x, y, r); return (x, y); }
                }
                return b.Buscar(r);
            }
            if (JuntoLibre(0.32, 3 + 1.2 * f, 1.3 * f) is (double mx, double my)) b.Mercenarios(0, "barracon", mx, my);
            if (JuntoLibre(0.38, -4, 2) is (double nx, double ny)) b.Nodo("piedra", nx, ny);
            // la primera ruta como la tuya: a campo abierto, de unas 18 u hasta un sitio libre (antes que lo fijo: con sitio de sobra)
            if (b.Buscar(2.5) is (double rx, double ry))
                foreach (var (d, k) in new[] { 18.0, 14, 22 }.SelectMany(d => Enumerable.Range(0, 16).Select(k => (d, k))))
                {
                    double a = k * Math.PI / 8, sx = rx + d * Math.Cos(a), sy = ry + d * Math.Sin(a);
                    if (!b.Libre(sx, sy, 1.5)) continue;
                    b.Ocupar(sx, sy, 1.5);
                    Rutas[RutaCampo] = ((sx, sy), (rx, ry));
                    break;
                }
            // lo fijo
            foreach (var (nivel, zona) in new[] { (1u, 16.0), (2u, 25.0), (3u, 35.0), (4u, 45.0), (5u, 55.0) })
                if (b.Buscar(BancoDelMapa.RadioParaPlaza(nivel, zona, nivel >= 2)) is (double x, double y))
                    b.Plaza($"plaza de nivel {nivel}", nivel, zona, nivel >= 2, "f-1", x, y);
            string madre = b.Foto.Entidades.Select(e => e.Asentamiento).FirstOrDefault(a => a != null)?.Id ?? "previa-madre";
            foreach (var recurso in new[] { "piedra", "cobre", "estano", "hierro", "oro" })
                if (b.Buscar(3.5 * f) is (double x, double y)) b.PuebloExtractor(recurso, 2, madre, x, y);
            foreach (var (cultivo, nivel) in new[] { ("trigo", 1u), ("trigo", 2u), ("trigo", 3u), ("ganado", 2u) })
                if (b.Buscar(4.5 * f) is (double x, double y)) b.Aldea(cultivo, nivel, madre, x, y);
            foreach (var (nombre, etapa, ciudadela) in new[] { ("atalaya", 1u, 0), ("fuerte", 2u, 0), ("ciudadela", 3u, 1) })
                if (b.Sitio(BancoDelMapa.RadioParaAvanzada(etapa, 3 * f), ciudadela == 0 ? null : (Func<double, bool>)(sem => MaquetasDelMapa.VarianteDeCiudadela(sem) == ciudadela)) is (double x, double y))
                    b.Avanzada(nombre, etapa, false, TipoObra.NoEspecificado, false, madre, x, y);
            if (b.Sitio(3 * f) is (double bx, double by)) b.Bandidos(2, false, bx, by);
            if (b.Sitio(2.5 * f) is (double ox, double oy)) b.Obra("obra arrancada", TipoObra.ObraPueblo, true, madre, ox, oy);
            if (b.Sitio(1.5 * f) is (double ax, double ay)) b.Alijo("alijo de trigo", "trigo", ax, ay);
            if (b.Sitio(3 * f) is (double tx, double ty)) b.Batalla(tx, ty);
            // la segunda ruta como la tuya: a la plaza de nivel 5 (desde 25 u de su centro)
            if (b.Sitios.Where(k => k.nombre.StartsWith("plaza de nivel 5")).Select(k => ((double, double)?)(k.x, k.y)).FirstOrDefault() is (double px, double py))
                // (un sitio libre a 20-35 u; si no lo hay, en tierra)
                foreach (var libre in new[] { true, false })
                {
                    foreach (var (d, k) in new[] { 25.0, 20, 30, 35 }.SelectMany(d => Enumerable.Range(0, 16).Select(k => (d, k))))
                    {
                        double a = k * Math.PI / 8, sx = px + d * Math.Cos(a), sy = py + d * Math.Sin(a);
                        if (libre ? !b.Libre(sx, sy, 0.8) : b.Relieve.AguaEn(sx, sy)) continue;
                        Rutas[RutaPlaza] = ((sx, sy), (px, py));
                        break;
                    }
                    if (Rutas.ContainsKey(RutaPlaza)) break;
                }
            // los nodos: uno de cada recurso del mundo (el de piedra, ya junto al camino)
            foreach (var recurso in b.Artefacto.Nodos.Select(n => n.Tipo).Distinct().Where(t => t != "piedra").OrderBy(t => t).ToList())
                if (b.Buscar(2) is (double x, double y)) b.Nodo(recurso, x, y);
            // la familia real: una plaza de nivel 3 con una aldea y un pueblo a 3 u y una avanzada a su distancia mínima (zona × 0,05 + 0,1)
            if (b.Buscar(Math.Max(BancoDelMapa.RadioParaPlaza(3, 35, true) + 2 * BancoDelMapa.RadioParaAvanzada(1, 0), BancoDelMapa.HuellaDePlaza(3, 35) * 1.8 + 1)) is (double fx, double fy))
            {
                var p = b.Plaza("familia real: plaza de nivel 3", 3, 35, true, "f-1", fx, fy);
                b.Aldea("trigo", 2, p.Id, fx + 3, fy);
                b.PuebloExtractor("piedra", 2, p.Id, fx - 1.5, fy + 2.6);
                // (a la distancia mínima del motor, 1,85 u; o, si su maqueta se montaría en la de la plaza, justo fuera: el radio de la plaza con
                // su muralla, el de la avanzada y la holgura)
                double dv = Math.Max(1.85, CosasDelMapa.HuellaConMuralla(CosasDelMapa.HuellaDePlaza(3.5, 3, fx, fy), 3, fx, fy, true) + BancoDelMapa.RadioParaAvanzada(1, 0));
                b.Avanzada($"avanzada a {dv:0.00} u (mínimo del motor 1,85 u)", 1, false, TipoObra.NoEspecificado, false, p.Id, fx - 0.5 * dv, fy - 0.866 * dv);
            }
            // lo que se mueve, por el camino
            var mas = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 3 };
            var red = new RedCaminos();
            var tramo = new TramoDeRed { Id = "previa-camino", Escalon = 2 };
            for (double t = 0; t <= 1.0001; t += 0.05) { var (x, y) = Junto(t, 0); tramo.Puntos.Add(BancoDelMapa.Pu(x, y)); }
            red.Tramos.Add(tramo);
            mas.Entidades.Add(new Entidad { RedCaminos = red });
            Columna(mas, "previa-personal", new[] { "heroe-personal" }, Tropas.Take(2).ToList(), false, "f-1");
            Columna(mas, "previa-ejercito", new[] { "heroe-ejercito-1", "heroe-ejercito-2" }, Tropas, true, "f-1");
            Caravana(mas, "previa-buey", TipoCaravana.CaravanaComercial, new[] { ("basico", "buey") });
            Caravana(mas, "previa-caballo", TipoCaravana.CaravanaComercial, new[] { ("reforzado", "caballo") });
            Caravana(mas, "previa-camello", TipoCaravana.CaravanaComercial, new[] { ("basico", "camello") });
            Caravana(mas, "previa-fundacion", TipoCaravana.CaravanaDeFundacion, new (string, string)[0]);
            mas.Entidades.Add(new Entidad { AedaItinerante = new AedaItinerante { Id = "previa-aeda", Posicion = BancoDelMapa.Pu(caminoA.x, caminoA.y) } });
            Moviles.Add("previa-aeda");
            Columna(mas, "previa-ajena", new[] { "heroe-ajeno" }, Tropas.Skip(2).ToList(), false, "f-2");
            // las dos rutas como la tuya (no van con lo que se mueve: no se paran ni se aparcan)
            foreach (var (id, de, a) in Rutas.Select(k => (k.Key, k.Value.desde, k.Value.hasta)).ToList())
            {
                Columna(mas, id, new[] { "heroe-" + id }, Tropas.Take(1).ToList(), false, "f-1");
                Moviles.Remove(id);
                rotulos.Add((id == RutaCampo ? "ruta a campo abierto" : "ruta contra la plaza de nivel 5", de.x, de.y, null));
            }
            foreach (var (nombre, x, y) in b.Sitios) rotulos.Add((nombre, x, y, null));
            mira = o;
            (almacen, sesion, Mapa) = b.Montar("Previa del mapa", o, mas);
            // (sin las zonas de las plazas: en la partida nunca hay cinco plazas tan juntas; la zona sigue contando para su tamaño)
            Mapa.Zonas = false;
            Mapa.TecladoDeCamara = false;
            for (int i = 0; i < Moviles.Count; i++) Andar(Moviles[i], i);
            foreach (var id in Rutas.Keys) { Mapa.RutasComoLaMia.Add(id); AndarRuta(id); }
            Mapa.RehacerRayas();
            // los modelos de las figuras: el horno, como en la partida
            if (Prefabs != null)
            {
                var clase = ClaseDeHeroe.Leer(b.Datos).FirstOrDefault();
                var piezas = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
                string Primera(List<Data.Avatar.AvatarPartDefinition> l) => l?.FirstOrDefault(p => !string.IsNullOrEmpty(p.id))?.id;
                Mapa.gameObject.AddComponent<HornoDeFiguras>().Iniciar(Prefabs, id => new SelectorDePersonaje.HeroeParaVer { Nombre = id, Clase = clase, Armadura = "media",
                    Cabeza = Primera(piezas?.faceParts), Pelo = Primera(piezas?.hairParts), Barba = Primera(piezas?.beardParts), Cejas = Primera(piezas?.eyebrowsParts) });
            }
            else Debug.LogWarning("[Previa del mapa] sin Prefabs: las figuras, de bloques");
        }

        void Columna(Foto vista, string id, IList<string> heroes, IList<string> tropas, bool ejercito, string faccion)
        {
            var c = new Ejercito { Id = id, FaccionId = faccion, Tipo = ejercito ? TipoColumna.ColumnaEjercito : TipoColumna.ColumnaPersonal, LiderId = heroes[0],
                Estado = EstadoColumna.ColumnaMarchando, Posicion = BancoDelMapa.Pu(caminoA.x, caminoA.y) };
            foreach (var h in heroes)
            {
                c.Participantes.Add(new Participante { HeroeId = h });
                vista.Entidades.Add(new Entidad { Heroe = new Heroe { Id = h, DisplayName = h, FaccionId = faccion, ClassDefinitionId = "" } });
            }
            for (int i = 0; i < tropas.Count; i++)
            {
                var sid = $"{id}-s{i}";
                c.EscuadronIds.Add(sid);
                vista.Entidades.Add(new Entidad { Escuadron = new Escuadron { Id = sid, HeroeId = heroes[i % heroes.Count], TropaId = tropas[i], Cantidad = 10,
                    Contenedor = new ContenedorEscuadron { EjercitoId = id } } });
            }
            vista.Entidades.Add(new Entidad { Ejercito = c });
            Moviles.Add(id);
        }

        void Caravana(Foto vista, string id, TipoCaravana tipo, (string tipo, string animal)[] carros)
        {
            var k = new Caravana { Id = id, Tipo = tipo, FaccionId = "f-1", Posicion = BancoDelMapa.Pu(caminoA.x, caminoA.y) };
            foreach (var (t, an) in carros) k.Carros.Add(new CarroCaravana { TipoCarro = t, Animal = an });
            vista.Entidades.Add(new Entidad { Caravana = k });
            Moviles.Add(id);
        }

        // ------------------------------------------------------------------ andar y parar

        (Func<Plan> leer, Func<Punto> donde, Action<Plan, Punto> poner) Plan(string id)
        {
            var e = almacen.Buscar(TipoEntidad.Ejercito, id)?.Ejercito;
            if (e != null) return (() => e.Plan, () => e.Posicion, (p, q) => { e.Plan = p; e.Posicion = q; e.Estado = p != null ? EstadoColumna.ColumnaMarchando : EstadoColumna.ColumnaEstacionada; });
            var k = almacen.Buscar(TipoEntidad.Caravana, id)?.Caravana;
            if (k != null) return (() => k.Plan, () => k.Posicion, (p, q) => { k.Plan = p; k.Posicion = q; });
            var a = almacen.Buscar(TipoEntidad.Aeda, id)?.AedaItinerante;
            return (() => a?.Plan, () => a?.Posicion, (p, q) => { if (a != null) { a.Plan = p; a.Posicion = q; } });
        }

        /// <summary>Echa a andar `id` desde donde está, de un extremo al otro del camino y vuelta, unas horas; `orden` los escalona al empezar.</summary>
        void Andar(string id, int orden = -1)
        {
            var (leer, donde, poner) = Plan(id);
            double ahora = sesion.AhoraMundo;
            (double x, double y) desde;
            bool b;
            if (orden >= 0)
            {
                // escalonados por el camino, la mitad a la ida y la otra mitad a la vuelta
                double t = (orden * 0.37) % 1.0;
                b = orden % 2 == 0;
                desde = Junto(b ? t : 1 - t, 0);
            }
            else
            {
                var pos = Instante.Posicion(leer(), donde(), ahora);
                desde = (pos.x / 1000, pos.y / 1000);
                b = !haciaB.TryGetValue(id, out var h) || h;
            }
            var plan = new Plan();
            plan.Vertices.Add(new Vertice { EnMs = (long)ahora, P = BancoDelMapa.Pu(desde.x, desde.y) });
            double tiempo = ahora;
            var antes = desde;
            for (int i = 0; i < 60; i++, b = !b)
            {
                var hasta = b ? caminoB : caminoA;
                tiempo += Math.Sqrt((hasta.x - antes.x) * (hasta.x - antes.x) + (hasta.y - antes.y) * (hasta.y - antes.y)) / Velocidad * 1000;
                plan.Vertices.Add(new Vertice { EnMs = (long)tiempo, P = BancoDelMapa.Pu(hasta.x, hasta.y) });
                antes = hasta;
            }
            poner(plan, BancoDelMapa.Pu(desde.x, desde.y));
        }

        /// <summary>Echa a andar la columna de ruta `id` desde su salida hasta su destino (despacio: la ruta se ve casi entera); al llegar, vuelve a
        /// salir (`Update`).</summary>
        void AndarRuta(string id)
        {
            var (de, a) = Rutas[id];
            double ahora = sesion.AhoraMundo, d = Math.Sqrt((a.x - de.x) * (a.x - de.x) + (a.y - de.y) * (a.y - de.y));
            var plan = new Plan();
            plan.Vertices.Add(new Vertice { EnMs = (long)ahora, P = BancoDelMapa.Pu(de.x, de.y) });
            plan.Vertices.Add(new Vertice { EnMs = (long)(ahora + d / VelocidadDeRuta * 1000), P = BancoDelMapa.Pu(a.x, a.y) });
            Plan(id).poner(plan, BancoDelMapa.Pu(de.x, de.y));
        }

        const double VelocidadDeRuta = 0.08;

        /// <summary>Adónde va la columna de ruta `id` (u; la segunda, al centro de la plaza, como el motor).</summary>
        public (double x, double y) DestinoDe(string id) => Rutas[id].hasta;

        /// <summary>Para las capturas: la columna de ruta `id` a `fraccion` de su camino (y sigue andando desde ahí).</summary>
        public void PonerRuta(string id, double fraccion)
        {
            var (de, a) = Rutas[id];
            double ahora = sesion.AhoraMundo, d = Math.Sqrt((a.x - de.x) * (a.x - de.x) + (a.y - de.y) * (a.y - de.y));
            var plan = new Plan();
            plan.Vertices.Add(new Vertice { EnMs = (long)(ahora - fraccion * d / VelocidadDeRuta * 1000), P = BancoDelMapa.Pu(de.x, de.y) });
            plan.Vertices.Add(new Vertice { EnMs = (long)(ahora + (1 - fraccion) * d / VelocidadDeRuta * 1000), P = BancoDelMapa.Pu(a.x, a.y) });
            Plan(id).poner(plan, BancoDelMapa.Pu(de.x, de.y));
        }

        void Parar(string id)
        {
            var (leer, donde, poner) = Plan(id);
            var plan = leer();
            if (plan == null) return;
            double ahora = sesion.AhoraMundo;
            var pos = Instante.Posicion(plan, donde(), ahora);
            // hacia dónde iba: el vértice siguiente
            var siguiente = plan.Vertices.FirstOrDefault(v => v.EnMs > ahora);
            if (siguiente != null)
            {
                double db = Math.Abs(siguiente.P.X / 1000.0 - caminoB.x) + Math.Abs(siguiente.P.Y / 1000.0 - caminoB.y);
                haciaB[id] = db < 1;
            }
            poner(null, BancoDelMapa.Pu(pos.x / 1000, pos.y / 1000));
        }

        public void AndarOParar(bool andar)
        {
            Andando = andar;
            foreach (var id in Moviles) if (andar) Andar(id); else Parar(id);
            Mapa.RehacerRayas();
            if (botonAndar != null) botonAndar.text = andar ? "Parar" : "Andar";
        }

        /// <summary>Para las capturas: lo que se mueve, parado en fila junto al camino desde `t0` (fracción del camino), cada `paso`, a un lado y
        /// otro a `lado` u.</summary>
        public void Aparcar(double t0, double paso, double lado)
        {
            AndarOParar(false);
            for (int i = 0; i < Moviles.Count; i++)
            {
                var (x, y) = Junto(t0 + paso * i, i % 2 == 0 ? -lado : lado);
                Plan(Moviles[i]).poner(null, BancoDelMapa.Pu(x, y));
            }
            Mapa.RehacerRayas();
        }

        // ------------------------------------------------------------------ el repaso del 23-11 (capturas de las plazas)

        /// <summary>Para las capturas: lleva la plaza cuyo nombre empieza por `nombre` a (x, y), con su zona (el tamaño no cambia).</summary>
        public void MoverPlaza(string nombre, double x, double y)
        {
            foreach (var e in almacen.Todas(Entidad.CuerpoOneofCase.Asentamiento))
            {
                var a = e.Asentamiento;
                if (a.Nombre == null || !a.Nombre.StartsWith(nombre)) continue;
                double dx = x - a.Posicion.X / 1000.0, dy = y - a.Posicion.Y / 1000.0;
                a.Posicion = BancoDelMapa.Pu(x, y);
                for (int i = 0; i < a.Zona.Count; i++) a.Zona[i] = BancoDelMapa.Pu(a.Zona[i].X / 1000.0 + dx, a.Zona[i].Y / 1000.0 + dy);
            }
            Mapa.RehacerRayas();
        }

        /// <summary>Dónde está la plaza cuyo nombre empieza por `nombre` (u).</summary>
        public (double x, double y)? DondePlaza(string nombre)
        {
            foreach (var e in almacen.Todas(Entidad.CuerpoOneofCase.Asentamiento))
                if (e.Asentamiento.Nombre != null && e.Asentamiento.Nombre.StartsWith(nombre)) return (e.Asentamiento.Posicion.X / 1000.0, e.Asentamiento.Posicion.Y / 1000.0);
            return null;
        }

        /// <summary>Un sitio en ladera para una plaza de radio `r` (u): en tierra, sin nada puesto cerca, con una pendiente media de 0,08 a 0,3
        /// (la más fuerte que haya) a lo ancho, a 30-150 u del origen.</summary>
        public (double x, double y)? Ladera(double r)
        {
            var b = Banco;
            (double x, double y)? mejor = null;
            double pm = 0, e = EscalaDelMapa.MetrosPorUnidad;
            foreach (var d in new[] { 30.0, 45, 60, 80, 100, 125, 150 })
                for (int k = 0; k < 24; k++)
                {
                    double x = b.Origen.x + d * Math.Cos(k * Math.PI / 12), y = b.Origen.y + d * Math.Sin(k * Math.PI / 12);
                    bool seco = true;
                    for (int i = 0; i < 12 && seco; i++)
                        for (double f = 0; f <= 1.6; f += 0.4)
                            if (b.Relieve.AguaEn(x + r * f * Math.Cos(i * Math.PI / 6), y + r * f * Math.Sin(i * Math.PI / 6))) { seco = false; break; }
                    if (!seco || rotulos.Exists(q => Math.Abs(q.x - x) < r * 2.5 + 6 && Math.Abs(q.y - y) < r * 2.5 + 6)) continue;
                    double gx = (b.Relieve.AlturaEn(x + r, y) - b.Relieve.AlturaEn(x - r, y)) / (2 * r * e);
                    double gy = (b.Relieve.AlturaEn(x, y + r) - b.Relieve.AlturaEn(x, y - r)) / (2 * r * e);
                    double p = Math.Sqrt(gx * gx + gy * gy);
                    if (p > pm && p < 0.3) { pm = p; mejor = (x, y); }
                }
            Debug.Log($"[Previa del mapa] ladera: pendiente {pm:0.000} en {mejor}");
            return pm >= 0.08 ? mejor : null;
        }

        /// <summary>Un llano seco y despejado de `largo` × 24 u (a lo largo de x) para una fila de plazas, a 40-200 u del origen; su comienzo.</summary>
        public (double x, double y)? Llano(double largo)
        {
            var b = Banco;
            (double x, double y)? mejor = null;
            double pm = double.MaxValue, e = EscalaDelMapa.MetrosPorUnidad;
            foreach (var d in new[] { 40.0, 60, 80, 100, 130, 160, 200 })
                for (int k = 0; k < 24; k++)
                {
                    double x = b.Origen.x + d * Math.Cos(k * Math.PI / 12), y = b.Origen.y + d * Math.Sin(k * Math.PI / 12);
                    bool vale = true;
                    double hmin = double.MaxValue, hmax = double.MinValue;
                    for (double u = -6; u <= largo + 6 && vale; u += 3)
                        for (double w = -12; w <= 12; w += 3)
                        {
                            if (b.Relieve.AguaEn(x + u, y + w) || b.Cauces.ACauce(x + u, y + w, 3) < 1.5) { vale = false; break; }
                            double h = b.Relieve.AlturaEn(x + u, y + w);
                            hmin = Math.Min(hmin, h); hmax = Math.Max(hmax, h);
                        }
                    if (!vale || rotulos.Exists(q => q.x > x - 14 && q.x < x + largo + 14 && Math.Abs(q.y - y) < 20)) continue;
                    double p = (hmax - hmin) / e;
                    if (p < pm) { pm = p; mejor = (x, y); }
                }
            return mejor;
        }

        // ------------------------------------------------------------------ los factores

        /// <summary>Pone el factor `clave` (figuras, fijos, plazas, pueblos, avanzadas, campamentos, menudo, nodos) y lo aplica en vivo.</summary>
        public void Fijar(string clave, double valor)
        {
            var f = Factores.First(x => x.clave == clave);
            f.poner(valor);
            if (deslizadores.TryGetValue(clave, out var s) && Math.Abs(s.value - valor) > 1e-4) s.SetValueWithoutNotify((float)valor);
            Mapa?.RehacerRayas();
        }

        public void Restablecer()
        {
            for (int i = 0; i < Factores.Length; i++) Fijar(Factores[i].clave, delCodigo[i]);
        }

        public string Valores() => string.Join("; ", Factores.Select(f => $"{f.clave}={f.leer().ToString("0.00", CultureInfo.InvariantCulture)}"));

        // ------------------------------------------------------------------ el panel y los rótulos

        void Panel()
        {
            var ajustes = ScriptableObject.CreateInstance<PanelSettings>();
            ajustes.themeStyleSheet = Resources.Load<ThemeStyleSheet>("MundoUI/TemaMundo");
            ajustes.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ajustes.referenceResolution = new Vector2Int(1920, 1080);
            ajustes.match = 0.5f;
            var hijo = new GameObject("Panel de la previa");
            hijo.SetActive(false);
            hijo.transform.SetParent(transform, false);
            var doc = hijo.AddComponent<UIDocument>();
            doc.panelSettings = ajustes;
            hijo.SetActive(true);
            var raiz = doc.rootVisualElement;
            raiz.pickingMode = PickingMode.Ignore;
            var estilo = Resources.Load<StyleSheet>("MundoUI/Mundo");
            if (estilo != null) raiz.styleSheets.Add(estilo);
            capaRotulos = new VisualElement { pickingMode = PickingMode.Ignore };
            capaRotulos.style.position = Position.Absolute;
            capaRotulos.style.left = capaRotulos.style.top = 0;
            capaRotulos.style.right = capaRotulos.style.bottom = 0;
            raiz.Add(capaRotulos);
            for (int i = 0; i < rotulos.Count; i++)
            {
                var l = new Label(rotulos[i].nombre) { pickingMode = PickingMode.Ignore };
                l.style.position = Position.Absolute;
                l.style.fontSize = 12;
                l.style.color = new Color(0.96f, 0.93f, 0.85f);
                l.style.backgroundColor = new Color(0.1f, 0.08f, 0.06f, 0.7f);
                l.style.paddingLeft = l.style.paddingRight = 4;
                l.style.borderTopLeftRadius = l.style.borderTopRightRadius = l.style.borderBottomLeftRadius = l.style.borderBottomRightRadius = 3;
                capaRotulos.Add(l);
                rotulos[i] = (rotulos[i].nombre, rotulos[i].x, rotulos[i].y, l);
            }
            var panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.right = 16;
            panel.style.top = 16;
            panel.style.width = 340;
            panel.style.paddingLeft = panel.style.paddingRight = panel.style.paddingTop = panel.style.paddingBottom = 10;
            panel.style.backgroundColor = new Color(0.12f, 0.1f, 0.08f, 0.9f);
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius = panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 6;
            var titulo = new Label("Previa del mapa");
            titulo.style.fontSize = 18;
            titulo.style.unityFontStyleAndWeight = FontStyle.Bold;
            titulo.style.color = new Color(0.95f, 0.88f, 0.7f);
            panel.Add(titulo);
            foreach (var (nombre, clave, min, max, leer, _) in Factores)
            {
                var s = new Slider(nombre, min, max) { value = (float)leer(), showInputField = true };
                s.labelElement.style.minWidth = 150;
                s.labelElement.style.color = new Color(0.9f, 0.86f, 0.78f);
                var campo = s.Q<TextField>();
                if (campo != null) { campo.style.width = 52; campo.Query<TextElement>().ForEach(t => t.style.color = new Color(0.1f, 0.08f, 0.06f)); }
                string k = clave;
                s.RegisterValueChangedCallback(ev => Fijar(k, Math.Round(ev.newValue, k == "cinta" ? 3 : 2)));
                deslizadores[clave] = s;
                panel.Add(s);
            }
            var mundo = new Label($"Mundo {Banco.Hash} · {Banco.OrigenDelMundo}");
            mundo.style.color = new Color(0.75f, 0.7f, 0.6f);
            mundo.style.fontSize = 11;
            panel.Add(mundo);
            var zonas = new Toggle("Zonas") { value = false };
            zonas.labelElement.style.color = new Color(0.9f, 0.86f, 0.78f);
            zonas.RegisterValueChangedCallback(ev => { Mapa.Zonas = ev.newValue; Mapa.RehacerRayas(); });
            panel.Add(zonas);
            lectura = new Label();
            lectura.style.color = new Color(0.9f, 0.86f, 0.78f);
            lectura.style.marginTop = 6;
            lectura.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(lectura);
            var fila = new VisualElement();
            fila.style.flexDirection = FlexDirection.Row;
            fila.style.marginTop = 8;
            fila.Add(new Button(Restablecer) { text = "Restablecer" });
            fila.Add(new Button(() => { GUIUtility.systemCopyBuffer = Valores(); Debug.Log("[Previa del mapa] copiado: " + Valores()); }) { text = "Copiar valores" });
            botonAndar = new Button(() => AndarOParar(!Andando)) { text = "Parar" };
            fila.Add(botonAndar);
            panel.Add(fila);
            var ayuda = new Label("WASD o flechas: mover · arrastrar: girar · rueda: acercar");
            ayuda.style.fontSize = 11;
            ayuda.style.color = new Color(0.75f, 0.7f, 0.6f);
            ayuda.style.marginTop = 6;
            panel.Add(ayuda);
            raiz.Add(panel);
            this.panel = panel;
        }

        VisualElement panel;
        /// <summary>Para las capturas: el panel a la vista o no (los rótulos siguen).</summary>
        public void VerPanel(bool ver) { if (panel != null) panel.style.display = ver ? DisplayStyle.Flex : DisplayStyle.None; }

        /// <summary>Lo que mide ahora: el alto del soldado (u) y el radio de la plaza de cada nivel (u, sin muralla).</summary>
        public string Lectura()
        {
            double soldado = 1.75 * ModelosDeFiguras.MetrosAEscena / EscalaDelMapa.MetrosPorUnidad;
            var plazas = new[] { (1u, 16.0), (2u, 25.0), (3u, 35.0), (4u, 45.0), (5u, 55.0) }
                .Select(p => $"{p.Item1}: {CosasDelMapa.HuellaDePlaza(p.Item2 * 0.1, p.Item1):0.0}");
            return $"Soldado: {soldado:0.00} u de alto\nPlaza (radio, u) · {string.Join(" · ", plazas)}";
        }

        void Update()
        {
            if (!Lista || Mapa == null) return;
            // la cámara por la escena: la columna del banco (a la que sigue) va donde se mira
            var teclado = Keyboard.current;
            var cam = Mapa.Vista;
            if (teclado != null && cam != null)
            {
                Vector2 v = Vector2.zero;
                if (teclado.wKey.isPressed || teclado.upArrowKey.isPressed) v.y += 1;
                if (teclado.sKey.isPressed || teclado.downArrowKey.isPressed) v.y -= 1;
                if (teclado.dKey.isPressed || teclado.rightArrowKey.isPressed) v.x += 1;
                if (teclado.aKey.isPressed || teclado.leftArrowKey.isPressed) v.x -= 1;
                if (v != Vector2.zero)
                {
                    // (en el mapa, z = −y: adelante de la cámara en el plano, en u)
                    var adelante = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                    var derecha = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
                    var d = (adelante * v.y + derecha * v.x).normalized;
                    float alto = Mathf.Max(1, cam.transform.position.y / EscalaDelMapa.MetrosPorUnidad);
                    double paso = Time.unscaledDeltaTime * Mathf.Clamp(alto * 0.8f, 4, 60);
                    mira = (mira.x + d.x * paso, mira.y - d.z * paso);
                    almacen.Buscar(TipoEntidad.Ejercito, "ej-banco").Ejercito.Posicion = BancoDelMapa.Pu(mira.x, mira.y);
                }
            }
            lectura.text = Lectura();
            // las columnas de ruta: al llegar, otra vez desde su salida
            foreach (var id in Rutas.Keys)
            {
                var c = almacen.Buscar(TipoEntidad.Ejercito, id)?.Ejercito;
                if (c?.Plan != null && Instante.RutaRestante(c.Plan, c.Posicion, sesion.AhoraMundo).Count < 2) AndarRuta(id);
            }
            // los rótulos, sobre lo suyo
            if (cam == null || capaRotulos?.panel == null) return;
            if (Time.unscaledTime >= proximosAltos)
            {
                // (la altura de cada rótulo: por encima de lo suyo, con su tamaño de ahora)
                proximosAltos = Time.unscaledTime + 0.5f;
                altos.Clear();
                foreach (var c in Mapa.Cosas)
                    if (c.Clase != ClaseDeCosa.Nodo || rotulos.Exists(r => Math.Abs(r.x - c.X) < 0.01 && Math.Abs(r.y - c.Y) < 0.01))
                        altos[(Math.Round(c.X, 1), Math.Round(c.Y, 1))] = c.Alto;
            }
            foreach (var (nombre, x, y, l) in rotulos)
            {
                double sobre = altos.TryGetValue((Math.Round(x, 1), Math.Round(y, 1)), out var h) ? h : 0.5;
                var donde = EscalaDelMapa.AEscena(x, y, Mapa.AlturaVisible(x, y) + (float)(sobre * 0.35 + 1.0) * EscalaDelMapa.MetrosPorUnidad);
                var cerca = (x - mira.x) * (x - mira.x) + (y - mira.y) * (y - mira.y) < 90 * 90;
                var pantalla = cam.WorldToViewportPoint(donde);
                bool ve = cerca && pantalla.z > 0 && pantalla.x > 0 && pantalla.x < 1 && pantalla.y > 0 && pantalla.y < 1;
                l.style.display = ve ? DisplayStyle.Flex : DisplayStyle.None;
                if (!ve) continue;
                // (en fracción de la vista sobre la capa, como los carteles del mapa: vale también con la cámara en una textura)
                l.style.left = pantalla.x * capaRotulos.layout.width - 40;
                l.style.top = (1 - pantalla.y) * capaRotulos.layout.height;
            }
        }
    }
}
