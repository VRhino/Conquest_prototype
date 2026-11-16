// Las misiones (paso 12 del bloque 4, tanda 3), con el ratón de verdad contra el motor. El Rey y Gobernador funda su plaza y entra: el rastreador
// sale arriba en la columna derecha de la plaza con sus misiones seguidas; el botón «Misiones» de la barra abre el diario; reclama la misión de
// Facción cumplida al fundar; en «Tablón de …» acepta una individual y elige las de la semana que viene; en Facción › Misiones y › Tablón, lo de la
// Facción.
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
        public IEnumerator TusMisiones()
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
            if (Environment.GetEnvironmentVariable("MUNDO_PROTO") == "misiones")
            {
                Debug.Log($"[Prueba] escena del prototipo: yo={nick} plaza={plazaId}");
                LogAssert.ignoreFailingMessages = false;
                yield break;
            }
            yield return Quieto(1f);
            Debug.Log($"[Prueba] misiones: héroe {string.Join(",", Yo().Misiones.Select(m => $"{m.Id}:{m.Estado}"))}; plaza {string.Join(",", Plaza().Misiones.Select(m => $"{m.Id}:{m.Estado}"))}; Facción {string.Join(",", Faccion()?.Misiones.Select(m => $"{m.Id}:{m.Estado}") ?? new string[0])}; tablón plaza {Plaza().Tablon?.Misiones.Count}; tablón Facción {Faccion()?.Tablon?.Misiones.Count}");

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
            IEnumerator Clic(VisualElement e) => ClicEnVentana(raton, raiz, e);
            IEnumerator Tecla(UnityEngine.InputSystem.Key k)
            {
                teclado.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
                yield return null;
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
            }
            VisualElement Diario() => raiz.Q("misiones-ventana");
            VisualElement Faccionv() => raiz.Q("faccion-ventana");
            try
            {
                // El rastreador: arriba, al principio de la columna derecha de la plaza, con las misiones del héroe seguidas solas.
                yield return Esperar(() => raiz.Q("plaza-der")?.Q("rastreador") != null, 10, "el rastreador en la columna derecha");
                Assert.AreEqual(0, raiz.Q("plaza-der").IndexOf(raiz.Q("rastreador")), "encima de lo demás de la columna");
                yield return Capturar("210_rastreador_plaza");
                yield return EnTamanos(raiz, "210_rastreador_plaza", () => DentroDelMarco(raiz.Q("rastreador"), "el rastreador"));

                // El botón «Misiones» de la barra, tras la bandera: abre el diario.
                var boton = raiz.Q<Button>("misiones");
                Assert.IsNotNull(boton, "el botón Misiones en la barra");
                Assert.AreEqual(boton.parent.IndexOf(raiz.Q<Button>("faccion")) + 1, boton.parent.IndexOf(boton), "tras la bandera");
                yield return ClicFijo(raton, raiz, boton);
                yield return Esperar(() => Diario()?.Q("diario-detalle") != null, 5, "el diario");
                yield return Capturar("211_diario");
                yield return EnTamanos(raiz, "211_diario", () => DentroDelMarco(Diario().Q("diario"), "el diario"));

                // La misión de Facción cumplida al fundar: se abre y se reclama.
                var cumplida = Faccion()?.Misiones.FirstOrDefault(m => m.Estado == V1.EstadoMision.MisionCumplida);
                if (cumplida != null)
                {
                    yield return Clic(Diario().Q<Button>($"diario-item-faccion-{cumplida.Id}"));
                    yield return Esperar(() => Diario()?.Q<Button>("mision-reclamar") != null, 5, "«Reclamar»");
                    yield return Clic(Diario().Q<Button>("mision-reclamar"));
                    yield return Esperar(() => Faccion().Misiones.First(m => m.Id == cumplida.Id).Estado == V1.EstadoMision.MisionReclamada, 10, "la misión reclamada en el motor");
                }
                else Debug.Log("[Prueba] misiones: ninguna de Facción cumplida que reclamar");

                // Seguir: con el rastreador lleno (3) no sale «Seguir» (solo lo que se puede hacer); se deja de seguir una tuya y se sigue la primera
                // de la plaza, que entra en el rastreador.
                var dePlaza = Plaza().Misiones.FirstOrDefault(m => m.Estado == V1.EstadoMision.MisionActiva);
                var seguidaMia = Yo().Misiones.First(m => m.Estado != V1.EstadoMision.MisionReclamada);
                yield return Clic(Diario().Q<Button>($"diario-item-heroe-{seguidaMia.Id}"));
                yield return Esperar(() => Diario()?.Q<Button>("mision-seguir")?.text == "Dejar de seguir", 5, "«Dejar de seguir»");
                if (dePlaza != null)
                {
                    yield return Clic(Diario().Q<Button>($"diario-item-plaza-{dePlaza.Id}"));
                    yield return Esperar(() => Diario()?.Q("diario-detalle") != null && Diario().Q<Button>("mision-seguir") == null, 5, "sin «Seguir» con el rastreador lleno");
                    yield return Clic(Diario().Q<Button>($"diario-item-heroe-{seguidaMia.Id}"));
                    yield return Clic(Diario().Q<Button>("mision-seguir"));
                    yield return Clic(Diario().Q<Button>($"diario-item-plaza-{dePlaza.Id}"));
                    yield return Esperar(() => Diario()?.Q<Button>("mision-seguir")?.text == "Seguir", 5, "«Seguir» con hueco");
                    yield return Clic(Diario().Q<Button>("mision-seguir"));
                    yield return Esperar(() => Diario()?.Q<Button>("mision-seguir")?.text == "Dejar de seguir", 5, "seguida");
                    yield return Esperar(() => raiz.Q("rastreador")?.Q<Button>($"rastreador-plaza-{dePlaza.Id}") != null, 5, "en el rastreador");
                }

                // El tablón de la plaza: aceptar una individual y elegir las de la semana que viene.
                yield return Esperar(() => Plaza().Tablon != null, 10, "el tablón de la plaza");
                yield return Clic(Diario().Q<Button>("diario-vista-tablon"));
                yield return Esperar(() => Diario()?.Q("tablon-diarias") != null, 5, "el tablón en el diario");
                var aceptable = Plaza().Tablon.Misiones.FirstOrDefault(m => m.Definicion.Tipo == V1.TipoMision.MisionIndividual && !m.Definicion.HasCargo && m.HastaMs > s.AhoraMundo);
                yield return Capturar("212_tablon_plaza");
                yield return EnTamanos(raiz, "212_tablon_plaza", () => DentroDelMarco(Diario().Q("diario"), "el tablón de la plaza"));
                if (aceptable != null)
                {
                    var b = Diario().Q<Button>("tablon-aceptar-" + aceptable.Definicion.Id);
                    yield return Clic(b);
                    yield return Esperar(() => Plaza().Tablon.Misiones.First(m => m.Definicion.Id == aceptable.Definicion.Id).Aceptaciones.Any(a => a.HeroeId == conexion.HeroeId), 10, "aceptada en el motor");
                }
                else Debug.Log("[Prueba] misiones: ninguna individual que aceptar");
                var elegir = Diario().Q<Foldout>("tablon-elegir");
                Assert.IsNotNull(elegir, "«Elegir las de la semana que viene» (el Gobernador)");
                yield return Clic(elegir.Q<Toggle>(className: "unity-foldout__toggle"));
                yield return Esperar(() => Diario()?.Q<Button>("tablon-guardar")?.resolvedStyle.display == DisplayStyle.Flex, 5, "la elección abierta");
                var opcion = Diario().Q<Foldout>("tablon-elegir").Query<Toggle>(className: "tablon-opcion").ToList().FirstOrDefault(t => !t.value && t.enabledSelf);
                if (opcion != null)
                {
                    var nombre = opcion.name;
                    yield return Clic(opcion);
                    yield return Esperar(() => Diario()?.Q<Toggle>(nombre)?.value == true, 5, "marcada");
                }
                yield return Capturar("213_elegir_tablon");
                yield return Clic(Diario().Q<Button>("tablon-guardar"));
                yield return Esperar(() => Plaza().Tablon.ConEleccion, 10, "la elección en el motor");
                yield return Tecla(UnityEngine.InputSystem.Key.Escape);
                yield return Esperar(() => Diario() == null, 5, "Esc cierra el diario");

                // Facción › Misiones y › Tablón.
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("faccion"));
                yield return Esperar(() => Faccionv()?.Q<Button>("pestana-faccion-misiones") != null, 5, "la pestaña Misiones de la Facción");
                yield return Clic(Faccionv().Q<Button>("pestana-faccion-misiones"));
                yield return Esperar(() => Faccionv()?.Q("misiones-cartas") != null || Faccionv()?.Q("misiones-hechas") != null, 5, "las misiones de la Facción");
                yield return Capturar("214_faccion_misiones");
                yield return EnTamanos(raiz, "214_faccion_misiones", () => DentroDelMarco(Faccionv(), "Facción › Misiones"));
                yield return Clic(Faccionv().Q<Button>("pestana-faccion-tablon"));
                yield return Esperar(() => Faccionv()?.Q("tablon-diarias") != null || Faccionv()?.Q<Label>(className: "campo-ayuda") != null, 5, "el tablón de la Facción");
                yield return Capturar("215_faccion_tablon");
                yield return EnTamanos(raiz, "215_faccion_tablon", () => DentroDelMarco(Faccionv(), "Facción › Tablón"));
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(teclado);
                foreach (var m in otros) if (m.added) UnityEngine.InputSystem.InputSystem.EnableDevice(m);
                entrada.backgroundBehavior = fondo;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
