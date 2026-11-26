// El generador del campo del lugar contra la mesa (`generarCampo` de `pruebas/campo3d/generador.ts`, BronzeAgeClient) con tres tickets reales
// del motor (`cargo run -p nucleo --example ticket_del_lugar`): el tipo, los spawns, el desnivel y los árboles tienen que salir como allí.
// Las cifras esperadas son las de la mesa (2026-11-26); el puerto es entero y la mesa calcula en `number`, así que se admite un margen pequeño.
// Y la aritmética entera de base: el azar y las manchas, iguales que en la mesa; el seno, el coseno y la exponencial, precisos.
using System;
using System.IO;
using ConquestTactics.Mundo.Campo;
using NUnit.Framework;
using UnityEngine;

namespace ConquestTactics.Mundo.Tests
{
    public class CampoDelLugarTests
    {
        static CampoGenerado Generar(string ticket) =>
            GeneradorDelCampo.Generar(Lugar.DesdeJsonDeLaMesa(File.ReadAllText(Path.Combine(Application.dataPath, "Tests/EditMode/Mundo/Datos", ticket))));

        static void Comparar(CampoGenerado c, TipoDeCampo tipo, bool llanoForzado, double ax, double ay, long giro, double distancia, double desnivel, int arboles, int jugables, int sotos)
        {
            Assert.AreEqual(tipo, c.Informe.Tipo, "tipo");
            Assert.AreEqual(llanoForzado, c.Informe.LlanoForzado, "llano forzado");
            Assert.IsNotNull(c.Spawns, "spawns");
            Assert.AreEqual(ax, c.Spawns.Atacante.X / 1000.0, 0.05, "spawn del atacante, x (m)");
            Assert.AreEqual(ay, c.Spawns.Atacante.Y / 1000.0, 0.05, "spawn del atacante, y (m)");
            Assert.AreEqual(-c.Spawns.Atacante.X, c.Spawns.Defensor.X, "el defensor, enfrente");
            Assert.AreEqual(giro, c.Spawns.Giro, "giro");
            Assert.AreEqual(distancia, c.Spawns.Distancia / 1000.0, 1e-9, "distancia al centro");
            Assert.AreEqual(desnivel, c.Informe.Desnivel / 1000.0, 0.05, "desnivel del área jugable (m)");
            Assert.AreEqual(arboles, c.Informe.Arboles, Math.Max(2, arboles / 1000), "árboles");
            Assert.AreEqual(jugables, c.Informe.ArbolesJugables, Math.Max(2, jugables / 1000), "árboles del área jugable");
            Assert.AreEqual(sotos, c.Informe.Sotos, "sotos");
        }

        [Test]
        public void UnVadoPequenoSaleComoEnLaMesa()
        {
            var c = Generar("ticket_vado.json");
            Comparar(c, TipoDeCampo.Vado, false, -16.67, -274.49, 10, 275, 14.66, 17182, 276, 155);
            Assert.AreEqual(1, c.Vados.Count, "el vado, en el campo");
        }

        [Test]
        public void ElMismoVadoEnGrandeSaleComoEnLaMesa()
        {
            var c = Generar("ticket_vado_grande.json");
            Assert.AreEqual(1_000_000, c.Jugable, "seis héroes: el área jugable grande");
            Comparar(c, TipoDeCampo.Vado, false, -66.32, -445.09, 5, 450, 20.72, 27514, 2024, 209);
        }

        [Test]
        public void PegadoAlRioSinVadoCaeAlLlanoComoEnLaMesa()
        {
            var c = Generar("ticket_pegado_al_rio.json");
            Assert.AreEqual(TipoDeCampo.Ribera, c.Lectura.Tipo, "la lectura, ribera");
            Comparar(c, TipoDeCampo.Llano, true, -74.31, -264.77, 20, 275, 49.76, 8060, 101, 155);
            Assert.IsTrue(c.Informe.Avisos.Exists(a => a.Contains("cae al llano")), "avisa de que cae al llano");
        }

        [Test]
        public void DosVecesElMismoTicketDaElMismoCampo()
        {
            CampoGenerado a = Generar("ticket_vado.json"), b = Generar("ticket_vado.json");
            CollectionAssert.AreEqual(a.Alturas, b.Alturas);
            CollectionAssert.AreEqual(a.Zonas, b.Zonas);
            CollectionAssert.AreEqual(a.Arboles, b.Arboles);
        }

        [Test]
        public void ElAzarYLasManchasSonLosDeLaMesa()
        {
            Assert.AreEqual(2968257788u, Entero.Azar(1, 2, 3));
            Assert.AreEqual(3807884057u, Entero.Azar(-7, 123456, 21));
            // manchas(107331,975 m, −42,5 m, 90 m, 13) en la mesa: 0,42054507…
            Assert.AreEqual(0.4205450721753046, Entero.Manchas(107_331_975, -42_500, 90_000, 13) / 65536.0, 2e-4);
        }

        [Test]
        public void ElSenoElCosenoYLaExponencialSonPrecisos()
        {
            for (int g = -360; g <= 360; g += 7)
            {
                Assert.AreEqual(Math.Sin(g * Math.PI / 180), Entero.Seno(Entero.Grados(g)) / (double)Entero.Uno30, 1e-8, $"seno de {g}°");
                Assert.AreEqual(Math.Cos(g * Math.PI / 180), Entero.Coseno(Entero.Grados(g)) / (double)Entero.Uno30, 1e-8, $"coseno de {g}°");
            }
            Assert.AreEqual(1, Entero.Tangente(45) / (double)Entero.Uno30, 1e-8, "tangente de 45°");
            foreach (double x in new[] { 0.0, 0.3, 1, 2.5, 7 })
                Assert.AreEqual(Math.Exp(-x), Entero.ExpNegativa((long)(x * Entero.Uno)) / (double)Entero.Uno, 1e-4, $"e^-{x}");
        }
    }
}
