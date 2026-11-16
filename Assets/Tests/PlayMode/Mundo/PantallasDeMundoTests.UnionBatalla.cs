// Unirse a una batalla en curso desde su ficha del mapa (paso 11 del bloque 4, tanda 2), con el ratón de verdad. Hace falta el motor con las
// batallas en Unity (`BRONZEAGE_BATALLAS_EN_UNITY=1`): con números se resuelven al momento y no hay a cuál unirse. La corre
// `tasks/pruebas/correr_batalla.sh` en una vuelta propia, sin lanzador: la batalla se queda convocándose (detrás de `DelMapaALaBatalla`, su
// lanzador la asigna y ya no admite a nadie).
// Dos héroes salen de su campamento con dos escuadras prestadas y marchan a los mismos bandidos; el otro los ataca y al tuyo, en la ficha de la
// batalla, le sale «Unirse» con el lado que le da el motor. Se une; el otro la cancela al final.
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
        /// <summary>Una cuenta nueva con su héroe fuera de su campamento, con dos escuadras prestadas. Devuelve su nick.</summary>
        static IEnumerator HeroeConColumna(string url, string nombre, Action<string> nickDe)
        {
            string nick = null;
            yield return OtroHeroe(url, nombre, "espada_escudo_mediano", "masculino", null, x => nick = x);
            yield return ConSesionDe(url, nick, s =>
            {
                var o = new V1.OrdenPedirPrestamo();
                o.TropaIds.Add("lenadores");
                o.TropaIds.Add("granjeros");
                return new V1.Orden { PedirPrestamo = o };
            });
            yield return ConSesionDe(url, nick, s =>
            {
                var h = s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Heroe).Select(e => e.Heroe).First(x => x.Ubicacion?.DondeCase == V1.UbicacionHeroe.DondeOneofCase.CampamentoId);
                var salir = new V1.OrdenSalirDelCampamento { CampamentoId = h.Ubicacion.CampamentoId, HeroeId = h.Id };
                salir.EscuadronIds.AddRange(h.EscuadronesIds);
                return new V1.Orden { SalirDelCampamento = salir };
            });
            nickDe(nick);
        }

        [UnityTest, Timeout(900_000), Explicit("Con las batallas en Unity: la corre tasks/pruebas/correr_batalla.sh")]
        public IEnumerator TuUnionALaBatalla()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url) || Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_LANZADOR") == null) Assert.Ignore("sin el servidor de prueba con batallas en Unity");
            string nick = null, nickOtro = null;
            yield return HeroeConColumna(url, "Uni" + DateTime.UtcNow.Ticks % 1_000_000, x => nick = x);
            yield return HeroeConColumna(url, "Ata" + DateTime.UtcNow.Ticks % 1_000_000, x => nickOtro = x);

            foreach (var k in Preferencias) PlayerPrefs.DeleteKey(k);
            go = new GameObject("Mundo");
            go.SetActive(false);
            var conexion = go.AddComponent<ConexionDeMundo>();
            typeof(ConexionDeMundo).GetField("servidor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(conexion, url);
            go.AddComponent<PantallasDeMundo>();
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
            selector.confirmButton.onClick.Invoke();
            yield return Esperar(() => raiz.Q("mapa") != null && ElMapaSiHay() != null, 60, "el mapa");
            var mapa = ElMapa();
            yield return Esperar(() => mapa.Columna != null && mapa.Centro != null, 20, "la columna fuera");

            var entrada = UnityEngine.InputSystem.InputSystem.settings;
            var fondo = entrada.backgroundBehavior;
            var editorAntes = entrada.editorInputBehaviorInPlayMode;
            entrada.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            entrada.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var otros = UnityEngine.InputSystem.InputSystem.devices.OfType<UnityEngine.InputSystem.Mouse>().Where(m => m.enabled).ToList();
            foreach (var m in otros) UnityEngine.InputSystem.InputSystem.DisableDevice(m);
            var raton = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            raton.MakeCurrent();
            var sistema = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null
                ? new GameObject("EventSystem de la prueba", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)) : null;
            IEnumerator Clic(VisualElement e) => ClicEnVentana(raton, raiz, e);
            Button EnFicha(string texto) => raiz.Q("ficha-acciones")?.Query<Button>().ToList().FirstOrDefault(b => TextoDe(b) == texto);
            string batalla = null;
            try
            {
                // Unos bandidos propios de la prueba, fuera de la protección del campamento de mercenarios de al lado (los del mundo pueden seguir en la
                // batalla de `DelMapaALaBatalla`, que corre antes: `batalla.bloqueado`). Los dos marchan allí (tú, con «Marchar aquí»).
                var radioProteccion = conexion.Datos.Numero("documentos.Campamentos_Mercenarios.MERCENARIOS.radioProteccion");
                var (cx, cy) = mapa.Centro.Value;
                var campamento = mapa.Cosas.Where(x => x.Clase == ClaseDeCosa.Mercenarios).OrderBy(x => CosasDelMapa.Distancia(x.X, x.Y, cx, cy)).First();
                string bid = null;
                foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1) })
                {
                    var n = Math.Sqrt(dx * dx + dy * dy);
                    var donde = new Google.Protobuf.WellKnownTypes.Struct();
                    donde.Fields["x"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(Math.Round((campamento.X + dx / n * radioProteccion * 1.6) * 1000));
                    donde.Fields["y"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(Math.Round((campamento.Y + dy / n * radioProteccion * 1.6) * 1000));
                    var p = new Google.Protobuf.WellKnownTypes.Struct();
                    p.Fields["posicion"] = Google.Protobuf.WellKnownTypes.Value.ForStruct(donde);
                    p.Fields["nivel"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(1);
                    V1.ResultadoOrden r = null;
                    yield return Administrar(url, "colocar_bandidos", p, x => r = x);
                    if (r?.Resultado == V1.Resultado.OrdenAceptada) { bid = r.Creadas.First().Id; break; }
                }
                Assert.IsNotNull(bid, "unos bandidos de la prueba");
                yield return Esperar(() => mapa.Cosas.Any(x => x.Id == bid), 20, "los bandidos de la prueba en el mapa");
                var bandidos = mapa.Cosas.First(x => x.Id == bid);
                yield return ConSesionDe(url, nickOtro, s =>
                {
                    var h = s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Heroe).Select(e => e.Heroe).First(x => x.Ubicacion?.DondeCase == V1.UbicacionHeroe.DondeOneofCase.EjercitoId);
                    return new V1.Orden { MarcharA = new V1.OrdenMarcharA { HeroeId = h.Id, Objetivo = new V1.OrdenMarcharA.Types.Objetivo { Punto = new V1.Punto { X = (long)(bandidos.X * 1000), Y = (long)(bandidos.Y * 1000) } } } };
                });
                yield return Reloj(url, 30_000);
                mapa.Seleccionar(bandidos);
                yield return Esperar(() => EnFicha("Marchar aquí") != null, 5, "«Marchar aquí»");
                yield return Clic(EnFicha("Marchar aquí"));
                yield return Esperar(() => EnFicha("Atacar") != null, 120, "«Atacar» al llegar");
                yield return Reloj(url, 1_000);

                // El otro ataca: la batalla, en el mapa, con «Unirse» en su ficha.
                yield return ConSesionDe(url, nickOtro, s =>
                {
                    var h = s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Heroe).Select(e => e.Heroe).First(x => x.Ubicacion?.DondeCase == V1.UbicacionHeroe.DondeOneofCase.EjercitoId);
                    return new V1.Orden { Atacar = new V1.OrdenAtacar { HeroeId = h.Id, Objetivo = new V1.OrdenAtacar.Types.Objetivo { CampamentoId = bandidos.Id } } };
                });
                // La de nuestros bandidos: en su punto (a quien no combate le llega resumida, sin lo que bloquea).
                bool Nuestra(CosaDelMapa x) => x.Clase == ClaseDeCosa.Batalla && CosasDelMapa.Distancia(x.X, x.Y, bandidos.X, bandidos.Y) < 3;
                yield return Esperar(() => mapa.Cosas.Any(Nuestra), 20, "la batalla en el mapa");
                var cosa = mapa.Cosas.First(Nuestra);
                batalla = cosa.Id;
                mapa.Seleccionar(cosa);
                yield return Esperar(() => EnFicha("Unirse") != null, 20, "«Unirse» en la ficha de la batalla");
                yield return Capturar("175_ficha_batalla");
                yield return EnTamanos(raiz, "175_ficha_batalla", () => DentroDelMarco(raiz.Q("ficha"), "la ficha de la batalla"));
                yield return Clic(EnFicha("Unirse"));
                yield return Esperar(() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Batalla, batalla)?.Batalla?.HasLadoPropio == true, 15, "dentro de la batalla");
                Assert.AreEqual(V1.LadoId.Atacante, conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Batalla, batalla).Batalla.LadoPropio, "con quien ataca");
            }
            finally
            {
                if (sistema != null) UnityEngine.Object.Destroy(sistema);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                foreach (var m in otros) if (m.added) UnityEngine.InputSystem.InputSystem.EnableDevice(m);
                entrada.backgroundBehavior = fondo;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
            }
            // El otro, que la inició, la cancela antes de que empiece: nadie pierde nada y no queda para la prueba siguiente.
            yield return ConSesionDe(url, nickOtro, s => new V1.Orden { CancelarBatalla = new V1.OrdenCancelarBatalla { BattleId = batalla } });
        }
    }
}
