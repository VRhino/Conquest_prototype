// Defender tu plaza eligiendo las escuadras (tanda 3 del motor c94a960), con el ratón de verdad y la batalla de verdad (lanzador y servidor de
// batalla: `tasks/pruebas/correr_batalla.sh`). El ejemplo de Víctor: el Gobernador tiene en su campamento granjeros (7 de Liderazgo), mineros (7)
// y milicia de lanceros (14), con 14 de Liderazgo. Otra Facción asedia su plaza con un ejército (creado por la administración). Dentro, el aviso
// «Asedio a tu plaza · Defender» abre el panel de elegidas: se une con los granjeros solos. En la preparación, «Elegir escuadras» añade los
// mineros (14/14); la milicia no cabe, no se ofrece y se queda en el campamento. En la batalla, su reserva son granjeros y mineros, sin la milicia.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ConquestTactics.Net;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using V1 = BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class PantallasDeMundoTests
    {
        [Category("Batalla")]
        [UnityTest, Timeout(900_000), Explicit("Con el lanzador del motor y el servidor de batalla de verdad: la corre tasks/pruebas/correr_batalla.sh")]
        public IEnumerator TuDefensaDeTuPlaza()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            var lanzadorExe = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_LANZADOR");
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(lanzadorExe)) Assert.Ignore("sin servidor de prueba o sin lanzador");
            Google.Protobuf.WellKnownTypes.Struct P(params (string k, object v)[] ps)
            {
                var q = new Google.Protobuf.WellKnownTypes.Struct();
                foreach (var (k, v) in ps)
                    q.Fields[k] = v is string t ? Google.Protobuf.WellKnownTypes.Value.ForString(t)
                        : v is Google.Protobuf.WellKnownTypes.Struct st ? Google.Protobuf.WellKnownTypes.Value.ForStruct(st)
                        : Google.Protobuf.WellKnownTypes.Value.ForNumber(Convert.ToDouble(v));
                return q;
            }

            // La plaza del Gobernador, con las tres tropas del ejemplo en su campamento.
            string nick = null, plazaId = null, heroeId = null;
            V1.Punto donde = null;
            yield return FundarPlaza(url, x => nick = x, x => plazaId = x, x => donde = x);
            yield return HeroeDeLaCuenta(url, nick, x => heroeId = x);
            foreach (var tropa in new[] { "granjeros", "mineros", "milicia_lanceros" })
                yield return Administrar(url, "dar_escuadra", P(("heroe_id", heroeId), ("tropa_id", tropa), ("cantidad", 20)));

            // El asaltante: otra Facción y un ejército suyo a la puerta, con una escuadra. Ataca declarando la guerra y vuelve a la IA.
            TexturaDeSigilo.Asegurar();
            var azar = Sigilos.Aleatorio(new System.Random());
            string faccionAsaltante = null;
            yield return CuentaConFaccion(url, "Asaltante" + DateTime.UtcNow.Ticks % 100_000, "Micenas",
                SigiloDePrueba(azar.FormaId, azar.CampoId, azar.EmblemaId, azar.ColorPrimarioId, azar.ColorSecundarioId), _ => { }, x => faccionAsaltante = x);

            // El Gobernador entra en su plaza por la interfaz.
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
            V1.Escuadron Escuadra(string id) => s.Almacen.Buscar(V1.TipoEntidad.Escuadron, id)?.Escuadron;
            string DeTropa(string tropa) => Yo().EscuadronesIds.FirstOrDefault(id => Escuadra(id)?.TropaId == tropa);
            yield return Esperar(() => DeTropa("granjeros") != null && DeTropa("mineros") != null && DeTropa("milicia_lanceros") != null, 10, "las tres tropas en el campamento");
            string granjeros = DeTropa("granjeros"), mineros = DeTropa("mineros"), milicia = DeTropa("milicia_lanceros");
            Debug.Log($"[Prueba] defensa: Liderazgo {Yo().LiderazgoBase}; granjeros {granjeros}, mineros {mineros}, milicia {milicia}");
            yield return Quieto(1f);
            // La plaza recién fundada está protegida un día de mundo: el reloj, hasta un minuto después (antes de crear al asaltante, para que no
            // gaste víveres).
            V1.Asentamiento Plaza() => s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId)?.Asentamiento;
            if (Plaza()?.HasProtegidaHastaMs == true && Plaza().ProtegidaHastaMs > s.AhoraMundo)
                yield return AvanzarReloj(url, s, Plaza().ProtegidaHastaMs - (long)s.AhoraMundo + 60_000);
            yield return Esperar(() => Plaza() != null && (!Plaza().HasProtegidaHastaMs || Plaza().ProtegidaHastaMs <= s.AhoraMundo), 20, "la plaza, ya sin protección");
            var nickAsalto = "asalto-" + DateTime.UtcNow.Ticks % 100_000_000;
            var acceso = new Acceso(url);
            var alta = acceso.Registrar(nickAsalto, "clave-de-prueba-1");
            yield return Esperar(() => alta.IsCompleted, 20, "la cuenta del asaltante");
            V1.ResultadoOrden creado = null;
            yield return Administrar(url, "crear_ejercito", P(("cuenta", alta.Result.CuentaId), ("faccion_id", faccionAsaltante),
                ("posicion", P(("x", donde.X + 500), ("y", donde.Y))), ("velocidad", 1_000), ("viveres", 500_000)), x => creado = x);
            Assert.AreEqual(V1.Resultado.OrdenAceptada, creado?.Resultado, $"el ejército del asaltante: {creado?.Rechazo?.Codigo} {creado?.Rechazo?.Mensaje}");
            string asaltante = null;
            yield return HeroeDeLaCuenta(url, nickAsalto, x => asaltante = x);
            yield return Administrar(url, "dar_escuadra", P(("heroe_id", asaltante), ("tropa_id", "milicia_lanceros"), ("cantidad", 20)));

            var entrada = UnityEngine.InputSystem.InputSystem.settings;
            var fondo = entrada.backgroundBehavior;
            var editorAntes = entrada.editorInputBehaviorInPlayMode;
            entrada.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            entrada.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var otros = UnityEngine.InputSystem.InputSystem.devices.OfType<UnityEngine.InputSystem.Mouse>().Where(m => m.enabled).ToList();
            foreach (var m in otros) UnityEngine.InputSystem.InputSystem.DisableDevice(m);
            var raton = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            raton.MakeCurrent();
            var teclado = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            IEnumerator Tecla(UnityEngine.InputSystem.Key k)
            {
                teclado.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
                yield return null;
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
            }
            IEnumerator Clic(VisualElement e) => ClicEnVentana(raton, raiz, e);
            VisualElement Panel() => raiz.Q("ventana-defensa");
            System.Diagnostics.Process lanzador = null;
            try
            {
                lanzador = ArrancarLanzador(lanzadorExe);
                // El asedio: el asaltante (un héroe de bot, como nace con `crear_ejercito`) ataca la plaza declarando la guerra; en la batalla lo lleva la IA.
                yield return ConSesionDe(url, nickAsalto, x => new V1.Orden { Atacar = new V1.OrdenAtacar
                {
                    HeroeId = asaltante, DeclaraGuerra = true, Objetivo = new V1.OrdenAtacar.Types.Objetivo { AsentamientoId = plazaId },
                } });

                // Dentro de la plaza, el aviso; «Defender» abre el panel con las elegidas.
                yield return Esperar(() => raiz.Q("plaza-asedio")?.Q<Button>("defender") != null, 20, "«Asedio a tu plaza · Defender»");
                yield return Capturar("230_asedio_a_tu_plaza");
                // Soltar el ratón con Alt, para el HUD (como en las demás pruebas de la plaza).
                yield return Tecla(UnityEngine.InputSystem.Key.LeftAlt);
                yield return Clic(raiz.Q("plaza-asedio").Q<Button>("defender"));
                yield return Esperar(() => Panel() != null, 5, "el panel de defender");
                // Que quede solo con los granjeros: se añaden si no están y se quitan las demás.
                if (Panel().Q("elegida-" + granjeros) == null)
                {
                    yield return Clic(Panel().Q<Button>("defensa-mas"));
                    yield return Esperar(() => Panel().Q<Button>("anadir-" + granjeros) != null, 5, "los granjeros entre las que caben");
                    yield return Clic(Panel().Q<Button>("anadir-" + granjeros));
                }
                foreach (var otra in Panel().Q("elegidas").Children().Where(x => x.name != null && x.name.StartsWith("elegida-") && x.name != "elegida-" + granjeros).Select(x => x.name.Substring(8)).ToList())
                    yield return Clic(Panel().Q<Button>("quitar-" + otra));
                yield return Esperar(() => Panel().Q("elegidas").Children().Count(x => x.name != null && x.name.StartsWith("elegida-")) == 1, 5, "solo los granjeros elegidos");
                yield return Capturar("231_defender_elegidas");
                yield return EnTamanos(raiz, "231_defender_elegidas", () => DentroDelMarco(Panel().Q(className: "defensa-ventana"), "el panel de defender"));
                yield return Clic(Panel().Q<Button>("defensa-defender"));
                V1.Batalla Mia() => s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Batalla).Select(e => e.Batalla).FirstOrDefault(x => x.HasLadoPropio && x.Estado != V1.EstadoBatalla.BatallaAplicada);
                yield return Esperar(() => Mia() != null, 15, "dentro de la batalla, defendiendo");
                Assert.AreEqual(V1.LadoId.Defensor, Mia().LadoPropio, "defiende");

                // A la preparación (desde la plaza). «Elegir escuadras»: añade los mineros; la milicia no cabe y no se ofrece.
                yield return Esperar(() => SceneManager.GetActiveScene().name == PantallasDeMundo.EscenaPreparacion && raiz.Q<Button>("batalla-elegir") != null, 60, "la preparación con «Elegir escuadras»");
                yield return Esperar(() => PreparacionRed.Conectado, 150, "conectado con el servidor de batalla");
                yield return Clic(raiz.Q<Button>("batalla-elegir"));
                yield return Esperar(() => Panel()?.Q("elegida-" + granjeros) != null, 5, "el panel con los granjeros");
                yield return Clic(Panel().Q<Button>("defensa-mas"));
                yield return Esperar(() => Panel().Q<Button>("anadir-" + mineros) != null, 5, "los mineros, que caben");
                var cabe = (int)Yo().LiderazgoBase - 7 - 7 >= 14;
                Assert.AreEqual(cabe, Panel().Q<Button>("anadir-" + milicia) != null, cabe ? "con Liderazgo de sobra, la milicia cabe" : "la milicia no cabe: no se ofrece");
                yield return Clic(Panel().Q<Button>("anadir-" + mineros));
                yield return Esperar(() => Panel().Q("elegida-" + mineros) != null, 5, "los mineros elegidos");
                if (!cabe) Assert.IsNotNull(Panel().Q("se-quedan"), "la milicia, en «Se quedan en el campamento»");
                Assert.IsNull(Panel().Q<Button>("defensa-mas"), "lleno el Liderazgo, ya no hay «+»");
                Debug.Log($"[Prueba] defensa: Liderazgo {Panel().Q("liderazgo").Q<Label>().text}");
                yield return Capturar("232_elegir_en_la_preparacion");
                yield return EnTamanos(raiz, "232_elegir_en_la_preparacion", () => DentroDelMarco(Panel().Q(className: "defensa-ventana"), "el panel en la preparación"));
                yield return Clic(Panel().Q<Button>("defensa-guardar"));
                bool ConLasDos() => Mia()?.Ticket?.Defensor?.Participantes.FirstOrDefault(p => p.HeroeId == conexion.HeroeId)?.Escuadras.Select(e => e.SquadId).OrderBy(x => x)
                    .SequenceEqual(new[] { granjeros, mineros }.OrderBy(x => x)) == true;
                yield return Esperar(ConLasDos, 20, "en el ticket, granjeros y mineros");
                Debug.Log($"[Prueba] defensa: ticket revisión {Mia().Ticket.TicketRevision}, estado {Mia().Estado}");

                // «Listo» y a la batalla: su reserva, granjeros y mineros; la milicia no está.
                yield return Esperar(() => PreparacionRed.Conectado && raiz.Q<Button>("prep-listo")?.enabledSelf == true, 150, "conectado otra vez y «Listo»");
                yield return Esperar(() => raiz.Query(className: "prep-escuadra").ToList().Count == 2, 10, "las dos elegidas en el riel de la preparación");
                Pulsar(raiz.Q<Button>("prep-listo"));
                yield return Esperar(() => SceneManager.GetActiveScene().name == "BattleScene", 90, "BattleScene");
                yield return Esperar(() => EspejoClienteSystem.CamaraDelJuego, 120, "mi héroe en la batalla");
                var cliente = MundoDe("ClientWorld");
                var em = cliente.EntityManager;
                using var mandos = em.CreateEntityQuery(typeof(InterfazRed), typeof(ReservaRed), typeof(Unity.NetCode.GhostOwnerIsLocal));
                ReservaRed[] Reserva() { using var e = mandos.ToEntityArray(Unity.Collections.Allocator.Temp); return e.Length == 0 ? new ReservaRed[0] : em.GetBuffer<ReservaRed>(e[0]).AsNativeArray().ToArray(); }
                yield return Esperar(() => Reserva().Length == 2, 15, "la reserva con las dos elegidas");
                CollectionAssert.AreEquivalent(new[] { "granjeros", "mineros" }, Reserva().Select(r => r.Tropa.ToString()).ToArray(), "en la batalla, granjeros y mineros");
                Debug.Log($"[Prueba] defensa: reserva en la batalla {string.Join(", ", Reserva().Select(r => r.Tropa.ToString()))}");
                yield return CapturarPantalla("233_defendiendo_tu_plaza");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(teclado);
                foreach (var m in otros) if (m.added) UnityEngine.InputSystem.InputSystem.EnableDevice(m);
                entrada.backgroundBehavior = fondo;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
                LogAssert.ignoreFailingMessages = false;
                if (lanzador != null && !lanzador.HasExited) { try { lanzador.StandardInput.WriteLine("parar"); lanzador.WaitForExit(5000); } catch { } if (!lanzador.HasExited) lanzador.Kill(); }
            }
        }
    }
}
