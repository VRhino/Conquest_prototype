// Tema 9, ronda 3: la vida del mapa, solo pájaros y el polvo de lo que anda. Capturas («nv_…» en `MUNDO_CAPTURAS`): una columna de ejército y
// una caravana andando en llano seco y en llano fértil (con hierba), con polvo y sin él, de cerca y a zoom habitual; las bandadas con la cámara
// baja; y el coste con la vida y sin ella.
using System;
using System.Collections;
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
        [Category("Mapa"), Explicit("tema 9: pájaros y polvo")]
        [UnityTest, Timeout(3_000_000)]
        [Category("Visual")]  // (arte del mapa: en la pasada de antes del push, no en la de cada commit)
        public IEnumerator LaVidaDelMapa()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "nv-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var sitios = BuscarSitios(relieve, artefacto, datos);
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo);
            MapaDeMundo.AclararCopas = false;
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
            try
            {
                var seco = sitios["llano_seco"];
                var fertil = sitios["llano_fertil"];
                var (almacen, sesion) = Almacen(url, seco, null);
                var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
                double ahora = sesion.AhoraMundo;
                foreach (var (d, s) in new[] { (seco, "s"), (fertil, "f") })
                {
                    Columna(vista, $"ej-{s}", new[] { $"heroe-{s}" }, TropasConModelo, true, (d.x - 3, d.y), (d.x + 60, d.y), 0.15, ahora);
                    UnaCaravana(vista, $"k-{s}", TipoCaravana.CaravanaComercial, "f-1", new[] { ("basico", "buey"), ("reforzado", "caballo") }, 0, false,
                        (d.x - 3, d.y - 1.5), (d.x + 60, d.y - 1.5), 0.15, ahora);
                }
                almacen.Aplicar(new MensajeDeServidor { Foto = vista });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                MapaDeMundo.PintarMiColumna = false;
                Horno(mapa, datos);
                (double x, double y) Cabeza(string s) { var p = Instante.Posicion(almacen.Buscar(TipoEntidad.Ejercito, $"ej-{s}").Ejercito.Plan, null, sesion.AhoraMundo); return (p.x / 1000, p.y / 1000); }
                foreach (var s in new[] { "s", "f" })
                {
                    var c = Cabeza(s);
                    Debug.Log($"[Vida] suelo {s}: polvo {PolvoDelMapa_Suelo(mapa, c):0.00} (1 seco, 0 nada)");
                    yield return Llegar(mapa, almacen, (c.x - 0.5, c.y - 0.8));
                    EsconderLaColumna();
                    yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"nv_{s}_0_pidiendo");
                    yield return EsperarHorneado(ModelosDeFiguras.ClaveDeHeroe($"heroe-{s}"));
                    foreach (var (zoom, inc, z) in new[] { (ZoomDelMapa.Cerca, 30.0, "1_cerca"), (ZoomDelMapa.Normal, 35.0, "2_habitual") })
                    {
                        c = Cabeza(s);
                        almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito.Posicion = Pu(c.x - 0.5, c.y - 0.8);
                        PolvoDelMapa.Encendido = false;
                        yield return Vista(mapa, zoom, inc, $"nv_{s}_{z}_sin");
                        PolvoDelMapa.Encendido = true;
                        yield return Fotograma($"nv_{s}_{z}_con", 2.5f);
                    }
                }
                Debug.Log($"[Vida] bocanadas echadas: {mapa.Polvo.Echadas}");
                // las bandadas, desde la cámara del juego: a la bandada más cercana, de cerca y a zoom habitual, con lo que mide un pájaro (px)
                Debug.Log($"[Vida] pájaros alrededor: {mapa.Aves.Pajaros}");
                // la bandada más cercana a lo seco y a la hierba: el plumaje claro sobre los dos suelos
                foreach (var sv in new[] { "s", "f" })
                {
                    var cv = Cabeza(sv);
                    var aqui = EscalaDelMapa.AEscena(cv.x, cv.y, 0);
                    // las bandadas se ponen alrededor de lo que se mira: primero, mirar ese sitio
                    almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito.Posicion = Pu(cv.x, cv.y);
                    yield return Llegar(mapa, almacen, cv);
                    // en lo seco, la bandada más cercana que vuele sobre suelo seco (si no hay, la más cercana)
                    var cerca = mapa.Aves.Centros.OrderBy(b => (new Vector2(b.x, b.z) - new Vector2(aqui.x, aqui.z)).sqrMagnitude).ToList();
                    double SueloDe(Vector3 b) { var (mx, _, my) = EscalaDelMapa.AlMapa(b); return PolvoDelMapa_Suelo(mapa, (mx, my)); }
                    var bandada = sv == "s" ? cerca.Where(b => SueloDe(b) > 0.8).DefaultIfEmpty(cerca[0]).First() : cerca[0];
                    var (bx, _, by) = EscalaDelMapa.AlMapa(bandada);
                    Debug.Log($"[Vida] bandada ({sv}): suelo {SueloDe(bandada):0.00} (1 seco), de {cerca.Count}");
                    almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito.Posicion = Pu(bx, by);
                    yield return Llegar(mapa, almacen, (bx, by));
                    // el primer pájaro de esa bandada y dónde está a la hora `t` del reloj del mapa (lo mismo que `Conquest/MapaAves`)
                    var vuelos = mapa.Aves.Vuelos;
                    int primero = Enumerable.Range(0, vuelos.Count / 2).OrderBy(k => (new Vector2(vuelos[2 * k].x, vuelos[2 * k].z) - new Vector2(bandada.x, bandada.z)).sqrMagnitude).First();
                    Vector3 Pajaro(int k, float t)
                    {
                        var a0 = vuelos[2 * k];
                        var a1 = vuelos[2 * k + 1];
                        float ang = a1.y + t * a1.x;
                        return new Vector3(a0.x + Mathf.Cos(ang) * a0.w, a0.y + Mathf.Sin(t * 0.7f + a1.z) * 0.3f * EscalaDelMapa.MetrosPorUnidad, a0.z + Mathf.Sin(ang) * a0.w);
                    }
                    double Px()
                    {
                        var cam = mapa.Vista;
                        var der = cam.transform.right * (float)(AvesDelMapa.Envergadura * EscalaDelMapa.MetrosPorUnidad / 2);
                        var a = cam.WorldToScreenPoint(bandada - der);
                        var b = cam.WorldToScreenPoint(bandada + der);
                        return Vector2.Distance(a, b) * 1600.0 / cam.pixelWidth;
                    }
                    foreach (var (zoom, inc, z) in (sv == "s" ? new[] { (ZoomDelMapa.Normal, 35.0, "2_habitual"), (ZoomDelMapa.Cerca, 30.0, "1_cerca") } : new[] { (ZoomDelMapa.Normal, 35.0, "2_habitual") }))
                    {
                        yield return Vista(mapa, zoom, inc, $"nv_aves_{sv}_{z}_centro");
                        Debug.Log($"[Vida] pájaro ({sv}) a zoom {z}: {Px():0.0} px de envergadura (1600 de ancho), con {AvesDelMapa.Envergadura:0.00} u");
                        // la cámara del juego tal cual (su ángulo y su distancia), siguiendo al primer pájaro de la bandada: así sale en el cuadro
                        var cam = mapa.Vista.transform;
                        var desdeMira = cam.position - (cam.position + cam.forward * Vector3.Distance(cam.position, bandada));
                        for (int f = 0; f < 90; f++)
                        {
                            var ave = Pajaro(primero, Time.timeSinceLevelLoad);
                            MapaDeMundo.CamaraFija = (ave + desdeMira, ave);
                            yield return null;
                            if (f == 60) yield return Fotograma($"nv_aves_{sv}_{z}", 0);
                            if (f == 89) yield return Fotograma($"nv_aves_{sv}_{z}_b", 0);
                        }
                        MapaDeMundo.CamaraFija = null;
                    }
                }
                // el coste, a zoom habitual en lo seco con la columna andando
                var cs = Cabeza("s");
                almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito.Posicion = Pu(cs.x - 0.5, cs.y - 0.8);
                yield return Vista(mapa, ZoomDelMapa.Normal, 35, "nv_coste");
                yield return Medir(mapa, "[Vida] con pájaros y polvo");
                AvesDelMapa.Encendidas = false;
                PolvoDelMapa.Encendido = false;
                yield return Medir(mapa, "[Vida] sin");
            }
            finally
            {
                AvesDelMapa.Encendidas = true;
                PolvoDelMapa.Encendido = true;
                (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo) = antes;
                MapaDeMundo.PintarMiColumna = true;
            }
        }

        static double PolvoDelMapa_Suelo(MapaDeMundo mapa, (double x, double y) c) => mapa.Polvo?.Suelo(c.x, c.y) ?? -1;
    }
}
