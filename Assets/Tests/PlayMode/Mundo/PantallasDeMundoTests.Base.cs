// La Base personal (paso 6 del bloque 4, tanda 1) de punta a punta contra el servidor de prueba, con teclado y ratón de verdad: un héroe nuevo en su
// campamento con una escuadra prestada y otra suya mermada; B abre la Base sobre el interior (la barra solo con la Base y los Avisos), el retrato 3D
// se gira arrastrando, el riel con sus huecos vacíos, la ficha de la escuadra al pulsarla, la formación (guardada en el motor) y «Reponer» con su
// coste. Bienes: al carro y «Todo al almacén personal». Esc la cierra. En «Salir al mundo», ▶ cambia el orden y el motor lo guarda al salir. En
// el mapa, el yelmo de la barra la abre y el menú tiene lo demás. Capturas de Tropas y Bienes a los cuatro tamaños.
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
        [UnityTest, Timeout(600_000)]
        public IEnumerator LaBasePersonal()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta dar oro y una escuadra mermada");
            string nick = null;
            // Con aspecto (pelo y cejas), como uno hecho en el creador.
            var aspecto = new V1.OrdenCrearHeroe.Types.Avatar { CabezaId = "head_03", PeloId = "hair_02", BarbaId = "", CejasId = "eyebrow_01" };
            yield return OtroHeroe(url, "Base" + DateTime.UtcNow.Ticks % 1_000_000, "espada_escudo_mediano", "masculino", aspecto, x => nick = x);
            foreach (var k in Preferencias) PlayerPrefs.DeleteKey(k);
            go = new GameObject("Mundo");
            go.SetActive(false);
            var conexion = go.AddComponent<ConexionDeMundo>();
            typeof(ConexionDeMundo).GetField("servidor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(conexion, url);
            var pantallas = go.AddComponent<PantallasDeMundo>();
#if UNITY_EDITOR
            // Como en MundoScene: los prefabs visuales de Conquest para los modelos 3D.
            pantallas.PrefabsVisuales = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>("Assets/ScriptableObjects/VisualPrefabConfig.asset");
#endif
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
            yield return Esperar(() => raiz.Q("interior") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior");
            // La pantalla de carga, del todo fuera (con todo ya cargado de otra prueba, se llega al interior mientras aún se desvanece).
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            yield return new WaitForSecondsRealtime(1f);
            var interior = SelectorDePersonaje.Interior;
            V1.Heroe Yo() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            V1.Escuadron Escuadra(string id) => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Escuadron, id)?.Escuadron;
            var campId = Yo().Ubicacion.CampamentoId;
            V1.CampamentoMercenarios Camp() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.CampamentoMercenarios, campId).CampamentoMercenarios;

            // Lo que hace falta para ver la Base con algo: oro y trigo en el almacén, una escuadra prestada y otra suya mermada (10 hombres).
            yield return DarAlmacen(url, conexion.HeroeId, "oro", 400_000);
            yield return DarAlmacen(url, conexion.HeroeId, "trigo", 60_000);
            var leva = conexion.Datos.TropasReclutables.OfType<Newtonsoft.Json.Linq.JObject>().First(t => (string)t["tecnologia"] == "leva_comunal");
            var prestamo = new V1.OrdenPedirPrestamo();
            prestamo.TropaIds.Add((string)leva["id"]);
            var pedir = conexion.Sesion.Ordenar(new V1.Orden { PedirPrestamo = prestamo });
            yield return Esperar(() => pedir.IsCompleted, 10, "el préstamo");
            Assert.AreEqual(V1.Resultado.OrdenAceptada, pedir.Result.Resultado, pedir.Result.Rechazo?.Codigo);
            yield return Esperar(() => Camp().Reclutables.Count > 0, 10, "lo que ofrece el campamento");
            // La suya, de una tropa que el campamento ya recluta (si no hay ninguna, de la primera: entonces no se puede reponer aquí).
            var oferta = Camp().Reclutables.FirstOrDefault(o => o.Desbloqueada) ?? Camp().Reclutables.First();
            Debug.Log($"[Prueba] el campamento ofrece: {string.Join(", ", Camp().Reclutables.Select(o => o.TropaId + (o.Desbloqueada ? "" : " (aún no)")))}");
            var escuadra = new Google.Protobuf.WellKnownTypes.Struct();
            escuadra.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(conexion.HeroeId);
            escuadra.Fields["tropa_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(oferta.TropaId);
            escuadra.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(10);
            yield return Administrar(url, "dar_escuadra", escuadra);
            yield return Esperar(() => Yo().EscuadronesIds.Count >= 2 && Milesimas(Yo().AlmacenPersonal, "oro") >= 400, 10, "las dos escuadras y el oro");
            // Un objeto en la mochila: quitarse las botas (`desequiparObjeto`) las deja en ella.
            var desequipar = conexion.Sesion.Ordenar(new V1.Orden { DesequiparObjeto = new V1.OrdenDesequiparObjeto { Hueco = "botas" } });
            yield return Esperar(() => desequipar.IsCompleted, 10, "quitarse las botas");
            Assert.AreEqual(V1.Resultado.OrdenAceptada, desequipar.Result.Resultado, desequipar.Result.Rechazo?.Codigo);
            yield return Esperar(() => Yo().Inventario.Count > 0, 10, "las botas en la mochila");
            var mia = Yo().EscuadronesIds.Select(Escuadra).First(s => !s.HasPrestadaPorCampamentoId);
            Debug.Log($"[Prueba] escuadras: {string.Join(", ", Yo().EscuadronesIds.Select(Escuadra).Select(s => $"{s.TropaId} {s.Cantidad}{(s.HasPrestadaPorCampamentoId ? " prestada" : "")}"))}");

            var entrada = UnityEngine.InputSystem.InputSystem.settings;
            var fondo = entrada.backgroundBehavior;
            var editorAntes = entrada.editorInputBehaviorInPlayMode;
            entrada.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            entrada.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var teclado = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            // Un solo ratón para toda la prueba, como el de verdad (uno que aparece y desaparece en cada clic no lo ve la interfaz sin EventSystem).
            // Otros ratones (el del sistema o uno que dejó otra prueba) se apagan mientras tanto: con dos, el módulo de la interfaz sigue al otro.
            var otros = UnityEngine.InputSystem.InputSystem.devices.OfType<UnityEngine.InputSystem.Mouse>().Where(m => m.enabled).ToList();
            foreach (var m in otros) UnityEngine.InputSystem.InputSystem.DisableDevice(m);
            var raton = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            raton.MakeCurrent();
            IEnumerator Tecla(UnityEngine.InputSystem.Key k)
            {
                teclado.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
                yield return null;
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
            }
            try
            {
                // B: la Base sobre el interior. El héroe no anda, el ratón queda libre, y la barra solo tiene la Base, la Facción (sin ella: elegirla,
                // paso 8) y los Avisos.
                yield return Tecla(UnityEngine.InputSystem.Key.B);
                yield return Esperar(() => raiz.Q("base-personal") != null, 5, "la Base con B");
                Assert.IsTrue(interior.PanelAbierto, "con la Base abierta el héroe no anda");
                var barra = raiz.Q(className: "barra-arriba");
                var enLaBarra = barra.Query<Button>().ToList().Where(Visible).Select(b => b.name).ToList();
                CollectionAssert.AreEquivalent(new[] { "base", "faccion", "avisos" }, enLaBarra, "la barra con la Base abierta: " + string.Join(", ", enLaBarra));
                var basePersonal = raiz.Q("base-personal");
                CollectionAssert.AreEqual(new[] { "pestana-heroe", "pestana-tropas", "pestana-loadouts", "pestana-bienes" }, basePersonal.Query<Button>(className: "base-pestana").ToList().Select(p => p.name).ToArray(), "cuatro pestañas");

                // Héroe (calco del detalle del héroe de Conquest): atributos, el héroe en 3D de frente y con su pelo, y las siete casillas del equipo.
                yield return Esperar(() => raiz.Q("heroe-atributos") != null, 5, "la pestaña Héroe");
                var atributos = raiz.Q("heroe-atributos").Query(className: "heroe-atributo").ToList();
                Assert.AreEqual(5, atributos.Count, "Liderazgo, Fuerza, Destreza, Armadura y Dureza");
                Debug.Log("[Prueba] atributos: " + string.Join(" | ", atributos.Select(f => string.Join(" ", f.Query<Label>().ToList().Select(l => l.text)))));
                var heroeYo = Yo();
                StringAssert.Contains(heroeYo.AtributosBase.Vitalidad.ToString(), raiz.Q("atributo-dureza").Q<Label>(className: "heroe-atributo-valor").text, "Dureza = vitalidad del motor");
                Assert.AreEqual(7, raiz.Q("heroe-equipo").Query(className: "equipo-casilla").ToList().Count, "siete casillas con la montura");
                StringAssert.Contains("nada puesto", raiz.Q("equipo-botas").tooltip, "las botas, quitadas");
                Debug.Log("[Prueba] equipo: " + string.Join(" | ", raiz.Q("heroe-equipo").Query(className: "equipo-casilla").ToList().Select(x => x.tooltip)));
                Assert.IsTrue(raiz.Q("heroe-modelo").ClassListContains("base-modelo--3d"), "el héroe en 3D");
                var modelo = GameObject.Find("Modelo de la Base hero_preview").transform.GetChild(0);
                Assert.Less(Mathf.Abs(Mathf.DeltaAngle(0, modelo.eulerAngles.y)), 1f, "de frente");
                var pelos = modelo.GetComponentsInChildren<Renderer>().Where(x => x.enabled && x.gameObject.activeInHierarchy && x.name.ToLowerInvariant().Contains("hair")).Select(x => x.name).ToList();
                Debug.Log("[Prueba] pelo a la vista: " + string.Join(", ", pelos));
                Assert.IsNotEmpty(pelos, "con el pelo que se eligió (hair_02)");
                yield return GuardarModelo(raiz, "heroe-modelo", "47_heroe_modelo");
                yield return Capturar("47_base_heroe");
                FondoOpaco(raiz, "47_base_heroe");
                yield return EnTamanos(raiz, "47_base_heroe", () => { FondoOpaco(raiz, "47_base_heroe"); SinBarras(raiz, "Héroe"); });
                var giroAntes = modelo.eulerAngles.y;
                yield return ArrastrarUITK(raton, raiz, raiz.Q("heroe-modelo"), 120);
                Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(giroAntes, modelo.eulerAngles.y)), 10, "el héroe gira al arrastrar");

                // Tropas: la rejilla con sus huecos vacíos, la tropa en 3D y sus datos.
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("pestana-tropas"));
                yield return Esperar(() => raiz.Q("tropas-rejilla") != null, 5, "la pestaña Tropas");
                Assert.IsNull(GameObject.Find("Modelo de la Base hero_preview"), "sin el héroe en 3D fuera de su pestaña");
                Assert.AreEqual(8, raiz.Q("tropas-rejilla").Query(className: "tropas-hueco").ToList().Count, "dos filas de cuatro, con los huecos vacíos");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("escuadra-" + mia.Id));
                yield return Esperar(() => raiz.Q("tropas-datos")?.Q<Label>(className: "tropas-nombre") != null && raiz.Q<Button>("escuadra-" + mia.Id).Q(className: "tropa-tarjeta--elegida") != null, 5, "la suya elegida");
                Assert.IsTrue(raiz.Q("tropa-modelo").ClassListContains("base-modelo--3d"), "la tropa en 3D");
                StringAssert.Contains($"{mia.Cantidad}/", string.Join(" ", raiz.Q("tropas-cifras").Query<Label>().ToList().Select(l => l.text)), "sus hombres");
                Assert.IsFalse(GameObject.Find("Modelo de la Base squad_preview").GetComponentsInChildren<MeshRenderer>().Any(x => x.enabled && x.name == "Cube"), "sin el cubo marcador encima");
                yield return GuardarModelo(raiz, "tropa-modelo", "48_tropa_modelo");
                yield return Capturar("48_base_tropas");
                yield return EnTamanos(raiz, "48_base_tropas", () => SinBarras(raiz, "Tropas"));
                // «Reponer» solo si el campamento ya recluta su tropa (en un mundo recién hecho aún no recluta nada: entonces no sale).
                Assert.AreEqual(oferta.Desbloqueada, raiz.Q("tropas-datos").Q<Button>("reponer") != null, "«Reponer» solo donde se puede");
                // Una formación: el botón de otra, y el motor la guarda.
                var antesFormacion = Escuadra(mia.Id).Formacion;
                var otra = raiz.Q("formaciones").Query<Button>().ToList().FirstOrDefault(b => !b.ClassListContains("activo"));
                Assert.IsNotNull(otra, "más de una formación para elegir");
                yield return ClicFijo(raton, raiz, otra);
                yield return Esperar(() => Escuadra(mia.Id).Formacion != antesFormacion, 10, "la formación guardada en el motor");
                Debug.Log($"[Prueba] formación: «{antesFormacion}» → «{Escuadra(mia.Id).Formacion}»");
                // Atributos: los del panel del barracón.
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("subpestana-atributos"));
                yield return Esperar(() => raiz.Q("tropas-datos").Query(className: "tropas-atributo").ToList().Count > 10, 5, "los atributos de la tropa");
                Debug.Log("[Prueba] atributos de la tropa: " + string.Join(" | ", raiz.Q("tropas-datos").Query(className: "tropas-atributo").ToList().Select(f => string.Join(" ", f.Query<Label>().ToList().Select(l => l.text)))));
                yield return Capturar("49_base_tropas_atributos");
                yield return EnTamanos(raiz, "49_base_tropas_atributos", () => SinBarras(raiz, "Atributos de la tropa"));
                // Los filtros: a distancia no tiene ninguna (las dos son de infantería).
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("filtro-Distance"));
                yield return Esperar(() => raiz.Q("tropas-rejilla").Query<Button>(className: "tropas-hueco").ToList().Count == 0, 5, "el filtro a distancia");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("filtro-todas"));
                yield return Esperar(() => raiz.Q("tropas-rejilla").Query<Button>(className: "tropas-hueco").ToList().Count == 2, 5, "todas otra vez");
                if (oferta.Desbloqueada)
                {
                    var oroAntes = Milesimas(Yo().AlmacenPersonal, "oro");
                    yield return ClicFijo(raton, raiz, raiz.Q<Button>("subpestana-caracteristicas"));
                    yield return Esperar(() => raiz.Q("tropas-datos")?.Q<Button>("reponer") != null, 5, "«Reponer»");
                    yield return ClicFijo(raton, raiz, raiz.Q("tropas-datos").Q<Button>("reponer"));
                    yield return Esperar(() => Escuadra(mia.Id).Cantidad > mia.Cantidad, 10, "la escuadra repuesta");
                    Assert.Less(Milesimas(Yo().AlmacenPersonal, "oro"), oroAntes, "reponer cuesta oro");
                }
                else Debug.Log("[Prueba] el campamento aún no recluta " + oferta.TropaId + ": «Reponer» no sale y no se prueba");

                // Bienes: el almacén de objetos (144) y la mochila (72) en casillas; un objeto de la mochila al almacén (dentro de tu residencia, con
                // la orden del motor); luego Recursos (del almacén al carro) y Carro («Todo al almacén personal»).
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("pestana-bienes"));
                yield return Esperar(() => raiz.Q("rejilla-almacen") != null && raiz.Q("rejilla-mochila") != null, 5, "Bienes");
                Assert.IsNull(GameObject.Find("Modelo de la Base squad_preview"), "sin la tropa en 3D fuera de su pestaña");
                Assert.AreEqual(144, raiz.Q("rejilla-almacen").childCount, "las casillas del almacén de objetos");
                Assert.AreEqual(72, raiz.Q("rejilla-mochila").childCount, "las casillas de la mochila");
                Debug.Log($"[Prueba] mochila: {string.Join(", ", Yo().Inventario.Select(x => $"{x.ItemDefinitionId}@{x.CasillaInventario}"))}; almacén: {string.Join(", ", Yo().AlmacenObjetos.Select(x => $"{x.ItemDefinitionId}@{x.CasillaInventario}"))}");
                yield return Capturar("42_base_bienes");
                yield return EnTamanos(raiz, "42_base_bienes", () => SinBarras(raiz, "Bienes (las 144 y 72 casillas)"));
                var enMochila = Yo().Inventario.FirstOrDefault();
                if (enMochila != null)
                {
                    var antesAlmacen = Yo().AlmacenObjetos.Count;
                    yield return ClicFijo(raton, raiz, raiz.Q<Button>("mochila-" + enMochila.CasillaInventario));
                    yield return Esperar(() => raiz.Q<Button>("mover-mochila") != null, 5, "«Al almacén» del objeto elegido");
                    yield return Capturar("42b_base_objeto_elegido");
                    yield return ClicFijo(raton, raiz, raiz.Q<Button>("mover-mochila"));
                    yield return Esperar(() => Yo().AlmacenObjetos.Count > antesAlmacen, 10, "el objeto en el almacén");
                    yield return Esperar(() => raiz.Q("rejilla-almacen")?.Query<Button>(className: "bienes-casilla").ToList().Count > 0, 5, "el objeto en la rejilla del almacén");
                    yield return Capturar("42c_base_objeto_en_el_almacen");
                }
                else Debug.Log("[Prueba] la mochila del héroe nuevo está vacía: no se prueba pasar un objeto");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("subpestana-recursos"));
                yield return Esperar(() => raiz.Q<IntegerField>("cantidad-personal-trigo") != null, 5, "Recursos");
                raiz.Q<IntegerField>("cantidad-personal-trigo").value = 20;
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("personal-trigo-al-carro"));
                yield return Esperar(() => Milesimas(Yo().FueraCarro, "trigo") >= 20, 10, "el trigo en el carro");
                // (la orden repinta la Base 0,4 s después: el botón de antes puede quedar suelto a mitad del clic; ClicEnVentana busca el nuevo por su nombre)
                yield return ClicEnVentana(raton, raiz, raiz.Q<Button>("subpestana-carro"));
                yield return Esperar(() => raiz.Q<Button>("vaciar-carro") != null, 5, "«Todo al almacén personal»");
                yield return Capturar("43_base_bienes_con_carro");
                yield return EnTamanos(raiz, "43_base_bienes_con_carro", () => SinBarras(raiz, "Recursos y Carro"));
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("vaciar-carro"));
                for (var fin = Time.realtimeSinceStartup + 10; Time.realtimeSinceStartup < fin && Milesimas(Yo().FueraCarro, "trigo") >= 1;) yield return null;
                Assert.Less(Milesimas(Yo().FueraCarro, "trigo"), 1, "el carro vacío; error: " + raiz.Q<Label>("error-base")?.text);

                // Esc cierra; se vuelve a andar.
                yield return Tecla(UnityEngine.InputSystem.Key.Escape);
                yield return Esperar(() => raiz.Q("base-personal") == null, 5, "la Base cerrada con Esc");
                Assert.IsFalse(interior.PanelAbierto, "cerrada, el héroe anda");
                Assert.IsNull(GameObject.Find("Retrato de la Base"), "sin el retrato al cerrar");

                // «Salir al mundo»: ▶ en la primera tarjeta cambia el orden de combate, y al salir el motor lo tiene.
                Pulsar(raiz.Q<Button>("salir-al-mundo"));
                yield return Esperar(() => raiz.Q("ventana-salida") != null, 5, "la ventana de salida");
                var ventana = raiz.Q("ventana-salida");
                var riel = ventana.Q("riel").Query(className: "salida-hueco-tropa").ToList().Select(h => h.name.Substring("hueco-".Length)).ToList();
                Assume.That(riel.Count, Is.GreaterThanOrEqualTo(2), "dos escuadras en el riel");
                yield return ClicFijo(raton, raiz, ventana.Q<Button>("despues-" + riel[0]));
                var ahora = raiz.Q("ventana-salida").Q("riel").Query(className: "salida-hueco-tropa").ToList().Select(h => h.name.Substring("hueco-".Length)).ToList();
                CollectionAssert.AreEqual(new[] { riel[1], riel[0] }, ahora.Take(2).ToArray(), "▶ la pasa detrás");
                yield return Capturar("44_salir_con_orden");
                yield return EnTamanos(raiz, "44_salir_con_orden");
                // Las dos no caben en su Liderazgo (14): sale con la que ahora va primera, y el motor la pone la primera del héroe.
                yield return ClicFijo(raton, raiz, raiz.Q("ventana-salida").Q<Button>("quitar-" + riel[0]));
                yield return ClicFijo(raton, raiz, raiz.Q("ventana-salida").Q<Button>("salir"));
                yield return Esperar(() => raiz.Q("mapa") != null && ElMapa()?.Columna != null, 30, "el mapa con la columna");
                Assert.AreEqual(riel[1], Yo().EscuadronesIds[0], "el motor guarda el orden elegido");
                Assert.AreEqual(riel[0], Yo().EscuadronesIds[1], "la que se quedó, detrás");

                // En el mapa: el yelmo abre la Base (sin «Reponer» fuera del campamento), y el menú tiene lo demás.
                // En el mapa no hay EventSystem (MundoScene no lo tiene) y, en modo batch, sin el foco de la ventana, el sistema de eventos propio de
                // UI Toolkit no recibe el ratón (0 pulsaciones medidas). Para que el ratón siga siendo de verdad, la prueba pone uno como el del
                // interior (EventSystem con el módulo del Input System) mientras dura esta parte.
                var sistema = new GameObject("EventSystem de la prueba", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                yield return null;
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("base"));
                for (var fin = Time.realtimeSinceStartup + 2; Time.realtimeSinceStartup < fin && raiz.Q("base-personal") == null;) yield return null;
                if (raiz.Q("base-personal") == null)
                {
                    Debug.Log($"[Prueba] el clic en el yelmo no abrió la Base: yelmo en {raiz.Q<Button>("base").worldBound}, interior {SelectorDePersonaje.Interior != null}, cursor {UnityEngine.Cursor.lockState} {UnityEngine.Cursor.visible}, pantalla {string.Join(",", raiz.Query<Button>().ToList().Where(Visible).Select(b => b.name))}; "
                        + $"sistemas de eventos: {string.Join(", ", UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(m => m.GetType().Name.Contains("EventSystem") || m.GetType().Name.Contains("InputModule")).Select(m => $"{m.GetType().Name} en {m.gameObject.name} ({m.gameObject.scene.name}, {(m.isActiveAndEnabled ? "activo" : "apagado")})"))}");
                    Pulsar(raiz.Q<Button>("base"));
                }
                yield return Esperar(() => raiz.Q("base-personal") != null, 5, "la Base en el mapa");
                Assert.IsTrue(ElMapa().EntradaApagada, "con la Base, el mapa no se mueve");
                yield return Capturar("45_base_en_el_mapa");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("base-cerrar"));
                yield return Esperar(() => raiz.Q("base-personal") == null, 5, "cerrada con «Cerrar»");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("menu"));
                Assert.IsTrue(Visible(raiz.Q("menu-barra")), "el menú abierto");
                CollectionAssert.IsSubsetOf(new[] { "opciones", "capa-nodos", "capa-fertilidad", "marcha-forzada", "cambiar-partida", "cerrar-sesion" },
                    raiz.Q("menu-barra").Query<Button>().ToList().Select(b => b.name).ToList());
                Assert.AreEqual("opciones", raiz.Q("menu-barra").Query<Button>().First().name, "«Opciones», la primera del menú");
                yield return Capturar("46_menu_de_la_barra");

                // La ventana «Opciones»: «Estandarte de las tropas», encendida por defecto; se apaga con el ratón, se guarda y Esc la cierra.
                PlayerPrefs.DeleteKey(OpcionesDelJuego.EstandarteDeTropas);
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("opciones"));
                yield return Esperar(() => raiz.Q("ventana-opciones")?.Q("opcion-" + OpcionesDelJuego.EstandarteDeTropas) != null, 5, "la ventana «Opciones»");
                var interruptor = raiz.Q("ventana-opciones").Q("opcion-" + OpcionesDelJuego.EstandarteDeTropas);
                Assert.IsTrue(interruptor.ClassListContains("encendido"), "encendida por defecto");
                yield return EnTamanos(raiz, "46b_opciones", () => DentroDelMarco(raiz.Q("ventana-opciones").Q(className: "placa"), "las Opciones"));
                yield return ClicFijo(raton, raiz, raiz.Q("ventana-opciones").Q("opcion-" + OpcionesDelJuego.EstandarteDeTropas));
                yield return Esperar(() => !OpcionesDelJuego.Valor(OpcionesDelJuego.EstandarteDeTropas), 5, "apagada y guardada");
                Assert.AreEqual(0, PlayerPrefs.GetInt(OpcionesDelJuego.EstandarteDeTropas, 1), "en PlayerPrefs");
                yield return Capturar("46c_opciones_apagada");
                yield return Tecla(UnityEngine.InputSystem.Key.Escape);
                yield return Esperar(() => raiz.Q("ventana-opciones") == null, 5, "Esc cierra las Opciones");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("menu"));
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("opciones"));
                yield return Esperar(() => raiz.Q("ventana-opciones")?.Q("opcion-" + OpcionesDelJuego.EstandarteDeTropas) != null, 5, "las Opciones otra vez");
                Assert.IsFalse(raiz.Q("ventana-opciones").Q("opcion-" + OpcionesDelJuego.EstandarteDeTropas).ClassListContains("encendido"), "sigue apagada al volver");
                yield return ClicFijo(raton, raiz, raiz.Q("ventana-opciones").Q("opcion-" + OpcionesDelJuego.EstandarteDeTropas));
                yield return Esperar(() => OpcionesDelJuego.Valor(OpcionesDelJuego.EstandarteDeTropas), 5, "encendida otra vez");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("opciones-cerrar"));
                yield return Esperar(() => raiz.Q("ventana-opciones") == null, 5, "«Cerrar» cierra las Opciones");
                UnityEngine.Object.Destroy(sistema);
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

        /// <summary>Arrastra con un ratón de verdad sobre el elemento, `pixeles` hacia la derecha (con la textura de la prueba quitada, como `ClicUITK`).</summary>
        IEnumerator ArrastrarUITK(UnityEngine.InputSystem.Mouse raton, VisualElement raiz, VisualElement e, float pixeles)
        {
            var doc = go.GetComponentInChildren<UIDocument>();
            var textura = doc.panelSettings.targetTexture;
            try
            {
                raton.MakeCurrent();
                doc.panelSettings.targetTexture = null;
                yield return null;
                yield return null;
                var panel = raiz.worldBound;
                var centro = e.worldBound.center;
                var donde = new Vector2(centro.x * Screen.width / panel.width, Screen.height - centro.y * Screen.height / panel.height);
                var boton = UnityEngine.InputSystem.LowLevel.MouseButton.Left;
                // Primero se acerca (unos fotogramas: el módulo de entrada tiene que ver el ratón y el puntero entrar en el retrato).
                for (int i = 0; i < 5; i++)
                {
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde + new Vector2(5 - i, 0) });
                    yield return null;
                }
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde }.WithButton(boton, true));
                yield return null;
                yield return null;
                for (int i = 1; i <= 6; i++)
                {
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde + new Vector2(pixeles * i / 6, 0) }.WithButton(boton, true));
                    yield return null;
                    yield return null;
                }
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde + new Vector2(pixeles, 0) });
                yield return null;
                yield return null;
            }
            finally
            {
                if (doc != null) doc.panelSettings.targetTexture = textura;
            }
        }

        /// <summary>Sin barras de desplazamiento en la Base donde todo cabe (Víctor, 2026-11-14): ninguna a la vista.</summary>
        static void SinBarras(VisualElement raiz, string donde)
        {
            var barras = raiz.Q("base-personal").Query(className: "unity-scroller").ToList().Where(Visible).ToList();
            Assert.IsEmpty(barras, $"sin barras en {donde}: {string.Join(", ", barras.Select(b => b.parent?.parent?.name ?? b.name))}");
        }

        /// <summary>La ventana de la Base es opaca: un punto de su fondo, abajo a la izquierda, tiene el color del fondo (`--marco-bg`, #191410),
        /// sin restos del fotograma anterior ni el mundo detrás.</summary>
        void FondoOpaco(VisualElement raiz, string donde)
        {
            var ventana = raiz.Q("base-personal").worldBound;
            var panel = raiz.worldBound;
            var x = (int)((ventana.xMin + 30) * rt.width / panel.width);
            var y = (int)(rt.height - (ventana.yMax - 30) * rt.height / panel.height);
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            RenderTexture.active = antes;
            var color = t.GetPixel(0, 0);
            UnityEngine.Object.Destroy(t);
            var esperado = new Color32(0x19, 0x14, 0x10, 0xff);
            Assert.IsTrue(Mathf.Abs(color.r - esperado.r / 255f) < 0.04f && Mathf.Abs(color.g - esperado.g / 255f) < 0.04f && Mathf.Abs(color.b - esperado.b / 255f) < 0.04f && color.a > 0.98f,
                $"el fondo de la ventana es opaco en {donde} a {rt.width}x{rt.height}: {color}");
        }

        /// <summary>La textura de un modelo 3D tal cual (con su transparencia), para ver el encuadre.</summary>
        IEnumerator GuardarModelo(VisualElement raiz, string nombre, string archivo)
        {
            yield return null;
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            var textura = raiz.Q(nombre)?.resolvedStyle.backgroundImage.renderTexture;
            if (string.IsNullOrEmpty(carpeta) || textura == null) yield break;
            var antes = RenderTexture.active;
            RenderTexture.active = textura;
            var t = new Texture2D(textura.width, textura.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, textura.width, textura.height), 0, 0);
            RenderTexture.active = antes;
            Debug.Log($"[Prueba] {nombre}: esquina con alfa {t.GetPixel(2, 2).a:0.00} (0 = transparente)");
            System.IO.Directory.CreateDirectory(carpeta);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, archivo + ".png"), t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
        }

        /// <summary>Un clic con el ratón de la prueba en el centro del elemento (con la textura de la prueba quitada: el panel, a pantalla).</summary>
        IEnumerator ClicFijo(UnityEngine.InputSystem.Mouse raton, VisualElement raiz, VisualElement e)
        {
            Assert.IsNotNull(e, "el elemento del clic");
            var doc = go.GetComponentInChildren<UIDocument>();
            var textura = doc.panelSettings.targetTexture;
            try
            {
                doc.panelSettings.targetTexture = null;
                // Unos fotogramas: al cambiar de tamaño se vuelven a colocar las rejillas y las listas flotantes, y el elemento se mueve.
                for (int i = 0; i < 8; i++) yield return null;
                var panel = raiz.worldBound;
                var centro = e.worldBound.center;
                var donde = new Vector2(centro.x * Screen.width / panel.width, Screen.height - centro.y * Screen.height / panel.height);
                Assert.IsTrue(new Rect(0, 0, Screen.width, Screen.height).Contains(donde), $"«{e.name}» dentro de la pantalla ({donde} en {Screen.width}x{Screen.height})");
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde });
                yield return null;
                yield return null;
                yield return Clic(raton, donde);
            }
            finally
            {
                if (doc != null) doc.panelSettings.targetTexture = textura;
            }
        }
    }
}
