// El árbol de tropas de BronzeAgeClient (`arbolReclutamiento.ts`: `grafoDeTropas` y `disponerTropas`; `arbolTecnologias.ts`: `requisitos`),
// portado tal cual para el reclutamiento del campamento (paso 6 del bloque 4, tanda 3). Solo datos del balance (`/v1/datos`: TECNOLOGIAS, ERAS,
// EDIFICIO_CATALOGO y TROPAS_RECLUTABLES); el estado de cada tropa lo pone quien lo pinta.
//   - Carriles = el edificio donde se recluta (Centro Urbano, Barracón, Galería de tiro, Caballerizas).
//   - Una tropa B va tras A cuando B necesita la tecnología que habilita a A: «hito» si es por el hito de su tecnología, «implicita» si la piden
//     su edificio o su equipo, «logro» si solo la pide el logro. De las hermanas (misma tecnología), una sola línea.
//   - Columnas: por Era y, dentro, por el nivel del edificio, siempre a la derecha de su previa de la misma Era.
// La prueba `ArbolDeTropasTests` compara la disposición con la del prototipo sobre los mismos datos.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ConquestTactics.Mundo
{
    public sealed class ArbolDeTropas
    {
        public sealed class Tropa
        {
            public string Id, Nombre, Tecnologia, Edificio;
            public int NivelRequerido, Escalon, Unidades;
            public List<string> Equipo;
        }

        public sealed class Arista { public string De, Tipo; }

        public sealed class Nodo
        {
            public Tropa T;
            public int Era;
            public string Carril;
            /// <summary>Las tecnologías que pide (todas las de su cadena).</summary>
            public List<string> Tecs;
            public List<Arista> Cands = new List<Arista>(), Todos = new List<Arista>(), Preds = new List<Arista>();
            public List<string> Hijos = new List<string>(), HijosTodos = new List<string>();
            public int? Col, Fila;
        }

        public sealed class Era { public string Id, Nombre; public int Orden, PlazoSemanas; public int? MinimoSemanas; }
        public sealed class Carril { public string Id; public List<string> Tropas; }

        public readonly List<Tropa> Tropas = new List<Tropa>();
        /// <summary>En el orden de `Tropas`.</summary>
        public readonly Dictionary<string, Nodo> Nodos = new Dictionary<string, Nodo>();
        public readonly List<string> OrdenNodos = new List<string>();
        public readonly List<Carril> Carriles = new List<Carril>();
        public readonly List<Era> Eras = new List<Era>();

        // ── el balance ──

        sealed class Tec { public string Era; public JArray Hito; public string Contador; }
        sealed class Edificio { public int Plaza; public string Tec; public Dictionary<int, (int plaza, string edificio, int edificioNivel, string tec)> Niveles = new Dictionary<int, (int, string, int, string)>(); }
        sealed class Receta { public string Edificio, Tec; public int Nivel; public List<string> Consume; }

        readonly Dictionary<string, Tec> tec = new Dictionary<string, Tec>();
        readonly List<string> ordenTec = new List<string>();
        readonly Dictionary<string, int> ordenEra = new Dictionary<string, int>();
        readonly Dictionary<string, Edificio> edificios = new Dictionary<string, Edificio>();
        readonly Dictionary<string, Receta> recetas = new Dictionary<string, Receta>();

        static readonly string[] OrdenCarriles = { "centroUrbano", "barracon", "galeriaDeTiro", "caballerizas" };
        /// <summary>`BRUTO` del prototipo: la tecnología que pide cada materia prima fuera de las recetas (solo el hierro).</summary>
        static readonly Dictionary<string, string> TecDeBruto = new Dictionary<string, string> { ["hierro"] = "forja_hierro_temprana" };
        static readonly HashSet<string> Brutos = new HashSet<string> { "madera", "piedra", "cobre", "estano", "hierro", "livestock", "trigo", "oro" };
        static readonly HashSet<string> HitoPueblo = new HashSet<string> { "corral", "cantera", "minaHierro" };

        public int EraDe(string eraId) => eraId != null && ordenEra.TryGetValue(eraId, out var o) ? o : 99;

        public ArbolDeTropas(JObject tecnologias, JObject eras, JObject catalogo, JArray tropas)
        {
            foreach (var p in tecnologias.Properties())
            {
                var t = (JObject)p.Value;
                tec[p.Name] = new Tec { Era = (string)t["era"], Hito = t["hito"] as JArray ?? new JArray(), Contador = (string)t["logro"]?["contador"] };
                ordenTec.Add(p.Name);
            }
            foreach (var p in eras.Properties())
            {
                ordenEra[p.Name] = (int)p.Value["orden"];
                Eras.Add(new Era { Id = p.Name, Nombre = (string)p.Value["nombre"], Orden = (int)p.Value["orden"], PlazoSemanas = (int?)p.Value["plazoSemanas"] ?? 0, MinimoSemanas = (int?)p.Value["minimoSemanas"] });
            }
            Eras.Sort((a, b) => a.Orden.CompareTo(b.Orden));
            foreach (var p in catalogo.Properties())
            {
                var v = (JObject)p.Value;
                var e = new Edificio { Plaza = (int?)v["requisitoNivelAsentamientoConstruccion"] ?? 1, Tec = (string)v["requiereTecnologia"] };
                var niveles = (v["niveles"] as JObject)?.Properties().OrderBy(x => int.Parse(x.Name)).ToList() ?? new List<JProperty>();
                foreach (var n in niveles)
                {
                    var l = (JObject)n.Value;
                    e.Niveles[int.Parse(n.Name)] = ((int?)l["requisitoNivelAsentamiento"] ?? 0, (string)l["requiereEdificio"], (int?)l["requiereEdificioNivel"] ?? 0, (string)l["requiereTecnologia"]);
                }
                edificios[p.Name] = e;
                if (p.Name == "granFundicion") continue;
                foreach (var n in niveles)
                    foreach (var r in n.Value["recetas"] as JArray ?? new JArray())
                    {
                        var produce = (string)r["produce"];
                        if (produce != null && !recetas.ContainsKey(produce))
                            recetas[produce] = new Receta { Edificio = p.Name, Nivel = int.Parse(n.Name), Tec = (string)r["requiereTecnologia"],
                                Consume = (r["consumePorUnidad"] as JObject)?.Properties().Select(x => x.Name).ToList() ?? new List<string>() };
                    }
            }
            foreach (var t in tropas.OfType<JObject>())
                Tropas.Add(new Tropa
                {
                    Id = (string)t["id"], Nombre = (string)t["nombre"], Tecnologia = (string)t["tecnologia"], Edificio = (string)t["edificio"],
                    NivelRequerido = (int?)t["nivelRequerido"] ?? 1, Escalon = (int?)t["escalon"] ?? 0, Unidades = (int?)t["unidadesPorDefecto"] ?? 0,
                    Equipo = (t["costoEquipo"] as JObject)?.Properties().Select(x => x.Name).ToList() ?? new List<string>(),
                });
            Construir();
        }

        // ── requisitos (`requisitos` del prototipo) ──

        /// <summary>Tecnología → motivos, en orden de llegada.</summary>
        sealed class Acc { public readonly List<string> Orden = new List<string>(); public readonly Dictionary<string, HashSet<string>> Por = new Dictionary<string, HashSet<string>>(); public readonly Dictionary<string, int> Eds = new Dictionary<string, int>(); }

        static void Anota(Acc a, string t, string por)
        {
            if (string.IsNullOrEmpty(t)) return;
            if (!a.Por.TryGetValue(t, out var s)) { a.Por[t] = s = new HashSet<string>(); a.Orden.Add(t); }
            s.Add(por);
        }

        void ReqEdificio(Acc a, string tipo, int nivel)
        {
            if (tipo == "centroUrbano") return;
            if ((a.Eds.TryGetValue(tipo, out var ya) ? ya : 0) >= nivel) return;
            a.Eds[tipo] = nivel;
            if (!edificios.TryGetValue(tipo, out var e)) return;
            Anota(a, e.Tec, "e:" + tipo);
            for (int n = 2; n <= nivel; n++)
            {
                if (!e.Niveles.TryGetValue(n, out var l)) continue;
                Anota(a, l.tec, $"e:{tipo}:{n}");
                if (l.edificio != null) ReqEdificio(a, l.edificio, l.edificioNivel > 0 ? l.edificioNivel : 1);
            }
        }

        void ReqRecurso(Acc a, string r, HashSet<string> vistos = null)
        {
            vistos ??= new HashSet<string>();
            if (!vistos.Add(r)) return;
            if (recetas.TryGetValue(r, out var rc))
            {
                Anota(a, rc.Tec, "r:" + r);
                ReqEdificio(a, rc.Edificio, rc.Nivel);
                foreach (var x in rc.Consume) ReqRecurso(a, x, vistos);
            }
            else if (Brutos.Contains(r) && TecDeBruto.TryGetValue(r, out var t)) Anota(a, t, "r:" + r);
        }

        Acc ReqTropa(Tropa t)
        {
            var a = new Acc();
            Anota(a, t.Tecnologia, "la habilita");
            ReqEdificio(a, t.Edificio, t.NivelRequerido);
            foreach (var r in t.Equipo) ReqRecurso(a, r);
            return a;
        }

        List<string> LogroDe(string id)
        {
            var c = tec.TryGetValue(id, out var x) ? x.Contador ?? "" : "";
            List<string> DeTropa(string tid) { var t = Tropas.FirstOrDefault(y => y.Id == tid); return t != null ? ReqTropa(t).Orden : new List<string>(); }
            List<string> DeRecurso(string r) { var a = new Acc(); ReqRecurso(a, r); return a.Orden; }
            var vias = new List<List<string>>();
            if (c == "reclutados.caballeria") vias = Tropas.Where(t => t.Edificio == "caballerizas").Select(t => DeTropa(t.Id)).ToList();
            else if (c.StartsWith("reclutados.") && c != "reclutados.escuadrones") vias = new List<List<string>> { DeTropa(c.Split('.')[1]) };
            else if (c == "batallas.conHoplitas") vias = new List<List<string>> { DeTropa("hoplitas_ciudadanos") };
            else if (c == "fabricado.equipoBronce") vias = new[] { "armaBronce", "armaBronceCalidad", "armaduraBronce", "armaduraBronceCalidad" }.Select(DeRecurso).ToList();
            else if (c == "fabricado.armaduraBronce") vias = new List<List<string>> { DeRecurso("armaduraBronce") };
            else if (c == "extraido.hierro") vias = new List<List<string>> { new List<string> { "forja_hierro_temprana" } };
            vias = vias.Where(v => v.Count > 0 && !v.Contains(id)).ToList();
            if (vias.Count == 0) return new List<string>();
            return vias[0].Where(y => tec.ContainsKey(y) && vias.All(v => v.Contains(y))).ToList();
        }

        Acc ReqTec(string id)
        {
            var a = new Acc();
            foreach (var h in tec[id].Hito.OfType<JObject>())
            {
                var tipo = (string)h["tipo"];
                var edificio = (string)h["edificio"];
                if (tipo == "tecnologia") Anota(a, (string)h["id"], "hito");
                else if (tipo == "edificio" && !HitoPueblo.Contains(edificio)) ReqEdificio(a, edificio, (int?)h["nivelInterno"] is int ni && ni > 0 ? ni : 1);
                else if (tipo == "edificio" && edificio == "minaHierro") Anota(a, "forja_hierro_temprana", "pueblo de mina de hierro");
                else if (tipo == "capitalEnNivel") ReqEdificio(a, (string)h["conEdificio"], 1);
                else if (tipo == "edificioEnPlazaDeNivel") ReqEdificio(a, edificio, (int?)h["nivelInterno"] is int nj && nj > 0 ? nj : 1);
            }
            foreach (var x in LogroDe(id)) Anota(a, x, "logro");
            Quitar(a, id);
            foreach (var t in a.Orden.ToList()) if (!tec.ContainsKey(t)) Quitar(a, t);
            return a;
        }

        static void Quitar(Acc a, string t) { if (a.Por.Remove(t)) a.Orden.Remove(t); }

        // ── el grafo (`grafoDeTropas`) ──

        void Construir()
        {
            var tecReq = ordenTec.ToDictionary(id => id, ReqTec);
            string TipoTec(string id, string x)
            {
                var m = tecReq[id].Por[x];
                return m.Contains("hito") ? "hito" : m.All(v => v == "logro") ? "logro" : "implicita";
            }
            HashSet<string> Cierre(IEnumerable<string> desde, Func<string, string, bool> vale)
            {
                var s = new HashSet<string>();
                void V(string t) { if (!tecReq.TryGetValue(t, out var a)) return; foreach (var x in a.Orden) if (vale(t, x) && s.Add(x)) V(x); }
                foreach (var t in desde) { s.Add(t); V(t); }
                return s;
            }
            var accTropa = Tropas.ToDictionary(t => t.Id, ReqTropa);
            foreach (var t in Tropas)
            {
                var directas = accTropa[t.Id].Orden.Where(tec.ContainsKey).ToList();
                var todas = Cierre(directas, (_, __) => true);
                var porHito = Cierre(new[] { t.Tecnologia }, (a, x) => TipoTec(a, x) == "hito");
                var sinLogro = Cierre(directas, (a, x) => TipoTec(a, x) != "logro");
                var n = new Nodo
                {
                    T = t, Carril = t.Edificio, Tecs = directas,
                    Era = Math.Max(1, directas.Select(x => EraDe(tec[x].Era)).DefaultIfEmpty(1).Max()),
                };
                foreach (var a in Tropas)
                    if (a.Tecnologia != t.Tecnologia && todas.Contains(a.Tecnologia))
                        n.Cands.Add(new Arista { De = a.Id, Tipo = porHito.Contains(a.Tecnologia) ? "hito" : sinLogro.Contains(a.Tecnologia) ? "implicita" : "logro" });
                Nodos[t.Id] = n;
                OrdenNodos.Add(t.Id);
            }
            var antT = new Dictionary<string, HashSet<string>>();
            HashSet<string> AntTDe(string id)
            {
                if (antT.TryGetValue(id, out var ya)) return ya;
                var s = new HashSet<string>();
                antT[id] = s;
                foreach (var p in Nodos[id].Cands) { s.Add(p.De); foreach (var x in AntTDe(p.De)) s.Add(x); }
                return s;
            }
            foreach (var id in OrdenNodos)
            {
                var n = Nodos[id];
                n.Todos = n.Cands.Where(p => !n.Cands.Any(q => q.De != p.De && AntTDe(q.De).Contains(p.De))).ToList();
            }
            foreach (var id in OrdenNodos)
            {
                var n = Nodos[id];
                var c = tec.TryGetValue(n.T.Tecnologia, out var tt) ? tt.Contador : null;
                var preferida = c != null && c.StartsWith("reclutados.") ? c.Split('.')[1] : c == "batallas.conHoplitas" ? "hoplitas_ciudadanos" : null;
                var grupos = new List<(string tec, List<Arista> l)>();
                foreach (var p in n.Todos)
                {
                    var k = Nodos[p.De].T.Tecnologia;
                    var g = grupos.FindIndex(x => x.tec == k);
                    if (g < 0) grupos.Add((k, new List<Arista> { p })); else grupos[g].l.Add(p);
                }
                n.Preds = grupos.Select(g => g.l.FirstOrDefault(p => p.De == preferida) ?? g.l.FirstOrDefault(p => Nodos[p.De].Carril == n.Carril) ?? g.l[0]).ToList();
            }
            foreach (var id in OrdenNodos)
            {
                foreach (var p in Nodos[id].Preds) Nodos[p.De].Hijos.Add(id);
                foreach (var p in Nodos[id].Todos) Nodos[p.De].HijosTodos.Add(id);
            }
            foreach (var cid in OrdenCarriles.Concat(Tropas.Select(t => t.Edificio)).Distinct())
            {
                var ts = Tropas.Where(t => t.Edificio == cid).Select(t => t.Id).ToList();
                if (ts.Count > 0) Carriles.Add(new Carril { Id = cid, Tropas = ts });
            }
        }

        /// <summary>Lo que va antes (todas sus previas) y lo que lleva después, para el resaltado.</summary>
        public HashSet<string> Antes(string id) { var s = new HashSet<string>(); void V(string i) { foreach (var x in Nodos[i].Todos) if (s.Add(x.De)) V(x.De); } V(id); return s; }
        public HashSet<string> Despues(string id) { var s = new HashSet<string>(); void V(string i) { foreach (var h in Nodos[i].HijosTodos) if (s.Add(h)) V(h); } V(id); return s; }

        // ── la disposición (`disponerTropas`) ──

        public sealed class Disposicion
        {
            public List<Era> ErasVis;
            public Dictionary<int, int> ColsEra = new Dictionary<int, int>(), BaseEra = new Dictionary<int, int>();
            public int NCOL;
            public Dictionary<string, List<HashSet<int>>> Ocupa = new Dictionary<string, List<HashSet<int>>>();
            public Dictionary<string, int> Filas = new Dictionary<string, int>();
        }

        public Disposicion Disponer(int eraVisible, ISet<string> plegados = null, ISet<string> ocultas = null)
        {
            plegados ??= new HashSet<string>();
            ocultas ??= new HashSet<string>();
            var d = new Disposicion { ErasVis = Eras.Where(e => e.Orden <= eraVisible).ToList() };
            var vis = OrdenNodos.Select(id => Nodos[id]).Where(n => n.Era <= eraVisible && !plegados.Contains(n.Carril) && !ocultas.Contains(n.T.Id)).ToList();
            var enVis = new HashSet<string>(vis.Select(n => n.T.Id));
            var local = new Dictionary<string, int>();
            int ColDe(Nodo n)
            {
                if (local.TryGetValue(n.T.Id, out var ya)) return ya;
                local[n.T.Id] = 0;
                var c = n.Carril == "centroUrbano" ? 0 : n.T.NivelRequerido - 1;
                foreach (var p in n.Preds)
                {
                    var x = Nodos[p.De];
                    if (x.Era == n.Era && enVis.Contains(x.T.Id)) c = Math.Max(c, ColDe(x) + 1);
                }
                local[n.T.Id] = c;
                return c;
            }
            foreach (var n in vis) ColDe(n);
            // El Centro Urbano en cuadro (y una Era tiene al menos dos columnas en el tablero).
            foreach (var e in d.ErasVis)
            {
                var cu = vis.Where(n => n.Era == e.Orden && n.Carril == "centroUrbano" && n.Preds.Count == 0 && !n.Hijos.Any(h => Nodos[h].Era == e.Orden)).ToList();
                var usadas = new HashSet<int>(vis.Where(n => n.Era == e.Orden && n.Carril != "centroUrbano").Select(n => local[n.T.Id]));
                var ancho = Math.Max(1, Math.Min((int)Math.Ceiling(Math.Sqrt(cu.Count)), Math.Max(2, usadas.Count)));
                for (int i = 0; i < cu.Count; i++) local[cu[i].T.Id] = i % ancho;
            }
            var compacta = new Dictionary<string, int>();
            foreach (var e in d.ErasVis)
            {
                var deEra = vis.Where(n => n.Era == e.Orden).ToList();
                var usadas = deEra.Select(n => local[n.T.Id]).Distinct().OrderBy(x => x).ToList();
                foreach (var n in deEra) compacta[n.T.Id] = usadas.IndexOf(local[n.T.Id]);
                d.ColsEra[e.Orden] = Math.Max(1, usadas.Count);
                d.BaseEra[e.Orden] = d.NCOL;
                d.NCOL += d.ColsEra[e.Orden];
            }
            foreach (var n in Nodos.Values) { n.Fila = null; n.Col = enVis.Contains(n.T.Id) ? d.BaseEra[n.Era] + compacta[n.T.Id] : (int?)null; }
            foreach (var c in Carriles)
            {
                var occ = new List<HashSet<int>>();
                d.Ocupa[c.Id] = occ;
                bool Libre(int r, int col) => r >= occ.Count || occ[r] == null || !occ[r].Contains(col);
                var ts = c.Tropas.Select(t => Nodos[t]).Where(n => enVis.Contains(n.T.Id)).Select((n, i) => (n, i))
                    .OrderBy(x => x.n.Col).ThenBy(x => x.n.T.NivelRequerido).ThenBy(x => x.n.T.Escalon).ThenBy(x => c.Tropas.IndexOf(x.n.T.Id)).ThenBy(x => x.i).Select(x => x.n).ToList();
                foreach (var n in ts)
                {
                    var dentro = n.Preds.Select(p => Nodos[p.De]).Where(x => x.Carril == c.Id && x.Fila != null).ToList();
                    bool Ok(int r)
                    {
                        if (!Libre(r, n.Col.Value)) return false;
                        var p = dentro.FirstOrDefault(q => q.Fila == r);
                        if (p == null) return true;
                        for (int k = p.Col.Value + 1; k < n.Col.Value; k++) if (!Libre(r, k)) return false;
                        return true;
                    }
                    var candidatas = dentro.Select(x => x.Fila.Value).Concat(Enumerable.Range(0, 16));
                    n.Fila = candidatas.Where(Ok).Select(r => (int?)r).FirstOrDefault() ?? 0;
                    while (occ.Count <= n.Fila.Value) occ.Add(null);
                    (occ[n.Fila.Value] ??= new HashSet<int>()).Add(n.Col.Value);
                }
                d.Filas[c.Id] = Math.Max(1, occ.Count);
            }
            return d;
        }
    }
}
