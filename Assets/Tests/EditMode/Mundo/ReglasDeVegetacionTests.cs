// Las reglas de la vegetación del mapa de mundo (`ReglasDeVegetacion`) frente al cliente jugador: los valores esperados salen de ejecutar con node
// el TypeScript de BronzeAgeClient (`src/mundo3d/ruido.ts`, `especies.ts` y `matas.ts`) en los mismos puntos, con los umbrales del prototipo
// (agua 375, costa 420, colina 545, montaña 600 m). Si coinciden, el mapa de especies, los rodales y las matas salen iguales que allí.
using NUnit.Framework;

namespace ConquestTactics.Mundo.Tests
{
    public class ReglasDeVegetacionTests
    {
        const double Agua = 375, Costa = 420, Colina = 545, Montana = 600;

        static void Igual(double[] esperado, double[] valor)
        {
            Assert.AreEqual(esperado.Length, valor.Length);
            for (int i = 0; i < esperado.Length; i++) Assert.AreEqual(esperado[i], valor[i], 1e-12, $"posición {i}");
        }

        [Test]
        public void AzarComoElPrototipo()
        {
            Assert.AreEqual(0.0, ReglasDeVegetacion.Azar(0, 0, 0), 1e-12);
            Assert.AreEqual(0.6911013713106513, ReglasDeVegetacion.Azar(1, 2, 3), 1e-12);
            Assert.AreEqual(0.7528522084467113, ReglasDeVegetacion.Azar(-5, 7, 11), 1e-12);
            Assert.AreEqual(0.7160182376392186, ReglasDeVegetacion.Azar(123, 456, 789), 1e-12);
            Assert.AreEqual(0.6957798656076193, ReglasDeVegetacion.Azar(2482, 798, 53), 1e-12);
            Assert.AreEqual(0.15003638342022896, ReglasDeVegetacion.Azar(-1, -1, 307), 1e-12);
            Assert.AreEqual(0.5256961705163121, ReglasDeVegetacion.Azar(99999, -77, 41), 1e-12);
            Assert.AreEqual(0.25720089743845165, ReglasDeVegetacion.Azar(17, 23, 67), 1e-12);
        }

        [Test]
        public void ManchasComoElPrototipo()
        {
            Assert.AreEqual(0.6752765909768641, ReglasDeVegetacion.Manchas(0.0, 0.0, 7, 101), 1e-12);
            Assert.AreEqual(0.3073875387199223, ReglasDeVegetacion.Manchas(0.0, 0.0, 9, 211), 1e-12);
            Assert.AreEqual(0.7863202537409961, ReglasDeVegetacion.Manchas(0.0, 0.0, 22, 307), 1e-12);
            Assert.AreEqual(0.5555400240700692, ReglasDeVegetacion.Manchas(0.0, 0.0, 6, 77), 1e-12);
            Assert.AreEqual(0.55290343741986, ReglasDeVegetacion.Manchas(3.7, 9.2, 7, 101), 1e-12);
            Assert.AreEqual(0.6503198468708137, ReglasDeVegetacion.Manchas(3.7, 9.2, 9, 211), 1e-12);
            Assert.AreEqual(0.5503209513602347, ReglasDeVegetacion.Manchas(3.7, 9.2, 22, 307), 1e-12);
            Assert.AreEqual(0.4302160255358812, ReglasDeVegetacion.Manchas(3.7, 9.2, 6, 77), 1e-12);
            Assert.AreEqual(0.5457727456878856, ReglasDeVegetacion.Manchas(2482.4, 798.9, 7, 101), 1e-12);
            Assert.AreEqual(0.7608338242692333, ReglasDeVegetacion.Manchas(2482.4, 798.9, 9, 211), 1e-12);
            Assert.AreEqual(0.7159315544629825, ReglasDeVegetacion.Manchas(2482.4, 798.9, 22, 307), 1e-12);
            Assert.AreEqual(0.4736705075410643, ReglasDeVegetacion.Manchas(2482.4, 798.9, 6, 77), 1e-12);
            Assert.AreEqual(0.5237748676500921, ReglasDeVegetacion.Manchas(1166.25, 1830.5, 7, 101), 1e-12);
            Assert.AreEqual(0.5390116022691169, ReglasDeVegetacion.Manchas(1166.25, 1830.5, 9, 211), 1e-12);
            Assert.AreEqual(0.7402965225050371, ReglasDeVegetacion.Manchas(1166.25, 1830.5, 22, 307), 1e-12);
            Assert.AreEqual(0.7254146914345239, ReglasDeVegetacion.Manchas(1166.25, 1830.5, 6, 77), 1e-12);
            Assert.AreEqual(0.5529954648614582, ReglasDeVegetacion.Manchas(-12.5, 44.1, 7, 101), 1e-12);
            Assert.AreEqual(0.42858709956932334, ReglasDeVegetacion.Manchas(-12.5, 44.1, 9, 211), 1e-12);
            Assert.AreEqual(0.896446134453563, ReglasDeVegetacion.Manchas(-12.5, 44.1, 22, 307), 1e-12);
            Assert.AreEqual(0.7490534953466764, ReglasDeVegetacion.Manchas(-12.5, 44.1, 6, 77), 1e-12);
            Assert.AreEqual(0.9918068776804189, ReglasDeVegetacion.Manchas(882.3, 1806.7, 7, 101), 1e-12);
            Assert.AreEqual(0.7766882193061025, ReglasDeVegetacion.Manchas(882.3, 1806.7, 9, 211), 1e-12);
            Assert.AreEqual(0.6320285544843756, ReglasDeVegetacion.Manchas(882.3, 1806.7, 22, 307), 1e-12);
            Assert.AreEqual(0.24436459103440958, ReglasDeVegetacion.Manchas(882.3, 1806.7, 6, 77), 1e-12);
        }

        [Test]
        public void ZonaYEspecieComoElPrototipo()
        {
            Assert.AreEqual(ZonaDeVegetacion.Llano, ReglasDeVegetacion.ZonaDe(2482.4, 798.9, 490, 10.0, Costa, Colina, Montana));
            Assert.AreEqual(ZonaDeVegetacion.Colina, ReglasDeVegetacion.ZonaDe(1166.25, 1830.5, 552, 10.0, Costa, Colina, Montana));
            Assert.AreEqual(ZonaDeVegetacion.RiberaAlta, ReglasDeVegetacion.ZonaDe(882.3, 1806.7, 447, 1.2, Costa, Colina, Montana));
            Assert.AreEqual(ZonaDeVegetacion.Llano, ReglasDeVegetacion.ZonaDe(334.5, 126.5, 456, 30.0, Costa, Colina, Montana));
            Assert.AreEqual(ZonaDeVegetacion.Costa, ReglasDeVegetacion.ZonaDe(100.1, 200.2, 405, 5.0, Costa, Colina, Montana));
            Assert.AreEqual(ZonaDeVegetacion.Montana, ReglasDeVegetacion.ZonaDe(500.5, 600.6, 610, 8.0, Costa, Colina, Montana));
            Assert.AreEqual(ZonaDeVegetacion.Ribera, ReglasDeVegetacion.ZonaDe(700.7, 800.8, 430, 2.0, Costa, Colina, Montana));
            Assert.AreEqual(ZonaDeVegetacion.Colina, ReglasDeVegetacion.ZonaDe(1500.2, 90.9, 580, 50.0, Costa, Colina, Montana));
            Assert.AreEqual(ZonaDeVegetacion.Montana, ReglasDeVegetacion.ZonaDe(33.3, 44.4, 700, 9.0, Costa, Colina, Montana));
            Assert.AreEqual(ZonaDeVegetacion.RiberaAlta, ReglasDeVegetacion.ZonaDe(2000.5, 1500.5, 540, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(ZonaDeVegetacion.Llano, ReglasDeVegetacion.ZonaDe(10.5, 20.5, 421, 3.0, Costa, Colina, Montana));
            Assert.AreEqual(ZonaDeVegetacion.Montana, ReglasDeVegetacion.ZonaDe(3000.3, 2000.7, 600, 4.0, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Olivo, ReglasDeVegetacion.EspecieDe(2482.4, 798.9, 490, 10.0, 0.1, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Olivo, ReglasDeVegetacion.EspecieDe(2482.4, 798.9, 490, 10.0, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Encina, ReglasDeVegetacion.EspecieDe(2482.4, 798.9, 490, 10.0, 0.75, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(2482.4, 798.9, 490, 10.0, 0.95, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Olivo, ReglasDeVegetacion.EspecieDe(1166.25, 1830.5, 552, 10.0, 0.1, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Olivo, ReglasDeVegetacion.EspecieDe(1166.25, 1830.5, 552, 10.0, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(1166.25, 1830.5, 552, 10.0, 0.75, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Cipres, ReglasDeVegetacion.EspecieDe(1166.25, 1830.5, 552, 10.0, 0.95, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Alamo, ReglasDeVegetacion.EspecieDe(882.3, 1806.7, 447, 1.2, 0.1, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Alamo, ReglasDeVegetacion.EspecieDe(882.3, 1806.7, 447, 1.2, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Alamo, ReglasDeVegetacion.EspecieDe(882.3, 1806.7, 447, 1.2, 0.75, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Encina, ReglasDeVegetacion.EspecieDe(882.3, 1806.7, 447, 1.2, 0.95, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Olivo, ReglasDeVegetacion.EspecieDe(334.5, 126.5, 456, 30.0, 0.1, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Olivo, ReglasDeVegetacion.EspecieDe(334.5, 126.5, 456, 30.0, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Encina, ReglasDeVegetacion.EspecieDe(334.5, 126.5, 456, 30.0, 0.75, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(334.5, 126.5, 456, 30.0, 0.95, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(100.1, 200.2, 405, 5.0, 0.1, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(100.1, 200.2, 405, 5.0, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(100.1, 200.2, 405, 5.0, 0.75, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Encina, ReglasDeVegetacion.EspecieDe(100.1, 200.2, 405, 5.0, 0.95, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Abeto, ReglasDeVegetacion.EspecieDe(500.5, 600.6, 610, 8.0, 0.1, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Abeto, ReglasDeVegetacion.EspecieDe(500.5, 600.6, 610, 8.0, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Abeto, ReglasDeVegetacion.EspecieDe(500.5, 600.6, 610, 8.0, 0.75, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(500.5, 600.6, 610, 8.0, 0.95, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Alamo, ReglasDeVegetacion.EspecieDe(700.7, 800.8, 430, 2.0, 0.1, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Alamo, ReglasDeVegetacion.EspecieDe(700.7, 800.8, 430, 2.0, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Alamo, ReglasDeVegetacion.EspecieDe(700.7, 800.8, 430, 2.0, 0.75, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Palmera, ReglasDeVegetacion.EspecieDe(700.7, 800.8, 430, 2.0, 0.95, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(1500.2, 90.9, 580, 50.0, 0.1, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(1500.2, 90.9, 580, 50.0, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(1500.2, 90.9, 580, 50.0, 0.75, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Cipres, ReglasDeVegetacion.EspecieDe(1500.2, 90.9, 580, 50.0, 0.95, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Abeto, ReglasDeVegetacion.EspecieDe(33.3, 44.4, 700, 9.0, 0.1, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Abeto, ReglasDeVegetacion.EspecieDe(33.3, 44.4, 700, 9.0, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Abeto, ReglasDeVegetacion.EspecieDe(33.3, 44.4, 700, 9.0, 0.75, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(33.3, 44.4, 700, 9.0, 0.95, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Alamo, ReglasDeVegetacion.EspecieDe(2000.5, 1500.5, 540, 0.5, 0.1, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Alamo, ReglasDeVegetacion.EspecieDe(2000.5, 1500.5, 540, 0.5, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Alamo, ReglasDeVegetacion.EspecieDe(2000.5, 1500.5, 540, 0.5, 0.75, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Encina, ReglasDeVegetacion.EspecieDe(2000.5, 1500.5, 540, 0.5, 0.95, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Olivo, ReglasDeVegetacion.EspecieDe(10.5, 20.5, 421, 3.0, 0.1, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Olivo, ReglasDeVegetacion.EspecieDe(10.5, 20.5, 421, 3.0, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Encina, ReglasDeVegetacion.EspecieDe(10.5, 20.5, 421, 3.0, 0.75, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(10.5, 20.5, 421, 3.0, 0.95, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(3000.3, 2000.7, 600, 4.0, 0.1, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(3000.3, 2000.7, 600, 4.0, 0.5, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Abeto, ReglasDeVegetacion.EspecieDe(3000.3, 2000.7, 600, 4.0, 0.75, Costa, Colina, Montana));
            Assert.AreEqual(EspecieDeArbol.Pino, ReglasDeVegetacion.EspecieDe(3000.3, 2000.7, 600, 4.0, 0.95, Costa, Colina, Montana));
        }

        [Test]
        public void ProbabilidadesDeLasMatasComoElPrototipo()
        {
            Igual(new[] { 0.5351715937512814, 0.0, 0.045, 0.0, 0.012 }, ReglasDeVegetacion.Probabilidades(450.0, 0.2, 0.5, 0.1, 5.0, 0.6, Agua, Costa, Colina, Montana));
            Igual(new[] { 0.1222338972487597, 0.0, 0.126, 0.0, 0.07174055530268549 }, ReglasDeVegetacion.Probabilidades(560.0, 0.5, 0.2, 0.3, 0.5, 0.9, Agua, Costa, Colina, Montana));
            Igual(new[] { 0.35, 0.0, 0.014499999999999999, 0.31666666666666665, 0.012 }, ReglasDeVegetacion.Probabilidades(380.0, 0.05, 0.8, 0.0, 0.8, 0.2, Agua, Costa, Colina, Montana));
            Igual(new[] { 0.014699519999999995, 0.0, 0.04525000000000001, 0.0, 0.2979808830223032 }, ReglasDeVegetacion.Probabilidades(620.0, 0.9, 0.1, 0.6, 9.0, 0.5, Agua, Costa, Colina, Montana));
            Igual(new[] { 0.3844889089343557, 0.023437499999999997, 0.07699999999999999, 0.0, 0.012 }, ReglasDeVegetacion.Probabilidades(430.0, 0.0, 0.4, 0.15, 2.0, 0.7, Agua, Costa, Colina, Montana));
        }

        [Test]
        public void DistanciaAlAgua()
        {
            // Una fila de 5 muestras con agua en la del medio: 2, 1, 0, 1, 2; y sin agua, infinito.
            var d = ReglasDeVegetacion.DistanciaAlAgua(new[] { false, false, true, false, false }, 5, 1);
            CollectionAssert.AreEqual(new[] { 2f, 1f, 0f, 1f, 2f }, d);
            Assert.IsTrue(float.IsPositiveInfinity(ReglasDeVegetacion.DistanciaAlAgua(new[] { false, false }, 2, 1)[0]));
        }
    }
}
