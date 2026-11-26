// El rastreador de misiones con su estilo (26-11): en la plaza, al principio de su columna derecha; en el mapa, con el héroe fuera, la tarjeta de
// arriba a la derecha. En el mapa salía sin estilo (a todo el ancho, gris). Se mira el
// estilo calculado, no solo las clases. (La causa: `Resources.Load<StyleSheet>("MundoUI/Plaza")` daba la hoja en línea de Plaza.uxml; ver
// `PantallasDeMundo.HojaDePlaza`.)
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class PantallasDeMundoTests
    {
        [UnityTest, Timeout(600_000)]
        public IEnumerator ElRastreadorConSuEstilo()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza");
            string nick = null, plazaId = null;
            yield return FundarPlaza(url, x => nick = x, x => plazaId = x);
            foreach (var k0 in Preferencias) PlayerPrefs.DeleteKey(k0);
            go = new GameObject("Mundo");
            go.SetActive(false);
            var conexion = go.AddComponent<ConexionDeMundo>();
            typeof(ConexionDeMundo).GetField("servidor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(conexion, url);
            go.AddComponent<PantallasDeMundo>();
            CamaraComoLaDeMundoScene(go);
            go.SetActive(true);
            yield return null;
            var doc = go.GetComponentInChildren<UIDocument>();
            rt = new RenderTexture(1920, 1080, 24);
            doc.panelSettings.targetTexture = rt;
            doc.panelSettings.clearColor = true;
            var raiz = doc.rootVisualElement;
            yield return null;
            raiz.Q<TextField>("nick").value = nick;
            raiz.Q<TextField>("clave").value = "clave-de-prueba-1";
            Pulsar(raiz.Q<Button>("enviar"));
            HeroSelectionSceneController selector = null;
            yield return Esperar(() => (selector = UnityEngine.Object.FindAnyObjectByType<HeroSelectionSceneController>()) != null, 30, "el selector");
            LogAssert.ignoreFailingMessages = true;
            selector.confirmButton.onClick.Invoke();
            yield return Esperar(() => raiz.Q("plaza") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior de la plaza");
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            yield return Esperar(() => raiz.Q("rastreador") != null, 10, "el rastreador");
            ConSuEstilo(raiz, "en la plaza");
            yield return Capturar("220_rastreador_plaza");
            Pulsar(raiz.Q<Button>("salir-al-mundo"));
            yield return Esperar(() => raiz.Q("ventana-salida") != null, 5, "la ventana de salida");
            Pulsar(raiz.Q("ventana-salida").Q<Button>("salir"));
            yield return Esperar(() => raiz.Q("mapa") != null, 30, "el mapa");
            yield return Esperar(() => raiz.Q("rastreador") != null, 10, "el rastreador en el mapa");
            for (int i = 0; i < 30; i++) yield return null;
            ConSuEstilo(raiz, "en el mapa");
            var r = raiz.Q("rastreador");
            Assert.AreEqual(Position.Absolute, r.resolvedStyle.position, "en el mapa, flotando");
            Assert.That(r.layout.width, Is.EqualTo(260).Within(1), "en el mapa, la tarjeta de 260 px");
            Assert.That(raiz.layout.width - r.worldBound.xMax, Is.EqualTo(16).Within(2), "a 16 px del borde derecho");
            yield return Capturar("221_rastreador_mapa");
            // (a otro tamaño, como el de la pantalla de Víctor)
            var otro = new RenderTexture(2000, 1112, 24);
            doc.panelSettings.targetTexture = otro;
            rt.Release();
            rt = otro;
            for (int i = 0; i < 10; i++) yield return null;
            ConSuEstilo(raiz, "en el mapa a 2000×1112");
            yield return Capturar("222_rastreador_mapa_2000");
        }

        /// <summary>El rastreador con el estilo de su hoja (no solo con sus clases): fondo oscuro, la cabecera en fila y los títulos a la izquierda.</summary>
        static void ConSuEstilo(VisualElement raiz, string donde)
        {
            var r = raiz.Q("rastreador");
            Assert.That(r.resolvedStyle.backgroundColor.a, Is.GreaterThan(0.5f), $"{donde}: el fondo oscuro de la tarjeta");
            Assert.That(r.resolvedStyle.backgroundColor.r, Is.LessThan(0.2f), $"{donde}: oscuro, no el gris del tema");
            Assert.AreEqual(FlexDirection.Row, r.Q<Button>("rastreador-plegar").resolvedStyle.flexDirection, $"{donde}: la cabecera en fila");
            var titulo = r.Q<Label>(className: "rastreador-item-titulo");
            if (titulo != null) Assert.AreEqual(TextAnchor.MiddleLeft, titulo.resolvedStyle.unityTextAlign, $"{donde}: los títulos a la izquierda");
        }

    }
}
