// La Facción (paso 8 del bloque 4, tanda 1), con el ratón de verdad contra el motor. `TuFaccionSinFaccion`: un héroe sin Facción abre «Facción»
// desde la barra (la bandera), «Elige tu facción», pide ingreso a la Facción de otra cuenta (con su sigilo en la lista) y luego crea la suya con el
// selector de sigilo; el motor la tiene con ese sigilo y la ventana pasa a la tuya. `ElSelectorConElSigilo`: el selector de personaje enseña el escudo de su Facción.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ConquestTactics.Mundo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using V1 = BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class PantallasDeMundoTests
    {
        /// <summary>Una cuenta nueva con su héroe y su Facción, con el sigilo dado (en el motor: el sigilo es único por servidor).</summary>
        static IEnumerator CuentaConFaccion(string url, string nombreHeroe, string nombreFaccion, V1.OrdenCrearFaccion.Types.Sigilo sigilo, Action<string> nickDe, Action<string> faccionDe)
        {
            string nick = null;
            yield return OtroHeroe(url, nombreHeroe, nickDe: x => nick = x);
            nickDe?.Invoke(nick);
            var acceso = new Acceso(url);
            var login = acceso.Entrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => login.IsCompleted, 20, "el login");
            var s = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
            var abrir = s.Abrir();
            for (var f = Time.realtimeSinceStartup + 20; !abrir.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
            for (var f = Time.realtimeSinceStartup + 2; Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
            V1.ResultadoOrden r = null;
            foreach (var n in new[] { nombreFaccion, nombreFaccion + " II", nombreFaccion + " III" })
            {
                var orden = s.Ordenar(new V1.Orden { CrearFaccion = new V1.OrdenCrearFaccion { Nombre = n, Sigilo = sigilo } });
                for (var f = Time.realtimeSinceStartup + 20; !orden.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
                r = orden.Result;
                if (r.Resultado == V1.Resultado.OrdenAceptada) break;
            }
            Assert.AreEqual(V1.Resultado.OrdenAceptada, r.Resultado, r.Rechazo?.Codigo);
            faccionDe?.Invoke(r.Creadas.First(x => x.Entidad == V1.TipoEntidad.Faccion).Id);
            s.Cerrar();
        }

        /// <summary>Entra con la cuenta por la pantalla de acceso y el selector, hasta el interior de su campamento.</summary>
        IEnumerator EntrarAlCampamento(string url, string nick, Action<VisualElement, ConexionDeMundo> listo)
        {
            foreach (var k in Preferencias) PlayerPrefs.DeleteKey(k);
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
            listo(raiz, conexion);
        }

        static V1.OrdenCrearFaccion.Types.Sigilo SigiloDePrueba(string forma, string campo, string emblema, string c1, string c2) => new V1.OrdenCrearFaccion.Types.Sigilo
        {
            FormaId = forma, CampoId = campo, EmblemaId = emblema, ColorPrimarioId = c1, ColorSecundarioId = c2, ColorEmblemaId = "blanco", OrlaId = "lisa",
            ColorOrlaId = "oro",
        };

        [UnityTest, Timeout(600_000)]
        public IEnumerator TuFaccionSinFaccion()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            // La Facción de otra cuenta, a la que se pide ingreso (un sigilo al azar del catálogo: es único por servidor).
            TexturaDeSigilo.Asegurar();
            var azar = Sigilos.Aleatorio(new System.Random());
            string faccionAjena = null;
            yield return CuentaConFaccion(url, "Ajeno" + DateTime.UtcNow.Ticks % 100_000, "Esparta",
                SigiloDePrueba(azar.FormaId, azar.CampoId, azar.EmblemaId, azar.ColorPrimarioId, azar.ColorSecundarioId), null, x => faccionAjena = x);
            string nick = null;
            yield return OtroHeroe(url, "Sinfac" + DateTime.UtcNow.Ticks % 100_000, nickDe: x => nick = x);
            VisualElement raiz = null;
            ConexionDeMundo conexion = null;
            yield return EntrarAlCampamento(url, nick, (r, c) => { raiz = r; conexion = c; });
            LogAssert.ignoreFailingMessages = true; // el error de relleno del escuadrón vacío al nacer el héroe en FeudoScene
            UnityEngine.Object.FindAnyObjectByType<HeroSelectionSceneController>().confirmButton.onClick.Invoke();
            yield return Esperar(() => raiz.Q("interior") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior");
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            yield return new WaitForSecondsRealtime(1f);
            V1.Heroe Yo() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            V1.Faccion Faccion(string id) => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Faccion, id)?.Faccion;

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
            try
            {
                // La bandera de la barra, junto al yelmo: sin Facción, abre «Elige tu facción».
                yield return Esperar(() => raiz.Q<Button>("faccion")?.resolvedStyle.display == DisplayStyle.Flex, 5, "la bandera en la barra");
                Assert.AreEqual(raiz.Q<Button>("base").parent.IndexOf(raiz.Q<Button>("base")) + 1, raiz.Q<Button>("faccion").parent.IndexOf(raiz.Q<Button>("faccion")), "junto al yelmo");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("faccion"));
                yield return Esperar(() => Ventana()?.Q<Button>("btn-crear-faccion") != null, 5, "«Elige tu facción»");
                Assert.IsTrue(SelectorDePersonaje.Interior.PanelAbierto, "con la ventana abierta, el héroe no anda");
                yield return EnTamanos(raiz, "93_elige_tu_faccion", () => DentroDelMarco(Ventana(), "la ventana de Facción"));
                // Esc la cierra; la bandera la vuelve a abrir.
                yield return Tecla(UnityEngine.InputSystem.Key.Escape);
                yield return Esperar(() => Ventana() == null && !SelectorDePersonaje.Interior.PanelAbierto, 5, "Esc cierra la ventana");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("faccion"));
                yield return Esperar(() => Ventana()?.Q<Button>("btn-unirse-faccion") != null, 5, "otra vez «Elige tu facción»");

                // «Pedir ingreso»: la Facción de la otra cuenta, con su sigilo; pedirlo y el motor lo apunta.
                yield return ClicFijo(raton, raiz, Ventana().Q<Button>("btn-unirse-faccion"));
                yield return Esperar(() => Ventana()?.Q("lista-facciones")?.Q("faccion-" + faccionAjena) != null, 15, "la Facción ajena en la lista");
                var fila = Ventana().Q("faccion-" + faccionAjena);
                Assert.IsNotNull(fila.Q(className: "sigilo"), "con su sigilo");
                yield return EnTamanos(raiz, "95_pedir_ingreso", () => DentroDelMarco(Ventana(), "Pedir ingreso"));
                yield return ClicEnVentana(raton, raiz, fila.Q<Button>("pedir-" + faccionAjena));
                yield return Esperar(() => Faccion(faccionAjena)?.SolicitudesIds.Contains(conexion.HeroeId) == true, 10, "la solicitud en el motor");
                yield return Esperar(() => Ventana().Q("faccion-" + faccionAjena)?.Q<Label>(className: "faccion-pedido")?.text == "Pedido: decide su Rey"
                    || Ventana().Q<Button>("pedir-" + faccionAjena)?.text == "Pedido: decide su Rey", 5, "«Pedido: decide su Rey»");
                yield return Capturar("95b_pedido");

                // «Crear facción»: sin nombre, «Crear» apagado; con nombre y un sigilo elegido, el motor la crea con ese sigilo.
                yield return ClicEnVentana(raton, raiz, Ventana().Q<Button>("faccion-volver"));
                yield return Esperar(() => Ventana()?.Q<Button>("btn-crear-faccion") != null, 5, "de vuelta a elegir");
                yield return ClicFijo(raton, raiz, Ventana().Q<Button>("btn-crear-faccion"));
                yield return Esperar(() => Ventana()?.Q<TextField>("input-nombre-faccion") != null, 5, "«Crear facción»");
                Assert.IsFalse(Ventana().Q<Button>("btn-submit-crear-faccion").enabledSelf, "sin nombre, «Crear» apagado");
                yield return EnTamanos(raiz, "94_crear_faccion", () => DentroDelMarco(Ventana(), "Crear facción"));
                Ventana().Q<TextField>("input-nombre-faccion").value = "Tebas" + DateTime.UtcNow.Ticks % 10_000;
                Ventana().Q<DropdownField>("sigilo-campo").value = "cuartelado";
                Ventana().Q<DropdownField>("sigilo-forma").value = "torre";
                var c1 = Ventana().Q<DropdownField>("sigilo-color1").value;
                Ventana().Q<DropdownField>("sigilo-color2").value = c1;
                yield return null;
                Assert.IsFalse(Ventana().Q<Button>("btn-submit-crear-faccion").enabledSelf, "con el fondo de un color, «Crear» apagado");
                StringAssert.Contains("el mismo", string.Join(" ", Ventana().Q("error-faccion").Query<Label>().ToList().Select(l => l.text)), "su motivo");
                yield return Capturar("94b_crear_con_motivo");
                Ventana().Q<DropdownField>("sigilo-color2").value = Sigilos.Colores.First(x => x.Id != c1).Id;
                yield return null;
                Assert.IsTrue(Ventana().Q<Button>("btn-submit-crear-faccion").enabledSelf, "todo bien, «Crear» encendido");
                yield return Capturar("94c_crear_listo");
                yield return ClicEnVentana(raton, raiz, Ventana().Q<Button>("btn-submit-crear-faccion"));
                yield return Esperar(() => Yo().HasFaccionId, 15, "tu Facción en el motor");
                yield return Esperar(() => Faccion(Yo().FaccionId)?.Sigilo != null, 10, "su sigilo");
                var mia = Faccion(Yo().FaccionId);
                Assert.AreEqual("torre", mia.Sigilo.FormaId);
                Assert.AreEqual("cuartelado", mia.Sigilo.CampoId);
                Assert.AreEqual(c1, mia.Sigilo.ColorPrimarioId);
                // Ya con Facción, la ventana pasa a la tuya (tanda 2): su lateral con el sigilo elegido.
                yield return Esperar(() => Ventana()?.Q("faccion-lateral") != null, 10, "tu Facción en la ventana");
                Debug.Log($"[Prueba] facción {mia.Id} «{mia.Nombre}» creada con {Sigilos.Clave(mia.Sigilo)}");
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

        [UnityTest, Timeout(300_000)]
        public IEnumerator ElSelectorConElSigilo()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            TexturaDeSigilo.Asegurar();
            var azar = Sigilos.Aleatorio(new System.Random());
            string nick = null;
            yield return CuentaConFaccion(url, "Conescudo" + DateTime.UtcNow.Ticks % 100_000, "Corinto",
                SigiloDePrueba(azar.FormaId, azar.CampoId, azar.EmblemaId, azar.ColorPrimarioId, azar.ColorSecundarioId), x => nick = x, null);
            yield return EntrarAlCampamento(url, nick, (r, c) => { });
            yield return Esperar(() => GameObject.Find("Heroe del motor") != null, 10, "la ficha del héroe");
            var ficha = GameObject.Find("Heroe del motor");
            var icono = ficha.transform.Find("Icon");
            Assert.IsNotNull(icono, "el hueco del escudo");
            Assert.IsTrue(icono.gameObject.activeSelf, "el escudo de su Facción");
            var imagen = icono.GetComponent<UnityEngine.UI.Image>();
            StringAssert.StartsWith("Sigilo ", imagen.sprite.name, "con el sigilo");
            StringAssert.Contains("Corinto", ficha.GetComponentsInChildren<TMPro.TMP_Text>().First(t => t.text.StartsWith("Nivel ")).text, "y su nombre");
            foreach (var (ancho, alto) in Tamanos) yield return CapturarEscena("CharacterselecctionScene", $"96_selector_con_sigilo_{ancho}x{alto}", ancho, alto);
        }
    }
}
