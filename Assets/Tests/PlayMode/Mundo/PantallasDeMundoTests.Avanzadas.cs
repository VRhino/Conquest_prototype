// Las avanzadas y la puerta desde la ficha del mapa (paso 9 del bloque 4, tanda 2), con el ratón y el teclado de verdad contra el motor. El Maestro
// de Obras funda su plaza (Vía Rápida) y, en Territorio, la columna de Avanzadas: «Elegir el sitio de la atalaya» abre el mapa 2D y un clic dentro
// de la zona deja su obra. Sale al mundo con los materiales en el carro (la administración se los da), elige la obra con un clic en el mapa 2D (la
// columna marcha a su puerta), la ficha enseña lo que pide con lo que lleva y «Entregar materiales» la completa. Levantada la atalaya, su ficha a la
// puerta tiene «Entrar» y «Guarnición», que abre su ventana. Al final, la capa «Parcelas de bosque» del mapa 2D.
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
        [UnityTest, Timeout(1_200_000)]
        public IEnumerator TuAvanzadaYSuObra()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza y dar suministro");
            string nick = null;
            yield return FundarPlaza(url, x => nick = x);
            foreach (var k in Preferencias) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.DeleteKey("mundo.mapa2d.fertilidad");
            PlayerPrefs.DeleteKey("mundo.mapa2d.parcelas");
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
            LogAssert.ignoreFailingMessages = true;
            selector.confirmButton.onClick.Invoke();
            yield return Esperar(() => raiz.Q("plaza") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior de la plaza");
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            yield return new WaitForSecondsRealtime(1.5f);
            V1.Heroe Yo() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            var plazaId = Yo().Ubicacion.AsentamientoId;
            var plaza = conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId).Asentamiento;
            System.Collections.Generic.List<V1.ObraDeMapa> Obras() => conexion.Sesion.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Obra).Select(e => e.Obra).Where(o => o.AsentamientoMadreId == plazaId).ToList();
            System.Collections.Generic.List<V1.Avanzada> Avanzadas() => conexion.Sesion.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Avanzada).Select(e => e.Avanzada).Where(x => x.AsentamientoMadreId == plazaId).ToList();

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
            VisualElement Ventana() => raiz.Q("plaza-ventana");
            // Un clic del ratón sobre el punto (x, y) del mundo en el mapa 2D abierto.
            IEnumerator ClicEnElMapa(double x, double y)
            {
                var textura = doc.panelSettings.targetTexture;
                try
                {
                    doc.panelSettings.targetTexture = null;
                    for (int i = 0; i < 8; i++) yield return null;
                    var p = pantallas.Mapa2D.Q("mapa-lienzo").LocalToWorld(pantallas.Mapa2D.ALienzo(x, y));
                    var donde = new Vector2(p.x * Screen.width / raiz.worldBound.width, Screen.height - p.y * Screen.height / raiz.worldBound.height);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde });
                    yield return null;
                    yield return null;
                    yield return PantallasDeMundoTests.Clic(raton, donde);
                }
                finally { doc.panelSettings.targetTexture = textura; }
            }
            try
            {
                // Territorio: la columna de Avanzadas, vacía, con lo que pide la atalaya y «Elegir el sitio de la atalaya».
                yield return Clic(raiz.Q("plaza").Q("riel").Q<Button>("riel-territorio"));
                yield return Esperar(() => Ventana()?.Q("avanzadas") != null, 5, "la columna de Avanzadas");
                Assert.IsTrue(Ventana().Q<Button>("elegir-avanzada").enabledSelf, "se puede elegir el sitio (plaza a nivel 3, cupo libre)");
                StringAssert.StartsWith("0 / ", Ventana().Q("cupo-avanzadas").Q<Label>().text);
                yield return Clic(Ventana().Q<Button>("elegir-avanzada"));
                yield return Esperar(() => pantallas.Mapa2D?.Eleccion != null && pantallas.Mapa2D.Cosas.Count > 0, 10, "el mapa 2D eligiendo el sitio de la atalaya");
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capturar("120_elegir_sitio_de_atalaya");
                // Dentro de la zona y fuera de la ciudad: se prueba a unas unidades de la plaza en cada rumbo hasta que el motor lo acepte.
                var (px, py) = (plaza.Posicion.X / 1000.0, plaza.Posicion.Y / 1000.0);
                foreach (var (dx, dy) in new[] { (10.0, 0.0), (0.0, 10.0), (-10.0, 0.0), (0.0, -10.0), (8.0, 8.0), (-8.0, 8.0), (8.0, -8.0), (-8.0, -8.0) })
                {
                    if (pantallas.Mapa2D?.Eleccion == null) break;
                    yield return ClicEnElMapa(px + dx, py + dy);
                    for (var fin = Time.realtimeSinceStartup + 5; Time.realtimeSinceStartup < fin && !Obras().Any(o => o.Tipo == V1.TipoObra.ObraAvanzada); ) yield return null;
                    if (Obras().Any(o => o.Tipo == V1.TipoObra.ObraAvanzada)) break;
                    Debug.Log("[Prueba] atalaya rechazada en " + (dx, dy) + ": " + raiz.Q<Label>("mapa-eleccion-error")?.text);
                }
                Assert.IsTrue(Obras().Any(o => o.Tipo == V1.TipoObra.ObraAvanzada), "la obra de la atalaya");
                yield return Esperar(() => pantallas.Mapa2D == null, 5, "el mapa se cierra");
                var obra = Obras().First(o => o.Tipo == V1.TipoObra.ObraAvanzada);
                yield return Esperar(() => Ventana()?.Q("avanzadas")?.Q("tarjeta-" + obra.Id) != null, 5, "la obra en la columna de Avanzadas");
                StringAssert.StartsWith("1 / ", Ventana().Q("cupo-avanzadas").Q<Label>().text);
                Assert.IsNotNull(raiz.Q("obras-hud")?.Q("obra-mapa-" + obra.Id), "la obra de la atalaya en el HUD");
                yield return Capturar("121_territorio_con_avanzadas");
                yield return EnTamanos(raiz, "121_territorio_con_avanzadas", () => DentroDelMarco(Ventana(), "Territorio con avanzadas"));

                // Al mundo, con lo que pide en el carro (lo da la administración).
                yield return Clic(Ventana().Q<Button>("ventana-cerrar"));
                yield return Esperar(() => Ventana() == null || Ventana().ClassListContains("oculto") || Ventana().Q("avanzadas") == null, 5, "Territorio cerrado");
                // Una escuadra para la guarnición de la atalaya (sale con la columna).
                var escuadra = new Google.Protobuf.WellKnownTypes.Struct();
                escuadra.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(conexion.HeroeId);
                escuadra.Fields["tropa_id"] = Google.Protobuf.WellKnownTypes.Value.ForString("lenadores");
                escuadra.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(30);
                yield return Administrar(url, "dar_escuadra", escuadra);
                yield return new WaitForSecondsRealtime(1f);
                yield return Clic(raiz.Q<Button>("salir-al-mundo"));
                yield return Esperar(() => raiz.Q("ventana-salida") != null, 5, "la ventana de salida");
                yield return Clic(raiz.Q("ventana-salida").Q<Button>("salir"));
                yield return Esperar(() => raiz.Q("mapa") != null && ElMapa()?.Columna != null, 30, "el mapa con la columna");
                sistema = new GameObject("EventSystem de la prueba", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                var columnaId = ElMapa().Columna.Id;
                foreach (var kv in obra.Requerido.PorId)
                {
                    var q = new Google.Protobuf.WellKnownTypes.Struct();
                    q.Fields["ejercito_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(columnaId);
                    q.Fields["recurso"] = Google.Protobuf.WellKnownTypes.Value.ForString(kv.Key);
                    q.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(kv.Value);
                    yield return Administrar(url, "dar_suministro", q);
                }
                // El mapa 2D: un clic en la obra la elige y la columna marcha a su puerta.
                yield return Clic(raiz.Q<Button>("mapa-2d"));
                yield return Esperar(() => pantallas.Mapa2D != null && pantallas.Mapa2D.Cosas.Any(c => c.Id == obra.Id), 10, "la obra en el mapa 2D");
                yield return ClicEnElMapa(obra.Posicion.X / 1000.0, obra.Posicion.Y / 1000.0);
                yield return Esperar(() => ElMapa().Seleccionada?.Id == obra.Id, 5, "la obra elegida");
                yield return Esperar(() => raiz.Q("ficha")?.Q<Button>("entregar-" + obra.Id)?.enabledSelf == true, 60, "la columna a la puerta de la obra");
                Assert.IsTrue(raiz.Q("ficha").Query(className: "obra-entrega-fila").ToList().Count > 0, "lo que pide, material a material");
                yield return Capturar("122_ficha_de_la_obra");
                yield return EnTamanos(raiz, "122_ficha_de_la_obra");
                yield return Clic(raiz.Q("ficha").Q<Button>("entregar-" + obra.Id));
                yield return Esperar(() => Obras().FirstOrDefault(o => o.Id == obra.Id)?.HasTerminaEnMs != false, 10, "todo entregado: se levanta");
                yield return Capturar("122b_obra_levantandose");
                yield return Esperar(() => Avanzadas().Count > 0, 300, "la atalaya levantada");
                var atalaya = Avanzadas()[0];

                // Su ficha a la puerta: «Entrar» y «Guarnición», que abre su ventana.
                var cosa = ElMapa().Cosas.FirstOrDefault(c => c.Id == atalaya.Id);
                yield return Esperar(() => (cosa = ElMapa().Cosas.FirstOrDefault(c => c.Id == atalaya.Id)) != null, 10, "la atalaya en el mapa");
                ElMapa().Seleccionar(cosa);
                yield return Esperar(() => raiz.Q("ficha")?.Query<Button>().ToList().Any(b => b.name == "puerta-guarnicion") == true, 20, "«Guarnición» en la ficha de la atalaya");
                yield return Clic(raiz.Q("ficha").Q<Button>("puerta-guarnicion"));
                yield return Esperar(() => raiz.Q("ventana-puerta") != null, 5, "la ventana de la guarnición");
                yield return Capturar("123_guarnicion_de_la_atalaya");
                yield return EnTamanos(raiz, "123_guarnicion_de_la_atalaya");
                var dejar = raiz.Q("ventana-puerta").Query<Button>().ToList().FirstOrDefault(b => b.name.StartsWith("dejar-") && b.enabledSelf);
                if (dejar != null)
                {
                    var id = dejar.name.Substring("dejar-".Length);
                    yield return Clic(dejar);
                    yield return Esperar(() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Escuadron, id)?.Escuadron?.Contenedor?.AvanzadaId == atalaya.Id, 10, "la escuadra en la guarnición");
                    yield return Esperar(() => raiz.Q("ventana-puerta")?.Q<Button>("retirar-" + id) != null, 5, "«Retirar» la tuya");
                }
                yield return Clic(raiz.Q<Button>("ventana-puerta-cerrar"));
                yield return Esperar(() => raiz.Q("ventana-puerta") == null, 5, "cerrada");

                // La capa de parcelas de bosque en el mapa 2D.
                yield return Clic(raiz.Q<Button>("mapa-2d"));
                yield return Esperar(() => pantallas.Mapa2D != null, 10, "el mapa 2D");
                yield return Clic(pantallas.Mapa2D.Q<Button>("mapa-capas-boton"));
                yield return Esperar(() => pantallas.Mapa2D.Q<Toggle>("capa-parcelas") != null, 5, "las capas");
                yield return Clic(pantallas.Mapa2D.Q<Toggle>("capa-parcelas"));
                yield return Esperar(() => pantallas.Mapa2D.Parcelas, 5, "la capa de parcelas puesta");
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Capturar("124_capa_de_parcelas");
                yield return EnTamanos(raiz, "124_capa_de_parcelas");
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
        }
    }
}
