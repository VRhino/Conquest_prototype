// La tecnología de tu Facción (paso 8 del bloque 4, tanda 3), con el ratón y el teclado de verdad contra el motor. El Rey funda su plaza (la
// capital, con la Vía Rápida) y la administración hace aparecer los Escudos ligeros sin adoptarlos. En el árbol de tropas de la plaza, los Lanceros
// de mimbre salen «adoptables» con la etiqueta «Adoptar», que abre la pestaña Tecnología de la Facción con la ficha fija de esa tecnología. Allí:
// Esc cierra la ficha, la ficha corta al pasar el ratón, «Ver todas las conexiones», el tablero que se arrastra y, con la ficha otra vez, «Adoptar»
// con la tarifa de su Era del almacén de la capital.
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
        public IEnumerator TuTecnologiaDelRey()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza y fijar las tecnologías");
            const string tec = "escudos_ligeros", tropa = "lanceros_mimbre";
            string nick = null;
            yield return FundarPlaza(url, x => nick = x);
            foreach (var k in Preferencias) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.DeleteKey("mundo.plaza.barra");
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
            yield return new WaitForSecondsRealtime(1.5f);
            V1.Heroe Yo() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            var plazaId = Yo().Ubicacion.AsentamientoId;
            var faccionId = Yo().FaccionId;
            V1.Faccion Faccion() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Faccion, faccionId)?.Faccion;
            V1.TecnologiasFaccion Tecs() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.TecnologiaFaccion, faccionId)?.Tecnologias;

            // La plaza, capital de la Facción (la primera no lo es sola).
            if (!Faccion().HasCapitalId || Faccion().CapitalId != plazaId)
            {
                var designar = conexion.Sesion.Ordenar(new V1.Orden { DesignarCapital = new V1.OrdenDesignarCapital { FaccionId = faccionId, AsentamientoId = plazaId } });
                yield return Esperar(() => designar.IsCompleted, 20, "designar la capital");
                Assert.AreEqual(V1.Resultado.OrdenAceptada, designar.Result.Resultado, designar.Result.Rechazo?.Codigo);
                yield return Esperar(() => Faccion().HasCapitalId && Faccion().CapitalId == plazaId, 10, "la capital");
            }
            // Los Escudos ligeros, aparecidos y sin adoptar: `fijar_tecnologias` los da como aparecidos y adoptados; otra vez sin ellos, quedan solo aparecidos.
            foreach (var lista in new[] { new[] { tec }, new string[0] })
            {
                var q = new Google.Protobuf.WellKnownTypes.Struct();
                q.Fields["faccion_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(faccionId);
                q.Fields["tecnologias"] = Google.Protobuf.WellKnownTypes.Value.ForList(lista.Select(Google.Protobuf.WellKnownTypes.Value.ForString).ToArray());
                yield return Administrar(url, "fijar_tecnologias", q);
            }
            yield return Esperar(() => Tecs() != null && Tecs().Aparecidas.Contains(tec) && !Tecs().Adoptadas.Contains(tec), 10, "los Escudos ligeros aparecidos sin adoptar");

            var entrada = UnityEngine.InputSystem.InputSystem.settings;
            var fondo = entrada.backgroundBehavior;
            var editorAntes = entrada.editorInputBehaviorInPlayMode;
            entrada.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            entrada.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var teclado = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            var otros = UnityEngine.InputSystem.InputSystem.devices.OfType<UnityEngine.InputSystem.Mouse>().Where(m => m.enabled).ToList();
            foreach (var m in otros) UnityEngine.InputSystem.InputSystem.DisableDevice(m);
            var raton = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            raton.MakeCurrent();
            IEnumerator Tecla(UnityEngine.InputSystem.Key k)
            {
                teclado.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
                yield return null;
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
            }
            VisualElement Ventana() => raiz.Q("faccion-ventana");
            VisualElement Tip() => Ventana()?.Q("tec-tip");
            bool TipVisible() => Tip() != null && Tip().resolvedStyle.display == DisplayStyle.Flex;
            try
            {
                // El árbol de tropas de la plaza: los Lanceros de mimbre, «adoptables», con la etiqueta «Adoptar».
                var plaza = raiz.Q("plaza");
                yield return ClicFijo(raton, raiz, plaza.Q("riel").Q<Button>("riel-reclutamiento"));
                yield return Esperar(() => raiz.Q("panel-tropa")?.Q("recl-tablero") != null, 10, "el árbol de la plaza");
                var carta = raiz.Q("panel-tropa").Q<Button>("nodo-" + tropa);
                Assert.IsTrue(carta.ClassListContains("adoptable"), "los Lanceros de mimbre, adoptables");
                Assert.AreEqual("!", carta.Q<Label>(className: "recl-medalla")?.text, "la medalla «!»");
                var etiqueta = carta.Q<Button>("adoptar-" + tropa);
                Assert.IsNotNull(etiqueta, "la etiqueta «Adoptar»");
                yield return MoverRaton(raton, raiz, carta, () => raiz.Q("recl-tip")?.resolvedStyle.display == DisplayStyle.Flex);
                Assert.IsTrue(raiz.Q("recl-tip").Query<Label>().ToList().Any(l => l.text.StartsWith("Solo falta adoptar")), "la frase del prototipo en la ficha corta");
                Assert.IsTrue(raiz.Q("recl-tip").Query<Label>().ToList().Any(l => l.text == "TECNOLOGÍAS DE TU FACCIÓN"), "las tecnologías de tu Facción, marcadas");
                // Lo que le faltaba a la ficha corta: «Edificios y pueblos», «Por soldado», «En campaña» y «Combate», con sus iconos.
                var fichaCorta = raiz.Q("recl-tip");
                var secciones = fichaCorta.Query<Label>(className: "recl-tip-seccion").ToList().Select(l => l.text).ToList();
                foreach (var s in new[] { "EDIFICIOS Y PUEBLOS", "POR SOLDADO", "EN CAMPAÑA", "COMBATE" }) CollectionAssert.Contains(secciones, s, "la sección " + s);
                Assert.IsNotNull(fichaCorta.Q("edificios-y-pueblos"), "las fichas de edificios");
                Assert.IsNotNull(fichaCorta.Q("en-campana"), "poder, velocidad y liderazgo");
                Assert.IsNotNull(fichaCorta.Q("combate"), "vida, cadencia y alcance");
                Assert.IsFalse(fichaCorta.Query<Label>().ToList().Any(l => l.text.Contains("ArmaMadera") || l.text.Contains("armaMadera")), "sin ids de recurso a la vista");
                Debug.Log("[Prueba] ficha de tropa: " + string.Join(" | ", secciones) + "; tabla de combate: " + (fichaCorta.Q("tabla-combate") != null));
                yield return EnTamanos(raiz, "102_tropa_adoptable");
                yield return ClicEnVentana(raton, raiz, etiqueta);

                // La pestaña Tecnología, con la ficha fija de los Escudos ligeros.
                yield return Esperar(() => raiz.Q("panel-tropa") == null && Ventana()?.Q("tec-panel") != null, 10, "la pestaña Tecnología");
                CollectionAssert.AreEqual(new[] { "pestana-faccion-miembros", "pestana-faccion-plazas", "pestana-faccion-tecnologia", "pestana-faccion-diplomacia", "pestana-faccion-ajustes" },
                    Ventana().Q("faccion-pestanas").Query<Button>().ToList().Select(b => b.name).ToArray(), "las pestañas que ya hay");
                Assert.IsTrue(Ventana().Q<Button>("pestana-faccion-tecnologia").ClassListContains("activo"), "Tecnología, la elegida");
                yield return Esperar(() => TipVisible() && Tip().ClassListContains("fijo") && Tip().Q<Button>("adoptar-" + tec) != null, 10, "la ficha fija con «Adoptar»");
                var cartaTec = Ventana().Q<Button>("tec-" + tec);
                Assert.IsTrue(cartaTec.ClassListContains("adoptable"), "la carta, adoptable");
                Debug.Log("[Prueba] tecnología: " + string.Join(", ", Ventana().Query<Button>(className: "tec-carta").ToList().Select(b => $"{b.name.Substring(4)}({string.Join(" ", b.GetClasses().Where(x => x != "tec-carta" && x != "unity-button" && x != "unity-text-element"))})")));
                Debug.Log("[Prueba] tecnología, eras: " + string.Join(" | ", Ventana().Query(className: "tec-era-cab").ToList().Select(e => string.Join(" · ", e.Query<Label>().ToList().Select(l => l.text)))));
                yield return EnTamanos(raiz, "103_tecnologia_ficha", () => DentroDelMarco(Ventana(), "la Tecnología"));

                // Esc cierra la ficha (no la ventana); al pasar el ratón por otra carta, su ficha corta.
                yield return Tecla(UnityEngine.InputSystem.Key.Escape);
                yield return Esperar(() => Ventana() != null && !TipVisible(), 5, "Esc cierra la ficha");
                // (la de más arriba: al cambiar de tamaño para mover el ratón de verdad, el tablero se recoloca y una de abajo puede quedar fuera)
                var otra = Ventana().Query<Button>(className: "tec-carta").ToList().Where(b => b.name != "tec-" + tec && b.ClassListContains("bloqueada"))
                    .OrderBy(b => b.layout.y).ThenBy(b => b.layout.x).First();
                var marcoTec = otra.GetFirstAncestorOfType<ScrollView>();
                marcoTec.scrollOffset = Vector2.zero;
                yield return null;
                yield return null;
                yield return MoverRaton(raton, raiz, otra, () => TipVisible() && !Tip().ClassListContains("fijo"));
                Assert.IsTrue(Tip().Query<Label>().ToList().Any(l => l.text == "REQUISITOS"), "los requisitos en la ficha corta");
                yield return Capturar("103b_tecnologia_tip");
                // «Ver todas las conexiones».
                yield return ClicEnVentana(raton, raiz, Ventana().Q<Button>("tec-ver-todas"));
                yield return Esperar(() => Ventana()?.Q<Button>("tec-ver-todas")?.ClassListContains("activo") == true, 5, "todas las conexiones");
                // El tablero se arrastra (si no cabe entero).
                var marco = Ventana().Q("tec-panel").Q<ScrollView>("recl-marco");
                if (marco.horizontalScroller.highValue > 1)
                {
                    var antes = marco.scrollOffset.x;
                    yield return ArrastrarUITK(raton, raiz, Ventana().Q("tec-panel").Q(className: "recl-esquina"), -200);
                    Assert.Greater(Ventana().Q("tec-panel").Q<ScrollView>("recl-marco").scrollOffset.x, antes + 50, "el tablero arrastrado");
                }
                else Debug.Log("[Prueba] tecnología: el tablero cabe entero, no se arrastra");

                // Adoptar: la ficha otra vez y «Adoptar» con la tarifa de la Era.
                yield return ClicEnVentana(raton, raiz, Ventana().Q<Button>("tec-" + tec));
                yield return Esperar(() => TipVisible() && Tip().ClassListContains("fijo") && Tip().Q<Button>("adoptar-" + tec) != null, 5, "la ficha fija otra vez");
                var adoptar = Tip().Q<Button>("adoptar-" + tec);
                Assert.IsTrue(adoptar.enabledSelf, "«Adoptar», con la tarifa en la capital");
                yield return ClicEnVentana(raton, raiz, adoptar);
                yield return Esperar(() => Tecs().Adoptadas.Contains(tec), 10, "adoptada en el motor");
                yield return Esperar(() => Ventana()?.Q<Button>("tec-" + tec)?.ClassListContains("adoptada") == true, 5, "la carta, adoptada");
                yield return Esperar(() => TipVisible() && Tip().Q<Button>("adoptar-" + tec) == null, 5, "la ficha, sin «Adoptar»");
                yield return Capturar("104_tecnologia_adoptada");
                Debug.Log($"[Prueba] tecnología: {tec} adoptada; adoptadas {string.Join(", ", Tecs().Adoptadas)}");
                yield return Tecla(UnityEngine.InputSystem.Key.Escape);
                yield return Esperar(() => Ventana() != null && !TipVisible(), 5, "Esc cierra la ficha");
                yield return Tecla(UnityEngine.InputSystem.Key.Escape);
                yield return Esperar(() => Ventana() == null, 5, "Esc cierra la ventana");

                // En el árbol de tropas, los Lanceros de mimbre ya no son «adoptables».
                yield return ClicFijo(raton, raiz, raiz.Q("plaza").Q("riel").Q<Button>("riel-reclutamiento"));
                yield return Esperar(() => raiz.Q("panel-tropa")?.Q<Button>("nodo-" + tropa) != null, 10, "el árbol de la plaza otra vez");
                Assert.IsFalse(raiz.Q("panel-tropa").Q<Button>("nodo-" + tropa).ClassListContains("adoptable"), "ya no adoptables");
                Debug.Log("[Prueba] tecnología: los Lanceros de mimbre, " + string.Join(" ", raiz.Q("panel-tropa").Q<Button>("nodo-" + tropa).GetClasses().Where(x => x != "recl-carta" && x != "unity-button" && x != "unity-text-element")));
                yield return Capturar("104b_tropa_tras_adoptar");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(teclado);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                foreach (var m in otros) if (m.added) UnityEngine.InputSystem.InputSystem.EnableDevice(m);
                entrada.backgroundBehavior = fondo;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
