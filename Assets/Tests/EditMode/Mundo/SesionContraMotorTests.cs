// De punta a punta contra un servidor del motor de verdad: cuenta de prueba, login, `Bienvenida` y fotos, datos de juego, artefacto y relieve
// con caché, crear el héroe en un campamento, salir, marchar (resultado causal, delta y posición al instante), reconectar reanudando y la
// «sesión reemplazada». Solo corre con `BRONZEAGE_PRUEBA_URL` (p. ej. `http://127.0.0.1:7891`) apuntando a un servidor propio: nunca al de
// juego (una sesión por cuenta; la prueba crea cuentas nuevas en él). Cómo levantarlo: `Docs/Arquitectura/23_Conexion_Al_Motor_2026-11-13.md`.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Tests
{
    public class SesionContraMotorTests
    {
        const string Clave = "clave-de-prueba-1";
        string url;

        [SetUp]
        public void Preparar()
        {
            url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
        }

        static T Esperar<T>(Task<T> t, Sesion s = null, int ms = 20_000)
        {
            var reloj = Stopwatch.StartNew();
            while (!t.IsCompleted)
            {
                s?.Bombear();
                if (reloj.ElapsedMilliseconds > ms) Assert.Fail($"sin respuesta en {ms} ms");
                Thread.Sleep(5);
            }
            return t.GetAwaiter().GetResult();
        }

        static void Bombear(Sesion s, int ms, Func<bool> hasta = null)
        {
            var reloj = Stopwatch.StartNew();
            while (reloj.ElapsedMilliseconds < ms && !(hasta?.Invoke() ?? false)) { s.Bombear(); Thread.Sleep(5); }
        }

        static ResultadoOrden Aceptada(Sesion s, Orden o)
        {
            var r = Esperar(s.Ordenar(o), s);
            Assert.AreEqual(Resultado.OrdenAceptada, r.Resultado, $"{o.CuerpoCase}: {r.Rechazo?.Codigo} {Textos.Explicar(r.Rechazo)}");
            return r;
        }

        static string Creada(ResultadoOrden r, TipoEntidad t) => r.Creadas.First(c => c.Entidad == t).Id;

        [Test]
        public void EntrarNacerSalirMarcharYReconectar()
        {
            var acceso = new Acceso(url);
            var nick = "conquest-" + DateTime.UtcNow.Ticks % 100_000_000;
            Esperar(acceso.Registrar(nick, Clave));
            var e = Assert.Throws<ErrorDeAcceso>(() => Esperar(acceso.Registrar(nick, Clave)));
            Assert.AreEqual("nick_repetido", e.Codigo);
            var login = Esperar(acceso.Entrar(nick, Clave));
            Assert.IsNotEmpty(login.Token);

            // 1. La Bienvenida de una cuenta nueva: sin héroe, con los campamentos donde nacer y el reloj.
            var s = new Sesion(acceso.UrlSesion, login.Token, "conquest-prueba");
            var cambios = 0;
            s.AlCambiar += _ => cambios++;
            var b = Esperar(s.Abrir(), s);
            Assert.AreEqual(EstadoSesion.Abierta, s.Estado);
            Assert.IsFalse(b.HasHeroeId);
            Assert.IsNotEmpty(b.CampamentosParaElegir);
            Assert.IsNotEmpty(b.Mundo.Hash);

            // 2. Las reglas, el artefacto y el relieve, con caché: la segunda vez no se baja nada.
            var cache = Path.Combine(Path.GetTempPath(), "conquest-prueba-mundo-" + Guid.NewGuid().ToString("N"));
            try
            {
                var datos = Esperar(DatosDeJuego.Cargar(acceso, b.Mundo, new CacheDeUno(Path.Combine(cache, "datos"))));
                Assert.Greater(datos.VisionJugadorSolo, 0);
                var artefacto = Esperar(MundoEnDisco.CargarArtefacto(acceso, b.Mundo, new CacheDeUno(Path.Combine(cache, "mundo"))), ms: 60_000);
                Assert.IsNotNull(artefacto.Manifiesto);
                Assert.AreEqual(b.Mundo.Hash, new CacheDeUno(Path.Combine(cache, "mundo")).Guardada());
                if (!string.IsNullOrEmpty(b.Mundo.HashRelieve))
                {
                    var relieve = Esperar(MundoEnDisco.CargarRelieve(acceso, b.Mundo, new CacheDeUno(Path.Combine(cache, "relieve"))), ms: 60_000);
                    Assert.AreEqual(b.Mundo.HashRelieve, relieve.Hash);
                }
                var sinRed = new Acceso("http://127.0.0.1:1"); // de la caché, sin servidor
                Assert.IsNotNull(Esperar(MundoEnDisco.CargarArtefacto(sinRed, b.Mundo, new CacheDeUno(Path.Combine(cache, "mundo")))));

                // 3. Nace en un campamento: con el resultado ya están su HEROE y su CAMPAMENTO (respuesta causal).
                var clase = (string)datos.Bloque("Objetos_Del_Heroe", "CLASES_HEROE")["clases"][0]["id"];
                var camp = b.CampamentosParaElegir[0];
                var r = Aceptada(s, new Orden { CrearHeroe = new OrdenCrearHeroe { DisplayName = "P" + nick.Substring(9), CampamentoId = camp.CampamentoId, ClassDefinitionId = clase, Genero = "masculino" } });
                var heroe = Creada(r, TipoEntidad.Heroe);
                Assert.IsTrue(s.Almacen.Ambitos.ContainsKey("HEROE:" + heroe), string.Join(", ", s.Almacen.Ambitos.Keys));
                Assert.IsTrue(s.Almacen.Ambitos.ContainsKey("CAMPAMENTO:" + camp.CampamentoId));
                Assert.AreEqual("P" + nick.Substring(9), Textos.NombreDeHeroe(s.Almacen, heroe));

                // 4. Sale con su columna y marcha: el resultado llega después de la delta que pone el plan.
                var columna = Creada(Aceptada(s, new Orden { SalirDelCampamento = new OrdenSalirDelCampamento { CampamentoId = camp.CampamentoId, HeroeId = heroe } }), TipoEntidad.Ejercito);
                var antes = cambios;
                ResultadoOrden marcha = null;
                foreach (var (dx, dy) in new[] { (8, 0), (-8, 0), (0, 8), (0, -8), (5, 5), (-5, -5) })
                {
                    var destino = new Punto { X = camp.Posicion.X + dx * 1000, Y = camp.Posicion.Y + dy * 1000 };
                    marcha = Esperar(s.Ordenar(new Orden { MarcharA = new OrdenMarcharA { HeroeId = heroe, Objetivo = new OrdenMarcharA.Types.Objetivo { Punto = destino } } }), s);
                    if (marcha.Resultado == Resultado.OrdenAceptada) break;
                }
                Assert.AreEqual(Resultado.OrdenAceptada, marcha.Resultado, marcha.Rechazo?.Codigo);
                Assert.Greater(cambios, antes, "llegó la delta antes que el resultado");
                var ej = s.Almacen.Buscar(TipoEntidad.Ejercito, columna)?.Ejercito;
                Assert.IsNotNull(ej, "su columna está en el almacén");
                Assert.IsNotEmpty(ej.Plan.Vertices, "con su plan");
                var p0 = Instante.Posicion(ej.Plan, ej.Posicion, s.AhoraMundo);
                Bombear(s, 1500);
                var p1 = Instante.Posicion(ej.Plan, ej.Posicion, s.AhoraMundo);
                Assert.IsTrue(p0.enMarcha);
                Assert.Greater(s.TramasZstd, 0, "las fotos grandes llegan en zstd");
                Assert.AreNotEqual((p0.x, p0.y), (p1.x, p1.y), "anda con el reloj del mundo sin que llegue nada");

                // 5. Reconecta con lo que tiene: el HEROE se reanuda.
                s.Cerrar();
                Assert.AreEqual(EstadoSesion.Cerrada, s.Estado);
                var otra = new Sesion(acceso.UrlSesion, login.Token, "conquest-prueba", s.Almacen);
                var b2 = Esperar(otra.Abrir(), otra);
                Assert.AreEqual(heroe, b2.HeroeId);
                Bombear(otra, 3000, () => otra.Almacen.Ambitos.ContainsKey("HEROE:" + heroe));
                Assert.IsTrue(otra.Almacen.Ambitos.ContainsKey("HEROE:" + heroe));

                // 6. Una sesión por cuenta: la que entra después echa a la de antes, que no reconecta sola.
                var login2 = Esperar(acceso.Entrar(nick, Clave));
                var tercera = new Sesion(acceso.UrlSesion, login2.Token, "conquest-prueba");
                Esperar(tercera.Abrir(), tercera);
                Bombear(otra, 5000, () => otra.Estado == EstadoSesion.Cerrada);
                Assert.AreEqual(EstadoSesion.Cerrada, otra.Estado);
                Assert.AreEqual("sesion_reemplazada", otra.UltimoError?.Codigo);
                Bombear(otra, 1500);
                Assert.AreEqual(EstadoSesion.Cerrada, otra.Estado, "no vuelve a entrar sola");

                // 7. Cierra sesión: saca al héroe del mundo y anula el token.
                Aceptada(tercera, new Orden { CerrarSesion = new OrdenCerrarSesion { HeroeId = heroe } });
                tercera.Cerrar();
                acceso.Anular(login.Token);
                acceso.Anular(login2.Token);
            }
            finally
            {
                if (Directory.Exists(cache)) Directory.Delete(cache, true);
            }
        }
    }
}
