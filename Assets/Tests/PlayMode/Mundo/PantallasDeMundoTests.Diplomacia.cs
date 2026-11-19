// La Diplomacia de tu Facción (paso 8 del bloque 4, tanda 4), con el ratón y el teclado de verdad contra el motor y dos Reyes. El Rey de Micenas
// abre la pestaña Diplomacia: elige la otra Facción en la lista y le propone una alianza («Nueva propuesta»), la retira, le declara la guerra (con
// su confirmación) y ofrece la paz; la otra (por su sesión) la ofrece también y la guerra acaba. Después la otra le propone un vasallaje con
// tributo y el Rey lo acepta desde su ficha (con su confirmación): sale «Eres vasalla de …» con «Rebelarse».
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
        /// <summary>Una orden con la sesión de otra cuenta (su héroe ya creado).</summary>
        static IEnumerator OrdenComo(string url, string nick, Func<V1.Orden> orden, Action<V1.ResultadoOrden> resultado = null)
        {
            var acceso = new Acceso(url);
            var login = acceso.Entrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => login.IsCompleted, 20, "el login de " + nick);
            var s = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
            var abrir = s.Abrir();
            for (var f = Time.realtimeSinceStartup + 20; !abrir.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
            var r = s.Ordenar(orden());
            for (var f = Time.realtimeSinceStartup + 20; !r.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
            Debug.Log($"[Prueba] diplomacia, {nick}: {orden().CuerpoCase} {r.Result.Resultado} {r.Result.Rechazo?.Codigo}");
            resultado?.Invoke(r.Result);
            s.Cerrar();
        }

        [Category("Faccion")]
        [UnityTest, Timeout(900_000)]
        public IEnumerator TuDiplomaciaDelRey()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            TexturaDeSigilo.Asegurar();
            var azar = new System.Random();
            var s1 = Sigilos.Aleatorio(azar);
            var s2 = Sigilos.Aleatorio(azar);
            string nick = null, nickOtra = null, mia = null, otra = null;
            yield return CuentaConFaccion(url, "Rey" + DateTime.UtcNow.Ticks % 100_000, "Micenas",
                SigiloDePrueba(s1.FormaId, s1.CampoId, s1.EmblemaId, s1.ColorPrimarioId, s1.ColorSecundarioId), x => nick = x, x => mia = x);
            yield return CuentaConFaccion(url, "Vecino" + DateTime.UtcNow.Ticks % 100_000, "Esparta",
                SigiloDePrueba(s2.FormaId, s2.CampoId, s2.EmblemaId, s2.ColorPrimarioId, s2.ColorSecundarioId), x => nickOtra = x, x => otra = x);
            VisualElement raiz = null;
            ConexionDeMundo conexion = null;
            yield return EntrarAlCampamento(url, nick, (r, c) => { raiz = r; conexion = c; });
            LogAssert.ignoreFailingMessages = true; // el error de relleno del escuadrón vacío al nacer el héroe en FeudoScene
            UnityEngine.Object.FindAnyObjectByType<HeroSelectionSceneController>().confirmButton.onClick.Invoke();
            yield return Esperar(() => raiz.Q("interior") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior");
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            yield return Quieto(1f);
            var al = conexion.Sesion.Almacen;
            V1.RelacionPolitica Relacion() => al.Todas(V1.Entidad.CuerpoOneofCase.Relacion).Select(e => e.Relacion)
                .FirstOrDefault(r => r.Estado == V1.EstadoRelacion.RelacionActiva && (r.FaccionAId == mia && r.FaccionBId == otra || r.FaccionAId == otra && r.FaccionBId == mia));
            V1.Propuesta Propuesta() => al.Todas(V1.Entidad.CuerpoOneofCase.Propuesta).Select(e => e.Propuesta)
                .FirstOrDefault(p => p.ClaseCase == V1.Propuesta.ClaseOneofCase.Relacion && (p.Relacion.FaccionAId == mia || p.Relacion.FaccionBId == mia));

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
            IEnumerator Tecla(UnityEngine.InputSystem.Key k)
            {
                teclado.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
                yield return null;
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
            }
            VisualElement Ventana() => raiz.Q("faccion-ventana");
            IEnumerator Clic(VisualElement e) => ClicEnVentana(raton, raiz, e);
            // Acepta el diálogo de confirmación con el ratón.
            IEnumerator Confirmar(string que)
            {
                yield return Esperar(() => raiz.Q(className: "dialogo-velo")?.Q<Button>(className: "boton-primario") != null, 5, "la confirmación de " + que);
                yield return Clic(raiz.Q(className: "dialogo-velo").Q<Button>(className: "boton-primario"));
            }
            try
            {
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("faccion"));
                yield return Esperar(() => Ventana()?.Q<Button>("pestana-faccion-diplomacia") != null, 5, "tu Facción");
                yield return Clic(Ventana().Q<Button>("pestana-faccion-diplomacia"));
                yield return Esperar(() => Ventana()?.Q("diplo-lista") != null, 5, "la Diplomacia");
                Assert.IsNotNull(Ventana().Q("diplomacia-propia"), "lo tuyo (eres el Rey: puedes proponer)");
                yield return Esperar(() => Ventana().Q<Button>("diplo-faccion-" + otra) != null, 10, "la otra Facción en la lista");

                // La otra Facción: su ficha y «Nueva propuesta» con una alianza; se manda y se retira.
                yield return Clic(Ventana().Q<Button>("diplo-faccion-" + otra));
                yield return Esperar(() => Ventana()?.Q("diplo-ficha-cab") != null && Ventana().Q("nueva-propuesta") != null, 5, "su ficha con «Nueva propuesta»");
                Assert.IsNotNull(Ventana().Q("diplo-datos"), "sus datos");
                yield return Clic(Ventana().Q<Button>("opcion-alianza"));
                yield return Esperar(() => Ventana()?.Q("clausula-alianza") != null, 5, "la cláusula de la alianza");
                yield return EnTamanos(raiz, "105_diplomacia_otra", () => DentroDelMarco(Ventana(), "la Diplomacia con otra"));
                yield return Clic(Ventana().Q<Button>("btn-proponer"));
                yield return Esperar(() => Propuesta() != null, 10, "la alianza propuesta en el motor");
                var pid = Propuesta().Id;
                yield return Esperar(() => Ventana()?.Q<Button>("retirar-" + pid) != null, 5, "«Retirar» la propuesta");
                yield return Capturar("105b_alianza_propuesta");
                yield return Clic(Ventana().Q<Button>("retirar-" + pid));
                yield return Esperar(() => Propuesta() == null, 10, "la propuesta retirada");
                yield return Esperar(() => Ventana()?.Q<Button>("retirar-" + pid) == null && Ventana()?.Q<Button>("opcion-alianza") != null, 5, "la ficha sin la propuesta");
                yield return Quieto(0.6f);

                // La guerra: se declara con su confirmación y se ofrece la paz; la otra la ofrece también y se acaba.
                yield return Esperar(() => Ventana()?.Q<Button>("opcion-guerra") != null, 5, "la opción de la guerra");
                yield return Clic(Ventana().Q<Button>("opcion-guerra"));
                yield return Esperar(() => Ventana()?.Q<Button>("btn-proponer")?.text == "Declarar la guerra", 5, "«Declarar la guerra»");
                yield return Clic(Ventana().Q<Button>("btn-proponer"));
                yield return Confirmar("la guerra");
                yield return Esperar(() => Relacion()?.Tipo == V1.TipoRelacion.RelacionGuerra, 10, "la guerra en el motor");
                var guerra = Relacion().Id;
                yield return Esperar(() => Ventana()?.Q<Button>("paz-" + guerra) != null, 5, "«Ofrecer la paz»");
                Assert.IsTrue(Ventana().Q<Button>("diplo-faccion-" + otra).ClassListContains("guerra"), "en la lista, en guerra");
                yield return Capturar("105c_en_guerra");
                yield return Clic(Ventana().Q<Button>("paz-" + guerra));
                yield return Esperar(() => Relacion()?.PazPropuestaPor == mia, 10, "la paz ofrecida");
                yield return Esperar(() => Ventana()?.Q<Label>(className: "diplo-relacion-sub")?.text == "Has ofrecido la paz: falta que la ofrezca la otra", 5, "«Has ofrecido la paz»");
                yield return OrdenComo(url, nickOtra, () => new V1.Orden { ProponerPaz = new V1.OrdenProponerPaz { RelacionId = guerra, FaccionId = otra } });
                yield return Esperar(() => Relacion() == null, 15, "la paz firmada");

                // La otra propone un vasallaje (ella, la señora; tributo de 5 de trigo por minuto) y el Rey lo acepta desde lo suyo.
                yield return OrdenComo(url, nickOtra, () => new V1.Orden { ProponerRelacion = new V1.OrdenProponerRelacion
                    { Tipo = "vasallaje", FaccionAId = otra, FaccionBId = mia, TributoRecurso = "trigo", TributoCantidad = 5_000 } });
                yield return Esperar(() => Propuesta() != null, 15, "el vasallaje propuesto");
                var vid = Propuesta().Id;
                yield return Clic(Ventana().Q<Button>("diplo-faccion-" + mia));
                yield return Esperar(() => Ventana()?.Q<Button>("aceptar-" + vid) != null, 10, "«Aceptar» en las propuestas recibidas");
                yield return EnTamanos(raiz, "106_diplomacia_propia", () => DentroDelMarco(Ventana(), "tu Diplomacia"));
                yield return Clic(Ventana().Q<Button>("aceptar-" + vid));
                yield return Confirmar("el vasallaje");
                yield return Esperar(() => Relacion()?.Tipo == V1.TipoRelacion.RelacionVasallaje, 10, "el vasallaje en el motor");
                var vas = Relacion().Id;
                yield return Esperar(() => Ventana()?.Q<Button>("rebelarse-" + vas) != null, 5, "«Rebelarse»");
                StringAssert.StartsWith("Eres vasalla de", Ventana().Q("relacion-" + vas).Q<Label>(className: "diplo-relacion-titulo").text);
                Assert.AreEqual("Tributo: 5 Trigo/min", Ventana().Q("relacion-" + vas).Q<Label>(className: "diplo-relacion-sub").text, "el tributo");
                yield return Capturar("106b_vasalla");
                Debug.Log($"[Prueba] diplomacia: alianza propuesta y retirada, guerra y paz, vasallaje aceptado ({vas})");
                yield return Tecla(UnityEngine.InputSystem.Key.Escape);
                yield return Esperar(() => Ventana() == null, 5, "Esc cierra tu Facción");
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
