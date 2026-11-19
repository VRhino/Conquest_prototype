// Fundar desde el mapa (duda 38, tanda 1): un héroe con su Facción sale del campamento con una Caravana de Fundación (la administración se la da)
// y, ya en un sitio, abre con la casa del riel el panel «Fundar asentamiento»: los recursos a su alcance y «Fundar aquí». Funda y queda dentro de
// su plaza nueva. Si el motor no deja fundar ahí, su motivo sale en el panel.
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
        [Category("Mapa")]
        [UnityTest, Timeout(600_000)]
        public IEnumerator TuFundacionDesdeElMapa()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta darle la caravana");
            string nick = null;
            yield return FundarPlaza(url, x => nick = x, soloLaColumna: true);

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
            LogAssert.ignoreFailingMessages = true;
            selector.confirmButton.onClick.Invoke();
            yield return Esperar(() => ElMapaSiHay()?.Columna != null && raiz.Q<Button>("riel-fundar") != null, 60, "el mapa con tu columna y la casa en el riel");
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");

            // La barra de víveres, con los de la columna (no los del héroe, que fuera no los lleva): más de 0.
            yield return Esperar(() => raiz.Q<Label>("viveres-cifra") != null, 10, "la barra de víveres");
            var cifraViveres = raiz.Q<Label>("viveres-cifra").text;
            Debug.Log("[Prueba] víveres: " + cifraViveres);
            Assert.Greater(int.Parse(cifraViveres.Split('/')[0].Trim()), 0, "los víveres de la columna en la barra: " + cifraViveres);

            // La casa abre el panel: la llevas tú, así que «Fundar aquí» con los recursos a tu alcance.
            Pulsar(raiz.Q<Button>("riel-fundar"));
            yield return Esperar(() => raiz.Q("ventana-fundar")?.Q<Button>("fundar-aqui") != null, 10, "el panel de fundar con «Fundar aquí»");
            var panel = raiz.Q("ventana-fundar");
            Assert.IsNotNull(panel.Q("fundar-recursos"), "los recursos a tu alcance");
            var textos = panel.Q("fundar-recursos").Query<Label>().ToList().Select(l => l.text).ToList();
            Debug.Log("[Prueba] fundar: " + string.Join(" | ", textos));
            Assert.IsTrue(textos.Any(t => t == "Recursos en la zona inicial"), "«Recursos en la zona inicial»");
            Assert.IsTrue(textos.Any(t => t.Contains("bosque")), "el acceso a bosque o sus parcelas");
            // Con el panel abierto, la vista previa en el mapa 3D (tanda 2): la zona, los sitios de aldea y las parcelas.
            Assert.IsNotNull(GameObject.Find("Fundar: vista previa"), "la vista previa de fundar en el mapa");
            yield return Capturar("300_fundar_panel");

            // Fundar aquí: la plaza nueva, y dentro de ella; si el motor no deja, su motivo en el panel.
            var antes = conexion.Sesion.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Asentamiento).Count();
            Pulsar(panel.Q<Button>("fundar-aqui"));
            yield return Esperar(() => conexion.Sesion.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Asentamiento).Count() > antes
                || !string.IsNullOrEmpty(raiz.Q("ventana-fundar")?.Q<Label>("fundar-error")?.text), 20, "la plaza fundada o el motivo");
            var motivo = raiz.Q("ventana-fundar")?.Q<Label>("fundar-error")?.text;
            Assert.IsTrue(string.IsNullOrEmpty(motivo), "el motor no dejó fundar aquí: " + motivo);
            yield return Esperar(() => raiz.Q("plaza") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "dentro de la plaza nueva");
            Assert.IsNull(raiz.Q("ventana-fundar"), "el panel de fundar, cerrado");
            LogAssert.ignoreFailingMessages = false;
        }
    }
}
