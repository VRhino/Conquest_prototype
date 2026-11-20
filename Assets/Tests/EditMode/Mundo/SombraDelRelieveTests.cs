// La sombra del relieve: un muro da una sombra de h / tan(elevación), el fondo de un pozo estrecho queda a la sombra y el de uno ancho no, el
// llano siempre al sol y dos teselas vecinas dan lo mismo en su borde.
using System;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Tests
{
    public class SombraDelRelieveTests
    {
        const double E = 12; // metros por unidad
        static readonly double TanElev = Math.Tan(56 * Math.PI / 180);

        [Test]
        public void UnMuroDaUnaSombraDeSuAltoEntreLaTangenteDeLaElevacion()
        {
            // Muro de 120 m en x = 20..21 u, el sol hacia +x: la sombra llega a 120 / (12 · tan 56°) = 6,74 u por delante del muro.
            double Altura(double x, double y) => x >= 20 && x < 21 ? 120 : 0;
            double largo = 120 / (E * TanElev);
            Assert.AreEqual(0, SombraDelRelieve.Visibilidad(Altura, 20 - largo * 0.5, 0, 1, 0, TanElev, E), 1e-3, "a media sombra del muro");
            Assert.AreEqual(1, SombraDelRelieve.Visibilidad(Altura, 20 - largo * 2, 0, 1, 0, TanElev, E), 1e-3, "al doble del largo, al sol");
            Assert.AreEqual(0.5, SombraDelRelieve.Visibilidad(Altura, 20 - largo, 0, 1, 0, TanElev, E), 0.3, "en el borde, la penumbra");
            Assert.AreEqual(1, SombraDelRelieve.Visibilidad(Altura, 22, 0, 1, 0, TanElev, E), 1e-3, "detrás del muro (de espaldas al sol), al sol");
        }

        [Test]
        public void ElFondoDeUnPozoEstrechoQuedaALaSombraYElDeUnoAnchoNo()
        {
            Func<double, double, double> Pozo(double radio) => (x, y) => x * x + y * y < radio * radio ? -60 : 0;
            Assert.AreEqual(0, SombraDelRelieve.Visibilidad(Pozo(2), 0, 0, 1, 0, TanElev, E), 1e-3, "pozo de 2 u y 60 m");
            Assert.AreEqual(1, SombraDelRelieve.Visibilidad(Pozo(10), 0, 0, 1, 0, TanElev, E), 1e-3, "pozo de 10 u y 60 m");
        }

        [Test]
        public void ElLlanoSiempreEstaAlSol()
        {
            Assert.AreEqual(1, SombraDelRelieve.Visibilidad((x, y) => 350, 5, 7, 0.6, -0.8, TanElev, E), 1e-6);
            Assert.AreEqual(1, SombraDelRelieve.Visibilidad((x, y) => 350, 5, 7, 0.6, -0.8, Math.Tan(10 * Math.PI / 180), E), 1e-6, "con el sol bajo");
        }

        [Test]
        public void DosTeselasVecinasDanLoMismoEnSuBorde()
        {
            // Un relieve de ondas: la rejilla de la tesela (0, 0) y la de la (1, 0) comparten la columna x = 64 u.
            double Altura(double x, double y) => 400 + 300 * Math.Sin(x * 0.3) * Math.Cos(y * 0.25);
            var (dx, dy, tan) = SombraDelRelieve.DelSol(-60, 110, -45);
            int n = (int)(64 / SombraDelRelieve.PasoRejilla) + 1;
            var a = SombraDelRelieve.Rejilla(Altura, 0, 0, n, SombraDelRelieve.PasoRejilla, dx, dy, tan, E);
            var b = SombraDelRelieve.Rejilla(Altura, 64, 0, n, SombraDelRelieve.PasoRejilla, dx, dy, tan, E);
            bool haySombra = false;
            for (int j = 0; j < n; j++)
            {
                Assert.AreEqual(a[j * n + n - 1], b[j * n], 1e-6, $"fila {j}");
                Assert.AreEqual(SombraDelRelieve.En(a, n, SombraDelRelieve.PasoRejilla, 64, j * 2.0), SombraDelRelieve.En(b, n, SombraDelRelieve.PasoRejilla, 0, j * 2.0), 1e-6);
                haySombra |= a[j * n + n - 1] < 0.5f;
            }
            Assert.IsTrue(haySombra || Array.Exists(a, v => v < 0.5f), "el relieve de ondas da alguna sombra");
        }

        [Test]
        public void ElSolDelPrototipoMiraAlNoroesteConUnos56Grados()
        {
            var (dx, dy, tan) = SombraDelRelieve.DelSol(-60, 110, -45);
            Assert.Less(dx, 0, "hacia el oeste");
            Assert.Greater(dy, 0, "hacia y creciente del mapa (z = −y)");
            Assert.AreEqual(55.7, Math.Atan(tan) * 180 / Math.PI, 0.5);
        }
    }
}
