// El mosaico de la vegetación (`MosaicoDeVegetacion`, wave function collapse por reglas de vecindad) sobre un mundo de mentira con bosques,
// lindes, un lago y una ladera: la misma semilla da el mismo mosaico; en un lote de teselas no hay ninguna vecindad prohibida en el interior ni
// dos especies tocándose sin celda mixta; lo que mandan el motor y el prototipo no se viola nunca; el anillo del borde sale igual visto desde
// dos teselas vecinas; y, sin solución, cae al reparto del prototipo (null).
using System;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Tests
{
    public class MosaicoDeVegetacionTests
    {
        const double Costa = 420, Colina = 545, Montana = 600;
        const int N = 32;
        static readonly TablaDelMosaico Tabla = new TablaDelMosaico();

        static double Densidad(double x, double y) => Math.Max(0, Math.Min(1, 0.45 + 0.6 * Math.Sin(x / 17) * Math.Cos(y / 13) + 0.2 * Math.Sin((x + y) / 7)));
        static bool Lago(double x, double y) => (x - 150) * (x - 150) + (y - 40) * (y - 40) < 14 * 14;
        static double Altura(double x, double y) => 450 + 80 * Math.Max(0, (y - 90) / 40);
        static double Pendiente(double x, double y) => y > 100 && y < 120 && x > 60 && x < 90 ? 1.3 : 0.15;

        static DatosDeCelda Celda(double x, double y) => new DatosDeCelda
        {
            X = x, Y = y,
            Densidad = Densidad(x, y),
            Fertilidad = 0.3 + 0.4 * ReglasDeVegetacion.Manchas(x, y, 20, 3),
            Pendiente = Pendiente(x, y),
            DAgua = Math.Max(0, Math.Sqrt((x - 150) * (x - 150) + (y - 40) * (y - 40)) - 14),
            Altura = Altura(x, y),
            Vacia = Lago(x, y),
            AguaAlLado = !Lago(x, y) && (Lago(x + 2, y) || Lago(x - 2, y) || Lago(x, y + 2) || Lago(x, y - 2)),
        };

        static DatosDeCelda[] Tesela(int tx, int ty)
        {
            var d = new DatosDeCelda[N * N];
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                    d[j * N + i] = Celda(tx * 64 + (i + 0.5) * 2, ty * 64 + (j + 0.5) * 2);
            return d;
        }

        static bool EnElAnillo(int k) { int i = k % N, j = k / N; return i == 0 || j == 0 || i == N - 1 || j == N - 1; }

        [Test]
        public void LaMismaSemillaDaElMismoMosaico()
        {
            var d = Tesela(1, 0);
            var a = MosaicoDeVegetacion.Estructura(d, N, 1234, Colina, Tabla, out _);
            var b = MosaicoDeVegetacion.Estructura(d, N, 1234, Colina, Tabla, out _);
            Assert.IsNotNull(a);
            CollectionAssert.AreEqual(a, b);
            var c = MosaicoDeVegetacion.Estructura(d, N, 999, Colina, Tabla, out _);
            Assert.IsNotNull(c);
            CollectionAssert.AreNotEqual(a, c, "otra semilla, otro mosaico");
        }

        [Test]
        public void NingunaVecindadProhibidaNiReglaDelMotorRota()
        {
            int teselas = 0, celdas = 0;
            for (int ty = 0; ty < 3; ty++)
                for (int tx = 0; tx < 4; tx++)
                {
                    var d = Tesela(tx, ty);
                    var e = MosaicoDeVegetacion.Estructura(d, N, tx * 31 + ty * 7, Colina, Tabla, out _);
                    if (e == null) continue; // cayó al prototipo: no hay mosaico que comprobar
                    teselas++;
                    for (int k = 0; k < N * N; k++)
                    {
                        // lo que mandan el motor y el prototipo
                        Assert.AreNotEqual(0, MosaicoDeVegetacion.Admitidos(d[k], Colina) & (1 << (int)e[k]), $"({tx}, {ty}) celda {k}: {e[k]} no admitido");
                        if (d[k].Densidad < ReglasDeVegetacion.DensidadMinima)
                            Assert.That(e[k], Is.Not.EqualTo(EstadoDeCelda.Cerrado).And.Not.EqualTo(EstadoDeCelda.Abierto).And.Not.EqualTo(EstadoDeCelda.Linde));
                        if (d[k].Densidad >= 0.7 && !d[k].Vacia && d[k].Pendiente <= 1.1)
                            Assert.That(e[k], Is.EqualTo(EstadoDeCelda.Cerrado).Or.EqualTo(EstadoDeCelda.Abierto).Or.EqualTo(EstadoDeCelda.Claro).Or.EqualTo(EstadoDeCelda.Ribera)
                                .Or.EqualTo(EstadoDeCelda.Canaveral));
                        if (e[k] == EstadoDeCelda.Ribera) Assert.Less(d[k].DAgua, ReglasDeVegetacion.Ribera);
                        if (e[k] == EstadoDeCelda.Canaveral) Assert.IsTrue(d[k].AguaAlLado);
                        // las vecindades, salvo entre dos celdas del anillo (fijas por los datos)
                        int i = k % N, j = k / N;
                        foreach (var v in new[] { i < N - 1 ? k + 1 : -1, j < N - 1 ? k + N : -1 })
                        {
                            if (v < 0 || (EnElAnillo(k) && EnElAnillo(v))) continue;
                            Assert.IsTrue(Tabla.Permitido(e[k], e[v]), $"({tx}, {ty}): {e[k]} junto a {e[v]}");
                            celdas++;
                        }
                    }
                }
            Assert.Greater(teselas, 8, "casi todas las teselas se resuelven");
            Assert.Greater(celdas, 10000);
        }

        [Test]
        public void LosRodalesNoSeTocanSinCeldaMixta()
        {
            const int n = 8;
            var zonas = new ZonaDeVegetacion[n * n];
            var bordes = new EspecieDeArbol[n * n];
            for (int k = 0; k < n * n; k++)
            {
                double x = (k % n + 0.5) * 8, y = (k / n + 0.5) * 8;
                zonas[k] = k % 3 == 0 ? ZonaDeVegetacion.Colina : ZonaDeVegetacion.Llano;
                bordes[k] = ReglasDeVegetacion.DominanteDe(x, y, zonas[k]);
            }
            var r = MosaicoDeVegetacion.Rodales(zonas, bordes, n, 77, Tabla, out _);
            Assert.IsNotNull(r);
            CollectionAssert.AreEqual(r, MosaicoDeVegetacion.Rodales(zonas, bordes, n, 77, Tabla, out _));
            for (int k = 0; k < n * n; k++)
            {
                int i = k % n, j = k / n;
                bool anillo = i == 0 || j == 0 || i == n - 1 || j == n - 1;
                if (!anillo)
                {
                    Assert.AreNotEqual((int)EspecieDeArbol.Cipres, r[k], "el ciprés nunca manda");
                    Assert.AreNotEqual((int)EspecieDeArbol.Alamo, r[k], "el álamo solo en la ribera");
                }
                foreach (var v in new[] { i < n - 1 ? k + 1 : -1, j < n - 1 ? k + n : -1 })
                {
                    if (v < 0 || anillo && (v % n == 0 || v / n == 0 || v % n == n - 1 || v / n == n - 1)) continue;
                    Assert.IsTrue(r[k] == r[v] || r[k] < 0 || r[v] < 0, $"rodales {r[k]} y {r[v]} sin celda mixta");
                }
            }
        }

        [Test]
        public void ElAnilloSaleIgualDesdeLasDosTeselas()
        {
            // El anillo es una función pura de los datos del sitio: el mismo estado lo calcule quien lo calcule.
            var a = MosaicoDeVegetacion.Estructura(Tesela(0, 0), N, 5, Colina, Tabla, out _);
            var b = MosaicoDeVegetacion.Estructura(Tesela(1, 0), N, 6, Colina, Tabla, out _);
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            for (int j = 0; j < N; j++)
            {
                var este = Celda(64 - 1, (j + 0.5) * 2);
                var oeste = Celda(64 + 1, (j + 0.5) * 2);
                Assert.AreEqual(MosaicoDeVegetacion.EstadoPorDatos(este, Colina), a[j * N + N - 1], $"fila {j}, borde este de la primera");
                Assert.AreEqual(MosaicoDeVegetacion.EstadoPorDatos(oeste, Colina), b[j * N], $"fila {j}, borde oeste de la segunda");
            }
        }

        [Test]
        public void SinSolucionCaeAlPrototipo()
        {
            // Un anillo de bosque denso alrededor de un llano sin bosque y sin lindes posibles: el cerrado no puede tocar la pradera ni el
            // matorral sin pasar por linde, y en el llano (densidad 0) no cabe linde. No hay solución: null (reparto del prototipo).
            var d = new DatosDeCelda[N * N];
            for (int k = 0; k < N * N; k++)
            {
                bool anillo = EnElAnillo(k);
                d[k] = new DatosDeCelda { X = k % N * 2, Y = k / N * 2, Densidad = anillo ? 1 : 0, Fertilidad = 0.5, Pendiente = 0.1, DAgua = 50, Altura = 450 };
            }
            Assert.IsNull(MosaicoDeVegetacion.Estructura(d, N, 3, Colina, Tabla, out var intentos));
            Assert.AreEqual(Tabla.Reintentos + 1, intentos, "reintenta antes de rendirse");
        }
    }
}
