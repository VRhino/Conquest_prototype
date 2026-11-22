// Las parcelas de las aldeas «Mejorado» (`ParcelasDeAldea`): deterministas, dentro del radio, sin pisar lo construido ni lo vedado ni lo que
// está más cerca de un vecino, sin grupos sueltos de menos de 8 casillas, con el barbecho arado en un 10-15 % y las hileras seguidas.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public class ParcelasDeAldeaTests
    {
        static readonly (double x, double y, double r)[] Nucleo = { (0.3, 0.2, 0.14), (-0.25, 0.25, 0.14), (-0.3, -0.2, 0.14), (0.15, -0.3, 0.14), (0, 0, 0.1) };

        static IEnumerable<(int i, int j)> Casillas(ParcelasDeAldea p)
        {
            for (int i = 0; i < p.N; i++) for (int j = 0; j < p.N; j++) if (p.Parcela[i, j] >= 0) yield return (i, j);
        }

        [Test]
        public void ElCoste()
        {
            ParcelasDeAldea.Hacer(3, false, 1, Nucleo);
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            for (int s = 0; s < 12; s++) ParcelasDeAldea.Hacer(1 + s % 3, s % 4 == 0, s, Nucleo);
            UnityEngine.Debug.Log($"[Parcelas] 12 aldeas: {reloj.Elapsed.TotalMilliseconds:0.0} ms");
        }

        [Test]
        public void EsDeterminista()
        {
            foreach (int nivel in new[] { 1, 2, 3 })
            {
                var a = ParcelasDeAldea.Hacer(nivel, false, 17, Nucleo);
                var b = ParcelasDeAldea.Hacer(nivel, false, 17, Nucleo);
                Assert.That(b.Parcela, Is.EqualTo(a.Parcela));
                Assert.That(b.Estados, Is.EqualTo(a.Estados));
                Assert.That(ParcelasDeAldea.Hacer(nivel, false, 18, Nucleo).Parcela, Is.Not.EqualTo(a.Parcela), "otra semilla, otras parcelas");
            }
        }

        [Test]
        public void RespetaElRadioLoConstruidoLoVedadoYLosVecinos()
        {
            // vedado: un arroyo de este a oeste; vecino: otro asentamiento al este
            Func<double, double, bool> arroyo = (x, y) => Math.Abs(y - 0.8) < 0.1;
            var vecinos = new[] { (3.0, 0.0) };
            foreach (int nivel in new[] { 1, 2, 3 })
                foreach (bool ganado in new[] { false, true })
                {
                    var p = ParcelasDeAldea.Hacer(nivel, ganado, 5 + nivel, Nucleo, arroyo, vecinos);
                    Assert.That(Casillas(p).Count(), Is.GreaterThan(30), $"nivel {nivel}: hay campos");
                    foreach (var (i, j) in Casillas(p))
                    {
                        var (x, y) = p.Centro(i, j);
                        double d = Math.Sqrt(x * x + y * y);
                        Assert.That(d, Is.LessThan(p.Radio + ParcelasDeAldea.Celda), "dentro del radio");
                        Assert.That(Nucleo.All(o => Math.Sqrt((x - o.x) * (x - o.x) + (y - o.y) * (y - o.y)) >= o.r + ParcelasDeAldea.Celda * 0.9), "lejos de lo construido");
                        Assert.That(arroyo(x, y), Is.False, "nada en lo vedado");
                        Assert.That(Math.Sqrt((x - 3) * (x - 3) + y * y), Is.GreaterThanOrEqualTo(d), "nada más cerca del vecino");
                    }
                }
        }

        [Test]
        public void SinGruposSueltosYConElBarbechoEnSuMedida()
        {
            int parcelas = 0, aradas = 0;
            for (int semilla = 1; semilla <= 12; semilla++)
            {
                var p = ParcelasDeAldea.Hacer(1 + semilla % 3, semilla % 4 == 0, semilla, Nucleo);
                // grupos 4-conexos de casillas con parcela: ninguno de menos de 8
                var visto = new HashSet<(int, int)>();
                foreach (var c in Casillas(p))
                {
                    if (!visto.Add(c)) continue;
                    var pila = new Stack<(int, int)>(new[] { c });
                    int n = 1;
                    while (pila.Count > 0)
                    {
                        var (a, b) = pila.Pop();
                        foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            var v = (a + di, b + dj);
                            if (v.Item1 < 0 || v.Item2 < 0 || v.Item1 >= p.N || v.Item2 >= p.N || p.Parcela[v.Item1, v.Item2] < 0 || !visto.Add(v)) continue;
                            n++;
                            pila.Push(v);
                        }
                    }
                    Assert.That(n, Is.GreaterThanOrEqualTo(8), "sin grupos sueltos");
                }
                // los ids, seguidos; cada parcela, de una sola orientación
                int cuantas = p.Estados.Length;
                Assert.That(Casillas(p).Select(c => p.Parcela[c.i, c.j]).Distinct().OrderBy(v => v), Is.EqualTo(Enumerable.Range(0, cuantas)));
                foreach (var g in Casillas(p).GroupBy(c => p.Parcela[c.i, c.j]))
                    Assert.That(g.Select(c => p.Horizontal[c.i, c.j]).Distinct().Count(), Is.EqualTo(1), "una orientación por parcela");
                parcelas += cuantas;
                aradas += p.Estados.Count(e => e == ParcelasDeAldea.Estado.Arado);
            }
            Assert.That(aradas / (double)parcelas, Is.InRange(0.08, 0.2), "el barbecho arado, en torno al 10-15 %");
        }
    }
}
