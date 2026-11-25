// Las vacas de las aldeas ganaderas (25-11): la equivalencia entre las cabezas del rebaño y las vacas que se ven en cada corral.
using ConquestTactics.Mundo.Mapa;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public sealed class VacasDelRebanoTests
    {
        /// <summary>Una vaca por cada cuatro cabezas de su corral (hacia arriba), hasta 10: el pie de cría (20 por corral) da 5; el tope (40), 10;
        /// una recién saqueada, 2 o 3; vacía, ninguna; sin el dato, 5. Las cabezas se reparten entre los corrales (1, 2 o 3).</summary>
        [Test]
        public void UnaVacaPorCadaCuatroCabezasHastaDiez()
        {
            for (int corrales = 1; corrales <= 3; corrales++)
            {
                Assert.AreEqual(5, MaquetasDelMapa.VacasPorCorral(20 * corrales, corrales), "el pie de cría");
                Assert.AreEqual(10, MaquetasDelMapa.VacasPorCorral(40 * corrales, corrales), "el tope");
                Assert.AreEqual(10, MaquetasDelMapa.VacasPorCorral(400 * corrales, corrales), "más que el tope, 10");
                Assert.AreEqual(3, MaquetasDelMapa.VacasPorCorral(10 * corrales, corrales), "saqueada");
                Assert.AreEqual(0, MaquetasDelMapa.VacasPorCorral(0, corrales), "vacía");
                Assert.AreEqual(5, MaquetasDelMapa.VacasPorCorral(-1, corrales), "sin el dato");
            }
            Assert.AreEqual(1, MaquetasDelMapa.VacasPorCorral(1, 1), "una cabeza, una vaca");
            Assert.AreEqual(2, MaquetasDelMapa.VacasPorCorral(15, 2), "7,5 cabezas por corral: 2");
        }
    }
}
