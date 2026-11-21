// El banco de pruebas de caminos y territorio (`SueloDelMapaTests.LosCaminosYElTerritorio`): sobre el mundo del servidor de prueba, una red
// verosímil trazada por el grafo de navegación real del artefacto (nodos cada 4 u, ocho direcciones; A* con su coste), cerca de un vado de tierra
// con bosque denso, pradera y ladera alrededor; y un territorio inventado en un llano sin agua. Nada sale de datos de jugadores.
using System;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo;
using ConquestTactics.Mundo.Mapa;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.UI.Tests
{
    sealed class BancoDeCaminos
    {
        public const double RadioZona = 10;
        public readonly Dictionary<string, (double x, double y)> Sitios = new Dictionary<string, (double x, double y)>();
        /// <summary>Los sitios de cada suelo (pradera, tierra seca, arena, roca), con un sendero y un camino uno junto al otro: para los recortes de cerca.</summary>
        public readonly Dictionary<string, (double x, double y)> SitiosDeSuelo = new Dictionary<string, (double x, double y)>();
        /// <summary>Los recortes de cerca: por suelo y clase («suelo_pradera_sendero»...), el punto medio de su camino (que corre de oeste a este).</summary>
        public readonly Dictionary<string, (double x, double y)> Recortes = new Dictionary<string, (double x, double y)>();
        /// <summary>Los sitios de agua: un vado de otro río (sin camino) y, para comprobar que fuera de los vados el agua no cambia, un río y una costa.</summary>
        public readonly Dictionary<string, (double x, double y)> SitiosDeAgua = new Dictionary<string, (double x, double y)>();
        /// <summary>Las zonas donde se mide el territorio: su centro, su radio y hacia dónde queda fuera el borde que se mide (sin vecinos por ahí).</summary>
        public readonly List<(string nombre, (double x, double y) centro, double radio, (double x, double y) fuera)> Medidas = new List<(string, (double, double), double, (double, double))>();
        ZonaFaccion zonaPropia;
        public readonly List<string> Registro = new List<string>();
        /// <summary>Los nodos de vado por los que han pasado las rutas trazadas.</summary>
        public readonly List<(double x, double y)> PorVados = new List<(double x, double y)>();
        readonly List<TramoDeRed> tramos = new List<TramoDeRed>();
        readonly List<Entidad> cosas = new List<Entidad>();
        TramoDeRed nuevo;

        // el grafo
        double[] nx, ny;
        List<(int b, double c)>[] vecinos;
        readonly HashSet<(int, int)> aristasVado = new HashSet<(int, int)>();
        readonly Dictionary<(int, int), List<int>> casillas = new Dictionary<(int, int), List<int>>();

        static Punto Pu(double x, double y) => new Punto { X = (long)Math.Round(x * 1000), Y = (long)Math.Round(y * 1000) };

        public Foto Vista(bool conCaminoNuevo = false)
        {
            var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = conCaminoNuevo ? 3UL : 2UL };
            var red = new RedCaminos();
            red.Tramos.AddRange(tramos.Select(t => t.Clone()));
            if (conCaminoNuevo) red.Tramos.Add(nuevo.Clone());
            vista.Entidades.Add(new Entidad { RedCaminos = red });
            vista.Entidades.AddRange(cosas.Select(c => c.Clone()));
            return vista;
        }

        public static BancoDeCaminos Hacer(Relieve relieve, ArtefactoDeMundo artefacto, DatosDeJuego datos = null)
        {
            var b = new BancoDeCaminos();
            b.Grafo(artefacto.Navegacion);
            b.Buscar(relieve, artefacto);
            if (datos != null) b.BuscarSuelosYAgua(relieve, artefacto, datos);
            return b;
        }

        /// <summary>Los sitios de cada suelo, con su sendero y su camino (30 u, a 5 u uno del otro), y los de agua.</summary>
        void BuscarSuelosYAgua(Relieve relieve, ArtefactoDeMundo artefacto, DatosDeJuego datos)
        {
            var fer = new RasterDeMundo(artefacto.Fertilidad);
            var hum = new RasterDeMundo(artefacto.Humedad);
            var bos = new RasterDeMundo(artefacto.DensidadBosque);
            double U(string k) => datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION." + k) * Relieve.MetrosAltura;
            double costa = U("umbralCosta"), montana = U("umbralMontana"), cima = U("umbralCima");
            double Pendiente(double x, double y) =>
                Math.Max(Math.Abs(relieve.AlturaEn(x + 2, y) - relieve.AlturaEn(x - 2, y)), Math.Abs(relieve.AlturaEn(x, y + 2) - relieve.AlturaEn(x, y - 2))) / (4 * EscalaDelMapa.MetrosPorUnidad);
            bool SinAgua(double x, double y, double r)
            {
                for (double dy = -r; dy <= r; dy += 3)
                    for (double dx = -r; dx <= r; dx += 3)
                        if (relieve.AguaEn(x + dx, y + dy)) return false;
                return true;
            }
            var (vx, vy) = Sitios["vado"];
            var mejor = new Dictionary<string, (double p, double x, double y)>();
            // los caminos de cada suelo van rectos por los nodos (sin ruta): ningún río a menos de 25 u, o cruzarían fuera de un vado
            var puntosDeRio = artefacto.Rios.SelectMany(r => r.Puntos).Select(q => (x: q.X / 1000.0, y: q.Y / 1000.0)).ToList();
            bool SinRio(double x, double y) => puntosDeRio.All(q => (q.x - x) * (q.x - x) + (q.y - y) * (q.y - y) >= 25 * 25);
            for (double y = vy - 600; y <= vy + 600; y += 8)
                for (double x = vx - 600; x <= vx + 600; x += 8)
                {
                    if (x < 100 || y < 100 || x > relieve.Columnas * relieve.Paso - 100 || y > relieve.Filas * relieve.Paso - 100 || relieve.AguaEn(x, y)) continue;
                    double h = relieve.AlturaEn(x, y), pend = Pendiente(x, y), humedo = 0.5 * hum.En(x, y) / 255.0 + 0.5 * fer.En(x, y) / 255.0, b = bos.En(x, y) / 255.0;
                    void Probar(string k, double p) { if ((!mejor.TryGetValue(k, out var m) || p > m.p) && SinRio(x, y)) mejor[k] = (p, x, y); }
                    if (b < 0.1 && pend < 0.06) Probar("suelo_pradera", humedo);
                    if (b < 0.1 && pend < 0.06) Probar("suelo_seco", -humedo);
                    if (h < costa + 2 && pend < 0.08 && b < 0.2) Probar("suelo_arena", -Math.Abs(h - (costa - 4)) - (relieve.AguaEn(x + 20, y) || relieve.AguaEn(x - 20, y) ? 0 : 30));
                    // la roca, por debajo de la nieve (que empieza por encima de la cima)
                    if (h > montana + 25 && h < cima - 15 && pend < 0.3) Probar("suelo_roca", -Math.Abs(h - (montana + cima) / 2) + pend * 100);
                }
            // Tres zonas sobre la pradera, separadas (sin recortarse): tuya (azul, en tu zona fusionada), ajena verde y la negra.
            if (mejor.TryGetValue("suelo_pradera", out var mp))
            {
                foreach (var (id, faccion, dx, nombre) in new[] { ("as-pa", "f-1", 0.0, "azul sobre pradera"), ("as-pv", "f-2", 40.0, "verde sobre pradera"), ("as-pn", "f-3", 80.0, "negro sobre pradera") })
                {
                    var c = Nodo((mp.x - 40 + dx, mp.y + 40));
                    var disco = new TrazadoDelMapa.Disco { Id = id, Centro = c, Radio = RadioZona, FaccionId = faccion };
                    var pol = TrazadoDelMapa.PoligonoDeZona(disco, new TrazadoDelMapa.Disco[0]);
                    var a = new Asentamiento { Id = id, Nombre = nombre, Nivel = 1, FaccionId = faccion, Posicion = Pu(c.x, c.y) };
                    foreach (var q in pol) a.Zona.Add(Pu(q.x, q.y));
                    cosas.Add(new Entidad { Asentamiento = a });
                    if (faccion == "f-1" && zonaPropia != null)
                    {
                        var cz = new ContornoZona();
                        foreach (var q in pol) cz.Puntos.Add(Pu(q.x, q.y));
                        zonaPropia.Contornos.Add(cz);
                    }
                    Medidas.Add((nombre, c, RadioZona, (0, -1)));
                }
                Sitios["territorio_pradera"] = Nodo((mp.x, mp.y + 40));
            }
            foreach (var k in new[] { "suelo_pradera", "suelo_seco", "suelo_arena", "suelo_roca" })
            {
                if (!mejor.TryGetValue(k, out var m)) { Registro.Add($"{k}: no hay"); continue; }
                var c = Nodo((m.x, m.y));
                // en línea recta por los nodos del grafo (uno cada 4 u), a 8 u uno del otro: sin rodeos ni solapes entre el sendero y el camino
                foreach (var (clase, escalon, dy) in new[] { ("sendero", 0, -4.0), ("camino", 1, 4.0) })
                {
                    var t = new TramoDeRed { Id = k + "-" + clase, Escalon = (uint)escalon };
                    (double x, double y)? ultimo = null;
                    for (double dx = -14; dx <= 14; dx += 4)
                    {
                        var q = Nodo((c.x + dx, c.y + dy));
                        if (ultimo == q) continue;
                        t.Puntos.Add(Pu(q.x, q.y));
                        ultimo = q;
                    }
                    tramos.Add(t);
                    if (t.Puntos.Count > 0) { var q = t.Puntos[t.Puntos.Count / 2]; Recortes[k + "_" + clase] = (q.X / 1000.0, q.Y / 1000.0); }
                }
                SitiosDeSuelo[k] = c;
                Registro.Add($"sitio {k}: ({c.x:0.0}, {c.y:0.0}), {relieve.AlturaEn(c.x, c.y):0} m, pendiente {Pendiente(c.x, c.y):0.00}, húmedo {(0.5 * hum.En(c.x, c.y) + 0.5 * fer.En(c.x, c.y)) / 255.0:0.00}");
            }
            // Otro vado (el grupo de aristas de vado más alejado del del banco, a menos de 400 u), un río sin vado cerca y una costa.
            var padre = new Dictionary<int, int>();
            int Raiz(int q) { while (padre[q] != q) { padre[q] = padre[padre[q]]; q = padre[q]; } return q; }
            foreach (var (a, b2) in aristasVado) { if (!padre.ContainsKey(a)) padre[a] = a; if (!padre.ContainsKey(b2)) padre[b2] = b2; }
            foreach (var (a, b2) in aristasVado) padre[Raiz(a)] = Raiz(b2);
            var grupos = padre.Keys.ToList().GroupBy(Raiz).Select(g => (x: g.Average(i => nx[i]), y: g.Average(i => ny[i]), n: g.Count())).ToList();
            var otro = grupos.Where(g => Math.Sqrt((g.x - vx) * (g.x - vx) + (g.y - vy) * (g.y - vy)) is double d && d > 60 && d < 400)
                .OrderByDescending(g => g.n).FirstOrDefault();
            if (otro.n > 0) { SitiosDeAgua["vado_rio"] = (otro.x, otro.y); Registro.Add($"otro vado: ({otro.x:0.0}, {otro.y:0.0}), {otro.n} nodos"); }
            foreach (var rio in artefacto.Rios)
            {
                foreach (var q in rio.Puntos)
                {
                    double x = q.X / 1000.0, y = q.Y / 1000.0;
                    if (relieve.AguaEn(x, y) || grupos.Any(g => (g.x - x) * (g.x - x) + (g.y - y) * (g.y - y) < 60 * 60)) continue;
                    if ((x - vx) * (x - vx) + (y - vy) * (y - vy) > 500 * 500) continue;
                    SitiosDeAgua["rio_sin_vado"] = (x, y);
                    break;
                }
                if (SitiosDeAgua.ContainsKey("rio_sin_vado")) break;
            }
            for (double r = 20; r < 800 && !SitiosDeAgua.ContainsKey("costa"); r += 10)
                for (int k = 0; k < 36; k++)
                {
                    double x = vx + r * Math.Cos(k * Math.PI / 18), y = vy + r * Math.Sin(k * Math.PI / 18);
                    if (!relieve.AguaEn(x, y) && relieve.AguaEn(x + 8, y) && relieve.AguaEn(x + 16, y) && relieve.AlturaEn(x + 16, y) < costa - 30) { SitiosDeAgua["costa"] = (x, y); break; }
                }
            foreach (var kv in SitiosDeAgua) Registro.Add($"sitio de agua {kv.Key}: ({kv.Value.x:0.0}, {kv.Value.y:0.0})");
        }

        void Grafo(GrafoNavegacion g)
        {
            int n = 0;
            foreach (var nodo in g.Nodos) n = Math.Max(n, (int)nodo.Id + 1);
            nx = new double[n]; ny = new double[n];
            vecinos = new List<(int, double)>[n];
            foreach (var nodo in g.Nodos)
            {
                int i = (int)nodo.Id;
                nx[i] = nodo.Posicion.X / 1000.0; ny[i] = nodo.Posicion.Y / 1000.0;
                vecinos[i] = new List<(int, double)>();
                var k = ((int)Math.Floor(nx[i] / 8), (int)Math.Floor(ny[i] / 8));
                if (!casillas.TryGetValue(k, out var l)) casillas[k] = l = new List<int>();
                l.Add(i);
            }
            foreach (var a in g.Aristas)
            {
                int i = (int)a.A, j = (int)a.B;
                if (i >= n || j >= n || vecinos[i] == null || vecinos[j] == null) continue;
                var c = Math.Sqrt((nx[i] - nx[j]) * (nx[i] - nx[j]) + (ny[i] - ny[j]) * (ny[i] - ny[j])) * a.CosteMilesimas / 1000.0;
                vecinos[i].Add((j, c)); vecinos[j].Add((i, c));
                if (a.Vado) { aristasVado.Add((i, j)); aristasVado.Add((j, i)); }
            }
            Registro.Add($"grafo: {g.Nodos.Count} nodos, {g.Aristas.Count} aristas ({aristasVado.Count / 2} de vado)");
        }

        int Cercano((double x, double y) p)
        {
            int ci = (int)Math.Floor(p.x / 8), cj = (int)Math.Floor(p.y / 8), mejor = -1;
            double dmin = double.MaxValue;
            for (int anillo = 1; anillo <= 4 && mejor < 0; anillo++)
                for (int dj = -anillo; dj <= anillo; dj++)
                    for (int di = -anillo; di <= anillo; di++)
                        if (casillas.TryGetValue((ci + di, cj + dj), out var l))
                            foreach (var i in l)
                            {
                                var d = (nx[i] - p.x) * (nx[i] - p.x) + (ny[i] - p.y) * (ny[i] - p.y);
                                if (d < dmin && vecinos[i].Count > 0) { dmin = d; mejor = i; }
                            }
            return mejor;
        }

        (double x, double y) Nodo((double x, double y) p) { int i = Cercano(p); return i < 0 ? p : (nx[i], ny[i]); }

        /// <summary>Los nodos del camino más barato de `a` a `b` por el grafo (como los manda el motor), o null.</summary>
        List<(double x, double y)> Ruta((double x, double y) a, (double x, double y) b)
        {
            int o = Cercano(a), d = Cercano(b);
            if (o < 0 || d < 0) return null;
            double H(int i) => Math.Sqrt((nx[i] - nx[d]) * (nx[i] - nx[d]) + (ny[i] - ny[d]) * (ny[i] - ny[d]));
            var g = new Dictionary<int, double> { [o] = 0 };
            var de = new Dictionary<int, int>();
            var abiertos = new SortedSet<(double f, int i)> { (H(o), o) };
            while (abiertos.Count > 0 && g.Count < 200_000)
            {
                var (_, i) = abiertos.Min;
                abiertos.Remove(abiertos.Min);
                if (i == d)
                {
                    var r = new List<(double, double)>();
                    for (int k = d; ; k = de[k])
                    {
                        r.Add((nx[k], ny[k]));
                        if (k == o) break;
                        if (aristasVado.Contains((k, de[k]))) PorVados.Add((nx[k], ny[k]));
                    }
                    r.Reverse();
                    return r;
                }
                foreach (var (j, c) in vecinos[i])
                {
                    var nuevoG = g[i] + c;
                    if (g.TryGetValue(j, out var viejo)) { if (nuevoG >= viejo) continue; abiertos.Remove((viejo + H(j), j)); }
                    g[j] = nuevoG; de[j] = i;
                    abiertos.Add((nuevoG + H(j), j));
                }
            }
            return null;
        }

        /// <summary>Un pasillo del grafo por bosque cerrado: los nodos con densidad ≥ 0,7, unidos por sus aristas; del grupo conexo (a menos de
        /// `alcance` u de `cerca`) con el camino más largo entre dos de sus nodos (dos barridos de Dijkstra), el trecho de `largo` u del medio.</summary>
        List<(double x, double y)> PasilloDeBosque(Func<double, double, double> bosque, (double x, double y) cerca, double alcance, double largo)
        {
            var dentro = new HashSet<int>();
            for (int i = 0; i < nx.Length; i++)
                if (vecinos[i] != null && Math.Abs(nx[i] - cerca.x) < alcance && Math.Abs(ny[i] - cerca.y) < alcance && bosque(nx[i], ny[i]) >= 0.7) dentro.Add(i);
            (int lejos, double d, Dictionary<int, int> de) Barrer(int s)
            {
                var dist = new Dictionary<int, double> { [s] = 0 };
                var de = new Dictionary<int, int>();
                var abiertos = new SortedSet<(double, int)> { (0, s) };
                int lejos = s;
                while (abiertos.Count > 0)
                {
                    var (du, u) = abiertos.Min;
                    abiertos.Remove(abiertos.Min);
                    if (du > dist[lejos]) lejos = u;
                    foreach (var (w, _) in vecinos[u])
                    {
                        if (!dentro.Contains(w)) continue;
                        double nd = du + Math.Sqrt((nx[u] - nx[w]) * (nx[u] - nx[w]) + (ny[u] - ny[w]) * (ny[u] - ny[w]));
                        if (dist.TryGetValue(w, out var vieja)) { if (nd >= vieja) continue; abiertos.Remove((vieja, w)); }
                        dist[w] = nd; de[w] = u;
                        abiertos.Add((nd, w));
                    }
                }
                return (lejos, dist[lejos], de);
            }
            var hechos = new HashSet<int>();
            (double l, int a, int b, Dictionary<int, int> de) mejor = (0, -1, -1, null);
            foreach (var s in dentro)
            {
                if (hechos.Contains(s)) continue;
                var (a, _, deA) = Barrer(s);
                foreach (var k in deA.Keys) hechos.Add(k);
                hechos.Add(s);
                var (b, l, deB) = Barrer(a);
                if (l > mejor.l) mejor = (l, a, b, deB);
            }
            var camino = new List<(double x, double y)>();
            if (mejor.de == null) { Registro.Add("pasillo de bosque: no hay"); return camino; }
            for (int k = mejor.b; ; k = mejor.de[k]) { camino.Add((nx[k], ny[k])); if (k == mejor.a) break; }
            // el trecho del medio, de `largo` u
            var andado = new List<double> { 0 };
            for (int k = 1; k < camino.Count; k++) andado.Add(andado[k - 1] + Math.Sqrt(Math.Pow(camino[k].x - camino[k - 1].x, 2) + Math.Pow(camino[k].y - camino[k - 1].y, 2)));
            double total = andado[andado.Count - 1], desde = Math.Max(0, total / 2 - largo / 2), hasta = Math.Min(total, total / 2 + largo / 2);
            var r = camino.Where((q, k) => andado[k] >= desde && andado[k] <= hasta).ToList();
            Registro.Add($"pasillo de bosque: el más largo, {total:0} u entre ({camino[0].x:0}, {camino[0].y:0}) y ({camino[camino.Count - 1].x:0}, {camino[camino.Count - 1].y:0}); su trecho del medio, {r.Count} nodos");
            return r;
        }

        /// <summary>Un tramo por los puntos dados, ruta a ruta (cada punto, un nodo del camino).</summary>
        TramoDeRed Tramo(string id, int escalon, params (double x, double y)[] por)
        {
            var t = new TramoDeRed { Id = id, Escalon = (uint)escalon };
            var todos = new List<(double x, double y)>();
            for (int k = 1; k < por.Length; k++)
            {
                var r = Ruta(por[k - 1], por[k]);
                if (r == null) { Registro.Add($"{id}: sin ruta de {por[k - 1]} a {por[k]}"); continue; }
                if (todos.Count > 0 && todos[todos.Count - 1] == r[0]) r.RemoveAt(0);
                todos.AddRange(r);
            }
            foreach (var p in todos) t.Puntos.Add(Pu(p.x, p.y));
            double largo = 0;
            for (int k = 1; k < todos.Count; k++) largo += Math.Sqrt((todos[k].x - todos[k - 1].x) * (todos[k].x - todos[k - 1].x) + (todos[k].y - todos[k - 1].y) * (todos[k].y - todos[k - 1].y));
            Registro.Add($"{id} (escalón {escalon}): {todos.Count} nodos, {largo:0} u");
            return t;
        }

        void Buscar(Relieve relieve, ArtefactoDeMundo artefacto)
        {
            var bos = new RasterDeMundo(artefacto.DensidadBosque);
            var fer = new RasterDeMundo(artefacto.Fertilidad);
            double Bosque(double x, double y) => bos.En(x, y) / 255.0;
            double Fertil(double x, double y) => fer.En(x, y) / 255.0;
            double Pendiente(double x, double y) =>
                Math.Max(Math.Abs(relieve.AlturaEn(x + 1, y) - relieve.AlturaEn(x - 1, y)), Math.Abs(relieve.AlturaEn(x, y + 1) - relieve.AlturaEn(x, y - 1))) / (2 * EscalaDelMapa.MetrosPorUnidad);
            bool Seco(double x, double y, double r)
            {
                for (double dy = -r; dy <= r; dy += 4)
                    for (double dx = -r; dx <= r; dx += 4)
                        if (dx * dx + dy * dy <= r * r && relieve.AguaEn(x + dx, y + dy)) return false;
                return true;
            }
            double ancho = relieve.Columnas * relieve.Paso, alto = relieve.Filas * relieve.Paso;

            // El vado: uno de tierra, lejos del borde, con bosque denso, pradera y ladera a menos de 60 u.
            (double x, double y) vado = (0, 0), dirRio = (1, 0);
            double mejor = -1;
            var orillas = new List<(double x, double y)>();
            foreach (var rio in artefacto.Rios)
                foreach (var t in RiosDelMapa.Tramos(rio, relieve))
                    foreach (var q in t.Puntos) orillas.Add((q.x, q.y));
            foreach (var rio in artefacto.Rios)
            {
                var tramosRio = RiosDelMapa.Tramos(rio, relieve);
                foreach (var v in rio.Vados)
                {
                    double vx = v.X / 1000.0, vy = v.Y / 1000.0;
                    if (relieve.AguaEn(vx, vy) || vx < 200 || vy < 200 || vx > ancho - 200 || vy > alto - 200) continue;
                    int denso = 0, pradera = 0, ladera = 0;
                    for (double dy = -60; dy <= 60; dy += 6)
                        for (double dx = -60; dx <= 60; dx += 6)
                        {
                            double x = vx + dx, y = vy + dy;
                            if (dx * dx + dy * dy < 20 * 20 || relieve.AguaEn(x, y)) continue;
                            double bb = Bosque(x, y);
                            if (bb > 0.7) denso++;
                            if (bb < 0.08 && Fertil(x, y) > 0.5) pradera++;
                            if (Pendiente(x, y) > 0.12) ladera++;
                        }
                    double puntos = Math.Min(denso, 20) + Math.Min(pradera, 20) + Math.Min(ladera, 10) * 2;
                    if (puntos <= mejor) continue;
                    // la dirección del río en el vado
                    Vector2Cercano(tramosRio, vx, vy, out var dir);
                    mejor = puntos; vado = (vx, vy); dirRio = dir;
                }
            }
            Registro.Add($"vado ({vado.x:0.0}, {vado.y:0.0}), río hacia ({dirRio.x:0.00}, {dirRio.y:0.00}), puntos {mejor}");
            var normal = (-dirRio.y, dirRio.x);
            var elegidos = new List<(double x, double y)> { vado };
            bool Lejos(double x, double y, double d) => elegidos.All(e => (e.x - x) * (e.x - x) + (e.y - y) * (e.y - y) >= d * d);
            // los caminos de prueba (salvo el del vado), lejos de los ríos: el grafo los cruza por cualquier sitio
            var riosCerca = orillas.Where(q => Math.Abs(q.x - vado.x) < 220 && Math.Abs(q.y - vado.y) < 220).ToList();
            bool LejosDelRio(double x, double y) => riosCerca.All(q => (q.x - x) * (q.x - x) + (q.y - y) * (q.y - y) >= 30 * 30);
            (double x, double y) Mejor(Func<double, double, double> puntos, double radio = 100)
            {
                (double x, double y) r = vado;
                double m = double.NegativeInfinity;
                for (double dy = -radio; dy <= radio; dy += 2)
                    for (double dx = -radio; dx <= radio; dx += 2)
                    {
                        double x = vado.x + dx, y = vado.y + dy;
                        if (!Lejos(x, y, 26) || !Seco(x, y, 18) || !LejosDelRio(x, y)) continue;
                        double p = puntos(x, y);
                        if (p > m) { m = p; r = (x, y); }
                    }
                elegidos.Add(r);
                return r;
            }
            var pradera0 = Mejor((x, y) => Bosque(x, y) < 0.08 && Bosque(x + 10, y) < 0.15 && Bosque(x - 10, y) < 0.15 ? Fertil(x, y) - Pendiente(x, y) : -9);
            // el bosque: un pasillo del grafo por bosque cerrado (el mosaico «cerrado» va desde 0,7 de densidad): los nodos con esa densidad,
            // unidos por sus aristas; del grupo con el camino más largo (a menos de 600 u del vado), el trecho del medio de ese camino (60 u).
            var caminoBosque = PasilloDeBosque(Bosque, vado, 600, 60);
            var bosque0 = caminoBosque.Count > 0 ? caminoBosque[caminoBosque.Count / 2] : vado;
            elegidos.Add(bosque0);
            var ladera0 = Mejor((x, y) => Math.Min(Pendiente(x, y), 0.35) + (Pendiente(x + 6, y) > 0.08 && Pendiente(x - 6, y) > 0.08 ? 0.1 : 0));
            var cruce0 = Mejor((x, y) => -Pendiente(x, y) - Math.Abs(Bosque(x, y) - 0.3), 90);

            // 1. El vado: de orilla a orilla por el grafo.
            var v0 = (vado.x - normal.Item1 * 18, vado.y - normal.Item2 * 18);
            var v1 = (vado.x + normal.Item1 * 18, vado.y + normal.Item2 * 18);
            PorVados.Clear();
            var tVado = Tramo("banco-vado", 1, v0, v1);
            Registro.Add("el camino del vado cruza por los nodos de vado " + string.Join(", ", PorVados.Distinct().Select(q => $"({q.x:0.0}, {q.y:0.0})")));
            tramos.Add(tVado);
            double cercaVado = tVado.Puntos.Min(q => Math.Sqrt(Math.Pow(q.X / 1000.0 - vado.x, 2) + Math.Pow(q.Y / 1000.0 - vado.y, 2)));
            Registro.Add($"el camino del vado pasa a {cercaVado:0.0} u del vado");
            Sitios["vado"] = vado;
            // 2. Sendero por pradera; 3. camino por bosque denso; 6. ladera (subiendo por la pendiente).
            PorVados.Clear();
            tramos.Add(Tramo("banco-sendero", 0, (pradera0.x - 16, pradera0.y + 3), pradera0, (pradera0.x + 16, pradera0.y - 2)));
            Sitios["sendero"] = pradera0;
            var tBosque = new TramoDeRed { Id = "banco-bosque", Escalon = 1 };
            foreach (var q in caminoBosque) tBosque.Puntos.Add(Pu(q.x, q.y));
            var dirBosque = caminoBosque.Count > 1 ? (x: caminoBosque[caminoBosque.Count - 1].x - caminoBosque[0].x, y: caminoBosque[caminoBosque.Count - 1].y - caminoBosque[0].y) : (x: 1.0, y: 0.0);
            double ld = Math.Max(1e-6, Math.Sqrt(dirBosque.x * dirBosque.x + dirBosque.y * dirBosque.y));
            dirBosque = (dirBosque.x / ld, dirBosque.y / ld);
            tramos.Add(tBosque);
            Sitios["camino_bosque"] = bosque0;
            {
                // cuánto del camino va por bosque cerrado (densidad ≥ 0,7), a lo largo
                double dentro = 0, total = 0;
                for (int k = 1; k < tBosque.Puntos.Count; k++)
                {
                    double ax = tBosque.Puntos[k - 1].X / 1000.0, ay = tBosque.Puntos[k - 1].Y / 1000.0, bx = tBosque.Puntos[k].X / 1000.0, by = tBosque.Puntos[k].Y / 1000.0;
                    double l = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                    total += l;
                    if (Bosque((ax + bx) / 2, (ay + by) / 2) >= 0.7) dentro += l;
                }
                Registro.Add($"camino por el bosque: ({bosque0.x:0.0}, {bosque0.y:0.0}) hacia ({dirBosque.x:0.00}, {dirBosque.y:0.00}); {dentro:0} de {total:0} u por bosque cerrado");
            }
            Registro.Add("vados de los demás caminos: " + (PorVados.Count == 0 ? "ninguno" : string.Join(", ", PorVados.Distinct().Select(q => $"({q.x:0.0}, {q.y:0.0})"))));
            nuevo = Tramo("banco-nuevo", 1, (bosque0.x - dirBosque.y * 14 + dirBosque.x * 6, bosque0.y + dirBosque.x * 14 + dirBosque.y * 6),
                (bosque0.x + dirBosque.y * 14 + dirBosque.x * 6, bosque0.y - dirBosque.x * 14 + dirBosque.y * 6));
            double gx = relieve.AlturaEn(ladera0.x + 2, ladera0.y) - relieve.AlturaEn(ladera0.x - 2, ladera0.y);
            double gy = relieve.AlturaEn(ladera0.x, ladera0.y + 2) - relieve.AlturaEn(ladera0.x, ladera0.y - 2);
            double gl = Math.Max(1e-6, Math.Sqrt(gx * gx + gy * gy));
            tramos.Add(Tramo("banco-ladera", 1, (ladera0.x - gx / gl * 16, ladera0.y - gy / gl * 16), (ladera0.x + gx / gl * 16, ladera0.y + gy / gl * 16)));
            Sitios["ladera"] = ladera0;
            // 4. Un cruce de tres ramas (dos caminos y un sendero).
            var j = Nodo(cruce0);
            tramos.Add(Tramo("banco-cruce-1", 1, j, (j.x + 18, j.y + 2)));
            tramos.Add(Tramo("banco-cruce-2", 1, j, (j.x - 9, j.y + 16)));
            tramos.Add(Tramo("banco-cruce-3", 0, j, (j.x - 10, j.y - 15)));
            Sitios["cruce"] = j;

            // El territorio: un llano sin agua a menos de 55 u, cerca del vado.
            (double x, double y) llano = vado;
            double mejorLlano = double.NegativeInfinity;
            for (double dy = -220; dy <= 220; dy += 8)
                for (double dx = -220; dx <= 220; dx += 8)
                {
                    double x = vado.x + dx, y = vado.y + dy;
                    if (!Lejos(x, y, 70)) continue;
                    double p = -Pendiente(x, y) - Pendiente(x + 15, y) - Pendiente(x - 15, y);
                    if (p <= mejorLlano || !Seco(x, y, 55)) continue;
                    mejorLlano = p; llano = (x, y);
                }
            var P = Nodo(llano);
            Registro.Add($"llano del territorio ({P.x:0.0}, {P.y:0.0})");
            // las plazas: A y A2 (tuyas, se tocan), B (ajena, pegada a A2), C (oscura), D (sin Facción) y R (recordada, ajena)
            var discos = new List<TrazadoDelMapa.Disco>
            {
                new TrazadoDelMapa.Disco { Id = "as-a", Centro = P, Radio = RadioZona, FaccionId = "t-a" },
                new TrazadoDelMapa.Disco { Id = "as-a2", Centro = Nodo((P.x + 16, P.y + 6)), Radio = RadioZona, FaccionId = "t-a2" },
                new TrazadoDelMapa.Disco { Id = "as-b", Centro = Nodo((P.x + 33, P.y + 9)), Radio = RadioZona, FaccionId = "t-b" },
                new TrazadoDelMapa.Disco { Id = "as-c", Centro = Nodo((P.x - 6, P.y + 32)), Radio = RadioZona, FaccionId = "t-c" },
                new TrazadoDelMapa.Disco { Id = "as-d", Centro = Nodo((P.x - 32, P.y + 2)), Radio = RadioZona, FaccionId = "t-d" },
            };
            var zonas = discos.ToDictionary(d => d.Id, d => TrazadoDelMapa.PoligonoDeZona(d, discos));
            void Plaza(string id, string nombre, string faccion, int i)
            {
                var a = new Asentamiento { Id = id, Nombre = nombre, Nivel = 2, Posicion = Pu(discos[i].Centro.x, discos[i].Centro.y) };
                if (faccion != null) a.FaccionId = faccion;
                foreach (var q in zonas[id]) a.Zona.Add(Pu(q.x, q.y));
                cosas.Add(new Entidad { Asentamiento = a });
            }
            Plaza("as-a", "Micenas", "f-1", 0);
            Plaza("as-a2", "Tirinto", "f-1", 1);
            Plaza("as-b", "Pilos", "f-2", 2);
            Plaza("as-c", "Tebas", "f-3", 3);
            Plaza("as-d", "Orcómeno", null, 4);
            var R = Nodo((P.x + 30, P.y - 22));
            cosas.Add(new Entidad { FichaConocida = new FichaConocida { AsentamientoId = "as-r", Nombre = "Esparta", FaccionId = "f-2", Nivel = 1, Tipo = TipoEntidad.Asentamiento, Posicion = Pu(R.x, R.y), RadioPotencial = (long)(RadioZona * 1000) } });
            // la Facción oscura: su sigilo, negro
            cosas.Add(new Entidad { Faccion = new Faccion { Id = "f-3", Nombre = "Oscura", Sigilo = new Sigilo { FormaId = "clasico", CampoId = "liso", EmblemaId = "sin_bandera", ColorPrimarioId = "negro", ColorSecundarioId = "blanco", ColorEmblemaId = "blanco", OrlaId = "ninguna", ColorOrlaId = "negro" } } });
            // tu zona fusionada: A ∪ A2 (estrellada desde el punto medio: sus vértices por ángulo)
            var medio = ((discos[0].Centro.x + discos[1].Centro.x) / 2, (discos[0].Centro.y + discos[1].Centro.y) / 2);
            var union = zonas["as-a"].Concat(zonas["as-a2"])
                .GroupBy(q => (Math.Round(q.x, 3), Math.Round(q.y, 3))).Select(g => g.First())
                .OrderBy(q => Math.Atan2(q.y - medio.Item2, q.x - medio.Item1)).ToList();
            var zf = new ZonaFaccion { FaccionId = "f-1" };
            zonaPropia = zf;
            Medidas.Add(("azul sobre seco", discos[0].Centro, RadioZona, (0, -1)));
            var contorno = new ContornoZona();
            foreach (var q in union) contorno.Puntos.Add(Pu(q.x, q.y));
            zf.Contornos.Add(contorno);
            cosas.Add(new Entidad { ZonaFaccion = zf });
            // 5. Un camino que pasa por la plaza A y su pueblo, y sigue a la plaza ajena.
            var Q = Nodo((P.x + 4, P.y - 9));
            cosas.Add(new Entidad { Pueblo = new Pueblo { Id = "pu-a", AsentamientoMadreId = "as-a", TipoExtractor = "aldeaAgricola", Posicion = Pu(Q.x, Q.y) } });
            tramos.Add(Tramo("banco-plaza", 1, (P.x - 20, P.y - 4), P, Q, (P.x + 22, P.y - 12)));
            Sitios["plaza_pueblo"] = ((P.x + Q.x) / 2, (P.y + Q.y) / 2);
            // El anillo de un campamento de mercenarios, lejos de todo.
            var M = Nodo((P.x - 30, P.y - 30));
            cosas.Add(new Entidad { CampamentoMercenarios = new CampamentoMercenarios { Id = "banco-merc", Posicion = Pu(M.x, M.y) } });

            (double, double) Mitad((double x, double y) a, (double x, double y) b) => ((a.x + b.x) / 2, (a.y + b.y) / 2);
            Sitios["frontera_propia"] = (P.x - 6, P.y - 10);
            Sitios["frontera_vecino"] = Mitad(discos[1].Centro, discos[2].Centro);
            Sitios["provincias"] = Mitad(discos[0].Centro, discos[1].Centro);
            Sitios["faccion_oscura"] = discos[3].Centro;
            Sitios["sin_faccion"] = discos[4].Centro;
            Sitios["recordada"] = R;
            Sitios["anillo_mercenarios"] = M;
            foreach (var kv in Sitios) Registro.Add($"sitio {kv.Key}: ({kv.Value.x:0.0}, {kv.Value.y:0.0}), {relieve.AlturaEn(kv.Value.x, kv.Value.y):0} m, bosque {Bosque(kv.Value.x, kv.Value.y):0.00}, pendiente {Pendiente(kv.Value.x, kv.Value.y):0.00}");
        }

        static void Vector2Cercano(List<RiosDelMapa.Tramo> tramos, double x, double y, out (double x, double y) dir)
        {
            dir = (1, 0);
            double dmin = double.MaxValue;
            foreach (var t in tramos)
                for (int i = 0; i < t.Puntos.Count; i++)
                {
                    double d = (t.Puntos[i].x - x) * (t.Puntos[i].x - x) + (t.Puntos[i].y - y) * (t.Puntos[i].y - y);
                    if (d >= dmin || t.Puntos.Count < 2) continue;
                    dmin = d;
                    var a = t.Puntos[Math.Max(0, i - 1)];
                    var b = t.Puntos[Math.Min(t.Puntos.Count - 1, i + 1)];
                    double l = Math.Max(1e-6, Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y)));
                    dir = ((b.x - a.x) / l, (b.y - a.y) / l);
                }
        }
    }
}
