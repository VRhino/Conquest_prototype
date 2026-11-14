// La BattlePrepScene de Conquest (UGUI), tal cual y con su relleno de prueba (TestEnv), capturada a los cuatro tamaños de ventana: es la
// referencia de la escena de preparación del mundo (PreparacionScene, UI Toolkit), que la calca. Sus lienzos se pasan a una cámara que
// pinta en una textura del tamaño pedido; nada más se toca. Va aparte ([Explicit]): solo hace capturas.
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ConquestTactics.Mundo.UI.Tests
{
    public class PreparacionDeConquestTests
    {
        static readonly (int ancho, int alto)[] Tamanos = { (1280, 720), (1600, 900), (1920, 1080), (1950, 1130) };

        [UnityTest, Explicit("Solo capturas de referencia de la BattlePrepScene original")]
        public IEnumerator LaBattlePrepSceneOriginal()
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            if (string.IsNullOrEmpty(carpeta)) Assert.Ignore("sin MUNDO_CAPTURAS");
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("BattlePrepScene");
            yield return null;
            var fin = Time.realtimeSinceStartup + 10;
            while (Time.realtimeSinceStartup < fin) yield return null;
            var camara = new GameObject("Camara captura").AddComponent<Camera>();
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = Color.black;
            camara.cullingMask = 1 << 5; // UI
            foreach (var lienzo in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!lienzo.isRootCanvas || lienzo.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                lienzo.renderMode = RenderMode.ScreenSpaceCamera;
                lienzo.worldCamera = camara;
                lienzo.planeDistance = 1;
            }
            foreach (var (ancho, alto) in Tamanos)
            {
                var rt = new RenderTexture(ancho, alto, 24);
                camara.targetTexture = rt;
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return null;
                camara.Render();
                RenderTexture.active = rt;
                var t = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
                t.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory(carpeta);
                File.WriteAllBytes(Path.Combine(carpeta, $"50_battleprep_original_{ancho}x{alto}.png"), t.EncodeToPNG());
                UnityEngine.Object.Destroy(t);
                camara.targetTexture = null;
                rt.Release();
            }
            // El inventario de la disposición a 1950x1130: cada elemento visible con su rectángulo en píxeles, su sprite, su color y su texto.
            var rtInv = new RenderTexture(1950, 1130, 24);
            camara.targetTexture = rtInv;
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            var sb = new System.Text.StringBuilder();
            foreach (var lienzo in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!lienzo.isRootCanvas) continue;
                void Volcar(Transform t, int nivel)
                {
                    if (!t.gameObject.activeInHierarchy) return;
                    var r = t as RectTransform;
                    var linea = new string(' ', nivel * 2) + t.name;
                    if (r != null)
                    {
                        var esquinas = new Vector3[4];
                        r.GetWorldCorners(esquinas);
                        var a0 = RectTransformUtility.WorldToScreenPoint(camara, esquinas[0]);
                        var a2 = RectTransformUtility.WorldToScreenPoint(camara, esquinas[2]);
                        linea += $" [{a0.x:0},{1130 - a2.y:0} {a2.x - a0.x:0}x{a2.y - a0.y:0}]";
                    }
                    var img = t.GetComponent<UnityEngine.UI.Image>();
                    if (img != null && img.enabled) linea += $" img={(img.sprite != null ? img.sprite.name : "-")} col=#{ColorUtility.ToHtmlStringRGBA(img.color)} tipo={img.type}";
                    var raw = t.GetComponent<UnityEngine.UI.RawImage>();
                    if (raw != null && raw.enabled) linea += $" raw={(raw.texture != null ? raw.texture.name : "-")}";
                    var tmp = t.GetComponent<TMPro.TMP_Text>();
                    if (tmp != null && tmp.enabled) linea += $" texto=«{tmp.text}» fuente={tmp.font?.name} tam={tmp.fontSize} col=#{ColorUtility.ToHtmlStringRGBA(tmp.color)} estilo={tmp.fontStyle} alin={tmp.alignment}";
                    var boton = t.GetComponent<UnityEngine.UI.Button>();
                    if (boton != null) linea += " BOTON";
                    foreach (var mb in t.GetComponents<MonoBehaviour>())
                        if (mb != null && mb.GetType().Namespace == null && !(mb is UnityEngine.EventSystems.UIBehaviour)) linea += " <" + mb.GetType().Name + ">";
                    sb.AppendLine(linea);
                    foreach (Transform h in t) Volcar(h, nivel + 1);
                }
                Volcar(lienzo.transform, 0);
            }
            File.WriteAllText(Path.Combine(carpeta, "50_battleprep_inventario.txt"), sb.ToString());
            camara.targetTexture = null;
            rtInv.Release();
            LogAssert.ignoreFailingMessages = false;
        }
    }
}
