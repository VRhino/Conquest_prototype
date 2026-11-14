// Las piezas de tropa del mundo (UI Toolkit) frente a las de Conquest (UGUI). Pinta las mismas tropas, una de cada rareza, con los prefabs
// originales (Squad_Icon y Squad_option_Prep, sin tocarlos) y con `PiezasDeTropa`, en dos capturas para compararlas. Comprueba también
// que cada tropa de `/v1/datos` tiene su SquadData con sus tres imágenes: si falta, la prueba lo dice, sin nada de reserva.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI.Tests
{
    public class PiezasDeTropaTests
    {
        readonly List<UnityEngine.Object> sobran = new List<UnityEngine.Object>();
        readonly List<RenderTexture> texturas = new List<RenderTexture>();

        [TearDown]
        public void Limpiar()
        {
            foreach (var o in sobran) if (o != null) UnityEngine.Object.Destroy(o);
            foreach (var t in texturas) t.Release();
            sobran.Clear();
            texturas.Clear();
        }

        /// <summary>Una tropa del motor por rareza (de la más baja a la más alta).</summary>
        static List<SquadData> UnaPorRareza() =>
            Resources.LoadAll<SquadData>("Squads/BronzeAge").GroupBy(d => d.rarity).OrderBy(g => g.Key).Select(g => g.OrderBy(d => d.id).First()).ToList();

        [Test]
        public void CadaTropaDelMotorTieneSusImagenes()
        {
            var catalogo = Resources.Load<TextAsset>("Squads/BronzeAge/catalogo");
            Assert.IsNotNull(catalogo, "el catálogo de tropas del motor que escribe TropasDelMotor");
            var todas = Resources.LoadAll<SquadData>("Squads/BronzeAge");
            Assert.Greater(todas.Length, 0, "las tropas del motor en Resources/Squads/BronzeAge");
            var faltan = new List<string>();
            foreach (var d in todas)
            {
                var sin = new List<string>();
                if (d.icon == null) sin.Add("icon");
                if (d.background == null) sin.Add("background");
                if (d.unitImage == null) sin.Add("unitImage");
                if (sin.Count > 0) faltan.Add($"{d.id} sin {string.Join(", ", sin)}");
                if (PiezasDeTropa.Datos(d.id) != d) faltan.Add($"{d.id}: no se encuentra por su id en Resources/Squads/BronzeAge/{d.id}/{d.id}");
            }
            Assert.IsEmpty(faltan, "cada tropa del motor con sus tres imágenes");
        }

        /// <summary>Cada tropa de las reglas que da el motor (`/v1/datos`) tiene su SquadData; las que no, se dicen.</summary>
        [UnityTest]
        public IEnumerator CadaTropaDeLasReglasTieneSuPieza()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            var bajada = new Acceso(url).Bajar("/v1/datos");
            var fin = Time.realtimeSinceStartup + 20;
            while (!bajada.IsCompleted && Time.realtimeSinceStartup < fin) yield return null;
            var datos = DatosDeJuego.Leer(bajada.Result);
            var tropas = datos.TropasReclutables.Select(x => (string)x["id"]).ToList();
            var sinPieza = tropas.Where(id => PiezasDeTropa.Datos(id) == null || PiezasDeTropa.Miniatura(id) == null || PiezasDeTropa.Tarjeta(id, null, null, null) == null).ToList();
            Debug.Log($"[Tropas] {tropas.Count} tropas en /v1/datos; sin pieza de Conquest: [{string.Join(", ", sinPieza)}]");
            // Las que faltan a sabiendas, cada una con su porqué (no se rellenan con nada: en las pantallas salen sin pieza).
            foreach (var (id, porque) in SinPiezaASabiendas)
                if (sinPieza.Remove(id)) Debug.LogWarning($"[Tropas] {id} sin pieza de Conquest, a sabiendas: {porque}");
            Assert.IsEmpty(sinPieza, "cada tropa del motor con su pieza de Conquest (o en la lista de las que faltan a sabiendas)");
        }

        /// <summary>Tropas del motor que Conquest aún no tiene, documentadas (no es una reserva: se dicen en cada pasada).</summary>
        static readonly (string id, string porque)[] SinPiezaASabiendas =
        {
            ("carros_guerra", "Conquest aún no tiene carros de guerra; un ticket con carros se rechaza (Docs/Tropas_Del_Motor.md, TropasDelMotor.Bases)"),
        };

        [Test]
        public void LasQueFaltanASabiendasSiguenFaltando()
        {
            // Cuando una llegue a Conquest, sale de la lista.
            foreach (var (id, _) in SinPiezaASabiendas) Assert.IsNull(PiezasDeTropa.Datos(id), $"{id} ya tiene SquadData: quítala de SinPiezaASabiendas");
        }

        [UnityTest]
        public IEnumerator LasPiezasComoLasDeConquest()
        {
            var tropas = UnaPorRareza();
            Assert.Greater(tropas.Count, 1, "tropas de varias rarezas");
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            bool capturar = !string.IsNullOrEmpty(carpeta) && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

#if UNITY_EDITOR
            // Las de Conquest (UGUI): los prefabs tal cual, en un lienzo que pinta en una textura.
            var rtConquest = new RenderTexture(1100, 560, 24);
            texturas.Add(rtConquest);
            var camara = new GameObject("Camara piezas Conquest").AddComponent<Camera>();
            sobran.Add(camara.gameObject);
            camara.targetTexture = rtConquest;
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = new Color32(0x19, 0x14, 0x10, 0xff);
            var lienzo = new GameObject("Lienzo piezas Conquest", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            sobran.Add(lienzo.gameObject);
            lienzo.renderMode = RenderMode.ScreenSpaceCamera;
            lienzo.worldCamera = camara;
            var icono = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattlePrep/Squad_Icon.prefab");
            var opcion = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattlePrep/Squad_option_Prep.prefab");
            Assert.IsNotNull(icono, "el prefab Squad_Icon");
            Assert.IsNotNull(opcion, "el prefab Squad_option_Prep");
            for (int i = 0; i < tropas.Count; i++)
            {
                var d = tropas[i];
                var mini = UnityEngine.Object.Instantiate(icono, lienzo.transform);
                var rm = (RectTransform)mini.transform;
                rm.anchorMin = rm.anchorMax = rm.pivot = new Vector2(0, 1);
                rm.anchoredPosition = new Vector2(30 + i * 200, -30);
                mini.GetComponent<SquadIconController>().Initialize(new SquadIconData(d.id, d.background, d.icon, SquadUtils.GetRarityColor(d.rarity)));
                var tarjeta = UnityEngine.Object.Instantiate(opcion, lienzo.transform);
                var rt = (RectTransform)tarjeta.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(30 + i * 200, -100);
                var ui = tarjeta.GetComponent<SquadOptionUI>();
                ui.SetSquadData(d);
                ui.ToggleBattlePreMode();
                ui.SetSelected(i == 0);
                var nombre = new GameObject("Nombre", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                nombre.transform.SetParent(lienzo.transform, false);
                var rn = nombre.rectTransform;
                rn.anchorMin = rn.anchorMax = rn.pivot = new Vector2(0, 1);
                rn.anchoredPosition = new Vector2(30 + i * 200, -350);
                rn.sizeDelta = new Vector2(180, 60);
                nombre.fontSize = 14;
                nombre.text = $"{d.id}\n{d.rarity}";
            }
            yield return null;
            yield return null;
            if (capturar) Guardar(rtConquest, Path.Combine(carpeta, "31_piezas_de_conquest_ugui.png"));
#endif

            // Las del mundo (UI Toolkit), con las mismas tropas.
            var go = new GameObject("Piezas del mundo");
            sobran.Add(go);
            var doc = go.AddComponent<UIDocument>();
            var ajustes = ScriptableObject.CreateInstance<PanelSettings>();
            sobran.Add(ajustes);
            ajustes.themeStyleSheet = Resources.Load<ThemeStyleSheet>("MundoUI/TemaMundo");
            ajustes.scaleMode = PanelScaleMode.ConstantPixelSize;
            var rtMundo = new RenderTexture(1100, 560, 24);
            texturas.Add(rtMundo);
            ajustes.targetTexture = rtMundo;
            ajustes.clearColor = true;
            ajustes.colorClearValue = new Color32(0x19, 0x14, 0x10, 0xff);
            doc.panelSettings = ajustes;
            yield return null;
            var raiz = doc.rootVisualElement;
            raiz.styleSheets.Add(Resources.Load<StyleSheet>("MundoUI/Mundo"));
            raiz.style.flexDirection = FlexDirection.Row;
            raiz.style.paddingLeft = 30;
            raiz.style.paddingTop = 30;
            for (int i = 0; i < tropas.Count; i++)
            {
                var d = tropas[i];
                var columna = new VisualElement { style = { width = 200 } };
                var mini = PiezasDeTropa.Miniatura(d.id);
                var tarjeta = PiezasDeTropa.Tarjeta(d.id, 14, 25, 30, i == 0);
                var estrellas = PiezasDeTropa.Estrellas(d.id);
                Assert.IsNotNull(mini, d.id);
                Assert.IsNotNull(tarjeta, d.id);
                Assert.IsNotNull(estrellas, d.id);
                Assert.AreEqual(Mathf.CeilToInt(SquadUtils.GetRarityInfo(d.rarity).stars), estrellas.childCount, $"las estrellas de {d.id} ({d.rarity})");
                mini.style.marginBottom = 30;
                columna.Add(mini);
                columna.Add(tarjeta);
                estrellas.style.marginTop = 8;
                columna.Add(estrellas);
                columna.Add(new Label($"{d.id}\n{d.rarity}") { style = { color = Color.white, fontSize = 14, whiteSpace = WhiteSpace.Normal } });
                raiz.Add(columna);
            }
            yield return null;
            yield return null;
            if (capturar) Guardar(rtMundo, Path.Combine(carpeta, "32_piezas_del_mundo.png"));
        }

        static void Guardar(RenderTexture rt, string ruta)
        {
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.WriteAllBytes(ruta, t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
        }
    }
}
