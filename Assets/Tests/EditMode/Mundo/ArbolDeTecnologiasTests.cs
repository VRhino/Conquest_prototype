// El árbol de tecnologías portado da la misma disposición que el prototipo (BronzeAgeClient `construirGrafo` + `disponer` de `arbolTecnologias.ts`)
// con los mismos datos (`ArbolDeTropas.datos.json`). `ArbolDeTecnologias.esperado.json` lo sacó el código del prototipo, con las Eras I, III y V a
// la vista, y la última Era visible sin nada adoptado y con las de arranque.
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ConquestTactics.Mundo.Tests
{
    public class ArbolDeTecnologiasTests
    {
        static JObject Leer(string nombre) => JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Tests/EditMode/Mundo", nombre)));

        [Test]
        public void LaMismaDisposicionQueElPrototipo()
        {
            var d = Leer("ArbolDeTropas.datos.json");
            var esperado = Leer("ArbolDeTecnologias.esperado.json");
            var tropas = new ArbolDeTropas((JObject)d["TECNOLOGIAS"], (JObject)d["ERAS"], (JObject)d["EDIFICIO_CATALOGO"], (JArray)d["TROPAS_RECLUTABLES"]);
            var a = new ArbolDeTecnologias((JObject)d["TECNOLOGIAS"], null, tropas);
            foreach (var era in new[] { 1, 3, 5 })
            {
                var e = (JObject)esperado[era.ToString()];
                var lay = a.Disponer(era);
                Assert.AreEqual((int)e["NCOL"], lay.NCOL, $"columnas (Era {era})");
                var filas = (JObject)e["filas"];
                CollectionAssert.AreEquivalent(filas.Properties().Select(p => p.Name).ToArray(), lay.Filas.Where(p => p.Value > 0).Select(p => p.Key).ToArray(), $"carriles (Era {era})");
                foreach (var p in filas.Properties()) Assert.AreEqual((int)p.Value, lay.Filas[p.Name], $"filas de {p.Name} (Era {era})");
                var nodos = (JObject)e["nodos"];
                Assert.AreEqual(nodos.Count, a.Nodos.Values.Count(n => n.Col != null), $"tecnologías en el tablero (Era {era})");
                foreach (var p in nodos.Properties())
                {
                    var n = a.Nodos[p.Name];
                    Assert.AreEqual((string)p.Value["preds"], string.Join(",", n.Preds), $"previas de {p.Name}");
                    Assert.AreEqual((string)p.Value["carril"], n.Carril, $"carril de {p.Name}");
                    Assert.AreEqual((int)p.Value["era"], n.Era, $"Era de {p.Name}");
                    Assert.AreEqual((int)p.Value["col"], n.Col, $"columna de {p.Name} (Era {era})");
                    Assert.AreEqual((int)p.Value["fila"], n.Fila, $"fila de {p.Name} (Era {era})");
                }
            }
            Assert.AreEqual((int)esperado["ultima"]["ninguna"], a.UltimaEraVisible(new string[0]), "Era visible sin nada adoptado");
            var arranque = a.Orden.Where(id => a.Nodos[id].DeArranque).ToList();
            Assert.AreEqual((int)esperado["ultima"]["arranque"], a.UltimaEraVisible(arranque), "Era visible con las de arranque");
            // Con el 30 % de la Era I adoptado aparece la II.
            var primera = a.TecsDeEra(1);
            Assert.AreEqual(2, a.UltimaEraVisible(primera.Take(a.CupoEra(1)).ToList()), "la Era II aparece con el cupo de la I");
        }
    }
}
