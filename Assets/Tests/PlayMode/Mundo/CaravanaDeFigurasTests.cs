// Tema 7, ronda A: los animales y los carros de las caravanas, traídos al juego y horneados como las tropas (sin el mapa todavía). Se cuecen con
// `HornoDeFiguras` y se fotografían las mallas cocidas («car_…» en `MUNDO_CAPTURAS`): la fila a escala (soldado, héroe a caballo, caballo de
// tiro y los tres animales), cada carro enganchado de perfil y en tres cuartos por delante y por detrás, la prueba neutra del paso (reposo y tres
// fotogramas del andar) y, al lado, el animal con el clip del caballo puesto tal cual, para ver si lo encoge. Las medidas, en «car_medidas.txt».
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConquestTactics.Mundo.Mapa;
using ConquestTactics.Visual;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConquestTactics.Mundo.UI.Tests
{
    public class CaravanaDeFigurasTests
    {
        static readonly string[] AnimalesDeCaravana = { "buey", "camello", "burro" };
        static readonly string[] Enganches = { "basico|buey", "basico|caballo", "basico|camello", "reforzado|buey", "reforzado|caballo", "reforzado|camello", "fundacion|buey", "carrito|burro" };

        [Category("Mapa"), Explicit("tema 7, ronda A: animales y carros de las caravanas horneados")]
        [UnityTest, Timeout(3_000_000)]
        [Category("Visual")]  // (arte del mapa: en la pasada de antes del push, no en la de cada commit)
        public IEnumerator LosAnimalesYLosCarros()
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
#if UNITY_EDITOR
            var prefabs = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>("Assets/ScriptableObjects/VisualPrefabConfig.asset");
#else
            VisualPrefabConfiguration prefabs = null;
#endif
            Assert.That(prefabs, Is.Not.Null);
            var raiz = new GameObject("Caravana");
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            ModelosDeFiguras.Reiniciar();
            HornoDeFiguras.Bitacora.Clear();
            HornoDeFiguras.MedirPatas = true;
            try
            {
                var horno = raiz.AddComponent<HornoDeFiguras>();
                var piezas = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
                string Primera(List<Data.Avatar.AvatarPartDefinition> l) => l?.FirstOrDefault(p => !string.IsNullOrEmpty(p.id))?.id;
                horno.Iniciar(prefabs, id => new SelectorDePersonaje.HeroeParaVer { Nombre = id, Armadura = "media", Femenino = false,
                    Piezas = new Dictionary<string, string> { ["torso"] = "torso_media_inicio", ["guantes"] = "guantes_media_inicio",
                        ["pantalones"] = "pantalones_media_inicio", ["botas"] = "botas_media_inicio", ["montura"] = HornoDeFiguras.MonturaDeTiro },
                    Cabeza = Primera(piezas?.faceParts), Pelo = Primera(piezas?.hairParts), Barba = Primera(piezas?.beardParts), Cejas = Primera(piezas?.eyebrowsParts) });
                var claves = AnimalesDeCaravana.Select(a => "animal|" + a).Concat(new[] { "animal|caballo", ModelosDeFiguras.ClaveDeTropa("milicia_lanceros", 0),
                    ModelosDeFiguras.ClaveDeHeroe("h-tiro") }).Concat(Enganches.Select(e => "carro|" + e)).ToArray();
                foreach (var k in claves) ModelosDeFiguras.Pedir(k);
                var fin = Time.realtimeSinceStartup + 600;
                while (claves.Any(k => !ModelosDeFiguras.Listos.ContainsKey(k)) && Time.realtimeSinceStartup < fin) yield return null;
                yield return new WaitForSecondsRealtime(1);
                foreach (var b in HornoDeFiguras.Bitacora) Debug.Log("[Caravana] " + b);
                Assert.That(claves.All(ModelosDeFiguras.Listos.ContainsKey), "sin hornear: " + string.Join(", ", claves.Where(k => !ModelosDeFiguras.Listos.ContainsKey(k))));

                // la escena: luz, cámara ortográfica y las figuras cocidas como mallas sueltas (con sus materiales del mapa, sin bruma)
                var luz = new GameObject("Sol").AddComponent<Light>();
                luz.type = LightType.Directional;
                luz.transform.SetParent(raiz.transform);
                luz.transform.rotation = Quaternion.Euler(45, -40, 0);
                var cam = new GameObject("Camara").AddComponent<Camera>();
                cam.transform.SetParent(raiz.transform);
                cam.targetTexture = rt;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.8f, 0.8f, 0.78f);
                var puestas = new List<GameObject>();
                GameObject Poner(Mesh m, Material[] mats, Vector3 pie, float giro)
                {
                    var go = new GameObject(m.name);
                    go.transform.SetParent(raiz.transform);
                    go.transform.SetPositionAndRotation(pie, Quaternion.Euler(0, giro, 0));
                    go.AddComponent<MeshFilter>().sharedMesh = m;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = mats.Select(x => { var c = new Material(x); if (c.HasProperty("_ConNiebla")) c.SetFloat("_ConNiebla", 0); return c; }).ToArray();
                    puestas.Add(go);
                    return go;
                }
                void Quitar() { foreach (var g in puestas) UnityEngine.Object.Destroy(g); puestas.Clear(); }
                IEnumerator Foto(string nombre, Vector3 mira, Vector3 desde, float alto)
                {
                    cam.orthographic = true;
                    cam.orthographicSize = alto;
                    cam.transform.position = mira + desde;
                    cam.transform.LookAt(mira);
                    yield return null;
                    cam.Render();
                    if (string.IsNullOrEmpty(carpeta)) yield break;
                    var antes = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = antes;
                    File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
                    UnityEngine.Object.Destroy(tex);
                }
                TiraDeFiguras T(string k) => ModelosDeFiguras.Listos[k];
                float Largo(Mesh m) => m.bounds.size.z;

                // (a) la fila a escala, de perfil: mirando a +x, uno tras otro
                var fila = new[] { ModelosDeFiguras.ClaveDeTropa("milicia_lanceros", 0), ModelosDeFiguras.ClaveDeHeroe("h-tiro"), "animal|caballo" }
                    .Concat(AnimalesDeCaravana.Select(a => "animal|" + a)).ToArray();
                float x = 0;
                foreach (var k in fila)
                {
                    var m = T(k).Reposo[0];
                    x += Largo(m) / 2 + 0.4f;
                    Poner(m, T(k).Materiales, new Vector3(x, 0, 0), 90);
                    x += Largo(m) / 2 + 0.4f;
                }
                yield return Foto("car_fila", new Vector3(x / 2, 1.2f, 0), new Vector3(0, 0, -20), x / 2 * 900 / 1600 + 0.3f);
                Quitar();

                // (b) cada carro enganchado: de perfil, y en tres cuartos por delante y por detrás
                foreach (var e in Enganches)
                {
                    var t = T("carro|" + e);
                    var m = t.Andar[0];
                    Poner(m, t.Materiales, Vector3.zero, 90);
                    var centro = new Vector3(m.bounds.center.z, 1.0f, 0);
                    float alto = Mathf.Max(1.6f, m.bounds.size.z * 0.36f);
                    var nombre = e.Replace('|', '_');
                    yield return Foto($"car_{nombre}_perfil", centro, new Vector3(0, 0, -20), alto);
                    yield return Foto($"car_{nombre}_delante", centro, new Vector3(14, 9, -14), alto);
                    yield return Foto($"car_{nombre}_detras", centro, new Vector3(-14, 9, -14), alto);
                    Quitar();
                }

                // (c) la prueba neutra del paso: reposo y los fotogramas 0, 4 y 8 del andar, de perfil, con el caballo de referencia
                foreach (var a in new[] { "caballo" }.Concat(AnimalesDeCaravana))
                {
                    var t = T("animal|" + a);
                    var poses = new[] { t.Reposo[0], t.Andar[0], t.Andar[4], t.Andar[8] };
                    for (int i = 0; i < poses.Length; i++) Poner(poses[i], t.Materiales, new Vector3(i * 3.2f, 0, 0), 90);
                    yield return Foto($"car_paso_{a}", new Vector3(1.5f * 3.2f, 1.1f, 0), new Vector3(0, 0, -20), (4 * 3.2f + 0.6f) / 2 * 900 / 1600);
                    Quitar();
                }

                // ¿el clip del caballo, puesto tal cual, encoge al animal? Se muestrea su andar sobre el animal y se mide lo que cambian sus huesos
                var caballo = Resources.Load<HeroHorseVisual>(HeroHorseVisual.ResourcePath);
                var andar = caballo.Animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0);
                var medidas = new List<string>();
#if UNITY_EDITOR
                if (andar != null)
                {
                    var curvas = UnityEditor.AnimationUtility.GetCurveBindings(andar);
                    int pos = curvas.Count(c => c.propertyName.StartsWith("m_LocalPosition")) / 3, rot = curvas.Count(c => c.propertyName.StartsWith("m_LocalRotation")) / 4;
                    medidas.Add($"el clip «{andar.name}»: {rot} huesos con giro y {pos} con posición ({string.Join(", ", curvas.Where(c => c.propertyName == "m_LocalPosition.x").Select(c => c.path.Split('/').Last()).Take(40))})");
                }
#endif
                var huesosCaballo = caballo.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Horse").rootBone;
                if (andar != null)
                    foreach (var a in AnimalesDeCaravana)
                    {
                        var go = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(HornoDeFiguras.Caravana + HornoDeFiguras.Animales[a]), raiz.transform);
                        go.transform.position = new Vector3(0, 0, 0);
                        go.transform.rotation = Quaternion.Euler(0, 90, 0);
                        var cuerpo = go.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name == "Body");
                        // (los nombres de lo que hay entre la raíz y el primer hueso, como en el caballo, para que el clip encuentre sus rutas)
                        for (Transform ta = cuerpo.rootBone, tc = huesosCaballo; ta != null && tc != null && ta != go.transform && tc.parent != null; ta = ta.parent, tc = tc.parent)
                            ta.name = tc.name;
                        var antes = cuerpo.bones.ToDictionary(b => b, b => b.localPosition);
                        float Pata() { var c = cuerpo.bones.First(b => b.name == "Chest"); return go.transform.InverseTransformPoint(c.position).y; }
                        float pataAntes = Pata();
                        andar.SampleAnimation(go, andar.length * 0.35f);
                        var cambio = cuerpo.bones.Max(b => (b.localPosition - antes[b]).magnitude);
                        medidas.Add($"{a}: el clip «{andar.name}» tal cual mueve los huesos hasta {cambio * 100:0.0} cm; el pecho pasa de {pataAntes:0.00} a {Pata():0.00} m de alto");
                        yield return Foto($"car_clip_tal_cual_{a}", new Vector3(0, 1.1f, 0), new Vector3(0, 0, -20), 1.4f);
                        UnityEngine.Object.Destroy(go);
                    }

                // (d) lo de cada pieza: triángulos, materiales (llamadas: una por material) y lo medido al enganchar
                foreach (var k in claves)
                {
                    var t = T(k);
                    medidas.Add($"{k}: {t.Triangulos} triángulos, {t.Materiales.Length} materiales, andar {t.Andar.Length} fotogramas en {t.DuracionAndar:0.00} s, paso {t.PasoDelClip:0.00} m/s, alto {t.Alto:0.00} m"
                                + (HornoDeFiguras.Medidas.TryGetValue(k.Substring(k.IndexOf('|') + 1), out var md) ? "; " + md : ""));
                }
                // el largo real de cada pieza (m): lo más adelantado y lo más atrasado de todos sus fotogramas, desde su origen (el eje del carro, los pies)
                foreach (var k in claves)
                {
                    var t = T(k);
                    float frente = t.Andar.Concat(t.Reposo).Max(m => m.bounds.max.z), cola = -t.Andar.Concat(t.Reposo).Min(m => m.bounds.min.z);
                    float ancho = t.Andar.Concat(t.Reposo).Max(m => Mathf.Max(m.bounds.max.x, -m.bounds.min.x));
                    medidas.Add($"LARGO {k}: delante {frente:0.000} m, detrás {cola:0.000} m, medio ancho {ancho:0.000} m");
                }
                foreach (var l in medidas) Debug.Log("[Caravana] " + l);
                if (!string.IsNullOrEmpty(carpeta)) File.WriteAllLines(Path.Combine(carpeta, "car_medidas.txt"), medidas.Concat(HornoDeFiguras.Bitacora));
            }
            finally
            {
                UnityEngine.Object.Destroy(raiz);
                rt.Release();
                ModelosDeFiguras.Reiniciar();
                HornoDeFiguras.MedirPatas = false;
            }
        }

        [Category("Mapa"), Explicit("tema 7: el Aeda horneado, de cerca (para depurar el horno)")]
        [UnityTest, Timeout(600_000)]
        [Category("Visual")]  // (arte del mapa: en la pasada de antes del push, no en la de cada commit)
        public IEnumerator ElAedaHorneado()
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            var raiz = new GameObject("Aeda");
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            ModelosDeFiguras.Reiniciar();
            HornoDeFiguras.Bitacora.Clear();
            HornoDeFiguras.MedirPatas = true;
            try
            {
                var horno = raiz.AddComponent<HornoDeFiguras>();
                horno.Iniciar(null, _ => null);
                ModelosDeFiguras.Pedir("aeda");
                var fin = Time.realtimeSinceStartup + 120;
                while (!ModelosDeFiguras.Listos.ContainsKey("aeda") && Time.realtimeSinceStartup < fin) yield return null;
                foreach (var b in HornoDeFiguras.Bitacora) Debug.Log("[AedaHorno] " + b);
                Assert.That(ModelosDeFiguras.Listos.ContainsKey("aeda"));
                var t = ModelosDeFiguras.Listos["aeda"];
                var luz = new GameObject("Sol").AddComponent<Light>();
                luz.type = LightType.Directional; luz.transform.SetParent(raiz.transform); luz.transform.rotation = Quaternion.Euler(45, -40, 0);
                var cam = new GameObject("Camara").AddComponent<Camera>();
                cam.transform.SetParent(raiz.transform); cam.targetTexture = rt; cam.orthographic = true; cam.orthographicSize = 1.3f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.8f, 0.8f, 0.78f);
                var poses = new[] { t.Reposo[0], t.Andar[0], t.Andar[4], t.Andar[8] };
                for (int i = 0; i < poses.Length; i++)
                {
                    var go = new GameObject("p" + i);
                    go.transform.SetParent(raiz.transform);
                    go.transform.SetPositionAndRotation(new Vector3(i * 1.4f, 0, 0), Quaternion.Euler(0, 90, 0));
                    go.AddComponent<MeshFilter>().sharedMesh = poses[i];
                    go.AddComponent<MeshRenderer>().sharedMaterials = t.Materiales.Select(x => { var c = new Material(x); if (c.HasProperty("_ConNiebla")) c.SetFloat("_ConNiebla", 0); return c; }).ToArray();
                }
                cam.transform.position = new Vector3(2.1f, 1.0f, -20);
                cam.transform.LookAt(new Vector3(2.1f, 1.0f, 0));
                cam.orthographicSize = 1.6f;
                yield return null;
                cam.Render();
                if (!string.IsNullOrEmpty(carpeta))
                {
                    var antes = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = antes;
                    File.WriteAllBytes(Path.Combine(carpeta, "aeda_horneado.png"), tex.EncodeToPNG());
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(raiz);
                rt.Release();
                ModelosDeFiguras.Reiniciar();
                HornoDeFiguras.MedirPatas = false;
            }
        }
    }
}
