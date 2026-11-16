// El comercio a distancia del Mercado (paso 10 del bloque 4, tanda 3), con el ratón de verdad contra el motor. El Gobernador (con su plaza, el
// Mercado y la Taberna en pie, y otra plaza de su Facción) da a su caravana una ruta que recoge trigo en el origen y lo deja en la otra plaza, la
// ve dibujada en Rutas y la detiene; propone un trueque a la otra plaza; compra en la Taberna el informe de órdenes de la región y abre «Región».
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
        public IEnumerator TuComercioADistancia()
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
            // Una caravana con un carro y su buey (la flota ya la prueba TuFlota): por órdenes.
            IEnumerator Mandar(V1.Orden o)
            {
                var r = s.Ordenar(o);
                for (var f = Time.realtimeSinceStartup + 20; !r.IsCompleted && Time.realtimeSinceStartup < f;) yield return null;
                Assert.AreEqual(V1.Resultado.OrdenAceptada, r.Result.Resultado, $"{o.CuerpoCase}: {r.Result.Rechazo?.Codigo}");
            }
            yield return Mandar(new V1.Orden { CrearCaravana = new V1.OrdenCrearCaravana { AsentamientoId = plazaId } });
            System.Collections.Generic.List<V1.Caravana> Flota() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Caravana).Select(e => e.Caravana)
                .Where(x => x.Tipo == V1.TipoCaravana.CaravanaComercial && x.OrigenAsentamientoId == plazaId).ToList();
            yield return Esperar(() => Flota().Count == 1, 10, "la caravana");
            var k = Flota()[0];
            yield return Mandar(new V1.Orden { AgregarCarroCaravana = new V1.OrdenAgregarCarroCaravana { CaravanaId = k.Id, TipoCarro = "basico" } });
            yield return Mandar(new V1.Orden { ComprarAnimalCaravana = new V1.OrdenComprarAnimalCaravana { CaravanaId = k.Id, CarroIndice = 0, TipoAnimal = "buey" } });
            yield return Esperar(() => Flota()[0].Carros.Count == 1 && Flota()[0].Carros[0].HasAnimal, 10, "el carro con su buey");
            V1.Faccion Faccion() => s.Almacen.Buscar(V1.TipoEntidad.Faccion, faccion)?.Faccion;
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
            VisualElement Ventana() => raiz.Q("plaza-ventana");
            IEnumerator Confirmar(string que)
            {
                yield return Esperar(() => raiz.Q(className: "dialogo-velo")?.Q<Button>(className: "boton-peligro") != null, 5, "la confirmación de " + que);
                yield return Clic(raiz.Q(className: "dialogo-velo").Q<Button>(className: "boton-peligro"));
            }
            try
            {
                var riel = raiz.Q("plaza").Q("riel");
                yield return Esperar(() => riel.Q<Button>("riel-mercado") != null, 10, "Mercado en el riel");
                yield return Clic(riel.Q<Button>("riel-mercado"));
                yield return Esperar(() => Ventana()?.Q<Button>("sub-rutas") != null && Ventana().Q<Button>("sub-trueques") != null, 5, "las pestañas Rutas y Trueques");

                // El encargo: Caravanas, la caravana, «Encargo»; una ruta a la otra plaza de la Facción que recoge 10 de trigo en el origen y lo deja allí.
                yield return Clic(Ventana().Q<Button>("sub-caravanas"));
                yield return Esperar(() => Ventana()?.Q<Button>("caravana-modo-encargo") != null, 5, "«Encargo» en la caravana");
                yield return Clic(Ventana().Q<Button>("caravana-modo-encargo"));
                yield return Esperar(() => Ventana()?.Q("encargo-form") != null, 5, "el formulario del encargo");
                yield return Clic(Ventana().Q<Button>("enc-origen-recoger-trigo"));
                yield return Esperar(() => Ventana().Q<IntegerField>("enc-origen-cantidad-trigo") != null, 5, "la fila del trigo");
                Ventana().Q<IntegerField>("enc-origen-cantidad-trigo").value = 10;
                yield return Esperar(() => Ventana().Q<Button>("enc-destino-dejar-trigo") != null, 5, "«Deja» trigo en el destino");
                yield return Clic(Ventana().Q<Button>("enc-destino-dejar-trigo"));
                yield return Esperar(() => Ventana().Q<IntegerField>("enc-destino-tope-trigo") != null, 5, "la fila de dejar trigo");
                yield return Capturar("150_encargo");
                yield return EnTamanos(raiz, "150_encargo", () => DentroDelMarco(Ventana(), "el encargo"));
                yield return Clic(Ventana().Q<Button>("encargo-enviar"));
                yield return Esperar(() => Flota()[0].Encargo != null, 10, "el encargo en el motor");
                Assert.AreEqual("ruta", Flota()[0].Encargo.Modo);
                Assert.AreEqual(otraPlaza, Flota()[0].Encargo.DestinoAsentamientoId, "hacia la otra plaza de la Facción");
                Assert.AreEqual(10_000, Flota()[0].Encargo.ParadaOrigen.Recoger[0].Cantidad);

                // Rutas: la caravana con su ruta dibujada; «Detener ruta», con su confirmación.
                yield return Clic(Ventana().Q<Button>("sub-rutas"));
                yield return Esperar(() => Ventana()?.Q("encargo-" + k.Id) != null, 5, "la ruta en la pestaña Rutas");
                Assert.IsNotNull(Ventana().Q("encargo-" + k.Id).Q(className: "ruta-vis"), "la ruta dibujada");
                yield return Capturar("151_rutas");
                yield return EnTamanos(raiz, "151_rutas", () => DentroDelMarco(Ventana(), "las rutas"));
                yield return Clic(Ventana().Q<Button>("encargo-detener"));
                yield return Confirmar("detener la ruta");
                yield return Esperar(() => Flota()[0].Encargo?.Detener == true || Flota()[0].Encargo == null, 10, "la ruta detenida");

                // Trueques: dar 10 de madera por 5 de trigo a la otra plaza.
                yield return Clic(Ventana().Q<Button>("sub-trueques"));
                yield return Esperar(() => Ventana()?.Q("trueque-proponer") != null, 5, "proponer un trueque");
                var da = Ventana().Q<DropdownField>("trueque-a-recurso-0");
                da.index = da.choices.IndexOf(CosasDelMapa.NombreDeRecurso("madera"));
                Ventana().Q<IntegerField>("trueque-a-cantidad-0").value = 10;
                var recibe = Ventana().Q<DropdownField>("trueque-b-recurso-0");
                recibe.index = recibe.choices.IndexOf(CosasDelMapa.NombreDeRecurso("trigo"));
                Ventana().Q<IntegerField>("trueque-b-cantidad-0").value = 5;
                var con = Ventana().Q<DropdownField>("trueque-destino");
                var nombreOtra = s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, otraPlaza)?.Asentamiento?.Nombre;
                if (nombreOtra != null) con.index = con.choices.FindIndex(x => x.StartsWith(nombreOtra));
                yield return Clic(Ventana().Q<Button>("trueque-proponer-boton"));
                yield return Esperar(() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.AcuerdoTrueque).Any(e => e.AcuerdoTrueque.AsentamientoAId == plazaId), 10, "el trueque propuesto en el motor");
                var pacto = s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.AcuerdoTrueque).Select(e => e.AcuerdoTrueque).First(x => x.AsentamientoAId == plazaId);
                Assert.AreEqual(otraPlaza, pacto.AsentamientoBId);
                yield return Esperar(() => Ventana()?.Q("pacto-" + pacto.Id) != null, 5, "el pacto en curso");
                yield return Capturar("152_trueques");
                yield return EnTamanos(raiz, "152_trueques", () => DentroDelMarco(Ventana(), "los trueques"));

                // La Taberna: el informe de órdenes de la región, con oro del almacén; después, «Región» en el Mercado.
                yield return Clic(Ventana().Q<Button>("ventana-cerrar"));
                yield return Esperar(() => riel.Q<Button>("riel-taberna") != null, 10, "la Taberna en el riel");
                yield return Clic(riel.Q<Button>("riel-taberna"));
                yield return Esperar(() => Ventana()?.Q<Button>("comprar-informe-ordenes") != null, 5, "comprar el informe de órdenes");
                yield return Capturar("153_taberna");
                yield return EnTamanos(raiz, "153_taberna", () => DentroDelMarco(Ventana(), "la taberna"));
                yield return Clic(Ventana().Q<Button>("comprar-informe-ordenes"));
                yield return Esperar(() => Faccion()?.InformesRegion.Any(x => x.Tipo == "ordenes") == true, 10, "el informe de órdenes de la Facción");
                yield return Esperar(() => Ventana()?.Q("informe-ordenes")?.Q(className: "intel-vigente") != null, 5, "el informe, a la vista");
                yield return Clic(Ventana().Q<Button>("ventana-cerrar"));
                yield return Clic(riel.Q<Button>("riel-mercado"));
                yield return Esperar(() => Ventana()?.Q<Button>("sub-region") != null, 5, "«Región» en el Mercado");
                yield return Clic(Ventana().Q<Button>("sub-region"));
                yield return Esperar(() => Ventana()?.Q<Label>(className: "plaza-vacio") != null || Ventana()?.Q(className: "region-plaza") != null, 5, "las órdenes de la región");
                yield return Capturar("154_region");
                yield return EnTamanos(raiz, "154_region", () => DentroDelMarco(Ventana(), "la región"));
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
