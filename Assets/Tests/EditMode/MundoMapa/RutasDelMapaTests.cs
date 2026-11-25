// El corte de la ruta en la huella del destino (`RutasDelMapa.Cortar`): la cinta acaba donde la ruta entra en la huella, y si la columna ya
// está dentro no queda ruta.
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public sealed class RutasDelMapaTests
    {
        [Test, Category("Mapa")]
        public void LaRutaSeCortaEnElBordeDeLaHuella()
        {
            // de (0, 0) a (10, 0) y de ahí a (10, 10), al centro de una plaza de 3 u en (10, 10): acaba en (10, 7)
            var l = RutasDelMapa.Cortar(new List<(double x, double y)> { (0, 0), (10, 0), (10, 10) }, 10, 10, 3);
            Assert.That(l, Has.Count.EqualTo(3));
            Assert.That(l[2].x, Is.EqualTo(10).Within(1e-9));
            Assert.That(l[2].y, Is.EqualTo(7).Within(1e-9));
            // en diagonal: el punto a 3 u del centro
            l = RutasDelMapa.Cortar(new List<(double x, double y)> { (0, 0), (10, 10) }, 10, 10, 3);
            Assert.That(Math.Sqrt((l[1].x - 10) * (l[1].x - 10) + (l[1].y - 10) * (l[1].y - 10)), Is.EqualTo(3).Within(1e-9));
            // sin huella en el camino, entera
            Assert.That(RutasDelMapa.Cortar(new List<(double x, double y)> { (0, 0), (5, 0), (9, 0) }, 30, 30, 3), Has.Count.EqualTo(3));
            // ya dentro: solo donde está
            Assert.That(RutasDelMapa.Cortar(new List<(double x, double y)> { (9, 9), (10, 10) }, 10, 10, 3), Has.Count.EqualTo(1));
        }
    }
}
