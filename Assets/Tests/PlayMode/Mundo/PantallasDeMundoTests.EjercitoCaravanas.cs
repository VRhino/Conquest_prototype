// Las caravanas del ejército (paso 11 del bloque 4, tanda 3), con el ratón de verdad contra el motor. El Gobernador funda su plaza con el Mercado
// y crea una caravana comercial. En «Salir al mundo» → «Ejército» convoca y, en la preparación, junto a la Caravana de Fundación (si la hay), le salen
// las caravanas comerciales libres de la plaza: «Llevárnosla». Sale con el ejército y la caravana se engancha. En el mapa, la ventana ⚑ la enseña
// con «Cargar» (a la puerta de su plaza); carga, y su carro 3D va detrás del ejército. Entra con el ejército en la plaza y, en su lateral,
// «Aparcar aquí» la deja aparcada.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ConquestTactics.Mundo.Mapa;
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
        [UnityTest, Timeout(900_000)]
        public IEnumerator TuEjercitoConCaravanas()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza");
            string nick = null;
            yield return FundarPlaza(url, x => nick = x, mercadoEnPie: true);
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
            var yo = conexion.HeroeId;
            var plazaId = s.Almacen.Buscar(V1.TipoEntidad.Heroe, yo).Heroe.Ubicacion.AsentamientoId;
            V1.Asentamiento Plaza() => s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId)?.Asentamiento;
            yield return ConViaRapidaHasta(s, plazaId, yo, () => Plaza()?.Edificios.Any(e => e.Tipo == "mercado" && e.Estado == V1.EstadoEdificio.EdificioActivo) == true, 60, bombear: false);
            Assume.That(Plaza().Edificios.Any(e => e.Tipo == "mercado" && e.Estado == V1.EstadoEdificio.EdificioActivo), "el Mercado en pie");
            // Una caravana comercial de la plaza (la flota se prueba en TuFlota) y tropa en el campamento para el ejército.
            {
                var r = s.Ordenar(new V1.Orden { CrearCaravana = new V1.OrdenCrearCaravana { AsentamientoId = plazaId } });
                yield return Esperar(() => r.IsCompleted, 10, "la caravana creada");
                Assert.AreEqual(V1.Resultado.OrdenAceptada, r.Result.Resultado, "crear la caravana: " + r.Result.Rechazo?.Codigo);
            }
            V1.Caravana Caravana() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Caravana).Select(e => e.Caravana)
                .FirstOrDefault(x => x.Tipo == V1.TipoCaravana.CaravanaComercial && x.OrigenAsentamientoId == plazaId);
            yield return Esperar(() => Caravana() != null, 10, "la caravana en la sesión");
            var kid = Caravana().Id;
            // Un carro con su buey, para que quepa algo (una caravana sin carros no lleva nada: `caravana.sin_capacidad`).
            foreach (var o in new[]
            {
                new V1.Orden { AgregarCarroCaravana = new V1.OrdenAgregarCarroCaravana { CaravanaId = kid, TipoCarro = "basico" } },
                new V1.Orden { ComprarAnimalCaravana = new V1.OrdenComprarAnimalCaravana { CaravanaId = kid, CarroIndice = 0, TipoAnimal = "buey" } },
            })
            {
                var r = s.Ordenar(o);
                yield return Esperar(() => r.IsCompleted, 10, o.CuerpoCase.ToString());
                Assert.AreEqual(V1.Resultado.OrdenAceptada, r.Result.Resultado, $"{o.CuerpoCase}: {r.Result.Rechazo?.Codigo} {r.Result.Rechazo?.Mensaje}");
            }
            yield return Esperar(() => Caravana()?.Carros.Count == 1 && Caravana().Carros[0].HasAnimal, 10, "el carro con su buey");
            var escuadra = new Google.Protobuf.WellKnownTypes.Struct();
            escuadra.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(yo);
            escuadra.Fields["tropa_id"] = Google.Protobuf.WellKnownTypes.Value.ForString("lenadores");
            escuadra.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(30);
            yield return Administrar(url, "dar_escuadra", escuadra);
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
            GameObject sistema = null;
            IEnumerator Clic(VisualElement e) => ClicEnVentana(raton, raiz, e);
            VisualElement Salida() => raiz.Q("ventana-salida");
            VisualElement Ventana() => raiz.Q("ventana-ejercito");
            V1.Ejercito MiCol() => ElMapaSiHay()?.Columna;
            try
            {
                // «Salir al mundo» → «Ejército» → convocar: en la preparación, la caravana de la plaza con «Se queda aquí» o «Llevárnosla».
                yield return Clic(raiz.Q<Button>("salir-al-mundo"));
                yield return Esperar(() => Salida()?.Q<RadioButton>("ejercito") != null, 5, "«Ejército» en «Cómo sales»");
                yield return Clic(Salida().Q<RadioButton>("ejercito"));
                yield return Esperar(() => Salida().Q<Button>("convocar")?.resolvedStyle.display == DisplayStyle.Flex, 5, "«Convocar ejército»");
                yield return Clic(Salida().Q<Button>("convocar"));
                yield return Esperar(() => Salida()?.Q("conv-comercial-" + kid) != null, 10, "la caravana de la plaza en la preparación");
                Assert.IsTrue(Salida().Q<Button>("conv-comercial-queda-" + kid).ClassListContains("elegida"), "de entrada, se queda");
                yield return Clic(Salida().Q<Button>("conv-comercial-llevar-" + kid));
                yield return Esperar(() => Salida()?.Q<Button>("conv-comercial-llevar-" + kid)?.ClassListContains("elegida") == true, 5, "«Llevárnosla» elegida");
                yield return Capturar("180_preparacion_caravanas");
                yield return EnTamanos(raiz, "180_preparacion_caravanas", () => DentroDelMarco(Salida().Q(className: "placa"), "la preparación con la caravana"));

                // Sale con el ejército: la caravana se engancha.
                yield return Clic(Salida().Q<Button>("conv-partir"));
                yield return Esperar(() => raiz.Q("mapa") != null && MiCol() != null, 30, "el mapa con el ejército");
                yield return Esperar(() => MiCol()?.CaravanasAdjuntasIds.Contains(kid) == true, 10, "la caravana enganchada al ejército");
                Assert.AreEqual(V1.TipoColumna.ColumnaEjercito, MiCol().Tipo, "un ejército");
                // Solo la escena, para las capturas del prototipo (`tasks/pruebas/cdp/ejercito-caravanas.mjs`): el ejército con su caravana a la puerta.
                if (Environment.GetEnvironmentVariable("MUNDO_PROTO") == "caravanas")
                {
                    var pl = Plaza().Posicion;
                    Debug.Log($"[Prueba] escena del prototipo: yo={nick} caravana={kid} x={pl.X / 1000.0:0.###} y={pl.Y / 1000.0:0.###}");
                    yield break;
                }
                sistema = new GameObject("EventSystem de la prueba", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

                // La ventana ⚑: la caravana, con «Cargar de …» (a la puerta de su plaza). Carga lo que propone.
                yield return Esperar(() => raiz.Q<Button>("riel-ejercito") != null && !raiz.Q("mapa-riel").ClassListContains("oculto"), 10, "⚑ en el mapa");
                yield return Clic(raiz.Q<Button>("riel-ejercito"));
                yield return Esperar(() => Ventana()?.Q("ej-caravana-" + kid) != null, 5, "la caravana en la ventana Ejército");
                yield return Esperar(() => Ventana()?.Q<Button>("ej-cargar-" + kid) != null, 10, "«Cargar» a la puerta de la plaza");
                yield return Clic(Ventana().Q<Button>("ej-cargar-" + kid));
                yield return Esperar(() => (s.Almacen.Buscar(V1.TipoEntidad.Caravana, kid)?.Caravana?.Contenido?.PorId.Values.Sum() ?? 0) > 0, 10, "la caravana cargada en el motor");
                var error = Ventana()?.Q<Label>("error-ejercito");
                Assert.IsTrue(error == null || error.ClassListContains("oculto"), "sin rechazo: " + error?.text);
                yield return Esperar(() => Ventana()?.Q("ej-caravana-carga-" + kid)?.childCount > 0, 5, "su carga en la ventana");
                yield return Capturar("181_ventana_caravanas");
                yield return EnTamanos(raiz, "181_ventana_caravanas", () => DentroDelMarco(Ventana(), "la ventana Ejército con la caravana"));

                // El carro 3D, detrás del ejército.
                yield return Clic(Ventana().Q<Button>("ejercito-cerrar"));
                var mapa = ElMapa();
                yield return Esperar(() => GameObject.Find("Caravana " + kid) != null, 10, "el carro 3D de la caravana");
                var carro = GameObject.Find("Caravana " + kid).transform.position;
                var (cx, cy) = mapa.Centro.Value;
                var ejercito = EscalaDelMapa.AEscena(cx, cy, 0);
                var d = Vector2.Distance(new Vector2(carro.x, carro.z), new Vector2(ejercito.x, ejercito.z)) / EscalaDelMapa.MetrosPorUnidad;
                Assert.That(d, Is.InRange(0.2f, 1.5f), "el carro, pegado detrás del ejército (u)");
                yield return CapturarDeCerca(GameObject.Find("Caravana " + kid), "182_carro_detras_del_ejercito");

                // Entra con el ejército en la plaza: en su lateral, «Aparcar aquí» la deja aparcada.
                UnityEngine.Object.Destroy(sistema);
                sistema = null;
                var plaza = mapa.Cosas.First(x => x.Clase == ClaseDeCosa.Plaza && x.Id == plazaId);
                mapa.Seleccionar(plaza);
                yield return Esperar(() => raiz.Q("ficha-acciones")?.Query<Button>().ToList().Any(b => TextoDe(b) == "Entrar con el ejército") == true, 10, "«Entrar con el ejército» en la ficha de la plaza");
                Pulsar(raiz.Q("ficha-acciones").Query<Button>().ToList().First(b => TextoDe(b) == "Entrar con el ejército"));
                yield return Esperar(() => raiz.Q("plaza") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "dentro de la plaza con el ejército");
                yield return Esperar(() => raiz.Q("plaza").Q<Button>("aparcar-" + kid) != null, 15, "«Aparcar aquí» en el lateral");
                yield return Capturar("183_aparcar_en_la_plaza");
                yield return EnTamanos(raiz, "183_aparcar_en_la_plaza", () => Assert.IsTrue(Contiene(raiz.worldBound, raiz.Q("plaza").Q("caravanas-plaza").worldBound), "la tarjeta, entera en pantalla"));
                yield return Clic(raiz.Q("plaza").Q<Button>("aparcar-" + kid));
                yield return Esperar(() => s.Almacen.Buscar(V1.TipoEntidad.Caravana, kid)?.Caravana?.Estado == V1.EstadoCaravana.CaravanaAparcada, 10, "la caravana aparcada en el motor");
                yield return Esperar(() => raiz.Q("plaza").Q("caravanas-plaza") == null, 5, "sin la tarjeta, ya aparcada");
            }
            finally
            {
                if (sistema != null) UnityEngine.Object.Destroy(sistema);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                foreach (var m in otros) if (m.added) UnityEngine.InputSystem.InputSystem.EnableDevice(m);
                entrada.backgroundBehavior = fondo;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
