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
            if (!UnityEngine.SceneManagement.SceneManager.GetSceneByName("AvatarCreator").isLoaded) yield break;
            var descarga = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("AvatarCreator");
            while (descarga != null && !descarga.isDone) yield return null;
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
                creador.sliderController.hairSlider.value = 2;
                yield return null;
                var aspecto = creador.sliderController.avatarPartSelector.GetCurrentSelection();
                Assert.IsNotEmpty(aspecto.hairId, "el pelo, con su id de AvatarPartDatabase");
                StringAssert.Contains(raiz.Q<TextField>("nombre").value, creador.heroNameInput.text, "el nombre, como texto");
                Assert.IsFalse(creador.heroNameInput.interactable, "que no se edita allí");
                yield return CapturarCreador("8d_creador_listo");
                yield return ClicUGUI(ratonCrear, Boton(creador, "continueButton"));
                yield return Esperar(() => raiz.Q("mapa") != null && raiz.Q(className: "carga") == null, 60, "el mapa");
                Assert.IsNull(UnityEngine.Object.FindAnyObjectByType<AvatarCreatorUIController>(), "el creador, descargado");
                var heroe = conexion.Sesion.Almacen.Todas(BronzeAge.Contract.V1.Entidad.CuerpoOneofCase.Heroe).Select(e => e.Heroe).FirstOrDefault(h => h.Id == conexion.HeroeId);
                if (heroe != null) Assert.AreEqual(claseElegida, heroe.ClassDefinitionId, "el héroe nace con la clase elegida en el creador");
                else Debug.Log("[Prueba] el héroe aún no está en el almacén: la clase no se comprueba");
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

            // Dentro del campamento: dónde estás y la salida.
            yield return Esperar(() => !raiz.Q("dentro").ClassListContains("oculto"), 5, "el aviso de dentro");
            StringAssert.Contains("campamento", raiz.Q<Label>("dentro-texto").text);
            yield return CapturarMapa(mapa, "9_mapa_dentro_del_campamento");
            yield return Capturar("9b_interfaz_dentro");
            Pulsar(raiz.Q<Button>("salir"));
            yield return Esperar(() => mapa.Columna != null, 10, "la columna al salir");
            yield return Esperar(() => raiz.Q("dentro").ClassListContains("oculto"), 5, "que se vaya el aviso de dentro");

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
        IEnumerator CapturarCreador(string nombre)
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            yield return null;
            if (string.IsNullOrEmpty(carpeta) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            var raices = UnityEngine.SceneManagement.SceneManager.GetSceneByName("AvatarCreator").GetRootGameObjects();
            var cam = raices.SelectMany(g => g.GetComponentsInChildren<Camera>()).FirstOrDefault(x => x.isActiveAndEnabled && x.targetTexture == null);
            Assert.IsNotNull(cam, "la cámara del creador");
            var lienzos = raices.SelectMany(g => g.GetComponentsInChildren<Canvas>()).Where(x => x.isRootCanvas && x.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            var destino = new RenderTexture(1600, 900, 24);
            foreach (var l in lienzos) { l.renderMode = RenderMode.ScreenSpaceCamera; l.worldCamera = cam; l.planeDistance = cam.nearClipPlane + 0.05f; }
            cam.targetTexture = destino;
            yield return null;
            yield return null;
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

        /// <summary>Otro héroe, con otra cuenta, para tener un nombre ya usado.</summary>
        static IEnumerator OtroHeroe(string url, string nombre)
        {
            var acceso = new Acceso(url);
            var nick = "otro-" + DateTime.UtcNow.Ticks % 100_000_000;
            var registro = acceso.Registrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => registro.IsCompleted, 20, "la otra cuenta");
            var login = acceso.Entrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => login.IsCompleted, 20, "el otro login");
            var s = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
            var abrir = s.Abrir();
            var fin = Time.realtimeSinceStartup + 20;
            while (!abrir.IsCompleted && Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
            var clase = "espada_escudo_mediano";
            var orden = s.Ordenar(new BronzeAge.Contract.V1.Orden { CrearHeroe = new BronzeAge.Contract.V1.OrdenCrearHeroe { DisplayName = nombre, CampamentoId = abrir.Result.CampamentosParaElegir[0].CampamentoId, ClassDefinitionId = clase } });
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
