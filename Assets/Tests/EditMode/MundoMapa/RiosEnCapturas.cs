// Los ríos del mapa en capturas (26-11, la página «Ríos nuevos del mapa»): en la previa, con el mundo del banco, las mismas cámaras (el más ancho,
// una confluencia, una trenza, meandros, un brazo muerto, un río fino de cerca y de lejos, una boca en el mar, la entrada en un lago, un vado en
// río ancho y en río fino, de lejos y un nacimiento) a 1920 × 1080 en `MUNDO_CAPTURAS`, como `rios_<RIOS_ETAPA>_NN.png`; y el coste (mediana
// del fotograma y 23 teselas construidas con el campo de ríos y sin él). Solo dibujo: explícita y de la pasada visual.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public sealed class RiosEnCapturas
    {
        [UnityTest, Explicit("capturas para la página"), Category("Visual"), Category("Mapa"), Timeout(3600000)]
        public IEnumerator LosRios()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/BronzeAge/PreviaDelMapa.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return Hacer();
            yield return new ExitPlayMode();
        }

        static (double x, double y) P(Punto p) => (p.X / 1000.0, p.Y / 1000.0);

        static IEnumerator Hacer()
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            var etapa = Environment.GetEnvironmentVariable("RIOS_ETAPA") ?? "x";
            Directory.CreateDirectory(carpeta);
            var tipo = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ConquestTactics.Mundo.UI.PreviaDelMapa")).First(t => t != null);
            Component previa = null;
            var hasta = Time.realtimeSinceStartup + 240;
            while (Time.realtimeSinceStartup < hasta)
            {
                previa = previa != null ? previa : UnityEngine.Object.FindAnyObjectByType(tipo) as Component;
                if (previa != null && (bool)tipo.GetProperty("Lista").GetValue(previa)) break;
                yield return null;
            }
            var mapa = (MapaDeMundo)tipo.GetProperty("Mapa").GetValue(previa);
            var banco = (BancoDelMapa)tipo.GetProperty("Banco").GetValue(previa);
            var a = banco.Artefacto;
            var relieve = banco.Relieve;
            // los datos
            var rios = a.Rios.ToList();
            var anchos = rios.SelectMany(r => r.Anchos).Select(w => w / 1000.0).OrderBy(w => w).ToList();
            bool Cerca((double x, double y) p, Rio otro, double d) => otro.Puntos.Any(q => { var (qx, qy) = P(q); return (qx - p.x) * (qx - p.x) + (qy - p.y) * (qy - p.y) < d * d; });
            var finesEnRio = rios.Where(r => r.Puntos.Count > 1 && rios.Any(o => o != r && Cerca(P(r.Puntos.Last()), o, 0.6))).ToList();
            var trenzas = rios.Where(r => r.Puntos.Count > 1 && rios.Any(o => o != r && Cerca(P(r.Puntos.First()), o, 0.6)) && rios.Any(o => o != r && Cerca(P(r.Puntos.Last()), o, 0.6))).ToList();
            Debug.Log($"[Ríos] mundo {banco.Hash} ({banco.OrigenDelMundo}): {rios.Count} ríos, con anchos {rios.Count(r => r.Anchos.Count == r.Puntos.Count && r.Anchos.Count > 0)}, " +
                      $"anchos {(anchos.Count > 0 ? $"{anchos.First():0.00} a {anchos.Last():0.00}, mediana {anchos[anchos.Count / 2]:0.00}" : "ninguno")}; " +
                      $"acaban en otro río {finesEnRio.Count}; trenzas (empiezan y acaban en otro) {trenzas.Count}; a lago {rios.Count(r => r.TerminaEnLago)}; vados {rios.Sum(r => r.Vados.Count)}; " +
                      $"orden {string.Join(",", rios.GroupBy(r => r.Orden).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}"))}");
            if (rios.Count == 0 || anchos.Count == 0) yield break;
            // las vistas: (nombre, x, y, zoom, giro)
            var vistas = new List<(string nombre, double x, double y, ZoomDelMapa zoom)>();
            (double x, double y) PuntoDeAncho(Func<double, double, bool> mejor)
            {
                (double x, double y) r = default; double w0 = double.NaN;
                foreach (var rio in rios)
                    for (int i = 0; i < Math.Min(rio.Puntos.Count, rio.Anchos.Count); i++)
                    {
                        var p = P(rio.Puntos[i]);
                        if (relieve.AguaEn(p.x, p.y)) continue;
                        double w = rio.Anchos[i] / 1000.0;
                        if (double.IsNaN(w0) || mejor(w, w0)) { w0 = w; r = p; }
                    }
                return r;
            }
            var ancho = PuntoDeAncho((w, w0) => w > w0);
            vistas.Add(("El más ancho", ancho.x, ancho.y, ZoomDelMapa.Normal));
            var conf = finesEnRio.Except(trenzas).OrderByDescending(r => r.Anchos.Count > 0 ? r.Anchos.Max() : 0).FirstOrDefault();
            if (conf != null) { var p = P(conf.Puntos.Last()); vistas.Add(("Confluencia", p.x, p.y, ZoomDelMapa.Cerca)); }
            var tr = trenzas.OrderByDescending(r => r.Anchos.Count > 0 ? r.Anchos.Max() : 0).FirstOrDefault();
            if (tr != null) { var p = P(tr.Puntos[tr.Puntos.Count / 2]); vistas.Add(("Trenza", p.x, p.y, ZoomDelMapa.Normal)); }
            // meandros: el tramo de 12 u más sinuoso; y un brazo muerto (un río corto que no acaba en agua ni en otro río)
            (double x, double y) meandro = default; double sinu = 0;
            foreach (var rio in rios)
            {
                var ps = rio.Puntos.Select(P).ToList();
                for (int i = 0; i < ps.Count; i++)
                {
                    double largo = 0; int j = i;
                    while (j + 1 < ps.Count && largo < 12) { largo += Math.Sqrt(Math.Pow(ps[j + 1].x - ps[j].x, 2) + Math.Pow(ps[j + 1].y - ps[j].y, 2)); j++; }
                    double cuerda = Math.Sqrt(Math.Pow(ps[j].x - ps[i].x, 2) + Math.Pow(ps[j].y - ps[i].y, 2));
                    if (largo >= 12 && cuerda > 0.1 && largo / cuerda > sinu) { sinu = largo / cuerda; meandro = ps[(i + j) / 2]; }
                }
            }
            vistas.Add(("Meandros", meandro.x, meandro.y, ZoomDelMapa.Normal));
            var muerto = rios.Where(r => r.Puntos.Count > 1 && !finesEnRio.Contains(r) && !relieve.AguaEn(P(r.Puntos.Last()).x, P(r.Puntos.Last()).y)
                                         && rios.Any(o => o != r && Cerca(P(r.Puntos.First()), o, 0.6))).OrderBy(r => r.Puntos.Count).FirstOrDefault();
            if (muerto != null) { var p = P(muerto.Puntos[muerto.Puntos.Count / 2]); vistas.Add(("Brazo muerto", p.x, p.y, ZoomDelMapa.Normal)); }
            var fino = PuntoDeAncho((w, w0) => w < w0);
            vistas.Add(("Río fino de cerca", fino.x, fino.y, ZoomDelMapa.Cerca));
            vistas.Add(("Río fino de lejos", fino.x, fino.y, ZoomDelMapa.Alejado));
            // (cuánta agua hay en 25 u: el mar, mucha; un lago, poca)
            double Agua(double x, double y) { int c = 0; for (int i = -10; i <= 10; i++) for (int j = -10; j <= 10; j++) if (relieve.AguaEn(x + i * 2.5, y + j * 2.5)) c++; return c / 441.0; }
            bool JuntoAlAgua((double x, double y) p) => relieve.AguaEn(p.x, p.y) || Enumerable.Range(1, 16).Any(k => relieve.AguaEn(p.x + k * 0.5, p.y) || relieve.AguaEn(p.x - k * 0.5, p.y) || relieve.AguaEn(p.x, p.y + k * 0.5) || relieve.AguaEn(p.x, p.y - k * 0.5));
            // la boca: otro río que entre en el mar (lejos del más ancho, que ya se ve en la primera)
            var boca = rios.Where(r => !r.TerminaEnLago && r.Puntos.Count > 1 && !finesEnRio.Contains(r)).OrderByDescending(r => r.Anchos.Count > 0 ? r.Anchos.Last() : 0)
                .Select(r => P(r.Puntos.Last())).Where(p => JuntoAlAgua(p) && Math.Abs(p.x - ancho.x) + Math.Abs(p.y - ancho.y) > 60 && Agua(p.x, p.y) > 0.3).FirstOrDefault();
            if (boca != default) vistas.Add(("Boca en el mar", boca.x, boca.y, ZoomDelMapa.Normal));
            // el lago: de los que acaban en lago, el de menos agua alrededor (uno de verdad, no la costa)
            var lago = rios.Where(r => r.TerminaEnLago && r.Puntos.Count > 1).Select(r => P(r.Puntos.Last())).Where(p => Agua(p.x, p.y) > 0).OrderBy(p => Agua(p.x, p.y)).FirstOrDefault();
            foreach (var r in rios.Where(r => r.TerminaEnLago)) Debug.Log($"[Ríos] lago {r.Id}: fin {P(r.Puntos.Last())} agua {Agua(P(r.Puntos.Last()).x, P(r.Puntos.Last()).y):0.00}");
            if (lago != default) vistas.Add(("Entra en un lago", lago.x, lago.y, ZoomDelMapa.Normal));
            (double x, double y, double w)? Vado(Func<double, bool> cual)
            {
                foreach (var rio in rios.OrderByDescending(r => r.Anchos.Count > 0 ? r.Anchos.Max() : 0))
                    foreach (var v in rio.Vados)
                    {
                        var p = P(v);
                        int i = Enumerable.Range(0, Math.Min(rio.Puntos.Count, rio.Anchos.Count)).OrderBy(k => Math.Pow(P(rio.Puntos[k]).x - p.x, 2) + Math.Pow(P(rio.Puntos[k]).y - p.y, 2)).DefaultIfEmpty(-1).First();
                        if (i >= 0 && cual(rio.Anchos[i] / 1000.0)) return (p.x, p.y, rio.Anchos[i] / 1000.0);
                    }
                return null;
            }
            if (Vado(w => w >= 2) is var (vx, vy, vw)) vistas.Add(($"Vado en río ancho", vx, vy, ZoomDelMapa.Cerca));
            if (Vado(w => w < 1) is var (fx, fy, fw)) vistas.Add(($"Vado en río fino", fx, fy, ZoomDelMapa.Cerca));
            // de lejos: donde más ríos hay en 30 u
            var todos = rios.SelectMany(r => r.Puntos.Select(P)).ToList();
            var lejos = todos.Where((p, k) => k % 25 == 0).OrderByDescending(p => rios.Count(r => r.Puntos.Any(q => Math.Abs(P(q).x - p.x) < 30 && Math.Abs(P(q).y - p.y) < 30))).First();
            vistas.Add(("De lejos", lejos.x, lejos.y, ZoomDelMapa.Alejado));
            // un nacimiento de cerca: el de un río que no sale de otro ni del agua, lejos del río fino (que ya se ve)
            var nace = rios.Where(r => r.Puntos.Count > 20 && !rios.Any(o => o != r && Cerca(P(r.Puntos.First()), o, 1.5)) && !JuntoAlAgua(P(r.Puntos.First())))
                .Select(r => P(r.Puntos.First())).Where(p => Math.Abs(p.x - fino.x) + Math.Abs(p.y - fino.y) > 60).FirstOrDefault();
            if (nace != default) vistas.Add(("Nacimiento de cerca", nace.x, nace.y, ZoomDelMapa.Cerca));
            // (sin aclarar las copas alrededor del centro: troncos sin copa y copas punteadas que no son de los ríos)
            MapaDeMundo.AclararCopas = false;
            foreach (var v in vistas) Debug.Log($"[Ríos] vista {v.nombre}: ({v.x:0.0}, {v.y:0.0}) {v.zoom}");

            var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            mapa.Vista.targetTexture = rt;
            foreach (var doc in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None)) doc.panelSettings.targetTexture = rt;
            tipo.GetMethod("VerPanel").Invoke(previa, new object[] { false });
            int n = 0;
            var soloVistas = Environment.GetEnvironmentVariable("RIOS_VISTAS");
            foreach (var (nombre, x, y, zoom) in vistas)
            {
                if (!string.IsNullOrEmpty(soloVistas) && !soloVistas.Split(',').Contains((n + 1).ToString("00"))) { n++; continue; }
                tipo.GetMethod("Mirar").Invoke(previa, new object[] { x, y });
                mapa.Camara.Zoom = zoom;
                mapa.Camara.Giro = 0.6;
                mapa.Camara.Inclinacion = zoom == ZoomDelMapa.Alejado ? 55 : 38;
                var reloj = System.Diagnostics.Stopwatch.StartNew();
                yield return null;
                yield return null;
                var fin = Time.realtimeSinceStartup + 90;
                while (!mapa.SueloCompleto && Time.realtimeSinceStartup < fin) yield return null;
                yield return new WaitForSecondsRealtime(3f);
                Guardar(rt, Path.Combine(carpeta, $"rios_{etapa}_{++n:00}.png"));
                File.AppendAllText(Path.Combine(carpeta, $"rios_{etapa}_vistas.txt"), $"{n:00}\t{nombre}\t{x:0.0}\t{y:0.0}\t{zoom}\t{reloj.Elapsed.TotalSeconds:0.0}s\n");
                if (n == 1)
                {
                    // el coste: la mediana del fotograma aquí y, en 23 teselas de paso 0,5 alrededor, construir con el campo de ríos y sin él (antes)
                    var fot = new List<float>();
                    for (int i = 0; i < 300; i++) { yield return null; fot.Add(Time.unscaledDeltaTime * 1000); }
                    fot.Sort();
                    var terreno = mapa.Terreno;
                    var construir = typeof(TerrenoDelMapa).GetMethod("Construir", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    float lado = (float)relieve.LadoEnUnidades;
                    int cx = (int)(x / lado), cy = (int)(y / lado);
                    var teselas = new List<(int, int)>();
                    for (int r = 0; teselas.Count < 23; r++)
                        for (int j = -r; j <= r && teselas.Count < 23; j++)
                            for (int i = -r; i <= r && teselas.Count < 23; i++)
                                if (Math.Max(Math.Abs(i), Math.Abs(j)) == r) teselas.Add((cx + i, cy + j));
                    double Medir()
                    {
                        var rl = System.Diagnostics.Stopwatch.StartNew();
                        foreach (var (tx, ty) in teselas) UnityEngine.Object.DestroyImmediate((GameObject)construir.Invoke(terreno, new object[] { tx, ty, 0.5f, lado }));
                        return rl.Elapsed.TotalMilliseconds;
                    }
                    Medir(); // (en caliente)
                    double con = Medir();
                    var campo = terreno.Campo;
                    terreno.Campo = null;
                    double sin = Medir();
                    // el fotograma de antes: las teselas rehechas sin el campo (sin la cinta de agua, que ya no se construye)
                    var dict = (System.Collections.IDictionary)typeof(TerrenoDelMapa).GetField("teselas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(terreno);
                    void Rehacer()
                    {
                        foreach (var v in dict.Values) UnityEngine.Object.DestroyImmediate((GameObject)v.GetType().GetField("Item1").GetValue(v));
                        dict.Clear();
                    }
                    Rehacer();
                    yield return null;
                    var limite = Time.realtimeSinceStartup + 90;
                    while (!mapa.SueloCompleto && Time.realtimeSinceStartup < limite) yield return null;
                    yield return new WaitForSecondsRealtime(2f);
                    var fotSin = new List<float>();
                    for (int i = 0; i < 300; i++) { yield return null; fotSin.Add(Time.unscaledDeltaTime * 1000); }
                    fotSin.Sort();
                    terreno.Campo = campo;
                    Rehacer();
                    yield return null;
                    limite = Time.realtimeSinceStartup + 90;
                    while (!mapa.SueloCompleto && Time.realtimeSinceStartup < limite) yield return null;
                    var linea = $"coste: fotograma mediana {fot[150]:0.00} ms (p95 {fot[285]:0.00}), sin el campo {fotSin[150]:0.00} ms (p95 {fotSin[285]:0.00}); 23 teselas de paso 0,5: con el campo {con:0} ms, sin él (antes) {sin:0} ms";
                    Debug.Log("[Ríos] " + linea);
                    File.AppendAllText(Path.Combine(carpeta, $"rios_{etapa}_coste.txt"), linea + "\n");
                }
            }
        }

        static void Guardar(RenderTexture rt, string ruta)
        {
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = antes;
            File.WriteAllBytes(ruta, t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
        }
    }
}
