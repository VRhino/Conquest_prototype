// El mapa estratégico 2D (paso 9 del bloque 4, tanda 1), con el ratón y el teclado de verdad contra el motor. Desde dentro de tu plaza: el botón
// «Mapa» de la barra lo abre centrado en ella, la rueda y los botones +/− acercan y alejan, arrastrar mueve la vista, un clic en el suelo no hace
// nada (solo para mirar) y Esc lo cierra. Fuera, en el mundo con la columna: un clic en el suelo la pone en marcha y la ruta sale en el mapa
// 2D, que sigue abierto; un clic sobre la plaza cierra el mapa, la elige y sale su ficha.
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
        [UnityTest, Timeout(900_000)]
        public IEnumerator TuMapaEstrategico()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza");
            string nick = null;
            yield return FundarPlaza(url, x => nick = x);
            foreach (var k in Preferencias) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.DeleteKey("mundo.mapa2d.fertilidad");
            PlayerPrefs.SetInt("mundo.capa.nodos", 1);
            go = new GameObject("Mundo");
            go.SetActive(false);
            var conexion = go.AddComponent<ConexionDeMundo>();
            typeof(ConexionDeMundo).GetField("servidor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(conexion, url);
            var pantallas = go.AddComponent<PantallasDeMundo>();
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
            yield return Quieto(1.5f);
            V1.Heroe Yo() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            var plazaId = Yo().Ubicacion.AsentamientoId;
            var plaza = conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId).Asentamiento;

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
            GameObject sistema = null;
            IEnumerator Tecla(UnityEngine.InputSystem.Key k)
            {
                teclado.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
                yield return null;
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
            }
            IEnumerator Clic(VisualElement e) => ClicEnVentana(raton, raiz, e);
            MapaEstrategico Mapa2D() => pantallas.Mapa2D;
            // Un punto del panel (px del panel) a la pantalla, con la textura de la prueba quitada (el panel, a pantalla).
            Vector2 APantalla(Vector2 p) => new Vector2(p.x * Screen.width / raiz.worldBound.width, Screen.height - p.y * Screen.height / raiz.worldBound.height);
            IEnumerator ConRaton(Func<IEnumerator> hacer)
            {
                var textura = doc.panelSettings.targetTexture;
                try
                {
                    doc.panelSettings.targetTexture = null;
                    for (int i = 0; i < 8; i++) yield return null;
                    yield return hacer();
                }
                finally { if (doc != null) doc.panelSettings.targetTexture = textura; }
            }
            IEnumerator Mover(Vector2 donde, bool pulsado)
            {
                var s = new UnityEngine.InputSystem.LowLevel.MouseState { position = donde };
                if (pulsado) s = s.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, s);
                yield return null;
                yield return null;
            }
            // Un clic del ratón sobre el punto del mundo (x, y) del mapa 2D.
            IEnumerator ClicEnElMundo(double x, double y) => ConRaton(() => ClicEn(Mapa2D().ALienzo(x, y)));
            IEnumerator ClicEn(Vector2 local)
            {
                var donde = APantalla(Mapa2D().Q("mapa-lienzo")?.LocalToWorld(local) ?? Mapa2D().LocalToWorld(local));
                yield return Mover(donde, false);
                yield return PantallasDeMundoTests.Clic(raton, donde);
            }
            try
            {
                // Desde la plaza: el botón «Mapa» de la barra lo abre, centrado en la plaza, a un cuarto de la escala de visión.
                yield return Clic(raiz.Q<Button>("mapa-2d"));
                yield return Esperar(() => Mapa2D() != null && Mapa2D().Cosas.Count > 0, 10, "el mapa 2D abierto");
                yield return Quieto(0.5f);
                var c0 = Mapa2D().Camara;
                Assert.AreEqual(plaza.Posicion.X / 1000.0, c0.Cx, 1.0, "centrado en la plaza");
                Assert.IsNotNull(Mapa2D().Q("mapa-plaza-" + plazaId), "tu plaza con su sigilo");
                Assert.IsTrue(Mapa2D().Q("mapa-iconos-bajo").Query(className: "mapa-cosa").ToList().Count > 0, "los nodos, bajo la niebla");
                Assert.IsTrue(Mapa2D().Query<Label>(className: "mapa-cosa-nombre").ToList().Any(l => l.text == "Piedra"), "de cerca, los nodos con su nombre");
                yield return Capturar("110_mapa_2d");
                yield return EnTamanos(raiz, "110_mapa_2d");

                // La rueda aleja hacia el cursor; el botón «+» acerca ×1,4.
                var lienzo = Mapa2D().Q("mapa-lienzo");
                yield return ConRaton(() => RuedaEn(APantalla(lienzo.worldBound.center), -120, 6));
                Assert.Less(Mapa2D().Camara.Escala, c0.Escala * 0.6, "la rueda aleja");
                yield return Capturar("111_mapa_2d_lejos");
                var antes = Mapa2D().Camara.Escala;
                yield return Clic(Mapa2D().Q<Button>("zoom-mas"));
                Assert.AreEqual(antes * 1.4, Mapa2D().Camara.Escala, antes * 0.01, "«+» acerca ×1,4");
                yield return Clic(Mapa2D().Q<Button>("zoom-menos"));
                Assert.AreEqual(antes, Mapa2D().Camara.Escala, antes * 0.01, "«−» aleja lo mismo");

                // Arrastrar mueve la vista (el mundo sigue al puntero).
                var cx = Mapa2D().Camara.Cx;
                yield return ConRaton(() => Arrastrar(APantalla(lienzo.worldBound.center), APantalla(lienzo.worldBound.center + new Vector2(200, 0))));
                Assert.Less(Mapa2D().Camara.Cx, cx - 1, "arrastrar a la derecha lleva la vista a la izquierda");

                // Hasta la escala de visión, otra vez sobre la plaza (cerrar y abrir lo centra): el suelo de cerca, con sus árboles.
                yield return Clic(Mapa2D().Q<Button>("mapa-cerrar"));
                yield return Esperar(() => Mapa2D() == null, 5, "✕ cierra el mapa 2D");
                yield return Clic(raiz.Q<Button>("mapa-2d"));
                yield return Esperar(() => Mapa2D() != null && Mapa2D().Cosas.Count > 0, 10, "el mapa 2D otra vez");
                for (int i = 0; i < 8; i++) yield return Clic(Mapa2D().Q<Button>("zoom-mas"));
                yield return Quieto(0.5f);
                yield return Capturar("112_mapa_2d_cerca");

                // Desde dentro de la plaza, solo para mirar: un clic en el suelo no mueve a nadie. Esc cierra.
                yield return ClicEnElMundo(Mapa2D().Camara.Cx + 3, Mapa2D().Camara.Cy + 3);
                Assert.IsNotNull(Mapa2D(), "sigue abierto");
                Assert.AreEqual(V1.UbicacionHeroe.DondeOneofCase.AsentamientoId, Yo().Ubicacion.DondeCase, "sigue en la plaza");
                yield return Tecla(UnityEngine.InputSystem.Key.Escape);
                yield return Esperar(() => Mapa2D() == null, 5, "Esc cierra el mapa 2D");

                // Al mundo: «Salir al mundo» con lo que haya.
                Pulsar(raiz.Q<Button>("salir-al-mundo"));
                yield return Esperar(() => raiz.Q("ventana-salida") != null, 5, "la ventana de salida");
                yield return Clic(raiz.Q("ventana-salida").Q<Button>("salir"));
                yield return Esperar(() => raiz.Q("mapa") != null && ElMapa()?.Columna != null, 30, "el mapa con la columna");
                sistema = new GameObject("EventSystem de la prueba", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                yield return null;
                yield return Clic(raiz.Q<Button>("mapa-2d"));
                yield return Esperar(() => Mapa2D() != null && Mapa2D().Cosas.Count > 0, 10, "el mapa 2D en el mundo");
                Assert.IsTrue(ElMapa().EntradaApagada, "con el mapa 2D, el 3D no se mueve");
                yield return Quieto(0.5f);

                // Un clic en el suelo, a 12 u al este: la columna marcha y la ruta sale en el mapa 2D, que sigue abierto.
                var (hx, hy) = Mapa2D().PosicionDelHeroe().Value;
                yield return ClicEnElMundo(hx + 12, hy);
                yield return Esperar(() => ElMapa().Columna?.Estado == V1.EstadoColumna.ColumnaMarchando, 10, "la columna en marcha");
                yield return Esperar(() => Mapa2D()?.PuntosDeRuta > 1, 5, "la ruta en el mapa 2D");
                Assert.IsNotNull(Mapa2D(), "el mapa sigue abierto");
                yield return Capturar("113_mapa_2d_marchando");
                yield return EnTamanos(raiz, "113_mapa_2d_marchando");

                // Un clic sobre la plaza: se cierra el mapa, queda elegida y sale su ficha.
                yield return ClicEnElMundo(plaza.Posicion.X / 1000.0, plaza.Posicion.Y / 1000.0);
                yield return Esperar(() => Mapa2D() == null, 5, "cerrado al elegir");
                yield return Esperar(() => ElMapa().Seleccionada?.Id == plazaId, 5, "la plaza elegida");
                yield return Capturar("114_plaza_elegida_desde_el_mapa_2d");
            }
            finally
            {
                if (sistema != null) UnityEngine.Object.Destroy(sistema);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(teclado);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                foreach (var m in otros) if (m.added) UnityEngine.InputSystem.InputSystem.EnableDevice(m);
                entrada.backgroundBehavior = fondo;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
                LogAssert.ignoreFailingMessages = false;
            }

            IEnumerator RuedaEn(Vector2 donde, float delta, int veces)
            {
                yield return Mover(donde, false);
                for (int i = 0; i < veces; i++)
                {
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde, scroll = new Vector2(0, delta) });
                    yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde });
                    yield return null;
                }
            }

            IEnumerator Arrastrar(Vector2 desde, Vector2 hasta)
            {
                yield return Mover(desde, false);
                yield return Mover(desde, true);
                for (int i = 1; i <= 8; i++) yield return Mover(Vector2.Lerp(desde, hasta, i / 8f), true);
                yield return Mover(hasta, false);
            }
        }
    }
}
