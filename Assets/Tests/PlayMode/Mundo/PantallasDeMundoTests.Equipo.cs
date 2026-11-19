// El equipo y los loadouts de la Base (paso 6 del bloque 4, tanda 2) contra el servidor de prueba, con ratón y teclado de verdad: un héroe nuevo
// en su campamento; en la pestaña Héroe, pulsar la casilla de las botas y «Quitar» (el motor las pasa a la mochila y el héroe en 3D se queda
// descalzo); la ficha del objeto al pasar el ratón por ellas en la mochila, con su comparación; ponérselas otra vez desde la casilla. En Loadouts,
// añadir una escuadra al loadout y guardarlo, crear otro, activarlo y borrarlo. Capturas a los cuatro tamaños.
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
        [Category("Base")]
        [UnityTest, Timeout(600_000)]
        public IEnumerator ElEquipoYLosLoadouts()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN");
            string nick = null;
            var aspecto = new V1.OrdenCrearHeroe.Types.Avatar { CabezaId = "head_03", PeloId = "hair_02", BarbaId = "", CejasId = "eyebrow_01" };
            yield return OtroHeroe(url, "Equipo" + DateTime.UtcNow.Ticks % 1_000_000, "espada_escudo_mediano", "masculino", aspecto, x => nick = x);
            foreach (var k in Preferencias) PlayerPrefs.DeleteKey(k);
            go = new GameObject("Mundo");
            go.SetActive(false);
            var conexion = go.AddComponent<ConexionDeMundo>();
            typeof(ConexionDeMundo).GetField("servidor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(conexion, url);
            var pantallas = go.AddComponent<PantallasDeMundo>();
#if UNITY_EDITOR
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
            LogAssert.ignoreFailingMessages = true;
            selector.confirmButton.onClick.Invoke();
            yield return Esperar(() => raiz.Q("interior") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior");
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            yield return Quieto(1f);
            V1.Heroe Yo() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            // Una escuadra propia para el loadout (prestada no: esas no van en loadouts de otros campamentos).
            var campId = Yo().Ubicacion.CampamentoId;
            var camp = conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.CampamentoMercenarios, campId).CampamentoMercenarios;
            yield return Esperar(() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.CampamentoMercenarios, campId).CampamentoMercenarios.Reclutables.Count > 0, 10, "lo que ofrece el campamento");
            var escuadra = new Google.Protobuf.WellKnownTypes.Struct();
            escuadra.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(conexion.HeroeId);
            escuadra.Fields["tropa_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.CampamentoMercenarios, campId).CampamentoMercenarios.Reclutables.First().TropaId);
            escuadra.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(20);
            yield return Administrar(url, "dar_escuadra", escuadra);
            yield return Esperar(() => Yo().EscuadronesIds.Count >= 1, 10, "la escuadra");
            var mia = Yo().EscuadronesIds.First();

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
            try
            {
                teclado.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.B));
                yield return null;
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return Esperar(() => raiz.Q("heroe-equipo") != null, 5, "la Base en Héroe");
                yield return Quieto(0.5f);

                // Las botas puestas: su pieza de Conquest (por hueco y peso) visible en el héroe en 3D.
                var partes = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
                var botasMedia = Data.Avatar.AvatarVisualUtils.FindAvatarPartById(partes, "BootsMediumArmorDefault").attachments[0].prefabPathMale;
                Transform Modelo() => GameObject.Find("Modelo de la Base hero_preview").transform.GetChild(0);
                bool BotasVisibles() => Modelo().GetComponentsInChildren<Transform>(true).Any(t => t.name == botasMedia && t.gameObject.activeInHierarchy);
                Assert.IsTrue(BotasVisibles(), "las botas de inicio (media) en el héroe en 3D");
                yield return Capturar("51_equipo_heroe");
                yield return EnTamanos(raiz, "51_equipo_heroe", () => SinBarras(raiz, "Héroe"));

                // Quitarse las botas: su casilla, la lista y «Quitar».
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("casilla-botas"));
                yield return Esperar(() => raiz.Q<Button>("quitar-botas") != null, 5, "la lista de las botas con «Quitar»");
                yield return Capturar("52_elegir_botas");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("quitar-botas"));
                for (var fin = Time.realtimeSinceStartup + 10; Time.realtimeSinceStartup < fin && Yo().Equipamiento.Botas != null;) yield return null;
                Assert.IsNull(Yo().Equipamiento.Botas, $"las botas quitadas; lista {(raiz.Q("elegir-equipo") != null ? "abierta" : "cerrada")}, error «{raiz.Q("elegir-equipo")?.Q<Label>(className: "error")?.text}»");
                for (var fin = Time.realtimeSinceStartup + 10; Time.realtimeSinceStartup < fin && Yo().Inventario.Count == 0;) yield return null;
                Debug.Log($"[Prueba] tras quitar: mochila {Yo().Inventario.Count}, almacén {Yo().AlmacenObjetos.Count}, equipo {Yo().Equipamiento}");
                Assert.Greater(Yo().Inventario.Count + Yo().AlmacenObjetos.Count, 0, "las botas, en la mochila o el almacén");
                yield return Esperar(() => !BotasVisibles(), 5, "el héroe en 3D, descalzo");
                yield return Capturar("53_sin_botas");

                // La ficha de las botas al pasar el ratón en la mochila (Bienes).
                yield return Esperar(() => raiz.Q("elegir-equipo") == null, 5, "la lista cerrada tras quitar");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("pestana-bienes"));
                var casilla = Yo().Inventario.First().CasillaInventario;
                Debug.Log($"[Prueba] en Bienes: rejilla {(raiz.Q("rejilla-mochila") != null)}, botones {string.Join(",", raiz.Q("rejilla-mochila")?.Query<Button>().ToList().Select(x => x.name) ?? new string[0])}, casilla {casilla}");
                yield return Esperar(() => raiz.Q<Button>("mochila-" + casilla) != null, 5, "las botas en la mochila");
                yield return MoverRaton(raton, raiz, raiz.Q<Button>("mochila-" + casilla), () => raiz.Q("ficha-objeto") != null);
                yield return Esperar(() => raiz.Q("ficha-objeto") != null, 5, "la ficha del objeto");
                var ficha = raiz.Q("ficha-objeto");
                var textos = string.Join(" | ", ficha.Query<Label>().ToList().Select(l => l.text));
                Debug.Log("[Prueba] ficha: " + textos);
                StringAssert.Contains("Botas de inicio", textos);
                StringAssert.Contains("Defensa", textos);
                yield return Capturar("54_ficha_del_objeto");
                yield return EnTamanos(raiz, "54_ficha_del_objeto");

                // Ponérselas otra vez desde su casilla del equipo.
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("pestana-heroe"));
                yield return Esperar(() => raiz.Q<Button>("casilla-botas") != null, 5, "Héroe");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("casilla-botas"));
                yield return Esperar(() => raiz.Q<Button>("equipar-" + casilla) != null, 5, "las botas en la lista de su casilla");
                yield return MoverRaton(raton, raiz, raiz.Q<Button>("equipar-" + casilla), () => raiz.Q("ficha-objeto") != null);
                yield return Esperar(() => raiz.Q("ficha-objeto") != null, 5, "la ficha en la lista");
                yield return Capturar("55_ficha_en_la_lista");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("equipar-" + casilla));
                yield return Esperar(() => Yo().Equipamiento.Botas != null, 10, "las botas puestas");
                yield return Esperar(() => BotasVisibles(), 5, "el héroe en 3D, calzado");

                // Loadouts: añadir la escuadra al loadout y guardar; uno nuevo, activarlo y borrarlo.
                yield return Esperar(() => raiz.Q("elegir-equipo") == null, 5, "la lista cerrada tras equipar");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("pestana-loadouts"));
                yield return Esperar(() => raiz.Q("loadouts-lista") != null, 5, "Loadouts");
                var primero = Yo().Loadouts.FirstOrDefault();
                Debug.Log($"[Prueba] loadouts: {string.Join(", ", Yo().Loadouts.Select(l => $"{l.DisplayName} ({l.SquadIds.Count}{(l.Activo ? ", activo" : "")})"))}");
                yield return Capturar("56_loadouts");
                yield return EnTamanos(raiz, "56_loadouts", () => SinBarras(raiz, "Loadouts"));
                if (primero != null && !primero.SquadIds.Contains(mia))
                {
                    yield return ClicFijo(raton, raiz, raiz.Q<Button>("anadir-a-loadout"));
                    yield return Esperar(() => raiz.Q<Button>("anadir-" + mia) != null, 5, "la escuadra para añadir");
                    yield return ClicFijo(raton, raiz, raiz.Q<Button>("anadir-" + mia));
                    yield return Esperar(() => raiz.Q("en-loadout-" + mia) != null, 5, "la escuadra en el riel");
                    yield return ClicFijo(raton, raiz, raiz.Q<Button>("guardar-loadout"));
                    yield return Esperar(() => Yo().Loadouts.First(l => l.Id == primero.Id).SquadIds.Contains(mia), 10, "el loadout guardado en el motor");
                }
                var antes = Yo().Loadouts.Count;
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("loadout-nuevo"));
                yield return Esperar(() => raiz.Q<Button>("guardar-loadout") != null, 5, "el nuevo");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("guardar-loadout"));
                yield return Esperar(() => Yo().Loadouts.Count == antes + 1, 10, "el loadout nuevo en el motor");
                var nuevo = Yo().Loadouts.First(l => primero == null || l.Id != primero.Id);
                yield return Esperar(() => raiz.Q<Button>("loadout-" + nuevo.Id) != null, 5, "el nuevo en la lista");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("loadout-" + nuevo.Id));
                yield return Esperar(() => raiz.Q<Button>("activar-loadout") != null, 5, "«Activar»");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("activar-loadout"));
                yield return Esperar(() => Yo().Loadouts.First(l => l.Id == nuevo.Id).Activo, 10, "el nuevo, activo");
                yield return Capturar("57_loadout_activo");
                yield return Esperar(() => raiz.Q<Button>("borrar-loadout") != null, 5, "«Borrar»");
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("borrar-loadout"));
                yield return Esperar(() => raiz.Q(className: "dialogo") != null, 5, "la confirmación");
                yield return ClicFijo(raton, raiz, raiz.Q(className: "dialogo").Q<Button>(className: "boton-primario"));
                yield return Esperar(() => Yo().Loadouts.All(l => l.Id != nuevo.Id), 10, "el loadout borrado");
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

        /// <summary>Lleva el ratón de la prueba al centro del elemento, sin pulsar (para las fichas al pasar el ratón).</summary>
        IEnumerator MoverRaton(UnityEngine.InputSystem.Mouse raton, VisualElement raiz, VisualElement e, Func<bool> hasta = null)
        {
            Assert.IsNotNull(e, "el elemento");
            var doc = go.GetComponentInChildren<UIDocument>();
            var textura = doc.panelSettings.targetTexture;
            doc.panelSettings.targetTexture = null;
            // Unos fotogramas: al cambiar de tamaño, las rejillas vuelven a medir sus casillas y el elemento se mueve.
            for (int i = 0; i < 8; i++) yield return null;
            var panel = raiz.worldBound;
            var centro = e.worldBound.center;
            var donde = new Vector2(centro.x * Screen.width / panel.width, Screen.height - centro.y * Screen.height / panel.height);
            for (int i = 3; i >= 0; i--)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(raton, new UnityEngine.InputSystem.LowLevel.MouseState { position = donde + new Vector2(i * 3, 0) });
                yield return null;
                yield return null;
            }
            for (var fin = Time.realtimeSinceStartup + 3; hasta != null && Time.realtimeSinceStartup < fin && !hasta();) yield return null;
            Assert.IsTrue(hasta == null || hasta(), $"al pasar el ratón por «{e.name}»");
            doc.panelSettings.targetTexture = textura;
            // Con la textura de la prueba la ventana vuelve a su tamaño y el elemento se mueve de debajo del puntero (la ficha se va): para la
            // captura, la misma entrada del puntero, mandada al elemento (la del ratón de verdad ya se ha comprobado arriba).
            yield return null;
            yield return null;
            using (var entra = PointerEnterEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = e.worldBound.center }))
            {
                entra.target = e;
                e.SendEvent(entra);
            }
            yield return null;
            yield return null;
            yield return null;
        }
    }
}
