// Tu Facción (paso 8 del bloque 4, tanda 2), con el ratón y el teclado de verdad contra el motor, con dos cuentas. El Rey de una Facción sin plaza
// abre la bandera: el lateral (sigilo con su nivel, la franja, el Rey y el Embajador, los cupos, los títulos, «Dejar la Facción»); en Miembros acepta
// la solicitud de otra cuenta y la nombra Embajador; Plazas (sin ninguna); en Ajustes del Rey admite a otras Facciones; en la Base, bajo su nombre,
// su sigilo y «Rey»; y en la taberna del campamento, la subpestaña Fondo: aporta madera de su almacén al fondo de refundación de su Facción.
using System;
using System.Collections;
using System.Linq;
using ConquestTactics.Mundo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using V1 = BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class PantallasDeMundoTests
    {
        /// <summary>Una cuenta nueva con su héroe, sin Facción, que pide entrar en `faccionId`. Devuelve el id del héroe.</summary>
        static IEnumerator CuentaQuePideIngreso(string url, string faccionId, Action<string> heroeDe)
        {
            string nick = null;
            yield return OtroHeroe(url, "Aspirante" + DateTime.UtcNow.Ticks % 100_000, nickDe: x => nick = x);
            var acceso = new Acceso(url);
            var login = acceso.Entrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => login.IsCompleted, 20, "el login");
            var s = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
            var abrir = s.Abrir();
            for (var f = Time.realtimeSinceStartup + 20; !abrir.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
            for (var f = Time.realtimeSinceStartup + 2; Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
            var orden = s.Ordenar(new V1.Orden { SolicitarIngreso = new V1.OrdenSolicitarIngreso { FaccionId = faccionId } });
            for (var f = Time.realtimeSinceStartup + 20; !orden.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
            Assert.AreEqual(V1.Resultado.OrdenAceptada, orden.Result.Resultado, orden.Result.Rechazo?.Codigo);
            heroeDe(abrir.Result.HeroeId);
            s.Cerrar();
        }

        [UnityTest, Timeout(900_000)]
        public IEnumerator TuFaccionDelRey()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta dar madera");
            TexturaDeSigilo.Asegurar();
            var azar = Sigilos.Aleatorio(new System.Random());
            string nick = null, faccion = null, aspirante = null;
            yield return CuentaConFaccion(url, "Rey" + DateTime.UtcNow.Ticks % 100_000, "Micenas",
                SigiloDePrueba(azar.FormaId, azar.CampoId, azar.EmblemaId, azar.ColorPrimarioId, azar.ColorSecundarioId), x => nick = x, x => faccion = x);
            yield return CuentaQuePideIngreso(url, faccion, x => aspirante = x);
            VisualElement raiz = null;
            ConexionDeMundo conexion = null;
            yield return EntrarAlCampamento(url, nick, (r, c) => { raiz = r; conexion = c; });
            LogAssert.ignoreFailingMessages = true; // el error de relleno del escuadrón vacío al nacer el héroe en FeudoScene
            UnityEngine.Object.FindAnyObjectByType<HeroSelectionSceneController>().confirmButton.onClick.Invoke();
            yield return Esperar(() => raiz.Q("interior") != null && SelectorDePersonaje.Interior?.HeroeDentro == true, 60, "el interior");
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            yield return Quieto(1f);
            V1.Heroe Yo() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            V1.Faccion Faccion() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Faccion, faccion)?.Faccion;
            var interior = SelectorDePersonaje.Interior;
            var heroe = UnityEngine.Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>(FindObjectsSortMode.None).First(x => x.IsLocalHero);
            yield return DarAlmacen(url, conexion.HeroeId, "madera", 200_000);

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
            IEnumerator Pestana(string id)
            {
                yield return ClicEnVentana(raton, raiz, Ventana().Q<Button>("pestana-faccion-" + id));
                yield return Esperar(() => Ventana()?.Q<Button>("pestana-faccion-" + id)?.ClassListContains("activo") == true, 5, "la pestaña " + id);
            }
            try
            {
                // La bandera, ya con Facción: el lateral y Miembros, con la solicitud de la otra cuenta.
                yield return ClicFijo(raton, raiz, raiz.Q<Button>("faccion"));
                yield return Esperar(() => Ventana()?.Q("faccion-lateral") != null, 5, "tu Facción");
                var lateral = Ventana().Q("faccion-lateral");
                Assert.IsNotNull(lateral.Q(className: "faccion-sigilo-grande"), "el sigilo grande");
                Assert.IsNotNull(lateral.Q(className: "faccion-nivel"), "con su nivel");
                Assert.IsNotNull(lateral.Q("dejar-faccion"), "«Dejar la Facción»");
                Assert.IsNotNull(lateral.Q("franja-miembros"), "la franja");
                CollectionAssert.AreEqual(new[] { "pestana-faccion-miembros", "pestana-faccion-plazas", "pestana-faccion-tecnologia", "pestana-faccion-diplomacia", "pestana-faccion-ajustes" },
                    Ventana().Q("faccion-pestanas").Query<Button>().ToList().Select(b => b.name).ToArray(), "las pestañas que ya hay");
                yield return Esperar(() => Ventana().Q<Button>("aceptar-" + aspirante) != null, 10, "la solicitud en Miembros");
                yield return EnTamanos(raiz, "97_tu_faccion", () => DentroDelMarco(Ventana(), "tu Facción"));
                yield return ClicEnVentana(raton, raiz, Ventana().Q<Button>("aceptar-" + aspirante));
                yield return Esperar(() => Faccion().CiudadanosIds.Contains(aspirante), 10, "el aspirante, ciudadano");
                yield return Esperar(() => Ventana().Q("miembro-" + aspirante) != null, 5, "en la lista de miembros");
                // Designar Embajador al nuevo ciudadano.
                yield return Esperar(() => Ventana().Q<DropdownField>("sel-embajador") != null, 5, "el selector del Embajador");
                var combo = Ventana().Q<DropdownField>("sel-embajador");
                combo.index = combo.choices.IndexOf(aspirante);
                Assert.GreaterOrEqual(combo.index, 0, "el aspirante en el selector");
                yield return ClicEnVentana(raton, raiz, Ventana().Q<Button>("btn-embajador"));
                yield return Esperar(() => Faccion().HasEmbajadorId && Faccion().EmbajadorId == aspirante, 10, "el Embajador en el motor");
                yield return Esperar(() => Ventana().Q("faccion-lateral").Q<Label>(className: "faccion-cargo-nombre") != null
                    && Ventana().Q("faccion-lateral").Query<Label>(className: "faccion-cargo-nombre").ToList().Any(l => l.text == Textos.NombreDeHeroe(conexion.Sesion.Almacen, aspirante)), 5, "el Embajador en el lateral");
                yield return Capturar("97b_embajador");

                // Plazas: sin ninguna todavía.
                yield return Pestana("plazas");
                yield return Esperar(() => Ventana().Q<Label>(className: "faccion-vacia")?.text == "Tu Facción aún no tiene plazas.", 5, "sin plazas");
                yield return Capturar("98_plazas");
                // Ajustes del Rey: admitir a otras Facciones en los ataques.
                yield return Pestana("ajustes");
                yield return EnTamanos(raiz, "99_ajustes", () => DentroDelMarco(Ventana(), "Ajustes del Rey"));
                yield return ClicEnVentana(raton, raiz, Ventana().Q<Button>("btn-admision"));
                yield return Esperar(() => Faccion().AdmiteOtrasEnAtaques, 10, "admitidas en el motor");
                yield return Esperar(() => Ventana().Q<Button>("btn-admision")?.text == "Dejar de admitirlas", 5, "el botón cambiado");
                yield return Tecla(UnityEngine.InputSystem.Key.Escape);
                yield return Esperar(() => Ventana() == null, 5, "Esc cierra tu Facción");

                // La Base: bajo el nombre, el sigilo y «Rey».
                yield return Tecla(UnityEngine.InputSystem.Key.B);
                yield return Esperar(() => raiz.Q("base-personal")?.Q("base-cargos") != null, 5, "los cargos en la Base");
                StringAssert.StartsWith("Rey", raiz.Q("base-personal").Q("base-cargos").Q<Label>(className: "base-cargos-texto").text);
                yield return Capturar("100_base_cargos");
                yield return Tecla(UnityEngine.InputSystem.Key.Escape);
                yield return Esperar(() => raiz.Q("base-personal") == null, 5, "la Base cerrada");

                // El Fondo de refundación, en la taberna (con Facción y sin plaza): aportar 10 de madera del almacén.
                yield return AndarHasta(teclado, heroe.transform, "taberna", () => interior.ZonaCercana == "taberna");
                yield return Esperar(() => interior.ZonaCercana == "taberna", 5, "en la taberna");
                yield return Tecla(UnityEngine.InputSystem.Key.F);
                yield return Esperar(() => raiz.Q("panel-taberna")?.Q<Button>("seccion-fondo") != null, 5, "la subpestaña Fondo");
                yield return ClicFijo(raton, raiz, raiz.Q("panel-taberna").Q<Button>("seccion-fondo"));
                yield return Esperar(() => raiz.Q("panel-taberna").Q<Button>("fondo-aportar") != null, 5, "el Fondo");
                yield return EnTamanos(raiz, "101_fondo", () => DentroDelMarco(raiz.Q("panel-taberna").Q(className: "placa"), "el Fondo"));
                var panel = raiz.Q("panel-taberna");
                var recurso = panel.Q<DropdownField>("fondo-recurso");
                Assert.IsNotNull(recurso, "lo que se puede aportar");
                recurso.index = recurso.choices.FindIndex(x => x.StartsWith("Madera"));
                Assert.GreaterOrEqual(recurso.index, 0, "la madera del almacén");
                panel.Q<IntegerField>("fondo-cantidad").value = 10;
                V1.CampamentoMercenarios Camp() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.CampamentoMercenarios, Yo().Ubicacion.CampamentoId).CampamentoMercenarios;
                yield return ClicEnVentana(raton, raiz, panel.Q<Button>("fondo-aportar"));
                yield return Esperar(() => Camp().Fondos.TryGetValue(faccion, out var f) && f.PorId.TryGetValue("madera", out var n) && n >= 10_000, 10, "la madera en el fondo");
                yield return Esperar(() => raiz.Q("panel-taberna")?.Q("fondo-madera")?.Q<Label>(className: "fondo-cifra")?.text.StartsWith("10 /") == true, 5, "reunido en su fila");
                yield return Capturar("101b_fondo_aportado");
                Debug.Log($"[Prueba] fondo: {string.Join(", ", Camp().Fondos[faccion].PorId.Select(kv => $"{kv.Key} {kv.Value / 1000}"))}");
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
