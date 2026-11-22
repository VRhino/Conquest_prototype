#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using ConquestTactics.Visual;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// La prueba neutra de los caballos de guerra (tema 7): fuera de la batalla, el caballo de antes y uno nuevo (el tracio de la montura de tiro)
/// lado a lado, con el mismo Animator, el mismo clip y el mismo instante (parado, paso, trote y galope, tres instantes cada uno), de perfil, con
/// las articulaciones marcadas (nuca, cruz, codo, rodilla, babilla y corvejón): tienen que caer en el mismo sitio de la malla en los dos.
/// «neutra_{andar}_{instante}.png» en `MUNDO_CAPTURAS`.
/// </summary>
public class CaballosDeGuerraNeutraTests
{
    [UnityTest, Explicit("tema 7: los caballos de guerra, la prueba neutra")]
    public IEnumerator ElCaballoDeAntesYElNuevoConElMismoClip()
    {
        var prefab = Resources.Load<HeroHorseVisual>(HeroHorseVisual.ResourcePath);
        var guerra = CaballosDeGuerra.Cargar();
        Assert.That(prefab, Is.Not.Null);
        Assert.That(guerra, Is.Not.Null);
        var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
        var raiz = new GameObject("Neutra");
        var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
        try
        {
            var luz = new GameObject("Sol").AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.transform.SetParent(raiz.transform);
            luz.transform.rotation = Quaternion.Euler(40, -60, 0);
            var antes = UnityEngine.Object.Instantiate(prefab, new Vector3(0, 0, -1.6f), Quaternion.identity, raiz.transform);
            var nuevo = UnityEngine.Object.Instantiate(prefab, new Vector3(0, 0, 1.6f), Quaternion.identity, raiz.transform);
            Assert.That(guerra.Vestir(nuevo.gameObject, "montura_tiro", null), Is.True, "no se vistió el nuevo");
            // (los dos, uno detrás de otro en profundidad: de perfil desde la derecha, el de antes a la izquierda de la imagen)
            antes.transform.position = new Vector3(0, 0, 0);
            nuevo.transform.position = new Vector3(0, 0, 0);
            antes.transform.position += Vector3.forward * 0f;
            var marcas = new[] { "Head", "Chest", "Forearm.L", "FrontCannon.L", "Gaskin.L", "HindCannon.L" };
            GameObject Bola(Color c)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.transform.SetParent(raiz.transform);
                b.transform.localScale = Vector3.one * 0.07f;
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = c };
                b.GetComponent<Renderer>().sharedMaterial = m;
                return b;
            }
            var bolasA = Array.ConvertAll(marcas, _ => Bola(Color.red));
            var bolasN = Array.ConvertAll(marcas, _ => Bola(Color.yellow));
            Transform Hueso(Component c, string n)
            {
                foreach (var t in c.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
                return null;
            }
            var cam = new GameObject("Camara").AddComponent<Camera>();
            cam.transform.SetParent(raiz.transform);
            cam.orthographic = true;
            cam.orthographicSize = 1.4f;
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.78f, 0.8f, 0.82f);
            foreach (var (andar, mezcla) in new[] { ("parado", 0f), ("paso", 1f), ("trote", 2f), ("galope", 3f) })
                for (int k = 0; k < 3; k++)
                {
                    // el mismo estado y el mismo instante para los dos: desde cero, el andar puesto y la misma fracción del ciclo
                    float t = 0.15f + 0.3f * k;
                    foreach (var h in new[] { antes, nuevo })
                    {
                        var a = h.Animator;
                        a.Rebind();
                        a.SetFloat(AnimationHashes.MountGaitBlend, mezcla);
                        a.SetFloat(AnimationHashes.MountGaitScale, 1f);
                        a.Update(0f);
                        a.Update(t);
                    }
                    // las dos imágenes, una al lado de otra: primero el de antes, luego el nuevo (el otro escondido)
                    var hojas = new Texture2D[2];
                    for (int j = 0; j < 2; j++)
                    {
                        var este = j == 0 ? antes : nuevo;
                        var otro = j == 0 ? nuevo : antes;
                        foreach (var r in otro.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                        foreach (var r in este.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                        var bolas = j == 0 ? bolasA : bolasN;
                        for (int i = 0; i < marcas.Length; i++)
                        {
                            var hu = Hueso(este, marcas[i]);
                            bolas[i].SetActive(hu != null);
                            if (hu != null) bolas[i].transform.position = hu.position + Vector3.right * 0.4f;
                        }
                        foreach (var b in j == 0 ? bolasN : bolasA) b.SetActive(false);
                        cam.transform.position = este.transform.position + new Vector3(6, 1.1f, 0);
                        cam.transform.LookAt(este.transform.position + new Vector3(0, 1.1f, 0));
                        yield return null;
                        cam.Render();
                        var antesRt = RenderTexture.active;
                        RenderTexture.active = rt;
                        hojas[j] = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        hojas[j].ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                        hojas[j].Apply();
                        RenderTexture.active = antesRt;
                    }
                    if (!string.IsNullOrEmpty(carpeta))
                        for (int j = 0; j < 2; j++)
                            File.WriteAllBytes(Path.Combine(carpeta, $"neutra_{andar}_{k}_{(j == 0 ? "antes" : "nuevo")}.png"), hojas[j].EncodeToPNG());
                    foreach (var h in hojas) UnityEngine.Object.Destroy(h);
                }
        }
        finally
        {
            UnityEngine.Object.Destroy(raiz);
            rt.Release();
        }
    }
}
#endif
