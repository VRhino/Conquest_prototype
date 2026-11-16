// Tu plaza por dentro, tanda 3 (paso 7 del bloque 4), con el ratón de verdad contra el motor. Dentro de `TuPlazaPorDentro`: Defensa (la guarnición;
// abandonar el anillo de muralla y trazarlo otra vez; abrir la puerta a los neutrales y el almacén a los aliados), Reclutamiento (el árbol de la
// plaza: reclutar los granjeros del Centro Urbano) y la Armería desde la ficha de su edificio (comprar y vender, la forja y abrirla a los aliados).
// Y `TuPlazaAjenaDeVisita`: un héroe sin Facción colocado dentro de esa plaza ve su interior con lo de fuera: el blasón sin lo tuyo, sin obras, solo la
// Armería en el riel y «Salir y retomar mi columna».
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
        /// <summary>El elemento que ocupa el sitio de `e` tras repintarse: el mismo camino de nombres desde la raíz.</summary>
        static VisualElement ElMismo(VisualElement raiz, VisualElement e)
        {
            var nombres = new System.Collections.Generic.List<string>();
            for (var x = e; x != null && x != raiz; x = x.parent) if (!string.IsNullOrEmpty(x.name)) nombres.Add(x.name);
            VisualElement r = raiz;
            for (var i = nombres.Count - 1; i >= 0 && r != null; i--) r = r.Q(nombres[i]);
            return r;
        }

        /// <summary>Como `ClicFijo`, desplazando antes la ventana hasta el elemento (con el panel ya al tamaño del clic, que recoloca la ventana).</summary>
        IEnumerator ClicEnVentana(UnityEngine.InputSystem.Mouse raton, VisualElement raiz, VisualElement e)
        {
            Assert.IsNotNull(e, "el elemento del clic");
            var doc = go.GetComponentInChildren<UIDocument>();
            var textura = doc.panelSettings.targetTexture;
            try
            {
                doc.panelSettings.targetTexture = null;
                for (int i = 0; i < 8; i++) yield return null;
                // Si la ventana se repintó mientras (lo que cambia en el motor la rehace), el elemento de antes ya no está: el nuevo, por sus nombres.
                if (e.panel == null) e = ElMismo(raiz, e) ?? e;
                for (var p = e.parent; p != null; p = p.parent)
                    if (p is ScrollView sv) { if (sv.panel != null) sv.ScrollTo(e); break; }
                for (int i = 0; i < 3; i++) yield return null;
                if (e.panel == null) e = ElMismo(raiz, e) ?? e;
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

        /// <summary>Una cuenta nueva con su héroe en el campamento (para vetarlo, colocarlo…). Devuelve su id por `idDe`.</summary>
        static IEnumerator HeroeDeOtraCuenta(string url, Action<string> idDe)
        {
            var nick = "otro-" + DateTime.UtcNow.Ticks % 100_000_000;
            var acceso = new Acceso(url);
            var registro = acceso.Registrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => registro.IsCompleted, 20, "la cuenta");
            var login = acceso.Entrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => login.IsCompleted, 20, "el login");
            var s = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
            var abrir = s.Abrir();
            for (var f = Time.realtimeSinceStartup + 20; !abrir.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
            var camp = abrir.Result.CampamentosParaElegir[0].CampamentoId;
            foreach (var nombre in new[] { "Egisto", "Orestes", "Pílades", "Electrión" })
            {
                var r = s.Ordenar(new V1.Orden { CrearHeroe = new V1.OrdenCrearHeroe { DisplayName = nombre, CampamentoId = camp, ClassDefinitionId = "espada_escudo_mediano", Genero = "masculino", Avatar = new V1.OrdenCrearHeroe.Types.Avatar() } });
                for (var f = Time.realtimeSinceStartup + 20; !r.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
                if (r.Result.Resultado == V1.Resultado.OrdenAceptada) { idDe(r.Result.Creadas.First(c => c.Entidad == V1.TipoEntidad.Heroe).Id); break; }
            }
            s.Cerrar();
        }

        IEnumerator TandaTres(UnityEngine.InputSystem.Mouse raton, VisualElement raiz, VisualElement plaza, Func<V1.Asentamiento> Plaza, Func<V1.Heroe> Yo,
            Func<VisualElement> Ficha, Func<UnityEngine.InputSystem.Key, IEnumerator> Tecla, ConexionDeMundo conexion)
        {
            VisualElement Riel() => plaza.Q("riel");
            VisualElement Ventana() => plaza.Q("plaza-ventana");
            IEnumerator ClicEn(VisualElement e) => ClicEnVentana(raton, raiz, e);
            var interior = SelectorDePersonaje.Interior;
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            Google.Protobuf.WellKnownTypes.Struct Parametros(params (string k, object v)[] ps)
            {
                var q = new Google.Protobuf.WellKnownTypes.Struct();
                foreach (var (k, v) in ps) q.Fields[k] = v is string t ? Google.Protobuf.WellKnownTypes.Value.ForString(t) : Google.Protobuf.WellKnownTypes.Value.ForNumber(Convert.ToDouble(v));
                return q;
            }

            // El riel, entero: Construcción, Gobierno, Defensa, Reclutamiento y la Armería (ya en pie).
            var armeria = Plaza().Edificios.FirstOrDefault(e => e.Tipo == "armeria" && e.Estado == V1.EstadoEdificio.EdificioActivo);
            var botones = Riel().Query<Button>(className: "plaza-riel-boton").ToList().Select(b => b.name).ToList();
            Debug.Log($"[Prueba] riel: {string.Join(", ", botones)}; armería {(armeria != null ? "en pie" : "sin acabar")}");
            CollectionAssert.IsSubsetOf(new[] { "riel-construccion", "riel-gobierno", "riel-defensa", "riel-reclutamiento" }, botones, "el riel de la plaza");
            if (armeria != null) CollectionAssert.Contains(botones, "riel-armeria", "la Armería en el riel");

            // ── El Barracón, que da cupo a la guarnición: desde el cajón; con la Vía Rápida y el reloj deprisa acaba en segundos, y la muralla
            // (con la Vía Rápida también cada celda) se levanta entera ──
            VisualElement Cajon() => plaza.Q("cajon-construir");
            yield return ClicFijo(raton, raiz, Riel().Q<Button>("riel-construccion"));
            yield return Esperar(() => Cajon() != null, 5, "el cajón de Construcción");
            if (Cajon().Q<Button>("grupo-militar") is Button militar)
            {
                yield return ClicFijo(raton, raiz, militar);
                yield return Esperar(() => Cajon().Q<Button>("grupo-militar").ClassListContains("activo"), 5, "la pestaña Militar");
            }
            yield return ConViaRapida(conexion.Sesion, Plaza().Id, conexion.HeroeId, bombear: false);
            Assert.IsNotNull(Cajon().Q<Button>("construir-barracon"), "el Barracón en el cajón");
            yield return ClicFijo(raton, raiz, Cajon().Q<Button>("construir-barracon"));
            yield return Esperar(() => Plaza().Edificios.Any(e => e.Tipo == "barracon"), 10, "el Barracón en la cola");
            yield return ClicFijo(raton, raiz, Cajon().Q<Button>("cajon-volver"));
            yield return Esperar(() => Cajon() == null && Riel().Q<Button>("riel-defensa") != null, 5, "el riel de vuelta");
            yield return Reloj(url, 60_000);
            bool BarraconEnPie() => Plaza().Edificios.Any(e => e.Tipo == "barracon" && e.Estado == V1.EstadoEdificio.EdificioActivo);
            // Sin recinto si el sitio no deja cerrar el anillo (`murallas.no_se_puede_cerrar` al fundar): entonces no hay muralla que esperar.
            var conMuralla = Plaza().Recintos.Count > 0;
            if (!conMuralla) Debug.Log("[Prueba] muralla: este sitio no deja cerrar el anillo; sin muralla en esta plaza");
            bool MurallaEntera() => !conMuralla || Plaza().Recintos.All(r => r.Avance >= r.Celdas.Count);
            yield return ConViaRapidaHasta(conexion.Sesion, Plaza().Id, conexion.HeroeId, () => BarraconEnPie() && MurallaEntera(), 240, bombear: false);
            Assert.IsTrue(BarraconEnPie(), "el Barracón en pie");
            Assert.IsTrue(MurallaEntera(), $"la muralla entera ({string.Join(", ", Plaza().Recintos.Select(r => $"{r.Avance} de {r.Celdas.Count}"))})");
            yield return Reloj(url, 1_000);
            Debug.Log($"[Prueba] Barracón en pie; muralla {string.Join(", ", Plaza().Recintos.Select(r => $"nivel {r.Nivel}: {r.Avance} de {r.Celdas.Count} celdas"))}");
            // Una escuadra en tu campamento para dejarla en la guarnición (de escalón 1: 7 de Liderazgo, lo que da un Barracón de nivel 1).
            var escuadras = conexion.Sesion.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Escuadron).Count();
            yield return Administrar(url, "dar_escuadra", Parametros(("heroe_id", conexion.HeroeId), ("tropa_id", "lenadores"), ("cantidad", 30)));
            yield return Esperar(() => conexion.Sesion.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Escuadron).Count() > escuadras, 10, "la escuadra dada");

            // ── Defensa: la guarnición ──
            yield return ClicFijo(raton, raiz, Riel().Q<Button>("riel-defensa"));
            yield return Esperar(() => !Ventana().ClassListContains("oculto") && Ventana().Q("guarnicion") != null, 5, "Defensa");
            yield return EnTamanos(raiz, "87_defensa_guarnicion", () => DentroDelMarco(Ventana(), "Defensa"));
            Debug.Log($"[Prueba] guarnición: cupo {Plaza().CupoGuarnicion}, «{Ventana().Q<Label>("cupo-guarnicion")?.text}»");
            Assert.Greater(Plaza().CupoGuarnicion, 0, "con el Barracón, cupo de guarnición");
            Assert.IsNotNull(Ventana().Q<Button>("guarnicion-mas"), "«+» para dejar una escuadra");
            yield return ClicEn(Ventana().Q<Button>("guarnicion-mas"));
            yield return Esperar(() => Ventana().Q("guarnicion-elegir") != null, 5, "las escuadras para dejar");
            yield return Capturar("87b_guarnicion_elegir");
            var asignar = Ventana().Q("guarnicion-elegir").Query<Button>().ToList().FirstOrDefault(b => b.enabledSelf);
            Assert.IsNotNull(asignar, "una escuadra que quepa en el cupo");
            var deGuarnicion = asignar.name.Substring("asignar-".Length);
            yield return ClicEn(asignar);
            yield return Esperar(() => Plaza().GuarnicionIds.Contains(deGuarnicion), 10, "la escuadra en la guarnición");
            yield return Esperar(() => Ventana().Q("guarnicion-" + deGuarnicion) != null, 5, "su hueco en la guarnición");
            yield return EnTamanos(raiz, "87c_guarnicion_con_escuadra", () => DentroDelMarco(Ventana(), "Defensa"));
            yield return ClicEn(Ventana().Q<Button>("retirar-" + deGuarnicion));
            yield return Esperar(() => !Plaza().GuarnicionIds.Contains(deGuarnicion), 10, "la escuadra fuera de la guarnición");
            // Y se deja otra vez: la guarnición se queda en la plaza al salir (sin ella, la columna se pasaría de Liderazgo).
            yield return Esperar(() => Ventana().Q<Button>("guarnicion-mas") != null && Ventana().Q("guarnicion-" + deGuarnicion) == null, 10, "«+» otra vez");
            yield return ClicEn(Ventana().Q<Button>("guarnicion-mas"));
            yield return Esperar(() => Ventana().Q<Button>("asignar-" + deGuarnicion) != null, 5, "la escuadra para dejar otra vez");
            yield return ClicEn(Ventana().Q<Button>("asignar-" + deGuarnicion));
            yield return Esperar(() => Plaza().GuarnicionIds.Contains(deGuarnicion), 10, "la escuadra otra vez en la guarnición");
            Debug.Log($"[Prueba] guarnición: dejada, sacada y dejada otra vez ({deGuarnicion})");

            // ── Defensa: la muralla, abandonar el anillo en obra y trazarlo otra vez ──
            yield return ClicEn(Ventana().Q<Button>("sub-muralla"));
            yield return Esperar(() => Ventana().Q("muralla-plano") != null || Ventana().Q<Button>("muralla-trazar") != null, 5, "la Muralla");
            yield return EnTamanos(raiz, "88_defensa_muralla", () => DentroDelMarco(Ventana(), "Muralla"));
            if (conMuralla) Assert.IsNotNull(Ventana().Q("muralla-plano"), "el plano de la muralla levantada");
            if (Ventana().Q<Button>("abandonar-recinto") is Button abandonar)
            {
                yield return ClicEn(abandonar);
                yield return Esperar(() => capaDialogo(raiz) != null, 5, "la confirmación");
                yield return ClicFijo(raton, raiz, capaDialogo(raiz).Query<Button>().ToList().First(b => b.text == "Abandonar"));
                yield return Esperar(() => Plaza().Recintos.Count == 0, 10, "el anillo abandonado");
                yield return Esperar(() => Ventana().Q<Button>("muralla-trazar")?.enabledSelf == true, 5, "«Trazar la muralla»");
                yield return Capturar("88b_muralla_sin_anillo");
                yield return ClicEn(Ventana().Q<Button>("muralla-trazar"));
                yield return Esperar(() => Plaza().Recintos.Count == 1, 10, "el anillo trazado otra vez");
                Debug.Log($"[Prueba] muralla: trazada otra vez, {Plaza().Recintos[0].Celdas.Count} celdas de nivel {Plaza().Recintos[0].Nivel}");
            }

            // ── Defensa: la puerta, abrirla a los neutrales; y el almacén a los aliados ──
            yield return ClicEn(Ventana().Q<Button>("sub-puerta"));
            yield return Esperar(() => Ventana().Q("puerta-cab") != null, 5, "la Puerta");
            yield return EnTamanos(raiz, "89_defensa_puerta", () => DentroDelMarco(Ventana(), "Puerta"));
            var neutralesCerrada = Ventana().Q<Button>("puerta-neutrales").ClassListContains("cerrada");
            yield return ClicEn(Ventana().Q<Button>("puerta-neutrales"));
            yield return Esperar(() => Ventana().Q<Button>("puerta-neutrales").ClassListContains("cambiada"), 5, "los neutrales cambiados");
            yield return Capturar("89b_puerta_cambiada");
            yield return ClicEn(Ventana().Q<Button>("puerta-fijar"));
            yield return Esperar(() => Plaza().PuertaCerradaA.Contains(V1.GrupoPuerta.GrupoNeutrales) != neutralesCerrada, 10, "la puerta fijada en el motor");
            yield return ClicEn(Ventana().Q("alternar-aliados"));
            yield return Esperar(() => Plaza().PermiteReabastecerAliados, 10, "el almacén abierto a los aliados");
            // Vetar: el héroe de otra cuenta, ciudadano de tu Facción (los vetables son sus ciudadanos y los héroes a la vista), y levantarle el veto.
            string otro = null;
            yield return HeroeDeOtraCuenta(url, x => otro = x);
            yield return Administrar(url, "fijar_faccion_de_heroe", Parametros(("heroe_id", otro), ("faccion_id", Plaza().FaccionId)));
            yield return Esperar(() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Faccion, Plaza().FaccionId)?.Faccion?.CiudadanosIds.Contains(otro) == true, 10, "el otro héroe, ciudadano");
            yield return ClicEn(Ventana().Q<Button>("sub-muralla"));
            yield return ClicEn(Ventana().Q<Button>("sub-puerta"));
            yield return Esperar(() => Ventana().Q<DropdownField>("vetar-a") != null, 5, "a quién vetar");
            var combo = Ventana().Q<DropdownField>("vetar-a");
            combo.index = combo.choices.IndexOf(Textos.NombreDeHeroe(conexion.Sesion.Almacen, otro));
            Assert.GreaterOrEqual(combo.index, 0, "el otro héroe en el combo");
            yield return ClicEn(Ventana().Q<Button>("vetar"));
            yield return Esperar(() => Plaza().VetadosIds.Contains(otro), 10, "vetado en el motor");
            yield return Esperar(() => Ventana().Q<Button>("levantar-veto") != null, 5, "el vetado en la lista");
            yield return Capturar("89c_vetado");
            yield return ClicEn(Ventana().Q<Button>("levantar-veto"));
            yield return Esperar(() => !Plaza().VetadosIds.Contains(otro), 10, "el veto levantado");
            Debug.Log("[Prueba] vetar: vetado y levantado");
            yield return Tecla(UnityEngine.InputSystem.Key.Escape);
            yield return Esperar(() => Ventana().ClassListContains("oculto"), 5, "Esc cierra Defensa");

            // ── Reclutamiento: el árbol de la plaza ──
            yield return ClicFijo(raton, raiz, Riel().Q<Button>("riel-reclutamiento"));
            yield return Esperar(() => raiz.Q("panel-tropa") != null && raiz.Q("panel-tropa").Q("recl-tablero") != null, 10, "el árbol de la plaza");
            Assert.IsTrue(interior.PanelAbierto, "con el árbol abierto, el héroe no anda");
            var panel = raiz.Q("panel-tropa");
            Debug.Log("[Prueba] reclutamiento: " + string.Join(", ", panel.Query<Button>(className: "recl-carta").ToList().Take(12).Select(b => $"{b.name.Substring(5)}({string.Join(" ", b.GetClasses().Where(x => x != "recl-carta" && x != "unity-button" && x != "unity-text-element"))})")));
            yield return EnTamanos(raiz, "90_reclutamiento", () => DentroDelMarco(panel.Q(className: "recl-ventana"), "el árbol"));
            // Una de pago: la milicia de lanceros, con la madera de su equipo del almacén de la plaza.
            yield return ClicFijo(raton, raiz, panel.Q<Button>("nodo-milicia_lanceros"));
            yield return Esperar(() => panel.Q<Button>("reclutar-milicia_lanceros") != null || panel.Q<Label>("motivo-milicia_lanceros") != null, 5, "la ficha de la milicia");
            if (panel.Q<Button>("reclutar-milicia_lanceros") is Button pago && pago.enabledSelf)
            {
                var madera = Plaza().Almacen.TryGetValue("madera", out var m) ? Math.Floor(m.Cantidad.ValorEnT0 / 1000.0) : 0;
                yield return ClicFijo(raton, raiz, pago);
                yield return Esperar(() => Yo().EscuadronesIds.Any(id => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Escuadron, id)?.Escuadron?.TropaId == "milicia_lanceros"), 10, "la milicia reclutada");
                Debug.Log($"[Prueba] reclutamiento: milicia de lanceros reclutada (madera {madera} → {(Plaza().Almacen.TryGetValue("madera", out var m2) ? Math.Floor(m2.Cantidad.ValorEnT0 / 1000.0) : 0)})");
            }
            else Debug.Log($"[Prueba] reclutamiento: la milicia no se recluta: «{panel.Q<Label>("motivo-milicia_lanceros")?.text}»");
            yield return ClicFijo(raton, raiz, panel.Q<Button>("nodo-granjeros"));
            yield return Esperar(() => panel.Q("recl-ficha")?.Q<Label>("estado-ficha") != null, 5, "la ficha de los granjeros");
            yield return Capturar("90b_ficha_granjeros");
            if (panel.Q<Button>("reclutar-granjeros") is Button reclutar && reclutar.enabledSelf)
            {
                yield return ClicFijo(raton, raiz, reclutar);
                yield return Esperar(() => Yo().EscuadronesIds.Any(id => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Escuadron, id)?.Escuadron?.TropaId == "granjeros"), 10, "los granjeros reclutados");
                yield return Esperar(() => panel.Q<Button>("nodo-granjeros").ClassListContains("tuya"), 5, "los granjeros, tuyos en el árbol");
                yield return Capturar("90c_granjeros_tuyos");
            }
            else Debug.Log($"[Prueba] reclutamiento: los granjeros no se reclutan aquí: «{panel.Q<Label>("motivo-granjeros")?.text ?? panel.Q<Label>("estado-ficha")?.text}»");
            yield return ClicFijo(raton, raiz, panel.Q<Button>("cerrar"));
            yield return Esperar(() => raiz.Q("panel-tropa") == null, 5, "el árbol cerrado");

            // ── La Armería: desde la ficha de su edificio ──
            if (armeria == null) { Debug.Log("[Prueba] armería: sin acabar, no se prueba"); yield break; }
            interior.AlInteractuar("edificio:" + armeria.Id);
            yield return Esperar(() => Ficha()?.Q<Button>("ficha-abrir-armeria") != null, 5, "la ficha de la Armería con su botón");
            yield return Capturar("91_ficha_armeria");
            yield return ClicFijo(raton, raiz, Ficha().Q<Button>("ficha-abrir-armeria"));
            yield return Esperar(() => Ventana().Q("tienda") != null && Ventana().ClassListContains("plaza-ventana-completa"), 5, "la Armería");
            yield return EnTamanos(raiz, "91b_armeria_tienda", () => DentroDelMarco(Ventana(), "la Armería"));
            var comprar = Ventana().Q("tienda").Query<Button>("comprar").ToList().FirstOrDefault();
            var antes = Yo().Inventario.Count;
            if (comprar != null)
            {
                var monedas = Yo().Monedas;
                yield return ClicEn(comprar);
                yield return Esperar(() => Yo().Inventario.Count > antes && Yo().Monedas < monedas, 10, "la pieza comprada en la mochila");
                var nueva = Yo().Inventario.OrderByDescending(x => x.CasillaInventario).First();
                Debug.Log($"[Prueba] armería: comprado {nueva.ItemDefinitionId} por {monedas - Yo().Monedas} monedas");
                yield return Esperar(() => Ventana().Q<Button>("mochila-" + nueva.CasillaInventario) != null, 5, "la pieza en la mochila de la Armería");
                yield return ClicEn(Ventana().Q<Button>("mochila-" + nueva.CasillaInventario));
                yield return Esperar(() => Ventana().Q<Button>("vender") != null, 5, "«Vender»");
                yield return Capturar("91c_vender");
                yield return ClicEn(Ventana().Q<Button>("vender"));
                yield return Esperar(() => Yo().Inventario.Count == antes, 10, "la pieza vendida");
            }
            else Debug.Log($"[Prueba] armería: nada que comprar con {Yo().Monedas} monedas");
            if (Ventana().Q<Button>("sub-forja") is Button forja)
            {
                yield return ClicEn(forja);
                yield return Esperar(() => Ventana().Q("forja-listas") != null, 5, "la Forja");
                yield return EnTamanos(raiz, "91d_armeria_forja", () => DentroDelMarco(Ventana(), "la Forja"));
            }
            if (Ventana().Q<Button>("sub-abrir") is Button abrir)
            {
                yield return ClicEn(abrir);
                yield return Esperar(() => Ventana().Q("abrir-aliados") != null, 5, "Abrir la forja");
                Ventana().Q("abrir-aliados").Q<DropdownField>("abierta").index = 0;
                yield return Esperar(() => Ventana().Q("abrir-aliados")?.Q<Button>("guardar")?.enabledSelf == true, 5, "«Guardar» con un cambio");
                yield return Capturar("91e_abrir_la_forja");
                yield return ClicEn(Ventana().Q("abrir-aliados").Q<Button>("guardar"));
                yield return Esperar(() => Plaza().Forja?.Aliados?.Abierta == true, 10, "la forja abierta a los aliados");
            }
            yield return Tecla(UnityEngine.InputSystem.Key.Escape);
            yield return Esperar(() => Ventana().ClassListContains("oculto"), 5, "Esc cierra la Armería");
        }

        /// <summary>La plaza ajena de visita: un héroe sin Facción, colocado dentro de una plaza fundada por otro, ve su interior con lo de fuera.</summary>
        [UnityTest, Timeout(900_000)]
        public IEnumerator TuPlazaAjenaDeVisita()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza");
            string plazaId = null;
            V1.Punto donde = null;
            yield return FundarPlaza(url, _ => { }, x => plazaId = x, x => donde = x, abrirANeutrales: true);
            // El visitante: una cuenta nueva con su héroe, sin Facción, que sale de su campamento, marcha hasta la plaza y entra (de visita, la plaza solo
            // llega por lo que ve: no vale colocarlo dentro).
            var nick = "visita-" + DateTime.UtcNow.Ticks % 100_000_000;
            var acceso = new Acceso(url);
            var registro = acceso.Registrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => registro.IsCompleted, 20, "la cuenta");
            var login = acceso.Entrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => login.IsCompleted, 20, "el login");
            var s = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
            var abrirSesion = s.Abrir();
            for (var f = Time.realtimeSinceStartup + 20; !abrirSesion.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
            V1.ResultadoOrden ultimo = null;
            IEnumerator Ordenar(V1.Orden o)
            {
                var r = s.Ordenar(o);
                for (var f = Time.realtimeSinceStartup + 20; !r.IsCompleted && Time.realtimeSinceStartup < f;) { s.Bombear(); yield return null; }
                ultimo = r.Result;
                Debug.Log($"[Prueba] visita: {o.CuerpoCase} {r.Result.Resultado} {r.Result.Rechazo?.Codigo}");
            }
            var camp = abrirSesion.Result.CampamentosParaElegir[0].CampamentoId;
            foreach (var nombre in new[] { "Visitante", "Viajero", "Forastero" })
            {
                yield return Ordenar(new V1.Orden { CrearHeroe = new V1.OrdenCrearHeroe { DisplayName = nombre, CampamentoId = camp, ClassDefinitionId = "espada_escudo_mediano", Genero = "masculino", Avatar = new V1.OrdenCrearHeroe.Types.Avatar() } });
                if (ultimo.Resultado == V1.Resultado.OrdenAceptada) break;
            }
            var heroe = ultimo.Creadas.First(c => c.Entidad == V1.TipoEntidad.Heroe).Id;
            yield return Ordenar(new V1.Orden { SalirDelCampamento = new V1.OrdenSalirDelCampamento { CampamentoId = camp, HeroeId = heroe } });
            var col = ultimo.Creadas.First(c => c.Entidad == V1.TipoEntidad.Ejercito).Id;
            yield return Reloj(url, 60_000);
            yield return Ordenar(new V1.Orden { MarcharA = new V1.OrdenMarcharA { HeroeId = heroe, Objetivo = new V1.OrdenMarcharA.Types.Objetivo { Punto = donde } } });
            bool Llegada() { var c = s.Almacen.Buscar(V1.TipoEntidad.Ejercito, col)?.Ejercito; return c != null && Math.Abs(c.Posicion.X - donde.X) < 3000 && Math.Abs(c.Posicion.Y - donde.Y) < 3000; }
            for (var f = Time.realtimeSinceStartup + 120; Time.realtimeSinceStartup < f && !Llegada();)
            {
                yield return AvanzarReloj(url, s, 60_000);
                for (var g = Time.realtimeSinceStartup + 0.3f; Time.realtimeSinceStartup < g && !Llegada();) { s.Bombear(); yield return null; }
            }
            yield return Ordenar(new V1.Orden { EntrarEnAsentamiento = new V1.OrdenEntrarEnAsentamiento { AsentamientoId = plazaId, HeroeId = heroe } });
            yield return Reloj(url, 1_000);
            s.Cerrar();

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
            for (var f = Time.realtimeSinceStartup + 60; Time.realtimeSinceStartup < f && !(raiz.Q("plaza") != null && SelectorDePersonaje.Interior?.HeroeDentro == true);) yield return null;
            if (raiz.Q("plaza") == null)
            {
                var yo = conexion.Sesion?.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId)?.Heroe;
                var a = conexion.Sesion?.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId)?.Asentamiento;
                Assert.Fail($"no llegó el interior de la plaza ajena: el héroe en {yo?.Ubicacion}; la plaza {(a == null ? "no llegó" : $"llegó con trazado {(a.Trazado != null)}, {a.Edificios.Count} edificios")}; «{raiz.Q<Label>("dentro-texto")?.text}»");
            }
            yield return Esperar(() => raiz.Q(className: "carga") == null, 10, "sin la pantalla de carga");
            yield return Quieto(1.5f);
            var plaza = raiz.Q("plaza");
            V1.Asentamiento Plaza() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId).Asentamiento;
            Debug.Log($"[Prueba] de visita en {Plaza().Nombre}: {Plaza().Edificios.Count} edificios, armería activa {Plaza().ArmeriaActiva}");
            Assert.IsNotNull(GameObject.Find("Plaza del motor") ?? GameObject.Find("Campamento del motor"), "la plaza montada (de visita, sin sus edificios: el motor no los manda)");
            Assert.IsNull(plaza.Q<Button>("renombrar"), "sin el lápiz: no resides aquí");
            Assert.IsNull(plaza.Q<Button>("residencia"), "sin la casa: no es de tu Facción");
            Assert.IsTrue(plaza.Q("obras-hud").ClassListContains("oculto"), "sin sus obras");
            var botones = plaza.Q("riel").Query<Button>(className: "plaza-riel-boton").ToList().Select(b => b.name).ToList();
            Debug.Log($"[Prueba] riel de visita: {string.Join(", ", botones)}");
            // De visita quedan Territorio (solo para mirar sus pueblos, paso 9) y la Armería si la hay.
            CollectionAssert.AreEqual(Plaza().ArmeriaActiva ? new[] { "riel-territorio", "riel-armeria" } : new[] { "riel-territorio" }, botones.ToArray(), "de visita, Territorio y la Armería");
            yield return EnTamanos(raiz, "92_plaza_ajena");
            var entrada = UnityEngine.InputSystem.InputSystem.settings;
            var fondo = entrada.backgroundBehavior;
            var editorAntes = entrada.editorInputBehaviorInPlayMode;
            entrada.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            entrada.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var teclado = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            var raton = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                teclado.MakeCurrent();
                raton.MakeCurrent();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.LeftAlt));
                yield return null;
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return null;
                if (botones.Contains("riel-armeria"))
                {
                    yield return ClicFijo(raton, raiz, plaza.Q("riel").Q<Button>("riel-armeria"));
                    yield return Esperar(() => plaza.Q("plaza-ventana").Q("tienda") != null, 5, "la Armería de visita");
                    Assert.IsNull(plaza.Q("plaza-ventana").Q<Button>("sub-abrir"), "de visita no se abre la forja");
                    yield return Capturar("92b_armeria_de_visita");
                    yield return ClicFijo(raton, raiz, plaza.Q("plaza-ventana").Q<Button>("ventana-cerrar"));
                    yield return Esperar(() => plaza.Q("plaza-ventana").ClassListContains("oculto"), 5, "la Armería cerrada");
                }
                yield return ClicFijo(raton, raiz, plaza.Q<Button>("salir-al-mundo"));
                yield return Esperar(() => raiz.Q("ventana-salida") != null, 5, "«Salir al mundo»");
                var salir = raiz.Q("ventana-salida").Q<Button>("salir");
                Debug.Log($"[Prueba] salir de visita: «{salir.text}»");
                Assert.AreEqual("SALIR Y RETOMAR MI COLUMNA", salir.text, "de otra plaza se retoma la columna");
                yield return Capturar("92c_salir_de_visita");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(teclado);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                entrada.backgroundBehavior = fondo;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
