// La crónica del mundo (paso 13 del bloque 4, tanda 2), con el ratón de verdad contra el motor. El Rey y Gobernador funda su plaza y entra; el
// botón «Crónica» de la barra, tras la campana, abre su panel: la fundación de su plaza sale marcada como suya, con su icono y su fecha. La
// campana cierra la crónica y abre los Avisos.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using V1 = BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class PantallasDeMundoTests
    {
        [Category("Faccion")]
        [UnityTest, Timeout(900_000)]
        public IEnumerator LaCronica()
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
            rt = new RenderTexture(1600, 900, 24);
            doc.panelSettings.targetTexture = rt;
            doc.panelSettings.clearColor = true;
            var raiz = doc.rootVisualElement;
            yield return null;
            raiz.Q<TextField>("nick").value = nick;
            raiz.Q<TextField>("clave").value = "clave-de-prueba-1";
            Pulsar(raiz.Q<Button>("enviar"));
            HeroSelectionSceneController selector = null;
            yield return Esperar(() => (selector = UnityEngine.Object.FindAnyObjectByType<HeroSelectionSceneController>()) != null, 30, "el selector");
            LogAssert.ignoreFailingMessages = true; // el error de relleno del escuadrón vacío al nacer el héroe en FeudoScene
            selector.confirmButton.onClick.Invoke();
            yield return Esperar(() => raiz.Q("plaza") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior de la plaza");
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            var s = conexion.Sesion;
            V1.Heroe Yo() => s.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            V1.Asentamiento Plaza() => s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId)?.Asentamiento;
            V1.Faccion Faccion() => s.Almacen.Buscar(V1.TipoEntidad.Faccion, Yo().FaccionId)?.Faccion;
            if (Environment.GetEnvironmentVariable("MUNDO_PROTO") == "cronica")
            {
                Debug.Log($"[Prueba] escena del prototipo: yo={nick} plaza={plazaId}");
                LogAssert.ignoreFailingMessages = false;
                yield break;
            }
            yield return Quieto(1f);
            var entrada = UnityEngine.InputSystem.InputSystem.settings;
            var fondo = entrada.backgroundBehavior;
            var editorAntes = entrada.editorInputBehaviorInPlayMode;
            entrada.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            entrada.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var otros = UnityEngine.InputSystem.InputSystem.devices.OfType<UnityEngine.InputSystem.Mouse>().Where(m => m.enabled).ToList();
            foreach (var m in otros) UnityEngine.InputSystem.InputSystem.DisableDevice(m);
            var raton = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            raton.MakeCurrent();
            VisualElement Panel() => raiz.Q("panel-cronica");
            try
            {
                // El libro, tras la campana.
                var boton = raiz.Q<Button>("cronica");
                Assert.IsNotNull(boton, "el botón Crónica en la barra");
                Assert.AreEqual(boton.parent.IndexOf(raiz.Q<Button>("avisos")) + 1, boton.parent.IndexOf(boton), "tras la campana");
                yield return ClicFijo(raton, raiz, boton);
                yield return Esperar(() => Panel()?.Q(className: "cronica-entrada") != null, 10, "las entradas de la crónica");
                var frases = Panel().Query<Label>(className: "cronica-frase").ToList().Select(l => l.text).ToList();
                Debug.Log("[Prueba] crónica: " + string.Join(" / ", frases.Take(6)));
                var nombre = Plaza().HasNombre ? Plaza().Nombre : plazaId;
                var propia = Panel().Query(className: "cronica-entrada").ToList().FirstOrDefault(e => e.ClassListContains("es-propia") && e.Q<Label>(className: "cronica-frase").text.Contains(nombre));
                Assert.IsNotNull(propia, "la fundación de la plaza, marcada como tuya");
                Assert.IsNotNull(propia.Q(className: "cronica-icono").Children().FirstOrDefault(), "con su icono");
                Assert.IsFalse(string.IsNullOrEmpty(propia.Q<Label>(className: "cronica-fecha").text), "con su fecha");
                yield return Capturar("230_cronica");
                yield return EnTamanos(raiz, "230_cronica", () => DentroDelMarco(Panel(), "la crónica"));
                // La campana: cierra la crónica y abre los Avisos.
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("avisos"));
                yield return Esperar(() => Panel() == null && raiz.Q(className: "panel-avisos") != null, 5, "los Avisos en lugar de la crónica");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                foreach (var m in otros) if (m.added) UnityEngine.InputSystem.InputSystem.EnableDevice(m);
                entrada.backgroundBehavior = fondo;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
