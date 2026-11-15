// El árbol de tropas portado da la misma disposición que el prototipo (BronzeAgeClient `grafoDeTropas` + `disponerTropas`) con los mismos datos.
// `ArbolDeTropas.esperado.json` lo sacó el código del prototipo sobre `ArbolDeTropas.datos.json`, con el árbol entero del campamento (las Eras
// I a V, sin carriles plegados y, del Centro Urbano, solo las tres de leva comunal que presta el campamento).
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
    }
}
