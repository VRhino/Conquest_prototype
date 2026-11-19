// El ejército en el mapa (paso 11 del bloque 4, tanda 2), con el ratón de verdad contra el motor. El Gobernador funda su plaza y otro héroe de
// su Facción (otra cuenta) sale de ella como ejército abierto, solo, y se queda a la puerta.
// - Dentro de la plaza, el riel trae «Unirse a un ejército»: el Gobernador marca su tropa y se une.
// - En el mapa, la ventana Ejército (⚑): los dos integrantes y, como no es el Líder, «Separarme del ejército».
// - Sola su columna: «Organizar ejército (abierto)» la pone a formar; «Cancelar la formación» la deshace.
// - La ficha del ejército del otro trae «Unirme»: vuelve a él. El otro le cede el mando; el Líder marcha, «Acampar aquí» lo para y «Ceder el
//   mando» se lo devuelve. Se separa otra vez.
// Unirse a una batalla va aparte (`TuUnionALaBatalla`): sin batallas en Unity el motor las resuelve al momento y no hay a cuál unirse.
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
        public IEnumerator TuEjercitoEnElMapa()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza");
            string nick = null, faccion = null, plazaId = null;
            yield return FundarPlaza(url, x => nick = x, x => plazaId = x, faccionDe: x => faccion = x);
            string otro = null, nickOtro = null;
            yield return OtroDeLaFaccion(url, faccion, plazaId, (i, n) => { otro = i; nickOtro = n; });

            // El otro, con su cuenta abierta toda la prueba: convoca un ejército abierto y sale solo con él, a la puerta.
            Sesion so;
            {
                var acceso = new Acceso(url);
                var login = acceso.Entrar(nickOtro, "clave-de-prueba-1");
                yield return Esperar(() => login.IsCompleted, 20, "el login del otro");
                so = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
                var abrir = so.Abrir();
                for (var f = Time.realtimeSinceStartup + 20; !abrir.IsCompleted && Time.realtimeSinceStartup < f;) { so.Bombear(); yield return null; }
                for (var f = Time.realtimeSinceStartup + 2; Time.realtimeSinceStartup < f;) { so.Bombear(); yield return null; }
            }
            IEnumerator DelOtro(Func<V1.Orden> orden)
            {
                for (var f = Time.realtimeSinceStartup + 1; Time.realtimeSinceStartup < f;) { so.Bombear(); yield return null; }
                var o = orden();
                var r = so.Ordenar(o);
                for (var f = Time.realtimeSinceStartup + 20; !r.IsCompleted && Time.realtimeSinceStartup < f;) { so.Bombear(); yield return null; }
                Assert.AreEqual(V1.Resultado.OrdenAceptada, r.Result.Resultado, $"el otro, {o.CuerpoCase}: {r.Result.Rechazo?.Codigo} {r.Result.Rechazo?.Mensaje}");
            }
            V1.Ejercito ColumnaDelOtro() => so.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Ejercito).Select(e => e.Ejercito).FirstOrDefault(x => x.Participantes.Any(p => p.HeroeId == otro));
            {
                var suyas = so.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Escuadron).Select(e => e.Escuadron).Where(x => x.HeroeId == otro && x.Cantidad > 0).Select(x => x.Id).ToList();
                var convocar = new V1.OrdenConvocarEjercito { HeroeId = otro, PoliticaDeUnion = "aceptar", Carga = new V1.Cantidades() };
                convocar.EscuadronIds.AddRange(suyas);
                yield return DelOtro(() => new V1.Orden { ConvocarEjercito = convocar });
            }
            var suConvocatoria = so.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Convocatoria).Select(e => e.Convocatoria).First(x => x.LiderId == otro).Id;
            string suEjercito = null;
            // El otro sale con su ejército, solo, y se queda a la puerta.
            IEnumerator Parte()
            {
                yield return DelOtro(() => new V1.Orden { PartirConvocatoria = new V1.OrdenPartirConvocatoria { HeroeId = otro } });
                yield return Esperar(() => { so.Bombear(); return ColumnaDelOtro() != null; }, 10, "el ejército del otro, fuera");
                suEjercito = ColumnaDelOtro().Id;
                Assert.AreEqual(V1.TipoColumna.ColumnaEjercito, ColumnaDelOtro().Tipo, "sale como ejército");
            }

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
            var yo = conexion.HeroeId;
            V1.Ejercito MiCol() => ElMapaSiHay()?.Columna;
            // Tropa libre en el campamento del Gobernador, para aportarla.
            var escuadra = new Google.Protobuf.WellKnownTypes.Struct();
            escuadra.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(yo);
            escuadra.Fields["tropa_id"] = Google.Protobuf.WellKnownTypes.Value.ForString("lenadores");
            escuadra.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(30);
            yield return Administrar(url, "dar_escuadra", escuadra);
            // Solo la escena, para las capturas del prototipo (`tasks/pruebas/cdp/ejercito-mapa.mjs`): el servidor sigue arriba con ella.
            if (Environment.GetEnvironmentVariable("MUNDO_PROTO") == "ejercito")
            {
                yield return Parte();
                Debug.Log($"[Prueba] escena del prototipo: yo={nick} otro={nickOtro} ejercito={suEjercito} plaza={plazaId} x={so.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId)?.Asentamiento?.Posicion.X / 1000.0:0.###} y={so.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId)?.Asentamiento?.Posicion.Y / 1000.0:0.###}");
                so.Cerrar();
                LogAssert.ignoreFailingMessages = false;
                yield break;
            }
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
            GameObject sistema = null;
            IEnumerator Clic(VisualElement e) => ClicEnVentana(raton, raiz, e);
            VisualElement Ventana() => raiz.Q("ventana-ejercito");
            Button EnFicha(string texto) => raiz.Q("ficha-acciones")?.Query<Button>().ToList().FirstOrDefault(b => TextoDe(b) == texto);
            IEnumerator AbrirEjercito()
            {
                if (Ventana() == null) yield return Clic(raiz.Q<Button>("riel-ejercito"));
                yield return Esperar(() => Ventana() != null, 5, "la ventana Ejército");
            }
            try
            {
                // El lateral de la plaza: la tarjeta del ejército que el otro prepara dentro, con sus integrantes plegables y «Unirse», que abre
                // «Salir al mundo» con «Ejército» y su convocatoria.
                VisualElement Tarjeta(string id) => raiz.Q("plaza")?.Q("tarjeta-" + id);
                yield return Esperar(() => Tarjeta(suConvocatoria) != null, 15, "la tarjeta del ejército preparándose");
                Assert.IsTrue(Tarjeta(suConvocatoria).Q<Foldout>().value, "los integrantes, desplegados");
                Assert.IsNotNull(Tarjeta(suConvocatoria).Q("mini-lenadores"), "su tropa, en miniatura");
                yield return Capturar("168_tarjeta_preparandose");
                yield return EnTamanos(raiz, "168_tarjeta_preparandose", () => Assert.IsTrue(Contiene(raiz.worldBound, Tarjeta(suConvocatoria).worldBound), "la tarjeta, entera en pantalla"));
                yield return Clic(Tarjeta(suConvocatoria).Q<Foldout>().Q<Toggle>());
                yield return Esperar(() => Tarjeta(suConvocatoria)?.Q<Foldout>().value == false && PlayerPrefs.GetInt("mundo.plaza.integrantes", 1) == 0, 5, "los integrantes plegados, y recordado");
                yield return Clic(Tarjeta(suConvocatoria).Q<Foldout>().Q<Toggle>());
                yield return Esperar(() => PlayerPrefs.GetInt("mundo.plaza.integrantes", 1) == 1, 5, "desplegados otra vez");
                yield return Clic(Tarjeta(suConvocatoria).Q<Button>("tarjeta-unirse-" + suConvocatoria));
                yield return Esperar(() => raiz.Q("ventana-salida")?.Q<Button>("unirse-" + suConvocatoria) != null, 5, "«Salir al mundo» con su convocatoria");
                Assert.IsTrue(raiz.Q("ventana-salida").Q<RadioButton>("ejercito").value, "con «Ejército» ya elegido");
                yield return Clic(raiz.Q("ventana-salida").Q<Button>("cancelar"));
                yield return Esperar(() => raiz.Q("ventana-salida") == null, 5, "la ventana cerrada");

                // Sale con su ejército: la tarjeta pasa a «a la puerta», y su «Unirse» abre la ventana del riel, que sigue ahí.
                yield return Parte();
                yield return Esperar(() => Tarjeta(suEjercito) != null && Tarjeta(suConvocatoria) == null, 15, "la tarjeta del ejército a la puerta");
                yield return Capturar("169_tarjeta_puerta");
                yield return EnTamanos(raiz, "169_tarjeta_puerta", () => Assert.IsTrue(Contiene(raiz.worldBound, Tarjeta(suEjercito).worldBound), "la tarjeta, entera en pantalla"));
                Assert.IsNotNull(raiz.Q("plaza").Q<Button>("riel-unirse"), "«Unirse a un ejército» sigue en el riel");
                yield return Clic(Tarjeta(suEjercito).Q<Button>("tarjeta-unirse-" + suEjercito));
                yield return Esperar(() => raiz.Q("plaza-ventana")?.Q<Button>("unirse-" + suEjercito) != null, 5, "el ejército del otro en la ventana");
                Assert.IsTrue(raiz.Q("plaza-ventana").Query<Toggle>().ToList().Any(t => t.name.StartsWith("aporta-") && t.value), "tu tropa, marcada");
                yield return Capturar("170_unirse_desde_plaza");
                yield return EnTamanos(raiz, "170_unirse_desde_plaza", () => DentroDelMarco(raiz.Q("plaza-ventana"), "unirse a un ejército"));
                yield return Clic(raiz.Q("plaza-ventana").Q<Button>("unirse-" + suEjercito));
                yield return Esperar(() => raiz.Q("mapa") != null && MiCol()?.Id == suEjercito, 30, "en el mapa, dentro del ejército del otro");
                Assert.AreEqual(2, MiCol().Participantes.Count, "con los dos");
                // Fuera del interior (que trae el suyo) el mapa necesita su EventSystem, como en la prueba de las avanzadas.
                sistema = new GameObject("EventSystem de la prueba", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

                // La ventana Ejército: los dos, y «Separarme del ejército» (no eres el Líder).
                yield return Esperar(() => raiz.Q("mapa-riel") != null && !raiz.Q("mapa-riel").ClassListContains("oculto"), 10, "el riel del mapa con ⚑");
                yield return AbrirEjercito();
                yield return Esperar(() => Ventana()?.Q("integrante-" + otro) != null && Ventana().Q("integrante-" + yo) != null, 5, "los dos integrantes");
                Assert.IsNotNull(Ventana().Q<Button>("ej-separarme"), "«Separarme del ejército»");
                Assert.IsNull(Ventana().Q<Button>("ej-ceder"), "sin «Ceder el mando»: no eres el Líder");
                yield return Capturar("171_ventana_ejercito");
                yield return EnTamanos(raiz, "171_ventana_ejercito", () => DentroDelMarco(Ventana(), "la ventana Ejército"));
                yield return Clic(Ventana().Q<Button>("ej-separarme"));
                yield return Esperar(() => MiCol() != null && MiCol().Id != suEjercito && MiCol().Tipo == V1.TipoColumna.ColumnaPersonal, 10, "tu columna personal");

                // Sola: organizar un ejército abierto (formación) y cancelarla.
                yield return Esperar(() => Ventana()?.Q<Button>("ej-organizar-aceptar") != null, 5, "«Organizar ejército (abierto)»");
                yield return Clic(Ventana().Q<Button>("ej-organizar-aceptar"));
                yield return Esperar(() => MiCol()?.HasFormacionExpiraEnMs == true, 10, "la formación en el motor");
                yield return Esperar(() => Ventana()?.Q<Button>("ej-cancelar-formacion") != null, 5, "«Cancelar la formación»");
                yield return Capturar("172_formacion");
                yield return EnTamanos(raiz, "172_formacion", () => DentroDelMarco(Ventana(), "la formación"));
                yield return Clic(Ventana().Q<Button>("ej-cancelar-formacion"));
                yield return Esperar(() => MiCol() != null && !MiCol().HasFormacionExpiraEnMs, 10, "sin formación");
                yield return Clic(Ventana().Q<Button>("ejercito-cerrar"));
                yield return Esperar(() => Ventana() == null, 5, "la ventana cerrada");
                // La ficha del ejército del otro: «Unirme».
                var mapa = ElMapa();
                yield return Esperar(() => mapa.Cosas.Any(x => x.Id == suEjercito && x.Clase == ClaseDeCosa.Ejercito), 10, "el ejército del otro en el mapa");
                mapa.Seleccionar(mapa.Cosas.First(x => x.Id == suEjercito));
                yield return Esperar(() => EnFicha("Unirme") != null, 10, "«Unirme» en su ficha");
                yield return Capturar("173_ficha_unirme");
                yield return EnTamanos(raiz, "173_ficha_unirme", () => DentroDelMarco(raiz.Q("ficha"), "la ficha del ejército"));
                yield return Clic(EnFicha("Unirme"));
                yield return Esperar(() => MiCol()?.Id == suEjercito && MiCol().Participantes.Count == 2, 15, "otra vez en su ejército");

                // El otro te cede el mando: marchas, acampas y se lo devuelves.
                yield return DelOtro(() => new V1.Orden { CederLiderazgo = new V1.OrdenCederLiderazgo { EjercitoId = suEjercito, HeroeId = otro, SucesorId = yo } });
                yield return Esperar(() => MiCol()?.LiderId == yo, 10, "tú, el Líder");
                mapa.Seleccionar(null);
                var (cx, cy) = mapa.Centro.Value;
                var lejos = (Vector2)mapa.Vista.WorldToScreenPoint(EscalaDelMapa.AEscena(cx + 12, cy + 4, mapa.AlturaVisible(cx + 12, cy + 4)));
                yield return PantallasDeMundoTests.Clic(raton, lejos);
                if (mapa.ListaAbierta) Pulsar(raiz.Q("lista-filas").Query<Button>().ToList().First());
                yield return Esperar(() => MiCol()?.Estado == V1.EstadoColumna.ColumnaMarchando, 10, "el ejército en marcha");
                yield return AbrirEjercito();
                yield return Esperar(() => Ventana()?.Q<Button>("ej-estacionar") != null && Ventana().Q<Button>("ej-ceder") != null, 5, "«Acampar aquí» y «Ceder el mando»");
                yield return Capturar("174_ejercito_lider");
                yield return EnTamanos(raiz, "174_ejercito_lider", () => DentroDelMarco(Ventana(), "el Líder"));
                yield return Clic(Ventana().Q<Button>("ej-estacionar"));
                yield return Esperar(() => MiCol()?.Estado == V1.EstadoColumna.ColumnaEstacionada, 10, "acampado");
                yield return Esperar(() => Ventana()?.Q<Button>("ej-estacionar") == null && Ventana()?.Q<Button>("ej-ceder") != null, 5, "sin «Acampar aquí», ya quieto");
                yield return Clic(Ventana().Q<Button>("ej-ceder"));
                yield return Esperar(() => MiCol()?.LiderId == otro, 10, "el mando, otra vez del otro");
                yield return Esperar(() => Ventana()?.Q<Button>("ej-separarme") != null, 5, "«Separarme del ejército»");
                yield return Clic(Ventana().Q<Button>("ej-separarme"));
                yield return Esperar(() => MiCol() != null && MiCol().Tipo == V1.TipoColumna.ColumnaPersonal, 10, "tu columna personal otra vez");
                yield return Clic(Ventana().Q<Button>("ejercito-cerrar"));
            }
            finally
            {
                so?.Cerrar();
                if (sistema != null) UnityEngine.Object.Destroy(sistema);
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
