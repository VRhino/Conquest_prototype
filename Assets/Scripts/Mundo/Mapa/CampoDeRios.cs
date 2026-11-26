// El campo de los ríos (26-11), como `CampoDeRios` de BronzeAgeClient (`src/mundo3d/rios.ts`): el río ya no es una cinta de agua puesta encima,
// sino parte del suelo y de la lámina común, como el mar y los lagos. Por segmento de la curva de cada río (la de `RiosDelMapa.Tramos`: los
// puntos y los anchos del motor o, sin ellos, la regla de antes), sus extremos, su medio ancho, su nivel y lo que se afina al nacer; en una
// rejilla de cubos para buscar los cercanos. En cada punto dice el suelo con el cauce tallado, el nivel del agua (ninguno fuera del río), la
// ribera, lo mojado y el rumbo de la corriente. Las juntas (confluencias, trenzas, bocas) se unen solas: es el mismo campo, y donde dos ríos
// cubren un punto manda el más bajo.
using System;
using System.Collections.Generic;
using BronzeAge.Contract.V1;
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa
{
    public sealed class CampoDeRios
    {
        /// <summary>Lado del cubo de búsqueda (u).</summary>
        const double Cubo = 2;
        /// <summary>Lo hondo del agua en medio del cauce (u): `HondoBase + HondoPorMedio · medio ancho`, como mucho `PinturaDelMapa.AguaMaxima`
        /// (exagerado, como el mar, para que el color somero de la orilla y el hondo del centro se lean a la escala del mapa). La orilla sube de
        /// la lámina al relieve en `Orilla` medios anchos (como poco `OrillaMinima` u).</summary>
        public const float HondoBase = 0.35f, HondoPorMedio = 0.5f, Orilla = 0.6f, OrillaMinima = 0.6f;
        /// <summary>La ribera pintada y el suelo mojado, por cada lado, a la medida del río: `PorAncho` × su ancho, entre `FranjaMinima` u y
        /// `RiberaMaxima` (la ribera) o `MojadoMaximo` (lo mojado) u, los de antes, a los que llegan los ríos de 1,5 u o más.</summary>
        public const float PorAncho = 1.2f, FranjaMinima = 0.35f, RiberaMaxima = 1.6f, MojadoMaximo = 1.2f;
        /// <summary>El medio ancho mínimo pintado, en pasos de la malla de la tesela (`MEDIO_MINIMO` del prototipo): un río más estrecho que la
        /// rejilla se perdería entre sus vértices.</summary>
        public const float MedioMinimo = 0.75f;
        /// <summary>El medio ancho con que se ve de cerca un río estrecho (u; el de la malla de paso 0,5): lo que se pone en el suelo (plantas,
        /// polvo, nodos, casas, caminos) cuenta con él para no caer en su agua pintada.</summary>
        public const float MedioPintadoDeCerca = MedioMinimo * 0.5f;
        /// <summary>Al nacer (un tramo que no sale de otro río ni del agua), el río se afina: su ancho pintado sube de `Punta` a todo en los
        /// primeros `Nacimiento` u, con la ribera.</summary>
        public const float Nacimiento = 2, Punta = 0.08f;
        /// <summary>Lo que tarda (u) un afluente en bajar al nivel del río en que entra.</summary>
        const double Junta = 4;
        /// <summary>Cuánto se suaviza el nivel a lo largo del río (u, a cada lado).</summary>
        const double Suavizado = 2;
        /// <summary>Lo más que se ensancha la ribera (la barra del lado de dentro de las curvas y la playa de grava del vado; `CaucesDelMapa.Ensanche`).</summary>
        const double EnsancheMaximo = 3.5;

        /// <summary>El medio ancho mínimo más grande que se pide (el de las teselas de paso 2 u).</summary>
        const double MedioMinimoMaximo = 1.5;

        /// <summary>Lo que queda el suelo bajo la lámina en la orilla (u).</summary>
        const double Bajo = 0.02;
        /// <summary>Lo más hondo del cauce, en medios anchos: en los ríos finos la orilla queda menos empinada y la luz no marca sus aristas.</summary>
        const double HondoPorMedioMaximo = 0.8;

        const int Campos = 13;
        /// <summary>Por segmento: ax ay bx by medioA medioB nivelA nivelB afinadoA afinadoB (u, u, m, 0..1), su alcance (u: el cauce, la orilla,
        /// la ribera más ensanchada y lo mojado, con el medio ancho mínimo más grande) y lo andado desde el nacimiento en sus extremos (u, como lo
        /// cuenta `CaucesDelMapa`).</summary>
        readonly double[] s;
        readonly Dictionary<long, List<int>> cubos = new Dictionary<long, List<int>>();
        readonly List<int> deRio;

        /// <summary>El nivel (m) al principio de cada segmento del río `rio` (su índice en el artefacto), río abajo, y el del final.</summary>
        public List<double> NivelesDe(int rio)
        {
            var r = new List<double>();
            for (int k = 0; k < deRio.Count; k++) if (deRio[k] == rio) r.Add(s[Campos * k + 6]);
            for (int k = deRio.Count - 1; k >= 0; k--) if (deRio[k] == rio) { r.Add(s[Campos * k + 7]); break; }
            return r;
        }

        /// <summary>Lo que da el campo en un punto: el suelo con el cauce tallado (m), el nivel del agua (m; NaN si no cae en un río), la ribera
        /// y lo mojado (0..1; 1 en el agua), el rumbo de la corriente (unitario) y lo deprisa que baja la lámina ahí (m por u). `NivelOrilla`:
        /// el nivel del río también en su orilla (NaN más allá), para las esquinas secas de la lámina: así su agua sigue el mismo campo y el
        /// suelo la corta sin escalones.</summary>
        public struct EnRio { public float Suelo, Nivel, NivelOrilla, Ribera, Mojado, Cx, Cy, Pendiente, Centro, S, Lado, Medio, AnchoRibera; public int Rio; }
        // `Medio` y `AnchoRibera` (u): el medio ancho pintado y el ancho de la ribera del río más cercano; con su distancia al eje, el suelo
        // decide arena o hierba por píxel (`MapaSuelo`), en una curva con el ruido de la cinta y no por los triángulos de la malla.
        // `Lado`: la distancia con signo (u) al eje de ese río (+ a la izquierda de la corriente). `S` y `Lado` son las coordenadas del agua del
        // río para el sombreado (a lo largo y de través, como la cinta): girar las del mundo con el rumbo interpolado de cada vértice, a cientos
        // de u del origen, hacía rayas poligonales.
        // `Rio` y `S`: el río más cercano (su índice en el artefacto; −1 si ninguno) y lo andado en él a la altura del punto, continuo (para lo
        // somero del vado: con la muestra más cercana, saltaba de una a otra y el vado se pintaba a trozos, con rayas de espuma).
        // `Centro`: 1 − distancia al eje / medio ancho pintado (1 en el eje, 0 en la orilla, negativo fuera; lo más alto de los ríos cercanos): de
        // él salen, en el sombreado, el borde del agua cortado por píxel (una curva limpia, no donde caen los vértices) y su hondura aparente.

        /// <summary>`nivelDelMar` (m): el umbral del agua; la lámina del río va, como la del mar y los lagos, un metro por debajo del lecho, y
        /// nunca por debajo de la del mar (así llega a la boca a su altura).</summary>
        public CampoDeRios(ArtefactoDeMundo a, Relieve relieve, float nivelDelMar)
        {
            var segs = new List<double>();
            var deRio = new List<int>();
            var nacen = new List<(int rio, int primero, int ultimo)>();
            var acaban = new List<(int rio, int primero, int ultimo)>();
            for (int ir = 0; ir < a.Rios.Count; ir++)
            {
                var rio = a.Rios[ir];
                if (rio.Puntos.Count < 2) continue;
                // el nivel: lo más bajo del lecho (el relieve en el eje, que el generador talla siempre bajando) desde el nacimiento
                double antes = double.PositiveInfinity, previo = double.PositiveInfinity;
                bool primero = true;
                double andadoRio = 0;
                var nace = new Vector2(rio.Puntos[0].X / 1000f, rio.Puntos[0].Y / 1000f);
                foreach (var tramo in RiosDelMapa.Tramos(rio, relieve))
                {
                    var p = tramo.Puntos;
                    var niveles = new double[p.Count];
                    for (int i = 0; i < p.Count; i++)
                        niveles[i] = Math.Max(nivelDelMar - 1, antes = Math.Min(antes, relieve.AlturaEn(p[i].x, p[i].y) - 1));
                    // (suavizado a lo largo, ±`Suavizado` u, y otra vez sin subir: el mínimo corrido solo, sobre el relieve muestreado, baja a
                    // escalones, y cada escalón se veía como una raya atravesada en el agua)
                    var crudos = (double[])niveles.Clone();
                    int radio = (int)Math.Round(Suavizado / 0.5);
                    double tope = double.PositiveInfinity;
                    for (int i = 0; i < p.Count; i++)
                    {
                        double suma = 0; int cuantos = 0;
                        for (int q = Math.Max(0, i - radio); q <= Math.Min(p.Count - 1, i + radio); q++) { suma += crudos[q]; cuantos++; }
                        niveles[i] = tope = Math.Min(tope, Math.Min(previo, Math.Min(crudos[i] + 0.5, suma / cuantos))); // (como mucho medio metro sobre el lecho)
                    }
                    if (p.Count > 0) previo = niveles[p.Count - 1];
                    int desde = segs.Count / Campos;
                    for (int i = 0; i + 1 < p.Count; i++)
                    {
                        int k = segs.Count / Campos;
                        double ma = tramo.Anchos[i] / 2, mb = tramo.Anchos[i + 1] / 2;
                        double mm = Math.Max(MedioMinimoMaximo, Math.Max(ma, mb));
                        double r = mm + Math.Max(Math.Max(OrillaMinima, mm * Orilla) + MedioMinimoMaximo / MedioMinimo, Math.Min(RiberaMaxima, Math.Max(FranjaMinima, PorAncho * 2 * Math.Max(ma, mb))) * EnsancheMaximo);
                        double largoSeg = Math.Sqrt(Sq(p[i + 1].x - p[i].x) + Sq(p[i + 1].y - p[i].y));
                        segs.AddRange(new double[] { p[i].x, p[i].y, p[i + 1].x, p[i + 1].y, ma, mb, niveles[i], niveles[i + 1], 1, 1, r, andadoRio, andadoRio + largoSeg });
                        andadoRio += largoSeg;
                        deRio.Add(ir);
                        for (long cy = (long)Math.Floor((Math.Min(p[i].y, p[i + 1].y) - r) / Cubo); cy <= (long)Math.Floor((Math.Max(p[i].y, p[i + 1].y) + r) / Cubo); cy++)
                            for (long cx = (long)Math.Floor((Math.Min(p[i].x, p[i + 1].x) - r) / Cubo); cx <= (long)Math.Floor((Math.Max(p[i].x, p[i + 1].x) + r) / Cubo); cx++)
                            {
                                long c = cx * 65536 + cy;
                                if (!cubos.TryGetValue(c, out var l)) cubos[c] = l = new List<int>();
                                l.Add(k);
                            }
                    }
                    // (solo el primer tramo, si empieza donde nace el río: uno que sale de un lago no se afina)
                    if (primero && p.Count > 1 && Vector2.Distance(p[0], nace) < 0.5f) nacen.Add((ir, desde, segs.Count / Campos - 1));
                    primero = false;
                    if (p.Count > 1) { if (acaban.Count > 0 && acaban[acaban.Count - 1].rio == ir) acaban.RemoveAt(acaban.Count - 1); acaban.Add((ir, desde, segs.Count / Campos - 1)); }
                }
            }
            s = segs.ToArray();
            this.deRio = deRio;
            // El nacimiento: si no cae en el cauce de otro río (un brazo de una trenza, un afluente que sale de él), se afina.
            foreach (var (rio, primero, ultimo) in nacen)
            {
                double x = s[Campos * primero], y = s[Campos * primero + 1];
                bool enOtro = false;
                if (cubos.TryGetValue(Clave(x, y), out var lista))
                    foreach (int k in lista)
                        if (deRio[k] != rio && Distancia(k, x, y, out double t) < Medio(k, t) + 0.3) { enOtro = true; break; }
                if (enOtro) continue;
                double andado = 0;
                for (int k = primero; k <= ultimo && andado < Nacimiento; k++)
                {
                    s[Campos * k + 8] = Afinado(andado);
                    andado += Math.Sqrt(Sq(s[Campos * k + 2] - s[Campos * k]) + Sq(s[Campos * k + 3] - s[Campos * k + 1]));
                    s[Campos * k + 9] = Afinado(andado);
                }
            }
            // La confluencia: un río que acaba en el cauce de otro más bajo baja hasta su nivel en los últimos `Junta` u (sin escalón en la junta).
            foreach (var (rio, primero, ultimo) in acaban)
            {
                double x = s[Campos * ultimo + 2], y = s[Campos * ultimo + 3], destino = double.PositiveInfinity;
                if (cubos.TryGetValue(Clave(x, y), out var lista))
                    foreach (int k in lista)
                        if (deRio[k] != rio && Distancia(k, x, y, out double t) < Medio(k, t) + 0.3)
                            destino = Math.Min(destino, s[Campos * k + 6] + (s[Campos * k + 7] - s[Campos * k + 6]) * t);
                if (!(destino < s[Campos * ultimo + 7])) continue;
                double andado = 0;
                for (int k = ultimo; k >= primero && andado < Junta; k--)
                {
                    double fin = s[Campos * k + 7], largo = Math.Sqrt(Sq(s[Campos * k + 2] - s[Campos * k]) + Sq(s[Campos * k + 3] - s[Campos * k + 1]));
                    s[Campos * k + 7] = Math.Min(fin, destino + (fin - destino) * Suave(andado / Junta));
                    andado += largo;
                    s[Campos * k + 6] = Math.Min(s[Campos * k + 6], destino + (s[Campos * k + 6] - destino) * Suave(andado / Junta));
                }
            }
        }

        static double Sq(double v) => v * v;
        static long Clave(double x, double y) => (long)Math.Floor(x / Cubo) * 65536 + (long)Math.Floor(y / Cubo);
        static double Suave(double t) { double u = Math.Min(1, Math.Max(0, t)); return u * u * (3 - 2 * u); }
        static double Afinado(double andado) => Punta + (1 - Punta) * Suave(andado / Nacimiento);
        double Medio(int k, double t) => s[Campos * k + 4] + (s[Campos * k + 5] - s[Campos * k + 4]) * t;

        /// <summary>Distancia (u) de (x, y) al segmento `k` y dónde cae en él (0..1).</summary>
        double Distancia(int k, double x, double y, out double t)
        {
            double ax = s[Campos * k], ay = s[Campos * k + 1], dx = s[Campos * k + 2] - ax, dy = s[Campos * k + 3] - ay, l2 = dx * dx + dy * dy;
            t = l2 < 1e-12 ? 0 : Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / l2));
            return Math.Sqrt(Sq(ax + dx * t - x) + Sq(ay + dy * t - y));
        }

        /// <summary>¿Hay algún cubo con río en el rectángulo (u)? Para no consultar punto a punto en las teselas sin ríos.</summary>
        public bool HayRioEn(double x0, double y0, double x1, double y1)
        {
            for (long cy = (long)Math.Floor(y0 / Cubo); cy <= (long)Math.Floor(y1 / Cubo); cy++)
                for (long cx = (long)Math.Floor(x0 / Cubo); cx <= (long)Math.Floor(x1 / Cubo); cx++)
                    if (cubos.ContainsKey(cx * 65536 + cy)) return true;
            return false;
        }

        /// <summary>El punto (x, y), con el suelo a `h` m: el suelo con el cauce de los ríos cercanos tallado y, si cae en uno, el nivel de su agua
        /// (el más bajo de los que lo cubren) y su corriente. `medioMinimo` (u): el medio ancho con que se pinta un río estrecho; `ensanche`: cuánto
        /// se ensancha ahí la ribera. Null lejos de todo río.</summary>
        public EnRio? En(double x, double y, float h, double medioMinimo, double ensanche = 1)
        {
            if (!cubos.TryGetValue(Clave(x, y), out var lista)) return null;
            double mpu = EscalaDelMapa.MetrosPorUnidad, seco = PinturaDelMapa.AguaSeco * mpu, bajo = Bajo * mpu, paso = medioMinimo / MedioMinimo;
            double suelo = double.PositiveInfinity, nivel = double.NaN, nivelOrilla = double.NaN, centro = -1, ribera = -1, mojado = 0, cx = 0, cy = 0, pend = 0;
            double dRio = double.PositiveInfinity, sRio = 0, ladoRio = 0, medioRio = 0, riberaRio = 0;
            int rioCerca = -1;
            bool cerca = false;
            foreach (int k in lista)
            {
                double d = Distancia(k, x, y, out double t);
                if (d >= s[Campos * k + 10]) continue;
                double real = Medio(k, t), afinado = s[Campos * k + 8] + (s[Campos * k + 9] - s[Campos * k + 8]) * t;
                double medio = Math.Max(1e-3, Math.Max(medioMinimo, real) * afinado), orilla = Math.Max(OrillaMinima * afinado, medio * Orilla);
                double franja = Math.Max(FranjaMinima, PorAncho * 2 * real) * afinado;
                double anchoRibera = Math.Min(RiberaMaxima, franja) * Math.Min(EnsancheMaximo, ensanche), anchoMojado = Math.Min(MojadoMaximo, franja);
                if (d >= medio + Math.Max(paso + orilla, Math.Max(anchoRibera, anchoMojado))) continue;
                cerca = true;
                centro = Math.Max(centro, 1 - d / medio);
                if (d < dRio)
                {
                    dRio = d; rioCerca = deRio[k]; sRio = s[Campos * k + 11] + (s[Campos * k + 12] - s[Campos * k + 11]) * t;
                    medioRio = medio; riberaRio = anchoRibera;
                    double ax = s[Campos * k], ay = s[Campos * k + 1], ex = s[Campos * k + 2] - ax, ey = s[Campos * k + 3] - ay;
                    ladoRio = ex * (y - ay) - ey * (x - ax) >= 0 ? d : -d;
                }
                double nv = s[Campos * k + 6] + (s[Campos * k + 7] - s[Campos * k + 6]) * t;
                // (al nacer, también menos hondo: si no, la malla enseña el primer vértice hondo como un corte recto)
                double hondo = Math.Min(Math.Min(PinturaDelMapa.AguaMaxima, HondoBase + HondoPorMedio * medio), HondoPorMedioMaximo * medio) * afinado * afinado * mpu;
                if (d < medio + paso + orilla && !(nv >= nivelOrilla))
                {
                    nivelOrilla = nv;
                    double l = Math.Max(1e-6, Math.Sqrt(Sq(s[Campos * k + 2] - s[Campos * k]) + Sq(s[Campos * k + 3] - s[Campos * k + 1])));
                    cx = (s[Campos * k + 2] - s[Campos * k]) / l; cy = (s[Campos * k + 3] - s[Campos * k + 1]) / l;
                    pend = (s[Campos * k + 6] - s[Campos * k + 7]) / l;
                }
                if (d < medio)
                {
                    // el cauce: de la orilla (`Bajo` bajo la lámina) a lo hondo en medio
                    suelo = Math.Min(suelo, Math.Min(h, nv - bajo - (hondo - bajo) * (1 - Sq(d / medio))));
                    ribera = mojado = 1;
                    if (!(nv >= nivel)) nivel = nv;
                    continue;
                }
                // la orilla: de la lámina al relieve
                // Y antes, una franja de medio paso de la malla a `Bajo` bajo la lámina: el agua la cubre y el sombreado la corta por píxel en la orilla.
                double fuera = d - medio, f = fuera - 0.5 * paso * afinado, b0 = nv - bajo;
                if (f < 0) suelo = Math.Min(suelo, Math.Min(h, b0));
                else if (f < orilla)
                    // (subiendo suave, sin el escalón empinado de antes: en los ríos finos la luz marcaba sus aristas; el borde del agua lo corta el
                    // sombreado por píxel, y si el relieve queda bajo el agua, la orilla sube hasta `seco` sobre ella)
                    suelo = Math.Min(suelo, b0 + (Math.Max(h, nv + seco) - b0) * Suave(f / orilla));
                ribera = Math.Max(ribera, Math.Max(-1, 1 - fuera / (0.95 * anchoRibera))); // (lineal hasta −1: el sombreado la corta por píxel)
                mojado = Math.Max(mojado, 1 - Suave(fuera / anchoMojado));
            }
            if (!cerca) return null;
            return new EnRio
            {
                Suelo = (float)(double.IsPositiveInfinity(suelo) ? h : suelo), Nivel = (float)nivel, NivelOrilla = (float)(double.IsNaN(nivel) ? nivelOrilla : nivel), Ribera = (float)ribera, Mojado = (float)mojado,
                Cx = (float)cx, Cy = (float)cy, Pendiente = (float)pend, Centro = (float)centro, Rio = rioCerca, S = (float)sRio, Lado = (float)ladoRio, Medio = (float)medioRio, AnchoRibera = (float)riberaRio,
            };
        }
    }
}
