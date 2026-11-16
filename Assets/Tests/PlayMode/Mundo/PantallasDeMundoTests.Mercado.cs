// El Mercado de la plaza (paso 10 del bloque 4, tanda 1), con el ratón y el teclado de verdad contra el motor. El Gobernador funda su plaza (con la
// Vía Rápida y el reloj adelantado hasta tener el Mercado en pie) y abre Mercado en el riel, entre Reclutamiento (y la Taberna) y la Armería:
// - pone madera a la venta con precio y pide trigo a precio de mercado (la boleta: Vender / Comprar, el recurso del almacén, cantidad y precio);
// - las dos órdenes salen en pie y cancela la de venta con su ✕ (`cancelarOrdenMercado`);
// - sale al mundo y abre la ventana Mercado desde la ficha de su plaza, con la columna a la puerta.
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
        public IEnumerator TuMercado()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza");
            string nick = null;
            yield return FundarPlaza(url, x => nick = x, mercadoEnPie: true);
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
            yield return Esperar(() => raiz.Q("plaza") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior de la plaza");
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            var s = conexion.Sesion;
            V1.Heroe Yo() => s.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            var plazaId = Yo().Ubicacion.AsentamientoId;
            V1.Asentamiento Plaza() => s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId)?.Asentamiento;
            System.Collections.Generic.List<V1.OrdenMercado> Ordenes() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.OrdenMercado).Select(e => e.OrdenMercado)
                .Where(o => o.AsentamientoId == plazaId && o.Estado == V1.EstadoOrden.OrdenActiva).ToList();
            // El Mercado, en pie: la Vía Rápida y el reloj adelantado.
            yield return ConViaRapidaHasta(s, plazaId, conexion.HeroeId, () => Plaza()?.Edificios.Any(e => e.Tipo == "mercado" && e.Estado == V1.EstadoEdificio.EdificioActivo) == true, 60, bombear: false);
            Assert.IsTrue(Plaza() != null && Plaza().Edificios.Any(e => e.Tipo == "mercado" && e.Estado == V1.EstadoEdificio.EdificioActivo), "el Mercado en pie");
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
            try
            {
                // Mercado en el riel, entre Reclutamiento y la Armería; a ventana completa, con la boleta (eres el Gobernador).
                var riel = raiz.Q("plaza").Q("riel");
                yield return Esperar(() => riel.Q<Button>("riel-mercado") != null, 10, "Mercado en el riel");
                var orden = riel.Query<Button>(className: "plaza-riel-boton").ToList().Select(b => b.name).ToList();
                // Como el prototipo: Reclutamiento, Taberna (si la hay) y Mercado.
                var antesDelMercado = orden.Contains("riel-taberna") ? "riel-taberna" : "riel-reclutamiento";
                Assert.AreEqual(orden.IndexOf(antesDelMercado) + 1, orden.IndexOf("riel-mercado"), "Mercado, detrás de Reclutamiento y la Taberna: " + string.Join(",", orden));
                yield return Clic(riel.Q<Button>("riel-mercado"));
                yield return Esperar(() => Ventana()?.Q("orden-boleta") != null, 5, "la ventana Mercado con la boleta");
                Assert.IsTrue(Ventana().ClassListContains("plaza-ventana-completa"), "a ventana completa");
                Assert.AreEqual("Mercado", Ventana().Q<Label>(className: "plaza-ventana-nombre").text);
                Assert.IsNotNull(Ventana().Q("ordenes-en-pie"), "las órdenes en pie");
                yield return Capturar("130_mercado_vacio");

                // Madera a la venta: ▲ Vender, la madera, 100 a 2 de oro (el total lo dice).
                yield return Clic(Ventana().Q<Button>("orden-vender"));
                yield return Clic(Ventana().Q<Button>("orden-recurso-madera"));
                yield return Esperar(() => Ventana().Q<Button>("orden-recurso-madera")?.ClassListContains("elegido") == true, 5, "la madera elegida");
                Ventana().Q<IntegerField>("orden-cantidad").value = 100;
                Ventana().Q<FloatField>("orden-precio").value = 2f;
                Assert.AreEqual("cobras 🪙 200", Ventana().Q<Label>("orden-total").text);
                Assert.AreEqual("Poner a la venta", Ventana().Q<Button>("orden-colocar").text);
                yield return Clic(Ventana().Q<Button>("orden-colocar"));
                yield return Esperar(() => Ordenes().Any(o => !o.EsCompra && o.Recurso == "madera"), 10, "la orden de venta en el motor");
                var venta = Ordenes().First(o => !o.EsCompra && o.Recurso == "madera");
                Assert.AreEqual(100_000, venta.Cantidad, "100 de madera");
                Assert.AreEqual(2_000, venta.PrecioUnitario, "a 2 de oro");
                yield return Esperar(() => Ventana()?.Q("orden-" + venta.Id) != null, 5, "la venta en pie");

                // Trigo pedido, a precio de mercado: ▼ Comprar, el trigo, 50.
                yield return Clic(Ventana().Q<Button>("orden-comprar"));
                yield return Esperar(() => Ventana().Q("orden-boleta")?.ClassListContains("compra") == true, 5, "la boleta de compra");
                yield return Clic(Ventana().Q<Button>("orden-recurso-trigo"));
                yield return Esperar(() => Ventana().Q<Button>("orden-recurso-trigo")?.ClassListContains("elegido") == true, 5, "el trigo elegido");
                Ventana().Q<IntegerField>("orden-cantidad").value = 50;
                Assert.AreEqual("a precio de mercado", Ventana().Q<Label>("orden-total").text);
                Assert.AreEqual("Pedir", Ventana().Q<Button>("orden-colocar").text);
                yield return Clic(Ventana().Q<Button>("orden-colocar"));
                yield return Esperar(() => Ordenes().Any(o => o.EsCompra && o.Recurso == "trigo"), 10, "la orden de compra en el motor");
                var compra = Ordenes().First(o => o.EsCompra && o.Recurso == "trigo");
                Assert.Greater(compra.PrecioUnitario, 0, "con el precio de mercado");
                yield return Esperar(() => Ventana()?.Q("orden-" + compra.Id) != null, 5, "la compra en pie");
                Assert.IsTrue(Ventana().Q("orden-" + compra.Id).ClassListContains("compra"));
                yield return Capturar("131_mercado_ordenes");
                yield return EnTamanos(raiz, "131_mercado_ordenes", () => DentroDelMarco(Ventana(), "Mercado"));

                // Cancelar la venta con su ✕.
                yield return Clic(Ventana().Q<Button>("cancelar-" + venta.Id));
                yield return Esperar(() => Ordenes().All(o => o.Id != venta.Id), 10, "la venta cancelada en el motor");
                yield return Esperar(() => Ventana()?.Q("orden-" + venta.Id) == null && Ventana()?.Q("orden-" + compra.Id) != null, 5, "solo la compra en pie");

                // Desde el mundo: la ficha de la plaza, con la columna a la puerta, abre la ventana Mercado.
                yield return Clic(Ventana().Q<Button>("ventana-cerrar"));
                yield return Esperar(() => Ventana() == null || Ventana().resolvedStyle.display == DisplayStyle.None, 5, "el Mercado cerrado");
                yield return Esperar(() => raiz.Q<Button>("salir-al-mundo")?.resolvedStyle.display == DisplayStyle.Flex, 5, "«Salir al mundo»");
                yield return Clic(raiz.Q<Button>("salir-al-mundo"));
                yield return Esperar(() => raiz.Q("ventana-salida") != null, 5, "la ventana de salida");
                yield return Clic(raiz.Q("ventana-salida").Q<Button>("salir"));
                yield return Esperar(() => raiz.Q("mapa") != null && ElMapa()?.Columna != null, 30, "el mapa con la columna");
                sistema = new GameObject("EventSystem de la prueba", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                CosaDelMapa cosa = null;
                yield return Esperar(() => (cosa = ElMapa().Cosas.FirstOrDefault(x => x.Clase == ClaseDeCosa.Plaza && x.Id == plazaId)) != null, 20, "la plaza en el mapa");
                ElMapa().Seleccionar(cosa);
                Button mercado = null;
                yield return Esperar(() => (mercado = raiz.Q("ficha")?.Query<Button>().ToList().FirstOrDefault(b => b.Q<Label>()?.text == "Mercado" || b.text == "Mercado")) != null, 20, "«Mercado» en la ficha de la plaza");
                yield return Clic(mercado);
                yield return Esperar(() => raiz.Q("ventana-puerta")?.Q("orden-boleta") != null, 5, "la ventana Mercado de la puerta");
                Assert.IsNotNull(raiz.Q("ventana-puerta").Q("orden-" + compra.Id), "la compra en pie, también desde la puerta");
                yield return Capturar("132_mercado_desde_la_puerta");
                yield return EnTamanos(raiz, "132_mercado_desde_la_puerta", () => DentroDelMarco(raiz.Q("ventana-puerta"), "Mercado de la puerta"));
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
