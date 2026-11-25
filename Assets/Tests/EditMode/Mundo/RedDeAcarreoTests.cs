// La red de los carros de acarreo de las escenas de banco (`RedDeAcarreo`), contra la regla del motor (`crates/rutas` y
// `comercio::red_caminos`): la recta a un espaciado o menos, el A* por el coste de las aristas, la atracción de la red, el escalón por peso
// y los tramos fundidos.
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Tests
{
    public sealed class RedDeAcarreoTests
    {
        /// <summary>Una rejilla de 5 × 3 nodos cada 8 u (espaciado 8), 8-conexa: 1000 en recto y 1414 en diagonal, salvo la fila y = 0, que
        /// cuesta `costeFila0` por arista recta (una cuesta).</summary>
        static GrafoNavegacion Rejilla(uint costeFila0 = 1000)
        {
            var g = new GrafoNavegacion { EspaciadoMin = 8000 };
            uint Id(int i, int j) => (uint)(j * 5 + i);
            for (int j = 0; j < 3; j++)
                for (int i = 0; i < 5; i++)
                    g.Nodos.Add(new NodoGrafo { Id = Id(i, j), Posicion = new Punto { X = i * 8000, Y = j * 8000 } });
            for (int j = 0; j < 3; j++)
                for (int i = 0; i < 5; i++)
                {
                    if (i < 4) g.Aristas.Add(new AristaGrafo { A = Id(i, j), B = Id(i + 1, j), CosteMilesimas = j == 0 ? costeFila0 : 1000 });
                    if (j < 2) g.Aristas.Add(new AristaGrafo { A = Id(i, j), B = Id(i, j + 1), CosteMilesimas = 1000 });
                    if (i < 4 && j < 2) g.Aristas.Add(new AristaGrafo { A = Id(i, j), B = Id(i + 1, j + 1), CosteMilesimas = 1414 });
                    if (i > 0 && j < 2) g.Aristas.Add(new AristaGrafo { A = Id(i, j), B = Id(i - 1, j + 1), CosteMilesimas = 1414 });
                }
            return g;
        }

        [Test]
        public void AUnEspaciadoOMenosEsLaRecta()
        {
            var r = new RedDeAcarreo(Rejilla()).Ruta(1000, 1000, 7000, 3000);
            CollectionAssert.AreEqual(new[] { (1000L, 1000L), (7000L, 3000L) }, r);
        }

        [Test]
        public void SeEngachaAlNodoMasCercanoYVaPorLoMasBarato()
        {
            var red = new RedDeAcarreo(Rejilla());
            var llano = red.Ruta(500, 300, 32_000, 0);
            Assert.That(llano.First(), Is.EqualTo((500L, 300L)), "empieza en el origen real");
            Assert.That(llano.Last(), Is.EqualTo((32_000L, 0L)), "acaba en el destino real");
            Assert.That(llano.Skip(1).All(p => p.y == 0), "en llano, por la fila recta");
            // con la fila y = 0 en cuesta (5000 por arista), sube a la fila de arriba: 1414 + 3 × 1000 + 1414 < 4 × 5000
            var cuesta = new RedDeAcarreo(Rejilla(5000)).Ruta(0, 0, 32_000, 0);
            Assert.That(cuesta.Any(p => p.y == 8000), "rodea la cuesta");
        }

        [Test]
        public void LaRedAtraeYElPesoDaElEscalon()
        {
            var red = new RedDeAcarreo(Rejilla());
            // la primera ruta, por la fila y = 8 (de 0,8 a 32,8)
            var a = red.Ruta(0, 8000, 32_000, 8000);
            red.Registrar(a);
            Assert.That(red.Pesos.Values.All(p => p == 1), "una ruta: todo peso 1");
            Assert.That(red.Tramos().Single().escalon, Is.EqualTo(0), "peso 1: sendero, en un solo tramo");
            // la segunda, de (0,0) a (32,8): sin red le da igual subir al principio o al final (1414 + 3 × 1000); la red (× 0,9) la sube al
            // principio, a la fila y = 8
            var b = red.Ruta(0, 0, 32_000, 8000);
            Assert.That(b.Count(p => p.y == 8000), Is.GreaterThanOrEqualTo(3), "atraída por la red: pisa la fila y = 8");
            red.Registrar(b);
            Assert.That(red.Pesos.Values.Max(), Is.EqualTo(2), "las aristas compartidas pesan 2");
            var tramos = red.Tramos();
            Assert.That(tramos.Any(t => t.escalon == 1), "peso 2: camino");
            Assert.That(tramos.Any(t => t.escalon == 0), "lo de una sola ruta sigue sendero");
            Assert.That(red.Tramos(1).All(t => t.escalon == 1), "forzado: todo camino");
        }

        [Test]
        public void LaClaveRedondeaALaUnidadYNoDependeDelSentido()
        {
            Assert.That(RedDeAcarreo.Clave(1499, 2501, 7000, 3000), Is.EqualTo(RedDeAcarreo.Clave(7000, 3000, 1499, 2501)));
            Assert.That(RedDeAcarreo.Clave(1499, 2501, 7000, 3000), Is.EqualTo(((1L, 3L), (7L, 3L))));
        }
    }
}
