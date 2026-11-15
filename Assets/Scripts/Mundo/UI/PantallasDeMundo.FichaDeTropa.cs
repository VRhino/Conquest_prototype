// Lo que le faltaba a la ficha corta de una tropa en el árbol de reclutamiento (paso 8, antes de la tanda 4), calcado de BronzeAgeClient
// (`tipTropa` de `ui/arbolReclutamiento.ts` y `htmlFichaCombate` de `ui/fichaCombate.ts`):
//   - «Edificios y pueblos» (solo en la plaza): el nivel de plaza, el Centro Urbano si es suya, los edificios a su nivel y los pueblos de los
//     que sale su equipo; y qué edificio pide una tecnología para construirse.
//   - «En campaña»: poder, velocidad y el Liderazgo que ocupa la escuadra.
//   - «Combate» con su tier y rareza: vida, bloqueo, cadencia, alcance, precisión y munición, y la tabla por tipo de daño (las filas a cero no
//     salen). Todo del balance (`TROPAS_RECLUTABLES[..].combate`, `poderBase`, `velocidad`, `tier`; `OBJETOS.rarezas`); sin ficha, no sale.
using System.Collections.Generic;
using System.Linq;
using ConquestTactics.Mundo.Mapa;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI
{
    public sealed partial class PantallasDeMundo
    {
        /// <summary>Los pueblos de los que sale una materia prima (`PUEBLO` del prototipo): su icono de edificio y su nombre.</summary>
        static readonly Dictionary<string, (string icono, string nombre)> PuebloDeBruto = new Dictionary<string, (string, string)>
        {
            ["un pueblo de mina de hierro"] = ("minaHierro", "Pueblo de mina de hierro"), ["un pueblo de mina de cobre"] = ("minaCobre", "Pueblo de mina de cobre"),
            ["un pueblo de mina de estaño"] = ("minaEstano", "Pueblo de mina de estaño"), ["un pueblo de cantera"] = ("cantera", "Pueblo de cantera"),
            ["el rebaño de una aldea ganadera"] = ("corral", "Aldea ganadera"), ["impuestos y pueblo de mina de oro"] = ("mina", "Pueblo de mina de oro"),
        };
        static readonly Dictionary<string, string> RarezaEnPantalla = new Dictionary<string, string>
        {
            ["comun"] = "común", ["pocoComun"] = "poco común", ["rara"] = "rara", ["epica"] = "épica", ["legendaria"] = "legendaria",
        };
        static readonly (string id, string nombre)[] TiposDeDano = { ("perforante", "Perforante"), ("cortante", "Cortante"), ("contundente", "Contundente") };
        static readonly (string id, string nombre, string titulo, bool porcentaje)[] FilasDeCombate =
        {
            ("dano", "Daño", "Lo que hace cada golpe, por tipo", false), ("penetracion", "Penetración", "Cuánto atraviesa la defensa del rival, en %", true),
            ("defensa", "Defensa", "Cuánto para de cada tipo de golpe, en %", true), ("alivio", "Alivio", "Cuánto le quita al daño que le llega, en %", true),
        };

        /// <summary>Un icono de atributo de Conquest (`heroe-icono-*`, los de la ficha de la tropa) a tamaño de línea.</summary>
        static VisualElement IconoDeAtributo(string nombre) => Dialogo.Caja("tec-icono heroe-icono-" + nombre);

        /// <summary>Una cifra con su icono (`IC` del prototipo).</summary>
        static VisualElement ChipDeCifra(VisualElement icono, string valor, string titulo)
        {
            var chip = Dialogo.Caja("tec-rec");
            chip.tooltip = titulo;
            if (icono != null) chip.Add(icono);
            if (valor != null) chip.Add(Dialogo.Texto(valor, "tec-rec-cifra"));
            return chip;
        }

        JObject TropaDelBalance(string id) => c.Datos.TropasReclutables?.FirstOrDefault(x => (string)x["id"] == id) as JObject;

        /// <summary>«Edificios y pueblos»: lo que la tropa pide de una plaza.</summary>
        void EdificiosYPueblos(VisualElement tip, ArbolDeTropas a, string id)
        {
            var n = a.Nodos[id];
            var req = n.Req;
            var centro = n.T.Edificio == "centroUrbano";
            tip.Add(Dialogo.Texto("EDIFICIOS Y PUEBLOS", "recl-tip-seccion"));
            var fichas = new List<VisualElement> { ChipDeEdificio("plaza", req.Plaza.ToString(), "Plaza") };
            if (centro) fichas.Add(ChipDeEdificio("centroUrbano", null, "Centro Urbano (lo tiene toda plaza)"));
            fichas.AddRange(req.Eds.Select(p => ChipDeEdificio(p.Key, p.Value.ToString())));
            fichas.AddRange(req.Pueblos.Select(p => PuebloDeBruto.TryGetValue(p, out var x) ? ChipDeEdificio(x.icono, null, x.nombre) : (VisualElement)Dialogo.Texto(p, "tec-renglon-texto")));
            var fila = Renglon("tec-fichas", fichas);
            fila.name = "edificios-y-pueblos";
            tip.Add(fila);
            var conTec = req.Eds.Keys.Select(x => (x, tec: a.TecnologiaDeConstruir(x))).Where(x => x.tec != null).ToList();
            if (conTec.Count > 0)
                tip.Add(Renglon("tec-nota-fila", conTec.SelectMany((x, i) => new object[]
                {
                    ChipDeEdificio(x.x), $"pide <b>{NombreDeTecnologia(x.tec)}</b> para construirse{(i == conTec.Count - 1 ? "." : ";")}",
                }).ToArray()));
        }

        /// <summary>«En campaña» y «Combate» (`stats` y `htmlFichaCombate`).</summary>
        void EnCampanaYCombate(VisualElement tip, string id)
        {
            var t = TropaDelBalance(id);
            if (t == null) return;
            var escalon = (int?)t["escalon"] ?? 1;
            tip.Add(Dialogo.Texto("EN CAMPAÑA", "recl-tip-seccion"));
            var campana = Renglon("tec-fichas",
                ChipDeCifra(IconosDelMundo.Icono("glifo", "atacar", 14), ((double?)t["poderBase"])?.ToString("0.##", Es) ?? "?", "Poder"),
                ChipDeCifra(IconoDeAtributo("velocidad"), ((double?)t["velocidad"])?.ToString("0.##", Es) ?? "?", $"Velocidad: {((double?)t["velocidad"])?.ToString("0.##", Es) ?? "?"} u/min"),
                ChipDeCifra(IconoDeAtributo("liderazgo"), LiderazgoDe(id)?.ToString() ?? "?", "Liderazgo que ocupa la escuadra"));
            campana.name = "en-campana";
            tip.Add(campana);

            if (!(t["combate"] is JObject cb)) return;
            var cab = Renglon("tec-combate-cab", Dialogo.Texto("COMBATE", "recl-tip-seccion"));
            var tier = (int?)t["tier"];
            if (tier != null)
            {
                var clave = (string)BloqueDeDatos("OBJETOS")?["rarezas"]?[tier.ToString()] ?? new[] { "comun", "pocoComun", "rara", "epica", "legendaria" }.ElementAtOrDefault(tier.Value - 1) ?? "comun";
                cab.Add(Dialogo.Texto($"Tier {Romano[System.Math.Min(tier.Value, Romano.Length - 1)]}, {(RarezaEnPantalla.TryGetValue(clave, out var r) ? r : clave)}", "tec-rareza r-" + clave));
            }
            tip.Add(cab);
            string Num(JToken x) => x == null ? null : ((double)x).ToString("0.##", Es);
            var sueltas = new List<VisualElement> { ChipDeCifra(IconoDeAtributo("vida"), Num(cb["vida"]) ?? "?", "Vida por soldado") };
            if ((double?)cb["bloqueo"] is double b && b != 0) sueltas.Add(ChipDeCifra(IconoDeAtributo("bloqueo"), Num(cb["bloqueo"]), "Bloqueo del escudo"));
            sueltas.Add(ChipDeCifra(IconosDelMundo.Icono("simbolo", "reloj", 14), $"{Num(cb["cadencia"]) ?? "?"} s", "Cadencia: segundos entre golpes"));
            sueltas.Add(ChipDeCifra(IconoDeAtributo("alcance"), $"{Num(cb["alcance"]) ?? "?"} m", "Alcance"));
            if (cb["precision"] != null) sueltas.Add(ChipDeCifra(IconoDeAtributo("destreza"), Num(cb["precision"]), "Precisión (1-100)"));
            if (cb["municion"] != null) sueltas.Add(ChipDeCifra(IconoDeAtributo("municion"), Num(cb["municion"]), "Munición"));
            var fila = Renglon("tec-fichas", sueltas);
            fila.name = "combate";
            tip.Add(fila);

            var filas = FilasDeCombate.Where(f => TiposDeDano.Any(x => ((double?)cb[f.id]?[x.id] ?? 0) != 0)).ToList();
            if (filas.Count == 0) return;
            var tabla = Dialogo.Caja("tec-combate");
            tabla.name = "tabla-combate";
            var encabezado = Dialogo.Caja("tec-combate-fila primera");
            encabezado.Add(Dialogo.Caja("tec-combate-nombre"));
            foreach (var (x, nombre) in TiposDeDano)
            {
                var celda = Dialogo.Caja("tec-combate-celda tec-combate-tipo");
                celda.tooltip = nombre;
                celda.Add(IconoDeAtributo(x));
                celda.Add(Dialogo.Texto(nombre, "tec-combate-tipo-texto"));
                encabezado.Add(celda);
            }
            tabla.Add(encabezado);
            foreach (var (k, nombre, titulo, porcentaje) in filas)
            {
                var f = Dialogo.Caja("tec-combate-fila");
                var nom = Dialogo.Texto(nombre, "tec-combate-nombre");
                nom.tooltip = titulo;
                f.Add(nom);
                foreach (var (x, _) in TiposDeDano)
                {
                    var v = (double?)cb[k]?[x] ?? 0;
                    f.Add(Dialogo.Texto(v != 0 ? v.ToString("0.##", Es) + (porcentaje ? " %" : "") : "·", "tec-combate-celda"));
                }
                tabla.Add(f);
            }
            tip.Add(tabla);
        }
    }
}
