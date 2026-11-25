// Tema 8, ronda 2: el cielo con cúmulos, las sombras de nube redondeadas y la bruma leve. Capturas («nc_…» en `MUNDO_CAPTURAS`): cada vista dos
// veces, con el cielo de antes («_antes») y, dos fotogramas después, con el nuevo («_despues»), así las nubes apenas se han movido: la cámara
// baja en cuatro rumbos y el mismo sitio a tres zooms con la luminancia media de toda la imagen y del centro (el tercio central), y el coste.
using System;
using System.Collections;
using ConquestTactics.Mundo.Mapa;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class SueloDelMapaTests
    {
        [Category("Mapa"), Explicit("tema 8: cielo, nubes y bruma")]
        [UnityTest, Timeout(3_000_000)]
        [Category("Visual")]  // (arte del mapa: en la pasada de antes del push, no en la de cada commit)
        public IEnumerator ElCieloYLaBruma()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "nc-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var sitios = BuscarSitios(relieve, artefacto, datos);
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            try
            {
                // Un valle con colinas alrededor: la bruma de los valles se ve.
                var donde = sitios["colinas"];
                var (almacen, sesion) = Almacen(url, donde, null);
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                MapaDeMundo.PintarMiColumna = false;
                AvesDelMapa.Encendidas = false; // (la ronda 3 tiene su prueba)
                yield return Llegar(mapa, almacen, donde);
                foreach (var (zoom, inc, n) in new[] { (ZoomDelMapa.Cerca, 25.0, "1_cerca"), (ZoomDelMapa.Normal, 35.0, "2_habitual"), (ZoomDelMapa.Alejado, 50.0, "3_alejado") })
                {
                    PinturaDelMapa.CieloNuevo = false;
                    yield return Vista(mapa, zoom, inc, $"nc_{n}_antes");
                    var (todo0, centro0) = Luminancias(ultima);
                    PinturaDelMapa.CieloNuevo = true;
                    yield return Inmediata($"nc_{n}_despues");
                    var (todo1, centro1) = Luminancias(ultima);
                    Debug.Log($"[Cielo] {n}: luminancia media {todo0:0.000} → {todo1:0.000} ({(todo1 / todo0 - 1) * 100:+0.0;-0.0} %), centro {centro0:0.000} → {centro1:0.000} ({(centro1 / centro0 - 1) * 100:+0.0;-0.0} %)");
                }
                // La cámara baja (se ve el cielo) en cuatro rumbos.
                foreach (var (giro, n) in new[] { (0.0, "1_norte"), (Math.PI / 2, "2_este"), (Math.PI, "3_sur"), (3 * Math.PI / 2, "4_oeste") })
                {
                    mapa.Camara.Giro = giro;
                    PinturaDelMapa.CieloNuevo = false;
                    yield return Vista(mapa, ZoomDelMapa.Alejado, 12, $"nc_baja_{n}_antes");
                    PinturaDelMapa.CieloNuevo = true;
                    yield return Inmediata($"nc_baja_{n}_despues");
                    // mirando 50° por encima del horizonte: las nubes
                    // (`Cenit` para el mapa: el interruptor va directo al sombreado)
                    Shader.SetGlobalFloat("_CieloNuevo", 0);
                    yield return Cenit(mapa, $"nc_cenit_{n}_antes");
                    Shader.SetGlobalFloat("_CieloNuevo", 1);
                    yield return Cenit(mapa, $"nc_cenit_{n}_despues");
                }
                mapa.Camara.Giro = 0;
                yield return Vista(mapa, ZoomDelMapa.Normal, 35, "nc_coste");
                yield return Medir(mapa, "[Cielo] nuevo");
                PinturaDelMapa.CieloNuevo = false;
                yield return Medir(mapa, "[Cielo] de antes");
            }
            finally
            {
                PinturaDelMapa.CieloNuevo = true;
                AvesDelMapa.Encendidas = true;
                MapaDeMundo.PintarMiColumna = true;
            }
        }

        /// <summary>La captura de lo que se ve dos fotogramas después (en `ultima` y, con `MUNDO_CAPTURAS`, en su carpeta), sin esperar al suelo.</summary>
        IEnumerator Inmediata(string nombre)
        {
            yield return null;
            yield return null;
            if (ultima != null) UnityEngine.Object.Destroy(ultima);
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            ultima = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            ultima.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            ultima.Apply();
            RenderTexture.active = antes;
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            if (!string.IsNullOrEmpty(carpeta)) System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), ultima.EncodeToPNG());
        }

        /// <summary>La luminancia media (sRGB, Rec. 709) de toda la imagen y de su tercio central.</summary>
        static (double todo, double centro) Luminancias(Texture2D t)
        {
            var px = t.GetPixels32();
            int w = t.width, h = t.height;
            double s = 0, sc = 0;
            int nc = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = px[y * w + x];
                    double l = (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) / 255;
                    s += l;
                    if (x > w / 3 && x < 2 * w / 3 && y > h / 3 && y < 2 * h / 3) { sc += l; nc++; }
                }
            return (s / (w * h), sc / nc);
        }
    }
}
