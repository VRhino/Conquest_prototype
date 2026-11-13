// Del mapa a la batalla (paso 5A del bloque 4), de punta a punta y con la batalla de verdad:
//   - el motor con las batallas de Unity encendidas (BRONZEAGE_BATALLAS_EN_UNITY=1);
//   - el lanzador del motor (BRONZEAGE_PRUEBA_LANZADOR, con su entorno) y el servidor de batalla de Conquest sin pantalla (PruebaRed.exe);
//   - este editor como cliente.
// Un héroe nuevo con la milicia prestada sale del campamento; en el mapa marcha hasta unos bandidos, ataca (sin lanzador la batalla espera
// servidor) y cancela. Arranca el lanzador, vuelve a atacar, llega la asignación con su token y entra en BattleScene con su héroe y su
// tropa, con la sesión del mundo viva. Va aparte ([Explicit]): necesita el lanzador y el editor sin -batchmode (la red no carga BattleScene
// en batchmode). La corre `Tools/BatallaDesdeElMundo/correr.sh`.
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class PantallasDeMundoTests
    {
        [UnityTest, Explicit("Con el lanzador del motor y el servidor de batalla de verdad: la corre Tools/BatallaDesdeElMundo/correr.sh")]
        public IEnumerator DelMapaALaBatalla()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            var lanzadorExe = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_LANZADOR");
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(lanzadorExe)) Assert.Ignore("sin servidor de prueba o sin lanzador");
            Process lanzador = null;
            try
            {
                // El héroe: nace, le prestan la milicia y sale del campamento con ella (lo de 4B, aquí por órdenes).
                string nick = null;
                yield return OtroHeroe(url, "Bat" + DateTime.UtcNow.Ticks % 1_000_000, "espada_escudo_mediano", "", null, x => nick = x);
                yield return ConSesionDe(url, nick, s =>
                {
                    var o = new OrdenPedirPrestamo();
                    o.TropaIds.Add("milicia_lanceros");
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
                yield return Esperar(() => raiz.Q("mapa") != null && UnityEngine.Object.FindObjectsByType<MapaDeMundo>(FindObjectsInactive.Include).Length == 1, 60, "el mapa");
                var mapa = ElMapa();
                yield return Esperar(() => mapa.Columna != null && mapa.Centro != null, 20, "la columna fuera");
                Heroe Yo() => conexion.Sesion.Almacen.Buscar(TipoEntidad.Heroe, conexion.HeroeId).Heroe;
                Assert.Greater(mapa.Columna.EscuadronIds.Count, 0, "sale con la milicia prestada");

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
                // Que la pantalla de carga haya acabado de irse antes de la captura.
                yield return Esperar(() => raiz.Q("carga") == null, 20, "el fin de la pantalla de carga");
                yield return new WaitForSecondsRealtime(1f);
                yield return Capturar("40_ficha_de_bandidos_con_atacar");

                // Primer ataque, sin lanzador: la batalla espera servidor. Su ficha, y cancelar.
                Pulsar(raiz.Q("ficha-acciones").Query<Button>().ToList().First(b => TextoDe(b) == "Atacar"));
                yield return Esperar(() => raiz.Q("panel-batalla") != null, 15, "la ficha de la batalla");
                yield return Esperar(() => raiz.Q<Label>("batalla-estado")?.text.Contains("CONVOCÁNDOSE") == true || raiz.Q<Label>("batalla-estado")?.text.Contains("A PUNTO") == true, 10, "la batalla esperando servidor");
                Assert.IsNotNull(raiz.Q<Button>("batalla-cancelar"), "quien la inició puede cancelarla antes de que empiece");
                Assert.IsFalse(raiz.Q("ficha-acciones").Query<Button>().ToList().Any(b => TextoDe(b) == "Atacar"), "con la columna en la batalla no se ataca otra vez");
                Debug.Log($"[Prueba] esperando: {raiz.Q<Label>("batalla-ahora").text} · {raiz.Q<Label>("batalla-plazo").text}");
                yield return EnTamanos(raiz, "41_batalla_esperando_servidor");
                Pulsar(raiz.Q<Button>("batalla-cancelar"));
                yield return Esperar(() => raiz.Q<Label>("batalla-estado")?.text.Contains("CANCELADA") == true, 10, "la batalla cancelada");
                Assert.IsNull(raiz.Q<Button>("batalla-cancelar"), "cancelada, ya no se ofrece cancelar");
                Debug.Log($"[Prueba] cancelada: {raiz.Q<Label>("batalla-ahora").text}");
                yield return Capturar("42_batalla_cancelada");
                Pulsar(raiz.Q<Button>("batalla-cerrar"));
                yield return Esperar(() => raiz.Q("ficha-acciones")?.Query<Button>().ToList().Any(b => TextoDe(b) == "Atacar") == true, 15, "«Atacar» otra vez con la columna libre");

                // El lanzador de verdad, y el ataque de verdad: asignación, token y a BattleScene.
                lanzador = ArrancarLanzador(lanzadorExe);
                yield return new WaitForSecondsRealtime(3f);
                // El cliente en red de BattleScene no trae BattleData y su controlador lo dice con un error (es así también con
                // `-red cliente` desde la línea de órdenes; es del hilo de tropas): se admite ese y solo ese, desde antes de atacar (la escena se carga en el mismo fotograma de la entrada).
                const string SinBattleData = "[BattleSceneController] No hay BattleData disponible y no hay TestEnvironmentInitializer";
                var errores = new System.Collections.Generic.List<string>();
                void Registro(string m, string pila, LogType t) { if ((t == LogType.Error || t == LogType.Exception) && m != SinBattleData) errores.Add(m); }
                Application.logMessageReceived += Registro;
                LogAssert.ignoreFailingMessages = true;
                Pulsar(raiz.Q("ficha-acciones").Query<Button>().ToList().First(b => TextoDe(b) == "Atacar"));
                var batallaId = (string)null;
                yield return Esperar(() => (batallaId = PantallasDeMundo.BatallaEnCurso) != null, 150, "la asignación con el token (y la entrada)");
                Debug.Log($"[Prueba] entrando en la batalla {batallaId}");
                yield return Esperar(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "BattleScene", 60, "BattleScene");
                yield return Esperar(() => ConquestTactics.Net.EspejoClienteSystem.CamaraDelJuego, 120, "mi héroe en la batalla (la cámara del juego lo sigue)");
                yield return new WaitForSecondsRealtime(3f);
                var visuales = UnityEngine.Object.FindObjectsByType<ConquestTactics.Visual.EntityVisualSync>(FindObjectsSortMode.None);
                // Mi héroe: la cámara del juego lo sigue (EspejoClienteSystem solo la engancha con héroe propio; el espejo de red no marca
                // IsLocalHero en las figuras). Mi tropa y los bandidos: las figuras de la escena (la milicia prestada son 25).
                Debug.Log($"[Prueba] en la batalla: {visuales.Length} figuras, cámara del juego en mi héroe {ConquestTactics.Net.EspejoClienteSystem.CamaraDelJuego}");
                Assert.Greater(visuales.Length, 1 + 25, "mi héroe, mi tropa y los bandidos");
                Application.logMessageReceived -= Registro;
                LogAssert.ignoreFailingMessages = false;
                Assert.IsEmpty(errores, "al entrar en la batalla, ningún error más que el de BattleData");

                // La sesión del mundo sigue viva: el estado de la batalla llega aquí también.
                Assert.IsTrue(go != null && conexion.Sesion != null && conexion.Sesion.Estado == EstadoSesion.Abierta, "la sesión del mundo sigue abierta");
                var b = conexion.Sesion.Almacen.Buscar(TipoEntidad.Batalla, batallaId)?.Batalla;
                yield return Esperar(() => (b = conexion.Sesion.Almacen.Buscar(TipoEntidad.Batalla, batallaId)?.Batalla)?.Estado == EstadoBatalla.BatallaEnCurso, 60, "la batalla en curso, vista desde la sesión");
                Debug.Log($"[Prueba] desde la sesión: {b.Estado}, columna en batalla {Yo().Ubicacion}");
                // Ya en curso, no se cancela (y con su frase).
                yield return Rechazo(conexion, new Orden { CancelarBatalla = new OrdenCancelarBatalla { BattleId = batallaId } }, "batalla.no_puede_cancelar");
                yield return CapturarPantalla("43_en_la_batalla");
            }
            finally
            {
                if (lanzador != null && !lanzador.HasExited) { try { lanzador.StandardInput.WriteLine("parar"); lanzador.WaitForExit(5000); } catch { } if (!lanzador.HasExited) lanzador.Kill(); }
            }
        }

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

        static IEnumerator OrdenAdmin(string url, Orden orden)
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
