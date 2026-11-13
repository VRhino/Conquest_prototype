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

            Pulsar(raiz.Q<Button>("entrar"));
            yield return Esperar(() => raiz.Q(className: "carga") != null, 5, "la pantalla de carga");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capturar("4_carga");
            // Sin héroe: crear el héroe (paso 3) sobre el mapa de los campamentos.
            yield return Esperar(() => raiz.Q("crear") != null && raiz.Q(className: "carga") == null, 60, "crear el héroe");
            Assert.IsNotNull(conexion.Artefacto, "el artefacto de mundo, cargado");
            var mapaCrear = UnityEngine.Object.FindAnyObjectByType<ConquestTactics.Mundo.Mapa.MapaDeMundo>();
            yield return Esperar(() => mapaCrear.Centro != null && mapaCrear.SueloCompleto, 60, "el suelo de los campamentos");
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsTrue(raiz.Q<Button>("nacer").ClassListContains("oculto"), "sin campamento ni clase, «Nacer» no sale");
            StringAssert.Contains("Elige campamento y clase", raiz.Q<Label>("falta").text);
            yield return Capturar("5_crear_heroe");
            yield return CapturarMapa(mapaCrear, "5b_mapa_de_campamentos");

            // Las 5 clases del motor, cada una con su equipo inicial.
            var botonesClase = raiz.Q("clases").Query<Button>().ToList();
            Assert.AreEqual(5, botonesClase.Count, "las 5 clases de /v1/datos");
            for (int i = 0; i < botonesClase.Count; i++)
            {
                Pulsar(botonesClase[i]);
                yield return null;
                Assert.IsTrue(botonesClase[i].ClassListContains("clase--elegida"));
                StringAssert.Contains("Caballo de tiro", raiz.Q<Label>("clase-equipo").text, "con su equipo inicial");
                yield return Capturar($"6_clase_{i + 1}");
            }

            // El campamento 4 (`camp-4` en la Bienvenida; en el artefacto, `campamento-4`: un río a 15 u y un lago a 1 u), desde la lista: su ficha con lo que lo rodea, y ya se puede nacer.
            var tarjeta = raiz.Q<ScrollView>("campamentos").Query<Button>().ToList().First(t => t.Q<Label>().text == "Campamento 4");
            Pulsar(tarjeta);
            yield return Esperar(() => mapaCrear.Seleccionada?.Id == "camp-4", 5, "el campamento elegido");
            Assert.Greater(raiz.Q("campamento-datos").childCount, 5, "lo que rodea al campamento");
            Assert.IsFalse(raiz.Q<Button>("nacer").ClassListContains("oculto"), "con campamento y clase, «Nacer» sale");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capturar("7_campamento_elegido");
            yield return CapturarMapa(mapaCrear, "7b_campamento_elegido_en_el_mapa");

            // Un nombre que ya tiene otro héroe: el rechazo, junto al campo.
            var ocupado = "Ocu" + nick.Substring(3);
            yield return OtroHeroe(url, ocupado);
            raiz.Q<TextField>("nombre").value = ocupado;
            Pulsar(raiz.Q<Button>("nacer"));
            yield return Esperar(() => !raiz.Q<Label>("nombre-error").ClassListContains("oculto"), 10, "el rechazo del nombre");
            StringAssert.Contains("nombre", raiz.Q<Label>("nombre-error").text);
            yield return Capturar("8_nombre_en_uso");

            // Nace: al aceptarse, el mapa con el héroe dentro de su campamento.
            raiz.Q<TextField>("nombre").value = "U" + nick.Substring(3);
            Pulsar(raiz.Q<Button>("nacer"));
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
                    Pulsar(raiz.Q("lista-filas").Query<Button>().ToList().First(b => b.Q<Label>().text == "Mercenarios"));
                    yield return Esperar(() => mapa.Seleccionada?.Clase == ClaseDeCosa.Mercenarios, 5, "el campamento elegido en la lista");
                }
                yield return Esperar(() => !raiz.Q("ficha").ClassListContains("oculto"), 5, "la ficha");
                yield return new WaitForSecondsRealtime(0.5f);
                var botones = raiz.Q("ficha-acciones").Query<Button>().ToList().Select(b => b.text).ToList();
                CollectionAssert.Contains(botones, "Marchar aquí");
                yield return Capturar("10_ficha_del_campamento");
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

        static void Pulsar(Button b)
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
