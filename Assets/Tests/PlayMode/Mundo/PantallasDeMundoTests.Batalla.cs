// Del mapa a la batalla (paso 5 del bloque 4: la 5A y la tanda 1, la preparación), de punta a punta y con la batalla de verdad:
//   - el motor con las batallas de Unity encendidas (BRONZEAGE_BATALLAS_EN_UNITY=1);
//   - el lanzador del motor (BRONZEAGE_PRUEBA_LANZADOR, con su entorno) y el servidor de batalla de Conquest sin pantalla (PruebaRed.exe);
//   - este editor como cliente.
// Un héroe nuevo (pelo hair_02, sin barba) con dos escuadras prestadas sale del campamento; en el mapa marcha hasta unos bandidos y ataca:
// a la escena de preparación, que conecta con el servidor de batalla en cuanto hay asignación. La cancela (asignada, ya conectado) y vuelve
// a MundoScene con la sesión viva. Ataca otra vez: en la preparación cambia el orden (entra con la segunda), dice «Listo» y entra en
// BattleScene con esa escuadra y su aspecto de verdad. La batalla acaba (por tiempo): el informe, «Salir» al mapa en el sitio de la
// batalla, el informe otra vez desde el aviso, y una segunda batalla que abandona, con su informe. Va aparte
// ([Explicit]): necesita el lanzador y el editor sin -batchmode (la red no carga BattleScene en batchmode). La corre
// `Tools/BatallaDesdeElMundo/correr.sh`.
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using ConquestTactics.Net;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class PantallasDeMundoTests
    {
        [UnityTest, Timeout(900_000), Explicit("Con el lanzador del motor y el servidor de batalla de verdad: la corre Tools/BatallaDesdeElMundo/correr.sh")]
        public IEnumerator DelMapaALaBatalla()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            var lanzadorExe = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_LANZADOR");
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(lanzadorExe)) Assert.Ignore("sin servidor de prueba o sin lanzador");
            Process lanzador = null;
            var errores = new System.Collections.Generic.List<string>();
            // Con la preparación, BattleScene se carga ya conectado: mientras carga (el editor baja a unos fotogramas por segundo) NetCode ve
            // saltar el tick del servidor, lo dice como error y lo corrige solo. Se admite ese, y solo ese.
            void Registro(string m, string pila, LogType t)
            {
                if ((t == LogType.Error || t == LogType.Exception) && !m.StartsWith("Large serverTick prediction error encountered!")) errores.Add(m);
            }
            try
            {
                // El héroe: nace con su aspecto, le prestan dos escuadras y sale del campamento con ellas (lo de 4B, aquí por órdenes).
                var aspecto = new OrdenCrearHeroe.Types.Avatar { CabezaId = "head_03", PeloId = "hair_02", BarbaId = "", CejasId = "eyebrow_01" };
                string nick = null;
                yield return OtroHeroe(url, "Bat" + DateTime.UtcNow.Ticks % 1_000_000, "espada_escudo_mediano", "masculino", aspecto, x => nick = x);
                yield return ConSesionDe(url, nick, s =>
                {
                    var o = new OrdenPedirPrestamo();
                    // Dos levas (escalón 1: 7 + 7 de Liderazgo, lo que tiene un héroe nuevo).
                    o.TropaIds.Add("lenadores");
                    o.TropaIds.Add("granjeros");
                    return new Orden { PedirPrestamo = o };
                });
                yield return ConSesionDe(url, nick, s =>
                {
                    var h = s.Almacen.Todas(Entidad.CuerpoOneofCase.Heroe).Select(e => e.Heroe).First(x => x.Ubicacion?.DondeCase == UbicacionHeroe.DondeOneofCase.CampamentoId);
                    var salir = new OrdenSalirDelCampamento { CampamentoId = h.Ubicacion.CampamentoId, HeroeId = h.Id };
                    salir.EscuadronIds.AddRange(h.EscuadronesIds);
                    return new Orden { SalirDelCampamento = salir };
                });

                // Entra por la interfaz: el selector y, con la columna fuera, el mapa.
                foreach (var k in Preferencias) PlayerPrefs.DeleteKey(k);
                go = new GameObject("Mundo");
                go.SetActive(false);
                var conexion = go.AddComponent<ConexionDeMundo>();
                typeof(ConexionDeMundo).GetField("servidor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(conexion, url);
                var pantallas = go.AddComponent<PantallasDeMundo>();
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
                yield return Esperar(() => raiz.Q("mapa") != null && UnityEngine.Object.FindObjectsByType<MapaDeMundo>(FindObjectsInactive.Include).Length == 1, 60, "el mapa");
                var mapa = ElMapa();
                yield return Esperar(() => mapa.Columna != null && mapa.Centro != null, 20, "la columna fuera");
                Heroe Yo() => conexion.Sesion.Almacen.Buscar(TipoEntidad.Heroe, conexion.HeroeId).Heroe;
                Assert.AreEqual(2, mapa.Columna.EscuadronIds.Count, "sale con las dos escuadras prestadas");

                // Los bandidos más cercanos que no estén junto a un campamento de mercenarios.
                var radioProteccion = conexion.Datos.Numero("documentos.Campamentos_Mercenarios.MERCENARIOS.radioProteccion");
                var mercenarios = mapa.Cosas.Where(x => x.Clase == ClaseDeCosa.Mercenarios).ToList();
                var (cx, cy) = mapa.Centro.Value;
                var bandidos = mapa.Cosas.Where(x => x.Clase == ClaseDeCosa.Bandidos && mercenarios.All(m => CosasDelMapa.Distancia(m.X, m.Y, x.X, x.Y) > radioProteccion * 1.5))
                    .OrderBy(x => CosasDelMapa.Distancia(x.X, x.Y, cx, cy)).FirstOrDefault();
                Assert.IsNotNull(bandidos, "unos bandidos a la vista");
                Debug.Log($"[Prueba] bandidos {bandidos.Id} ({bandidos.Detalle}) a {CosasDelMapa.Distancia(bandidos.X, bandidos.Y, cx, cy):0} u");

                // Rechazos por la orden de verdad: lejos (antes de ir) y un cancelar de una batalla que no existe.
                yield return Rechazo(conexion, new Orden { Atacar = new OrdenAtacar { HeroeId = conexion.HeroeId, Objetivo = new OrdenAtacar.Types.Objetivo { CampamentoId = bandidos.Id } } }, "combate.lejos");
                yield return Rechazo(conexion, new Orden { CancelarBatalla = new OrdenCancelarBatalla { BattleId = Guid.NewGuid().ToString() } }, "batalla.no_existe");

                // A los bandidos: su ficha y «Marchar aquí», con el reloj del mundo deprisa mientras se anda.
                yield return Reloj(url, 30_000);
                mapa.Seleccionar(bandidos);
                yield return Esperar(() => raiz.Q("ficha-acciones")?.Query<Button>().ToList().Any(b => TextoDe(b) == "Marchar aquí") == true, 5, "«Marchar aquí»");
                Pulsar(raiz.Q("ficha-acciones").Query<Button>().ToList().First(b => TextoDe(b) == "Marchar aquí"));
                yield return Esperar(() => raiz.Q("ficha-acciones")?.Query<Button>().ToList().Any(b => TextoDe(b) == "Atacar") == true, 120, "«Atacar» al llegar");
                yield return Reloj(url, 1_000);
                yield return Esperar(() => raiz.Q("carga") == null, 20, "el fin de la pantalla de carga");
                yield return new WaitForSecondsRealtime(1f);
                yield return Capturar("40_ficha_de_bandidos_con_atacar");

                lanzador = ArrancarLanzador(lanzadorExe);
                yield return new WaitForSecondsRealtime(3f);
                Application.logMessageReceived += Registro;
                LogAssert.ignoreFailingMessages = true;

                // Primer ataque: a la escena de preparación, que conecta en cuanto hay servidor. Se cancela ya conectado.
                Pulsar(raiz.Q("ficha-acciones").Query<Button>().ToList().First(b => TextoDe(b) == "Atacar"));
                yield return Esperar(() => SceneManager.GetActiveScene().name == PantallasDeMundo.EscenaPreparacion && raiz.Q("panel-batalla") != null, 30, "la escena de preparación");
                Assert.IsNull(ElMapaSiHay(), "en la preparación no hay mapa");
                yield return Esperar(() => PreparacionRed.Conectado && raiz.Q<Button>("prep-listo")?.enabledSelf == true, 150, "conectado con el servidor de batalla");
                Debug.Log($"[Prueba] preparación 1: {raiz.Q<Label>("batalla-estado").text} · {raiz.Q<Label>("batalla-ahora").text} · {raiz.Q<Label>("prep-queda")?.text} · {raiz.Q<Label>("prep-punto")?.text}");
                Assert.IsNotNull(raiz.Q<Button>("batalla-cancelar"), "asignada, quien la inició aún puede cancelarla");
                yield return EnTamanos(raiz, "41_preparacion", () => Assert.AreEqual(2, raiz.Query(className: "prep-escuadra").ToList().Count, "las dos escuadras en el riel"));
                Pulsar(raiz.Q<Button>("batalla-cancelar"));
                yield return Esperar(() => raiz.Q<Label>("batalla-estado")?.text.ToUpperInvariant().Contains("CANCELADA") == true, 15, "la batalla cancelada");
                Assert.IsFalse(PreparacionRed.Conectado, "cancelada, se deja el servidor de batalla");
                Assert.IsFalse(MundoDe("ClientWorld") != null, "sin mundo de cliente");
                yield return Capturar("42_preparacion_cancelada");
                Pulsar(raiz.Q<Button>("batalla-cerrar"));
                yield return Esperar(() => SceneManager.GetActiveScene().name == PantallasDeMundo.EscenaMundo && raiz.Q("mapa") != null && ElMapaSiHay() != null, 30, "de vuelta en MundoScene con el mapa");
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<PantallasDeMundo>(FindObjectsSortMode.None).Length, "una sola interfaz del mundo (la de la escena se aparta)");
                Assert.AreSame(pantallas, PantallasDeMundo.Viva, "la viva es la de antes, con su sesión");
                Assert.IsTrue(conexion.Sesion != null && conexion.Sesion.Estado == EstadoSesion.Abierta, "la sesión del mundo, abierta todo el rato");
                mapa = ElMapa();
                yield return Esperar(() => mapa.Columna != null && mapa.Centro != null, 20, "la columna en el mapa");
                yield return Esperar(() => raiz.Q("carga") == null, 20, "el fin de la pantalla de carga");
                mapa.Seleccionar(mapa.Cosas.First(x => x.Id == bandidos.Id));
                yield return Esperar(() => raiz.Q("ficha-acciones")?.Query<Button>().ToList().Any(b => TextoDe(b) == "Atacar") == true, 20, "«Atacar» otra vez con la columna libre");
                yield return Capturar("43_de_vuelta_en_el_mapa");

                // Segundo ataque: en la preparación se cambia el orden (entra con la segunda) y «Listo».
                Pulsar(raiz.Q("ficha-acciones").Query<Button>().ToList().First(b => TextoDe(b) == "Atacar"));
                yield return Esperar(() => SceneManager.GetActiveScene().name == PantallasDeMundo.EscenaPreparacion && raiz.Q("panel-batalla") != null, 30, "la preparación otra vez");
                yield return Esperar(() => PreparacionRed.Conectado && raiz.Q<Button>("prep-listo")?.enabledSelf == true, 150, "conectado otra vez");
                var orden = raiz.Query(className: "prep-escuadra").ToList().Select(e => e.name.Substring("prep-escuadra-".Length)).ToList();
                Assert.AreEqual(2, orden.Count);
                Pulsar(raiz.Q<Button>("despues-" + orden[0]));
                yield return null;
                var ahora = raiz.Query(className: "prep-escuadra").ToList().Select(e => e.name.Substring("prep-escuadra-".Length)).ToList();
                CollectionAssert.AreEqual(new[] { orden[1], orden[0] }, ahora, "▶ pasa la primera detrás");
                var b2 = conexion.Sesion.Almacen.Buscar(TipoEntidad.Batalla, MiBatallaId(conexion)).Batalla;
                var elegida = b2.Ticket.Atacante.Participantes.SelectMany(p => p.Escuadras).First(x => x.SquadId == orden[1]);
                Debug.Log($"[Prueba] entra con {elegida.SquadId} ({elegida.TropaId}) en vez de {orden[0]}");
                yield return EnTamanos(raiz, "44_preparacion_orden_cambiado");
                Pulsar(raiz.Q<Button>("prep-listo"));
                var batallaId = (string)null;
                yield return Esperar(() => (batallaId = PantallasDeMundo.BatallaEnCurso) != null, 90, "la salida del servidor (todos listos)");
                yield return Esperar(() => SceneManager.GetActiveScene().name == "BattleScene", 60, "BattleScene");
                yield return Esperar(() => EspejoClienteSystem.CamaraDelJuego, 120, "mi héroe en la batalla (la cámara del juego lo sigue)");
                yield return new WaitForSecondsRealtime(3f);

                // Entra con la escuadra elegida: la que el juego le da como activa (DataContainer, de InterfazRed).
                var cliente = MundoDe("ClientWorld");
                var em = cliente.EntityManager;
                var datos = em.CreateEntityQuery(typeof(DataContainerComponent)).GetSingleton<DataContainerComponent>();
                Assert.AreEqual(elegida.TropaId, datos.selectedSquadBaseID.ToString(), "entra con la escuadra que se puso primera en la preparación");

                // Con su aspecto de verdad: el pelo elegido y sin barba.
                var yo = em.CreateEntityQuery(typeof(Unity.NetCode.NetworkId)).GetSingleton<Unity.NetCode.NetworkId>().Value;
                var heroes = em.CreateEntityQuery(typeof(SoldadoRed), typeof(ConVisual)).ToEntityArray(Unity.Collections.Allocator.Temp);
                var mio = heroes.ToArray().First(e => em.GetComponentData<SoldadoRed>(e).Tipo == SoldadoRed.Heroe && em.GetComponentData<SoldadoRed>(e).Dueno == yo);
                Assert.IsTrue(VisualSyncUtility.TryGetVisual(cliente, mio, out var figura), "la figura de mi héroe");
                SoloLoElegido(figura.transform, aspecto, false, "en la batalla");
                // Los caballos de los héroes (los capitanes de la IA montan): uno que se desplaza montado tiene que llevar el paso en su Animator.
                yield return CaballosAlPaso(cliente, 15);

                var visuales = UnityEngine.Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>(FindObjectsSortMode.None);
                Debug.Log($"[Prueba] en la batalla: {visuales.Length} figuras");
                // Los estandartes (paso 8, tanda 4): el de mi héroe con su sigilo (el del ticket), uno por escuadra y las banderas de las zonas.
                Assert.IsTrue(em.HasComponent<EstandarteComponent>(mio) && em.GetComponentData<EstandarteComponent>(mio).Tipo == EstandarteComponent.DeHeroe
                    && !em.GetComponentData<EstandarteComponent>(mio).Clave.IsEmpty, "el estandarte de mi héroe, con su sigilo");
                yield return Esperar(() => figura.GetComponentsInChildren<Transform>(true).Length > 0 && GameObject.FindObjectsByType<EstandarteVisual>(FindObjectsSortMode.None).Length > 0
                    && UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Estandarte") >= 2, 10, "los estandartes en la batalla");
                var conEstandarte = em.CreateEntityQuery(typeof(EstandarteComponent)).ToComponentDataArray<EstandarteComponent>(Unity.Collections.Allocator.Temp).ToArray();
                Assert.Greater(conEstandarte.Count(x => x.Tipo == EstandarteComponent.DeEscuadra), 0, "un abanderado por escuadra");
                var banderas = UnityEngine.Object.FindObjectsByType<BanderaDeZona>(FindObjectsSortMode.None);
                Debug.Log($"[Prueba] estandartes: {UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Estandarte")} a la vista; "
                    + $"héroe {em.GetComponentData<EstandarteComponent>(mio).Clave}; escuadras {conEstandarte.Count(x => x.Tipo == EstandarteComponent.DeEscuadra)}; "
                    + $"zonas {banderas.Length}; atacante «{EstandartesDeBatalla.Atacante}», defensor «{EstandartesDeBatalla.Defensor}»");
                // Fotos de cerca (para revisarlas): el estandarte de mi héroe y la bandera de una zona, con sus sigilos.
                var carpetaFotos = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
                void Foto(Vector3 centro, Vector3 desde, string nombre)
                {
                    if (string.IsNullOrEmpty(carpetaFotos)) return;
                    var camFoto = new GameObject("foto").AddComponent<Camera>();
                    camFoto.transform.position = desde;
                    camFoto.transform.LookAt(centro);
                    var rtFoto = new RenderTexture(900, 900, 24);
                    camFoto.targetTexture = rtFoto;
                    camFoto.Render();
                    var antesFoto = RenderTexture.active;
                    RenderTexture.active = rtFoto;
                    var texFoto = new Texture2D(900, 900, TextureFormat.RGB24, false);
                    texFoto.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                    RenderTexture.active = antesFoto;
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpetaFotos, nombre + ".png"), texFoto.EncodeToPNG());
                    UnityEngine.Object.Destroy(camFoto.gameObject);
                }
                var mioEstandarte = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "Estandarte")
                    .OrderBy(t => Vector3.Distance(t.position, figura.transform.position)).FirstOrDefault();
                if (mioEstandarte != null)
                {
                    var centro = mioEstandarte.position + Vector3.up * 1.6f;
                    Foto(centro, centro - figura.transform.forward * 3.2f + figura.transform.right * 1.2f + Vector3.up * 0.4f, "45c_estandarte_de_cerca");
                }
                if (banderas.Length > 0 && banderas[0].transform.Find("SM_Prop_Flag_13")?.GetComponent<Renderer>() is Renderer telaZona)
                {
                    var c0 = telaZona.bounds.center;
                    var d0 = Mathf.Max(3f, telaZona.bounds.extents.magnitude * 3f);
                    Foto(c0, c0 + new Vector3(0.35f, 0.15f, -1f).normalized * d0, "45d_bandera_de_zona");
                    Foto(c0, c0 + new Vector3(-0.35f, 0.15f, 1f).normalized * d0, "45e_bandera_de_zona_detras");
                }
                // La opción «Estandarte de las tropas» apagada quita los de las escuadras, no el del héroe ni las banderas de las zonas.
                int Con(byte tipo) => UnityEngine.Object.FindObjectsByType<EstandarteVisual>(FindObjectsSortMode.None).Count(x => x.Mostrado == tipo);
                yield return Esperar(() => Con(EstandarteComponent.DeEscuadra) > 0 && Con(EstandarteComponent.DeHeroe) > 0, 10, "estandartes de héroe y de escuadra");
                var deHeroe = Con(EstandarteComponent.DeHeroe);
                OpcionesDelJuego.Poner(OpcionesDelJuego.EstandarteDeTropas, false);
                yield return Esperar(() => Con(EstandarteComponent.DeEscuadra) == 0, 5, "sin estandartes de escuadra con la opción apagada");
                Assert.AreEqual(deHeroe, Con(EstandarteComponent.DeHeroe), "los de los héroes siguen");
                Assert.AreEqual(banderas.Length, UnityEngine.Object.FindObjectsByType<BanderaDeZona>(FindObjectsSortMode.None).Length, "las banderas de las zonas siguen");
                yield return CapturarPantalla("45b_sin_estandartes_de_tropa");
                OpcionesDelJuego.Poner(OpcionesDelJuego.EstandarteDeTropas, true);
                yield return Esperar(() => Con(EstandarteComponent.DeEscuadra) > 0, 5, "los de las escuadras, otra vez");
                Debug.Log($"[Prueba] opción del estandarte: {deHeroe} de héroe siguen con ella apagada; las de escuadra vuelven al encenderla");
                Application.logMessageReceived -= Registro;
                LogAssert.ignoreFailingMessages = false;
                Assert.IsEmpty(errores, "ningún error en la preparación ni al entrar (tampoco el de BattleData; solo el salto de tick de NetCode al cargar)");

                // La sesión del mundo sigue viva: el estado de la batalla llega aquí también.
                Assert.IsTrue(go != null && conexion.Sesion != null && conexion.Sesion.Estado == EstadoSesion.Abierta, "la sesión del mundo sigue abierta");
                var b = conexion.Sesion.Almacen.Buscar(TipoEntidad.Batalla, batallaId)?.Batalla;
                yield return Esperar(() => (b = conexion.Sesion.Almacen.Buscar(TipoEntidad.Batalla, batallaId)?.Batalla)?.Estado == EstadoBatalla.BatallaEnCurso, 60, "la batalla en curso, vista desde la sesión");
                Debug.Log($"[Prueba] desde la sesión: {b.Estado}, columna en batalla {Yo().Ubicacion}");
                yield return Rechazo(conexion, new Orden { CancelarBatalla = new OrdenCancelarBatalla { BattleId = batallaId } }, "batalla.no_puede_cancelar");
                yield return CapturarPantalla("45_en_la_batalla");

                errores.Clear();
                Application.logMessageReceived += Registro;
                LogAssert.ignoreFailingMessages = true;
                // Tanda 2: la batalla acaba (CONQUEST_DURACION la acorta; por tiempo gana quien defiende). El cartel de Conquest y, a los
                // segundos, el informe en su escena, con «calculando…» hasta que el motor aplica el resultado.
                yield return Esperar(() => ReconexionClienteSystem.Acabada, 120, "el fin de la batalla");
                Debug.Log("[Prueba] fin de la partida: el cartel de Conquest");
                yield return CapturarPantalla("46_fin_de_la_batalla");
                yield return Esperar(() => SceneManager.GetActiveScene().name == PantallasDeMundo.EscenaInforme && raiz.Q("panel-informe") != null, 30, "la escena del informe");
                Assert.IsNull(MundoDe("ClientWorld"), "sin mundo de cliente tras la batalla");
                yield return Esperar(() => raiz.Q<Label>("informe-titulo") != null, 60, "el resultado aplicado por el motor");
                var aplicada = conexion.Sesion.Almacen.Buscar(TipoEntidad.Batalla, batallaId).Batalla;
                Debug.Log($"[Prueba] informe: {raiz.Q<Label>("informe-titulo").text}; gana {aplicada.Resultado.Ganador} por {aplicada.Resultado.Razon}");
                foreach (var s in aplicada.Ticket.Atacante.Participantes.SelectMany(p => p.Escuadras))
                    Assert.IsNotNull(raiz.Q("informe-propias").Q("fila-" + s.SquadId), $"la fila de {s.SquadId} en «Tus escuadras»");
                foreach (var s in aplicada.Ticket.Defensor.EscuadrasSinHeroe)
                    Assert.IsNotNull(raiz.Q("informe-rivales").Q("fila-" + s.SquadId), $"la fila de {s.SquadId} en «Escuadras rivales»");
                Assert.IsNotNull(raiz.Q("informe-heroes"), "los héroes, con si cayeron");
                if (aplicada.Resultado.Ganador != LadoId.Atacante) Assert.IsNotNull(raiz.Q("informe-herido"), "perdiste: quedas herido");
                yield return EnTamanos(raiz, "47_informe", () => SinPisarse(raiz.Q("informe")));

                // El ratón, libre al salir de la batalla (BattleScene lo bloquea), y «Salir» pulsado con el ratón de verdad (Input System).
                Assert.AreEqual(CursorLockMode.None, UnityEngine.Cursor.lockState, "el cursor, sin bloquear en el informe");
                Assert.IsTrue(UnityEngine.Cursor.visible, "el cursor, a la vista en el informe");
                // «Salir»: a MundoScene con la sesión viva, con la columna en el sitio de la batalla.
                yield return ClicUITK(raiz, raiz.Q<Button>("informe-salir"));
                yield return Esperar(() => SceneManager.GetActiveScene().name == PantallasDeMundo.EscenaMundo && ElMapaSiHay() != null, 30, "de vuelta en el mapa tras el informe");
                Assert.IsTrue(UnityEngine.Cursor.lockState == CursorLockMode.None && UnityEngine.Cursor.visible, "el cursor, libre en el mapa al volver");
                mapa = ElMapa();
                yield return Esperar(() => mapa.Columna != null && mapa.Centro != null, 20, "la columna en el mapa");
                var (bx, by) = (aplicada.Punto.X / 1000.0, aplicada.Punto.Y / 1000.0);
                var (mx, my) = mapa.Centro.Value;
                Debug.Log($"[Prueba] columna en ({mx:0.0}, {my:0.0}); la batalla fue en ({bx:0.0}, {by:0.0})");
                Assert.Less(CosasDelMapa.Distancia(mx, my, bx, by), 3.0, "la columna queda donde fue la batalla");
                Assert.IsFalse(mapa.Cosas.Any(x => x.Clase == ClaseDeCosa.Batalla && x.Id == batallaId), "la batalla acabada ya no está en el mapa");
                yield return Esperar(() => raiz.Q("carga") == null, 20, "el fin de la pantalla de carga");

                // El mismo informe desde el aviso del mundo («Ver informe»).
                Pulsar(raiz.Q<Button>("avisos"));
                yield return Esperar(() => raiz.Q<Button>("ver-informe-" + batallaId) != null, 10, "«Ver informe» en los avisos");
                Pulsar(raiz.Q<Button>("ver-informe-" + batallaId));
                yield return Esperar(() => raiz.Q<Label>("informe-titulo") != null, 10, "el informe desde el mundo");
                yield return Capturar("48_informe_desde_el_mundo");
                Pulsar(raiz.Q<Button>("informe-salir"));
                yield return Esperar(() => raiz.Q("panel-informe") == null, 5, "cerrar el informe");

                // Segunda batalla terminada en la misma sesión, esta vez abandonando. Antes, que pase la herida (el reloj del mundo deprisa).
                yield return Reloj(url, 60_000);
                yield return Esperar(() => !Yo().HasHeridoHastaMs || Yo().HeridoHastaMs <= conexion.Sesion.AhoraMundo, 60, "que se cure la herida");
                yield return Reloj(url, 1_000);
                mapa.Seleccionar(mapa.Cosas.First(x => x.Id == bandidos.Id));
                yield return Esperar(() => raiz.Q("ficha-acciones")?.Query<Button>().ToList().Any(b => TextoDe(b) == "Atacar") == true, 20, "«Atacar» para la segunda");
                Pulsar(raiz.Q("ficha-acciones").Query<Button>().ToList().First(b => TextoDe(b) == "Atacar"));
                yield return Esperar(() => PreparacionRed.Conectado && raiz.Q<Button>("prep-listo")?.enabledSelf == true, 150, "la preparación de la segunda");
                Pulsar(raiz.Q<Button>("prep-listo"));
                string segunda = null;
                yield return Esperar(() => (segunda = PantallasDeMundo.BatallaEnCurso) != null && segunda != batallaId, 90, "la segunda batalla");
                yield return Esperar(() => EspejoClienteSystem.CamaraDelJuego, 120, "mi héroe en la segunda batalla");
                yield return Esperar(() => raiz.Q<Button>("batalla-abandonar") != null, 10, "«Abandonar la batalla»");
                yield return CapturarPantalla("49_abandonar_en_la_batalla");
                Pulsar(raiz.Q<Button>("batalla-abandonar"));
                yield return Esperar(() => raiz.Query<Button>().ToList().Any(b => b.text == "Abandonar" && b.ClassListContains("boton-peligro")), 5, "la confirmación");
                Pulsar(raiz.Query<Button>().ToList().First(b => b.text == "Abandonar" && b.ClassListContains("boton-peligro")));
                yield return Esperar(() => SceneManager.GetActiveScene().name == PantallasDeMundo.EscenaInforme && raiz.Q("panel-informe") != null, 30, "el informe tras abandonar");
                Assert.IsTrue(raiz.Q("panel-informe").Query<Label>().ToList().Any(l => l.text.Contains("Abandonaste la batalla")), "dice que abandonaste");
                // Quien abandona deja de combatir: el motor ya no le enseña la batalla (ni su resultado). El informe dice lo suyo.
                yield return Esperar(() => raiz.Q<Label>("informe-titulo") != null && raiz.Q("informe-herido") != null, 90, "el informe del abandono, con la herida");
                Debug.Log($"[Prueba] segunda: {raiz.Q<Label>("informe-titulo").text}");
                yield return Capturar("49b_informe_tras_abandonar");
                Pulsar(raiz.Q<Button>("informe-salir"));
                yield return Esperar(() => SceneManager.GetActiveScene().name == PantallasDeMundo.EscenaMundo && ElMapaSiHay() != null, 30, "de vuelta tras la segunda");
                Assert.AreSame(pantallas, PantallasDeMundo.Viva, "la misma interfaz viva tras dos batallas");
                Assert.IsTrue(conexion.Sesion.Estado == EstadoSesion.Abierta, "la sesión, abierta tras dos batallas");
                Application.logMessageReceived -= Registro;
                LogAssert.ignoreFailingMessages = false;
                Assert.IsEmpty(errores, "ningún error del fin de la batalla a la vuelta (dos veces)");
            }
            finally
            {
                Application.logMessageReceived -= Registro;
                LogAssert.ignoreFailingMessages = false;
                if (lanzador != null && !lanzador.HasExited) { try { lanzador.StandardInput.WriteLine("parar"); lanzador.WaitForExit(5000); } catch { } if (!lanzador.HasExited) lanzador.Kill(); }
            }
        }

        /// <summary>
        /// Durante `segundos`, cada héroe montado que se desplaza (su espejo avanza) tiene que tener el paso del caballo en su Animator
        /// (`MountGaitBlend` > 0): si no, el caballo se desliza tieso. Contra bandidos puede no montar nadie: entonces solo lo dice.
        /// </summary>
        static IEnumerator CaballosAlPaso(World cliente, float segundos)
        {
            var em = cliente.EntityManager;
            var antes = new System.Collections.Generic.Dictionary<Entity, Vector3>();
            int vistos = 0, tiesos = 0;
            string ejemplo = null;
            var pelajes = new System.Collections.Generic.HashSet<string>();
            var fin = Time.realtimeSinceStartup + segundos;
            while (Time.realtimeSinceStartup < fin && vistos < 60)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                using var heroes = em.CreateEntityQuery(typeof(SoldadoRed), typeof(ConVisual), typeof(HeroMountComponent)).ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var e in heroes)
                {
                    if (!em.Exists(e) || em.GetComponentData<HeroMountComponent>(e).state != HeroMountState.Mounted) { antes.Remove(e); continue; }
                    Vector3 donde = em.GetComponentData<Unity.Transforms.LocalTransform>(e).Position;
                    if (antes.TryGetValue(e, out var previo) && (donde - previo).magnitude > 0.25f && VisualSyncUtility.TryGetVisual(cliente, e, out var figura))
                    {
                        var caballo = figura.GetComponentInChildren<ConquestTactics.Visual.HeroHorseVisual>();
                        float paso = caballo != null && caballo.Animator != null ? caballo.Animator.GetFloat(AnimationHashes.MountGaitBlend) : -1f;
                        vistos++;
                        // El pelaje de su montura del motor (paso 6, tanda 2): con montura, el caballo ya no lleva el gris del prefab.
                        var pelaje = em.GetComponentData<HeroMountComponent>(e).coat;
                        var piel = caballo != null ? caballo.GetComponentInChildren<SkinnedMeshRenderer>()?.sharedMaterial?.name : null;
                        pelajes.Add($"{figura.name}: pelaje {pelaje} ({piel})");
                        if (pelaje != 0) Assert.AreNotEqual("Horse_Grey", piel, figura.name + ": el pelaje de su montura");
                        if (paso <= 0.01f) { tiesos++; ejemplo ??= $"{figura.name}: se movió {(donde - previo).magnitude:0.00} m en 0,25 s con paso {paso:0.00} (caballo {(caballo != null ? "sí" : "no")})"; }
                    }
                    antes[e] = donde;
                }
            }
            Debug.Log($"[Prueba] caballos: {vistos} muestras de héroes montados andando, {tiesos} sin paso. {ejemplo} Pelajes: {string.Join("; ", pelajes)}");
            Assert.AreEqual(0, tiesos, "un caballo que anda lleva el paso en su Animator: " + ejemplo);
        }

        /// <summary>
        /// Un clic con el ratón de verdad (un ratón del Input System) en un elemento de UI Toolkit: el panel pinta un momento en la pantalla
        /// (la prueba lo tiene en una textura), el puntero va al centro del elemento y hace clic. Así se caza un ratón bloqueado o un panel
        /// que no recibe el puntero, que `Pulsar` (por dentro) no ve.
        /// </summary>
        /// <summary>La ventana del editor, al frente: sin el foco, el ratón de la prueba no llega al panel (la batalla corre sin -batchmode, en el
        /// escritorio de quien use el PC).</summary>
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

        IEnumerator ClicUITK(VisualElement raiz, VisualElement e)
        {
            Assert.IsNotNull(e, "el elemento del clic");
            var doc = go.GetComponentInChildren<UIDocument>();
            var textura = doc.panelSettings.targetTexture;
            var entrada = UnityEngine.InputSystem.InputSystem.settings;
            var fondoAntes = entrada.backgroundBehavior;
            var editorAntes = entrada.editorInputBehaviorInPlayMode;
            entrada.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            entrada.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var raton = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor) SetForegroundWindow(System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle);
                raton.MakeCurrent();
                doc.panelSettings.targetTexture = null;
                yield return null;
                yield return null;
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
                UnityEngine.InputSystem.InputSystem.RemoveDevice(raton);
                entrada.backgroundBehavior = fondoAntes;
                entrada.editorInputBehaviorInPlayMode = editorAntes;
                if (doc != null) doc.panelSettings.targetTexture = textura;
            }
        }

        /// <summary>Los bloques de cada columna del informe (y los recuadros de abajo), uno debajo de otro sin pisarse y con su alto.</summary>
        static void SinPisarse(VisualElement informe)
        {
            foreach (var grupo in informe.Query(className: "informe-columna").ToList().Concat(informe.Query(className: "informe-recuadros").ToList()))
            {
                var hijos = grupo.Children().Where(h => h.resolvedStyle.display != DisplayStyle.None).ToList();
                foreach (var h in hijos)
                {
                    Assert.Greater(h.worldBound.height, 4f, $"«{h.name ?? h.GetType().Name}» del informe, con su alto");
                    // Y lo de dentro, dentro: un recuadro aplastado deja sus textos fuera, encima de lo siguiente.
                    foreach (var l in h.Query<Label>().ToList().Where(l => l.resolvedStyle.display != DisplayStyle.None && l.worldBound.height > 0))
                        Assert.IsTrue(Contiene(h.worldBound, l.worldBound), $"«{l.text}» dentro de su bloque del informe ({l.worldBound} en {h.worldBound})");
                }
                for (int i = 0; i < hijos.Count; i++)
                    for (int j = i + 1; j < hijos.Count; j++)
                    {
                        var a = hijos[i].worldBound; var b = hijos[j].worldBound;
                        var dentro = new Rect(a.x + 0.5f, a.y + 0.5f, a.width - 1f, a.height - 1f);
                        Assert.IsFalse(dentro.Overlaps(b), $"en el informe, «{hijos[i].name ?? hijos[i].GetType().Name}» y «{hijos[j].name ?? hijos[j].GetType().Name}» se pisan ({a} y {b})");
                    }
            }
        }

        static MapaDeMundo ElMapaSiHay() => UnityEngine.Object.FindAnyObjectByType<MapaDeMundo>();

        static World MundoDe(string nombre)
        {
            foreach (var w in World.All) if (w.IsCreated && w.Name == nombre) return w;
            return null;
        }

        /// <summary>La batalla activa del héroe en el almacén de la sesión.</summary>
        static string MiBatallaId(ConexionDeMundo conexion) =>
            conexion.Sesion.Almacen.Todas(Entidad.CuerpoOneofCase.Batalla).Select(e => e.Batalla)
                .Where(x => x.HasLadoPropio && (x.Estado == EstadoBatalla.BatallaConvocando || x.Estado == EstadoBatalla.BatallaAsignada || x.Estado == EstadoBatalla.BatallaEnCurso))
                .OrderByDescending(x => x.Version).First().Id;

        /// <summary>El lanzador del motor (su entorno ya puesto por `Tools/BatallaDesdeElMundo/correr.sh`), con su registro en Logs/mundo.</summary>
        static Process ArrancarLanzador(string exe)
        {
            var info = new ProcessStartInfo(exe) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            var p = Process.Start(info);
            var registro = System.IO.Path.Combine(Application.dataPath, "..", "Logs", "mundo", "lanzador.log");
            var escritor = new System.IO.StreamWriter(registro) { AutoFlush = true };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (escritor) escritor.WriteLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (escritor) escritor.WriteLine(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            Debug.Log($"[Prueba] lanzador {p.Id}");
            return p;
        }

        /// <summary>Una orden desde la sesión del héroe en pantalla, que tiene que rechazarse con `codigo` y una frase propia (no el código crudo).</summary>
        static IEnumerator Rechazo(ConexionDeMundo conexion, Orden orden, string codigo)
        {
            var r = conexion.Sesion.Ordenar(orden);
            yield return Esperar(() => r.IsCompleted, 20, "la respuesta a " + orden.CuerpoCase);
            Assert.AreEqual(Resultado.OrdenRechazada, r.Result.Resultado, $"{orden.CuerpoCase} tenía que rechazarse");
            Assert.AreEqual(codigo, r.Result.Rechazo.Codigo, $"el rechazo de {orden.CuerpoCase}");
            var frase = Textos.Explicar(r.Result.Rechazo);
            Assert.AreNotEqual(codigo, frase, "el rechazo con su frase");
            Debug.Log($"[Prueba] rechazo {codigo}: {frase}");
        }

        /// <summary>Abre una sesión con la cuenta `nick`, espera al héroe y manda la orden que construye `orden` con lo que ya hay en el almacén.</summary>
        static IEnumerator ConSesionDe(string url, string nick, Func<Sesion, Orden> orden)
        {
            var acceso = new Acceso(url);
            var login = acceso.Entrar(nick, "clave-de-prueba-1");
            yield return Esperar(() => login.IsCompleted, 20, "el login");
            var s = new Sesion(acceso.UrlSesion, login.Result.Token, "conquest-prueba");
            var abrir = s.Abrir();
            var fin = Time.realtimeSinceStartup + 20;
            while (!abrir.IsCompleted && Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
            fin = Time.realtimeSinceStartup + 3;
            while (Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
            var r = s.Ordenar(orden(s));
            fin = Time.realtimeSinceStartup + 20;
            while (!r.IsCompleted && Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
            Assert.AreEqual(Resultado.OrdenAceptada, r.Result.Resultado, r.Result.Rechazo?.ToString());
            fin = Time.realtimeSinceStartup + 1;
            while (Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
            s.Cerrar();
        }

        /// <summary>El factor del reloj del mundo (en milésimas: 1000 = tiempo real), desde la sesión de administración.</summary>
        static IEnumerator Reloj(string url, long factorMilesimas)
        {
            yield return OrdenAdmin(url, new Orden { ControlarReloj = new OrdenControlarReloj { Accion = "fijar_factor", FactorMilesimas = factorMilesimas } });
        }

        /// <summary>Adelanta el reloj del mundo `ms` de golpe (lo que tocaba en ese rato pasa al momento), desde la sesión de administración.</summary>
        /// La sesión se entera del salto en su siguiente ping: hasta entonces su reloj va por detrás, así que se cuenta desde el último salto si va
        /// más adelante. Si aun así el mundo ya pasó de ahí, el motor contesta «sin cambios», y vale.
        static IEnumerator AvanzarReloj(string url, Sesion s, long ms)
        {
            ultimoSalto = Math.Max(ultimoSalto, (long)s.AhoraMundo) + ms;
            yield return OrdenAdmin(url, new Orden { ControlarReloj = new OrdenControlarReloj { Accion = "avanzar_hasta", HastaMs = ultimoSalto } }, sinCambiosVale: true);
        }
        static long ultimoSalto;

        static IEnumerator OrdenAdmin(string url, Orden orden, bool sinCambiosVale = false)
        {
            var codigo = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_ADMIN");
            if (string.IsNullOrEmpty(codigo)) { Debug.Log("[Prueba] sin BRONZEAGE_PRUEBA_ADMIN: no se manda " + orden.CuerpoCase); yield break; }
            var acceso = new Acceso(url);
            if (tokenAdmin == null)
            {
                var nick = "admin-" + DateTime.UtcNow.Ticks % 100_000_000;
                var registro = acceso.Registrar(nick, "clave-de-prueba-1", codigo);
                yield return Esperar(() => registro.IsCompleted, 20, "la cuenta admin");
                var login = acceso.Entrar(nick, "clave-de-prueba-1");
                yield return Esperar(() => login.IsCompleted, 20, "el login admin");
                tokenAdmin = login.Result.Token;
            }
            var s = new Sesion(acceso.UrlSesion, tokenAdmin, "conquest-prueba") { Tipo = TipoCliente.ClienteAdminWeb };
            var abrir = s.Abrir();
            var fin = Time.realtimeSinceStartup + 20;
            while (!abrir.IsCompleted && Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
            var r = s.Ordenar(orden);
            fin = Time.realtimeSinceStartup + 20;
            while (!r.IsCompleted && Time.realtimeSinceStartup < fin) { s.Bombear(); yield return null; }
            if (!(sinCambiosVale && r.Result.Resultado == Resultado.OrdenSinCambios))
                Assert.AreEqual(Resultado.OrdenAceptada, r.Result.Resultado, r.Result.Rechazo?.ToString());
            s.Cerrar();
        }

        /// <summary>Una captura de la pantalla entera (BattleScene, con su UGUI), a la carpeta de capturas.</summary>
        IEnumerator CapturarPantalla(string nombre)
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            if (string.IsNullOrEmpty(carpeta)) yield break;
            yield return new WaitForEndOfFrame();
            var t = ScreenCapture.CaptureScreenshotAsTexture();
            System.IO.Directory.CreateDirectory(carpeta);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
        }
    }
}
