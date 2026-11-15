// El sigilo de Facción (paso 8, tanda 1): el SVG calcado del prototipo, pintado por Unity. Una muestra de cada forma, campo y orla, más el
// neutro y el del Gran Rey; con MUNDO_CAPTURAS se guardan como PNG (y su SVG) para compararlos con el navegador.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConquestTactics.Mundo.UI.Tests
{
    public class SigilosTests
    {
        [UnityTest]
        public IEnumerator CadaFormaCampoYOrlaSePinta()
        {
            TexturaDeSigilo.Asegurar();
            Assert.IsTrue(Sigilos.Cargado, "el catálogo del prototipo");
            Assert.AreEqual(6, Sigilos.Formas.Count);
            Assert.AreEqual(18, Sigilos.Campos.Count);
            Assert.AreEqual(12, Sigilos.Colores.Count);
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            var muestras = Sigilos.Campos.Select((campo, i) => new Sigilo
            {
                FormaId = Sigilos.Formas[i % Sigilos.Formas.Count], CampoId = campo, EmblemaId = Sigilos.Emblemas[(i * 7) % Sigilos.Emblemas.Count],
                ColorPrimarioId = Sigilos.Colores[i % 12].Id, ColorSecundarioId = Sigilos.Colores[(i + 5) % 12].Id, ColorEmblemaId = Sigilos.Colores[(i + 9) % 12].Id,
                OrlaId = Sigilos.Orlas[i % Sigilos.Orlas.Count], ColorOrlaId = Sigilos.Colores[(i + 3) % 12].Id,
            }).Concat(new[] { Sigilos.Neutro }).ToList();
            for (var i = 0; i < muestras.Count; i++)
            {
                var tex = TexturaDeSigilo.De(muestras[i], 120, i == 3);
                Assert.AreEqual(120, tex.width);
                // Pintado: el centro del escudo no es transparente.
                Assert.Greater(tex.GetPixel(60, tex.height / 2).a, 0.5f, $"{Sigilos.Clave(muestras[i])}: el escudo pintado");
                Assert.AreSame(tex, TexturaDeSigilo.De(muestras[i], 120, i == 3), "de la caché");
                if (!string.IsNullOrEmpty(carpeta))
                {
                    Directory.CreateDirectory(Path.Combine(carpeta, "sigilos"));
                    File.WriteAllBytes(Path.Combine(carpeta, "sigilos", $"s{i:00}.png"), tex.EncodeToPNG());
                    File.WriteAllText(Path.Combine(carpeta, "sigilos", $"s{i:00}.svg"), Sigilos.Svg(muestras[i], 120, i == 3));
                }
                yield return null;
            }
        }
    }
}
