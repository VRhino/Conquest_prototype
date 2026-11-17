// Las pantallas de fuera de la batalla de punta a punta contra un servidor del motor propio (`BRONZEAGE_PRUEBA_URL`; nunca el de juego):
// crear cuenta, ver la partida, entrar con su pantalla de carga, la sesión viva, el panel de avisos y cerrar sesión con su confirmación.
// Si `MUNDO_CAPTURAS` dice una carpeta, guarda allí una captura de cada pantalla (para mirarlas; hace falta correr sin -nographics).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ConquestTactics.Mundo;
using ConquestTactics.Mundo.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class PantallasDeMundoTests
    {
        static readonly string[] Preferencias = { "mundo.token", "mundo.nick", "mundo.servidor", "mundo.plaza.integrantes" };
        string[] guardadas;
        GameObject go;
        RenderTexture rt;

        /// <summary>Las pruebas del lote 2 cuando la suite va en dos a la vez (`tasks/pruebas/correr_en_paralelo.sh`, `MUNDO_LOTE`); las demás, en el 1.
        /// Repartidas para que los dos lotes tarden lo mismo: con los tiempos de las pruebas del 2026-11-16 (paso 12, tanda 1), unos 450 s de pruebas
        /// el 1 y unos 420 el 2.</summary>
        static readonly HashSet<string> Lote2 = new HashSet<string>
        {
            "TuPlazaPorDentro", "TuAvanzadaYSuObra", "ElCampamentoPorDentro", "TuDiplomaciaDelRey",
            "TuComercioADistancia", "TuEjercitoEnElMapa", "TuEjercitoConCaravanas", "TuTaberna", "TuTablonDeMercenarios", "TusMisiones", "TusAedas", "LaCronica",
        };

        [SetUp]
        public void Guardar()
        {
            guardadas = null;
            var lote = Environment.GetEnvironmentVariable("MUNDO_LOTE");
            var esDel2 = Lote2.Contains(TestContext.CurrentContext.Test.MethodName);
            if ((lote == "1" && esDel2) || (lote == "2" && !esDel2)) Assert.Ignore($"en el otro lote (MUNDO_LOTE={lote})");
            guardadas = Array.ConvertAll(Preferencias, k => PlayerPrefs.GetString(k, null));
        }

        [TearDown]
        public void Restaurar()
        {
            // Lo que tuviera guardado quien corre la prueba (su login de verdad) se queda como estaba.
            // (sin `guardadas`, la prueba era del otro lote y no llegó a empezar)
            for (int i = 0; guardadas != null && i < Preferencias.Length; i++)
                if (guardadas[i] == null) PlayerPrefs.DeleteKey(Preferencias[i]); else PlayerPrefs.SetString(Preferencias[i], guardadas[i]);
            PlayerPrefs.Save();
            if (go != null) UnityEngine.Object.Destroy(go);
            if (rt != null) rt.Release();
        }

        /// <summary>Si una prueba se queda a medias con el creador de personaje abierto, se cierra antes de la siguiente.</summary>
        [UnityTearDown]
        public IEnumerator CerrarElCreador()
        {
            SelectorDePersonaje.Actual = null;
            SelectorDePersonaje.Heroe = null;
            SelectorDePersonaje.Interior = null;
            foreach (var escena in new[] { "AvatarCreator", "CharacterselecctionScene", "FeudoScene" })
            {
                if (!UnityEngine.SceneManagement.SceneManager.GetSceneByName(escena).isLoaded) continue;
                var descarga = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(escena);
                while (descarga != null && !descarga.isDone) yield return null;
            }
        }

        [UnityTest]
        public IEnumerator CrearCuentaEntrarYCerrarSesion()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
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
            doc.panelSettings.clearColor = true; // a una textura, el panel no limpia solo lo de antes
            var raiz = doc.rootVisualElement;
            yield return null;
            yield return Capturar("1_acceso");
            yield return EnTamanos(raiz, "1b_acceso");

            Pulsar(raiz.Q<Button>("pestana-registro"));
            var nick = "ui-" + DateTime.UtcNow.Ticks % 100_000_000;
            raiz.Q<TextField>("nick").value = nick;
            raiz.Q<TextField>("clave").value = "clave-de-prueba-1";
            raiz.Q<TextField>("repetir").value = "otra-distinta-1";
            Pulsar(raiz.Q<Button>("enviar"));
            yield return null;
            Assert.IsFalse(raiz.Q<Label>("repetir-error").ClassListContains("oculto"), "la repetida no coincide: se dice junto al campo");
            yield return Capturar("2_registro_con_error");
            raiz.Q<TextField>("repetir").value = "clave-de-prueba-1";
            Pulsar(raiz.Q<Button>("enviar"));

            yield return Esperar(() => raiz.Q<Label>(className: "partida-nombre") != null, 20, "la lista de partidas");
            StringAssert.Contains("sin héroe", raiz.Q<Label>(className: "partida-heroe").text);
            yield return Capturar("3_partidas");
            yield return EnTamanos(raiz, "3b_partidas");

            Pulsar(raiz.Q<Button>("entrar"));
            yield return Esperar(() => raiz.Q(className: "carga") != null, 5, "la pantalla de carga");
            yield return Quieto(0.8f);
            yield return Capturar("4_carga");
            // Sin héroe: «Nace tu héroe» (paso 3), solo el lugar y el nombre, sobre el mapa de los campamentos.
            yield return Esperar(() => raiz.Q("crear") != null && raiz.Q(className: "carga") == null, 60, "crear el héroe");
            Assert.IsNotNull(conexion.Artefacto, "el artefacto de mundo, cargado");
            var mapaCrear = ElMapa();
            yield return Esperar(() => mapaCrear.Centro != null && mapaCrear.SueloCompleto, 60, "el suelo de los campamentos");
            yield return Quieto(1f);
            Assert.IsNull(raiz.Q("clases"), "la clase ya no se elige aquí");
            Assert.IsTrue(raiz.Q<Button>("nacer").ClassListContains("oculto"), "sin campamento, «Nacer» no sale");
            StringAssert.Contains("Elige campamento", raiz.Q<Label>("falta").text);
            var tarjetas = raiz.Q<ScrollView>("campamentos").Query(className: "campamento").ToList();
            Assert.AreEqual(1, tarjetas.Count(t => t.Q<Label>(className: "campamento-recomendado") != null), "un solo «Recomendado»");
            foreach (var t in tarjetas)
                Assert.IsFalse(t.Q<Label>(className: "aviso-titulo").text.StartsWith("Campamento "), "cada campamento con el nombre de su región");
            yield return Capturar("5_crear_heroe");
            yield return CapturarMapa(mapaCrear, "5b_mapa_de_campamentos");

            var entrada = UnityEngine.InputSystem.InputSystem.settings;
            var fondoAntes = entrada.backgroundBehavior;
            var editorAntes = entrada.editorInputBehaviorInPlayMode;
            entrada.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            entrada.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var ratonCrear = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                ratonCrear.MakeCurrent();
                // Un clic con el ratón sobre el campamento del centro del mapa (el más poblado) lo elige, con su ficha.
                var mirado = mapaCrear.Adicionales.Where(x => x.Clase == ClaseDeCosa.Mercenarios).OrderByDescending(x => CosasDelMapa.Distancia(x.X, x.Y, mapaCrear.Enfoque.Value.x, mapaCrear.Enfoque.Value.y) < 1 ? 1 : 0).First();
                var sobreEl = (Vector2)mapaCrear.Vista.WorldToScreenPoint(ConquestTactics.Mundo.Mapa.EscalaDelMapa.AEscena(mirado.X, mirado.Y, mapaCrear.AlturaVisible(mirado.X, mirado.Y)));
                yield return Clic(ratonCrear, sobreEl);
                yield return Esperar(() => mapaCrear.Seleccionada?.Id == mirado.Id, 5, "el campamento elegido con el clic en el mapa");
                Assert.IsFalse(raiz.Q<Button>("nacer").ClassListContains("oculto"), "con campamento, «Nacer» sale");
                StringAssert.Contains(mirado.Nombre.ToUpperInvariant(), raiz.Q<Button>("nacer").text);
                yield return Quieto(1f);
                yield return Capturar("5c_elegido_con_el_raton");
                yield return CapturarMapa(mapaCrear, "5d_elegido_con_el_raton_en_el_mapa");

                // «Elegir por mí»: uno de los tres mejor valorados.
                Pulsar(raiz.Q<Button>("elegir-por-mi"));
                yield return null;
                Assert.IsNotNull(mapaCrear.Seleccionada);
                yield return Capturar("5e_elegir_por_mi");

                // Comparar dos: la tabla en lugar de la ficha; «Elegir» uno de ellos lo elige y vuelve la ficha.
                Pulsar(raiz.Q<ScrollView>("campamentos").Query(className: "campamento").ToList()[0].Q<Toggle>("comparar"));
                yield return null;
                Pulsar(raiz.Q<ScrollView>("campamentos").Query(className: "campamento").ToList()[1].Q<Toggle>("comparar"));
                yield return null;
                Assert.IsFalse(raiz.Q("comparacion").ClassListContains("oculto"), "con dos marcados, la comparación");
                Assert.Greater(raiz.Q("comparacion").Query(className: "comparar-fila").ToList().Count, 10, "fila a fila");
                Assert.IsNotNull(raiz.Q("comparacion").Q(className: "comparar-gana"), "con el mejor de cada fila marcado");
                yield return Capturar("6_comparar_dos");
                Pulsar(raiz.Q<Button>("elegir-b"));
                yield return null;
                Assert.IsTrue(raiz.Q("comparacion").ClassListContains("oculto"));

                // El campamento 4 (`camp-4`: un río a 15 u y un lago a 1 u), desde la lista: su ficha con lo que lo rodea.
                Pulsar(raiz.Q("tarjeta-camp-4").Q<Button>("elegir"));
                yield return Esperar(() => mapaCrear.Seleccionada?.Id == "camp-4", 5, "el campamento elegido");
                Assert.Greater(raiz.Q("campamento-datos").childCount, 5, "lo que rodea al campamento");
                yield return Quieto(1.5f);
                yield return Capturar("7_campamento_elegido");
                yield return CapturarMapa(mapaCrear, "7b_campamento_elegido_en_el_mapa");
                // El nombre y «Nacer» caben siempre en el panel, en cada tamaño de ventana.
                yield return EnTamanos(raiz, "7c_crear", () => Pie(raiz));

                // Un nombre que ya tiene otro héroe: el creador de personaje lo dice; al volver, junto al campo.
                var ocupado = "Ocu" + nick.Substring(3);
                yield return OtroHeroe(url, ocupado);
                raiz.Q<TextField>("nombre").value = ocupado;
                Pulsar(raiz.Q<Button>("nacer"));
                AvatarCreatorUIController creador = null;
                yield return Esperar(() => (creador = UnityEngine.Object.FindAnyObjectByType<AvatarCreatorUIController>()) != null, 20, "el creador de personaje");
                yield return Quieto(1f);
                SoloPintaLaEscena("AvatarCreator", "el creador");
                Assert.AreEqual(DisplayStyle.None, raiz.resolvedStyle.display, "la interfaz del mundo se aparta");
                Assert.IsFalse(mapaCrear.gameObject.activeSelf, "y el mapa");
                var clasesDelMotor = UnityEngine.Object.FindAnyObjectByType<HeroClassSelector>();
                var botonesClase = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude).Where(x => x.name.StartsWith("Clase ")).ToList();
                Assert.AreEqual(5, botonesClase.Count, "las 5 clases de /v1/datos");
                yield return CapturarCreador("8_creador_de_personaje");
                for (int i = 0; i < botonesClase.Count; i++)
                {
                    yield return ClicUGUI(ratonCrear, botonesClase[i]);
                    Assert.AreEqual(botonesClase[i].name, "Clase " + clasesDelMotor.ClaseDelMotor.Id, "la clase pulsada con el ratón");
                    yield return CapturarCreador($"8_clase_{i + 1}");
                }
                var nacer = Boton(creador, "continueButton");
                yield return ClicUGUI(ratonCrear, nacer);
                yield return Esperar(() => Error(creador) != "", 10, "el rechazo del nombre en el creador");
                StringAssert.Contains("nombre", Error(creador));
                yield return CapturarCreador("8b_nombre_en_uso_en_el_creador");
                yield return ClicUGUI(ratonCrear, Boton(creador, "cancelButton"));
                yield return Esperar(() => creador == null && raiz.resolvedStyle.display == DisplayStyle.Flex, 10, "volver a «Nace tu héroe»");
                Assert.IsTrue(mapaCrear.gameObject.activeSelf, "con el mapa");
                Assert.AreEqual("camp-4", mapaCrear.Seleccionada?.Id, "y el campamento que había");
                Assert.AreEqual(ocupado, raiz.Q<TextField>("nombre").value, "y el nombre");
                Assert.IsFalse(raiz.Q<Label>("nombre-error").ClassListContains("oculto"), "con el rechazo junto al campo");
                yield return Capturar("8c_nombre_en_uso_al_volver");

                // Nace: otro nombre, al creador; la clase, mujer y otro pelo; «Nacer» y al aceptarse, el mapa con el héroe dentro de su campamento.
                raiz.Q<TextField>("nombre").value = "U" + nick.Substring(3);
                Pulsar(raiz.Q<Button>("nacer"));
                yield return Esperar(() => (creador = UnityEngine.Object.FindAnyObjectByType<AvatarCreatorUIController>()) != null, 20, "el creador otra vez");
                yield return null;
                SoloPintaLaEscena("AvatarCreator", "el creador otra vez");
                yield return Quieto(1f);
                clasesDelMotor = UnityEngine.Object.FindAnyObjectByType<HeroClassSelector>();
                yield return ClicUGUI(ratonCrear, UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude).First(x => x.name.StartsWith("Clase ") && x.name != "Clase " + clasesDelMotor.ClaseDelMotor.Id));
                var claseElegida = clasesDelMotor.ClaseDelMotor.Id;
                yield return ClicUGUI(ratonCrear, creador.femaleButton);
                Assert.AreEqual(Gender.Female, creador.sliderController.avatarPartSelector.currentGender, "mujer, con el ratón");
                // Piezas que no son las de por defecto: pelo 2, cabeza 1 y cejas 1.
                creador.sliderController.hairSlider.value = 2;
                creador.sliderController.headSlider.value = 1;
                creador.sliderController.eyebrowSlider.value = 1;
                yield return null;
                var aspecto = creador.sliderController.avatarPartSelector.GetCurrentSelection();
                Assert.IsNotEmpty(aspecto.hairId, "el pelo, con su id de AvatarPartDatabase");
                StringAssert.Contains(raiz.Q<TextField>("nombre").value, creador.heroNameInput.text, "el nombre, como texto");
                Assert.IsFalse(creador.heroNameInput.interactable, "que no se edita allí");
                yield return CapturarCreador("8d_creador_listo");
                foreach (var (ancho, alto) in Tamanos) yield return CapturarCreador($"8e_creador_{ancho}x{alto}", ancho, alto);
                yield return ClicUGUI(ratonCrear, Boton(creador, "continueButton"));
                yield return Esperar(() => (raiz.Q("mapa") != null || raiz.Q("interior") != null) && raiz.Q(className: "carga") == null, 60, "el mapa o el interior");
                Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<AvatarCreatorUIController>(), "el creador, descargado");
                // El motor guarda lo elegido y lo devuelve en el Heroe propio: la clase, mujer y las mismas piezas.
                BronzeAge.Contract.V1.Heroe heroe = null;
                yield return Esperar(() => (heroe = conexion.Sesion.Almacen.Buscar(BronzeAge.Contract.V1.TipoEntidad.Heroe, conexion.HeroeId)?.Heroe) != null, 10, "el héroe en el almacén");
                Assert.AreEqual(claseElegida, heroe.ClassDefinitionId, "el héroe nace con la clase elegida en el creador");
                Assert.IsTrue(heroe.Femenino, "mujer, guardado por el motor");
                Assert.IsNotNull(heroe.Avatar, "con su avatar");
                Assert.AreNotEqual("head_00", aspecto.headId, "una cabeza que no es la de por defecto");
                Assert.AreEqual(aspecto.headId, heroe.Avatar.CabezaId, "la cabeza, de vuelta igual");
                Assert.AreEqual(aspecto.hairId, heroe.Avatar.PeloId, "el pelo");
                Assert.AreEqual(aspecto.eyebrowId, heroe.Avatar.CejasId, "las cejas");
                Assert.AreEqual(aspecto.beardId ?? "", heroe.Avatar.BarbaId, "sin barba");
                // Al nacer, dentro de su campamento: su interior en FeudoScene, andando.
                yield return EnElInterior(raiz, "16_interior", true);
                Debug.Log($"[Prueba] aspecto elegido {aspecto.headId}/{aspecto.hairId}/{aspecto.eyebrowId}; guardado {heroe.Avatar}, femenino {heroe.Femenino}");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(ratonCrear);
                entrada.backgroundBehavior = fondoAntes;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
            }
            // Con héroe, el mapa (paso 2). Todo lo que hace el jugador va por el mismo camino que él: botones con eventos de UI Toolkit y el
            // ratón con un dispositivo del Input System (en batch no hay foco: aquí el Input System no lo mira).
            yield return Esperar(() => raiz.Q("mapa") != null && raiz.Q(className: "carga") == null, 60, "el mapa");
            // El mapa nuevo (el de crear el héroe se está destruyendo).
            yield return Esperar(() => mapaCrear == null, 5, "que se vaya el mapa de crear el héroe");
            var mapa = ElMapa();
            Assert.IsNotNull(mapa);
            yield return Esperar(() => mapa.Centro != null && mapa.SueloCompleto && mapa.TeselasCargadas > 4, 60, "el suelo");
            yield return Esperar(() => conexion.Sesion.Almacen.Ambitos.Keys.Any(k => k.StartsWith("VISTA:")), 10, "la VISTA");
            var pantallas = go.GetComponent<PantallasDeMundo>();
            Assert.IsFalse(pantallas.SobreInterfaz(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), "el centro del mapa es del mapa: ahí el ratón no lo coge la interfaz");
            Assert.IsTrue(pantallas.SobreInterfaz(new Vector2(Screen.width - 20, Screen.height - 20)), "la barra de arriba sí es interfaz");
            Pulsar(raiz.Q<Button>("avisos"));
            yield return null;
            Assert.IsNotNull(raiz.Q(className: "panel-avisos"));
            yield return Capturar("9a_avisos");
            Pulsar(raiz.Q<Button>("avisos"));

            // Los ríos se ven: cerca del campamento, toda la cinta del río queda por encima del suelo pintado (el cauce está excavado en el relieve
            // y antes la cinta quedaba enterrada bajo las orillas).
            var centroCamp = mapa.Centro.Value;
            int cerca = 0;
            foreach (var vertice in mapa.MallaDeRios.vertices)
            {
                var (rx, _, ry) = ConquestTactics.Mundo.Mapa.EscalaDelMapa.AlMapa(vertice);
                if (CosasDelMapa.Distancia(rx, ry, centroCamp.x, centroCamp.y) > 60) continue;
                var malla = mapa.AlturaDeMalla(rx, ry);
                if (malla == null) continue;
                cerca++;
                Assert.Greater(vertice.y, malla.Value, $"el río en ({rx:0.0}, {ry:0.0}) queda por encima del suelo");
            }
            Assert.Greater(cerca, 0, "hay río cerca del campamento");
            mapa.Camara.PasoDeZoom(1);
            mapa.Camara.Inclinar(25);
            yield return Quieto(1.5f);
            yield return Esperar(() => mapa.SueloCompleto, 20, "el suelo alejado");
            yield return CapturarMapa(mapa, "9c_rio_y_agua");
            mapa.Camara.PasoDeZoom(-1);
            mapa.Camara.Inclinar(-25);

            // Fuera del campamento por «Salir al mundo»: la columna a la puerta (ya no hay aviso de «dentro»).
            yield return Esperar(() => mapa.Columna != null, 10, "la columna fuera");
            Assert.IsTrue(raiz.Q("dentro").ClassListContains("oculto"), "fuera, sin el aviso de dentro");
            yield return CapturarMapa(mapa, "9_mapa_tras_salir_al_mundo");
            // El aspecto que guardó el motor, en el diagnóstico (F2, con un teclado del Input System).
            var fondoF2 = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
            var editorF2 = UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode;
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // El teclado, después de ignorar el foco (si no, sin foco en batch nace apagado).
            var teclado = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            try
            {
                teclado.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F2));
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return Quieto(0.5f);
                yield return Esperar(() => raiz.Query<Label>().ToList().Any(l => l.text.StartsWith("mujer · cabeza") && Visible(l)), 5, "el aspecto en el diagnóstico (F2)");
                Assert.IsFalse(raiz.Query<Label>().ToList().Any(l => l.text == "Eres miembro, aún sin héroe." && Visible(l)), "recién nacido, el diagnóstico ya tiene héroe");
                yield return Capturar("9d_diagnostico_con_el_aspecto");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F2));
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(teclado);
                UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = fondoF2;
                UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = editorF2;
            }

            var ajustes = UnityEngine.InputSystem.InputSystem.settings;
            var fondo = ajustes.backgroundBehavior;
            var editor = ajustes.editorInputBehaviorInPlayMode;
            ajustes.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            ajustes.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var raton = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                raton.MakeCurrent();
                // La rueda cambia el zoom.
                var zoomAntes = mapa.Camara.Zoom;
                yield return Rueda(raton, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
                Assert.AreNotEqual(zoomAntes, mapa.Camara.Zoom, "la rueda cambia el zoom");

                // Un clic sobre el campamento lo elige: su ficha, con «Entrar» porque la columna está a la puerta.
                var campamento = mapa.Cosas.First(x => x.Clase == ClaseDeCosa.Mercenarios);
                var sobreElCampamento = (Vector2)mapa.Vista.WorldToScreenPoint(ConquestTactics.Mundo.Mapa.EscalaDelMapa.AEscena(campamento.X, campamento.Y, mapa.AlturaVisible(campamento.X, campamento.Y)));
                yield return Clic(raton, sobreElCampamento);
                yield return Esperar(() => mapa.Seleccionada?.Clase == ClaseDeCosa.Mercenarios || mapa.ListaAbierta, 5, "el campamento elegido con el clic (o la lista)");
                if (mapa.ListaAbierta)
                {
                    // Con varias cosas bajo el clic, la lista: se elige el campamento en ella.
                    yield return Capturar("10a_lista_bajo_el_clic");
                    yield return EnTamanos(raiz, "10c_lista_bajo_el_clic");
                    Pulsar(raiz.Q("lista-filas").Query<Button>().ToList().First(b => b.Q<Label>().text == "Mercenarios"));
                    yield return Esperar(() => mapa.Seleccionada?.Clase == ClaseDeCosa.Mercenarios, 5, "el campamento elegido en la lista");
                }
                yield return Esperar(() => !raiz.Q("ficha").ClassListContains("oculto"), 5, "la ficha");
                yield return Quieto(0.5f);
                var botones = raiz.Q("ficha-acciones").Query<Button>().ToList().Select(TextoDe).ToList();
                CollectionAssert.Contains(botones, "Marchar aquí");
                yield return Capturar("10_ficha_del_campamento");
                yield return EnTamanos(raiz, "10d_ficha");
                yield return CapturarMapa(mapa, "10b_campamento_elegido");

                // Un clic en el suelo, lejos de todo: se quita la selección y la columna marcha.
                var desde = mapa.Centro.Value;
                // Un punto de suelo sin nada debajo (lo que hay cerca del campamento también se puede coger con el clic).
                var suelo = new[] { 0.25f, 0.15f, 0.7f, 0.85f }.SelectMany(fy => new[] { 0.5f, 0.3f, 0.7f }.Select(fx => new Vector2(Screen.width * fx, Screen.height * fy)))
                    .First(pp => mapa.PuntoBajo(pp) is (double, double) q && CosasDelMapa.BajoElClic(mapa.Cosas, q.Item1, q.Item2, mapa.Camara.PxPorUnidad(Screen.height)).Count == 0);
                yield return Clic(raton, suelo);
                yield return Esperar(() => mapa.Seleccionada == null, 5, "que el clic en el suelo quite la selección");
                yield return Quieto(2f);
                var ahora = mapa.Centro.Value;
                Assert.Greater(Math.Abs(ahora.x - desde.x) + Math.Abs(ahora.y - desde.y), 0.1, "la columna anda hacia donde se hizo clic");
                Assert.Greater(mapa.PuntosDeRuta, 1, "con su ruta");
                yield return CapturarMapa(mapa, "11_mapa_marchando");

                // Volver a entrar: otra vez hasta el campamento («Marchar aquí» en su ficha) y, a la puerta, «Entrar»: de nuevo el interior.
                mapa.Seleccionar(campamento);
                yield return Esperar(() => !raiz.Q("ficha").ClassListContains("oculto"), 5, "la ficha del campamento");
                yield return null;
                Pulsar(raiz.Q("ficha-acciones").Query<Button>().ToList().First(b => TextoDe(b) == "Marchar aquí"));
                // Solo lo que se puede hacer: mientras la columna llega, «Entrar» no está; al parar a la puerta, sí.
                yield return Esperar(() => mapa.Columna?.Estado == BronzeAge.Contract.V1.EstadoColumna.ColumnaMarchando, 10, "la columna en marcha hacia el campamento");
                // La ficha se repinta en su paso siguiente: se espera a que quite «Entrar» (antes, una espera fija de 0,5 s).
                yield return Esperar(() => !raiz.Q("ficha-acciones").Query<Button>().ToList().Any(b => TextoDe(b) == "Entrar"), 2, "con la columna en marcha, sin «Entrar»");
                yield return Capturar("17a_llegando_sin_entrar");
                yield return Esperar(() => raiz.Q("ficha-acciones")?.Query<Button>().ToList().Any(b => TextoDe(b) == "Entrar") == true, 90, "«Entrar» con la columna parada a la puerta");
                Assert.AreEqual(BronzeAge.Contract.V1.EstadoColumna.ColumnaEstacionada, mapa.Columna.Estado, "«Entrar» sale con la columna parada");
                yield return Capturar("17_entrar_a_la_puerta");
                Pulsar(raiz.Q("ficha-acciones").Query<Button>().ToList().First(b => TextoDe(b) == "Entrar"));
                yield return EnElInterior(raiz, "17_de_vuelta", false);
                mapa = ElMapa();
                yield return Esperar(() => mapa.Centro != null && mapa.SueloCompleto, 60, "el suelo otra vez");
                // Y a marchar otra vez, con un clic en el suelo.
                yield return Quieto(0.5f);
                var sueloOtraVez = new[] { 0.25f, 0.15f, 0.7f, 0.85f }.SelectMany(fy => new[] { 0.5f, 0.3f, 0.7f }.Select(fx => new Vector2(Screen.width * fx, Screen.height * fy)))
                    .First(pp => mapa.PuntoBajo(pp) is (double, double) q && CosasDelMapa.BajoElClic(mapa.Cosas, q.Item1, q.Item2, mapa.Camara.PxPorUnidad(Screen.height)).Count == 0);
                yield return Clic(raton, sueloOtraVez);
                yield return Esperar(() => mapa.PuntosDeRuta > 1, 10, "la columna en marcha otra vez");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                ajustes.backgroundBehavior = fondo;
                ajustes.editorInputBehaviorInPlayMode = editor;
            }
            Pulsar(raiz.Q<Button>("marcha-forzada"));
            yield return Esperar(() => mapa.Columna?.MarchaForzada == true, 10, "la marcha forzada");
            yield return MedirFotogramas(mapa);
            Pulsar(raiz.Q<Button>("capa-fertilidad"));
            mapa.Camara.PasoDeZoom(1);
            mapa.Camara.PasoDeZoom(1);
            mapa.Camara.Inclinar(30);
            yield return Quieto(2f);
            yield return CapturarMapa(mapa, "12_capa_de_fertilidad");
            Pulsar(raiz.Q<Button>("capa-fertilidad"));

            // Entrar con la columna ya fuera y marchando (como al conectarse con la columna en marcha desde otro sitio): un mapa nuevo sobre la
            // misma sesión enseña la ruta y el destino desde el primer momento. («Cambiar de partida» no vale: saca al héroe del mundo.)
            var lejos = mapa.MarcharA(mapa.Centro.Value.x + 40, mapa.Centro.Value.y);
            yield return Esperar(() => lejos.IsCompleted, 10, "la marcha larga");
            UnityEngine.Object.Destroy(mapa.gameObject);
            yield return null;
            mapa = new GameObject("Mapa nuevo").AddComponent<ConquestTactics.Mundo.Mapa.MapaDeMundo>();
            mapa.Iniciar(conexion.Sesion, new Relieve(conexion.Relieve), conexion.Artefacto, conexion.Datos, () => conexion.HeroeId, _ => false, (t, p) => { }, null);
            yield return null;
            Assert.IsNotNull(mapa.Columna, "la columna, en el primer fotograma");
            Assert.Greater(mapa.PuntosDeRuta, 1, "su ruta, en el primer fotograma");
            yield return CapturarMapa(mapa, "13_ruta_al_volver");
            // Este mapa es de la prueba, no del juego: si se queda, las pruebas siguientes encuentran dos y a veces miran el que no es.
            UnityEngine.Object.Destroy(mapa.gameObject);
            yield return null;

            Pulsar(raiz.Q<Button>("cerrar-sesion"));
            yield return null;
            Assert.IsNotNull(raiz.Q(className: "dialogo"), "cerrar sesión pide confirmación");
            yield return Capturar("7_confirmar");
            Pulsar(raiz.Q(className: "dialogo").Q<Button>(className: "boton-primario"));
            yield return Esperar(() => raiz.Q<Button>("pestana-registro") != null, 10, "volver al acceso");
            Assert.IsFalse(conexion.HayToken, "el token se olvida");
        }

        /// <summary>El creador de personaje sin el encargo del motor: ya no trae las clases de relleno (tanda 3 de las clases y los objetos, 24a).
        /// No hay clases que elegir ni nada que crear: el creador solo vive en el flujo del motor.</summary>
        [UnityTest]
        public IEnumerator ElCreadorSinElMotorNoTraeClasesDeRelleno()
        {
            SelectorDePersonaje.Actual = null;
            var carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("AvatarCreator", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            while (!carga.isDone) yield return null;
            yield return null;
            yield return null;
            var selector = UnityEngine.Object.FindAnyObjectByType<HeroClassSelector>();
            Assert.IsNotNull(selector);
            Assert.IsNull(selector.ClaseDelMotor);
            Assert.IsFalse(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude).Any(b => b.name.StartsWith("Clase ")), "sin clases");
            var descarga = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("AvatarCreator");
            while (!descarga.isDone) yield return null;
        }

        /// <summary>Con héroe, al entrar sale el selector de personaje de Conquest en lugar de la tarjeta de la partida: el héroe en 3D con el
        /// aspecto que guardó el motor y su armadura, su nombre, nivel, clase y Facción, sin crear ni borrar; «Entrar al mundo» lleva al mapa.
        /// Dos cuentas: una mujer con piezas que no son las de por defecto y un hombre de otra clase. Las dos, sin Facción.</summary>
        [UnityTest]
        public IEnumerator ConHeroeSaleElSelectorDePersonaje()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            var cuentas = new[]
            {
                (nombre: "Ana" + DateTime.UtcNow.Ticks % 1_000_000, clase: "arco_lanza", genero: "femenino",
                    avatar: new BronzeAge.Contract.V1.OrdenCrearHeroe.Types.Avatar { CabezaId = "head_01", PeloId = "hair_03", CejasId = "eyebrow_02" }),
                (nombre: "Bru" + DateTime.UtcNow.Ticks % 1_000_000, clase: "espada_escudo_mediano", genero: "masculino",
                    avatar: new BronzeAge.Contract.V1.OrdenCrearHeroe.Types.Avatar { CabezaId = "head_02", PeloId = "hair_01", BarbaId = "facialhair_02", CejasId = "eyebrow_01" }),
                // El caso de Víctor (2026-11-13): un hombre con un pelo que no es el primero y SIN barba. El maniquí trae coleta y barba puestas:
                // un hueco vacío no puede quedarse con la del maniquí.
                (nombre: "Len" + DateTime.UtcNow.Ticks % 1_000_000, clase: "lanza_escudo_redondo", genero: "masculino",
                    avatar: new BronzeAge.Contract.V1.OrdenCrearHeroe.Types.Avatar { CabezaId = "head_03", PeloId = "hair_02", BarbaId = "", CejasId = "eyebrow_01" }),
            };
            for (int n = 0; n < cuentas.Length; n++)
            {
                var cuenta = cuentas[n];
                string nick = null;
                yield return OtroHeroe(url, cuenta.nombre, cuenta.clase, cuenta.genero, cuenta.avatar, x => nick = x);
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

                // Entrar como el jugador: nick, clave, «Entrar».
                raiz.Q<TextField>("nick").value = nick;
                raiz.Q<TextField>("clave").value = "clave-de-prueba-1";
                Pulsar(raiz.Q<Button>("enviar"));
                HeroSelectionSceneController selector = null;
                yield return Esperar(() => (selector = UnityEngine.Object.FindAnyObjectByType<HeroSelectionSceneController>()) != null, 30, "el selector de personaje");
                yield return Quieto(1f);
                SoloPintaLaEscena(selector.gameObject.scene.name, "el selector");
                Assert.AreEqual(DisplayStyle.None, raiz.resolvedStyle.display, "la interfaz del mundo se aparta");
                Assert.IsNull(raiz.Q(className: "partida-nombre"), "sin la tarjeta de la partida");
                var botones = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude);
                Assert.IsFalse(botones.Any(b => b.name.Contains("Create") || b.name.Contains("Delete")), "sin crear ni borrar héroe");
                var ficha = GameObject.Find("Heroe del motor");
                Assert.IsNotNull(ficha, "la ficha del héroe");
                var textos = ficha.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text).ToList();
                CollectionAssert.Contains(textos, cuenta.nombre, "su nombre");
                var linea = textos.First(t => t.StartsWith("Nivel "));
                StringAssert.Contains("Sin Facción", linea, "sin Facción");
                var clases = ClaseDeHeroe.Leer(conexion.Datos);
                StringAssert.Contains(clases.First(k => k.Id == cuenta.clase).Nombre, linea, "su clase");
                // El maniquí, con el género y las piezas del motor.
                var maniqui = (Transform)typeof(HeroSelectionSceneController).GetField("dummyRoot").GetValue(selector);
                foreach (var id in new[] { cuenta.avatar.CabezaId, cuenta.avatar.PeloId, cuenta.avatar.CejasId, cuenta.avatar.BarbaId }.Where(x => !string.IsNullOrEmpty(x)))
                    Assert.IsTrue(PiezaVisible(maniqui, id, cuenta.genero == "femenino"), $"la pieza {id} en el maniquí");
                // Y nada más que lo elegido en cada hueco: un hueco vacío (sin barba) se queda vacío, no con la pieza del maniquí.
                SoloLoElegido(maniqui, cuenta.avatar, cuenta.genero == "femenino", "en el selector");
                yield return CapturarEscena("CharacterselecctionScene", $"14_selector_{cuenta.clase}_{cuenta.genero}");
                if (n == 0)
                {
                    foreach (var (ancho, alto) in Tamanos) yield return CapturarEscena("CharacterselecctionScene", $"14b_selector_{ancho}x{alto}", ancho, alto);
                    // «Entrar al mundo» con el ratón: la carga y el mapa.
                    var entrada = UnityEngine.InputSystem.InputSystem.settings;
                    var fondoAntes = entrada.backgroundBehavior;
                    var editorAntes = entrada.editorInputBehaviorInPlayMode;
                    entrada.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                    entrada.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    var raton = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
                    try
                    {
                        raton.MakeCurrent();
                        LogAssert.ignoreFailingMessages = true; // el interior: el error de relleno del escuadrón vacío al nacer el héroe (ver EnElInterior)
                        yield return ClicUGUI(raton, selector.confirmButton);
                        yield return Esperar(() => (raiz.Q("mapa") != null || raiz.Q("interior") != null) && raiz.Q(className: "carga") == null, 60, "el mapa o el interior");
                    }
                    finally
                    {
                        UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                        entrada.backgroundBehavior = fondoAntes;
                        entrada.editorInputBehaviorInPlayMode = editorAntes;
                    }
                    // Nació en un campamento y sigue dentro: el interior; de ahí, «Salir al mundo» al mapa.
                    if (raiz.Q("interior") != null) yield return EnElInterior(raiz, "15a_interior_tras_el_selector", false);
                    Assert.IsFalse(UnityEngine.SceneManagement.SceneManager.GetSceneByName("CharacterselecctionScene").isLoaded, "el selector, descargado");
                    var mapa = ElMapa();
                    yield return Esperar(() => mapa.Centro != null && mapa.SueloCompleto, 60, "el suelo");
                    yield return Capturar("15_mapa_tras_el_selector");
                    yield return CapturarMapa(mapa, "15b_mapa_tras_el_selector");
                }
                // Cerrar sesión desde el mapa (o desde el selector, el segundo): de vuelta al acceso.
                if (n == 0)
                {
                    Pulsar(raiz.Q<Button>("cerrar-sesion"));
                    yield return null;
                    Pulsar(raiz.Q(className: "dialogo").Q<Button>(className: "boton-primario"));
                }
                else ((UnityEngine.UI.Button)typeof(HeroSelectionSceneController).GetField("exitButton", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(selector)).onClick.Invoke();
                yield return Esperar(() => raiz.Q<Button>("pestana-registro") != null && raiz.resolvedStyle.display == DisplayStyle.Flex, 15, "volver al acceso");
                Assert.IsFalse(UnityEngine.SceneManagement.SceneManager.GetSceneByName("CharacterselecctionScene").isLoaded);
                UnityEngine.Object.Destroy(go);
                go = null;
                yield return null;
            }
        }

        /// <summary>El campamento por dentro (paso 4B): las zonas con su cartel y la tecla F, la taberna, el mercado (comprar y verlo en tu almacén),
        /// la tropa (pedir prestada, reclutar o su motivo), los rechazos con su frase, y salir andando por la puerta con tropa, carga y víveres.</summary>
        [UnityTest]
        public IEnumerator ElCampamentoPorDentro()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            string nick = null;
            // El caso de Víctor (2026-11-13): un hombre con un pelo que no es el primero y sin barba; en el campamento tiene que salir igual.
            var aspecto = new BronzeAge.Contract.V1.OrdenCrearHeroe.Types.Avatar { CabezaId = "head_03", PeloId = "hair_02", BarbaId = "", CejasId = "eyebrow_01" };
            yield return OtroHeroe(url, "Cam" + DateTime.UtcNow.Ticks % 1_000_000, "lanza_escudo_redondo", "masculino", aspecto, x => nick = x);
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
            LogAssert.ignoreFailingMessages = true; // el error de relleno del escuadrón vacío al nacer el héroe en FeudoScene
            selector.confirmButton.onClick.Invoke();
            yield return Esperar(() => raiz.Q("interior") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior");
            LogAssert.ignoreFailingMessages = false;
            yield return Quieto(1f);
            var interior = SelectorDePersonaje.Interior;
            var heroe = UnityEngine.Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>(FindObjectsSortMode.None).First(x => x.IsLocalHero);
            // El héroe del campamento, con el aspecto que se eligió y nada más (ni la coleta ni la barba del maniquí).
            SoloLoElegido(heroe.transform, aspecto, false, "en el campamento");
            var campId = conexion.Sesion.Almacen.Buscar(BronzeAge.Contract.V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe.Ubicacion.CampamentoId;
            BronzeAge.Contract.V1.Heroe Yo() => conexion.Sesion.Almacen.Buscar(BronzeAge.Contract.V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            BronzeAge.Contract.V1.CampamentoMercenarios Camp() => conexion.Sesion.Almacen.Buscar(BronzeAge.Contract.V1.TipoEntidad.CampamentoMercenarios, campId).CampamentoMercenarios;
            foreach (var zona in new[] { "taberna", "mercado", "militar", "puerta" })
                Assert.IsNotNull(GameObject.Find("Zona " + zona), "la zona de " + zona);
            // Dentro, solo pinta la cámara de FeudoScene: la del mundo (y la del mapa) están apagadas.
            var camaras = Camera.allCameras.Where(x => x.isActiveAndEnabled).Select(x => $"{x.name} ({x.gameObject.scene.name})").ToList();
            Debug.Log("[Prueba] cámaras encendidas en el interior: " + string.Join(", ", camaras));
            Assert.IsTrue(Camera.allCameras.Where(x => x.isActiveAndEnabled).All(x => x.gameObject.scene.name == "FeudoScene"), "solo la cámara del interior");
            // El héroe recién nacido no tiene oro: el servidor de prueba le da 300 (orden de administración) para poder comprar y reclutar.
            yield return DarAlmacen(url, conexion.HeroeId, "oro", 300_000);
            yield return Esperar(() => Milesimas(Yo().AlmacenPersonal, "oro") >= 300 || Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null, 10, "el oro en el almacén");

            var entrada = UnityEngine.InputSystem.InputSystem.settings;
            var fondo = entrada.backgroundBehavior;
            entrada.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            var editorAntes = entrada.editorInputBehaviorInPlayMode;
            entrada.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var teclado = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            try
            {
                teclado.MakeCurrent();
                IEnumerator Tecla(UnityEngine.InputSystem.Key k)
                {
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
                    yield return null;
                    yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                    yield return null;
                }
                // Junto a una pieza: el héroe se coloca en el borde de su zona (andar entre las casas hasta ella no se prueba), el cartel y F.
                IEnumerator AbrirZona(string zona, string panel)
                {
                    // Andando de verdad desde donde está: el camino libre con el radio del héroe y el teclado (W/A/S/D) por él.
                    yield return AndarHasta(teclado, heroe.transform, zona, () => interior.ZonaCercana == zona);
                    yield return Esperar(() => interior.ZonaCercana == zona && !raiz.Q<Label>("cartel").ClassListContains("oculto"), 5, "el cartel de " + zona);
                    yield return CapturarEscena("FeudoScene", $"28_andando_hasta_{zona}");
                    yield return Capturar($"21_cartel_{zona}");
                    yield return Tecla(UnityEngine.InputSystem.Key.F);
                    yield return Esperar(() => raiz.Q(panel) != null, 5, "el panel de " + zona);
                    Assert.IsTrue(interior.PanelAbierto && DialogueUIState.IsDialogueOpen, "con el panel abierto, el héroe no anda");
                    yield return null;
                }

                // La taberna: residentes y que resides (el héroe nace residente: no hay «Residir aquí»).
                yield return AbrirZona("taberna", "panel-taberna");
                StringAssert.Contains("tu residencia", raiz.Q("panel-taberna").Q<Label>(className: "dialogo-texto").text);
                Assert.IsNull(raiz.Q("panel-taberna").Q<Button>("residir"), "ya resides: no sale «Residir aquí»");
                yield return Capturar("22_taberna");
                yield return EnTamanos(raiz, "22_taberna", () => DentroDelMarco(raiz.Q("panel-taberna"), "la taberna"));
                Pulsar(raiz.Q("panel-taberna").Q<Button>("cerrar"));
                yield return null;
                Assert.IsFalse(interior.PanelAbierto || DialogueUIState.IsDialogueOpen, "al cerrar vuelve el control");
                // Residir: el servidor de prueba le quita la residencia (`fijar_residencia` sin residencia) y en la taberna sale «Residir aquí».
                var sinResidencia = new Google.Protobuf.WellKnownTypes.Struct();
                sinResidencia.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(conexion.HeroeId);
                yield return Administrar(url, "fijar_residencia", sinResidencia);
                if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") != null)
                {
                    yield return Esperar(() => !Camp().ResidentesIds.Contains(conexion.HeroeId), 10, "sin residencia");
                    yield return Tecla(UnityEngine.InputSystem.Key.F);
                    yield return Esperar(() => raiz.Q("panel-taberna")?.Q<Button>("residir") != null, 5, "«Residir aquí»");
                    yield return Capturar("22b_taberna_sin_residir");
                    Pulsar(raiz.Q("panel-taberna").Q<Button>("residir"));
                    yield return Esperar(() => Camp().ResidentesIds.Contains(conexion.HeroeId), 10, "residir aceptado");
                    yield return Esperar(() => raiz.Q("panel-taberna")?.Q<Button>("residir") == null, 5, "la taberna repintada");
                    StringAssert.Contains("tu residencia", raiz.Q("panel-taberna").Q<Label>(className: "dialogo-texto").text);
                    yield return Capturar("22c_taberna_residiendo");
                    Pulsar(raiz.Q("panel-taberna").Q<Button>("cerrar"));
                    yield return null;
                }

                // El mercado: comprar una unidad y verla en tu almacén.
                yield return AbrirZona("mercado", "panel-mercado");
                var mercado = raiz.Q("panel-mercado");
                yield return Capturar("23_mercado");
                yield return EnTamanos(raiz, "23_mercado", () => DentroDelMarco(mercado, "el mercado"));
                var comprar = mercado.Q<Button>("comprar");
                if (comprar != null)
                {
                    var bien = mercado.Query<Button>().ToList().First(b => b.name.StartsWith("bien-") && b.ClassListContains("clase--elegida")).name.Substring(5);
                    var antes = Yo().AlmacenPersonal.PorId.TryGetValue(bien, out var a0) ? a0 : 0;
                    mercado.Q<SliderInt>("cantidad").value = 1;
                    Pulsar(comprar);
                    yield return Esperar(() => (Yo().AlmacenPersonal.PorId.TryGetValue(bien, out var a1) ? a1 : 0) > antes, 10, "lo comprado en tu almacén");
                    yield return Esperar(() => raiz.Q("panel-mercado").Q<Label>("ultima-compra") != null, 5, "la línea de la compra");
                    Debug.Log($"[Prueba] compra: {raiz.Q("panel-mercado").Q<Label>("ultima-compra").text}");
                    yield return Capturar("23b_mercado_comprado");
                }
                else Debug.Log("[Prueba] mercado sin «Comprar»: " + string.Join(" | ", mercado.Query<Label>().ToList().Select(l => l.text)));
                Pulsar(raiz.Q("panel-mercado").Q<Button>("cerrar"));
                yield return null;

                // La tropa (tanda 3 del paso 6): el árbol de reclutamiento entero, con el ratón de verdad. Pasar el ratón por una carta ilumina su
                // camino y saca su ficha corta; un clic abre su ficha a la derecha; «Pedir prestada» y «Reclutar» desde ella.
                yield return AbrirZona("militar", "panel-tropa");
                var raton = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
                raton.MakeCurrent();
                try
                {
                    VisualElement Tropa() => raiz.Q("panel-tropa");
                    yield return Esperar(() => Tropa()?.Q("recl-tablero") != null, 5, "el árbol");
                    var arbol = Tropa().Q("recl-tablero");
                    var cartas = arbol.Query<Button>().ToList().Where(x => x.name.StartsWith("nodo-")).ToList();
                    Debug.Log($"[Prueba] árbol: {cartas.Count} tropas; " + string.Join(", ", cartas.Select(x => x.name.Substring(5) + "=" + new[] { "lista", "tuya", "edificio", "bloqueada", "futura" }.First(x.ClassListContains))));
                    Assert.GreaterOrEqual(cartas.Count, 30, "el árbol entero");
                    foreach (var carril in new[] { "centroUrbano", "barracon", "galeriaDeTiro", "caballerizas" }) Assert.IsNotNull(arbol.Q("carril-" + carril), "el carril " + carril);
                    Assert.IsNotNull(arbol.Q("nodo-milicia_lanceros"), "la milicia en el carril del campamento");
                    yield return EnTamanos(raiz, "24_tropa", () => DentroDelMarco(Tropa(), "la tropa"));
                    // Pasar el ratón: la ficha corta y el camino iluminado.
                    // Una carta a la vista, con camino (la de los espadachines de bronce, o la primera del Barracón que se vea).
                    var vista = Tropa().Q("recl-marco").worldBound;
                    var otra = new[] { "nodo-espadachines_bronce", "nodo-espadachines_cobre" }.Select(n => arbol.Q<Button>(n)).FirstOrDefault(x => x != null && vista.Contains(x.worldBound.center))
                        ?? cartas.First(x => vista.Contains(x.worldBound.center));
                    yield return MoverRaton(raton, raiz, otra, () => Tropa().Q("recl-tip").resolvedStyle.display == DisplayStyle.Flex && arbol.ClassListContains("activo"));
                    yield return Capturar("24a_tropa_al_pasar");
                    // «Ver todas las conexiones».
                    yield return ClicFijo(raton, raiz, Tropa().Q<Button>("ver-todas"));
                    yield return Esperar(() => Tropa().Q<Button>("ver-todas").ClassListContains("activo"), 5, "todas las conexiones");
                    yield return Capturar("24b_tropa_todas");
                    yield return ClicFijo(raton, raiz, Tropa().Q<Button>("ver-todas"));
                    // Arrastrar el tablero con el ratón (desde el nombre de un carril, que se queda a la vista) y volver.
                    float Desplazado() => Tropa().Q<ScrollView>("recl-marco").scrollOffset.x;
                    var antesX = Desplazado();
                    yield return ArrastrarUITK(raton, raiz, Tropa().Q("carril-centroUrbano"), -300);
                    yield return Esperar(() => Desplazado() > antesX + 100, 3, "el tablero arrastrado");
                    yield return Capturar("24b2_tropa_arrastrada");
                    yield return ArrastrarUITK(raton, raiz, Tropa().Q("carril-centroUrbano"), 600);
                    yield return Esperar(() => Desplazado() < 1, 3, "el tablero de vuelta");
                    // La milicia: su ficha y «Pedir prestada».
                    yield return ClicFijo(raton, raiz, Tropa().Q<Button>("nodo-milicia_lanceros"));
                    yield return Esperar(() => Tropa().Q<Button>("pedir-milicia_lanceros") != null, 5, "«Pedir prestada» en la ficha");
                    yield return ClicFijo(raton, raiz, Tropa().Q<Button>("pedir-milicia_lanceros"));
                    yield return Esperar(() => Yo().EscuadronesIds.Count > 0, 10, "la escuadra prestada");
                    yield return Esperar(() => Tropa()?.Q<Label>("estado-milicia_lanceros")?.text.StartsWith("Prestada") == true, 5, "la carta, prestada");
                    Assert.IsNull(Tropa().Q<Button>("pedir-milicia_lanceros"), "ya no se pide otra vez");
                    yield return EnTamanos(raiz, "24c_tropa_prestada", () => DentroDelMarco(Tropa(), "la tropa con la prestada"));
                    // Reclutar lo que ofrece el campamento (o su motivo si no llega).
                    var lista = Tropa().Query<Button>().ToList().FirstOrDefault(x => x.name.StartsWith("nodo-") && x.Q<Label>("estado-" + x.name.Substring(5))?.text.StartsWith("Lista") == true);
                    if (lista != null)
                    {
                        var id = lista.name.Substring(5);
                        yield return ClicFijo(raton, raiz, lista);
                        yield return Esperar(() => Tropa().Q<Button>("reclutar-" + id) != null, 5, "«Reclutar» en la ficha");
                        yield return Capturar("24d_tropa_ficha_reclutar");
                        var reclutar = Tropa().Q<Button>("reclutar-" + id);
                        if (reclutar.enabledSelf)
                        {
                            var n = Yo().EscuadronesIds.Count;
                            yield return ClicFijo(raton, raiz, reclutar);
                            yield return Esperar(() => Yo().EscuadronesIds.Count > n, 10, "la escuadra reclutada");
                            yield return Esperar(() => Tropa()?.Q<Label>("estado-" + id)?.text.StartsWith("Tu escuadra") == true, 5, "la carta, tuya");
                            yield return Capturar("24e_tropa_reclutada");
                        }
                        else Debug.Log("[Prueba] reclutar: " + Tropa().Q<Label>("motivo-" + id)?.text);
                    }
                    else Debug.Log("[Prueba] el campamento aún no ofrece tropa que reclutar");
                    // Atributos de la tropa elegida.
                    yield return ClicFijo(raton, raiz, Tropa().Q<Button>("subpestana-atributos"));
                    yield return Esperar(() => Tropa().Q<Button>("subpestana-atributos").ClassListContains("activo"), 5, "Atributos");
                    yield return EnTamanos(raiz, "24f_tropa_atributos");
                    yield return ClicFijo(raton, raiz, Tropa().Q<Button>("subpestana-caracteristicas"));
                    yield return ClicFijo(raton, raiz, Tropa().Q<Button>("cerrar"));
                    yield return Esperar(() => Tropa() == null && !interior.PanelAbierto, 5, "la tropa cerrada");
                }
                finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(raton); }

                // Otra vez en la taberna, ya con la milicia: «tu tropa» con su miniatura de Conquest. Andar hasta ella ya se probó arriba;
                // aquí se abre por la misma puerta que la tecla F (la de la zona), para la captura.
                interior.AlInteractuar("taberna");
                yield return Esperar(() => raiz.Q("panel-taberna") != null, 5, "el panel de la taberna");
                yield return Esperar(() => raiz.Q("panel-taberna").Q("mini-milicia_lanceros") != null, 5, "la miniatura de la milicia en la taberna");
                yield return EnTamanos(raiz, "22d_taberna_con_tropa");
                Pulsar(raiz.Q("panel-taberna").Q<Button>("cerrar"));
                yield return null;

                // Un rechazo de cada orden, con su frase (lo que la interfaz no deja pedir se manda tal cual).
                foreach (var (orden, codigo) in new[]
                {
                    (new BronzeAge.Contract.V1.Orden { ResidirEnCampamento = new BronzeAge.Contract.V1.OrdenResidirEnCampamento { HeroeId = conexion.HeroeId, CampamentoId = campId } }, "residencia."),
                    (new BronzeAge.Contract.V1.Orden { ComprarEnCampamento = new BronzeAge.Contract.V1.OrdenComprarEnCampamento { Recurso = "trigo", Cantidad = 0, CampamentoId = campId, PagarCon = "almacenPersonal" } }, "mercenarios."),
                    (new BronzeAge.Contract.V1.Orden { ReclutarEnCampamento = new BronzeAge.Contract.V1.OrdenReclutarEnCampamento { TropaId = Camp().Reclutables.Count > 0 ? Camp().Reclutables[0].TropaId : "milicia_lanceros", PagarCon = "almacenPersonal" } }, "mercenarios."),
                    (new BronzeAge.Contract.V1.Orden { PedirPrestamo = new BronzeAge.Contract.V1.OrdenPedirPrestamo { TropaIds = { "milicia_lanceros" } } }, "mercenarios."),
                })
                {
                    var r = conexion.Sesion.Ordenar(orden);
                    yield return Esperar(() => r.IsCompleted, 10, "la respuesta");
                    Assert.AreEqual(BronzeAge.Contract.V1.Resultado.OrdenRechazada, r.Result.Resultado, orden.ToString());
                    var frase = Textos.Explicar(r.Result.Rechazo);
                    Debug.Log($"[Prueba] rechazo {r.Result.Rechazo.Codigo}: {frase}");
                    StringAssert.StartsWith(codigo, r.Result.Rechazo.Codigo);
                    Assert.AreNotEqual(r.Result.Rechazo.Codigo, frase, "con su frase en español");
                }

                // Salir andando por la puerta: S hasta salir del hueco, el cartel, F y la ventana con la tropa, la carga y los víveres.
                yield return AndarHasta(teclado, heroe.transform, "puerta", () => interior.ZonaCercana == "puerta");
                yield return Quieto(0.3f);

                yield return Capturar("25_cartel_puerta");
                yield return Tecla(UnityEngine.InputSystem.Key.F);
                yield return Esperar(() => raiz.Q("ventana-salida") != null, 5, "la ventana de salida");
                var ventana = raiz.Q("ventana-salida");
                // El riel de tropa, como el del prototipo: la escuadra en su tarjeta con su ✕; quitarla deja el «+», y con él se vuelve a poner.
                List<VisualElement> Huecos() => ventana.Query(className: "salida-hueco-tropa").ToList();
                Assert.Greater(Huecos().Count, 0, "con tus escuadras para elegir");
                Assert.IsNotNull(ventana.Q("velocidad"), "la velocidad de la columna");
                Debug.Log($"[Prueba] salida: {ventana.Q<Label>("liderazgo").text}; velocidad {string.Join(" ", ventana.Q("velocidad").Query<Label>().ToList().Select(l => l.text))}");
                var primera = Huecos()[0].name.Substring("hueco-".Length);
                Pulsar(ventana.Q<Button>("quitar-" + primera));
                yield return null;
                Assert.AreEqual(0, Huecos().Count(x => x.name == "hueco-" + primera), "quitada con su ✕");
                Assert.IsNotNull(ventana.Q<Button>("salida-mas"), "y sale el «+» para volver a ponerla");
                yield return Capturar("26c_salida_quitada");
                Pulsar(ventana.Q<Button>("salida-mas"));
                yield return null;
                yield return Capturar("26d_salida_elegir");
                Pulsar(ventana.Q<Button>("anadir-" + primera));
                yield return null;
                Assert.AreEqual(1, Huecos().Count(x => x.name == "hueco-" + primera), "puesta otra vez con el «+»");
                var campos = ventana.Query<IntegerField>().ToList();
                string cargado = null;
                if (campos.Count > 0) { campos[0].value = 1; cargado = campos[0].name.Substring("carga-".Length); }
                yield return null;
                yield return Capturar("26_salir_con_tropa_y_carga");
                yield return EnTamanos(raiz, "26_salir_con_tropa_y_carga", () => DentroDelMarco(ventana, "la ventana de salida"));
                var repostar = ventana.Q<Button>("repostar");
                Debug.Log($"[Prueba] víveres: {ventana.Q<Label>("viveres").text}; repostar {(repostar != null && Visible(repostar) ? "visible" : "oculto")}");
                if (repostar != null && Visible(repostar))
                {
                    var antes = ventana.Q<Label>("viveres").text;
                    Pulsar(repostar);
                    yield return Esperar(() => ventana.Q<Label>("viveres").text != antes, 10, "los víveres repostados");
                    Debug.Log($"[Prueba] víveres tras repostar: {ventana.Q<Label>("viveres").text}");
                    yield return Capturar("26b_viveres_repostados");
                }
                var elegidas = Huecos().Count;
                Pulsar(ventana.Q<Button>("salir"));
                yield return Esperar(() => raiz.Q("mapa") != null && !UnityEngine.SceneManagement.SceneManager.GetSceneByName("FeudoScene").isLoaded, 30, "el mapa tras salir");
                var mapa = ElMapa();
                yield return Esperar(() => mapa.Columna != null, 10, "la columna fuera");
                Assert.AreEqual(elegidas, mapa.Columna.EscuadronIds.Count, "la columna sale con las escuadras elegidas");
                if (cargado != null) Assert.GreaterOrEqual(mapa.Columna.Suministro.PorId.TryGetValue(cargado, out var q) ? q : 0, 1000, "con la carga elegida en el carro");
                Debug.Log($"[Prueba] salió con {mapa.Columna.EscuadronIds.Count} escuadras y carro {mapa.Columna.Suministro}");
                yield return Esperar(() => mapa.SueloCompleto, 30, "el suelo");
                yield return CapturarMapa(mapa, "27_fuera_con_tropa");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(teclado);
                entrada.backgroundBehavior = fondo;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
            }
        }

        /// <summary>El selector de personaje sin el héroe del motor: ya no lista los héroes de la sesión local (tanda 3 de las clases y los objetos,
        /// 23a). Solo vive en el flujo del motor.</summary>
        [UnityTest]
        public IEnumerator ElSelectorSinElMotorNoTraeLosHeroesViejos()
        {
            SelectorDePersonaje.Heroe = null;
            var jugador = new PlayerData();
            jugador.heroes.Add(HeroDataService.CreateNewHero("Viejo", new AvatarParts { headId = "head_00", hairId = "hair_01", eyebrowId = "eyebrow_01", beardId = "" }, "espada_escudo_mediano", "Male", new Equipment()));
            PlayerSessionService.SetPlayer(jugador);
            try
            {
                var carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("CharacterselecctionScene", UnityEngine.SceneManagement.LoadSceneMode.Additive);
                while (!carga.isDone) yield return null;
                yield return null;
                yield return null;
                Assert.IsNull(GameObject.Find("Heroe del motor"));
                var selector = UnityEngine.Object.FindAnyObjectByType<HeroSelectionSceneController>();
                Assert.IsFalse(selector.heroButtonContainer.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text == "Viejo"), "sin los héroes de player_save.json");
                var descarga = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("CharacterselecctionScene");
                while (!descarga.isDone) yield return null;
            }
            finally { PlayerSessionService.Clear(); }
        }

        /// <summary>Dentro de un campamento de mercenarios: su interior en FeudoScene con el héroe del motor andando (W, con un teclado del Input
        /// System), la cabecera, y «Salir al mundo» con su ventana hasta el mapa con la columna fuera. `completo`: también a los cuatro tamaños,
        /// andando, con el rendimiento.</summary>
        IEnumerator EnElInterior(VisualElement raiz, string prefijo, bool completo)
        {
            // FeudoScene trae errores de relleno que no son de aquí: el escuadrón vacío del héroe al nacer.
            LogAssert.ignoreFailingMessages = true;
            var conexion = UnityEngine.Object.FindAnyObjectByType<ConexionDeMundo>();
            for (var fin = Time.realtimeSinceStartup + 30; raiz.Q("interior") == null && Time.realtimeSinceStartup < fin;) yield return null;
            if (raiz.Q("interior") == null)
            {
                var h = conexion.Sesion.Almacen.Buscar(BronzeAge.Contract.V1.TipoEntidad.Heroe, conexion.HeroeId)?.Heroe;
                Debug.Log($"[Prueba] sin interior: ubicación {h?.Ubicacion}; ámbitos {string.Join(", ", conexion.Sesion.Almacen.Ambitos.Keys)}");
                yield return Capturar(prefijo + "_sin_interior");
            }
            yield return Esperar(() => raiz.Q("interior") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior con el héroe dentro");
            LogAssert.ignoreFailingMessages = false;
            Assert.IsNull(raiz.Q("mapa"), "sin el mapa");
            Assert.IsFalse(string.IsNullOrEmpty(raiz.Q<Label>("lugar").text), "dónde estás");
            StringAssert.Contains("Campamento de mercenarios", raiz.Q<Label>("lugar-detalle").text);
            var feudo = UnityEngine.SceneManagement.SceneManager.GetSceneByName("FeudoScene");
            Assert.IsTrue(feudo.isLoaded, "FeudoScene, cargada encima");
            foreach (var apagado in new[] { "UICanvas", "MinimapCanvas", "NPCs", "Matchmaking", "TestEnv" })
                Assert.IsFalse(feudo.GetRootGameObjects().Where(g => g.name == apagado).Any(g => g.activeSelf), apagado + ", apagado");
            Assert.IsNull(PlayerSessionService.SelectedHero, "sin héroe de player_save.json");
            var campamento = GameObject.Find("Campamento del motor");
            Assert.IsNotNull(campamento, "el campamento montado desde la planta");
            Assert.Greater(campamento.transform.childCount, 15, "con sus edificios, calles y empalizada");
            var heroe = UnityEngine.Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>(FindObjectsSortMode.None).First(x => x.IsLocalHero).transform;
            yield return Quieto(1f);
            yield return CapturarEscena("FeudoScene", prefijo);
            yield return Capturar(prefijo + "_cabecera");
            if (completo)
            {
                yield return EnTamanos(raiz, prefijo + "_cabecera");
                foreach (var (ancho, alto) in Tamanos) yield return CapturarEscena("FeudoScene", $"{prefijo}_{ancho}x{alto}", ancho, alto);
                // Andar: W un rato, con un teclado del Input System.
                var fondo = UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior;
                UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                var teclado = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
                try
                {
                    teclado.MakeCurrent();
                    var antes = heroe.position;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
                    var tiempos = new System.Collections.Generic.List<float>();
                    for (var fin = Time.realtimeSinceStartup + 2.5f; Time.realtimeSinceStartup < fin;) { yield return null; tiempos.Add(Time.unscaledDeltaTime * 1000); }
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                    yield return Quieto(0.5f);
                    var andado = Vector3.Distance(new Vector3(antes.x, 0, antes.z), new Vector3(heroe.position.x, 0, heroe.position.z));
                    Assert.Greater(andado, 1f, "el héroe anda con W");
                    tiempos.Sort();
                    Debug.Log($"[Interior] andado {andado:0.0} m; fotograma mediana {tiempos[tiempos.Count / 2]:0.0} ms, p95 {tiempos[(int)(tiempos.Count * 0.95)]:0.0} ms, peor {tiempos[tiempos.Count - 1]:0.0} ms ({tiempos.Count} fotogramas)");
                    yield return CapturarEscena("FeudoScene", prefijo + "_andando");
                }
                finally
                {
                    UnityEngine.InputSystem.InputSystem.RemoveDevice(teclado);
                    UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = fondo;
                }
            }
            // «Salir al mundo»: su ventana y, al aceptarse, el mapa con la columna fuera.
            Pulsar(raiz.Q<Button>("salir-al-mundo"));
            yield return null;
            Assert.IsNotNull(raiz.Q("ventana-salida"), "la ventana de salida");
            yield return Capturar(prefijo + "_salir_al_mundo");
            if (completo) yield return EnTamanos(raiz, prefijo + "_salir_al_mundo");
            Pulsar(raiz.Q("ventana-salida").Q<Button>("salir"));
            yield return Esperar(() => raiz.Q("mapa") != null && !UnityEngine.SceneManagement.SceneManager.GetSceneByName("FeudoScene").isLoaded, 30, "el mapa tras salir");
            Assert.IsNull(SelectorDePersonaje.Interior, "sin interior");
            Assert.IsFalse(UnityEngine.Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>(FindObjectsSortMode.None).Any(x => x.IsLocalHero), "sin el héroe del interior");
        }

        /// <summary>¿Está a la vista en el maniquí la pieza `id` de AvatarPartDatabase, para ese género?</summary>
        /// <summary>En cada hueco del aspecto (cara, pelo, cejas, barba) se ve la pieza elegida y ninguna otra; si se eligió vacío, ninguna.</summary>
        static void SoloLoElegido(Transform maniqui, BronzeAge.Contract.V1.OrdenCrearHeroe.Types.Avatar avatar, bool femenino, string donde)
        {
            var db = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
            var visibles = maniqui.GetComponentsInChildren<Transform>(false).Select(t => t.name).ToHashSet();
            foreach (var (lista, elegida, hueco) in new[] { (db.faceParts, avatar.CabezaId, "cabeza"), (db.hairParts, avatar.PeloId, "pelo"),
                         (db.eyebrowsParts, avatar.CejasId, "cejas"), (db.beardParts, avatar.BarbaId, "barba") })
            {
                if (hueco == "barba" && femenino) continue;
                var vistas = lista.Where(d => d.attachments.Any(a => visibles.Contains(femenino ? a.prefabPathFemale : a.prefabPathMale))).Select(d => d.id).ToList();
                var esperadas = string.IsNullOrEmpty(elegida) ? new List<string>() : new List<string> { elegida };
                CollectionAssert.AreEquivalent(esperadas, vistas, $"{hueco} {donde}: solo lo elegido ({(string.IsNullOrEmpty(elegida) ? "nada" : elegida)})");
            }
        }

        static bool PiezaVisible(Transform maniqui, string id, bool femenino)
        {
            var db = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
            var def = new[] { db.faceParts, db.hairParts, db.eyebrowsParts, db.beardParts }.SelectMany(l => l).FirstOrDefault(d => d.id == id);
            Assert.IsNotNull(def, "la pieza " + id + " en AvatarPartDatabase");
            var visibles = maniqui.GetComponentsInChildren<Transform>(false).Select(t => t.name).ToHashSet();
            return def.attachments.Any(a => visibles.Contains(femenino ? a.prefabPathFemale : a.prefabPathMale));
        }

        /// <summary>Con capturas (`MUNDO_CAPTURAS`, las pruebas nuevas de una tanda), los cuatro tamaños; sin ellas (la suite de regresión), solo el
        /// más estrecho, donde antes se sale algo de su sitio.</summary>
        static readonly (int ancho, int alto)[] Tamanos = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MUNDO_CAPTURAS"))
            ? new[] { (1280, 720) }
            : new[] { (1280, 720), (1600, 900), (1920, 1080), (1950, 1130) };

        /// <summary>La pantalla en cada tamaño de ventana: todo lo que se pulsa o se escribe queda dentro de la ventana y de su placa (lo de dentro de
        /// una lista con barra no cuenta: se ve al bajar), y `comprobar` para lo propio de cada pantalla. Una captura por tamaño.</summary>
        IEnumerator EnTamanos(VisualElement raiz, string nombre, Action comprobar = null)
        {
            var doc = go.GetComponentInChildren<UIDocument>();
            var original = rt;
            var usadas = new System.Collections.Generic.List<RenderTexture>();
            foreach (var (ancho, alto) in Tamanos)
            {
                rt = new RenderTexture(ancho, alto, 24);
                rt.Create();
                // Una textura nueva trae lo que hubiera en esa memoria de la tarjeta: se limpia (el panel solo limpia lo que pinta).
                var activa = RenderTexture.active;
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
                RenderTexture.active = activa;
                usadas.Add(rt);
                doc.panelSettings.targetTexture = rt;
                yield return null;
                yield return null;
                yield return null;
                DentroDelMarco(raiz, $"{nombre} a {ancho}x{alto}");
                comprobar?.Invoke();
                yield return Capturar($"{nombre}_{ancho}x{alto}");
            }
            rt = original;
            doc.panelSettings.targetTexture = rt;
            yield return null;
            foreach (var r in usadas) r.Release();
        }

        static bool Visible(VisualElement e)
        {
            for (var x = e; x != null; x = x.parent)
                if (x.resolvedStyle.display == DisplayStyle.None || x.ClassListContains("oculto")) return false;
            return e.worldBound.width > 0 && e.worldBound.height > 0;
        }

        static bool Contiene(Rect fuera, Rect dentro) =>
            dentro.xMin >= fuera.xMin - 0.5f && dentro.yMin >= fuera.yMin - 0.5f && dentro.xMax <= fuera.xMax + 0.5f && dentro.yMax <= fuera.yMax + 0.5f;

        static void DentroDelMarco(VisualElement raiz, string donde)
        {
            var ventana = raiz.worldBound;
            int vistos = 0;
            foreach (var e in raiz.Query<VisualElement>().Where(x => x is Button || x is TextField || x is Toggle).ToList())
            {
                if (!Visible(e) || e.GetFirstAncestorOfType<ScrollView>() != null) continue;
                vistos++;
                var que = $"«{(e as Button)?.text ?? e.name}» en {donde}";
                Assert.IsTrue(Contiene(ventana, e.worldBound), $"{que}: dentro de la ventana ({e.worldBound} en {ventana})");
                VisualElement placa = e.parent;
                while (placa != null && !placa.ClassListContains("placa")) placa = placa.parent;
                if (placa != null) Assert.IsTrue(Contiene(placa.worldBound, e.worldBound), $"{que}: dentro de su placa ({e.worldBound} en {placa.worldBound})");
            }
            Assert.Greater(vistos, 0, "algo que pulsar en " + donde);

            // Las piezas de tropa ni se pisan entre sí ni se cortan por los lados (en su lista con barra, se ven enteras de ancho).
            var piezas = raiz.Query<VisualElement>().Where(x => (x.ClassListContains("tropa-con-accion") || x.ClassListContains("tropa-mini")) && Visible(x)).ToList();
            for (int i = 0; i < piezas.Count; i++)
            {
                var a = piezas[i].worldBound;
                var marco = piezas[i].GetFirstAncestorOfType<ScrollView>()?.contentViewport.worldBound ?? ventana;
                Assert.IsTrue(a.xMin >= marco.xMin - 1 && a.xMax <= marco.xMax + 1, $"«{piezas[i].name}» en {donde}: entera de ancho ({a} en {marco})");
                for (int j = i + 1; j < piezas.Count; j++)
                {
                    var b = piezas[j].worldBound;
                    if (piezas[i].Contains(piezas[j]) || piezas[j].Contains(piezas[i])) continue;
                    bool pisa = a.xMin < b.xMax - 1 && b.xMin < a.xMax - 1 && a.yMin < b.yMax - 1 && b.yMin < a.yMax - 1;
                    Assert.IsFalse(pisa, $"«{piezas[i].name}» y «{piezas[j].name}» se pisan en {donde} ({a} y {b})");
                }
            }
        }

        /// <summary>El pie de «Nace tu héroe»: el campo del nombre y «Nacer» dentro del panel, con alto, y sin pisarse.</summary>
        static void Pie(VisualElement raiz)
        {
            var panel = raiz.Q(className: "panel-derecha").worldBound;
            var campo = raiz.Q<TextField>("nombre").worldBound;
            var boton = raiz.Q<Button>("nacer").worldBound;
            Assert.IsTrue(Contiene(panel, campo), $"el campo del nombre dentro del panel ({campo} en {panel})");
            Assert.IsTrue(Contiene(panel, boton), $"«Nacer» dentro del panel ({boton} en {panel})");
            Assert.IsFalse(campo.Overlaps(boton), "el campo y «Nacer» no se pisan");
            Assert.Greater(campo.height, 20, "el campo con su alto");
        }

        static IEnumerator ClicUGUI(UnityEngine.InputSystem.Mouse raton, UnityEngine.UI.Selectable s)
        {
            Assert.IsNotNull(s);
            var r = (RectTransform)s.transform;
            Vector2 donde = RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center));
            Assert.IsTrue(new Rect(0, 0, Screen.width, Screen.height).Contains(donde), $"«{s.name}» dentro de la pantalla ({donde} en {Screen.width}x{Screen.height})");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde });
            yield return null;
            yield return Clic(raton, donde);
        }

        static UnityEngine.UI.Button Boton(AvatarCreatorUIController creador, string campo) =>
            (UnityEngine.UI.Button)typeof(AvatarCreatorUIController).GetField(campo, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(creador);

        static string Error(AvatarCreatorUIController creador)
        {
            var t = (TMPro.TMP_Text)typeof(AvatarCreatorUIController).GetField("errorDelMotor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(creador);
            return t != null ? t.text : "";
        }

        /// <summary>El creador de personaje (UGUI, en pantalla): su lienzo pasa un momento a la cámara de su escena, que pinta en una textura.</summary>
        IEnumerator CapturarCreador(string nombre, int ancho = 1600, int alto = 900) => CapturarEscena("AvatarCreator", nombre, ancho, alto);

        /// <summary>Una escena UGUI de Conquest (el creador o el selector), igual: su cámara, a una textura del tamaño pedido.</summary>
        IEnumerator CapturarEscena(string escena, string nombre, int ancho = 1600, int alto = 900)
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            yield return null;
            if (string.IsNullOrEmpty(carpeta) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            var raices = UnityEngine.SceneManagement.SceneManager.GetSceneByName(escena).GetRootGameObjects();
            var cam = raices.SelectMany(g => g.GetComponentsInChildren<Camera>()).FirstOrDefault(x => x.isActiveAndEnabled && x.targetTexture == null);
            Assert.IsNotNull(cam, "la cámara de " + escena);
            var lienzos = raices.SelectMany(g => g.GetComponentsInChildren<Canvas>()).Where(x => x.isRootCanvas && x.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            var destino = new RenderTexture(ancho, alto, 24);
            foreach (var l in lienzos) { l.renderMode = RenderMode.ScreenSpaceCamera; l.worldCamera = cam; l.planeDistance = cam.nearClipPlane + 0.05f; }
            cam.targetTexture = destino;
            yield return null;
            yield return null;
            // Lo que se pulsa, dentro de la imagen a este tamaño.
            var esquinas = new Vector3[4];
            var fuera = new System.Collections.Generic.List<string>();
            foreach (var s in raices.SelectMany(g => g.GetComponentsInChildren<UnityEngine.UI.Selectable>()).Where(x => x.isActiveAndEnabled))
            {
                ((RectTransform)s.transform).GetWorldCorners(esquinas);
                foreach (var e in esquinas)
                {
                    var p = cam.WorldToScreenPoint(e);
                    if (!(p.x >= -1 && p.y >= -1 && p.x <= ancho + 1 && p.y <= alto + 1)) { fuera.Add($"«{s.name}» ({p})"); break; }
                }
            }
            cam.targetTexture = null;
            foreach (var l in lienzos) l.renderMode = RenderMode.ScreenSpaceOverlay;
            RenderTexture.active = destino;
            var t = new Texture2D(destino.width, destino.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, destino.width, destino.height), 0, 0);
            t.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(carpeta);
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
            destino.Release();
            Assert.IsEmpty(fuera, $"lo que se pulsa, dentro de {escena} a {ancho}x{alto} (captura {nombre})");
            yield return null;
        }

        static void Pulsar(VisualElement b)
        {
            Assert.IsNotNull(b);
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = b;
                b.SendEvent(e);
            }
        }

        /// <summary>Una cámara como la de MundoScene («Camara»: profundidad 0, fondo liso #0b0805) en el mundo de la prueba. Sin ella, la prueba no
        /// veía que esa cámara tapaba de negro el creador de personaje (el fallo que vio Víctor el 2026-11-13).</summary>
        static void CamaraComoLaDeMundoScene(GameObject mundo)
        {
            var cam = new GameObject("Camara").AddComponent<Camera>();
            cam.transform.SetParent(mundo.transform, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0b, 0x08, 0x05, 0xff);
            cam.depth = 0;
        }

        /// <summary>Con una escena de Conquest encima (el creador, el selector), solo pintan sus cámaras: las del mundo están apagadas.</summary>
        static void SoloPintaLaEscena(string escena, string que)
        {
            var otras = Camera.allCameras.Where(x => x.isActiveAndEnabled && x.gameObject.scene.name != escena).Select(x => $"{x.name} ({x.gameObject.scene.name}, profundidad {x.depth})").ToList();
            Assert.IsEmpty(otras, $"con {que} abierto solo pintan sus cámaras");
            Assert.IsTrue(Camera.allCameras.Any(x => x.isActiveAndEnabled && x.gameObject.scene.name == escena), $"{que} tiene su cámara");
        }

        /// <summary>El texto que se ve en un botón: el suyo o, si lleva glifo, el de su etiqueta.</summary>
        static string TextoDe(Button b) => string.IsNullOrEmpty(b.text) ? b.Q<Label>()?.text : b.text;

        /// <summary>El mapa en pantalla. Tiene que haber uno solo: con dos (uno olvidado por otra prueba), mirar «cualquiera» falla a veces.</summary>
        static ConquestTactics.Mundo.Mapa.MapaDeMundo ElMapa()
        {
            var mapas = UnityEngine.Object.FindObjectsByType<ConquestTactics.Mundo.Mapa.MapaDeMundo>(FindObjectsInactive.Include);
            Assert.AreEqual(1, mapas.Length, "un solo mapa en la escena: " + string.Join(", ", mapas.Select(m => m.name)));
            return mapas[0];
        }

        /// <summary>Como `Esperar`, para lo que es tiempo del motor (una marcha, una obra): mientras espera, el reloj del mundo corre `pasoMs` por cada
        /// 0,3 s (el factor del reloj, no saltos: un salto cuenta desde el reloj de la sesión, que va por detrás hasta su siguiente ping). Al acabar,
        /// el reloj vuelve a su paso de antes.</summary>
        static IEnumerator EsperarConReloj(string url, Sesion s, Func<bool> hasta, float segundos, string que, long pasoMs = 60_000)
        {
            if (hasta()) yield break;
            var antes = (long)Math.Round(s.FactorReloj * 1000);
            yield return Reloj(url, (long)(pasoMs / 0.3));
            var fin = Time.realtimeSinceStartup + segundos;
            while (!hasta() && Time.realtimeSinceStartup <= fin) yield return null;
            yield return Reloj(url, Math.Max(1_000, antes));
            if (!hasta()) Assert.Fail("no llegó " + que);
        }

        /// <summary>Lo que antes era una espera fija de `max` segundos para que la pantalla se asiente (tras un clic, al entrar): espera a que la
        /// interfaz deje de cambiar (cuántos elementos tiene y qué textos enseña, igual durante 0,25 s), sin pasar de `max`.</summary>
        static IEnumerator Quieto(float max)
        {
            var raiz = UnityEngine.Object.FindAnyObjectByType<UIDocument>()?.rootVisualElement;
            var fin = Time.realtimeSinceStartup + max;
            string antes = null;
            var desde = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < fin)
            {
                yield return null;
                var lista = raiz?.Query<VisualElement>().ToList();
                var firma = lista == null ? "" : lista.Count + ":" + lista.OfType<TextElement>().Sum(t => t.text?.Length ?? 0);
                if (firma != antes) { antes = firma; desde = Time.realtimeSinceStartup; }
                else if (Time.realtimeSinceStartup - desde >= 0.25f) yield break;
            }
        }

        static IEnumerator Esperar(Func<bool> hasta, float segundos, string que)
        {
            var fin = Time.realtimeSinceStartup + segundos;
            while (!hasta())
            {
                if (Time.realtimeSinceStartup > fin) Assert.Fail("no llegó " + que);
                yield return null;
            }
        }

        /// <summary>Lleva al héroe andando hasta una zona del interior con el teclado del Input System: busca un camino libre en una rejilla de
        /// 0,5 m con el radio del héroe (más un margen) contra las colisiones del campamento (edificios, empalizada) y lo sigue pulsando W/A/S/D
        /// según la cámara. Falla si no hay camino o si se queda encajado.</summary>
        static IEnumerator AndarHasta(UnityEngine.InputSystem.Keyboard teclado, Transform heroe, string zona, Func<bool> llegado)
        {
            var campamento = GameObject.Find("Campamento del motor").transform;
            var destino = GameObject.Find("Zona " + zona).GetComponent<BoxCollider>().bounds;
            var radio = heroe.GetComponent<CharacterController>().radius + 0.15f;
            var suelo = campamento.Find("Suelo").GetComponent<Collider>().bounds;
            const float paso = 0.5f;
            int nx = (int)(suelo.size.x / paso), nz = (int)(suelo.size.z / paso);
            var y = heroe.position.y;
            Vector3 Centro(int i, int k) => new Vector3(suelo.min.x + (i + 0.5f) * paso, y, suelo.min.z + (k + 0.5f) * paso);
            var libre = new bool?[nx, nz];
            bool Libre(int i, int k)
            {
                if (i < 0 || k < 0 || i >= nx || k >= nz) return false;
                if (libre[i, k] == null)
                {
                    var p = Centro(i, k);
                    libre[i, k] = !Physics.OverlapCapsule(p + Vector3.up * (radio + 0.25f), p + Vector3.up * 1.6f, radio, ~0, QueryTriggerInteraction.Ignore)
                        .Any(c => c.transform.IsChildOf(campamento) && c.name != "Suelo");
                }
                return libre[i, k].Value;
            }
            (int, int) Celda(Vector3 p) => ((int)((p.x - suelo.min.x) / paso), (int)((p.z - suelo.min.z) / paso));
            var inicio = Celda(heroe.position);
            var previo = new Dictionary<(int, int), (int, int)> { [inicio] = inicio };
            var cola = new Queue<(int, int)>();
            cola.Enqueue(inicio);
            (int, int)? meta = null;
            while (cola.Count > 0 && meta == null)
            {
                var (i, k) = cola.Dequeue();
                var c = Centro(i, k);
                if (c.x > destino.min.x + 0.3f && c.x < destino.max.x - 0.3f && c.z > destino.min.z + 0.3f && c.z < destino.max.z - 0.3f && Libre(i, k)) { meta = (i, k); break; }
                foreach (var (di, dk) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) })
                {
                    var n = (i + di, k + dk);
                    if (previo.ContainsKey(n) || !Libre(n.Item1, n.Item2)) continue;
                    if (di != 0 && dk != 0 && (!Libre(i + di, k) || !Libre(i, k + dk))) continue; // sin cortar esquinas
                    previo[n] = (i, k);
                    cola.Enqueue(n);
                }
            }
            Assert.IsNotNull(meta, $"hay camino libre andando hasta la zona de {zona}");
            var camino = new List<Vector3>();
            for (var x = meta.Value; x != inicio; x = previo[x]) camino.Add(Centro(x.Item1, x.Item2));
            camino.Reverse();
            Debug.Log($"[Prueba] camino hasta {zona}: {camino.Count * paso:0} m aprox.");
            var fin = Time.realtimeSinceStartup + 60;
            int siguiente = 0;
            var atasco = (pos: heroe.position, t: Time.realtimeSinceStartup);
            while (!llegado())
            {
                Assert.Less(Time.realtimeSinceStartup, fin, $"llegar andando a {zona}");
                while (siguiente < camino.Count - 1 && Vector2.Distance(new Vector2(heroe.position.x, heroe.position.z), new Vector2(camino[siguiente].x, camino[siguiente].z)) < 0.6f) siguiente++;
                var d = camino[Math.Min(siguiente + 1, camino.Count - 1)] - heroe.position;
                d.y = 0;
                var cam = Camera.main.transform;
                var f = cam.forward; f.y = 0; f.Normalize();
                var r = cam.right; r.y = 0; r.Normalize();
                float a = Vector3.Dot(d.normalized, f), b = Vector3.Dot(d.normalized, r);
                var teclas = new List<UnityEngine.InputSystem.Key>();
                if (a > 0.38f) teclas.Add(UnityEngine.InputSystem.Key.W);
                if (a < -0.38f) teclas.Add(UnityEngine.InputSystem.Key.S);
                if (b > 0.38f) teclas.Add(UnityEngine.InputSystem.Key.D);
                if (b < -0.38f) teclas.Add(UnityEngine.InputSystem.Key.A);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(teclas.ToArray()));
                yield return null;
                // Encajado: tres segundos sin moverse medio metro.
                if (Vector3.Distance(heroe.position, atasco.pos) > 0.5f) atasco = (heroe.position, Time.realtimeSinceStartup);
                Assert.Less(Time.realtimeSinceStartup - atasco.t, 3f, $"el héroe se queda encajado yendo a {zona} en {heroe.position}");
            }
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null;
        }

        static string tokenAdmin;

        /// <summary>Pone recursos en el almacén personal de un héroe con la orden de administración del motor (`dar_almacen_personal`), desde una
        /// sesión de administración con una cuenta admin del servidor de prueba (se registra con el código de un solo uso que imprime al arrancar,
        /// en `BRONZEAGE_PRUEBA_ADMIN`). Sin código, no hace nada y lo dice.</summary>
        static double Milesimas(BronzeAge.Contract.V1.Cantidades q, string r) => q != null && q.PorId.TryGetValue(r, out var v) ? v / 1000.0 : 0;

        static IEnumerator DarAlmacen(string url, string heroeId, string recurso, long milesimas)
        {
            var p = new Google.Protobuf.WellKnownTypes.Struct();
            p.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(heroeId);
            p.Fields["recurso"] = Google.Protobuf.WellKnownTypes.Value.ForString(recurso);
            p.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(milesimas);
            yield return Administrar(url, "dar_almacen_personal", p);
        }

        /// <summary>Una orden de administración del motor (`OrdenDeAdministracion`), desde una sesión admin del servidor de prueba.</summary>
        static IEnumerator Administrar(string url, string ordenAdmin, Google.Protobuf.WellKnownTypes.Struct parametros, Action<BronzeAge.Contract.V1.ResultadoOrden> resultado = null)
        {
            var codigo = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN");
            if (string.IsNullOrEmpty(codigo)) { Debug.Log("[Prueba] sin BRONZEAGE_PRUEBA_ADMIN: no se manda " + ordenAdmin); yield break; }
            var acceso = new Acceso(url);
            if (tokenAdmin == null)
            {
                var nick = "admin-" + DateTime.UtcNow.Ticks % 100_000_000;
                var registro = acceso.Registrar(nick, "clave-de-prueba-1", codigo);
                yield return Esperar(() => registro.IsCompleted, 20, "la cuenta admin");
                var login = acceso.Entrar(nick, "clave-de-prueba-1");
                yield return Esperar(() => login.IsCompleted, 20, "el login admin");
                tokenAdmin = login.Result.Token;
            }
            var s = new Sesion(acceso.UrlSesion, tokenAdmin, "conquest-prueba") { Tipo = BronzeAge.Contract.V1.TipoCliente.ClienteAdminWeb };
            var abrir = s.Abrir();
            var fin = Time.realtimeSinceStartup + 20;
            while (!abrir.IsCompleted && Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
            var orden = s.Ordenar(new BronzeAge.Contract.V1.Orden { Administracion = new BronzeAge.Contract.V1.OrdenDeAdministracion { Codigo = ordenAdmin, Parametros = parametros } });
            fin = Time.realtimeSinceStartup + 20;
            while (!orden.IsCompleted && Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
            // «Sin cambios» (dar recursos a un almacén lleno, …) no es un fallo: ya está como se pedía. Con `resultado`, lo mira quien llama.
            if (resultado != null) resultado(orden.Result);
            else if (orden.Result.Resultado != BronzeAge.Contract.V1.Resultado.OrdenSinCambios)
                Assert.AreEqual(BronzeAge.Contract.V1.Resultado.OrdenAceptada, orden.Result.Resultado, orden.Result.Rechazo?.ToString());
            s.Cerrar();
        }

        /// <summary>Otro héroe, con otra cuenta (para tener un nombre ya usado, o una cuenta con héroe): su clase, género y aspecto, y `nickDe`
        /// recibe el nick de la cuenta (la clave es «clave-de-prueba-1»).</summary>
        static IEnumerator OtroHeroe(string url, string nombre, string clase = "espada_escudo_mediano", string genero = "",
            BronzeAge.Contract.V1.OrdenCrearHeroe.Types.Avatar avatar = null, Action<string> nickDe = null)
        {
            var acceso = new Acceso(url);
            var nick = "otro-" + DateTime.UtcNow.Ticks % 100_000_000;
            nickDe?.Invoke(nick);
            var registro = acceso.Registrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => registro.IsCompleted, 20, "la otra cuenta");
            var login = acceso.Entrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => login.IsCompleted, 20, "el otro login");
            var s = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
            var abrir = s.Abrir();
            var fin = Time.realtimeSinceStartup + 20;
            while (!abrir.IsCompleted && Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
            var orden = s.Ordenar(new BronzeAge.Contract.V1.Orden { CrearHeroe = new BronzeAge.Contract.V1.OrdenCrearHeroe { DisplayName = nombre, CampamentoId = abrir.Result.CampamentosParaElegir[0].CampamentoId, ClassDefinitionId = clase, Genero = genero, Avatar = avatar ?? new BronzeAge.Contract.V1.OrdenCrearHeroe.Types.Avatar() } });
            fin = Time.realtimeSinceStartup + 20;
            while (!orden.IsCompleted && Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
            Assert.AreEqual(BronzeAge.Contract.V1.Resultado.OrdenAceptada, orden.Result.Resultado, orden.Result.Rechazo?.Codigo);
            s.Cerrar();
        }

        static IEnumerator Clic(UnityEngine.InputSystem.Mouse raton, Vector2 donde)
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true));
            yield return null;
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde });
            yield return null;
            yield return null;
        }

        static IEnumerator Rueda(UnityEngine.InputSystem.Mouse raton, Vector2 donde)
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde, scroll = new Vector2(0, 120) });
            yield return null;
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde });
            yield return null;
        }

        /// <summary>Lo que ve la cámara del mapa (sin la interfaz, que va a otra textura).</summary>
        IEnumerator CapturarMapa(ConquestTactics.Mundo.Mapa.MapaDeMundo mapa, string nombre)
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            yield return null;
            if (string.IsNullOrEmpty(carpeta) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            var cam = mapa.Vista;
            var destino = new RenderTexture(1600, 900, 24);
            var antes = cam.targetTexture;
            cam.targetTexture = destino;
            yield return null;
            yield return null;
            cam.targetTexture = antes;
            RenderTexture.active = destino;
            var t = new Texture2D(destino.width, destino.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, destino.width, destino.height), 0, 0);
            t.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(carpeta);
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
            destino.Release();
        }

        /// <summary>Cuánto tarda un fotograma con el mapa cargado, marchando (en el registro de la prueba).</summary>
        static IEnumerator MedirFotogramas(ConquestTactics.Mundo.Mapa.MapaDeMundo mapa)
        {
            var tiempos = new System.Collections.Generic.List<float>();
            for (int i = 0; i < 300; i++) { yield return null; tiempos.Add(Time.unscaledDeltaTime * 1000); }
            tiempos.Sort();
            Debug.Log($"[Mapa] {mapa.TeselasCargadas} teselas; fotograma mediana {tiempos[150]:0.0} ms, p95 {tiempos[285]:0.0} ms, peor {tiempos[299]:0.0} ms");
        }

        IEnumerator Capturar(string nombre)
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            yield return null; // en modo batch WaitForEndOfFrame no llega nunca
            if (string.IsNullOrEmpty(carpeta) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = antes;
            Directory.CreateDirectory(carpeta);
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
        }
    }
}
