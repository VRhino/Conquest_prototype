// Las pantallas de fuera de la batalla de punta a punta contra un servidor del motor propio (`BRONZEAGE_PRUEBA_URL`; nunca el de juego):
// crear cuenta, ver la partida, entrar con su pantalla de carga, la sesión viva, el panel de avisos y cerrar sesión con su confirmación.
// Si `MUNDO_CAPTURAS` dice una carpeta, guarda allí una captura de cada pantalla (para mirarlas; hace falta correr sin -nographics).
using System;
using System.Collections;
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
    public class PantallasDeMundoTests
    {
        static readonly string[] Preferencias = { "mundo.token", "mundo.nick", "mundo.servidor" };
        string[] guardadas;
        GameObject go;
        RenderTexture rt;

        [SetUp]
        public void Guardar() => guardadas = Array.ConvertAll(Preferencias, k => PlayerPrefs.GetString(k, null));

        [TearDown]
        public void Restaurar()
        {
            // Lo que tuviera guardado quien corre la prueba (su login de verdad) se queda como estaba.
            for (int i = 0; i < Preferencias.Length; i++)
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
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capturar("4_carga");
            // Sin héroe: «Nace tu héroe» (paso 3), solo el lugar y el nombre, sobre el mapa de los campamentos.
            yield return Esperar(() => raiz.Q("crear") != null && raiz.Q(className: "carga") == null, 60, "crear el héroe");
            Assert.IsNotNull(conexion.Artefacto, "el artefacto de mundo, cargado");
            var mapaCrear = UnityEngine.Object.FindAnyObjectByType<ConquestTactics.Mundo.Mapa.MapaDeMundo>();
            yield return Esperar(() => mapaCrear.Centro != null && mapaCrear.SueloCompleto, 60, "el suelo de los campamentos");
            yield return new WaitForSecondsRealtime(1f);
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
                yield return new WaitForSecondsRealtime(1f);
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
                yield return new WaitForSecondsRealtime(1.5f);
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
                yield return new WaitForSecondsRealtime(1f);
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
                yield return new WaitForSecondsRealtime(1f);
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
            var mapa = UnityEngine.Object.FindAnyObjectByType<ConquestTactics.Mundo.Mapa.MapaDeMundo>();
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
            yield return new WaitForSecondsRealtime(1.5f);
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
                yield return new WaitForSecondsRealtime(0.5f);
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
                yield return new WaitForSecondsRealtime(0.5f);
                var botones = raiz.Q("ficha-acciones").Query<Button>().ToList().Select(b => b.text).ToList();
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
                yield return new WaitForSecondsRealtime(2f);
                var ahora = mapa.Centro.Value;
                Assert.Greater(Math.Abs(ahora.x - desde.x) + Math.Abs(ahora.y - desde.y), 0.1, "la columna anda hacia donde se hizo clic");
                Assert.Greater(mapa.PuntosDeRuta, 1, "con su ruta");
                yield return CapturarMapa(mapa, "11_mapa_marchando");

                // Volver a entrar: otra vez hasta el campamento («Marchar aquí» en su ficha) y, a la puerta, «Entrar»: de nuevo el interior.
                mapa.Seleccionar(campamento);
                yield return Esperar(() => !raiz.Q("ficha").ClassListContains("oculto"), 5, "la ficha del campamento");
                yield return null;
                Pulsar(raiz.Q("ficha-acciones").Query<Button>().ToList().First(b => b.text == "Marchar aquí"));
                // Solo lo que se puede hacer: mientras la columna llega, «Entrar» no está; al parar a la puerta, sí.
                yield return Esperar(() => mapa.Columna?.Estado == BronzeAge.Contract.V1.EstadoColumna.ColumnaMarchando, 10, "la columna en marcha hacia el campamento");
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsFalse(raiz.Q("ficha-acciones").Query<Button>().ToList().Any(b => b.text == "Entrar"), "con la columna en marcha no se puede entrar");
                yield return Capturar("17a_llegando_sin_entrar");
                yield return Esperar(() => raiz.Q("ficha-acciones")?.Query<Button>().ToList().Any(b => b.text == "Entrar") == true, 90, "«Entrar» con la columna parada a la puerta");
                Assert.AreEqual(BronzeAge.Contract.V1.EstadoColumna.ColumnaEstacionada, mapa.Columna.Estado, "«Entrar» sale con la columna parada");
                yield return Capturar("17_entrar_a_la_puerta");
                Pulsar(raiz.Q("ficha-acciones").Query<Button>().ToList().First(b => b.text == "Entrar"));
                yield return EnElInterior(raiz, "17_de_vuelta", false);
                mapa = UnityEngine.Object.FindAnyObjectByType<ConquestTactics.Mundo.Mapa.MapaDeMundo>();
                yield return Esperar(() => mapa.Centro != null && mapa.SueloCompleto, 60, "el suelo otra vez");
                // Y a marchar otra vez, con un clic en el suelo.
                yield return new WaitForSecondsRealtime(0.5f);
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
            yield return new WaitForSecondsRealtime(2f);
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

            Pulsar(raiz.Q<Button>("cerrar-sesion"));
            yield return null;
            Assert.IsNotNull(raiz.Q(className: "dialogo"), "cerrar sesión pide confirmación");
            yield return Capturar("7_confirmar");
            Pulsar(raiz.Q(className: "dialogo").Q<Button>(className: "boton-primario"));
            yield return Esperar(() => raiz.Q<Button>("pestana-registro") != null, 10, "volver al acceso");
            Assert.IsFalse(conexion.HayToken, "el token se olvida");
        }

        /// <summary>El creador de personaje de Conquest sin el motor (como se abre desde CharacterSelection): sus 4 clases de relleno y el nombre
        /// editable, como antes. No se pulsa «Continuar»: escribiría `player_save.json` de verdad.</summary>
        [UnityTest]
        public IEnumerator ElCreadorDeConquestSigueConSusClases()
        {
            SelectorDePersonaje.Actual = null;
            var carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("AvatarCreator", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            while (!carga.isDone) yield return null;
            yield return null;
            yield return null;
            var selector = UnityEngine.Object.FindAnyObjectByType<HeroClassSelector>();
            var creador = UnityEngine.Object.FindAnyObjectByType<AvatarCreatorUIController>();
            Assert.AreEqual(4, selector.availableClasses.Count, "las 4 clases de relleno");
            Assert.IsNotNull(selector.selectedClass, "con una elegida al empezar");
            Assert.IsNull(selector.ClaseDelMotor);
            Assert.IsFalse(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude).Any(b => b.name.StartsWith("Clase ")), "sin las del motor");
            Assert.IsTrue(creador.heroNameInput.interactable, "el nombre se escribe aquí");
            yield return CapturarCreador("0_creador_de_conquest_sin_motor");
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
                yield return new WaitForSecondsRealtime(1f);
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
                    var mapa = UnityEngine.Object.FindAnyObjectByType<ConquestTactics.Mundo.Mapa.MapaDeMundo>();
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
            yield return OtroHeroe(url, "Cam" + DateTime.UtcNow.Ticks % 1_000_000, "arco_lanza", "femenino",
                new BronzeAge.Contract.V1.OrdenCrearHeroe.Types.Avatar { CabezaId = "head_01", PeloId = "hair_03", CejasId = "eyebrow_02" }, x => nick = x);
            foreach (var k in Preferencias) PlayerPrefs.DeleteKey(k);
            go = new GameObject("Mundo");
            go.SetActive(false);
            var conexion = go.AddComponent<ConexionDeMundo>();
            typeof(ConexionDeMundo).GetField("servidor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(conexion, url);
            go.AddComponent<PantallasDeMundo>();
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
            yield return new WaitForSecondsRealtime(1f);
            var interior = SelectorDePersonaje.Interior;
            var heroe = UnityEngine.Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>(FindObjectsSortMode.None).First(x => x.IsLocalHero);
            var campId = conexion.Sesion.Almacen.Buscar(BronzeAge.Contract.V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe.Ubicacion.CampamentoId;
            BronzeAge.Contract.V1.Heroe Yo() => conexion.Sesion.Almacen.Buscar(BronzeAge.Contract.V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            BronzeAge.Contract.V1.CampamentoMercenarios Camp() => conexion.Sesion.Almacen.Buscar(BronzeAge.Contract.V1.TipoEntidad.CampamentoMercenarios, campId).CampamentoMercenarios;
            foreach (var zona in new[] { "taberna", "mercado", "militar", "puerta" })
                Assert.IsNotNull(GameObject.Find("Zona " + zona), "la zona de " + zona);
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
                    var caja = GameObject.Find("Zona " + zona).GetComponent<BoxCollider>().bounds;
                    var cc = heroe.GetComponent<CharacterController>();
                    cc.enabled = false;
                    heroe.transform.position = new Vector3(caja.center.x, heroe.transform.position.y, caja.min.z + 0.8f);
                    cc.enabled = true;
                    yield return Esperar(() => interior.ZonaCercana == zona && !raiz.Q<Label>("cartel").ClassListContains("oculto"), 5, "el cartel de " + zona);
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

                // La tropa: pedir prestada una de leva comunal y verla; reclutar (o su motivo si no llega).
                yield return AbrirZona("militar", "panel-tropa");
                yield return Capturar("24_tropa");
                yield return EnTamanos(raiz, "24_tropa", () => DentroDelMarco(raiz.Q("panel-tropa"), "la tropa"));
                var pedir = raiz.Q("panel-tropa").Q<Button>("pedir-milicia_lanceros");
                Assert.IsNotNull(pedir, "se puede pedir prestada la milicia de lanceros");
                Pulsar(pedir);
                yield return Esperar(() => Yo().EscuadronesIds.Count > 0, 10, "la escuadra prestada");
                yield return Esperar(() => raiz.Q("panel-tropa")?.Q("tropa-milicia_lanceros")?.Q<Button>("pedir-milicia_lanceros") == null, 5, "la ficha repintada");
                StringAssert.Contains("prestada", raiz.Q("panel-tropa").Q("tropa-milicia_lanceros").Q<Label>().text);
                yield return Capturar("24b_tropa_prestada");
                var reclutar = raiz.Q("panel-tropa").Query<Button>().ToList().FirstOrDefault(b => b.name.StartsWith("reclutar-") && b.enabledSelf);
                if (reclutar != null)
                {
                    var n = Yo().EscuadronesIds.Count;
                    Pulsar(reclutar);
                    yield return Esperar(() => Yo().EscuadronesIds.Count > n, 10, "la escuadra reclutada");
                    yield return new WaitForSecondsRealtime(0.6f);
                    yield return Capturar("24c_tropa_reclutada");
                }
                else Debug.Log("[Prueba] reclutar: " + string.Join(" | ", raiz.Q("panel-tropa").Query<Label>().ToList().Where(l => l.name.StartsWith("motivo-")).Select(l => l.text)));
                Pulsar(raiz.Q("panel-tropa").Q<Button>("cerrar"));
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
                var cc2 = heroe.GetComponent<CharacterController>();
                var puerta = GameObject.Find("Zona puerta").GetComponent<BoxCollider>().bounds;
                cc2.enabled = false;
                heroe.transform.position = new Vector3(puerta.center.x, heroe.transform.position.y, puerta.max.z + 4f);
                cc2.enabled = true;
                yield return new WaitForSecondsRealtime(0.3f);
                // De cara a la puerta (al sur): la cámara mira al norte, así que es S.
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.S));
                yield return Esperar(() => interior.ZonaCercana == "puerta", 10, "la zona de la puerta andando");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capturar("25_cartel_puerta");
                yield return Tecla(UnityEngine.InputSystem.Key.F);
                yield return Esperar(() => raiz.Q("ventana-salida") != null, 5, "la ventana de salida");
                var ventana = raiz.Q("ventana-salida");
                var marcas = ventana.Query<Toggle>().ToList();
                Assert.Greater(marcas.Count, 0, "con tus escuadras para elegir");
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
                var elegidas = marcas.Where(m => m.value).Count();
                Pulsar(ventana.Q<Button>("salir"));
                yield return Esperar(() => raiz.Q("mapa") != null && !UnityEngine.SceneManagement.SceneManager.GetSceneByName("FeudoScene").isLoaded, 30, "el mapa tras salir");
                var mapa = UnityEngine.Object.FindAnyObjectByType<ConquestTactics.Mundo.Mapa.MapaDeMundo>();
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

        /// <summary>El selector de personaje sin el motor (el flujo viejo): los héroes de la sesión local con crear y borrar.</summary>
        [UnityTest]
        public IEnumerator ElSelectorDeConquestSigueConSusHeroes()
        {
            SelectorDePersonaje.Heroe = null;
            var claseVieja = Resources.LoadAll<HeroClassDefinition>("Data/HeroClasses").First();
            var jugador = new PlayerData();
            jugador.heroes.Add(HeroDataService.CreateNewHero("Viejo", new AvatarParts { headId = "head_00", hairId = "hair_01", eyebrowId = "eyebrow_01", beardId = "" }, claseVieja.name, "Male", new Equipment()));
            PlayerSessionService.SetPlayer(jugador);
            try
            {
                var carga = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("CharacterselecctionScene", UnityEngine.SceneManagement.LoadSceneMode.Additive);
                while (!carga.isDone) yield return null;
                yield return null;
                yield return null;
                var botones = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Exclude);
                Assert.IsTrue(botones.Any(b => b.name.Contains("Create")) && botones.Any(b => b.name.Contains("Delete")), "con crear y borrar");
                Assert.IsNull(GameObject.Find("Heroe del motor"));
                // A 1920×1080, el tamaño para el que está hecho (con píxeles fijos; a otro tamaño se sale, como siempre: no se toca).
                yield return CapturarEscena("CharacterselecctionScene", "0b_selector_de_conquest_sin_motor", 1920, 1080);
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
            yield return new WaitForSecondsRealtime(1f);
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
                    yield return new WaitForSecondsRealtime(0.5f);
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
        static bool PiezaVisible(Transform maniqui, string id, bool femenino)
        {
            var db = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
            var def = new[] { db.faceParts, db.hairParts, db.eyebrowsParts, db.beardParts }.SelectMany(l => l).FirstOrDefault(d => d.id == id);
            Assert.IsNotNull(def, "la pieza " + id + " en AvatarPartDatabase");
            var visibles = maniqui.GetComponentsInChildren<Transform>(false).Select(t => t.name).ToHashSet();
            return def.attachments.Any(a => visibles.Contains(femenino ? a.prefabPathFemale : a.prefabPathMale));
        }

        static readonly (int ancho, int alto)[] Tamanos = { (1280, 720), (1600, 900), (1920, 1080), (1950, 1130) };

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
                usadas.Add(rt);
                doc.panelSettings.targetTexture = rt;
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

        static IEnumerator Esperar(Func<bool> hasta, float segundos, string que)
        {
            var fin = Time.realtimeSinceStartup + segundos;
            while (!hasta())
            {
                if (Time.realtimeSinceStartup > fin) Assert.Fail("no llegó " + que);
                yield return null;
            }
        }

        static string tokenAdmin;

        /// <summary>Pone recursos en el almacén personal de un héroe con la orden de administración del motor (`dar_almacen_personal`), desde una
        /// sesión de administración con una cuenta admin del servidor de prueba (se registra con el código de un solo uso que imprime al arrancar,
        /// en `BRONZEAGE_PRUEBA_ADMIN`). Sin código, no hace nada y lo dice.</summary>
        static double Milesimas(BronzeAge.Contract.V1.Cantidades q, string r) => q != null && q.PorId.TryGetValue(r, out var v) ? v / 1000.0 : 0;

        static IEnumerator DarAlmacen(string url, string heroeId, string recurso, long milesimas)
        {
            var codigo = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN");
            if (string.IsNullOrEmpty(codigo)) { Debug.Log("[Prueba] sin BRONZEAGE_PRUEBA_ADMIN: no se da " + recurso); yield break; }
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
            var parametros = new Google.Protobuf.WellKnownTypes.Struct();
            parametros.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(heroeId);
            parametros.Fields["recurso"] = Google.Protobuf.WellKnownTypes.Value.ForString(recurso);
            parametros.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(milesimas);
            var orden = s.Ordenar(new BronzeAge.Contract.V1.Orden { Administracion = new BronzeAge.Contract.V1.OrdenDeAdministracion { Codigo = "dar_almacen_personal", Parametros = parametros } });
            fin = Time.realtimeSinceStartup + 20;
            while (!orden.IsCompleted && Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
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
