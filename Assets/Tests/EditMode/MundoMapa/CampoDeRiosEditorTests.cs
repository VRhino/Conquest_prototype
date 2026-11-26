// El campo de los ríos (`CampoDeRios`, 26-11) sobre el mundo en disco (el del generador si está, como la previa): el nivel nunca sube río
// abajo, el cauce queda bajo el agua, la lámina de la tesela cubre el eje de todo río en los tres pasos de malla, un afluente llega al nivel
// del río en que entra, sin los anchos del motor sigue habiendo río (la regla de antes) y cada vado cae sobre la curva de su río.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BronzeAge.Contract.V1;
using NUnit.Framework;
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public sealed class CampoDeRiosEditorTests
    {
        static BancoDelMapa banco;
        static CampoDeRios campo;
        static float mar;

        static void Mundo()
        {
            if (banco != null) return;
            if (!Directory.Exists(Path.Combine(Application.persistentDataPath, "mundo", "artefacto")))
                Assert.Ignore("no hay mundo en disco: entra antes una vez al mundo con el servidor");
            banco = BancoDelMapa.DelDisco("rios");
            EscalaDelMapa.Fijar(banco.Artefacto.Manifiesto.FactorUnity);
            mar = (float)(banco.Datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION.umbralAgua") * Relieve.MetrosAltura);
            campo = new CampoDeRios(banco.Artefacto, banco.Relieve, mar);
        }

        [Test, Category("Mapa")]
        public void ElNivelNuncaSubeRioAbajo()
        {
            Mundo();
            for (int ir = 0; ir < banco.Artefacto.Rios.Count; ir++)
            {
                var n = campo.NivelesDe(ir);
                for (int k = 1; k < n.Count; k++)
                    Assert.LessOrEqual(n[k], n[k - 1] + 1e-6, $"{banco.Artefacto.Rios[ir].Id}: sube en el segmento {k} ({n[k - 1]:0.000} → {n[k]:0.000} m)");
            }
        }

        [Test, Category("Mapa")]
        public void ElCauceQuedaBajoElAgua()
        {
            Mundo();
            var relieve = banco.Relieve;
            int vistos = 0;
            foreach (var rio in banco.Artefacto.Rios)
                foreach (var t in RiosDelMapa.Tramos(rio, relieve))
                    foreach (var p in t.Puntos)
                    {
                        if (relieve.AguaEn(p.x, p.y)) continue;
                        foreach (var paso in new[] { 0.5f, 1, 2 })
                        {
                            var en = campo.En(p.x, p.y, (float)relieve.AlturaEn(p.x, p.y), CampoDeRios.MedioMinimo * paso);
                            Assert.That(en.HasValue && !float.IsNaN(en.Value.Nivel), $"{rio.Id} ({p.x:0.00}, {p.y:0.00}), paso {paso}: el eje es río");
                            Assert.Less(en.Value.Suelo, en.Value.Nivel, $"{rio.Id} ({p.x:0.00}, {p.y:0.00}), paso {paso}: el cauce, bajo el agua");
                            vistos++;
                        }
                    }
            Assert.Greater(vistos, 1000);
        }

        /// <summary>En cada tesela con río (una de cada cuatro, para no tardar), en los tres pasos de malla: el vértice de la malla más cercano a
        /// cada punto del eje está en la lámina, y en él el agua queda por encima del suelo.</summary>
        [Test, Category("Mapa")]
        public void LaLaminaCubreElEjeDeTodoRio()
        {
            Mundo();
            var a = banco.Artefacto;
            var relieve = banco.Relieve;
            RasterDeMundo R(Raster r) => r != null && r.Datos.Length > 0 ? new RasterDeMundo(r) : null;
            var padre = new GameObject("prueba del campo de ríos");
            try
            {
                var umbrales = new Vector4(mar + 10, mar + 200, mar + 400, mar + 600);
                var terreno = new TerrenoDelMapa(relieve, R(a.Fertilidad), R(a.Humedad), R(a.DensidadBosque), mar, umbrales, padre.transform)
                    { Cauces = banco.Cauces, Campo = campo };
                var construir = typeof(TerrenoDelMapa).GetMethod("Construir", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                float lado = (float)relieve.LadoEnUnidades, e = EscalaDelMapa.MetrosPorUnidad;
                // los puntos de eje por tesela (fuera del mar y los lagos)
                var porTesela = new Dictionary<(int, int), List<Vector2>>();
                foreach (var rio in a.Rios)
                    foreach (var t in RiosDelMapa.Tramos(rio, relieve))
                        for (int ip = 0; ip < t.Puntos.Count; ip++)
                        {
                            var p = t.Puntos[ip];
                            // (sin el nacimiento, que se afina hasta casi nada: ahí la rejilla puede no tener vértice en el agua)
                            if (relieve.AguaEn(p.x, p.y) || ip * 0.5f < CampoDeRios.Nacimiento + 0.5f && Vector2.Distance(t.Puntos[0], new Vector2(rio.Puntos[0].X / 1000f, rio.Puntos[0].Y / 1000f)) < 0.5f) continue;
                            var k = ((int)(p.x / lado), (int)(p.y / lado));
                            if (!porTesela.TryGetValue(k, out var l)) porTesela[k] = l = new List<Vector2>();
                            l.Add(p);
                        }
                int vistos = 0, teselas = 0;
                var fallos = new List<string>();
                foreach (var kv in porTesela.OrderBy(kv => kv.Key).Where((kv, i) => i % 4 == 0))
                    foreach (var paso in new[] { 0.5f, 1, 2 })
                    {
                        var go = (GameObject)construir.Invoke(terreno, new object[] { kv.Key.Item1, kv.Key.Item2, paso, lado });
                        teselas++;
                        int n = Mathf.RoundToInt(lado / paso) + 1;
                        var suelo = go.GetComponent<MeshFilter>().sharedMesh.vertices;
                        var agua = go.transform.Find("agua")?.GetComponent<MeshFilter>()?.sharedMesh;
                        var lamina = new Dictionary<(int, int), float>();
                        if (agua != null)
                        {
                            var vs = agua.vertices;
                            foreach (int v in agua.triangles) lamina[(Mathf.RoundToInt(vs[v].x / (paso * e)), Mathf.RoundToInt(-vs[v].z / (paso * e)))] = vs[v].y;
                        }
                        foreach (var p in kv.Value)
                        {
                            int i = Mathf.RoundToInt((float)((p.x - kv.Key.Item1 * lado) / paso)), j = Mathf.RoundToInt((float)((p.y - kv.Key.Item2 * lado) / paso));
                            if (i < 0 || j < 0 || i >= n || j >= n) continue;
                            vistos++;
                            string Que()
                            {
                                var en = campo.En(p.x, p.y, (float)relieve.AlturaEn(p.x, p.y), CampoDeRios.MedioMinimo * paso);
                                bool aguaCerca = Enumerable.Range(-3, 7).Any(u => Enumerable.Range(-3, 7).Any(w => relieve.AguaEn(p.x + u * 0.5, p.y + w * 0.5)));
                                return $" [relieve {relieve.AlturaEn(p.x, p.y):0.00}, nivel {en?.Nivel:0.00}, cauce {en?.Suelo:0.00}, agua a 1,5 u: {aguaCerca}]";
                            }
                            if (!lamina.TryGetValue((i, j), out var y)) fallos.Add($"({p.x:0.00}, {p.y:0.00}) paso {paso}: sin lámina" + Que());
                            else if (y <= suelo[j * n + i].y) fallos.Add($"({p.x:0.00}, {p.y:0.00}) paso {paso}: el agua ({y:0.00} m) bajo el suelo ({suelo[j * n + i].y:0.00} m)" + Que());
                        }
                        UnityEngine.Object.DestroyImmediate(go);
                    }
                Debug.Log($"[Ríos] la lámina en el eje: {vistos} puntos en {teselas} teselas, {fallos.Count} fallos");
                Assert.Greater(vistos, 500);
                foreach (var f in fallos.Where((f, i) => i % 10 == 0)) Debug.Log("[Ríos] " + f);
                Assert.IsEmpty(fallos.Take(20), $"{fallos.Count} puntos del eje sin agua encima");
            }
            finally { UnityEngine.Object.DestroyImmediate(padre); }
        }

        [Test, Category("Mapa")]
        public void UnAfluenteLlegaAlNivelDelRioEnQueEntra()
        {
            Mundo();
            var a = banco.Artefacto;
            int juntas = 0;
            var todos = a.Rios.Select(r => RiosDelMapa.Tramos(r, banco.Relieve)).ToList();
            for (int ir = 0; ir < a.Rios.Count; ir++)
            {
                var tramos = todos[ir];
                if (tramos.Count == 0) continue;
                var fin = tramos[tramos.Count - 1].Puntos.Last();
                if (banco.Relieve.AguaEn(fin.x, fin.y)) continue;
                // ¿acaba en el cauce de otro? (el nivel del campo ahí es el más bajo de los que lo cubren)
                bool enOtro = Enumerable.Range(0, a.Rios.Count).Any(o => o != ir &&
                    todos[o].Any(t => t.Puntos.Select((q, k) => (q, k)).Any(x => Vector2.Distance(x.q, fin) < t.Anchos[x.k] / 2)));
                if (!enOtro) continue;
                var propio = campo.NivelesDe(ir).Last();
                var en = campo.En(fin.x, fin.y, (float)banco.Relieve.AlturaEn(fin.x, fin.y), 0.375);
                if (!en.HasValue || float.IsNaN(en.Value.Nivel)) continue;
                juntas++;
                Assert.LessOrEqual(propio - en.Value.Nivel, 0.05, $"{a.Rios[ir].Id}: llega a {propio:0.00} m y el agua en la junta está a {en.Value.Nivel:0.00} m");
            }
            Debug.Log($"[Ríos] juntas comprobadas: {juntas}");
            Assert.Greater(juntas, 10);
        }

        [Test, Category("Mapa")]
        public void SinLosAnchosDelMotorSigueHabiendoRio()
        {
            Mundo();
            var viejo = new ArtefactoDeMundo();
            foreach (var r in banco.Artefacto.Rios.Take(20))
            {
                var c = r.Clone();
                c.Anchos.Clear();
                viejo.Rios.Add(c);
            }
            var sin = new CampoDeRios(viejo, banco.Relieve, mar);
            int vistos = 0;
            foreach (var rio in viejo.Rios)
                foreach (var t in RiosDelMapa.Tramos(rio, banco.Relieve))
                {
                    if (t.Puntos.Count < 3) continue;
                    var p = t.Puntos[t.Puntos.Count / 2];
                    Assert.GreaterOrEqual(t.Anchos[t.Puntos.Count / 2], PinturaDelMapa.Rio * 0.75f - 1e-4, $"{rio.Id}: el ancho de la regla de antes");
                    var en = sin.En(p.x, p.y, (float)banco.Relieve.AlturaEn(p.x, p.y), 0.375);
                    Assert.That(en.HasValue && !float.IsNaN(en.Value.Nivel), $"{rio.Id} ({p.x:0.0}, {p.y:0.0}): hay agua");
                    vistos++;
                }
            Assert.Greater(vistos, 10);
        }

        /// <summary>Los ríos que acaban en el mar (no en un lago): cuántos llegan con un escalón de más de 5 m en sus últimos 2 u de tierra (el
        /// relieve fino en el eje, del punto a 2 u del agua al último en tierra). Solo el recuento, en el registro: es un dato del generador.</summary>
        [Test, Explicit("recuento para el informe"), Category("Mapa")]
        public void LasBocasEnElMarConEscalon()
        {
            Mundo();
            var relieve = banco.Relieve;
            int alMar = 0;
            var con = new List<string>();
            foreach (var rio in banco.Artefacto.Rios.Where(r => !r.TerminaEnLago))
            {
                var tramos = RiosDelMapa.Tramos(rio, relieve);
                if (tramos.Count == 0) continue;
                var p = tramos[tramos.Count - 1].Puntos;
                int primeroEnAgua = p.FindIndex(q => relieve.AguaEn(q.x, q.y));
                if (primeroEnAgua <= 0 || relieve.AlturaEn(p[primeroEnAgua].x, p[primeroEnAgua].y) > mar + 1) continue; // (no llega al mar)
                alMar++;
                var fin = p[primeroEnAgua - 1];
                double andado = 0; int i = primeroEnAgua - 1;
                while (i > 0 && andado < 2) { andado += Vector2.Distance(p[i], p[i - 1]); i--; }
                double arriba = relieve.AlturaEn(p[i].x, p[i].y), abajo = relieve.AlturaEn(p[primeroEnAgua].x, p[primeroEnAgua].y);
                if (arriba - abajo > 5) con.Add($"{rio.Id} en ({fin.x:0.0}, {fin.y:0.0}): {arriba - abajo:0.0} m en {andado:0.0} u");
            }
            Debug.Log($"[Ríos] bocas en el mar: {alMar}; con escalón de más de 5 m en sus últimos 2 u: {con.Count}" + string.Concat(con.Select(c => "; " + c)));
            Assert.Greater(alMar, 0);
        }

        /// <summary>El vado se pinta en la curva de su río (la muestra del cauce más cercana al punto del motor), a menos de 1,5 u de él; los que
        /// se apartan más de 0,3 u, en el registro.</summary>
        [Test, Category("Mapa")]
        public void CadaVadoCaeSobreSuRio()
        {
            Mundo();
            var lejos = banco.Cauces.Trechos.Where(t => t.Aparte > 0.3f).ToList();
            Debug.Log($"[Ríos] vados: {banco.Cauces.Trechos.Count} sobre su río de {banco.Cauces.Vados.Count}; aparte, mediana {banco.Cauces.Trechos.Select(t => t.Aparte).OrderBy(v => v).ElementAtOrDefault(banco.Cauces.Trechos.Count / 2):0.000} u, el peor {banco.Cauces.Trechos.Select(t => t.Aparte).DefaultIfEmpty().Max():0.000} u");
            Assert.Greater(banco.Cauces.Trechos.Count, 0);
            // (los de más de 0,3 u se cuentan en el registro: el punto del motor no cae en la curva de su río; para el informe)
            foreach (var t in lejos) Debug.Log($"[Ríos] vado ({t.X:0.00}, {t.Y:0.00}) a {t.Aparte:0.00} u de su río");
            Assert.That(banco.Cauces.Trechos.All(t => t.Aparte < 1.5f), "todo vado, junto a su río");
        }
    }
}
