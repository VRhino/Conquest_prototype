// Por dentro de un pueblo y de una avanzada (paso 9 del bloque 4, tanda 3), con el ratón de verdad contra el motor. El Maestro de Obras funda su
// plaza (Vía Rápida), marca una cantera en el nodo de piedra libre más cercano y coloca una atalaya. Sale al mundo con una escuadra y los
// materiales de las dos obras en el carro, entrega en cada una (marchando a su puerta) y, levantadas, entra en la cantera: el HUD del sitio con su
// riel (Resumen, Guarnición, Acarreo y escolta, Carga) y FeudoScene con su planta; deja la escuadra en la guarnición y sale al mapa. Luego entra en
// la atalaya: Resumen, Guarnición (la de serie y la tuya) y Tropa de serie, y sale.
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
        [UnityTest, Timeout(1_800_000)]
        public IEnumerator PorDentroDelPuebloYDeLaAvanzada()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza y dar suministro");
            string nick = null;
            yield return FundarPlaza(url, x => nick = x);
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
            LogAssert.ignoreFailingMessages = true;
            selector.confirmButton.onClick.Invoke();
            yield return Esperar(() => raiz.Q("plaza") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior de la plaza");
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            yield return Quieto(1.5f);
            var s = conexion.Sesion;
            V1.Heroe Yo() => s.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            var plazaId = Yo().Ubicacion.AsentamientoId;
            var plaza = s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId).Asentamiento;
            System.Collections.Generic.List<V1.ObraDeMapa> Obras() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Obra).Select(e => e.Obra).Where(o => o.AsentamientoMadreId == plazaId).ToList();
            V1.ResultadoOrden ultimo = null;
            IEnumerator Ordenar(V1.Orden o)
            {
                var r = s.Ordenar(o);
                for (var f = Time.realtimeSinceStartup + 20; !r.IsCompleted && Time.realtimeSinceStartup < f;) yield return null;
                ultimo = r.Result;
                Debug.Log($"[Prueba] {o.CuerpoCase}: {ultimo.Resultado} {ultimo.Rechazo?.Codigo}");
            }

            // La cantera en el nodo de piedra libre más cercano de la zona (el id del motor, `nodo-N`), y la atalaya a unas unidades.
            var zona = plaza.Zona.Select(q => (q.X / 1000.0, q.Y / 1000.0)).ToList();
            var (px, py) = (plaza.Posicion.X / 1000.0, plaza.Posicion.Y / 1000.0);
            var piedras = conexion.Artefacto.Nodos.Where(n => n.Tipo == "piedra" && SitiosDeAldea.Dentro(n.Posicion.X / 1000.0, n.Posicion.Y / 1000.0, zona))
                .OrderBy(n => Math.Pow(n.Posicion.X / 1000.0 - px, 2) + Math.Pow(n.Posicion.Y / 1000.0 - py, 2)).ToList();
            foreach (var n in piedras)
            {
                yield return Ordenar(new V1.Orden { MarcarPueblo = new V1.OrdenMarcarPueblo { AsentamientoId = plazaId, HeroeId = Yo().Id, NodoId = NodosDelMotor.Id(conexion.Artefacto, n) } });
                if (ultimo.Resultado == V1.Resultado.OrdenAceptada) break;
            }
            Assume.That(Obras().Any(o => o.Tipo == V1.TipoObra.ObraPueblo), "la obra de la cantera");
            foreach (var (dx, dy) in new[] { (10.0, 0.0), (0.0, 10.0), (-10.0, 0.0), (0.0, -10.0), (8.0, 8.0), (-8.0, -8.0) })
            {
                yield return Ordenar(new V1.Orden { ColocarAvanzada = new V1.OrdenColocarAvanzada { AsentamientoId = plazaId, HeroeId = Yo().Id, Posicion = new V1.Punto { X = (long)((px + dx) * 1000), Y = (long)((py + dy) * 1000) } } });
                if (ultimo.Resultado == V1.Resultado.OrdenAceptada) break;
            }
            Assume.That(Obras().Any(o => o.Tipo == V1.TipoObra.ObraAvanzada), "la obra de la atalaya");
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
            IEnumerator Tecla(UnityEngine.InputSystem.Key k)
            {
                teclado.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
                yield return null;
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
            }
            // Marcha a la puerta de (x, y), con la orden del mapa, y espera a llegar.
            IEnumerator AlaPuerta(V1.Punto p)
            {
                yield return ElMapa().MarcharA(p.X / 1000.0, p.Y / 1000.0).AsIEnumerator();
                bool Llegada() { var c = ElMapa().Columna; return c != null && c.Estado == V1.EstadoColumna.ColumnaEstacionada && Math.Abs(c.Posicion.X - p.X) < 2500 && Math.Abs(c.Posicion.Y - p.Y) < 2500; }
                yield return EsperarConReloj(url, s, Llegada, 90, "la columna a la puerta", 20_000);
                yield return Quieto(0.5f);
            }
            // Elige la cosa del mapa y pulsa la acción de su ficha.
            IEnumerator AccionDeFicha(string id, string texto)
            {
                ConquestTactics.Mundo.CosaDelMapa cosa = null;
                yield return Esperar(() => (cosa = ElMapa().Cosas.FirstOrDefault(c => c.Id == id)) != null, 20, "en el mapa: " + id);
                ElMapa().Seleccionar(cosa);
                Button boton = null;
                yield return Esperar(() => (boton = raiz.Q("ficha")?.Query<Button>().ToList().FirstOrDefault(b => b.Q<Label>()?.text == texto || b.text == texto)) != null, 20, $"«{texto}» en la ficha de {id}");
                yield return Clic(boton);
                // Dentro, FeudoScene trae su EventSystem: el de la prueba (para el mapa) sobra y le quitaría el ratón.
                if (texto == "Entrar" && sistema != null) { UnityEngine.Object.Destroy(sistema); sistema = null; }
            }
            try
            {
                // Al mundo, con la escuadra; los materiales de las dos obras, al carro.
                yield return Clic(raiz.Q<Button>("salir-al-mundo"));
                yield return Esperar(() => raiz.Q("ventana-salida") != null, 5, "la ventana de salida");
                yield return Clic(raiz.Q("ventana-salida").Q<Button>("salir"));
                yield return Esperar(() => raiz.Q("mapa") != null && ElMapa()?.Columna != null, 30, "el mapa con la columna");
                sistema = new GameObject("EventSystem de la prueba", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                var columnaId = ElMapa().Columna.Id;
                foreach (var o in Obras())
                    foreach (var kv in o.Requerido.PorId)
                    {
                        var q = new Google.Protobuf.WellKnownTypes.Struct();
                        q.Fields["ejercito_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(columnaId);
                        q.Fields["recurso"] = Google.Protobuf.WellKnownTypes.Value.ForString(kv.Key);
                        q.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(kv.Value);
                        yield return Administrar(url, "dar_suministro", q);
                    }
                // Entregar en cada obra, a su puerta, desde su ficha.
                foreach (var o in Obras().ToList())
                {
                    yield return AlaPuerta(o.Posicion);
                    ElMapa().Seleccionar(ElMapa().Cosas.FirstOrDefault(c => c.Id == o.Id));
                    yield return Esperar(() => raiz.Q("ficha")?.Q<Button>("entregar-" + o.Id)?.enabledSelf == true, 20, "«Entregar materiales» en " + o.Id);
                    yield return Clic(raiz.Q("ficha").Q<Button>("entregar-" + o.Id));
                    yield return Esperar(() => Obras().FirstOrDefault(x => x.Id == o.Id)?.HasTerminaEnMs != false, 10, "entregado " + o.Id);
                }
                V1.Pueblo Cantera() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Pueblo).Select(e => e.Pueblo).FirstOrDefault(x => x.AsentamientoMadreId == plazaId);
                V1.Avanzada Atalaya() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Avanzada).Select(e => e.Avanzada).FirstOrDefault(x => x.AsentamientoMadreId == plazaId);
                yield return EsperarConReloj(url, s, () => Cantera() != null && Atalaya() != null, 400, "la cantera y la atalaya levantadas", 600_000);

                // Dentro de la cantera.
                yield return AlaPuerta(Cantera().Posicion);
                yield return AccionDeFicha(Cantera().Id, "Entrar");
                yield return Esperar(() => Yo().Ubicacion.DondeCase == V1.UbicacionHeroe.DondeOneofCase.PuebloId, 10, "dentro del pueblo");
                yield return Esperar(() => raiz.Q("plaza") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior del pueblo");
                yield return Esperar(() => SelectorDePersonaje.Interior.Edificios.Count > 0, 20, "la planta del pueblo");
                yield return Esperar(() => raiz.Q("riel")?.Query<Button>().ToList().Count >= 3, 10, "el riel del pueblo");
                CollectionAssert.IsSubsetOf(new[] { "riel-resumen", "riel-guarnicion", "riel-acarreo" }, raiz.Q("riel").Query<Button>().ToList().Select(b => b.name).ToList());
                Assert.IsNotNull(raiz.Q<Label>("nombre-sitio"), "el blasón del pueblo");
                yield return Quieto(1f);
                yield return Capturar("125_dentro_de_la_cantera");
                yield return CapturarEscena("FeudoScene", "125c_cantera_escena");
                // Soltar el ratón con Alt, para el HUD (como en la plaza y el campamento).
                yield return Tecla(UnityEngine.InputSystem.Key.LeftAlt);
                yield return Clic(raiz.Q<Button>("riel-resumen"));
                yield return Esperar(() => raiz.Q("sitio-resumen") != null, 5, "Resumen");
                yield return EnTamanos(raiz, "125b_resumen_de_la_cantera");
                yield return Clic(raiz.Q<Button>("riel-guarnicion"));
                yield return Esperar(() => raiz.Q("sitio-guarnicion") != null, 5, "Guarnición");
                var dejar = raiz.Q("sitio-guarnicion").Query<Button>().ToList().FirstOrDefault(b => b.name.StartsWith("dejar-") && b.enabledSelf);
                Assert.IsNotNull(dejar, "«Dejar» la escuadra de la columna");
                var sid = dejar.name.Substring("dejar-".Length);
                yield return Clic(dejar);
                yield return Esperar(() => s.Almacen.Buscar(V1.TipoEntidad.Escuadron, sid)?.Escuadron?.Contenedor?.PuebloId == Cantera().Id, 10, "la escuadra en la guarnición del pueblo");
                yield return Esperar(() => raiz.Q("sitio-guarnicion")?.Q<Button>("retirar-" + sid) != null, 5, "«Retirar» la tuya");
                yield return EnTamanos(raiz, "126_guarnicion_del_pueblo");
                yield return Clic(raiz.Q("sitio-guarnicion").Q<Button>("retirar-" + sid));
                yield return Esperar(() => s.Almacen.Buscar(V1.TipoEntidad.Escuadron, sid)?.Escuadron?.Contenedor?.EjercitoId != null, 10, "la escuadra de vuelta en la columna");
                yield return Clic(raiz.Q<Button>("riel-acarreo"));
                yield return Esperar(() => raiz.Q("sitio-acarreo") != null, 5, "Acarreo y escolta");
                yield return EnTamanos(raiz, "127_acarreo_y_escolta");
                if (raiz.Q<Button>("riel-carga") != null)
                {
                    yield return Clic(raiz.Q<Button>("riel-carga"));
                    yield return Esperar(() => raiz.Q("sitio-carga") != null, 5, "Carga");
                    yield return Capturar("128_carga_del_pueblo");
                }
                yield return Clic(raiz.Q<Button>("salir-al-mundo"));
                yield return Esperar(() => raiz.Q(className: "dialogo-velo")?.Q<Button>(className: "boton-primario") != null, 5, "la salida");
                yield return Clic(raiz.Q(className: "dialogo-velo").Q<Button>(className: "boton-primario"));
                yield return Esperar(() => raiz.Q("mapa") != null && ElMapa()?.Columna != null, 30, "de vuelta en el mapa");
                sistema = new GameObject("EventSystem de la prueba", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

                // Dentro de la atalaya.
                yield return AlaPuerta(Atalaya().Posicion);
                yield return AccionDeFicha(Atalaya().Id, "Entrar");
                yield return Esperar(() => raiz.Q("plaza") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior de la atalaya");
                yield return Esperar(() => SelectorDePersonaje.Interior.Muralla.Count > 0, 10, "la empalizada de la atalaya");
                CollectionAssert.IsSubsetOf(new[] { "riel-resumen", "riel-guarnicion", "riel-tropa" }, raiz.Q("riel").Query<Button>().ToList().Select(b => b.name).ToList());
                yield return Quieto(1f);
                yield return Capturar("129_dentro_de_la_atalaya");
                yield return CapturarEscena("FeudoScene", "129c_atalaya_escena");
                yield return Tecla(UnityEngine.InputSystem.Key.LeftAlt);
                yield return Clic(raiz.Q<Button>("riel-guarnicion"));
                yield return Esperar(() => raiz.Q("sitio-guarnicion") != null, 5, "Guarnición de la atalaya");
                yield return EnTamanos(raiz, "130_guarnicion_de_la_atalaya");
                yield return Clic(raiz.Q<Button>("riel-tropa"));
                yield return Esperar(() => raiz.Q("sitio-tropa") != null, 5, "Tropa de serie");
                yield return EnTamanos(raiz, "131_tropa_de_la_atalaya");
                yield return Clic(raiz.Q<Button>("salir-al-mundo"));
                yield return Esperar(() => raiz.Q(className: "dialogo-velo")?.Q<Button>(className: "boton-primario") != null, 5, "la salida");
                yield return Clic(raiz.Q(className: "dialogo-velo").Q<Button>(className: "boton-primario"));
                yield return Esperar(() => raiz.Q("mapa") != null && ElMapa()?.Columna != null, 30, "de vuelta en el mapa");
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

    static class TareasEnPruebas
    {
        /// <summary>Espera a una tarea dentro de una corrutina.</summary>
        public static IEnumerator AsIEnumerator(this System.Threading.Tasks.Task t)
        {
            while (!t.IsCompleted) yield return null;
        }
    }
}
