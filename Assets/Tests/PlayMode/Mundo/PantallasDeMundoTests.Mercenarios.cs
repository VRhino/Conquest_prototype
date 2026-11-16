// Los mercenarios por contrato (paso 12 del bloque 4, tanda 2), con el ratón de verdad contra el motor. Un bot sin Facción se ofrece en el tablón de
// su campamento; el Rey de una Facción sin plaza, residente del mismo campamento y con oro en el almacén, tiene unos bandidos a la vista (el objetivo de
// «Atacar»), abre la pieza Militar → Mercenarios, elige 1 día y lo contrata. En Facción › Mercenarios lo marca, le da la orden «Ir a», prorroga el
// contrato y lo despide; el contrato acabado sale para valorarlo y lo valora «Buena».
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using V1 = BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class PantallasDeMundoTests
    {
        /// <summary>El id del héroe de la cuenta `nick`.</summary>
        static IEnumerator HeroeDeLaCuenta(string url, string nick, Action<string> de)
        {
            var acceso = new Acceso(url);
            var login = acceso.Entrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => login.IsCompleted, 20, "el login");
            var s = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
            var abrir = s.Abrir();
            for (var f = Time.realtimeSinceStartup + 20; !abrir.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
            de(abrir.Result.HeroeId);
            s.Cerrar();
        }

        [UnityTest, Timeout(900_000)]
        public IEnumerator TuTablonDeMercenarios()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta el bot");
            // El bot: una cuenta cualquiera pasada a bot, que se ofrece en el tablón de su campamento.
            string nickBot = null, botId = null;
            yield return OtroHeroe(url, "Mercenario " + DateTime.UtcNow.Ticks % 100_000, nickDe: x => nickBot = x);
            yield return HeroeDeLaCuenta(url, nickBot, x => botId = x);
            var bot = new Google.Protobuf.WellKnownTypes.Struct();
            bot.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(botId);
            bot.Fields["controlador"] = Google.Protobuf.WellKnownTypes.Value.ForString("bot");
            yield return Administrar(url, "fijar_controlador", bot);
            yield return ConSesionDe(url, nickBot, _ => new V1.Orden { OfrecerseComoMercenario = new V1.OrdenOfrecerseComoMercenario() });

            // El Rey de una Facción sin plaza, en el mismo campamento.
            TexturaDeSigilo.Asegurar();
            var azar = Sigilos.Aleatorio(new System.Random());
            string nick = null;
            yield return CuentaConFaccion(url, "Contratista" + DateTime.UtcNow.Ticks % 100_000, "Tirinto",
                SigiloDePrueba(azar.FormaId, azar.CampoId, azar.EmblemaId, azar.ColorPrimarioId, azar.ColorSecundarioId), x => nick = x, null);

            VisualElement raiz = null;
            ConexionDeMundo conexion = null;
            yield return EntrarAlCampamento(url, nick, (r, c) => { raiz = r; conexion = c; });
            LogAssert.ignoreFailingMessages = true; // el error de relleno del escuadrón vacío al nacer el héroe en FeudoScene
            UnityEngine.Object.FindAnyObjectByType<HeroSelectionSceneController>().confirmButton.onClick.Invoke();
            yield return Esperar(() => raiz.Q("interior") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior");
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            yield return DarAlmacen(url, conexion.HeroeId, "oro", 900_000);
            var s0 = conexion.Sesion;
            V1.Heroe Yo() => s0.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            V1.Faccion Faccion() => s0.Almacen.Buscar(V1.TipoEntidad.Faccion, Yo().FaccionId)?.Faccion;
            V1.Contrato Contrato() => Faccion()?.Contratos.FirstOrDefault(k => k.MercenarioId == botId);
            yield return Esperar(() => Milesimas(Yo().AlmacenPersonal, "oro") >= 300, 10, "el oro en el almacén");
            var campId = Yo().Ubicacion.CampamentoId;
            yield return Esperar(() => s0.Almacen.Buscar(V1.TipoEntidad.CampamentoMercenarios, campId)?.CampamentoMercenarios?.Tablon.Any(o => o.HeroeId == botId) == true, 10, "el bot en el tablón");
            // Unos bandidos junto al campamento, fuera de su protección: lo que «Atacar» ataca (el Rey lo firma).
            var camp = s0.Almacen.Buscar(V1.TipoEntidad.CampamentoMercenarios, campId).CampamentoMercenarios;
            var radio = conexion.Datos.Numero("documentos.Campamentos_Mercenarios.MERCENARIOS.radioProteccion");
            string bid = null;
            long bx = 0, by = 0;
            foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) })
            {
                var n = Math.Sqrt(dx * dx + dy * dy);
                var donde = new Google.Protobuf.WellKnownTypes.Struct();
                donde.Fields["x"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(Math.Round(camp.Posicion.X + dx / n * radio * 1.6 * 1000));
                donde.Fields["y"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(Math.Round(camp.Posicion.Y + dy / n * radio * 1.6 * 1000));
                var p = new Google.Protobuf.WellKnownTypes.Struct();
                p.Fields["posicion"] = Google.Protobuf.WellKnownTypes.Value.ForStruct(donde);
                p.Fields["nivel"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(1);
                V1.ResultadoOrden r = null;
                yield return Administrar(url, "colocar_bandidos", p, x => r = x);
                if (r?.Resultado == V1.Resultado.OrdenAceptada) { bid = r.Creadas.First().Id; bx = (long)donde.Fields["x"].NumberValue; by = (long)donde.Fields["y"].NumberValue; break; }
            }
            Assert.IsNotNull(bid, "unos bandidos de la prueba");
            // Sin plaza ni columna, la Facción no tiene ojos: el Rey compra una Mirada sobre ellos en la taberna del campamento (como haría un jugador).
            var orden = s0.Ordenar(new V1.Orden { ComprarMirada = new V1.OrdenComprarMirada
            {
                Origen = new V1.OrdenComprarMirada.Types.Origen { Tipo = "campamento", Id = campId },
                Centro = new V1.Punto { X = bx, Y = by },
            } });
            yield return Esperar(() => orden.IsCompleted, 10, "la Mirada");
            Debug.Log($"[Prueba] mercenarios: Mirada {orden.Result.Resultado} {orden.Result.Rechazo?.Codigo}");
            yield return Esperar(() => s0.Almacen.Buscar(V1.TipoEntidad.CampamentoBandidos, bid) != null, 20, "los bandidos a la vista desde el campamento");
            // Solo la escena, para las capturas del prototipo (`tasks/pruebas/cdp/mercenarios.mjs`).
            if (Environment.GetEnvironmentVariable("MUNDO_PROTO") == "mercenarios")
            {
                Debug.Log($"[Prueba] escena del prototipo: yo={nick} bot={botId}");
                LogAssert.ignoreFailingMessages = false;
                yield break;
            }
            yield return Quieto(1f);
            var interior = SelectorDePersonaje.Interior;
            var heroe = UnityEngine.Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>(FindObjectsSortMode.None).First(x => x.IsLocalHero);

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
            IEnumerator Tecla(UnityEngine.InputSystem.Key k)
            {
                teclado.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
                yield return null;
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
            }
            VisualElement Panel() => raiz.Q("panel-tropa");
            VisualElement Faccionv() => raiz.Q("faccion-ventana");
            try
            {
                // El rastreador de misiones (paso 12, tanda 3): también en el campamento, arriba a la derecha.
                yield return Esperar(() => raiz.Q("rastreador")?.ClassListContains("rastreador--flota") == true, 10, "el rastreador en el campamento");
                yield return Capturar("216_rastreador_campamento");
                // La pieza Militar: andando hasta ella y F; la pestaña Mercenarios, junto a Tropa.
                yield return AndarHasta(teclado, heroe.transform, "militar", () => interior.ZonaCercana == "militar");
                yield return Tecla(UnityEngine.InputSystem.Key.F);
                yield return Esperar(() => Panel()?.Q<Button>("militar-mercenarios") != null, 5, "la pestaña Mercenarios de la pieza Militar");
                yield return Clic(Panel().Q<Button>("militar-mercenarios"));
                yield return Esperar(() => Panel()?.Q("merc-oferta-" + botId) != null, 5, "la oferta del bot");
                var oferta = Panel().Q("merc-oferta-" + botId);
                Assert.IsNotNull(oferta.Q("merc-trabajo-" + botId + "-atacar"), "«Atacar»: el Rey firma contra los bandidos");
                Assert.IsNull(oferta.Q("merc-trabajo-" + botId + "-permanente"), "sin plaza, no hay permanente");
                yield return Clic(oferta.Q<Button>("merc-tarifa-" + botId + "-1"));
                yield return Esperar(() => Panel().Q("merc-oferta-" + botId).Q("merc-tarifa-" + botId + "-1").ClassListContains("activo"), 5, "1 día elegido");
                yield return Capturar("200_tablon_mercenarios");
                yield return EnTamanos(raiz, "200_tablon_mercenarios", () => DentroDelMarco(Panel(), "el tablón"));
                yield return Clic(Panel().Q<Button>("merc-contratar-" + botId));
                yield return Esperar(() => Contrato() != null, 10, "el contrato en la Facción");
                Assert.AreEqual("atacar", Contrato().Trabajo);
                Assert.AreEqual(1u, Contrato().PlazoDias);
                yield return Clic(Panel().Q<Button>("cerrar"));
                yield return Esperar(() => Panel() == null, 5, "la pieza Militar cerrada");

                // Facción › Mercenarios: el contrato, marcarlo y «Ir a» el punto donde está el jugador.
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("faccion"));
                yield return Esperar(() => Faccionv()?.Q<Button>("pestana-faccion-mercenarios") != null, 5, "la pestaña Mercenarios de la Facción");
                yield return Clic(Faccionv().Q<Button>("pestana-faccion-mercenarios"));
                yield return Esperar(() => Faccionv()?.Q("merc-contrato-" + botId) != null, 5, "el contrato en Facción › Mercenarios");
                yield return Clic(Faccionv().Q<Toggle>("merc-marcar-" + botId));
                yield return Esperar(() => Faccionv()?.Q<Button>("merc-orden-ir_a")?.enabledSelf == true, 5, "«Ir a» encendida con el marcado");
                yield return Clic(Faccionv().Q<Button>("merc-orden-ir_a"));
                yield return Esperar(() => Faccionv()?.Q<Button>("merc-dar") != null && Faccionv().Q<IntegerField>("merc-x") != null, 5, "el punto y «Dar la orden»");
                yield return Capturar("201_faccion_mercenarios");
                yield return EnTamanos(raiz, "201_faccion_mercenarios", () => DentroDelMarco(Faccionv(), "Facción › Mercenarios"));
                yield return Clic(Faccionv().Q<Button>("merc-dar"));
                yield return Esperar(() => Contrato()?.Orden?.Tipo == "ir_a", 10, "la orden «Ir a» en el motor");

                // Prorrogar: +1 día, en persona en su campamento base.
                var vence = Contrato().VenceEnMs;
                yield return Esperar(() => Faccionv()?.Q<Button>("merc-prorrogar-" + botId) != null, 5, "«Prorrogar»");
                yield return Clic(Faccionv().Q<Button>("merc-prorrogar-" + botId));
                yield return Esperar(() => Contrato()?.VenceEnMs > vence, 10, "el contrato prorrogado");

                // Despedir (con su confirmación): se acaba, y sale para valorarlo.
                yield return Esperar(() => Faccionv()?.Q<Button>("merc-despedir-" + botId) != null, 5, "«Despedir»");
                yield return Clic(Faccionv().Q<Button>("merc-despedir-" + botId));
                yield return Esperar(() => raiz.Q(className: "boton-peligro") != null, 5, "la confirmación");
                yield return Clic(raiz.Q<Button>(className: "boton-peligro"));
                yield return Esperar(() => Contrato() == null, 10, "el contrato acabado");
                yield return Esperar(() => Faccionv()?.Q<Button>("merc-valorar-buena-" + botId) != null, 10, "el contrato por valorar");
                yield return Capturar("202_valorar");
                yield return EnTamanos(raiz, "202_valorar", () => DentroDelMarco(Faccionv(), "valorar"));
                yield return Clic(Faccionv().Q<Button>("merc-valorar-buena-" + botId));
                yield return Esperar(() => Faccionv()?.Q<Button>("merc-valorar-buena-" + botId) == null, 10, "valorado");
                // Guardado en el equipo con la clave del jugador (servidor, cuenta y héroe), no solo con el id del héroe: otro servidor u otra
                // partida pueden dar el mismo `heroe-N` a otro.
                var clave = conexion.ClaveDeJugador(conexion.HeroeId);
                StringAssert.Contains(conexion.Nick, clave, "la cuenta en la clave");
                Assert.IsTrue(ContratosPorValorar.De(clave).Any(t => t.Id == botId && t.Valorado), "guardado como valorado en el equipo");
                // Los avisos de `mercenarios.*` (llegan con su delta: se esperan).
                for (var f = Time.realtimeSinceStartup + 10; !conexion.Avisos.Historial.Any(a => a.Titulo == "Mercenarios") && Time.realtimeSinceStartup < f;) yield return null;
                Debug.Log("[Prueba] mercenarios: avisos " + string.Join(" / ", conexion.Avisos.Historial.Select(a => $"{a.Titulo}: {a.Texto}")));
                Assert.IsTrue(conexion.Avisos.Historial.Any(a => a.Titulo == "Mercenarios"), "los avisos de los mercenarios");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(teclado);
                foreach (var m in otros) if (m.added) UnityEngine.InputSystem.InputSystem.EnableDevice(m);
                entrada.backgroundBehavior = fondo;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
