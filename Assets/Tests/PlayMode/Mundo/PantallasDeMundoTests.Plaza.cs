// Tu plaza por dentro (paso 7 del bloque 4, tanda 1) de punta a punta contra el servidor de prueba, con teclado y ratón de verdad. El motor de
// prueba no tiene orden de administración para crear una plaza: un héroe nuevo la funda como un jugador (crea su Facción, sale del campamento con
// una caravana de fundación, marcha y funda), y el servidor de prueba la sube a nivel 3 y le da recursos; el héroe, Gobernador de oficio, pone
// obras en la cola. Al entrar por el selector se abre su interior: la plaza montada desde el motor y el HUD calcado del prototipo, en los cuatro
// tamaños. El héroe anda hasta un edificio y, con F, su ficha; con el ratón: mejorar, el buey del molino, la receta de la bodega, la cola (◀ ✕ ▶),
// cancelar una obra, «Subir de nivel» desde el medallón, la casa y la corona, cambiar el nombre y «Salir al mundo».
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
        /// <summary>Regla de las pruebas con plaza (Víctor, 2026-11-15): nada más fundarla y antes de encolar nada, su héroe se nombra Maestro de Obras y
        /// activa la Vía Rápida de Construcción, que en el motor de prueba deja toda obra en el 1 % de su tiempo (mínimo, un minuto): edificios, mejoras,
        /// cada celda de muralla y la subida de nivel (motor cc1e5ec). Como toda política, dura 150 minutos de juego (dos minutos y medio con el reloj
        /// deprisa): esto la vuelve a activar si ya no está. Toda prueba que funde o use una plaza pasa por aquí (`FundarPlaza` lo hace al fundar).
        /// `bombear`: falso con la sesión de `ConexionDeMundo`, que ya se bombea sola.</summary>
        static IEnumerator ConViaRapida(Sesion s, string plaza, string heroe, bool bombear = true)
        {
            var a = s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plaza)?.Asentamiento;
            if (a != null && a.PoliticasActivas.Any(x => x.PoliticaId == "via_rapida")) yield break;
            var ordenes = new System.Collections.Generic.List<V1.Orden>();
            if (a?.Cargos == null || !a.Cargos.HasMaestroObrasId || a.Cargos.MaestroObrasId != heroe)
                ordenes.Add(new V1.Orden { AsignarCargoLocal = new V1.OrdenAsignarCargoLocal { AsentamientoId = plaza, Cargo = "maestroObras", HeroeId = heroe } });
            ordenes.Add(new V1.Orden { ActivarPolitica = new V1.OrdenActivarPolitica { AsentamientoId = plaza, Cargo = "maestroObras", PoliticaId = "via_rapida" } });
            foreach (var o in ordenes)
            {
                var r = s.Ordenar(o);
                for (var f = Time.realtimeSinceStartup + 20; !r.IsCompleted && Time.realtimeSinceStartup < f;) { if (bombear) s.Bombear(); yield return null; }
                Assert.IsTrue(r.IsCompleted, o.CuerpoCase + ": sin respuesta");
                Assert.AreEqual(V1.Resultado.OrdenAceptada, r.Result.Resultado, $"{o.CuerpoCase}: {r.Result.Rechazo?.Codigo}");
            }
            for (var f = Time.realtimeSinceStartup + 5; Time.realtimeSinceStartup < f && s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plaza)?.Asentamiento?.PoliticasActivas.Any(x => x.PoliticaId == "via_rapida") != true;)
            { if (bombear) s.Bombear(); yield return null; }
            Debug.Log($"[Prueba] {plaza}: Maestro de Obras con la Vía Rápida");
        }

        /// <summary>Espera a `hecho` (o `seg` segundos) con la Vía Rápida en vigor todo el rato.</summary>
        static IEnumerator ConViaRapidaHasta(Sesion s, string plaza, string heroe, Func<bool> hecho, float seg, bool bombear = true)
        {
            for (var f = Time.realtimeSinceStartup + seg; Time.realtimeSinceStartup < f && !hecho();)
            {
                yield return ConViaRapida(s, plaza, heroe, bombear);
                // El reloj del mundo, a saltos de 10 minutos, en vez de esperarlo.
                yield return AvanzarReloj(Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL"), s, 600_000);
                for (var g = Time.realtimeSinceStartup + 1; Time.realtimeSinceStartup < g && !hecho();) { if (bombear) s.Bombear(); yield return null; }
            }
        }

        /// <summary>Funda una plaza con una cuenta nueva, como un jugador, la sube a nivel 3 con recursos y le pone obras en la cola. Devuelve el
        /// nick por `nickDe`.</summary>
        static IEnumerator FundarPlaza(string url, Action<string> nickDe, Action<string> plazaDe = null, Action<V1.Punto> dondeDe = null, bool abrirANeutrales = false, bool mercadoEnPie = false, string enLaFaccion = null, Action<string> faccionDe = null)
        {
            var nick = "plaza-" + DateTime.UtcNow.Ticks % 100_000_000;
            nickDe(nick);
            var acceso = new Acceso(url);
            var registro = acceso.Registrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => registro.IsCompleted, 20, "la cuenta");
            var login = acceso.Entrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => login.IsCompleted, 20, "el login");
            var s = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
            var abrir = s.Abrir();
            var fin = Time.realtimeSinceStartup + 20;
            while (!abrir.IsCompleted && Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
            V1.ResultadoOrden ultimo = null;
            IEnumerator Ordenar(V1.Orden o)
            {
                var r = s.Ordenar(o);
                var f = Time.realtimeSinceStartup + 20;
                while (!r.IsCompleted && Time.realtimeSinceStartup < f) { s.Bombear(); yield return null; }
                ultimo = r.Result;
                Debug.Log($"[Prueba] fundar: {o.CuerpoCase} {r.Result.Resultado} {r.Result.Rechazo?.Codigo}");
            }
            IEnumerator Bombear(float seg) { var f = Time.realtimeSinceStartup + seg; while (Time.realtimeSinceStartup < f) { s.Bombear(); yield return null; } }
            var camp = abrir.Result.CampamentosParaElegir[0].CampamentoId;
            // Los nombres no se repiten en el mundo: si otra prueba ya los usó en este servidor, el siguiente.
            foreach (var nombre in new[] { "Fundador", "Atreo", "Tiestes", "Pélope", "Agamenón", "Menelao", "Egeo", "Teseo", "Cadmo", "Néstor" })
            {
                yield return Ordenar(new V1.Orden { CrearHeroe = new V1.OrdenCrearHeroe { DisplayName = nombre, CampamentoId = camp, ClassDefinitionId = "espada_escudo_mediano", Genero = "masculino", Avatar = new V1.OrdenCrearHeroe.Types.Avatar() } });
                if (ultimo.Resultado == V1.Resultado.OrdenAceptada) break;
            }
            var heroe = ultimo.Creadas.First(c => c.Entidad == V1.TipoEntidad.Heroe).Id;
            yield return Bombear(1);
            // Con `enLaFaccion`, el héroe entra en esa Facción (la administración) y su plaza será de ella; si no, crea la suya.
            if (enLaFaccion != null)
            {
                var q = new Google.Protobuf.WellKnownTypes.Struct();
                q.Fields["heroe_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(heroe);
                q.Fields["faccion_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(enLaFaccion);
                yield return Administrar(url, "fijar_faccion_de_heroe", q);
            }
            else
                foreach (var nombre in new[] { "Micenas", "Argos", "Tebas", "Pilos", "Corinto", "Esparta", "Atenas", "Creta", "Ítaca", "Tirinto" })
                {
                    yield return Ordenar(new V1.Orden { CrearFaccion = new V1.OrdenCrearFaccion { Nombre = nombre } });
                    if (ultimo.Resultado == V1.Resultado.OrdenAceptada) { faccionDe?.Invoke(ultimo.Creadas.FirstOrDefault(c => c.Entidad == V1.TipoEntidad.Faccion)?.Id); break; }
                }
            yield return Bombear(1);
            yield return Ordenar(new V1.Orden { SalirDelCampamento = new V1.OrdenSalirDelCampamento { CampamentoId = camp, HeroeId = heroe } });
            var col = ultimo.Creadas.First(c => c.Entidad == V1.TipoEntidad.Ejercito).Id;
            var p = new Google.Protobuf.WellKnownTypes.Struct();
            p.Fields["ejercito_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(col);
            yield return Administrar(url, "dar_caravana_fundacion", p);
            yield return Reloj(url, 60_000);
            yield return Bombear(1);
            var cp = s.Almacen.Buscar(V1.TipoEntidad.CampamentoMercenarios, camp)?.CampamentoMercenarios?.Posicion;
            V1.Ejercito Col() => s.Almacen.Buscar(V1.TipoEntidad.Ejercito, col)?.Ejercito;
            var bx = cp?.X ?? Col().Posicion.X; var by = cp?.Y ?? Col().Posicion.Y;
            string plaza = null;
            // Un sitio donde se pueda fundar (el bosque denso no deja): se prueba a un lado y a otro del campamento.
            foreach (var (dx, dy) in new[] { (45, 0), (0, 45), (-45, 0), (0, -45), (60, 40), (-60, 40), (60, -40), (-60, -40), (90, 0), (0, 90), (-90, 0), (0, -90), (120, 60), (-120, 60), (120, -60), (-120, -60) })
            {
                var objetivo = new V1.Punto { X = bx + dx * 1000, Y = by + dy * 1000 };
                // Lejos de las plazas que ya hay (la de otra prueba en este servidor): al pasar por la zona de otra, la columna pierde su caravana.
                if (s.Almacen.Todas(V1.Entidad.CuerpoOneofCase.Asentamiento).Any(e => Math.Abs(e.Asentamiento.Posicion.X - objetivo.X) < 80_000 && Math.Abs(e.Asentamiento.Posicion.Y - objetivo.Y) < 80_000)) continue;
                yield return Ordenar(new V1.Orden { MarcharA = new V1.OrdenMarcharA { HeroeId = heroe, Objetivo = new V1.OrdenMarcharA.Types.Objetivo { Punto = objetivo } } });
                if (ultimo.Resultado != V1.Resultado.OrdenAceptada) continue;
                // La marcha, con el reloj del mundo a saltos de un minuto en vez de esperarla.
                bool Llegada() { var c = Col(); return c != null && Math.Abs(c.Posicion.X - objetivo.X) < 1500 && Math.Abs(c.Posicion.Y - objetivo.Y) < 1500; }
                for (var f = Time.realtimeSinceStartup + 120; Time.realtimeSinceStartup < f && !Llegada();)
                {
                    yield return AvanzarReloj(url, s, 60_000);
                    for (var g = Time.realtimeSinceStartup + 0.3f; Time.realtimeSinceStartup < g && !Llegada();) { s.Bombear(); yield return null; }
                }
                yield return Bombear(1);
                // La caravana dada por la administración va con su columna mientras la columna exista y se pierde cuando se disuelve (p. ej., al
                // entrar el héroe en un asentamiento; motor: nucleo/fundacion.rs). Si en el intento anterior se disolvió, esta ya no la lleva: se le da otra.
                if (string.IsNullOrEmpty(Col()?.CaravanaFundacionId))
                {
                    Debug.Log("[Prueba] fundar: la columna perdió su caravana de fundación; se le da otra");
                    yield return Administrar(url, "dar_caravana_fundacion", p);
                    yield return Bombear(1);
                }
                yield return Ordenar(new V1.Orden { Fundar = new V1.OrdenFundar() });
                if (ultimo.Resultado == V1.Resultado.OrdenAceptada) { plaza = ultimo.Creadas.First(c => c.Entidad == V1.TipoEntidad.Asentamiento).Id; break; }
            }
            Assert.IsNotNull(plaza, "la plaza fundada");
            // Sin auto-construcción desde el principio: con el reloj deprisa, la plaza empezaría obras propias (y la cola, de tope cuatro, se llena).
            yield return Ordenar(new V1.Orden { AlternarAutoConstruccion = new V1.OrdenAlternarAutoConstruccion { AsentamientoId = plaza, Pausada = true } });
            // Recursos de sobra; se repiten antes de cada tanda de obras (la muralla, deprisa con la Vía Rápida, también se los lleva).
            IEnumerator DarRecursos()
            {
                foreach (var (r, n) in new[] { ("madera", 3_000_000L), ("piedra", 2_000_000L), ("oro", 1_000_000L), ("trigo", 2_000_000L), ("cobre", 300_000L), ("estano", 300_000L), ("uva", 200_000L), ("cebada", 200_000L), ("livestock", 50_000L), ("pan", 50_000L), ("carne", 50_000L) })
                {
                    var q = new Google.Protobuf.WellKnownTypes.Struct();
                    q.Fields["asentamiento_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(plaza);
                    q.Fields["recurso"] = Google.Protobuf.WellKnownTypes.Value.ForString(r);
                    q.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(n);
                    yield return Administrar(url, "dar_recursos", q);
                }
            }
            yield return DarRecursos();
            var nv = new Google.Protobuf.WellKnownTypes.Struct();
            nv.Fields["asentamiento_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(plaza);
            nv.Fields["nivel"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(3);
            yield return Administrar(url, "fijar_nivel_asentamiento", nv);
            yield return Bombear(2);
            yield return ConViaRapida(s, plaza, heroe);
            // Un recinto de muralla (empalizada): se levanta celda a celda con el reloj deprisa.
            yield return Ordenar(new V1.Orden { ComprometerRecinto = new V1.OrdenComprometerRecinto { AsentamientoId = plaza, Cargo = "gobernador", Nivel = "1" } });
            // La cola vacía, para poner las obras de la prueba (lo que la proyección aún no traía se quita en la vuelta siguiente).
            for (var f = Time.realtimeSinceStartup + 5; Time.realtimeSinceStartup < f;)
            {
                var enCola = s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plaza).Asentamiento.Edificios.Where(x => x.Estado == V1.EstadoEdificio.EdificioEnCola).ToList();
                if (enCola.Count == 0) break;
                foreach (var e in enCola)
                    yield return Ordenar(new V1.Orden { QuitarDeCola = new V1.OrdenQuitarDeCola { AsentamientoId = plaza, Cargo = "gobernador", EdificioId = e.Id } });
                yield return Bombear(0.5f);
            }
            // Primero lo corto (armería, curtiduría, molino, bodega), que acaba enseguida con el reloj deprisa; luego lo largo (granero, mercado,
            // taberna, almacén...), ya con el reloj a su paso para que se quede en obra o en la cola mientras dura la prueba (con la Vía Rápida, una obra
            // de horas son minutos; la cola tiene tope de cuatro: lo que no cabe, el motor lo rechaza; con `mercadoEnPie`, el Mercado va entre lo corto en lugar de la bodega).
            foreach (var tanda in mercadoEnPie
                ? new[] { new[] { "armeria", "curtiduria", "molino", "mercado" }, new[] { "granero", "bodega", "taberna", "almacen", "fundicion", "carpinteria" } }
                : new[] { new[] { "armeria", "curtiduria", "molino", "bodega" }, new[] { "granero", "mercado", "taberna", "almacen", "fundicion", "carpinteria" } })
            {
                V1.Asentamiento Ella() => s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plaza)?.Asentamiento;
                if (tanda[0] == "granero")
                {
                    // La muralla, entera antes de entrar (a medio levantar, la planta se rehace a cada celda): el reloj del mundo salta de 10 en 10
                    // minutos, con la Vía Rápida renovada, en vez de esperarla.
                    // Sin recinto si el sitio no deja cerrar el anillo: entonces no hay muralla que esperar.
                    bool Hecha() => Ella() == null || Ella().Recintos.All(r => r.Avance >= r.Celdas.Count);
                    int Avance() => Ella()?.Recintos.Sum(r => (int)r.Avance) ?? 0;
                    // La muralla se paga celda a celda del almacén y el motor no levanta una que lo deje por debajo de su reserva: con la madera de
                    // siempre se paraba en 17-22 celdas (madera 20). Antes de empezarla, la madera y la piedra que pide un anillo grande.
                    foreach (var (r, n) in new[] { ("madera", 10_000_000L), ("piedra", 5_000_000L) })
                    {
                        var q = new Google.Protobuf.WellKnownTypes.Struct();
                        q.Fields["asentamiento_id"] = Google.Protobuf.WellKnownTypes.Value.ForString(plaza);
                        q.Fields["recurso"] = Google.Protobuf.WellKnownTypes.Value.ForString(r);
                        q.Fields["cantidad"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(n);
                        yield return Administrar(url, "dar_recursos", q);
                    }
                    for (var f = Time.realtimeSinceStartup + 90; Time.realtimeSinceStartup < f && !Hecha();)
                    {
                        yield return ConViaRapida(s, plaza, heroe);
                        var antes = Avance();
                        // A saltos de 10 minutos: con saltos de una hora la muralla acaba antes, pero el mundo corre días y la plaza se pierde.
                        yield return AvanzarReloj(url, s, 600_000);
                        for (var g = Time.realtimeSinceStartup + 1; Time.realtimeSinceStartup < g && Avance() == antes;) { s.Bombear(); yield return null; }
                    }
                    Debug.Log($"[Prueba] fundar: muralla {string.Join(", ", (Ella()?.Recintos ?? new Google.Protobuf.Collections.RepeatedField<V1.Recinto>()).Select(r => $"{r.Avance} de {r.Celdas.Count}"))} celdas");
                    yield return Reloj(url, 1_000);
                    yield return DarRecursos();
                }
                foreach (var tipo in tanda)
                    yield return Ordenar(new V1.Orden { AnadirEdificioManualmente = new V1.OrdenAnadirEdificioManualmente { AsentamientoId = plaza, Cargo = "gobernador", Tipo = tipo } });
                // Lo corto, hasta que esté en pie; lo largo, hasta que salga en la plaza (en obra o en la cola).
                bool Listo(string tipo) => Ella()?.Edificios.Any(x => x.Tipo == tipo && (tanda[0] == "granero" || x.Estado == V1.EstadoEdificio.EdificioActivo)) == true;
                for (var f = Time.realtimeSinceStartup + 15; Time.realtimeSinceStartup < f && !tanda.All(Listo);)
                {
                    // Lo corto, con el reloj a saltos de 10 minutos; lo largo no se adelanta, para que siga en obra durante la prueba.
                    if (tanda[0] != "granero") yield return AvanzarReloj(url, s, 600_000);
                    for (var g = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < g && !tanda.All(Listo);) { s.Bombear(); yield return null; }
                }
                yield return Bombear(0.5f);
            }
            // Otra vez de sobra (las obras se los llevan), también para reclutar una tropa de pago.
            yield return DarRecursos();
            Debug.Log($"[Prueba] plaza {plaza} fundada por {nick}");
            if (abrirANeutrales)
            {
                // La puerta, abierta a los neutrales (de serie se les cierra): para la visita.
                var puerta = new V1.OrdenFijarPuerta { AsentamientoId = plaza, HeroeId = heroe };
                puerta.CerradaA.Add("enemigos");
                yield return Ordenar(new V1.Orden { FijarPuerta = puerta });
            }
            plazaDe?.Invoke(plaza);
            dondeDe?.Invoke(s.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plaza)?.Asentamiento?.Posicion);
            s.Cerrar();
        }

        [UnityTest, Timeout(900_000)]
        public IEnumerator TuPlazaPorDentro()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            if (Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN") == null) Assert.Ignore("sin BRONZEAGE_PRUEBA_ADMIN: hace falta fundar la plaza");
            string nick = null;
            yield return FundarPlaza(url, x => nick = x);
            foreach (var k in Preferencias) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.DeleteKey("mundo.plaza.barra");
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
            yield return Quieto(1.5f);
            var interior = SelectorDePersonaje.Interior;
            V1.Heroe Yo() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Heroe, conexion.HeroeId).Heroe;
            var plazaId = Yo().Ubicacion.AsentamientoId;
            V1.Asentamiento Plaza() => conexion.Sesion.Almacen.Buscar(V1.TipoEntidad.Asentamiento, plazaId).Asentamiento;
            var heroe = UnityEngine.Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>(FindObjectsSortMode.None).First(x => x.IsLocalHero);
            Debug.Log($"[Prueba] plaza {Plaza().Nombre} nivel {Plaza().Nivel}: " + string.Join(", ", Plaza().Edificios.Select(e => $"{e.Tipo}({e.Estado}{(e.HasNivelInterno ? " N" + e.NivelInterno : "")})")));

            // FeudoScene: la plaza montada desde el motor, un edificio por pieza del motor, con su zona, y la de la puerta.
            var montada = GameObject.Find("Plaza del motor");
            Assert.IsNotNull(montada, "la plaza montada");
            var internos = Plaza().Edificios.Where(e => e.Ambito != V1.AmbitoEdificio.Mapa).ToList();
            foreach (var e in internos) Assert.IsNotNull(GameObject.Find("Zona edificio:" + e.Id), "la zona de " + e.Tipo);
            Assert.IsNotNull(GameObject.Find("Zona puerta"), "la zona de la puerta");
            // La muralla, celda a celda: lo levantado (muro y torres) y lo que aún no (la zanja).
            var celdas = Plaza().Recintos.SelectMany(r => r.Celdas).Count();
            var piezasMuro = montada.GetComponentsInChildren<Transform>().Count(t => t.name == "Muralla" || t.name == "Torre" || t.name == "Muralla (sin levantar)" || t.name == "Puerta de la muralla");
            Debug.Log($"[Prueba] muralla: {Plaza().Recintos.Count} recinto(s), {celdas} celdas ({string.Join(", ", Plaza().Recintos.Select(r => $"nivel {r.Nivel}, {r.Avance} levantadas"))}); {piezasMuro} piezas");
            if (Plaza().Recintos.Count > 0) Assert.AreEqual(celdas, piezasMuro, "una pieza por celda de muralla");
            Assert.IsTrue(Camera.allCameras.Where(x => x.isActiveAndEnabled).All(x => x.gameObject.scene.name == "FeudoScene"), "solo la cámara del interior");
            Assert.IsNull(raiz.Q("interior"), "no es la cabecera del campamento");

            // El HUD: el blasón con su nombre y su nivel, las placas, las obras y la cola, «Salir al mundo» y el riel; sin minimapa todavía.
            var plaza = raiz.Q("plaza");
            Assert.AreEqual(Plaza().Nombre, plaza.Q<Label>("plaza-nombre").text, "el nombre en el blasón");
            // El escudo del blasón es el sigilo de su Facción (paso 8), pintado como textura.
            var escudoBlason = plaza.Q(className: "plaza-blason-escudo");
            Assert.IsTrue(escudoBlason != null && escudoBlason.ClassListContains("sigilo") && escudoBlason.resolvedStyle.backgroundImage.texture != null, "el sigilo en el blasón");
            Assert.AreEqual("3", plaza.Q<Button>("medallon").Q<Label>().text, "el nivel en el medallón");
            foreach (var n in new[] { "placa-poblacion", "placa-almacen", "placa-estado" }) Assert.IsNotNull(plaza.Q(n), n);
            yield return Esperar(() => plaza.Q("obras-hud").Q("cola") != null && !plaza.Q("obras-hud").ClassListContains("oculto"), 20, "las obras y la cola");
            Assert.IsNotNull(plaza.Q<Button>("residencia"), "la casa (resides aquí)");
            Assert.IsNotNull(plaza.Q<Button>("capital"), "la corona (eres el Rey: puedes designarla)");
            Assert.IsNotNull(plaza.Q<Button>("renombrar"), "el lápiz (resides aquí)");
            // El riel, con lo que se puede abrir, en el orden del prototipo: Construcción (eres Gobernador), Gobierno, Defensa, Reclutamiento y la
            // Armería (en pie).
            Assert.IsFalse(plaza.Q("riel").ClassListContains("oculto"), "el riel");
            var riel = new[] { "riel-construccion", "riel-gobierno", "riel-defensa", "riel-territorio", "riel-reclutamiento" }
                .Concat(Plaza().Edificios.Any(e => e.Tipo == "armeria" && e.Estado == V1.EstadoEdificio.EdificioActivo) ? new[] { "riel-armeria" } : new string[0]).ToArray();
            CollectionAssert.AreEqual(riel, plaza.Q("riel").Query<Button>(className: "plaza-riel-boton").ToList().Select(b => b.name).ToArray(), "el riel de la plaza");
            yield return EnTamanos(raiz, "70_plaza");
            yield return CapturarEscena("FeudoScene", "70b_plaza_escena");

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
                IEnumerator Tecla(UnityEngine.InputSystem.Key k)
                {
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
                    yield return null;
                    yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(teclado, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                    yield return null;
                }
                VisualElement Ficha() => plaza.Q("ficha-edificio");

                // Andando hasta un edificio en pie sin otro pegado (a 3,5 m o más: con los edificios juntos, la F es del más cercano), el cartel
                // y F: su ficha al borde derecho, y el héroe quieto.
                bool Lejos(SelectorDePersonaje.PiezaDePlanta p, SelectorDePersonaje.PiezaDePlanta q) =>
                    Math.Max(Math.Abs(p.X - q.X) - (p.Ancho + q.Ancho) / 2, Math.Abs(p.Y - q.Y) - (p.Alto + q.Alto) / 2) >= 3.5;
                // (uno al que haya que andar: no aquel en cuya zona ya está el héroe)
                bool Aqui(SelectorDePersonaje.PiezaDePlanta p) => GameObject.Find("Zona edificio:" + p.Id) is GameObject z && z.GetComponent<BoxCollider>().bounds.SqrDistance(heroe.transform.position) < 36;
                // Si el trazado no deja ninguno a 3,5 m de todos, el que tiene más hueco alrededor (la F es del más cercano: sigue siendo el suyo al llegar).
                double Hueco(SelectorDePersonaje.PiezaDePlanta p) => interior.Edificios.Where(q => q.Id != p.Id)
                    .Select(q => Math.Max(Math.Abs(p.X - q.X) - (p.Ancho + q.Ancho) / 2, Math.Abs(p.Y - q.Y) - (p.Alto + q.Alto) / 2)).DefaultIfEmpty(99).Min();
                var aislado = interior.Edificios.Where(p => p.Estado == null && !Aqui(p)).OrderByDescending(Hueco).First();
                var zona = "edificio:" + aislado.Id;
                var nombreAislado = Edificios.Nombre(aislado.Tipo);
                Debug.Log($"[Prueba] andando hasta {nombreAislado} ({aislado.Id})");
                // `AndarHasta` busca la planta por el nombre del campamento: aquí es la de la plaza.
                montada = GameObject.Find("Plaza del motor");
                montada.name = "Campamento del motor";
                try { yield return AndarHasta(teclado, heroe.transform, zona, () => interior.ZonaCercana == zona); }
                finally { if (montada != null) montada.name = "Plaza del motor"; }
                yield return Esperar(() => plaza.Q<Label>("cartel").text == "F · " + nombreAislado && !plaza.Q<Label>("cartel").ClassListContains("oculto"), 5, "el cartel de " + nombreAislado);
                yield return CapturarEscena("FeudoScene", "71_andando_hasta_un_edificio");
                yield return Capturar("71b_cartel_edificio");
                yield return Tecla(UnityEngine.InputSystem.Key.F);
                yield return Esperar(() => Ficha() != null, 5, "su ficha");
                Assert.IsTrue(interior.PanelAbierto && DialogueUIState.IsDialogueOpen, "con la ficha abierta, el héroe no anda");
                yield return Capturar("71c_ficha_" + aislado.Tipo);
                yield return ClicFijo(raton, raiz, Ficha().Q<Button>("ficha-cerrar"));
                yield return Esperar(() => Ficha() == null && !interior.PanelAbierto, 5, "la ficha cerrada");

                // El molino: el buey (si el almacén lo cubre) y la bodega: su receta. Se abren por la misma puerta que la tecla F (la de la zona).
                var molino = Plaza().Edificios.FirstOrDefault(e => e.Tipo == "molino" && e.Estado == V1.EstadoEdificio.EdificioActivo);
                if (molino != null)
                {
                    interior.AlInteractuar("edificio:" + molino.Id);
                    yield return Esperar(() => Ficha() != null, 5, "la ficha del molino");
                    yield return Capturar("73_ficha_molino");
                    if (Ficha().Q<Button>("comprar-buey") is Button buey)
                    {
                        yield return ClicFijo(raton, raiz, buey);
                        yield return Esperar(() => Plaza().Edificios.First(e => e.Id == molino.Id).Buey, 10, "el buey del molino");
                        yield return Esperar(() => Ficha()?.Q<Button>("comprar-buey") == null, 5, "la ficha con el buey");
                        yield return Capturar("73b_molino_con_buey");
                    }
                    yield return ClicFijo(raton, raiz, Ficha().Q<Button>("ficha-cerrar"));
                    yield return Esperar(() => Ficha() == null, 5, "la ficha cerrada");
                }
                var bodega = Plaza().Edificios.FirstOrDefault(e => e.Tipo == "bodega" && e.Estado == V1.EstadoEdificio.EdificioActivo);
                if (bodega != null)
                {
                    interior.AlInteractuar("edificio:" + bodega.Id);
                    yield return Esperar(() => Ficha() != null, 5, "la ficha de la bodega");
                    yield return Capturar("74_ficha_bodega");
                    var otra = Ficha().Query<Button>().ToList().FirstOrDefault(b => b.name.StartsWith("receta-elegir-"));
                    if (otra != null)
                    {
                        var insumo = otra.name.Substring("receta-elegir-".Length);
                        yield return ClicFijo(raton, raiz, otra);
                        yield return Esperar(() => Plaza().Edificios.First(e => e.Id == bodega.Id).Receta == insumo, 10, "la receta elegida");
                        yield return Capturar("74b_bodega_otra_receta");
                    }
                    yield return ClicFijo(raton, raiz, Ficha().Q<Button>("ficha-cerrar"));
                    yield return Esperar(() => Ficha() == null, 5, "la ficha cerrada");
                }

                // Soltar el ratón con Alt, para el resto del HUD (como en el campamento).
                yield return Tecla(UnityEngine.InputSystem.Key.LeftAlt);

                // La cola: al pasar el ratón por una casilla, ◀ ✕ ▶; ▶ la pasa detrás; ✕ la quita (y la planta 3D se rehace sin ella).
                var cola = Plaza().Edificios.Where(e => e.Estado == V1.EstadoEdificio.EdificioEnCola).OrderByDescending(e => e.Prioridad).ToList();
                Debug.Log($"[Prueba] la cola: {string.Join(", ", cola.Select(e => $"{e.Tipo} p{e.Prioridad}"))}");
                if (cola.Count >= 2)
                {
                    var primera = cola[0];
                    yield return MoverRaton(raton, raiz, plaza.Q("cola-" + primera.Id), () => plaza.Q("cola-" + primera.Id).Q(className: "plaza-obras-ctl").ClassListContains("visible"));
                    yield return Capturar("75_cola_al_pasar");
                    yield return ClicFijo(raton, raiz, plaza.Q<Button>("despues-" + primera.Id));
                    // El motor intercambia las prioridades con la vecina (`mover_en_cola`): con las dos iguales no cambia nada.
                    if (cola[0].Prioridad != cola[1].Prioridad)
                        yield return Esperar(() => Plaza().Edificios.Where(e => e.Estado == V1.EstadoEdificio.EdificioEnCola).OrderByDescending(e => e.Prioridad).First().Id != primera.Id, 10, "la cola reordenada");
                    else
                    {
                        yield return Quieto(1f);
                        Debug.Log($"[Prueba] ▶ con prioridades iguales ({cola[0].Prioridad}): la cola queda {string.Join(", ", Plaza().Edificios.Where(e => e.Estado == V1.EstadoEdificio.EdificioEnCola).Select(e => $"{e.Tipo} p{e.Prioridad}"))}");
                    }
                    var version = interior.Version;
                    yield return MoverRaton(raton, raiz, plaza.Q("cola-" + primera.Id), () => plaza.Q("cola-" + primera.Id).Q(className: "plaza-obras-ctl").ClassListContains("visible"));
                    yield return ClicFijo(raton, raiz, plaza.Q<Button>("quitar-" + primera.Id));
                    yield return Esperar(() => Plaza().Edificios.All(e => e.Id != primera.Id), 10, "quitada de la cola");
                    yield return Esperar(() => interior.Version > version && GameObject.Find("Zona edificio:" + primera.Id) == null, 5, "la planta sin ella");
                }

                // Sin nada más en la cola (la siguiente obra empezaría sola al cancelar), para que la cancelada deje libre su cuadrilla.
                foreach (var e in Plaza().Edificios.Where(x => x.Estado == V1.EstadoEdificio.EdificioEnCola).ToList())
                {
                    var q = conexion.Sesion.Ordenar(new V1.Orden { QuitarDeCola = new V1.OrdenQuitarDeCola { AsentamientoId = plazaId, Cargo = "gobernador", EdificioId = e.Id } });
                    yield return Esperar(() => q.IsCompleted, 10, "quitar de la cola");
                }

                // Cancelar una obra en marcha desde las obras: se confirma y desaparece (no se devuelve nada).
                var enObra = Plaza().Edificios.FirstOrDefault(e => e.Estado == V1.EstadoEdificio.EdificioEnConstruccion && e.Tipo != "curtiduria");
                if (enObra != null)
                {
                    yield return MoverRaton(raton, raiz, plaza.Q("obra-" + enObra.Id));
                    yield return ClicFijo(raton, raiz, plaza.Q<Button>("cancelar-obra-" + enObra.Id));
                    yield return Esperar(() => capaDialogo(raiz) != null, 5, "la confirmación");
                    yield return Capturar("76_cancelar_obra");
                    yield return ClicFijo(raton, raiz, capaDialogo(raiz).Query<Button>().ToList().First(b => b.text.StartsWith("Cancelar la")));
                    yield return Esperar(() => Plaza().Edificios.All(e => e.Id != enObra.Id), 10, "la obra cancelada");
                }

                // La curtiduría (ya en pie): su ficha, por la misma puerta que la tecla F (la de su zona).
                yield return EsperarConReloj(url, conexion.Sesion, () => Plaza().Edificios.Any(e => e.Tipo == "curtiduria" && e.Estado == V1.EstadoEdificio.EdificioActivo), 60, "la curtiduría en pie", 60_000);
                var curtiduria = Plaza().Edificios.First(e => e.Tipo == "curtiduria");
                interior.AlInteractuar("edificio:" + curtiduria.Id);
                yield return Esperar(() => Ficha() != null, 5, "la ficha de la curtiduría");
                yield return EnTamanos(raiz, "72_ficha_curtiduria", () => DentroDelMarco(Ficha(), "la ficha"));
                // Mejorar a N2 con el ratón: la obra sale en la ficha, en las obras y con su cartel sobre el edificio.
                yield return Esperar(() => Ficha()?.Q<Button>("mejorar") != null, 5, "«Mejorar» en la ficha");
                yield return ClicFijo(raton, raiz, Ficha().Q<Button>("mejorar"));
                for (var hasta = Time.realtimeSinceStartup + 10; Time.realtimeSinceStartup < hasta && Plaza().Edificios.First(e => e.Id == curtiduria.Id).Mejora == null;) yield return null;
                Assert.IsNotNull(Plaza().Edificios.First(e => e.Id == curtiduria.Id).Mejora, "la mejora en marcha; el aviso: " + raiz.Q<Label>(className: "toast")?.text);
                yield return Esperar(() => plaza.Q("obra-" + curtiduria.Id) != null, 5, "la mejora en las obras");
                StringAssert.StartsWith("Mejora a nivel 2", plaza.Q("obra-" + curtiduria.Id).Q<Label>(className: "plaza-obras-que").text);
                yield return Esperar(() => Ficha()?.Q<Button>("cancelar-obra") != null, 5, "la ficha con la mejora");
                // El cartel sale encima del edificio si está a la vista de la cámara (que sigue al héroe).
                yield return Esperar(() => raiz.Q<Label>("cartel-obra-" + curtiduria.Id) != null || interior.EnPantalla(curtiduria.Id) == null, 5, "el cartel de la mejora sobre la curtiduría");
                Debug.Log($"[Prueba] cartel de la mejora: {raiz.Q<Label>("cartel-obra-" + curtiduria.Id)?.text ?? "la curtiduría no está a la vista"}");
                yield return Capturar("72b_ficha_mejorando");
                yield return CapturarEscena("FeudoScene", "72c_mejora_en_la_escena");
                yield return ClicFijo(raton, raiz, Ficha().Q<Button>("ficha-cerrar"));
                yield return Esperar(() => Ficha() == null && !interior.PanelAbierto, 5, "la ficha cerrada");


                // El medallón: «Subir de nivel», con sus requisitos (aún no se cumplen: el botón, apagado).
                yield return ClicFijo(raton, raiz, plaza.Q<Button>("medallon"));
                yield return Esperar(() => !plaza.Q("plaza-ventana").ClassListContains("oculto") && plaza.Q("ascenso-tarjeta") != null, 5, "«Subir de nivel»");
                Assert.IsTrue(interior.PanelAbierto, "con la ventana abierta, el héroe no anda");
                var subir = plaza.Q<Button>("subir-nivel");
                Debug.Log($"[Prueba] subir de nivel: {(subir == null ? "sin botón" : subir.enabledSelf ? "se puede" : subir.tooltip)}");
                yield return EnTamanos(raiz, "77_subir_de_nivel", () => DentroDelMarco(plaza.Q("plaza-ventana"), "la ventana"));
                yield return Tecla(UnityEngine.InputSystem.Key.Escape);
                yield return Esperar(() => plaza.Q("plaza-ventana").ClassListContains("oculto"), 5, "Esc cierra la ventana");

                // La casa y la corona: sus tarjetas cuelgan del blasón.
                yield return ClicFijo(raton, raiz, plaza.Q<Button>("residencia"));
                yield return Esperar(() => plaza.Q<Button>("dejar-residencia") != null, 5, "la tarjeta de la residencia");
                yield return Capturar("78_residencia");
                yield return ClicFijo(raton, raiz, plaza.Q<Button>("capital"));
                yield return Esperar(() => plaza.Q<Button>("designar-capital") != null, 5, "la tarjeta de la capital");
                Assert.IsFalse(plaza.Q<Button>("designar-capital").enabledSelf, "sin Palacio no se designa");
                yield return Capturar("78b_capital");
                yield return ClicFijo(raton, raiz, plaza.Q<Button>("capital"));
                yield return Esperar(() => plaza.Q("blason-pop").ClassListContains("oculto"), 5, "la tarjeta recogida");

                // Cambiar el nombre: el lápiz, el nombre nuevo y ✓.
                yield return ClicFijo(raton, raiz, plaza.Q<Button>("renombrar"));
                yield return Esperar(() => plaza.Q<TextField>("nombre-plaza") != null, 5, "el campo del nombre");
                plaza.Q<TextField>("nombre-plaza").value = "Tirinto";
                yield return Capturar("79_renombrar");
                yield return ClicFijo(raton, raiz, plaza.Q<Button>("guardar-nombre"));
                yield return Esperar(() => Plaza().Nombre == "Tirinto", 10, "el nombre nuevo en el motor");
                yield return Esperar(() => plaza.Q<Label>("plaza-nombre")?.text == "Tirinto", 5, "el nombre nuevo en el blasón");

                // ── Tanda 2: Construcción, Gobierno y el Almacén ──
                yield return TandaDos(raton, raiz, plaza, Plaza, Yo, Ficha, Tecla, conexion);
                // ── Tanda 3: Defensa, Reclutamiento y la Armería ──
                yield return TandaTres(raton, raiz, plaza, Plaza, Yo, Ficha, Tecla, conexion);

                // «Salir al mundo»: la ventana de salida de tu residencia y, al salir, el mapa con la columna fuera.
                yield return ClicFijo(raton, raiz, plaza.Q<Button>("salir-al-mundo"));
                yield return Esperar(() => raiz.Q("ventana-salida") != null, 5, "«Salir al mundo»");
                StringAssert.Contains("Tus víveres van siempre contigo", raiz.Q("ventana-salida").Q<Label>(className: "dialogo-texto").text);
                yield return EnTamanos(raiz, "79b_salir_de_la_plaza", () => DentroDelMarco(raiz.Q("ventana-salida").Q(className: "salida-ventana"), "la salida"));
                yield return ClicFijo(raton, raiz, raiz.Q("ventana-salida").Q<Button>("salir"));
                // Si el motor no la deja salir, su motivo sale en la ventana.
                string MotivoDeSalida() => raiz.Q("ventana-salida")?.Query<Label>(className: "error").ToList().FirstOrDefault(l => !l.ClassListContains("oculto") && !string.IsNullOrEmpty(l.text))?.text;
                for (var f = Time.realtimeSinceStartup + 20; Time.realtimeSinceStartup < f && SelectorDePersonaje.Interior != null && MotivoDeSalida() == null;) yield return null;
                Assert.IsNull(MotivoDeSalida(), "el motor no deja salir");
                yield return Esperar(() => SelectorDePersonaje.Interior == null && raiz.Q("plaza") == null, 20, "fuera de la plaza");
                yield return Esperar(() => Yo().Ubicacion?.DondeCase != V1.UbicacionHeroe.DondeOneofCase.AsentamientoId, 10, "el héroe fuera");
                yield return Capturar("79c_fuera");
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

        static VisualElement capaDialogo(VisualElement raiz) => raiz.Query(className: "dialogo-velo").ToList().FirstOrDefault(v => v.Q(className: "dialogo") != null && v.name != "ventana-salida");
    }
}
