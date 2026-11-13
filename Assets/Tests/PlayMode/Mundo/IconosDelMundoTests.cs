// El registro de iconos del mundo: que todos los de la lista del generador se cargan (y una captura con el catálogo entero), y que cada tropa,
// edificio y recurso de `/v1/datos` tiene el suyo (los que faltan en el prototipo se listan en el registro de la prueba, no se inventan).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConquestTactics.Mundo;
using ConquestTactics.Mundo.UI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI.Tests
{
    public class IconosDelMundoTests
    {
        GameObject go;
        RenderTexture rt;

        [TearDown]
        public void Limpiar()
        {
            if (go != null) UnityEngine.Object.Destroy(go);
            if (rt != null) rt.Release();
        }

        static readonly string[] Familias = { "edificio", "parte", "tropa", "mapa", "recurso", "glifo", "simbolo" };

        static IEnumerable<string> Ids(string familia)
        {
            var l = IconosDelMundo.Lista[familia];
            return l is JArray a ? a.Select(x => (string)x) : l is JObject o ? o.Properties().Select(p => p.Name) : Enumerable.Empty<string>();
        }

        [UnityTest]
        public IEnumerator ElCatalogoEntero()
        {
            var faltan = Familias.SelectMany(f => Ids(f).Where(id => IconosDelMundo.Imagen(f, id) == null).Select(id => $"{f}/{id}")).ToList();

            // Una pantalla con todos, por familias, para verlos (ninguno en blanco).
            go = new GameObject("Catalogo");
            var doc = go.AddComponent<UIDocument>();
            var ajustes = ScriptableObject.CreateInstance<PanelSettings>();
            ajustes.themeStyleSheet = Resources.Load<ThemeStyleSheet>("MundoUI/TemaMundo");
            ajustes.scaleMode = PanelScaleMode.ConstantPixelSize;
            rt = new RenderTexture(1600, 1200, 24);
            ajustes.targetTexture = rt;
            ajustes.clearColor = true;
            ajustes.colorClearValue = new Color32(0x1a, 0x14, 0x0e, 0xff);
            doc.panelSettings = ajustes;
            yield return null;
            var raiz = doc.rootVisualElement;
            raiz.styleSheets.Add(Resources.Load<StyleSheet>("MundoUI/Mundo"));
            raiz.style.paddingLeft = raiz.style.paddingTop = 16;
            foreach (var f in Familias)
            {
                raiz.Add(new Label(f.ToUpperInvariant() + $" ({Ids(f).Count()})") { style = { color = IconosDelMundo.Bronce, marginTop = 8 } });
                var fila = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
                foreach (var id in Ids(f))
                {
                    var celda = new VisualElement { style = { width = 74, alignItems = Align.Center, marginBottom = 4 } };
                    celda.Add(IconosDelMundo.Icono(f, id, 32));
                    celda.Add(new Label(id) { style = { fontSize = 9, color = new Color(0.85f, 0.8f, 0.7f), unityTextAlign = TextAnchor.MiddleCenter, whiteSpace = WhiteSpace.Normal } });
                    fila.Add(celda);
                }
                raiz.Add(fila);
            }
            yield return null;
            yield return null;
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            if (string.IsNullOrEmpty(carpeta) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { Assert.IsEmpty(faltan, "cada icono de la lista se carga como VectorImage"); yield break; }
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = null;
            // Ninguno en blanco: cada icono deja algún píxel distinto del fondo en su sitio.
            var vacios = raiz.Query(className: "icono").ToList().Where(i =>
            {
                var b = i.worldBound;
                for (int x = (int)b.xMin + 2; x < b.xMax - 2; x += 2)
                    for (int y = (int)b.yMin + 2; y < b.yMax - 2; y += 2)
                    {
                        var p = t.GetPixel(x, rt.height - 1 - y);
                        if (Mathf.Abs(p.r - 0x1a / 255f) + Mathf.Abs(p.g - 0x14 / 255f) + Mathf.Abs(p.b - 0x0e / 255f) > 0.08f) return false;
                    }
                return true;
            }).Select(i => i.name).ToList();
            Directory.CreateDirectory(carpeta);
            File.WriteAllBytes(Path.Combine(carpeta, "30_catalogo_de_iconos.png"), t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
            Assert.IsEmpty(vacios, "ningún icono sale en blanco");
            Assert.IsEmpty(faltan, "cada icono de la lista se carga como VectorImage");
        }

        /// <summary>Cada tropa, edificio y recurso de las reglas del motor tiene icono; los que el prototipo no dibuja se dicen (no se inventan).</summary>
        [UnityTest]
        public IEnumerator CadaCosaDeLasReglasTieneIcono()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            var bajada = new Acceso(url).Bajar("/v1/datos");
            var fin = Time.realtimeSinceStartup + 20;
            while (!bajada.IsCompleted && Time.realtimeSinceStartup < fin) yield return null;
            var datos = DatosDeJuego.Leer(bajada.Result);
            var tropas = datos.TropasReclutables.Select(x => (string)x["id"]).ToList();
            JToken Si(string ruta) { try { return datos.En(ruta); } catch (FaltaDato) { return null; } }
            var edificios = ((Si("documentos.Economia_Construccion.EDIFICIO_CATALOGO") as JObject)?.Properties().Select(p => p.Name) ?? Enumerable.Empty<string>())
                .Concat((Si("documentos.Campamentos_Mercenarios.MERCENARIOS.edificiosMilitares") as JArray)?.Select(x => (string)x) ?? Enumerable.Empty<string>())
                .Concat(new[] { "taberna", "vivienda", "mercado", "puestoMercado", "plazaDeArmas", "plaza", "pozo", "parque" }).Distinct().ToList();
            // Los recursos: los que salen en algún coste, receta o nodo de las reglas.
            var recursos = new HashSet<string>();
            foreach (var p in ((JObject)datos.En("documentos")).Descendants().OfType<JProperty>())
                if ((p.Name == "costo" || p.Name == "entrada" || p.Name == "salida" || p.Name == "insumos" || p.Name == "productos") && p.Value is JObject o)
                    foreach (var r in o.Properties()) recursos.Add(r.Name);
            foreach (var r in (Si("documentos.Generacion_Del_Mundo.RECURSO_CANTIDAD_NODO") as JObject)?.Properties() ?? Enumerable.Empty<JProperty>()) recursos.Add(r.Name);
            var sinTropa = tropas.Where(id => IconosDelMundo.Imagen("tropa", id) == null).ToList();
            var sinEdificio = edificios.Where(id => IconosDelMundo.Imagen("edificio", id) == null).ToList();
            var sinRecurso = recursos.Where(id => IconosDelMundo.Imagen("recurso", id) == null).OrderBy(x => x).ToList();
            Debug.Log($"[Iconos] {tropas.Count} tropas, {edificios.Count} edificios, {recursos.Count} recursos en /v1/datos. Sin icono en el prototipo: " +
                $"tropas [{string.Join(", ", sinTropa)}] · edificios [{string.Join(", ", sinEdificio)}] · recursos [{string.Join(", ", sinRecurso)}]");
            // El prototipo dibuja un edificio genérico (una casa) para los que no tienen dibujo y el emoji 📦 para los recursos sin el suyo: aquí,
            // igual (lo hace `IconosDelMundo` al pedirlos). Lo que no puede faltar es la tropa: el prototipo dibuja todas.
            Assert.IsEmpty(sinTropa, "cada tropa de /v1/datos tiene su icono");
        }
    }
}
