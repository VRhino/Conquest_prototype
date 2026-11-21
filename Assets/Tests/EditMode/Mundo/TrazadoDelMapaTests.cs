// El trazado de caminos y fronteras y la textura de los caminos contra lo que dan las funciones del prototipo (BronzeAgeClient) sobre los
// mismos casos: `Datos/trazado_referencia.json`, que escribe `tasks/referencias/trazado.mts` ejecutándolas con node.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Tests
{
    public class TrazadoDelMapaTests
    {
        static JObject referencia;
        static JObject Ref => referencia ??= JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Tests/EditMode/Mundo/Datos/trazado_referencia.json")));

        static List<(double x, double y)> Puntos(JToken t) => t.Select(p => ((double)p["x"], (double)p["y"])).ToList();

        static void Iguales(JToken esperado, IReadOnlyList<(double x, double y)> dado, string que, double tol = 1e-9)
        {
            var e = Puntos(esperado);
            Assert.AreEqual(e.Count, dado.Count, $"{que}: cuántos puntos");
            for (int i = 0; i < e.Count; i++)
            {
                Assert.AreEqual(e[i].x, dado[i].x, tol, $"{que}: x del punto {i}");
                Assert.AreEqual(e[i].y, dado[i].y, tol, $"{que}: y del punto {i}");
            }
        }

        [Test]
        public void SuavizarCaminoDaLoMismoQueElPrototipo()
        {
            int i = 0;
            foreach (var c in Ref["caminos"])
            {
                var puntos = Puntos(c["puntos"]);
                var fijos = c["fijos"].Select(f => (int)f).ToList();
                Iguales(c["suave"], TrazadoDelMapa.SuavizarCamino(puntos, fijos), $"camino {i}");
                var rec = TrazadoDelMapa.Recorridos(puntos);
                var esperado = c["recorridos"].Select(v => (double)v).ToArray();
                for (int k = 0; k < esperado.Length; k++) Assert.AreEqual(esperado[k], rec[k], 1e-9, $"camino {i}: recorrido {k}");
                i++;
            }
        }

        [Test]
        public void LosTramosEnLaCajaSonLosDelPrototipo()
        {
            foreach (var c in Ref["cajas"])
            {
                var puntos = Puntos(c["puntos"]);
                var caja = c["caja"].Select(v => (double)v).ToArray();
                var dados = TrazadoDelMapa.TramosEnCaja(puntos, caja[0], caja[1], caja[2], caja[3]);
                var esperados = c["tramos"].ToList();
                Assert.AreEqual(esperados.Count, dados.Count, "cuántos tramos");
                for (int k = 0; k < dados.Count; k++)
                    Iguales(esperados[k], puntos.Skip(dados[k].desde).Take(dados[k].hasta - dados[k].desde + 1).ToList(), $"tramo {k}");
            }
        }

        static TrazadoDelMapa.Disco Disco(JToken d) => new TrazadoDelMapa.Disco
        {
            Id = (string)d["id"], Centro = ((double)d["centro"]["x"], (double)d["centro"]["y"]), Radio = (double)d["radio"], FaccionId = (string)d["faccionId"],
        };

        [Test]
        public void LasZonasRecortadasYLasFronterasSonLasDelPrototipo()
        {
            var discos = Ref["discos"].Select(Disco).ToList();
            var poligonos = Ref["poligonos"].ToList();
            for (int i = 0; i < discos.Count; i++)
            {
                var pol = TrazadoDelMapa.PoligonoDeZona(discos[i], discos.Where((_, j) => j != i));
                Iguales(poligonos[i], pol, $"zona {i}", 1e-9);
                if (pol.Count > 0) Assert.AreEqual((int)Ref["sentidos"][i], TrazadoDelMapa.Sentido(pol), $"sentido de la zona {i}");
            }
            var zonas = Ref["zonas"].Select(z => ((string)z["faccionId"], (IReadOnlyList<(double x, double y)>)Puntos(z["contorno"]))).ToList();
            var dadas = TrazadoDelMapa.PartirFronteras(zonas);
            var esperadas = Ref["fronteras"].ToList();
            Assert.AreEqual(esperadas.Count, dadas.Count, "cuántos tramos de frontera");
            for (int k = 0; k < dadas.Count; k++)
            {
                Assert.AreEqual((int)esperadas[k]["zona"], dadas[k].Zona, $"tramo {k}: zona");
                Assert.AreEqual((string)esperadas[k]["tipo"], dadas[k].Tipo == TrazadoDelMapa.TipoDeFrontera.Exterior ? "exterior" : "interior", $"tramo {k}: tipo");
                Iguales(esperadas[k]["puntos"], dadas[k].Puntos, $"tramo {k}");
            }
            Assert.IsTrue(dadas.Any(t => t.Tipo == TrazadoDelMapa.TipoDeFrontera.Interior), "hay una línea entre provincias");
        }

        [Test]
        public void LasZonasRecordadasSonLasDelPrototipo()
        {
            var poligonos = Ref["poligonos"].ToList();
            (double, double) C(double x, double y) => (x, y);
            var dadas = TrazadoDelMapa.ZonasDeLoRecordado(
                new[] { ("av1", C(0, 0), "f-1", (IReadOnlyList<(double x, double y)>)Puntos(poligonos[0])), ("av2", C(30, 30), "", (IReadOnlyList<(double x, double y)>)Circulo(30, 30, 10)) },
                new[] { ("co1", C(16, 6), "f-2", (double?)12), ("co2", C(60, 0), "", (double?)8), ("co3", C(0, 40), "f-5", (double?)null) },
                new[] { ("pr1", C(35, 8), "f-1", (IReadOnlyList<(double x, double y)>)Circulo(35, 8, 9)) });
            var esperadas = (JObject)Ref["recordadas"];
            Assert.AreEqual(esperadas.Count, dadas.Count, "cuántas zonas recordadas");
            foreach (var kv in esperadas) Iguales(kv.Value, dadas[kv.Key], $"zona recordada {kv.Key}");
        }

        static List<(double x, double y)> Circulo(double x, double y, double r)
        {
            var c = TrazadoDelMapa.Circulo((x, y), r);
            c.RemoveAt(c.Count - 1);
            return c;
        }

        /// <summary>El vado es un punto: un camino que cruza por él (como las rutas del motor: nodos cada 4 u y el nodo del vado entre los suyos)
        /// pasa a menos de 0,05 u del punto y cruza recto en los 0,75 u de alrededor; sin vados, el suavizado es el de siempre.</summary>
        [Test]
        public void ElCaminoCruzaPorElPuntoDelVado()
        {
            var vado = (x: 10.37, y: 5.21);
            var crudo = new List<(double x, double y)> { (0, 0), (4, 0), (8, 4), vado, (12, 8), (16, 8), (20, 12), (24, 12) };
            var vados = new List<(double x, double y)> { vado };
            var suave = TrazadoDelMapa.PorLosVados(TrazadoDelMapa.CaminoSuave(crudo, TrazadoDelMapa.Anclas(vados)), crudo, vados);
            double Distancia((double x, double y) p, (double x, double y) a, (double x, double y) b)
            {
                double dx = b.x - a.x, dy = b.y - a.y, l = dx * dx + dy * dy;
                double t = l > 0 ? Math.Max(0, Math.Min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l)) : 0;
                return Math.Sqrt(Math.Pow(p.x - a.x - t * dx, 2) + Math.Pow(p.y - a.y - t * dy, 2));
            }
            double alVado = double.MaxValue;
            for (int i = 1; i < suave.Count; i++) alVado = Math.Min(alVado, Distancia(vado, suave[i - 1], suave[i]));
            Assert.Less(alVado, 0.05, "el camino pasa por el punto del vado");
            // recto en los 0,75 u de alrededor: los vértices a menos de 0,75 u, en la recta del cruce
            double dirx = crudo[4].x - crudo[2].x, diry = crudo[4].y - crudo[2].y, ld = Math.Sqrt(dirx * dirx + diry * diry);
            foreach (var q in suave.Where(q => Math.Sqrt(Math.Pow(q.x - vado.x, 2) + Math.Pow(q.y - vado.y, 2)) <= 0.75 + 1e-9))
                Assert.Less(Math.Abs((q.x - vado.x) * diry - (q.y - vado.y) * dirx) / ld, 1e-6, $"({q.x}, {q.y}) en la recta del cruce");
            Assert.AreEqual(2, suave.Count(q => Math.Abs(Math.Sqrt(Math.Pow(q.x - vado.x, 2) + Math.Pow(q.y - vado.y, 2)) - 0.75) < 1e-9), "los dos extremos del cruce recto");
            // sin vados, igual que el suavizado del prototipo
            var sinVados = TrazadoDelMapa.CaminoSuave(crudo, new HashSet<(long, long)>());
            CollectionAssert.AreEqual(sinVados, TrazadoDelMapa.PorLosVados(sinVados, crudo, null));
            CollectionAssert.AreEqual(sinVados, TrazadoDelMapa.PorLosVados(TrazadoDelMapa.CaminoSuave(crudo, new HashSet<(long, long)>()), crudo, new List<(double, double)>()));
        }

        [Test]
        public void ElRefuerzoDeLosColoresOscurosEsElDelPrototipo()
        {
            foreach (var r in Ref["refuerzos"])
                Assert.AreEqual((double)r["refuerzo"], TrazadoDelMapa.Refuerzo(Convert.ToUInt32(((string)r["color"]).TrimStart('#'), 16)), 1e-12, (string)r["color"]);
        }

        [Test]
        public void LaTexturaDeLosCaminosEsLaDelPrototipo()
        {
            foreach (var (nombre, escalon) in new[] { ("sendero", 0), ("camino", 1) })
            {
                var esperado = Inflar((string)Ref["texturas"][nombre]);
                var dado = TexturaDeCamino.Pintar(escalon);
                Assert.AreEqual(esperado.Length, dado.Length, $"{nombre}: tamaño");
                int distintos = 0, peor = 0;
                for (int i = 0; i < dado.Length; i++)
                {
                    int d = Math.Abs(dado[i] - esperado[i]);
                    if (d > 0) distintos++;
                    peor = Math.Max(peor, d);
                }
                // (solo puede variar el redondeo de un seno o una exponencial entre V8 y .NET)
                Assert.LessOrEqual(peor, 1, $"{nombre}: ninguna componente se aparta más de 1");
                Assert.Less(distintos, dado.Length / 1000, $"{nombre}: casi todas iguales ({distintos} distintas)");
            }
        }

        static byte[] Inflar(string base64)
        {
            using var entrada = new DeflateStream(new MemoryStream(Convert.FromBase64String(base64)), CompressionMode.Decompress);
            using var salida = new MemoryStream();
            entrada.CopyTo(salida);
            return salida.ToArray();
        }
    }
}
