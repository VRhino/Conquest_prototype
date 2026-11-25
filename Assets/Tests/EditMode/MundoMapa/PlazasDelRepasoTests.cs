// El repaso del 23-11 de las maquetas de plaza: cada plaza trae el ancla de su estandarte (el de las columnas) en su json, las murallas de los
// niveles 4 y 5 una por torre de puerta, y la empalizada del nivel 2 es de troncos con volumen sin pasar de su tope de cerca.
using NUnit.Framework;
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public sealed class PlazasDelRepasoTests
    {
        static int Anclas(string nombre)
        {
            var t = Resources.Load<TextAsset>("MapaMundo/Asentamientos/" + nombre);
            Assert.That(t, Is.Not.Null, nombre + ".json");
            // (sin Newtonsoft: los cuatro números de cada ancla, [x, y, z, alto], de «estandartes»)
            int i = t.text.IndexOf("\"estandartes\"", System.StringComparison.Ordinal);
            if (i < 0) return 0;
            var ms = System.Text.RegularExpressions.Regex.Matches(t.text.Substring(i), @"\[\s*(-?[\d.]+),\s*(-?[\d.]+),\s*(-?[\d.]+),\s*(-?[\d.]+)\s*\]");
            foreach (System.Text.RegularExpressions.Match m in ms)
            {
                double D(int k) => double.Parse(m.Groups[k].Value, System.Globalization.CultureInfo.InvariantCulture);
                Assert.That(D(4), Is.GreaterThan(0.05).And.LessThan(1.0), $"{nombre}: alto del mástil en huellas");
                Assert.That(System.Math.Sqrt(D(1) * D(1) + D(2) * D(2)), Is.LessThan(1.15), $"{nombre}: dentro de la plaza");
            }
            return ms.Count;
        }

        [Test, Category("Mapa")]
        public void CadaPlazaTraeSuEstandarte()
        {
            foreach (var n in new[] { 1, 2, 3, 4, 5 })
                foreach (var v in new[] { 1, 2 })
                    Assert.That(Anclas($"plaza{n}_{v}"), Is.EqualTo(n == 3 ? 3 : 1), $"plaza{n}_{v}: el palacio o la casa grande{(n == 3 ? " y las dos torres de la puerta" : "")}");
            foreach (var n in new[] { 4, 5 })
                foreach (var v in new[] { 1, 2 })
                    Assert.That(Anclas($"muralla{n}_{v}"), Is.EqualTo(2), $"muralla{n}_{v}: una por torre de puerta");
            foreach (var v in new[] { 1, 2 })
                Assert.That(Anclas($"muralla2_{v}"), Is.EqualTo(0), "la empalizada no lleva estandarte");
        }

        [Test, Category("Mapa")]
        public void LaEmpalizadaDelNivel2EsDeTroncos()
        {
            foreach (var v in new[] { 1, 2 })
            {
                var raiz = Resources.Load<GameObject>($"MapaMundo/Asentamientos/muralla2_{v}");
                Assert.That(raiz, Is.Not.Null);
                int Tris(string nivel) => raiz.transform.Find($"muralla2_{v}_{nivel}").GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
                Assert.That(Tris("cerca"), Is.InRange(2500, 3000), $"muralla2_{v}: troncos con volumen, sin pasar de 3.000");
                Assert.That(Tris("medio"), Is.LessThanOrEqualTo(600));
                Assert.That(Tris("lejos"), Is.LessThanOrEqualTo(100));
            }
        }
    }
}
