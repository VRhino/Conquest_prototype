// El árbol de tropas portado da la misma disposición que el prototipo (BronzeAgeClient `grafoDeTropas` + `disponerTropas`) con los mismos datos.
// `ArbolDeTropas.esperado.json` lo sacó el código del prototipo sobre `ArbolDeTropas.datos.json`, con el árbol entero del campamento (las Eras
// I a V, sin carriles plegados y, del Centro Urbano, solo las tres de leva comunal que presta el campamento).
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ConquestTactics.Mundo.Tests
{
    public class ArbolDeTropasTests
    {
        static JObject Leer(string nombre) => JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Tests/EditMode/Mundo", nombre)));

        [Test]
        public void LaMismaDisposicionQueElPrototipo()
        {
            var d = Leer("ArbolDeTropas.datos.json");
            var esperado = Leer("ArbolDeTropas.esperado.json");
            var a = new ArbolDeTropas((JObject)d["TECNOLOGIAS"], (JObject)d["ERAS"], (JObject)d["EDIFICIO_CATALOGO"], (JArray)d["TROPAS_RECLUTABLES"]);
            CollectionAssert.AreEqual(new[] { "centroUrbano", "barracon", "galeriaDeTiro", "caballerizas" }, a.Carriles.Select(c => c.Id).ToArray());
            var presta = new[] { "milicia_lanceros", "lenadores", "granjeros" };
            var ocultas = a.Nodos.Values.Where(n => n.Carril == "centroUrbano" && !presta.Contains(n.T.Id)).Select(n => n.T.Id).ToHashSet();
            var lay = a.Disponer(5, null, ocultas);
            Assert.AreEqual((int)esperado["NCOL"], lay.NCOL, "columnas");
            foreach (var p in ((JObject)esperado["filas"]).Properties()) Assert.AreEqual((int)p.Value, lay.Filas[p.Name], "filas de " + p.Name);
            var nodos = (JObject)esperado["nodos"];
            Assert.AreEqual(nodos.Count, a.Nodos.Values.Count(n => n.Col != null), "tropas en el tablero");
            foreach (var p in nodos.Properties())
            {
                var n = a.Nodos[p.Name];
                Assert.AreEqual((string)p.Value["preds"], string.Join(",", n.Preds.Select(x => x.De + ":" + x.Tipo)), "previas de " + p.Name);
                Assert.AreEqual((int)p.Value["col"], n.Col, "columna de " + p.Name);
                Assert.AreEqual((int)p.Value["fila"], n.Fila, "fila de " + p.Name);
            }
        }

        [Test]
        public void PorTierElMasBajoALaIzquierdaHastaDosColumnasYLosCarrilesDeAntes()
        {
            var d = Leer("ArbolDeTropas.datos.json");
            var a = new ArbolDeTropas((JObject)d["TECNOLOGIAS"], (JObject)d["ERAS"], (JObject)d["EDIFICIO_CATALOGO"], (JArray)d["TROPAS_RECLUTABLES"]);
            var lay = a.Disponer(5, null, null, porTier: true);
            var vis = a.Nodos.Values.Where(n => n.Col != null).ToList();
            Assert.AreEqual(a.Nodos.Count, vis.Count, "todas en el tablero");
            CollectionAssert.AreEqual(new[] { "centroUrbano", "barracon", "galeriaDeTiro", "caballerizas" }, a.Carriles.Select(c => c.Id).ToArray(), "los carriles de siempre");
            foreach (var e in lay.ErasVis)
            {
                var deEra = vis.Where(n => n.Era == e.Orden).ToList();
                if (deEra.Count == 0) continue;
                foreach (var n in deEra) Assert.That(n.Col, Is.InRange(lay.BaseEra[e.Orden], lay.BaseEra[e.Orden] + lay.ColsEra[e.Orden] - 1), "dentro de su Era: " + n.T.Id);
                var tiers = deEra.Select(n => n.T.Tier).Distinct().OrderBy(x => x).ToList();
                for (int i = 0; i < tiers.Count; i++)
                {
                    var cols = deEra.Where(n => n.T.Tier == tiers[i]).Select(n => n.Col.Value).Distinct().ToList();
                    Assert.LessOrEqual(cols.Max() - cols.Min() + 1, ArbolDeTropas.ColumnasPorTier, $"Era {e.Orden}, tier {tiers[i]}: hasta dos columnas");
                    if (i > 0) Assert.Greater(cols.Min(), deEra.Where(n => n.T.Tier == tiers[i - 1]).Max(n => n.Col.Value), $"Era {e.Orden}: el tier {tiers[i]} a la derecha del {tiers[i - 1]} en todos los carriles");
                }
            }
            foreach (var g in vis.GroupBy(n => (n.Carril, n.Col, n.Fila))) Assert.AreEqual(1, g.Count(), $"una sola tropa en {g.Key}");
            foreach (var c in a.Carriles) Assert.AreEqual(Math.Max(1, vis.Where(n => n.Carril == c.Id).Select(n => n.Fila.Value + 1).DefaultIfEmpty(0).Max()), lay.Filas[c.Id], "filas de " + c.Id);
        }
    }
}
