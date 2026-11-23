// Las figuras de una columna en el mapa de mundo (tema 7), en reglas puras: puerto de `FigurasDeColumna`, `bloquesDeTropa` y `mover` de
// `BronzeAgeClient/src/mundo3d/figuras.ts`, y de cómo `estado.ts` saca de la proyección la tropa y los carros de cada columna.
// De la cabeza a la cola: los héroes a caballo (como mucho 4, a 0,15), la tropa por bloques (la primera figura lleva el estandarte), el carrito
// del héroe si la columna lleva suministro y los carros de las caravanas de dos en dos, cada uno con su animal, el de fundación el
// último. Una caravana suelta es una columna sin héroes (su escolta y sus carros); un Aeda, él a pie y su burro a un lado y un paso detrás.
// Las distancias van en unidades de figura (las del prototipo): por `Tropa` (2) son unidades del mapa; el soldado mide 0,27 u.
// Cada figura se pone por longitud de arco hacia atrás sobre lo que ha andado la cabeza: el plan de la columna (`Instante`) si llega, si no el
// rastro de la cabeza (formado hacia el sur la primera vez, como el prototipo).
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo
{
    public enum FiguraDeColumna { Heroe, Soldado, Carrito, Carro, CarroDeFundacion, Aeda, Burro }

    public enum AnimalDeTiro { Buey, Caballo, Camello }

    /// <summary>Dónde va una figura de la columna: cuánto detrás de la cabeza y a qué lado (en unidades de figura; el lado, en décimas), a qué
    /// escala, y lo que la distingue (la tropa de su escuadra con su variante, el animal de un carro, el estandarte).</summary>
    public struct SitioDeFigura
    {
        public FiguraDeColumna Figura;
        public double Atras, Lado, Escala;
        public bool Estandarte;
        /// <summary>La tropa de su escuadra (`Escuadron.tropa_id` o `EscuadraQueLleva.tropa_id`); null si no se sabe.</summary>
        public string TropaId;
        /// <summary>De 0 a 3 (A–D), fija por la semilla de la columna y su sitio en ella.</summary>
        public int Variante;
        public AnimalDeTiro Animal;
        /// <summary>Un carro de carga reforzado (`Carro.tipo_carro`); si no, el básico.</summary>
        public bool Reforzado;
        /// <summary>El héroe que va en este caballo (los jinetes, por orden de los que van en la columna); null en lo demás.</summary>
        public string HeroeId;
    }

    /// <summary>La tropa de una columna: la tropa de cada escuadra de cada héroe, en el orden en que van; y si es un ejército.</summary>
    public sealed class TropaDeColumna
    {
        public List<List<string>> PorHeroe = new List<List<string>>();
        public bool Ejercito;
    }

    /// <summary>Lo que arrastra: carros de carga (sus caravanas enganchadas), el de fundación, el carrito del héroe (lleva suministro) y el
    /// animal de cada carro, por orden (los que faltan, buey).</summary>
    public sealed class CarrosDeColumna
    {
        public int Carga;
        public bool Fundacion, Propio;
        public List<AnimalDeTiro> Animales = new List<AnimalDeTiro>();
        /// <summary>Si cada carro de carga (por orden, como `Animales`) es reforzado.</summary>
        public List<bool> Reforzados = new List<bool>();
    }

    public static class ColumnasDelMapa
    {
        /// <summary>`MUNDO3D.tropa`: la escala de las figuras (el soldado, 0,135 de figura, mide 0,27 u).</summary>
        public const double Tropa = 2;
        public const int MaxJinetes = 4, MaxCarrosCaravana = 3, MaxEscolta = 3, MaxCarrosDeCargaEnColumna = 2;
        /// <summary>Como mucho, escuadras que se pintan: 6 en una columna personal (36 figuras), 12 en un ejército (48).</summary>
        public const int MaxEscuadrasPersonal = 6, MaxEscuadrasEjercito = 12;
        /// <summary>El paso entre filas (y entre héroes) y el hueco de más entre un bloque de tropa y el siguiente.</summary>
        public const double Paso = 0.15, HuecoEntreBloques = 0.08;
        /// <summary>El estandarte, en la figura del centro de la primera fila (si no, en la primera, como antes; para comparar).</summary>
        public static bool PortadorAlCentro = true;
        /// <summary>La escala de los carros de caravana: la misma que la de todo lo demás (Víctor, 2026-11-22: una sola escala, la de la tropa; el
        /// ×1,5 del prototipo se descarta).</summary>
        public const double EscalaCarroCaravana = 1;
        /// <summary>De metros de un modelo a unidades de figura: el soldado, de 1,75 m, mide 0,135.</summary>
        public const double MetrosAFigura = 0.135 / 1.75;
        /// <summary>El hueco libre entre el morro de un animal de tiro y lo que lleva delante (la última fila, el carrito, la pareja de carros
        /// anterior): medio cuerpo de soldado (m).</summary>
        public const double HuecoLibre = 0.875;
        /// <summary>Lo que ocupa por detrás de su sitio la última fila (m): el soldado, la contera de la lanza al hombro; el jinete, la cola del
        /// caballo. Medido en los modelos horneados.</summary>
        public const double ColaDeSoldado = 0.74, ColaDeJinete = 1.26;
        /// <summary>A qué lado va cada carro de una pareja (en décimas de figura): queda un palmo entre los dos.</summary>
        public const double LadoCarroPareja = 1.3;
        /// <summary>Lo que ocupa cada conjunto (animal, varas y carro) delante y detrás de su eje, en m y sin su escala: el largo real de los
        /// modelos horneados, por todos sus fotogramas (`CaravanaDeFigurasTests`, «LARGO»). El de fundación solo va con buey; el carrito, con el burro.</summary>
        public static (double frente, double cola) LargoReal(FiguraDeColumna f, AnimalDeTiro animal, bool reforzado)
        {
            if (f == FiguraDeColumna.Carrito) return (3.08, 0.67);
            if (f == FiguraDeColumna.CarroDeFundacion) return (4.44, 1.46);
            switch (animal)
            {
                case AnimalDeTiro.Caballo: return reforzado ? (4.83, 1.45) : (4.38, 1.04);
                case AnimalDeTiro.Camello: return reforzado ? (4.65, 1.45) : (4.21, 1.06);
                default: return reforzado ? (4.54, 1.45) : (4.09, 1.03);
            }
        }
        /// <summary>El burro del Aeda: un paso por detrás y a un lado.</summary>
        public const double AtrasBurro = 0.1, LadoBurro = 0.9;

        /// <summary>Los bloques de tropa (Víctor, 2026-10-31). En una columna personal, un bloque por escuadra de dos filas de tres; en un
        /// ejército, un bloque por héroe con una fila de cuatro por cada una de sus escuadras. Cada fila lleva la tropa de su escuadra.</summary>
        public static List<(int filas, int ancho, List<string> tropaPorFila)> BloquesDeTropa(TropaDeColumna t)
        {
            var r = new List<(int, int, List<string>)>();
            int quedan = t.Ejercito ? MaxEscuadrasEjercito : MaxEscuadrasPersonal;
            foreach (var deUno in t.PorHeroe)
            {
                var suyas = deUno.Take(Math.Max(0, quedan)).ToList();
                quedan -= suyas.Count;
                if (t.Ejercito)
                {
                    if (suyas.Count > 0) r.Add((suyas.Count, 4, suyas));
                }
                else
                    foreach (var tropa in suyas) r.Add((2, 3, new List<string> { tropa, tropa }));
            }
            return r;
        }

        /// <summary>Las figuras de una columna, de la cabeza a la cola. `semilla`: la de las variantes (el id de la columna).</summary>
        public static List<SitioDeFigura> Disposicion(int jinetes, TropaDeColumna tropa, CarrosDeColumna carros, string semilla,
            IReadOnlyList<string> heroes = null)
        {
            var r = new List<SitioDeFigura>();
            void Poner(FiguraDeColumna f, double atras, double lado, double escala = 1, string tropaId = null, bool estandarte = false,
                AnimalDeTiro animal = AnimalDeTiro.Buey, bool reforzado = false) =>
                r.Add(new SitioDeFigura { Figura = f, Atras = atras, Lado = lado, Escala = escala, TropaId = tropaId, Estandarte = estandarte,
                    Animal = animal, Reforzado = reforzado, Variante = Variante(semilla, r.Count) });
            jinetes = Math.Max(0, Math.Min(MaxJinetes, jinetes));
            for (int k = 0; k < jinetes; k++)
            {
                Poner(FiguraDeColumna.Heroe, k * Paso, 0);
                if (heroes != null && k < heroes.Count) { var h = r[r.Count - 1]; h.HeroeId = heroes[k]; r[r.Count - 1] = h; }
            }
            // entre filas de un bloque, el paso de siempre; entre un bloque y el siguiente, un poco más
            double ultima = jinetes * Paso - Paso;
            int fila = jinetes, soldados = 0;
            var bloques = BloquesDeTropa(tropa);
            for (int i = 0; i < bloques.Count; i++)
            {
                var b = bloques[i];
                for (int f = 0; f < b.filas; f++, fila++)
                {
                    ultima = fila * Paso + i * HuecoEntreBloques;
                    // el estandarte, en la figura del centro de la primera fila (en filas de cuatro, la segunda por la izquierda)
                    bool primera = soldados == 0;
                    for (int k = 0; k < b.ancho; k++, soldados++)
                        Poner(FiguraDeColumna.Soldado, ultima, k - (b.ancho - 1) / 2.0, tropaId: b.tropaPorFila[f], estandarte: primera && k == (PortadorAlCentro ? (b.ancho - 1) / 2 : 0));
                }
            }
            // los carros detrás de la última fila (sin nadie delante, el primero va en la cabeza): el carrito y luego los de las caravanas, de
            // dos en dos (Víctor, 2026-11-04); si son impares, el último solo, en el centro. Entre el morro de cada fila de carros y lo de delante
            // (lo que ocupa por detrás), `HuecoLibre`, con el largo real de cada conjunto
            double? detras = jinetes + soldados > 0 ? ultima + (soldados > 0 ? ColaDeSoldado : ColaDeJinete) * MetrosAFigura : (double?)null;
            void Enganchar(List<(FiguraDeColumna f, AnimalDeTiro a, bool r)> filaDeCarros, double escala)
            {
                double frente = filaDeCarros.Max(c => LargoReal(c.f, c.a, c.r).frente) * MetrosAFigura * escala;
                double cola = filaDeCarros.Max(c => LargoReal(c.f, c.a, c.r).cola) * MetrosAFigura * escala;
                double atras = detras == null ? 0 : detras.Value + HuecoLibre * MetrosAFigura + frente;
                detras = atras + cola;
                for (int k = 0; k < filaDeCarros.Count; k++)
                    Poner(filaDeCarros[k].f, atras, filaDeCarros.Count == 1 ? 0 : (k == 0 ? -1 : 1) * LadoCarroPareja, escala, animal: filaDeCarros[k].a,
                          reforzado: filaDeCarros[k].r);
            }
            if (carros.Propio) Enganchar(new List<(FiguraDeColumna, AnimalDeTiro, bool)> { (FiguraDeColumna.Carrito, AnimalDeTiro.Buey, false) }, 1);
            int n = carros.Carga + (carros.Fundacion ? 1 : 0);
            (FiguraDeColumna, AnimalDeTiro, bool) CarroN(int k) => (carros.Fundacion && k == n - 1 ? FiguraDeColumna.CarroDeFundacion : FiguraDeColumna.Carro,
                k < carros.Animales.Count ? carros.Animales[k] : AnimalDeTiro.Buey, k < carros.Reforzados.Count && carros.Reforzados[k]);
            for (int k = 0; k < n; k += 2)
                Enganchar(k + 1 < n ? new List<(FiguraDeColumna, AnimalDeTiro, bool)> { CarroN(k), CarroN(k + 1) } : new List<(FiguraDeColumna, AnimalDeTiro, bool)> { CarroN(k) },
                    EscalaCarroCaravana);
            return r;
        }

        /// <summary>Un Aeda itinerante: él delante y su burro a su lado, un paso por detrás.</summary>
        public static List<SitioDeFigura> DeAeda() => new List<SitioDeFigura>
        {
            new SitioDeFigura { Figura = FiguraDeColumna.Aeda, Escala = 1 },
            new SitioDeFigura { Figura = FiguraDeColumna.Burro, Atras = AtrasBurro, Lado = LadoBurro, Escala = 1 },
        };

        /// <summary>La variante (0 a 3, A–D) de la figura `i` de la columna `semilla`: fija entre fotogramas y sesiones (FNV-1a, no
        /// `string.GetHashCode`, que cambia en cada proceso).</summary>
        public static int Variante(string semilla, int i)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (var ch in semilla ?? "") { h ^= ch; h *= 16777619; }
                h ^= (uint)i; h *= 16777619;
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (int)(h % 4);
            }
        }

        // ------------------------------------------------------------------ de la proyección

        static AnimalDeTiro Animal(string a) => a == "caballo" ? AnimalDeTiro.Caballo : a == "camello" ? AnimalDeTiro.Camello : AnimalDeTiro.Buey;

        /// <summary>Los héroes a caballo que se pintan: los que van en ella, de 1 a 4.</summary>
        public static int Jinetes(Ejercito c) => Math.Max(1, Math.Min(MaxJinetes, c.Participantes.Count));

        /// <summary>La tropa de una columna. De cada héroe que va en ella, sus escuadrones de la columna que se ven (`Escuadron` con su
        /// contenedor en ella: los de tu Facción) o, si no, las escuadras de su ficha pública (`Heroe.escuadras_que_lleva`); los escuadrones de
        /// la columna que no se sabe de quién son, con el último héroe (sin tropa conocida).</summary>
        public static TropaDeColumna TropaDe(Ejercito c, IEnumerable<Escuadron> escuadronesVistos, Func<string, Heroe> heroe)
        {
            var t = new TropaDeColumna { Ejercito = c.Tipo == TipoColumna.ColumnaEjercito };
            var deLaColumna = escuadronesVistos.Where(s => s.Cantidad > 0 && (s.Contenedor?.EjercitoId == c.Id || c.EscuadronIds.Contains(s.Id))).ToList();
            int contadas = 0;
            foreach (var p in c.Participantes)
            {
                var suyos = deLaColumna.Where(s => s.HeroeId == p.HeroeId).Select(s => s.TropaId).ToList();
                if (suyos.Count == 0) suyos = heroe(p.HeroeId)?.EscuadrasQueLleva.Select(e => e.TropaId).ToList() ?? new List<string>();
                contadas += suyos.Count;
                t.PorHeroe.Add(suyos);
            }
            int faltan = c.EscuadronIds.Count - contadas;
            if (faltan > 0)
            {
                if (t.PorHeroe.Count == 0) t.PorHeroe.Add(new List<string>());
                t.PorHeroe[t.PorHeroe.Count - 1].AddRange(Enumerable.Repeat<string>(null, faltan));
            }
            return t;
        }

        /// <summary>Lo que arrastra una columna. Un carro por caravana enganchada (como mucho dos) con el animal de su primer carro que lo lleve;
        /// el de fundación (con buey) si lleva la Caravana de Fundación; el carrito, si lleva algo en su carro (`suministro`: solo se sabe de las
        /// de tu Facción).</summary>
        public static CarrosDeColumna CarrosDe(Ejercito c, Func<string, Caravana> caravana)
        {
            var r = new CarrosDeColumna();
            var deCarga = new List<Caravana>();
            bool fundacion = !string.IsNullOrEmpty(c.CaravanaFundacionId);
            foreach (var id in c.CaravanasAdjuntasIds)
            {
                if (id == c.CaravanaFundacionId) continue;
                var k = caravana(id);
                if (k != null && k.Tipo == TipoCaravana.CaravanaDeFundacion) { fundacion = true; continue; }
                if (deCarga.Count < MaxCarrosDeCargaEnColumna) deCarga.Add(k);
            }
            r.Carga = deCarga.Count;
            r.Fundacion = fundacion;
            foreach (var k in deCarga)
            {
                var primero = k?.Carros.FirstOrDefault(x => x.HasAnimal);
                r.Animales.Add(Animal(primero?.Animal));
                r.Reforzados.Add(primero?.TipoCarro == "reforzado");
            }
            if (fundacion) r.Animales.Add(AnimalDeTiro.Buey);
            r.Propio = c.Suministro != null && c.Suministro.PorId.Values.Any(v => v > 0);
            return r;
        }

        /// <summary>Una caravana suelta: su escolta (como mucho 3 escuadras; la ajena escoltada, una) delante y detrás sus carros, uno por
        /// carro (como mucho 3; sin carros conocidos, uno con buey); la de fundación, solo su carro.</summary>
        public static (TropaDeColumna tropa, CarrosDeColumna carros) DeCaravana(Caravana c)
        {
            int escolta = c.EscoltaIds.Count > 0 ? Math.Min(MaxEscolta, c.EscoltaIds.Count) : c.Escoltada ? 1 : 0;
            var tropa = new TropaDeColumna();
            if (escolta > 0) tropa.PorHeroe.Add(Enumerable.Repeat<string>(null, escolta).ToList());
            if (c.Tipo == TipoCaravana.CaravanaDeFundacion) return (tropa, new CarrosDeColumna { Fundacion = true, Animales = { AnimalDeTiro.Buey } });
            var carros = new CarrosDeColumna();
            carros.Animales.AddRange(c.Carros.Take(MaxCarrosCaravana).Select(x => Animal(x.HasAnimal ? x.Animal : null)));
            carros.Reforzados.AddRange(c.Carros.Take(MaxCarrosCaravana).Select(x => x.TipoCarro == "reforzado"));
            carros.Carga = Math.Max(1, carros.Animales.Count);
            return (tropa, carros);
        }

        // ------------------------------------------------------------------ por el camino

        /// <summary>Lo que ha andado la cabeza, de la más nueva hacia atrás, según su plan: dónde está en `ahora` y los vértices ya pasados
        /// (en u).</summary>
        public static List<(double x, double y)> CaminoDelPlan(Plan plan, Punto posicion, double ahora)
        {
            var p = Instante.Posicion(plan, posicion, ahora);
            var r = new List<(double, double)> { (p.x / 1000, p.y / 1000) };
            var v = plan?.Vertices;
            if (v != null)
                for (int i = v.Count - 1; i >= 0; i--)
                    if (v[i].EnMs <= ahora) r.Add((v[i].P.X / 1000.0, v[i].P.Y / 1000.0));
            return r;
        }

        /// <summary>La longitud de un camino (u).</summary>
        public static double Largo(IReadOnlyList<(double x, double y)> camino)
        {
            double l = 0;
            for (int i = 1; i < camino.Count; i++) l += Math.Sqrt(Sq(camino[i].x - camino[i - 1].x) + Sq(camino[i].y - camino[i - 1].y));
            return l;
        }

        static double Sq(double v) => v * v;

        /// <summary>Dónde va lo que está `atras` u detrás de la cabeza por el `camino` (de la cabeza hacia atrás) y `lado` u a su derecha, y su
        /// rumbo (radianes, de x hacia y): el del tramo en el que cae. Más allá del final, sigue recto por el último tramo.</summary>
        public static (double x, double y, double rumbo) Colocar(IReadOnlyList<(double x, double y)> camino, double atras, double lado)
        {
            double x = camino[0].x, y = camino[0].y, rumbo = -Math.PI / 2; // (sin camino, la columna mira al norte: se forma hacia el sur)
            for (int i = 1; i < camino.Count; i++)
            {
                var b = camino[i - 1];
                var a = camino[i];
                double dx = b.x - a.x, dy = b.y - a.y, l = Math.Sqrt(dx * dx + dy * dy);
                if (l < 1e-9) continue;
                rumbo = Math.Atan2(dy, dx);
                if (l >= atras || i == camino.Count - 1)
                {
                    double t = atras / l;
                    x = b.x - dx * t;
                    y = b.y - dy * t;
                    atras = 0;
                    break;
                }
                atras -= l;
                x = a.x; y = a.y;
            }
            if (atras > 0) { x -= Math.Cos(rumbo) * atras; y -= Math.Sin(rumbo) * atras; }
            return (x - Math.Sin(rumbo) * lado, y + Math.Cos(rumbo) * lado, rumbo);
        }

        /// <summary>Lo que ocupa la columna de la cabeza a la cola (u), para saber cuánto camino hace falta.</summary>
        public static double Largo(List<SitioDeFigura> figuras) =>
            figuras.Count == 0 ? 0 : figuras.Max(f => f.Atras + (f.Figura == FiguraDeColumna.Carro || f.Figura == FiguraDeColumna.CarroDeFundacion
                || f.Figura == FiguraDeColumna.Carrito ? LargoReal(f.Figura, f.Animal, f.Reforzado).cola * MetrosAFigura * f.Escala : 0)) * Tropa;
    }

    /// <summary>El rastro de la cabeza (`mover` del prototipo): por dónde ha pasado, del más viejo al más nuevo. La primera vez o tras un salto
    /// (más de 6 u), la columna se forma detrás hacia el sur (+y); se apunta cada 0,03 u y guarda como mucho 400 puntos.</summary>
    public sealed class RastroDeColumna
    {
        public const int Maximo = 400;
        public const double Salto = 6, MinimoEntrePuntos = 0.03, PasoInicial = 0.3;
        public const int PuntosIniciales = 30;
        readonly List<(double x, double y)> puntos = new List<(double, double)>();

        public int Cuantos => puntos.Count;

        public void Mover(double x, double y)
        {
            var ultimo = puntos.Count > 0 ? puntos[puntos.Count - 1] : ((double x, double y)?)null;
            double d = ultimo == null ? double.MaxValue : Math.Sqrt((x - ultimo.Value.x) * (x - ultimo.Value.x) + (y - ultimo.Value.y) * (y - ultimo.Value.y));
            if (ultimo == null || d > Salto)
            {
                puntos.Clear();
                for (int k = 0; k < PuntosIniciales; k++) puntos.Add((x, y + (PuntosIniciales - 1 - k) * PasoInicial));
            }
            else if (d > MinimoEntrePuntos)
            {
                puntos.Add((x, y));
                if (puntos.Count > Maximo) puntos.RemoveAt(0);
            }
        }

        /// <summary>El rastro desde la cabeza (`x`, `y`) hacia atrás.</summary>
        public List<(double x, double y)> HaciaAtras(double x, double y)
        {
            var r = new List<(double, double)>(puntos.Count + 1) { (x, y) };
            for (int i = puntos.Count - 1; i >= 0; i--) r.Add(puntos[i]);
            return r;
        }

        /// <summary>Por dónde ir hacia atrás: el plan si llega a toda la columna (`largo` u); si no, el rastro.</summary>
        public List<(double x, double y)> Camino(Plan plan, Punto posicion, double ahora, double largo)
        {
            var delPlan = ColumnasDelMapa.CaminoDelPlan(plan, posicion, ahora);
            Mover(delPlan[0].x, delPlan[0].y);
            return ColumnasDelMapa.Largo(delPlan) >= largo ? delPlan : HaciaAtras(delPlan[0].x, delPlan[0].y);
        }
    }
}
