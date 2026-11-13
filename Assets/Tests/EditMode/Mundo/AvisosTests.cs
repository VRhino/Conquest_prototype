// El mecanismo de los avisos: urgentes aparte, los demás agrupados por clave, leídos, guardado y no repetir lo ya avisado al reanudar.
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Tests
{
    public class AvisosTests
    {
        static EventoDominio Mirada(ulong version) => new EventoDominio { Codigo = "columna.observada", Version = version };

        [Test]
        public void LosRepetidosSeFundenYLosUrgentesVanAparte()
        {
            var reloj = 1000L;
            var avisos = new Avisos { AhoraReal = () => reloj };
            EntradaAviso urgente = null;
            avisos.AlUrgente += a => urgente = a;

            avisos.Procesar(Mirada(1), "heroe-1", new Almacen(), null);
            reloj = 5000;
            avisos.Procesar(Mirada(2), "heroe-1", new Almacen(), null);
            Assert.AreEqual(1, avisos.Historial.Count, "la misma mirada se funde en una línea");
            Assert.AreEqual(2, avisos.Historial[0].Veces);
            Assert.AreEqual((1000L, 5000L), (avisos.Historial[0].Desde, avisos.Historial[0].Hasta));
            Assert.IsNull(urgente, "lo informativo no salta en pantalla");

            var hambre = new EventoDominio { Codigo = "ejercito.sin_viveres", Version = 3, ColumnaConHambre = new ColumnaConHambre { EjercitoId = "ej-1", SinViveres = true } };
            avisos.Procesar(hambre, "heroe-1", new Almacen(), null);
            avisos.Procesar(new EventoDominio { Codigo = "ejercito.sin_viveres", Version = 4, ColumnaConHambre = hambre.ColumnaConHambre }, "heroe-1", new Almacen(), null);
            Assert.AreEqual(3, avisos.Historial.Count, "los urgentes no se agrupan");
            Assert.IsNotNull(urgente);
            Assert.IsTrue(avisos.HayPeligroSinLeer);
            Assert.AreEqual(3, avisos.NoLeidos);

            avisos.MarcarLeidos(urgentes: true);
            Assert.AreEqual(1, avisos.NoLeidos);
            avisos.MarcarLeidos(CategoriaAviso.Inteligencia);
            Assert.AreEqual(0, avisos.NoLeidos);
        }

        [Test]
        public void LoYaAvisadoNoSeRepiteTrasGuardarYCargar()
        {
            var avisos = new Avisos();
            avisos.Procesar(Mirada(7), "heroe-1", new Almacen(), null);
            var otra = new Avisos();
            otra.DesdeJson(avisos.AJson());
            Assert.AreEqual(1, otra.Historial.Count);
            Assert.AreEqual(7UL, otra.UltimaVersion);
            otra.Procesar(Mirada(7), "heroe-1", new Almacen(), null);
            Assert.AreEqual(1, otra.Historial[0].Veces, "el hecho de la versión 7 que vuelve al reanudar no se avisa otra vez");
        }

        [Test]
        public void SoloAvisaDeLoQueTeTocaYSabeContar()
        {
            var avisos = new Avisos();
            avisos.Procesar(new EventoDominio { Codigo = "heroe.expulsado", Version = 1, HeroeExpulsado = new HeroeExpulsado { HeroeId = "heroe-2", LugarId = "as-1", Motivo = "conquista" } }, "heroe-1", new Almacen(), null);
            avisos.Procesar(new EventoDominio { Codigo = "obra.terminada", Version = 2 }, "heroe-1", new Almacen(), null);
            Assert.IsEmpty(avisos.Historial);
            avisos.Procesar(new EventoDominio { Codigo = "heroe.expulsado", Version = 3, HeroeExpulsado = new HeroeExpulsado { HeroeId = "heroe-1", LugarId = "as-1", Motivo = "conquista" } }, "heroe-1", new Almacen(), null);
            Assert.AreEqual(1, avisos.Historial.Count);
            Assert.IsTrue(avisos.Historial[0].Urgente);
        }
    }
}
