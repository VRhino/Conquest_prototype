// Salir como ejército (paso 11 del bloque 4, tanda 1), con el ratón de verdad contra el motor. El Gobernador funda su plaza y otro héroe de su Facción
// (otra cuenta) reside en ella. El Gobernador abre «Salir al mundo», elige «Ejército» con «Decide el Líder» y convoca: la ventana pasa a la
// preparación. El otro pide unirse; al Gobernador le sale su petición y la acepta: ya son dos integrantes. Cambia su tropa y la guarda, y sale con el
// ejército: en el mundo, un ejército con los dos.
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
        /// <summary>Otro héroe de la Facción `faccion`, con cuenta propia, que reside en la plaza `plaza`, está dentro y tiene una escuadra. Devuelve su
        /// id y su nick.</summary>
        static IEnumerator OtroDeLaFaccion(string url, string faccion, string plaza, Action<string, string> de)
        {
            var nick = "otro-" + DateTime.UtcNow.Ticks % 100_000_000;
            var acceso = new Acceso(url);
            var registro = acceso.Registrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => registro.IsCompleted, 20, "la cuenta");
            var login = acceso.Entrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => login.IsCompleted, 20, "el login");
            var s = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
            var abrir = s.Abrir();
            for (var f = Time.realtimeSinceStartup + 20; !abrir.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
            var camp = abrir.Result.CampamentosParaElegir[0].CampamentoId;
            string id = null;
            foreach (var nombre in new[] { "Egisto", "Orestes", "Pílades", "Electrión", "Tántalo", "Anfión" })
            {
                var r = s.Ordenar(new V1.Orden { CrearHeroe = new V1.OrdenCrearHeroe { DisplayName = nombre, CampamentoId = camp, ClassDefinitionId = "espada_escudo_mediano", Genero = "masculino", Avatar = new V1.OrdenCrearHeroe.Types.Avatar() } });
                for (var f = Time.realtimeSinceStartup + 20; !r.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
                if (r.Result.Resultado == V1.Resultado.OrdenAceptada) { id = r.Result.Creadas.First(c => c.Entidad == V1.TipoEntidad.Heroe).Id; break; }
            }
            s.Cerrar();
            Assert.IsNotNull(id, "el otro héroe");
            Google.Protobuf.WellKnownTypes.Struct P(params (string k, object v)[] ps)
            {
                var q = new Google.Protobuf.WellKnownTypes.Struct();
                foreach (var (k, v) in ps)
                    q.Fields[k] = v is string t ? Google.Protobuf.WellKnownTypes.Value.ForString(t) : Google.Protobuf.WellKnownTypes.Value.ForNumber(Convert.ToDouble(v));
                return q;
            }
            yield return Administrar(url, "fijar_faccion_de_heroe", P(("heroe_id", id), ("faccion_id", faccion)));
            yield return Administrar(url, "fijar_residencia", P(("heroe_id", id), ("residencia", plaza)));
            yield return Administrar(url, "colocar_heroe", P(("heroe_id", id), ("ubicacion", plaza)));
            yield return Administrar(url, "dar_escuadra", P(("heroe_id", id), ("tropa_id", "lenadores"), ("cantidad", 30)));
            de(id, nick);
        }

        [Category("Mapa")]
        [UnityTest, Timeout(900_000)]
        public IEnumerator TuEjercitoEnPreparacion()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza");
            string nick = null, faccion = null, plazaId = null;
            yield return FundarPlaza(url, x => nick = x, x => plazaId = x, faccionDe: x => faccion = x);
            string otro = null, nickOtro = null;
            yield return OtroDeLaFaccion(url, faccion, plazaId, (i, n) => { otro = i; nickOtro = n; });
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
            V1.Convocatoria Conv() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Convocatoria).Select(e => e.Convocatoria).FirstOrDefault(x => x.LiderId == conexion.HeroeId);
            // Una escuadra más en el campamento del Gobernador, para tener tropa que cambiar.
            var escuadra = new Google.Protobuf.WellKnownTypes.Struct();
            escuadra.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(conexion.HeroeId);
            escuadra.Fields["tropa_id"] = Google.Protobuf.WellKnownTypes.Value.ForString("lenadores");
            escuadra.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(30);
            yield return Administrar(url, "dar_escuadra", escuadra);
            yield return Quieto(1f);

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
            VisualElement Salida() => raiz.Q("ventana-salida");
            try
            {
                // «Salir al mundo» → «Cómo sales»: Ejército, «Decide el Líder», y convocar.
                yield return Clic(raiz.Q<Button>("salir-al-mundo"));
                yield return Esperar(() => Salida()?.Q<RadioButton>("ejercito") != null, 5, "«Ejército» en «Cómo sales»");
                yield return Clic(Salida().Q<RadioButton>("ejercito"));
                yield return Esperar(() => Salida().Q<Button>("convocar")?.resolvedStyle.display == DisplayStyle.Flex, 5, "«Convocar ejército»");
                Assert.AreEqual(DisplayStyle.None, Salida().Q<Button>("salir").resolvedStyle.display, "sin SALIR en modo ejército");
                yield return Clic(Salida().Q<RadioButton>("politica-preguntar"));
                yield return Capturar("160_salir_como_ejercito");
                yield return EnTamanos(raiz, "160_salir_como_ejercito", () => DentroDelMarco(Salida().Q(className: "placa"), "salir como ejército"));
                yield return Clic(Salida().Q<Button>("convocar"));
                yield return Esperar(() => Conv() != null, 10, "la convocatoria en el motor");
                Assert.AreEqual(V1.PoliticaUnion.UnionPreguntar, Conv().PoliticaDeUnion);
                yield return Esperar(() => Salida()?.Q("preparacion") != null, 5, "la preparación");
                Assert.IsNotNull(Salida().Q("integrante-" + conexion.HeroeId), "tú, entre los integrantes");

                // El otro pide unirse (su cuenta, aparte); la petición sale al Líder y la acepta.
                var otraSesion = default(Sesion);
                {
                    var acceso = new Acceso(url);
                    var login = acceso.Entrar(nickOtro, "clave-de-prueba-1");
                    yield return Esperar(() => login.IsCompleted, 20, "el login del otro");
                    otraSesion = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
                    var abrir = otraSesion.Abrir();
                    for (var f = Time.realtimeSinceStartup + 20; !abrir.IsCompleted && Time.realtimeSinceStartup < f;) { otraSesion.Bombear(); yield return null; }
                    for (var f = Time.realtimeSinceStartup + 2; Time.realtimeSinceStartup < f;) { otraSesion.Bombear(); yield return null; }
                    var suyas = otraSesion.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Escuadron).Select(e => e.Escuadron).Where(x => x.HeroeId == otro && x.Cantidad > 0).Select(x => x.Id).ToList();
                    var o = new V1.OrdenUnirseAConvocatoria { HeroeId = otro, ConvocatoriaId = Conv().Id, Carga = new V1.Cantidades() };
                    o.EscuadronIds.AddRange(suyas);
                    var r = otraSesion.Ordenar(new V1.Orden { UnirseAConvocatoria = o });
                    for (var f = Time.realtimeSinceStartup + 20; !r.IsCompleted && Time.realtimeSinceStartup < f;) { otraSesion.Bombear(); yield return null; }
                    Assert.AreEqual(V1.Resultado.OrdenAceptada, r.Result.Resultado, "la petición del otro: " + r.Result.Rechazo?.Codigo);
                }
                yield return Esperar(() => Salida()?.Q<Button>("aceptar-" + otro) != null, 10, "la petición del otro, al Líder");
                yield return Capturar("161_peticion");
                yield return Clic(Salida().Q<Button>("aceptar-" + otro));
                yield return Esperar(() => Conv()?.Integrantes.Count == 2, 10, "dos integrantes en el motor");
                yield return Esperar(() => Salida()?.Q("integrante-" + otro) != null, 5, "el otro, entre los integrantes");

                // Tu tropa: quitar una escuadra y guardar.
                var mias = Conv().Integrantes.First(x => x.HeroeId == conexion.HeroeId).Seleccion.EscuadronIds.ToList();
                if (mias.Count > 1)
                {
                    yield return Clic(Salida().Q<Button>("quitar-" + mias[0]));
                    yield return Esperar(() => Salida().Q<Button>("guardar-tropa")?.enabledSelf == true, 5, "«Guardar cambios»");
                    yield return Clic(Salida().Q<Button>("guardar-tropa"));
                    yield return Esperar(() => Conv().Integrantes.First(x => x.HeroeId == conexion.HeroeId).Seleccion.EscuadronIds.Count == mias.Count - 1, 10, "la tropa guardada en el motor");
                }
                yield return Capturar("162_preparacion");
                yield return EnTamanos(raiz, "162_preparacion", () => DentroDelMarco(Salida().Q(className: "placa"), "la preparación"));

                // Salir con el ejército: en el mundo, un ejército con los dos.
                yield return Clic(Salida().Q<Button>("conv-partir"));
                yield return Esperar(() => raiz.Q("mapa") != null && ElMapa()?.Columna != null, 30, "el mapa con el ejército");
                var col = ElMapa().Columna;
                Assert.AreEqual(V1.TipoColumna.ColumnaEjercito, col.Tipo, "un ejército");
                Assert.AreEqual(2, col.Participantes.Count, "con los dos");
                Assert.AreEqual(conexion.HeroeId, col.LiderId, "tú, su Líder");
                otraSesion?.Cerrar();
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
