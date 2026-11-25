// Tema 10, punto 9: las etiquetas del mapa no se amontonan (CosasEnElMapa.QueSeVen).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public sealed class EtiquetasQueNoSeAmontonanTests
    {
        static (string, Rect, int, bool, float) Caja(string id, float x, int prioridad, bool siempre = false, float lejania = 10) =>
            (id, new Rect(x, 0, 80, 24), prioridad, siempre, lejania);

        /// <summary>La que pisa a otra de más prioridad se esconde (plaza sobre columna, columna sobre caravana); entre iguales, la más cerca de
        /// la cámara; la de siempre (lo seleccionado, tu columna) se ve aunque pise; y lo que no se toca, todo.</summary>
        [Test]
        public void LaDeMenosPrioridadSeEsconde()
        {
            var nadie = new HashSet<string>();
            var v = CosasEnElMapa.QueSeVen(new[] { Caja("caravana", 30, 3), Caja("plaza", 0, 0), Caja("columna", 100, 2), Caja("lejos", 400, 5) }, nadie);
            CollectionAssert.AreEquivalent(new[] { "plaza", "columna", "lejos" }, v, "la caravana pisa a la plaza y a la columna: se esconde ella");
        }

        [Test]
        public void EntreIgualesLaMasCercaYLaDeSiempreSeVe()
        {
            var nadie = new HashSet<string>();
            CollectionAssert.AreEquivalent(new[] { "cerca" }, CosasEnElMapa.QueSeVen(new[] { Caja("lejos", 0, 2, lejania: 50), Caja("cerca", 20, 2, lejania: 5) }, nadie));
            CollectionAssert.AreEquivalent(new[] { "plaza", "mia" }, CosasEnElMapa.QueSeVen(new[] { Caja("plaza", 0, 0), Caja("mia", 20, 2, siempre: true) }, nadie),
                "tu columna (o lo seleccionado) se ve aunque pise a la plaza");
        }

        /// <summary>Histéresis: con 2 px de solape la que ya se veía sigue; la que estaba escondida no vuelve hasta tener HolguraDeSolape de hueco.</summary>
        [Test]
        public void NoParpadea()
        {
            var cajas = new[] { Caja("plaza", 0, 0), Caja("columna", 78, 2) };
            Assert.IsTrue(CosasEnElMapa.QueSeVen(cajas, new HashSet<string> { "plaza", "columna" }).Contains("columna"), "se veía: 2 px no la esconden");
            Assert.IsFalse(CosasEnElMapa.QueSeVen(cajas, new HashSet<string> { "plaza" }).Contains("columna"), "escondida: 2 px no bastan para volver");
            var separadas = new[] { Caja("plaza", 0, 0), Caja("columna", 80 + CosasEnElMapa.HolguraDeSolape + 1, 2) };
            Assert.IsTrue(CosasEnElMapa.QueSeVen(separadas, new HashSet<string> { "plaza" }).Contains("columna"), "con su hueco, vuelve");
        }
    }
}
