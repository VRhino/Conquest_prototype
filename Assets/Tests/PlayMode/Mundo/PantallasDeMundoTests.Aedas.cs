// Los Aedas (paso 13 del bloque 4, tanda 1), con el ratón de verdad contra el motor. El Rey y Gobernador funda su plaza y entra. En un servidor
// la administración pone en ella un itinerante y un residente (`colocar_aeda`, motor ecfb23d). Sale la lira en el riel: la ventana Aedas, con el
// itinerante, lo que os vende (y «Comprar» si hay algo y oro) y el residente. Si el itinerante no vende nada, la compra se queda sin prueba y lo dice.
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
        [UnityTest, Timeout(900_000)]
        public IEnumerator TusAedas()
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
            if (Environment.GetEnvironmentVariable("MUNDO_PROTO") == "aedas")
            {
                Debug.Log($"[Prueba] escena del prototipo: yo={nick} plaza={plazaId}");
                LogAssert.ignoreFailingMessages = false;
                yield break;
            }
            yield return Quieto(1f);
            V1.AedaItinerante Parado() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.AedaItinerante).Select(e => e.AedaItinerante).FirstOrDefault(x => x.HasEnAsentamientoId && x.EnAsentamientoId == plazaId);
            // Un itinerante parado en la plaza y un residente, con la orden de administración `colocar_aeda` (motor ecfb23d): ya no se espera a que
            // pare uno.
            var aqui = new Google.Protobuf.WellKnownTypes.Struct();
            aqui.Fields["asentamiento_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(plazaId);
            yield return Administrar(url, "colocar_aeda", aqui);
            var residente = new Google.Protobuf.WellKnownTypes.Struct();
            residente.Fields["asentamiento_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(plazaId);
            residente.Fields["residente"] = Google.Protobuf.WellKnownTypes.Value.ForBool(true);
            yield return Administrar(url, "colocar_aeda", residente);
            V1.AedaResidente Residente() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.AedaResidente).Select(e => e.AedaResidente).FirstOrDefault();
            for (var f = Time.realtimeSinceStartup + 10; (Parado() == null || Residente() == null) && Time.realtimeSinceStartup < f;) yield return null;
            Debug.Log($"[Prueba] aedas: itinerantes {string.Join(",", s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.AedaItinerante).Select(e => $"{e.AedaItinerante.Id}@{(e.AedaItinerante.HasEnAsentamientoId ? e.AedaItinerante.EnAsentamientoId : "camino")}"))}; residentes {s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.AedaResidente).Count()}");
            Assert.IsNotNull(Parado(), "el itinerante, parado en la plaza");
            Assert.IsNotNull(Residente(), "un residente en la plaza");

            var entrada = UnityEngine.InputSystem.InputSystem.settings;
            var fondo = entrada.backgroundBehavior;
            var editorAntes = entrada.editorInputBehaviorInPlayMode;
            entrada.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            entrada.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var otros = UnityEngine.InputSystem.InputSystem.devices.OfType<UnityEngine.InputSystem.Mouse>().Where(m => m.enabled).ToList();
            foreach (var m in otros) UnityEngine.InputSystem.InputSystem.DisableDevice(m);
            var raton = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            raton.MakeCurrent();
            IEnumerator Clic(VisualElement e) => ClicEnVentana(raton, raiz, e);
            VisualElement Ventana() => raiz.Q("plaza-ventana");
            try
            {
                // La lira en el riel, solo con un Aeda dentro: abre la ventana Aedas con el itinerante.
                yield return Esperar(() => raiz.Q("plaza").Q("riel").Q<Button>("riel-aedas") != null, 10, "los Aedas en el riel");
                yield return Clic(raiz.Q("plaza").Q("riel").Q<Button>("riel-aedas"));
                yield return Esperar(() => Ventana()?.Q("aedas-itinerante") != null, 5, "el itinerante en la ventana Aedas");
                Assert.IsTrue(Ventana().Q("aedas-itinerante").Q<Label>(className: "aedas-titulo-texto").text.Length > 0, "con su nombre");
                yield return Capturar("220_ventana_aedas");
                yield return EnTamanos(raiz, "220_ventana_aedas", () => DentroDelMarco(Ventana(), "la ventana Aedas"));
                // El residente, con sus épicas (las que haya: las de las tecnologías que la Facción tiene).
                yield return Esperar(() => Ventana()?.Q("aedas-residente-" + Residente().Id) != null, 5, "el residente en la ventana Aedas");
                var epicas = Ventana().Q("aedas-residente-" + Residente().Id).Query<VisualElement>().ToList().Count(x => x.name != null && x.name.StartsWith("aedas-epica-"));
                Debug.Log($"[Prueba] aedas: residente {Residente().Id}, {epicas} épicas");
                // Comprar, si os vende algo y el almacén tiene el oro (en una partida recién empezada no suele haber nada revelado).
                var comprar = Ventana().Query<Button>(className: "aedas-boton").ToList().FirstOrDefault(b => b.name.StartsWith("aedas-comprar-"));
                if (comprar != null)
                {
                    var tec = comprar.name.Substring("aedas-comprar-".Length);
                    yield return Clic(comprar);
                    yield return Esperar(() => s.Almacen.Buscar(V1.TipoEntidad.TecnologiaFaccion, Yo().FaccionId)?.Tecnologias?.Aparecidas.Contains(tec) == true, 10, "la tecnología comprada");
                }
                else Debug.Log("[Prueba] aedas: no os vende nada (nada revelado): la compra se queda sin prueba");
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
