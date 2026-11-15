// El árbol de tecnologías de BronzeAgeClient (`arbolTecnologias.ts`: `construirGrafo`, `disponer`, `estadoDe` y las Eras que se descubren),
// portado para la pestaña Tecnología de tu Facción (paso 8 del bloque 4, tanda 3). Los requisitos salen de `ArbolDeTropas` (`requisitos` del
// prototipo, el mismo balance); el estado de cada carta, de las tecnologías de la Facción y del estado del mundo.
//   - Carriles por tema (filas) × Eras (columnas). Las líneas, la lista fija `DependeDe` o las tecnologías del hito; las de otro carril las dice
//     la carta («Pide X») y se ven al pasar el ratón o con «Ver todas las conexiones».
//   - Se descubre por Eras: la siguiente aparece al adoptar el 30 % de la actual (con las de arranque, hacia arriba).
// La prueba `ArbolDeTecnologiasTests` compara la disposición con la del prototipo sobre los mismos datos.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ConquestTactics.Mundo
{
    public sealed class ArbolDeTecnologias
    {
        public sealed class Nodo
        {
            public string Id, Nombre, EraId, Carril;
            /// <summary>La Era en el tablero: la suya o la más alta de lo que pide.</summary>
            public int Era;
            public bool DeArranque;
            public JObject D;
            public ArbolDeTropas.Acc Acc;
            public List<string> Preds = new List<string>(), Hijos = new List<string>();
            public int? Col, Fila;
        }

        public sealed class Carril { public string Id, Nombre, Icono; public List<string> Tecs; }

        public readonly ArbolDeTropas A;
        public readonly Dictionary<string, Nodo> Nodos = new Dictionary<string, Nodo>();
        /// <summary>En el orden del catálogo.</summary>
        public readonly List<string> Orden = new List<string>();
        public readonly List<Carril> Carriles = new List<Carril>();
        /// <summary>La tarifa de adoptar, por Era: recurso → cantidad.</summary>
        public readonly Dictionary<string, Dictionary<string, long>> Tarifa = new Dictionary<string, Dictionary<string, long>>();

        /// <summary>Al 30 % de adoptadas de una Era aparece la siguiente (Víctor, 2026-11-06).</summary>
        public const double UmbralEra = 0.3;

        /// <summary>Tecnologías que en pantalla se llaman distinto que en el motor (`NOMBRE_TEC_EN_PANTALLA`).</summary>
        static readonly Dictionary<string, string> NombreEnPantalla = new Dictionary<string, string> { ["cria_caballar"] = "Cría de caballos" };

        static readonly (string id, string nombre, string icono, string[] tecs)[] CarrilesBase =
        {
            ("gobierno", "Gobierno y obras", "salaConsejo", new[] { "canteria", "instituciones_civicas", "cuerpo_oficiales", "logistica_campana" }),
            ("infanteria", "Infantería", "barracon", new[] { "leva_comunal", "escudos_ligeros", "armamento_palacial", "disciplina_formacion", "panoplia_bronce", "ciudadania_militar", "falange_hoplita", "ejercito_permanente", "infanteria_profesional", "reforma_macedonica", "falange_sarisa", "cuerpo_hipaspistas" }),
            ("metal", "Metalurgia", "fundicion", new[] { "metalurgia_cobre", "aleacion_bronce", "bronce_calidad_militar", "forja_hierro_temprana", "bronce_laminado", "forja_hierro_estandarizada", "acero_templado" }),
            ("proyectiles", "Proyectiles", "galeriaDeTiro", new[] { "hostigamiento_tribal", "arqueria_palacial", "arco_compuesto", "pantalla_escaramuzadores", "arqueria_especializada", "hostigadores_profesionales" }),
            ("caballeria", "Caballería", "caballerizas", new[] { "cria_caballar", "carros_guerra", "equitacion_militar", "caballeria_organizada", "carga_caballeria", "formacion_cuna", "companeros_reales" }),
            ("asedio", "Ingeniería y asedio", "carpinteria", new[] { "carpinteria_militar", "trabajos_asedio" }),
        };

        /// <summary>Las líneas del árbol (Víctor, 2026-11-06): una lista fija, solo de pantalla. Lo que no está va tras las tecnologías de su hito.</summary>
        static readonly Dictionary<string, string[]> DependeDe = new Dictionary<string, string[]>
        {
            ["aleacion_bronce"] = new[] { "metalurgia_cobre" },
            ["bronce_calidad_militar"] = new[] { "aleacion_bronce" },
            ["bronce_laminado"] = new[] { "bronce_calidad_militar" },
            ["forja_hierro_estandarizada"] = new[] { "forja_hierro_temprana" },
            ["armamento_palacial"] = new[] { "metalurgia_cobre" },
            ["panoplia_bronce"] = new[] { "bronce_calidad_militar", "armamento_palacial" },
            ["arco_compuesto"] = new[] { "arqueria_palacial", "aleacion_bronce" },
            ["arqueria_especializada"] = new[] { "arco_compuesto" },
            ["equitacion_militar"] = new[] { "cria_caballar" },
            ["caballeria_organizada"] = new[] { "forja_hierro_temprana", "equitacion_militar" },
        };

        public ArbolDeTecnologias(JObject tecnologias, JObject tarifa, ArbolDeTropas a)
        {
            A = a;
            if (tarifa != null)
                foreach (var p in tarifa.Properties())
                    Tarifa[p.Name] = (p.Value as JObject)?.Properties().ToDictionary(x => x.Name, x => (long)x.Value) ?? new Dictionary<string, long>();
            var carrilDe = new Dictionary<string, string>();
            foreach (var (id, nombre, icono, tecs) in CarrilesBase)
            {
                var c = new Carril { Id = id, Nombre = nombre, Icono = icono, Tecs = tecs.Where(t => tecnologias[t] != null).ToList() };
                Carriles.Add(c);
                foreach (var t in c.Tecs) carrilDe[t] = id;
            }
            var sueltas = tecnologias.Properties().Select(p => p.Name).Where(t => !carrilDe.ContainsKey(t)).ToList();
            if (sueltas.Count > 0) { Carriles.Add(new Carril { Id = "otras", Nombre = "Otras", Icono = "plaza", Tecs = sueltas }); foreach (var t in sueltas) carrilDe[t] = "otras"; }

            foreach (var p in tecnologias.Properties())
            {
                var d = (JObject)p.Value;
                Nodos[p.Name] = new Nodo
                {
                    Id = p.Name, D = d, EraId = (string)d["era"], Carril = carrilDe[p.Name], DeArranque = (bool?)d["deArranque"] ?? false,
                    Nombre = NombreEnPantalla.TryGetValue(p.Name, out var n) ? n : (string)d["nombre"] ?? p.Name,
                    Acc = a.RequisitosDeTecnologia(p.Name),
                };
                Orden.Add(p.Name);
            }
            foreach (var n in Nodos.Values)
            {
                n.Preds = (DependeDe.TryGetValue(n.Id, out var l) ? l : (n.D["hito"] as JArray ?? new JArray()).OfType<JObject>()
                    .Where(h => (string)h["tipo"] == "tecnologia").Select(h => (string)h["id"])).Where(Nodos.ContainsKey).ToList();
                n.Era = Math.Max(a.EraDe(n.EraId), n.Acc.Orden.Where(Nodos.ContainsKey).Select(t => a.EraDe(Nodos[t].EraId)).DefaultIfEmpty(0).Max());
            }
            foreach (var id in Orden) foreach (var p in Nodos[id].Preds) Nodos[p].Hijos.Add(id);
        }

        // ── las Eras ──

        public List<string> TecsDeEra(int orden) => Orden.Where(t => A.EraDe(Nodos[t].EraId) == orden).ToList();
        public int CupoEra(int orden) => (int)Math.Ceiling(UmbralEra * TecsDeEra(orden).Count - 1e-9);
        public int AdoptadasEn(ICollection<string> adoptadas, int orden) => TecsDeEra(orden).Count(adoptadas.Contains);

        /// <summary>La última Era que la Facción ve.</summary>
        public int UltimaEraVisible(ICollection<string> adoptadas)
        {
            var eras = A.Eras;
            var u = eras.Count > 0 ? eras[0].Orden : 1;
            foreach (var e in eras.Take(Math.Max(0, eras.Count - 1)))
            {
                if (e.Orden != u || AdoptadasEn(adoptadas, e.Orden) < CupoEra(e.Orden)) break;
                u = e.Orden + 1;
            }
            return u;
        }

        // ── el resaltado ──

        public HashSet<string> Antes(string id) { var s = new HashSet<string>(); void V(string i) { foreach (var x in Nodos[i].Preds) if (s.Add(x)) V(x); } V(id); return s; }
        public HashSet<string> Despues(string id) { var s = new HashSet<string>(); void V(string i) { foreach (var h in Nodos[i].Hijos) if (s.Add(h)) V(h); } V(id); return s; }

        // ── la disposición (`disponer`) ──

        public ArbolDeTropas.Disposicion Disponer(int eraVisible)
        {
            var d = new ArbolDeTropas.Disposicion { ErasVis = A.Eras.Where(e => e.Orden <= eraVisible).ToList() };
            var prof = new Dictionary<string, int>();
            int Prof(string id)
            {
                if (prof.TryGetValue(id, out var ya)) return ya;
                prof[id] = 0;
                var n = Nodos[id];
                var v = n.Preds.Select(p => Nodos[p]).Where(x => x.Era == n.Era).Select(x => Prof(x.Id)).DefaultIfEmpty(-1).Max() + 1;
                prof[id] = v;
                return v;
            }
            foreach (var e in d.ErasVis)
            {
                d.ColsEra[e.Orden] = 1 + Nodos.Values.Where(n => n.Era == e.Orden).Select(n => Prof(n.Id)).DefaultIfEmpty(0).Max();
                d.BaseEra[e.Orden] = d.NCOL;
                d.NCOL += d.ColsEra[e.Orden];
            }
            foreach (var n in Nodos.Values) { n.Fila = null; n.Col = n.Era <= eraVisible ? d.BaseEra[n.Era] + Prof(n.Id) : (int?)null; }
            foreach (var c in Carriles)
            {
                var occ = new List<HashSet<int>>();
                d.Ocupa[c.Id] = occ;
                bool Libre(int r, int col) => r >= occ.Count || occ[r] == null || !occ[r].Contains(col);
                var ts = c.Tecs.Select(t => Nodos[t]).Where(n => n.Era <= eraVisible).OrderBy(n => n.Col).ThenBy(n => c.Tecs.IndexOf(n.Id)).ToList();
                foreach (var n in ts)
                {
                    var dentro = n.Preds.Select(p => Nodos[p]).Where(x => x.Carril == c.Id && x.Fila != null).ToList();
                    bool Ok(int r)
                    {
                        if (!Libre(r, n.Col.Value)) return false;
                        var p = dentro.FirstOrDefault(q => q.Fila == r);
                        if (p == null) return true;
                        for (int k = p.Col.Value + 1; k < n.Col.Value; k++) if (!Libre(r, k)) return false;
                        return true;
                    }
                    n.Fila = dentro.Select(x => x.Fila.Value).Concat(Enumerable.Range(0, 12)).Where(Ok).Select(r => (int?)r).FirstOrDefault() ?? 0;
                    while (occ.Count <= n.Fila.Value) occ.Add(null);
                    (occ[n.Fila.Value] ??= new HashSet<int>()).Add(n.Col.Value);
                }
                d.Filas[c.Id] = ts.Count > 0 ? Math.Max(1, occ.Count) : 0;
            }
            return d;
        }

        // ── el estado de cada carta (`estadoDe`) ──

        /// <summary>Lo de la Facción y del mundo que pinta el árbol (`EstadoArbol`).</summary>
        public sealed class Estado
        {
            public HashSet<string> Adoptadas = new HashSet<string>(), Aparecidas = new HashSet<string>();
            /// <summary>Las reveladas por un Aeda: lo cumplido de su hito, condición a condición.</summary>
            public Dictionary<string, List<bool>> Reveladas = new Dictionary<string, List<bool>>();
            public List<(string contador, long umbral)> Logros = new List<(string, long)>();
            public List<(string contador, long umbral)> Pendientes;
            public Dictionary<string, long> Contadores = new Dictionary<string, long>();
            public int Plazas = 1;
            public string EraMundo;
        }

        /// <summary>Las partes del logro de una tecnología (la Aleación del bronce lleva dos: cobre y estaño).</summary>
        public static List<JObject> PartesLogro(JObject d)
        {
            var l = new List<JObject>();
            if (d["logro"] is JObject x) { l.Add(x); if (x["ademas"] is JObject y) l.Add(y); }
            return l;
        }

        /// <summary>El umbral de ahora de una parte: el fijo, o `porPlaza × max(N, 1)`.</summary>
        public static long UmbralDe(Estado e, JObject parte) => (long?)parte["umbral"] ?? ((long?)parte["porPlaza"] ?? 0) * Math.Max(1, e.Plazas);

        public static bool ParteCumplida(Estado e, JObject parte)
        {
            var u = UmbralDe(e, parte);
            var c = (string)parte["contador"];
            if (e.Pendientes != null) return !e.Pendientes.Any(l => l.contador == c && l.umbral == u);
            return e.Logros.Any(l => l.contador == c && l.umbral >= u);
        }

        public string EstadoDe(Estado e, string id)
        {
            if (e.Adoptadas.Contains(id)) return "adoptada";
            if (e.Aparecidas.Contains(id)) return "adoptable";
            if (Nodos[id].Era > A.EraDe(e.EraMundo)) return "futura";
            if (e.Reveladas.ContainsKey(id)) return "revelada";
            var partes = PartesLogro(Nodos[id].D);
            if (partes.Count > 0 && partes.All(x => ParteCumplida(e, x))) return "logro";
            return "bloqueada";
        }
    }
}
