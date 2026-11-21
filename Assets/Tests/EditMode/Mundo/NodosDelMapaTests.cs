// Los nodos de recursos «Prototipo» (`RocasDeNodos`) contra el propio código del prototipo: `Datos/nodos_referencia.json`, que escribe
// `tasks/referencias/nodos.mts` ejecutando el bloque de los nodos de `paisaje.ts` con su three (posición, giro, escala y color de cada roca, y
// los vértices de la roca).
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ConquestTactics.Mundo.Tests
{
    public class NodosDelMapaTests
    {
        static JObject referencia, iconos;
        static JObject Ref => referencia ??= JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Tests/EditMode/Mundo/Datos/nodos_referencia.json")));
        static string ColorDe(string tipo) =>
            (string)(iconos ??= JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Resources/MundoUI/Iconos/iconos.json"))))["recurso"]?[tipo]?["color"];

        [Test]
        public void LasRocasSonLasDelPrototipo()
        {
            int rocas = 0;
            foreach (JObject c in Ref["casos"])
            {
                string tipo = (string)c["tipo"];
                double x = (double)c["x"], y = (double)c["y"];
                var mias = RocasDeNodos.DelPrototipo(tipo, x, y);
                var suyas = (JArray)c["rocas"];
                Assert.AreEqual(suyas.Count, mias.Count, $"{tipo}: cuántas rocas");
                var color = RocasDeNodos.ColorLineal(tipo, ColorDe(tipo));
                for (int k = 0; k < mias.Count; k++, rocas++)
                {
                    var r = mias[k];
                    var p = suyas[k]["p"].Select(v => (double)v).ToArray();
                    var q = suyas[k]["q"].Select(v => (double)v).ToArray();
                    var l = suyas[k]["lineal"].Select(v => (double)v).ToArray();
                    string donde = $"{tipo} en ({x}, {y}), roca {k}";
                    // las posiciones del prototipo pasan por la matriz en float32
                    Assert.AreEqual(p[0], r.X, 2e-3, donde + ": x");
                    Assert.AreEqual(p[2], r.Y, 2e-3, donde + ": y");
                    Assert.AreEqual(p[1], 0.1 * r.X - 0.05 * r.Y + 2 + r.R * 0.25, 2e-3, donde + ": alto");
                    Assert.AreEqual((double)suyas[k]["s"], r.R, 1e-5, donde + ": radio");
                    double dq = Math.Abs(q[0] * r.Qx + q[1] * r.Qy + q[2] * r.Qz + q[3] * r.Qw);
                    Assert.AreEqual(1, dq, 1e-6, donde + ": giro");
                    Assert.AreEqual(l[0], color.r, 1e-6, donde + ": rojo");
                    Assert.AreEqual(l[1], color.g, 1e-6, donde + ": verde");
                    Assert.AreEqual(l[2], color.b, 1e-6, donde + ": azul");
                }
            }
            Assert.AreEqual(92, rocas);
        }

        [Test]
        public void LaRocaEsElDodecaedroAplastado()
        {
            var mia = RocasDeNodos.Dodecaedro();
            var suya = (JArray)Ref["roca"];
            Assert.AreEqual(suya.Count, mia.Length);
            for (int i = 0; i < mia.Length; i++)
            {
                Assert.AreEqual((double)suya[i][0], mia[i].x, 1e-6, $"vértice {i}");
                Assert.AreEqual((double)suya[i][1], mia[i].y, 1e-6, $"vértice {i}");
                Assert.AreEqual((double)suya[i][2], mia[i].z, 1e-6, $"vértice {i}");
            }
        }

        [Test]
        public void ElAzarEsElDeJavaScript()
        {
            foreach (JArray c in Ref["azar"])
                Assert.AreEqual((double)c[2], RocasDeNodos.Azar((double)c[0], (double)c[1]), 1e-15, $"azar({c[0]}, {c[1]})");
        }
    }
}
