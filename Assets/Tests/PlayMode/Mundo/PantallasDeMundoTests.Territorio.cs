// La ventana Territorio de la plaza (paso 9 del bloque 4, tanda 1), con el ratón y el teclado de verdad contra el motor. El Maestro de Obras funda
// su plaza (con la Vía Rápida) y abre Territorio en el riel: la zona de influencia con sus nodos y el bosque, el cupo de pueblos y «Marcar» abierto
// de serie. Marca un extractor sobre el nodo libre más cercano (la obra sale en la fila, en su detalle y en el HUD de obras), elige el sitio de una
// aldea en el mapa 2D sobre un punto verde (la obra de aldea, igual) y cancela la obra del extractor con su confirmación. Después, «Fundación».
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
        [Category("Plaza")]
        [UnityTest, Timeout(900_000)]
        public IEnumerator TuTerritorio()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza");
            string nick = null;
            yield return FundarPlaza(url, x => nick = x);
            foreach (var k in Preferencias) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.DeleteKey("mundo.mapa2d.fertilidad");
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
            LogAssert.ignoreFailingMessages = true; // el error de relleno del escuadrón vacío al nacer el héroe en FeudoScene
            selector.confirmButton.onClick.Invoke();
            yield return Esperar(() => raiz.Q("plaza") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior de la plaza");
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            yield return Quieto(1.5f);
            V1.Heroe Yo() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            var plazaId = Yo().Ubicacion.AsentamientoId;
            System.Collections.Generic.List<V1.ObraDeMapa> Obras() => conexion.Sesion.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Obra).Select(e => e.Obra).Where(o => o.AsentamientoMadreId == plazaId).ToList();

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
                // Territorio en el riel, entre Defensa y Reclutamiento; a ventana completa, con «Marcar» abierto (aún sin pueblos).
                var riel = raiz.Q("plaza").Q("riel");
                var orden = riel.Query<Button>(className: "plaza-riel-boton").ToList().Select(b => b.name).ToList();
                Assert.AreEqual(orden.IndexOf("riel-defensa") + 1, orden.IndexOf("riel-territorio"), "Territorio, detrás de Defensa: " + string.Join(",", orden));
                yield return Clic(riel.Q<Button>("riel-territorio"));
                yield return Esperar(() => Ventana()?.Q("territorio") != null, 5, "la ventana Territorio");
                Assert.IsTrue(Ventana().ClassListContains("plaza-ventana-completa"), "a ventana completa");
                CollectionAssert.AreEqual(new[] { "sub-pueblos", "sub-fundacion" }, Ventana().Query<Button>(className: "plaza-subtab").ToList().Select(b => b.name).ToArray());
                Assert.IsNotNull(Ventana().Q("zona-bosque"), "el bosque en la zona de influencia");
                Assert.IsTrue(Ventana().Q("zona-de-influencia").childCount >= 2, "los nodos de la zona y el bosque");
                Assert.IsNotNull(Ventana().Q("marcar"), "«Marcar» abierto de serie");
                StringAssert.StartsWith("0 / ", Ventana().Q("cupo-pueblos").Q<Label>().text, "el cupo vacío");
                yield return Capturar("115_territorio");
                yield return EnTamanos(raiz, "115_territorio", () => DentroDelMarco(Ventana(), "Territorio"));

                // Un extractor sobre el nodo libre más cercano: «⛏ Marcar».
                var botonNodo = Ventana().Q("marcar-extractor").Query<Button>(className: "territorio-nodo").First();
                Assume.That(botonNodo, Is.Not.Null, "un nodo de piedra o mineral libre en la zona");
                yield return Clic(botonNodo);
                yield return Clic(Ventana().Q<Button>("marcar-nodo"));
                for (var fin = Time.realtimeSinceStartup + 10; Time.realtimeSinceStartup < fin && !Obras().Any(o => o.Tipo == V1.TipoObra.ObraPueblo);) yield return null;
                Assert.IsTrue(Obras().Any(o => o.Tipo == V1.TipoObra.ObraPueblo), "la obra del extractor; aviso: " + raiz.Q<Label>(className: "toast")?.text);
                var obraPueblo = Obras().First(o => o.Tipo == V1.TipoObra.ObraPueblo);
                yield return Esperar(() => Ventana()?.Q("pueblo-abrir-" + obraPueblo.Id) != null, 5, "la obra en la fila");
                Assert.IsNotNull(raiz.Q("obras-hud")?.Q("obra-mapa-" + obraPueblo.Id), "la obra en el HUD de obras");
                yield return Clic(Ventana().Q<Button>("pueblo-abrir-" + obraPueblo.Id));
                yield return Esperar(() => Ventana()?.Q("tarjeta-" + obraPueblo.Id) != null, 5, "el detalle de la obra");
                Assert.IsTrue(Ventana().Q("tarjeta-" + obraPueblo.Id).Query(className: "territorio-linea").ToList().Count > 0, "sus materiales");
                yield return Capturar("116_obra_de_extractor");
                yield return EnTamanos(raiz, "116_obra_de_extractor", () => DentroDelMarco(Ventana(), "la obra"));

                // La aldea: «Elegir el sitio» abre el mapa 2D eligiendo, con la zona y los sitios verdes; un clic en uno la marca y cierra el mapa.
                yield return Clic(Ventana().Q<Button>("pueblo-abrir-marcar"));
                yield return Esperar(() => Ventana()?.Q("marcar-aldea") != null, 5, "«Marcar» otra vez");
                yield return Clic(Ventana().Q<Button>("elegir-aldea"));
                yield return Esperar(() => pantallas.Mapa2D?.Eleccion != null && pantallas.Mapa2D.Cosas.Count > 0, 10, "el mapa 2D eligiendo sitio");
                var eleccion = pantallas.Mapa2D.Eleccion;
                Assert.IsTrue(eleccion.Zona.Count > 2, "la zona de la plaza");
                Assert.IsFalse(raiz.Q("mapa-eleccion").ClassListContains("oculto"), "la franja con el texto");
                yield return Quieto(0.5f);
                yield return Capturar("117_elegir_sitio_de_aldea");
                yield return EnTamanos(raiz, "117_elegir_sitio_de_aldea");
                Assume.That(eleccion.SitiosValidos?.Count ?? 0, Is.GreaterThan(0), "tierra buena para el trigo en el anillo");
                // Un clic en el primer sitio verde que se ve (dentro del lienzo, lejos de sus bordes, de la franja y de los botones): lo que el mapa enseña
                // en verde, el motor lo acepta (tierra firme como él la mira: no agua y transitable).
                {
                    var lienzo2D = pantallas.Mapa2D.Q("mapa-lienzo").contentRect;
                    var sitio = eleccion.SitiosValidos.First(q =>
                    {
                        var l = pantallas.Mapa2D.ALienzo(q.x, q.y);
                        return l.x > 80 && l.x < lienzo2D.width - 80 && l.y > 130 && l.y < lienzo2D.height - 80;
                    });
                    var textura = doc.panelSettings.targetTexture;
                    try
                    {
                        doc.panelSettings.targetTexture = null;
                        for (int i = 0; i < 8; i++) yield return null;
                        var local = pantallas.Mapa2D.ALienzo(sitio.x, sitio.y);
                        var p = pantallas.Mapa2D.Q("mapa-lienzo").LocalToWorld(local);
                        var donde = new Vector2(p.x * Screen.width / raiz.worldBound.width, Screen.height - p.y * Screen.height / raiz.worldBound.height);
                        UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde });
                        yield return null;
                        yield return null;
                        yield return PantallasDeMundoTests.Clic(raton, donde);
                    }
                    finally { doc.panelSettings.targetTexture = textura; }
                }
                yield return Esperar(() => Obras().Any(o => o.Tipo == V1.TipoObra.ObraAldea), 10, "la obra de la aldea; franja: " + raiz.Q<Label>("mapa-eleccion-error")?.text);
                yield return Esperar(() => pantallas.Mapa2D == null, 5, "el mapa se cierra al aceptarse");
                var obraAldea = Obras().First(o => o.Tipo == V1.TipoObra.ObraAldea);
                yield return Esperar(() => Ventana()?.Q("pueblo-abrir-" + obraAldea.Id) != null, 5, "la obra de la aldea en la fila");
                StringAssert.StartsWith("2 / ", Ventana().Q("cupo-pueblos").Q<Label>().text, "el cupo con las dos obras");
                yield return Capturar("118_dos_obras");

                // Cancelar la obra del extractor, con su confirmación.
                yield return Clic(Ventana().Q<Button>("pueblo-abrir-" + obraPueblo.Id));
                yield return Esperar(() => Ventana()?.Q<Button>("cancelar-" + obraPueblo.Id) != null, 5, "«✕ Cancelar»");
                yield return Clic(Ventana().Q<Button>("cancelar-" + obraPueblo.Id));
                yield return Confirmar("cancelar la obra");
                yield return Esperar(() => !Obras().Any(o => o.Id == obraPueblo.Id), 10, "la obra cancelada");
                yield return Esperar(() => Ventana()?.Q("pueblo-abrir-" + obraPueblo.Id) == null, 5, "fuera de la fila");

                // Fundación: la caravana nueva con su coste y sus requisitos (nivel 1: no se puede lanzar).
                yield return Clic(Ventana().Q<Button>("sub-fundacion"));
                yield return Esperar(() => Ventana()?.Q("fundacion") != null, 5, "la subpestaña Fundación");
                Assert.IsFalse(Ventana().Q<Button>("lanzar-fundacion").enabledSelf, "sin todos los requisitos, «Lanzar caravana» apagado");
                Assert.AreEqual(5, Ventana().Q("nueva-caravana").Query(className: "plaza-req").ToList().Count, "los cinco requisitos");
                yield return Capturar("119_fundacion");
                yield return EnTamanos(raiz, "119_fundacion", () => DentroDelMarco(Ventana(), "Fundación"));
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
