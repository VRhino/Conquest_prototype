// Las mallas de las plazas (las de Resources, la de cerca de cada nivel y variante, villa y muralla): que no se vea a través. Rayos desde
// arriba en una rejilla y desde fuera a varias alturas: la primera cara que toca cada rayo ha de mirar hacia él. Si mira hacia el otro lado, se
// ve su cara de atrás (que Unity no pinta) y lo de detrás: un hueco (como la tapa de la terraza del palacio, que se encogía con el palacio).
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public sealed class MallasDePlazaTests
    {
        static IEnumerable<(string nombre, Mesh malla)> Mallas()
        {
            var nombres = new List<string>();
            for (int nivel = 1; nivel <= 5; nivel++)
                for (int v = 1; v <= 2; v++)
                    nombres.AddRange(new[] { $"plaza{nivel}_{v}", $"muralla{nivel}_{v}" });
            // (y las avanzadas, con sus estados)
            nombres.AddRange(new[] { "atalaya", "fuerte", "fuerte_obras", "fuerte_ruina", "ciudadela_1", "ciudadela_1_obras", "ciudadela_1_ruina", "ciudadela_2",
                "ciudadela_2_obras", "ciudadela_2_ruina" });
            // (y lo menudo: campamentos de mercenarios, de bandidos en sus niveles y alijos)
            nombres.AddRange(new[] { "mercenarios", "bandidos_1_1", "bandidos_1_2", "bandidos_1_3", "bandidos_2_1", "bandidos_2_2", "bandidos_2_3", "alijo_1", "alijo_2" });
            foreach (var nombre in nombres)
                    {
                        var raiz = Resources.Load<GameObject>("MapaMundo/Asentamientos/" + nombre);
                        var mf = raiz != null ? raiz.transform.Find(nombre + "_cerca")?.GetComponent<MeshFilter>() : null;
                        if (mf != null) yield return (nombre, mf.sharedMesh);
                        // (y la muralla que va dentro del FBX de su plaza, la del nivel 3: se dibuja aparte, con el material de la villa)
                        var mm = raiz != null ? raiz.transform.Find(nombre + "_muralla_cerca")?.GetComponent<MeshFilter>() : null;
                        if (mm != null) yield return (nombre + "_muralla", mm.sharedMesh);
                    }
        }

        /// <summary>La primera cara que toca el rayo (o, d): su normal (la media de las de sus vértices), o null si no toca nada; bajo el suelo
        /// (y &lt; 0: el zócalo enterrado, que tapa el terreno), como si no tocara.</summary>
        static Vector3? Primera(Vector3[] v, Vector3[] n, int[] t, Vector3 o, Vector3 d)
        {
            var (nor, dist) = PrimeraConDistancia(v, n, t, o, d);
            return nor != null && (o + d * dist).y >= 0 ? nor : null;
        }

        static (Vector3? nor, float dist) PrimeraConDistancia(Vector3[] v, Vector3[] n, int[] t, Vector3 o, Vector3 d)
        {
            float mejor = float.MaxValue;
            Vector3? nor = null;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(d, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-9f) continue;
                float inv = 1 / det;
                Vector3 s = o - a;
                float u = Vector3.Dot(s, p) * inv;
                if (u < 0 || u > 1) continue;
                Vector3 q = Vector3.Cross(s, e1);
                float w = Vector3.Dot(d, q) * inv;
                if (w < 0 || u + w > 1) continue;
                float dist = Vector3.Dot(e2, q) * inv;
                if (dist <= 1e-5f) continue;
                var ni = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
                // (dos caras en el mismo sitio, una por cada lado: la tela o el disco de doble cara; vale la que mira al rayo)
                if (Mathf.Abs(dist - mejor) < 1e-4f) { if (Vector3.Dot(ni, d) < 0) nor = ni; continue; }
                if (dist >= mejor) continue;
                mejor = dist;
                nor = ni;
            }
            return (nor, mejor);
        }

        [Test, Category("Mapa")]
        public void NoSeVeATravesDeLasPlazas()
        {
            var fallos = new List<string>();
            int mallas = 0;
            foreach (var (nombre, malla) in Mallas())
            {
                mallas++;
                var v = malla.vertices;
                var n = malla.normals;
                var t = malla.triangles;
                var caja = malla.bounds;
                int rayos = 0, atras = 0;
                // desde arriba, en una rejilla de 50 × 50
                for (int i = 0; i < 50; i++)
                    for (int j = 0; j < 50; j++)
                    {
                        var o = new Vector3(Mathf.Lerp(caja.min.x, caja.max.x, (i + 0.5f) / 50), caja.max.y + 1, Mathf.Lerp(caja.min.z, caja.max.z, (j + 0.5f) / 50));
                        var nor = Primera(v, n, t, o, Vector3.down);
                        if (nor == null) continue;
                        rayos++;
                        if (Vector3.Dot(nor.Value, Vector3.down) > 0) atras++;
                    }
                // desde fuera, de lado: 72 rumbos a cinco alturas, hacia el centro
                float r = Mathf.Max(caja.extents.x, caja.extents.z) * 1.5f;
                for (int k = 0; k < 72; k++)
                    for (int h = 1; h <= 5; h++)
                    {
                        float a = k * Mathf.PI * 2 / 72, y = Mathf.Lerp(caja.min.y, caja.max.y, h / 6f);
                        var o = new Vector3(caja.center.x + Mathf.Cos(a) * r, y, caja.center.z + Mathf.Sin(a) * r);
                        var d = (new Vector3(caja.center.x, y, caja.center.z) - o).normalized;
                        var nor = Primera(v, n, t, o, d);
                        if (nor == null) continue;
                        rayos++;
                        if (Vector3.Dot(nor.Value, d) > 0) atras++;
                    }
                Debug.Log($"[Mallas de plaza] {nombre}: {atras} de {rayos} rayos dan en una cara de atrás");
                // (un poco de margen: el paño de una bandera recortado, una arista justa)
                if (atras > Mathf.Max(3, rayos / 200)) fallos.Add($"{nombre}: {atras} de {rayos}");
            }
            Assert.That(mallas, Is.GreaterThan(0), "hay mallas de plaza en Resources");
            Assert.That(fallos, Is.Empty, "se ve a través de: " + string.Join("; ", fallos));
        }
    }
}
