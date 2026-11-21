// Las maquetas de asentamientos «Prototipo» (`MaquetasDelPrototipo`) contra el propio código del prototipo: `Datos/maquetas_referencia.json`,
// que escribe `tasks/referencias/maquetas.mts` ejecutando `maquetas.ts` y `huellas.ts` con su three (cada pieza y cada bandera de las ocho
// cosas en todos sus casos, el hash, el azar, la semilla y las huellas).
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Tests
{
    public class MaquetasDelMapaTests
    {
        static JObject referencia;
        static JObject Ref => referencia ??= JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Tests/EditMode/Mundo/Datos/maquetas_referencia.json")));

        static MaquetasDelPrototipo.Piezas Hacer(JObject c)
        {
            var p = new MaquetasDelPrototipo.Piezas();
            double x = (double)c["x"], y = (double)c["y"];
            switch ((string)c["tipo"])
            {
                case "ciudad":
                    bool? muralla = c["muralla"].Type == JTokenType.Null ? (bool?)null : (bool)c["muralla"];
                    MaquetasDelPrototipo.Ciudad(p, x, y, (double)c["rc"], (double)c["nivel"], (string)c["color"], (double)c["sem"], muralla);
                    break;
                case "pueblo":
                    MaquetasDelPrototipo.Pueblo(p, x, y, (bool)c["aldea"], (string)c["color"], (double)c["sem"], (string)c["cultivo"]);
                    break;
                case "avanzada": MaquetasDelPrototipo.Avanzada(p, x, y, (double)c["etapa"], (bool)c["ruinas"], (string)c["color"]); break;
                case "obra": MaquetasDelPrototipo.Obra(p, x, y, (bool)c["arrancada"], (string)c["color"]); break;
                case "campamento": MaquetasDelPrototipo.CampamentoDeMercenarios(p, x, y, (double)c["sem"]); break;
                case "bandidos": MaquetasDelPrototipo.Bandidos(p, x, y, (double)c["nivel"], (double)c["sem"]); break;
                case "alijo": MaquetasDelPrototipo.Alijo(p, x, y); break;
                case "batalla": MaquetasDelPrototipo.Batalla(p, x, y, (bool)c["propia"]); break;
                default: throw new ArgumentException((string)c["tipo"]);
            }
            return p;
        }

        static void Igual(JToken r, MaquetasDelPrototipo.Pieza p, string donde)
        {
            Assert.AreEqual((double)r["x"], p.X, 1e-9, donde + ": x");
            Assert.AreEqual((double)r["y"], p.Y, 1e-9, donde + ": y");
            Assert.AreEqual((double)r["ancho"], p.Ancho, 1e-9, donde + ": ancho");
            Assert.AreEqual((double)r["fondo"], p.Fondo, 1e-9, donde + ": fondo");
            Assert.AreEqual((double)r["alto"], p.Alto, 1e-9, donde + ": alto");
            Assert.AreEqual((double)r["giro"], p.Giro, 1e-9, donde + ": giro");
            Assert.AreEqual((double)r["base"], p.Base, 1e-9, donde + ": base");
            Assert.AreEqual((double)r["inclina"], p.Inclina, 1e-9, donde + ": inclina");
        }

        [Test]
        public void LasPiezasSonLasDelPrototipo()
        {
            int piezas = 0, banderas = 0;
            foreach (JObject c in Ref["casos"])
            {
                string caso = $"{c["tipo"]} nivel {c["nivel"]} rc {c["rc"]} muralla {c["muralla"]} aldea {c["aldea"]} cultivo {c["cultivo"]} etapa {c["etapa"]} ruinas {c["ruinas"]}";
                var p = Hacer(c);
                var mias = p.Todas().ToList();
                var suyas = (JArray)c["piezas"];
                Assert.AreEqual(suyas.Count, mias.Count, caso + ": cuántas piezas");
                for (int i = 0; i < mias.Count; i++, piezas++)
                {
                    Assert.AreEqual((string)suyas[i]["mat"], mias[i].mat, $"{caso}, pieza {i}: material");
                    Igual(suyas[i], mias[i].p, $"{caso}, pieza {i} ({mias[i].mat})");
                }
                var misBanderas = p.TodasLasBanderas().ToList();
                var susBanderas = (JArray)c["banderas"];
                Assert.AreEqual(susBanderas.Count, misBanderas.Count, caso + ": cuántas banderas");
                for (int i = 0; i < misBanderas.Count; i++, banderas++)
                {
                    Assert.AreEqual((string)susBanderas[i]["color"], misBanderas[i].color, $"{caso}, bandera {i}: color");
                    Igual(susBanderas[i], misBanderas[i].p, $"{caso}, bandera {i}");
                }
            }
            Assert.AreEqual(6120, piezas);
            Assert.AreEqual(71, banderas);
        }

        [Test]
        public void ElHashElAzarYLaSemillaSonLosDeJavaScript()
        {
            foreach (JArray h in Ref["hashes"])
            {
                Assert.AreEqual((double)h[3], MaquetasDelPrototipo.Hash((double)h[0], (double)h[1], (double)h[2]), 1e-15, $"hash({h[0]}, {h[1]}, {h[2]})");
                Assert.AreEqual((double)h[4], MaquetasDelPrototipo.Azar((double)h[0], (double)h[1], (double)h[2]), 1e-15, $"azar({h[0]}, {h[1]}, {h[2]})");
            }
            foreach (JArray s in Ref["semillas"])
                Assert.AreEqual((double)s[2], MaquetasDelPrototipo.Semilla((double)s[0], (double)s[1]), 0, $"semilla({s[0]}, {s[1]})");
        }

        [Test]
        public void LasHuellasSonLasDelPrototipo()
        {
            var h = (JObject)Ref["huellas"];
            foreach (JArray q in h["plaza"]) Assert.AreEqual((double)q[2], MaquetasDelPrototipo.RadioDePlaza((double)q[0], (double)q[1]), 1e-12);
            foreach (JArray q in h["avanzada"]) Assert.AreEqual((double)q[1], MaquetasDelPrototipo.RadioDeAvanzada((double)q[0]), 1e-12);
            Assert.AreEqual((double)h["pueblo"], MaquetasDelPrototipo.RadioDePueblo(), 1e-12);
            Assert.AreEqual((double)h["campos"], MaquetasDelPrototipo.RadioDeCampos(), 1e-12);
            Assert.AreEqual((double)h["obra"], MaquetasDelPrototipo.RadioDeObra(), 1e-12);
            Assert.AreEqual((double)h["campamento"], MaquetasDelPrototipo.RadioDeCampamento(), 1e-12);
            Assert.AreEqual((double)h["bandidos"], MaquetasDelPrototipo.RadioDeBandidos(), 1e-12);
            Assert.AreEqual((double)h["alijo"], MaquetasDelPrototipo.RadioDeAlijo(), 1e-12);
            Assert.AreEqual((double)h["batalla"], MaquetasDelPrototipo.RadioDeBatalla, 1e-12);
            Assert.AreEqual((double)h["holgura"], MaquetasDelPrototipo.HolguraAro, 1e-12);
            var m = (JObject)Ref["mundo3d"];
            Assert.AreEqual((double)m["maqueta"], MaquetasDelPrototipo.Maqueta);
            Assert.AreEqual((double)m["altoPlaza"], MaquetasDelPrototipo.AltoPlaza);
        }
    }
}
