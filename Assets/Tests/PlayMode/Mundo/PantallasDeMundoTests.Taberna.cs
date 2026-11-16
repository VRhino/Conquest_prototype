// La Taberna de la plaza (paso 12 del bloque 4, tanda 1), con el ratón de verdad contra el motor. El Gobernador funda su plaza con la Taberna
// (`mercadoEnPie`) y oro en el almacén; otra Facción funda la suya cerca, para tener una plaza ajena. Abre la Taberna del riel → Intel: compra una
// Mirada sobre un campamento de mercenarios (sale en «Tus Miradas»), un informe de la plaza ajena si la conoce (se abre su detalle) y el informe de
// región de sus plazas (sale en «Tus informes de región»).
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
        public IEnumerator TuTaberna()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza");
            string nick = null, plazaId = null;
            yield return FundarPlaza(url, x => nick = x, x => plazaId = x, mercadoEnPie: true);
            // Una plaza ajena (otra cuenta, otra Facción), para el informe de plaza.
            yield return FundarPlaza(url, _ => { }, _ => { });
            var oro = new Google.Protobuf.WellKnownTypes.Struct();
            oro.Fields["asentamiento_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(plazaId);
            oro.Fields["recurso"] = Google.Protobuf.WellKnownTypes.Value.ForString("oro");
            oro.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(2_000_000);
            yield return Administrar(url, "dar_recursos", oro);
            // Para «Mercenarios de la región»: un bot que se ofrece en el campamento de la región y monedas en el fondo de la plaza
            // (`dar_fondo_monedas`, motor ecfb23d; al héroe, `dar_monedas` al entrar).
            string nickBot = null, botId = null;
            yield return OtroHeroe(url, "Mercenario " + DateTime.UtcNow.Ticks % 100_000, nickDe: x => nickBot = x);
            yield return HeroeDeLaCuenta(url, nickBot, x => botId = x);
            var bot = new Google.Protobuf.WellKnownTypes.Struct();
            bot.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(botId);
            bot.Fields["controlador"] = Google.Protobuf.WellKnownTypes.Value.ForString("bot");
            yield return Administrar(url, "fijar_controlador", bot);
            yield return ConSesionDe(url, nickBot, _ => new V1.Orden { OfrecerseComoMercenario = new V1.OrdenOfrecerseComoMercenario() });
            var fondoPlaza = new Google.Protobuf.WellKnownTypes.Struct();
            fondoPlaza.Fields["asentamiento_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(plazaId);
            fondoPlaza.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(50_000);
            yield return Administrar(url, "dar_fondo_monedas", fondoPlaza);

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
            V1.Asentamiento Plaza() => s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId)?.Asentamiento;
            yield return ConViaRapidaHasta(s, plazaId, conexion.HeroeId, () => Plaza()?.Edificios.Any(e => e.Tipo == "taberna" && e.Estado == V1.EstadoEdificio.EdificioActivo) == true, 60, bombear: false);
            Assume.That(Plaza().Edificios.Any(e => e.Tipo == "taberna" && e.Estado == V1.EstadoEdificio.EdificioActivo), "la Taberna en pie");
            // Solo la escena, para las capturas del prototipo (`tasks/pruebas/cdp/taberna.mjs`): el Gobernador dentro, con la Taberna en pie.
            if (Environment.GetEnvironmentVariable("MUNDO_PROTO") == "taberna")
            {
                Debug.Log($"[Prueba] escena del prototipo: yo={nick} plaza={plazaId}");
                LogAssert.ignoreFailingMessages = false;
                yield break;
            }
            yield return Quieto(1f);

            var entrada = UnityEngine.InputSystem.InputSystem.settings;
            var fondo = entrada.backgroundBehavior;
            var editorAntes = entrada.editorInputBehaviorInPlayMode;
            entrada.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            entrada.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var otros = UnityEngine.InputSystem.InputSystem.devices.OfType<UnityEngine.InputSystem.Mouse>().Where(m => m.enabled).ToList();
            foreach (var m in otros) UnityEngine.InputSystem.InputSystem.DisableDevice(m);
            var raton = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            raton.MakeCurrent();
            IEnumerator Clic(VisualElement e) => ClicEnVentana(raton, raiz, e);
            VisualElement Ventana() => raiz.Q("plaza-ventana");
            int Miradas() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Mirada).Count();
            try
            {
                // La Taberna del riel → Intel.
                yield return Esperar(() => raiz.Q("plaza").Q("riel").Q<Button>("riel-taberna") != null, 10, "la Taberna en el riel");
                yield return Clic(raiz.Q("plaza").Q("riel").Q<Button>("riel-taberna"));
                yield return Esperar(() => Ventana()?.Q("intel-mirada") != null, 5, "la Mirada en la Taberna");
                Assert.IsNotNull(Ventana().Q("intel-informe"), "el informe de plaza");
                Assert.IsNotNull(Ventana().Q("informe-plazas"), "el informe de región");

                // Mirada: alrededor de un campamento de mercenarios (el desplegable) y «Comprar» con su precio en oro.
                var cerca = Ventana().Q<DropdownField>("intel-cerca");
                Assert.IsNotNull(cerca, "«Mirar alrededor de…»");
                cerca.index = cerca.choices.FindIndex(x => x.StartsWith("Campamento"));
                yield return Esperar(() => Ventana()?.Q<Button>("intel-comprar-mirada") != null, 5, "«Comprar» la Mirada");
                yield return Capturar("190_taberna_intel");
                yield return EnTamanos(raiz, "190_taberna_intel", () => DentroDelMarco(Ventana(), "la Taberna"));
                yield return Clic(Ventana().Q<Button>("intel-comprar-mirada"));
                yield return Esperar(() => Miradas() == 1, 10, "la Mirada comprada en el motor");
                yield return Esperar(() => Ventana()?.Q(className: "intel-mirada") != null, 5, "la Mirada en «Tus Miradas»");

                // El informe de la plaza ajena, si la conoce: se abre su detalle.
                if (Ventana().Q<DropdownField>("intel-plaza") != null)
                {
                    yield return Clic(Ventana().Q<Button>("intel-comprar-informe"));
                    yield return Esperar(() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Informe).Any(), 10, "el informe de plaza en el motor");
                    yield return Esperar(() => Ventana()?.Q("informe-detalle") != null, 5, "el detalle del informe");
                }
                else Debug.Log("[Prueba] taberna: sin plaza ajena conocida, no hay informe de plaza que comprar");

                // El informe de región de sus plazas: sale en «Tus informes de región» y su botón deja de estar.
                yield return Clic(Ventana().Q<Button>("comprar-informe-plazas"));
                yield return Esperar(() => Ventana()?.Q<Button>("comprar-informe-plazas") == null, 10, "el informe de región comprado");
                yield return Capturar("191_taberna_comprado");
                yield return EnTamanos(raiz, "191_taberna_comprado", () => DentroDelMarco(Ventana(), "la Taberna con lo comprado"));

                // Mercenarios de la región: el bot del campamento de la región, pagado con el fondo de monedas de la plaza (o con las del héroe).
                var monedas = new Google.Protobuf.WellKnownTypes.Struct();
                monedas.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(conexion.HeroeId);
                monedas.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(5_000);
                yield return Administrar(url, "dar_monedas", monedas);
                V1.Heroe Yo() => s.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
                V1.Contrato Contrato() => s.Almacen.Buscar(V1.TipoEntidad.Faccion, Yo().FaccionId)?.Faccion?.Contratos.FirstOrDefault(k => k.MercenarioId == botId);
                yield return Esperar(() => Yo().Monedas >= 5_000 && (Plaza()?.Forja?.Fondo ?? 0) >= 50_000, 10, "las monedas del héroe y del fondo");
                yield return Esperar(() => Plaza()?.MercenariosRegion.Any(o => o.HeroeId == botId) == true, 20, "el bot en los mercenarios de la región");
                yield return Esperar(() => Ventana()?.Q<Button>("sub-mercenarios") != null, 5, "la pestaña «Mercenarios de la región»");
                yield return Clic(Ventana().Q<Button>("sub-mercenarios"));
                VisualElement Oferta() => Ventana()?.Q("merc-oferta-" + botId);
                yield return Esperar(() => Oferta() != null, 10, "la oferta del bot en la Taberna");
                Assert.IsNotNull(Ventana().Q("merc-pagar") ?? Ventana().Q("merc-bolsa"), "con qué se paga: el fondo de la plaza y tus monedas");
                if (Oferta().Q<Button>("merc-tarifa-" + botId + "-1") is Button unDia)
                {
                    yield return Clic(unDia);
                    yield return Esperar(() => Oferta()?.Q("merc-tarifa-" + botId + "-1")?.ClassListContains("activo") == true, 5, "1 día elegido");
                }
                yield return Capturar("192_mercenarios_de_la_region");
                yield return EnTamanos(raiz, "192_mercenarios_de_la_region", () => DentroDelMarco(Ventana(), "los mercenarios de la región"));
                var fondoAntes = Plaza().Forja?.Fondo ?? 0;
                var contratar = Oferta().Q<Button>("merc-contratar-" + botId);
                Assert.IsTrue(contratar != null && contratar.enabledSelf, "«Contratar», con el fondo de la plaza");
                yield return Clic(contratar);
                yield return Esperar(() => Contrato() != null, 10, "el contrato del mercenario de la región");
                Debug.Log($"[Prueba] taberna: contratado {botId} ({Contrato().Trabajo}); fondo {fondoAntes} → {Plaza().Forja?.Fondo ?? 0}, monedas {Yo().Monedas}");
                Assert.Less(Plaza().Forja?.Fondo ?? 0, fondoAntes, "pagado con el fondo de la plaza");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                foreach (var m in otros) if (m.added) UnityEngine.InputSystem.InputSystem.EnableDevice(m);
                entrada.backgroundBehavior = fondo;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
