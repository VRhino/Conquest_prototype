// La matemática del mapa de mundo: la cámara (encuadre, zooms, inclinación), el rayo del clic contra el relieve y el decodificado de una tesela
// del paquete de relieve (predicción y zstd), sin Unity.
using System;
using BronzeAge.Contract.V1;
using Google.Protobuf;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Tests
{
    public class MapaTests
    {
        [Test]
        public void LosEncuadresSiguenALaVisionDeLaColumna()
        {
            Assert.AreEqual((15d, 22d), CamaraDelMapa.Encuadres(15), "héroe solo: 15 y 22 (BronzeAgeClient)");
            Assert.AreEqual((30d, 45d), CamaraDelMapa.Encuadres(30), "con soldados: 30 y 45");
            var c = new CamaraDelMapa { RadioVision = 15 };
            Assert.AreEqual(15, c.EnPantalla);
            Assert.IsTrue(c.PasoDeZoom(-1));
            Assert.AreEqual(9, c.EnPantalla, 1e-9, "de cerca, 0,6 del normal");
            Assert.IsFalse(c.PasoDeZoom(-1), "no hay más cerca");
            c.PasoDeZoom(1);
            c.PasoDeZoom(1);
            Assert.AreEqual(22, c.EnPantalla);
            Assert.IsFalse(c.PasoDeZoom(1));
            c.Inclinar(-100);
            Assert.AreEqual(10, c.Inclinacion);
            c.Inclinar(500);
            Assert.AreEqual(90, c.Inclinacion);
        }

        [Test]
        public void DesdeArribaElCirculoCabeEnElLadoCorto()
        {
            double tg = Math.Tan(35 * Math.PI / 360);
            // Mirando en vertical, un círculo de radio r cabe justo cuando r / d = tg (pantalla más ancha que alta).
            var d = CamaraDelMapa.DistanciaDeEncuadre(10, Math.PI / 2 - 1e-9, tg, 16 / 9.0);
            Assert.AreEqual(10 / tg, d, 0.05);
            // Inclinada a 30°, el círculo se ve achatado en vertical.
            Assert.Less(CamaraDelMapa.DistanciaDeEncuadre(10, 30 * Math.PI / 180, tg, 16 / 9.0), d, "inclinada, el círculo se ve achatado y cabe más cerca");
        }

        [Test]
        public void LaCamaraNoSeMeteEnLaLadera()
        {
            var c = new CamaraDelMapa { RadioVision = 15, Inclinacion = 10 };
            var llano = c.Colocar((100, 0, 100), 16 / 9.0, (x, y) => 0, 0);
            Assert.Greater(llano.h, 0);
            var monte = c.Colocar((100, 0, 100), 16 / 9.0, (x, y) => y > 100 ? 500 : 0, 0);
            Assert.GreaterOrEqual(monte.h, 500.15 - 1e-9, "detrás del héroe hay un monte: la cámara sube sobre él");
        }

        [Test]
        public void ElClicTocaElRelieveDondeCorresponde()
        {
            // Llano a altura 2: un rayo a 45° desde (0, 12, 0) hacia +x toca en x = 10.
            var p = CamaraDelMapa.TocarRelieve((0, 12, 0), (1, -1, 0), (x, y) => 2);
            Assert.IsNotNull(p);
            Assert.AreEqual(10, p.Value.x, 1e-3);
            Assert.AreEqual(0, p.Value.y, 1e-6);
            // Un muro de altura 20 a partir de x = 5: lo toca antes.
            var muro = CamaraDelMapa.TocarRelieve((0, 12, 0), (1, -1, 0), (x, y) => x >= 5 ? 20 : 2);
            Assert.AreEqual(5, muro.Value.x, 1e-3);
            Assert.IsNull(CamaraDelMapa.TocarRelieve((0, 12, 0), (1, 1, 0), (x, y) => 2), "mirando al cielo no toca nada");
        }

        [Test]
        public void LaTeselaDeRelieveDeshaceLaPrediccion()
        {
            const int lado = 2, n = lado + 1;
            var alturas = new ushort[] { 100, 200, 300, 150, 65535, 10, 0, 7, 40000 };
            var crudo = new byte[n * n * 2];
            for (int j = 0, k = 0; j < n; j++)
                for (int i = 0; i < n; i++, k++)
                {
                    int prediccion = i > 0 && j > 0 ? alturas[k - 1] + alturas[k - n] - alturas[k - n - 1] : i > 0 ? alturas[k - 1] : j > 0 ? alturas[k - n] : 0;
                    var resto = (ushort)((alturas[k] - prediccion) & 0xffff);
                    crudo[2 * k] = (byte)resto;
                    crudo[2 * k + 1] = (byte)(resto >> 8);
                }
            var agua = new byte[] { 0b0001_0001, 0b0000_0001 }; // muestras 0, 4 y 8
            using (var z = new ZstdSharp.Compressor())
            {
                var t = new TeselaRelieve { Altura = ByteString.CopyFrom(z.Wrap(crudo).ToArray()), Agua = ByteString.CopyFrom(z.Wrap(agua).ToArray()) };
                var abierta = Relieve.Abrir(t, lado, new ZstdSharp.Decompressor());
                CollectionAssert.AreEqual(alturas, abierta.Altura);
                CollectionAssert.AreEqual(new[] { true, false, false, false, true, false, false, false, true }, abierta.Agua);
            }
        }
    }
}
