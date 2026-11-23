// Tema 7: el estandarte de la columna en la figura del centro de la primera fila y el blasón sobre un paño que contrasta con él. Seis columnas
// de Facciones con sigilos distintos, cada una en su línea; capturas («tp_…» en `MUNDO_CAPTURAS`) del estandarte de cerca por delante y por
// detrás de cada una, la columna a los tres zooms con el portador en el centro y el antes y el después (portador a la izquierda y blasón
// recortado frente a portador en el centro y paño).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo.Mapa;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class SueloDelMapaTests
    {
        [Category("Mapa"), Explicit("tema 7: el estandarte en el centro y el blasón sobre su paño")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator ElEstandarteConSuPano()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "pano-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            // con sus emblemas, como en el juego (`TexturaDeSigilo.Asegurar`): las otras pruebas del mapa cargan el catálogo sin ellos
            TexturaDeSigilo.Asegurar();
            rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            var sitio = Buscador(relieve, artefacto);
            var donde = sitio(caminos.SitiosDeSuelo["suelo_pradera"], 10, null);
            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, FigurasDelMapa.EstandarteConPano, ColumnasDelMapa.PortadorAlCentro);
            MapaDeMundo.AclararCopas = false;
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
            var sigiloAntes = CosasEnElMapa.SigiloDe;
            try
            {
                var (almacen, sesion) = Almacen(url, donde, null);
                var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
                double ahora = sesion.AhoraMundo;
                // seis Facciones con sigilos al azar (semillas distintas: claros, oscuros y de color fuerte, de formas distintas), cada una con su
                // columna en su línea, a 2 u
                const int N = 6;
                for (int i = 0; i < N; i++)
                {
                    var fid = $"f-{i + 1}";
                    vista.Entidades.Add(new Entidad { Faccion = new Faccion { Id = fid, Nombre = fid, Sigilo = Sigilos.Aleatorio(new System.Random(31 + 17 * i)) } });
                    Columna(vista, $"ej-p{i}", new[] { $"heroe-p{i}" }, new[] { "milicia_lanceros", "lenadores" }, false, (donde.x - 4, donde.y + 2.0 * (i - 2.5)),
                            (donde.x + 60, donde.y + 2.0 * (i - 2.5)), 0.05, ahora, fid);
                }
                almacen.Aplicar(new MensajeDeServidor { Foto = vista });
                CosasEnElMapa.SigiloDe = (fid, ancho) => Sigilos.DeFaccion(almacen, fid) is Sigilo sg ? TexturaDeSigilo.De(sg, ancho * 2) : null;
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                MapaDeMundo.PintarMiColumna = false;
                Horno(mapa, datos);
                float metro = ModelosDeFiguras.MetrosAEscena;
                Vector3 Asta(string id)
                {
                    var l = FigurasDelMapa.Pies(id, f => f.Estandarte);
                    Assert.That(l.Count, Is.EqualTo(1), $"un estandarte en {id}");
                    // (el centro del paño: entre los travesaños, a unos 3,2 m sobre el suelo por la escala del estandarte)
                    return l[0] + Vector3.up * (4.4f * metro);
                }
                (double x, double y) EnMapa(Vector3 q) => (q.x / EscalaDelMapa.MetrosPorUnidad, -q.z / EscalaDelMapa.MetrosPorUnidad);
                yield return Llegar(mapa, almacen, (donde.x - 3, donde.y));
                EsconderLaColumna();
                // que se horneen las tropas (de cerca)
                ModelosDeFiguras.PixelesMinimos = 1;
                yield return Vista(mapa, ZoomDelMapa.Cerca, 30, "tp_pidiendo");
                var fin = Time.realtimeSinceStartup + 240;
                while (HornoDeFiguras.Bitacora.Count(b => b.StartsWith("tropa|")) < 6 && Time.realtimeSinceStartup < fin) yield return null;
                ModelosDeFiguras.PixelesMinimos = 14;
                yield return new WaitForSecondsRealtime(3);
                // el estandarte de cerca, por delante (desde el este, la columna va hacia el este) y por detrás, de cada columna
                for (int i = 0; i < N; i++)
                {
                    var id = $"ej-p{i}";
                    yield return Llegar(mapa, almacen, EnMapa(Asta(id)));
                    EsconderLaColumna();
                    yield return DeCerca(mapa, Asta(id), new Vector3(10f, 0.5f, -2f) * metro, $"tp_{i}_delante");
                    yield return DeCerca(mapa, Asta(id), new Vector3(-10f, 1.5f, 2f) * metro, $"tp_{i}_detras");
                }
                foreach (var kv in FigurasDelMapa.PanoElegido) Debug.Log($"[Paño] {kv.Key.name}: {kv.Value}");
                // la columna a los tres zooms, con el portador en el centro
                yield return Llegar(mapa, almacen, EnMapa(Asta("ej-p2")));
                EsconderLaColumna();
                foreach (var (zoom, z) in new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                    yield return Vista(mapa, zoom, 30, $"tp_zoom_{z}");
                yield return DeCerca(mapa, Asta("ej-p2") - Vector3.up * (3.0f * metro), new Vector3(10f, 7f, -6f) * metro, "tp_portador_centro");
                // el antes y el después, de cerca y de tres cuartos por delante, de dos columnas
                foreach (var (despues, n) in new[] { (false, "antes"), (true, "despues") })
                {
                    FigurasDelMapa.EstandarteConPano = despues;
                    ColumnasDelMapa.PortadorAlCentro = despues;
                    // (la disposición se rehace como mucho cada 2 s: `CosasDelMapa.RehacerFigurasMs`)
                    yield return new WaitForSecondsRealtime(2.5f);
                    foreach (var i in new[] { 1, 4 })
                    {
                        var id = $"ej-p{i}";
                        yield return Llegar(mapa, almacen, EnMapa(Asta(id)));
                        EsconderLaColumna();
                        yield return DeCerca(mapa, Asta(id) - Vector3.up * (1.6f * metro), new Vector3(10f, 4f, -7f) * metro, $"tp_{n}_{i}");
                    }
                }
            }
            finally
            {
                (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, FigurasDelMapa.EstandarteConPano, ColumnasDelMapa.PortadorAlCentro) = antes;
                CosasEnElMapa.SigiloDe = sigiloAntes;
                MapaDeMundo.PintarMiColumna = true;
                ModelosDeFiguras.PixelesMinimos = 14;
                ModelosDeFiguras.Reiniciar();
            }
        }
    }
}
