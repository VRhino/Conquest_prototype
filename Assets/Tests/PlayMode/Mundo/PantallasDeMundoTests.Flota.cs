// La flota del Mercado (paso 10 del bloque 4, tanda 2), con el ratón de verdad contra el motor. El Gobernador funda su plaza con el Mercado en pie,
// abre Mercado → Caravanas y crea una caravana; le añade un carro con su buey («+», el animal, «Agregar»), le cede una escuadra de escolta de su
// campamento y la lanza con trigo a otra plaza que conoce. Después sale al mundo: la caravana va por el mapa con su carro 3D y su ficha.
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
        /// <summary>Una captura de cerca de algo del mundo 3D (el carro de una caravana): una cámara propia, detrás y por encima, a 1600x900.</summary>
        static IEnumerator CapturarDeCerca(GameObject cosa, string nombre)
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            if (string.IsNullOrEmpty(carpeta) || cosa == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            var cam = new GameObject("Cámara de la prueba").AddComponent<Camera>();
            var destino = new RenderTexture(1600, 900, 24);
            cam.targetTexture = destino;
            cam.nearClipPlane = 0.1f;
            for (int i = 0; i < 3; i++)
            {
                var t = cosa.transform;
                cam.transform.position = t.position + t.right * 9f + t.forward * 2f + Vector3.up * 3f;
                cam.transform.LookAt(t.position + t.forward * 2f + Vector3.up * 0.8f);
                yield return null;
            }
            RenderTexture.active = destino;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            UnityEngine.Object.Destroy(cam.gameObject);
            destino.Release();
        }

        [UnityTest, Timeout(900_000)]
        public IEnumerator TuFlota()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza");
            string nick = null, faccion = null;
            yield return FundarPlaza(url, x => nick = x, mercadoEnPie: true, faccionDe: x => faccion = x);
            // Un destino: otra plaza de la misma Facción (de otra cuenta), que el héroe conoce sin haberla visto.
            // (la Facción, a nivel 3: a nivel 1 solo cabe una plaza, `fundacion.cap_alcanzado`)
            var nivel = new Google.Protobuf.WellKnownTypes.Struct();
            nivel.Fields["faccion_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(faccion);
            nivel.Fields["nivel"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(3);
            yield return Administrar(url, "fijar_nivel_faccion", nivel);
            string otraPlaza = null;
            yield return FundarPlaza(url, _ => { }, x => otraPlaza = x, enLaFaccion: faccion);
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
            var plazaId = Yo().Ubicacion.AsentamientoId;
            V1.Asentamiento Plaza() => s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId)?.Asentamiento;
            yield return ConViaRapidaHasta(s, plazaId, conexion.HeroeId, () => Plaza()?.Edificios.Any(e => e.Tipo == "mercado" && e.Estado == V1.EstadoEdificio.EdificioActivo) == true, 60, bombear: false);
            Assume.That(Plaza() != null && Plaza().Edificios.Any(e => e.Tipo == "mercado" && e.Estado == V1.EstadoEdificio.EdificioActivo), "el Mercado en pie");
            // Una escuadra en el campamento, para la escolta.
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
            GameObject sistema = null;
            IEnumerator Clic(VisualElement e) => ClicEnVentana(raton, raiz, e);
            VisualElement Ventana() => raiz.Q("plaza-ventana");
            System.Collections.Generic.List<V1.Caravana> Flota() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Caravana).Select(e => e.Caravana)
                .Where(x => x.Tipo == V1.TipoCaravana.CaravanaComercial && x.OrigenAsentamientoId == plazaId).ToList();
            try
            {
                // Mercado → Caravanas: los cupos de la flota y «＋ Crear».
                var riel = raiz.Q("plaza").Q("riel");
                yield return Esperar(() => riel.Q<Button>("riel-mercado") != null, 10, "Mercado en el riel");
                yield return Clic(riel.Q<Button>("riel-mercado"));
                yield return Esperar(() => Ventana()?.Q<Button>("sub-caravanas") != null, 5, "la subpestaña Caravanas");
                yield return Clic(Ventana().Q<Button>("sub-caravanas"));
                yield return Esperar(() => Ventana()?.Q<Button>("caravana-crear") != null, 5, "«＋ Crear»");
                Assert.IsNotNull(Ventana().Q(className: "flota-cupo-caravanas"), "el cupo de caravanas");
                yield return Clic(Ventana().Q<Button>("caravana-crear"));
                yield return Esperar(() => Flota().Count == 1, 10, "la caravana creada en el motor");
                var k = Flota()[0];
                yield return Esperar(() => Ventana()?.Q("flota-detalle-" + k.Id) != null, 5, "la caravana elegida a la derecha");
                Assert.IsNull(Ventana().Q<Button>("caravana-crear"), "con la espera entre caravanas, sin «＋ Crear»");

                // Un carro con su buey: «+», el buey, «Agregar».
                yield return Clic(Ventana().Q<Button>("carro-mas-" + k.Id));
                yield return Esperar(() => Ventana()?.Q("carro-nuevo") != null, 5, "el añadir carro");
                yield return Clic(Ventana().Q<Button>("carro-nuevo-buey"));
                yield return Esperar(() => Ventana().Q<Button>("carro-nuevo-buey")?.ClassListContains("elegido") == true, 5, "el buey elegido");
                yield return Clic(Ventana().Q<Button>("carro-agregar"));
                yield return Esperar(() => Flota()[0].Carros.Count == 1 && Flota()[0].Carros[0].Animal == "buey", 10, "el carro con su buey en el motor");
                yield return Esperar(() => Ventana()?.Q<Button>($"carro-{k.Id}-0") != null && Ventana().Q("carro-nuevo") == null, 5, "el carro en su hueco");
                StringAssert.Contains("Buey", Ventana().Q<Button>($"carro-{k.Id}-0").Q<Label>(className: "carro-hueco-pie").text);

                // La escolta: ceder la escuadra del campamento.
                yield return Esperar(() => Ventana()?.Query<Button>().ToList().Any(b => b.name.StartsWith("escoltar-")) == true, 5, "«Ceder» una escuadra");
                var ceder = Ventana().Query<Button>().ToList().First(b => b.name.StartsWith("escoltar-"));
                var sid = ceder.name.Substring("escoltar-".Length);
                yield return Clic(ceder);
                yield return Esperar(() => s.Almacen.Buscar(V1.TipoEntidad.Escuadron, sid)?.Escuadron?.Contenedor?.CaravanaId == k.Id, 10, "la escuadra en la escolta");
                yield return Esperar(() => Ventana()?.Q<Button>("quitar-escolta-" + sid) != null, 5, "«Retirar» la cedida");

                // El viaje: 10 de trigo hacia otra plaza.
                yield return Esperar(() => Ventana()?.Q("caravana-viaje") != null, 5, "el viaje");
                var destino = Ventana().Q<DropdownField>("viaje-destino");
                var nombreOtra = s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, otraPlaza)?.Asentamiento?.Nombre;
                if (nombreOtra != null && destino.choices.Any(x => x.StartsWith(nombreOtra))) destino.index = destino.choices.FindIndex(x => x.StartsWith(nombreOtra));
                Ventana().Q<IntegerField>("viaje-carga-trigo").value = 10;
                yield return Esperar(() => Ventana().Q("viaje-total").Q<Label>(className: "puerta-cupo-n").text.StartsWith("10 /"), 5, "la carga contada");
                yield return Capturar("140_flota");
                yield return EnTamanos(raiz, "140_flota", () => DentroDelMarco(Ventana(), "la flota"));
                yield return Clic(Ventana().Q<Button>("viaje-lanzar"));
                yield return Esperar(() => Flota()[0].Estado != V1.EstadoCaravana.CaravanaDisponible, 10, "la caravana lanzada");
                Assert.AreEqual(10_000, Flota()[0].Contenido.PorId["trigo"], "con su trigo");

                // Al mundo: la caravana va por el mapa con su carro 3D y su ficha.
                yield return Clic(Ventana().Q<Button>("ventana-cerrar"));
                yield return Esperar(() => Ventana() == null || Ventana().resolvedStyle.display == DisplayStyle.None, 5, "el Mercado cerrado");
                yield return Clic(raiz.Q<Button>("salir-al-mundo"));
                yield return Esperar(() => raiz.Q("ventana-salida") != null, 5, "la ventana de salida");
                yield return Clic(raiz.Q("ventana-salida").Q<Button>("salir"));
                yield return Esperar(() => raiz.Q("mapa") != null && ElMapa()?.Columna != null, 30, "el mapa con la columna");
                sistema = new GameObject("EventSystem de la prueba", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                yield return EsperarConReloj(url, s, () => Flota()[0].Estado == V1.EstadoCaravana.CaravanaEnTransito, 30, "la caravana en camino", 20_000);
                yield return Esperar(() => GameObject.Find("Caravana " + k.Id) != null, 10, "el carro 3D de la caravana");
                Assert.IsNotNull(GameObject.Find("Caravana " + k.Id).GetComponentInChildren<Animator>(), "con su animal");
                Assert.IsTrue(GameObject.Find("Caravana " + k.Id).GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.enabled), "el animal, a la vista");
                var mapa = ElMapa();
                CosaDelMapa cosa = null;
                yield return Esperar(() => (cosa = mapa.Cosas.FirstOrDefault(x => x.Id == k.Id)) != null, 10, "la caravana entre las cosas del mapa");
                mapa.Seleccionar(cosa);
                yield return Esperar(() => raiz.Q("ficha")?.Query<Label>().ToList().Any(l => l.text != null && l.text.Contains("🛞 1")) == true, 5, "su ficha, con su carro");
                yield return Capturar("141_caravana_en_el_mapa");
                yield return CapturarDeCerca(GameObject.Find("Caravana " + k.Id), "141b_carro_en_el_mapa");
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
