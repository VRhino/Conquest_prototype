// Tema 7, ronda B: las caravanas del mapa con sus modelos horneados (los animales y los carros de `HornoDeFiguras`): una columna con dos carros de
// carga enganchados (básico con buey y reforzado con caballo), el de fundación y el carrito del héroe; una caravana comercial suelta con su
// escolta y tres carros (básico con dromedario, reforzado con buey y básico con caballo); una de fundación suelta, y una ajena avistada y escoltada.
// Capturas («tk_…» en `MUNDO_CAPTURAS`) de cerca, a zoom habitual y alejado, la vuelta por los cuatro lados y cada caravana suelta; y el coste
// con caravanas dentro del encuadre, a zoom habitual y de cerca, con modelos y con bloques.
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
        /// <summary>Una caravana suelta andando de `a` a `b` a `velocidad` u/s (o enganchada, sin plan: `a` null).</summary>
        static Caravana UnaCaravana(Foto vista, string id, TipoCaravana tipo, string faccion, (string tipo, string animal)[] carros, int escolta, bool escoltada,
            (double x, double y)? a, (double x, double y) b, double velocidad, double ahora)
        {
            var k = new Caravana { Id = id, Tipo = tipo, FaccionId = faccion, Escoltada = escoltada };
            foreach (var (t, an) in carros) k.Carros.Add(new CarroCaravana { TipoCarro = t, Animal = an });
            for (int i = 0; i < escolta; i++) k.EscoltaIds.Add($"{id}-e{i}");
            if (a is (double, double) p)
            {
                double d = Math.Sqrt((b.x - p.x) * (b.x - p.x) + (b.y - p.y) * (b.y - p.y));
                double t0 = ahora - 20_000, t1 = t0 + d / velocidad * 1000;
                k.Plan = new Plan { Vertices = { new Vertice { EnMs = (long)t0, P = Pu(p.x, p.y) }, new Vertice { EnMs = (long)t1, P = Pu(b.x, b.y) } } };
                k.Posicion = Pu(p.x, p.y);
            }
            vista.Entidades.Add(new Entidad { Caravana = k });
            return k;
        }

        [Category("Mapa"), Explicit("tema 7, ronda B: las caravanas con sus modelos")]
        [UnityTest, Timeout(3_000_000)]
        [Category("Visual")]  // (arte del mapa: en la pasada de antes del push, no en la de cada commit)
        public IEnumerator LasCaravanasConSusModelos()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "caravanas-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            var sitio = Buscador(relieve, artefacto);
            var donde = sitio(caminos.SitiosDeSuelo["suelo_pradera"], 8, null);
            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, ModelosDeFiguras.Activos);
            MapaDeMundo.AclararCopas = false;
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
            var sigiloAntes = CosasEnElMapa.SigiloDe;
            try
            {
                var (almacen, sesion) = Almacen(url, donde, null);
                var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
                var azarF = new System.Random(5);
                foreach (var fid in new[] { "f-1", "f-2" })
                    vista.Entidades.Add(new Entidad { Faccion = new Faccion { Id = fid, Nombre = fid, Sigilo = Sigilos.Aleatorio(azarF) } });
                double ahora = sesion.AhoraMundo, v = 0.05;
                // la columna, hacia el este: el héroe de la montura de tiro, dos escuadras, dos caravanas de carga, la de fundación y su carrito
                Columna(vista, "ej-k", new[] { "h-tiro" }, new[] { "milicia_lanceros", "lenadores" }, false, (donde.x - 3, donde.y), (donde.x + 60, donde.y), v, ahora);
                var col = vista.Entidades.Last(x => x.Ejercito != null && x.Ejercito.Id == "ej-k").Ejercito;
                col.CaravanasAdjuntasIds.Add("k-1");
                col.CaravanasAdjuntasIds.Add("k-2");
                col.CaravanaFundacionId = "k-f";
                col.Suministro = new Cantidades { PorId = { ["trigo"] = 4000 } };
                UnaCaravana(vista, "k-1", TipoCaravana.CaravanaComercial, "f-1", new[] { ("basico", "buey") }, 0, false, null, default, v, ahora);
                UnaCaravana(vista, "k-2", TipoCaravana.CaravanaComercial, "f-1", new[] { ("reforzado", "caballo") }, 0, false, null, default, v, ahora);
                UnaCaravana(vista, "k-f", TipoCaravana.CaravanaDeFundacion, "f-1", new (string, string)[0], 0, false, null, default, v, ahora);
                // las sueltas, en paralelo y cada una en su línea, a 8 u (que ninguna figura de una pise a otra): la comercial con escolta, la de
                // fundación y la ajena avistada
                UnaCaravana(vista, "k-s", TipoCaravana.CaravanaComercial, "f-1", new[] { ("basico", "camello"), ("reforzado", "buey"), ("basico", "caballo") }, 2, false,
                         (donde.x - 3, donde.y + 8), (donde.x + 60, donde.y + 8), v, ahora);
                UnaCaravana(vista, "k-fs", TipoCaravana.CaravanaDeFundacion, "f-1", new (string, string)[0], 0, false, (donde.x - 3, donde.y - 8), (donde.x + 60, donde.y - 8), v, ahora);
                UnaCaravana(vista, "k-a", TipoCaravana.CaravanaComercial, "f-2", new (string, string)[0], 0, true, (donde.x - 3, donde.y + 16), (donde.x + 60, donde.y + 16), v, ahora);
                almacen.Aplicar(new MensajeDeServidor { Foto = vista });
                CosasEnElMapa.SigiloDe ??= (fid, ancho) => Sigilos.DeFaccion(almacen, fid) is Sigilo sg ? TexturaDeSigilo.De(sg, ancho * 2) : null;
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                MapaDeMundo.PintarMiColumna = false;
                Horno(mapa, datos, id => id == "h-tiro" ? new Dictionary<string, string> { ["torso"] = "torso_media_inicio", ["guantes"] = "guantes_media_inicio",
                    ["pantalones"] = "pantalones_media_inicio", ["botas"] = "botas_media_inicio", ["montura"] = "montura_tiro" } : null);
                (double x, double y) Cabeza(TipoEntidad t, string id)
                {
                    var e = almacen.Buscar(t, id);
                    var p = Instante.Posicion(t == TipoEntidad.Ejercito ? e.Ejercito.Plan : e.Caravana.Plan, null, sesion.AhoraMundo);
                    return (p.x / 1000, p.y / 1000);
                }
                yield return Llegar(mapa, almacen, Cabeza(TipoEntidad.Ejercito, "ej-k"));
                EsconderLaColumna();
                // de cerca, para que se pidan; luego, hasta que estén todas horneadas
                var claves = new[] { "carro|basico|buey", "carro|reforzado|caballo", "carro|fundacion|buey", "carro|carrito|burro", "carro|basico|camello",
                                     "carro|reforzado|buey", "carro|basico|caballo" };
                ModelosDeFiguras.PixelesMinimos = 1;
                for (int k = 0; k < 6 && !claves.All(ModelosDeFiguras.Listos.ContainsKey); k++)
                {
                    yield return Vista(mapa, k % 2 == 0 ? ZoomDelMapa.Normal : ZoomDelMapa.Cerca, 30, "tk_pidiendo");
                    var fin = Time.realtimeSinceStartup + 40;
                    while (!claves.All(ModelosDeFiguras.Listos.ContainsKey) && Time.realtimeSinceStartup < fin) yield return null;
                }
                ModelosDeFiguras.PixelesMinimos = 14;
                yield return new WaitForSecondsRealtime(3);
                foreach (var b in HornoDeFiguras.Bitacora.Where(b => !b.StartsWith("  ")).Distinct()) Debug.Log("[Caravanas] " + b);
                foreach (var kv in HornoDeFiguras.Medidas) Debug.Log($"[Caravanas] {kv.Key}: {kv.Value}");
                Assert.That(claves.All(ModelosDeFiguras.Listos.ContainsKey), "sin hornear: " + string.Join(", ", claves.Where(k => !ModelosDeFiguras.Listos.ContainsKey(k))));

                float metro = ModelosDeFiguras.MetrosAEscena;
                // dónde están los carros (o lo que se mire) de verdad: lo que pusieron las figuras en el último fotograma
                bool EsCarro(SitioDeFigura f) => f.Figura == FiguraDeColumna.Carro || f.Figura == FiguraDeColumna.CarroDeFundacion || f.Figura == FiguraDeColumna.Carrito;
                Vector3 Centro(string id, Func<SitioDeFigura, bool> cual)
                {
                    var l = FigurasDelMapa.Pies(id, cual);
                    if (l.Count == 0) l = FigurasDelMapa.Pies(id, _ => true);
                    Assert.That(l.Count, Is.GreaterThan(0), $"sin figuras puestas de {id}");
                    var c = Vector3.zero;
                    foreach (var p in l) c += p;
                    return c / l.Count + Vector3.up * (1.2f * metro);
                }
                (double x, double y) EnMapa(Vector3 v) => (v.x / EscalaDelMapa.MetrosPorUnidad, -v.z / EscalaDelMapa.MetrosPorUnidad);
                yield return Llegar(mapa, almacen, EnMapa(Centro("ej-k", EsCarro)));
                EsconderLaColumna();
                yield return DeCerca(mapa, Centro("ej-k", EsCarro), new Vector3(1.5f, 6f, -16f) * metro, "tk_columna_cerca");
                yield return Vista(mapa, ZoomDelMapa.Normal, 30, "tk_columna_habitual");
                yield return Vista(mapa, ZoomDelMapa.Alejado, 30, "tk_columna_alejado");
                // la vuelta por los cuatro lados, de cerca (de frente, por la derecha, por detrás y por la izquierda)
                foreach (var (desde, n) in new[] { (new Vector3(14f, 6f, -2f), "1_frente"), (new Vector3(2f, 6f, -14f), "2_derecha"),
                                                    (new Vector3(-14f, 6f, 2f), "3_detras"), (new Vector3(-2f, 6f, 14f), "4_izquierda") })
                    yield return DeCerca(mapa, Centro("ej-k", EsCarro), desde * metro, $"tk_vuelta_{n}");
                // las sueltas, de cerca y de tres cuartos
                foreach (var (id, n) in new[] { ("k-s", "comercial"), ("k-fs", "fundacion"), ("k-a", "ajena") })
                {
                    yield return Llegar(mapa, almacen, EnMapa(Centro(id, _ => true)));
                    EsconderLaColumna();
                    yield return DeCerca(mapa, Centro(id, _ => true), new Vector3(9f, 7f, -13f) * metro, $"tk_suelta_{n}");
                }

                // el coste: 12 caravanas comerciales de 3 carros y 2 de escolta dentro del encuadre, a zoom habitual y de cerca, con modelos y con bloques
                var extra = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 3 };
                foreach (var e in vista.Entidades) extra.Entidades.Add(e);
                var tiposDeCarro = new[] { ("basico", "buey"), ("reforzado", "caballo"), ("basico", "camello"), ("reforzado", "buey") };
                var centro = (x: donde.x + 10, y: donde.y - 24);
                for (int i = 0; i < 12; i++)
                    UnaCaravana(extra, $"k-c{i}", TipoCaravana.CaravanaComercial, i % 3 == 0 ? "f-2" : "f-1",
                             Enumerable.Range(0, 3).Select(j => tiposDeCarro[(i + j) % 4]).ToArray(), 2, false, (0, 0), (1, 0), 0.02, sesion.AhoraMundo);
                almacen.Aplicar(new MensajeDeServidor { Foto = extra });
                // en rejilla, todas hacia el este: 4 filas a 1,4 u y 3 columnas a 3,6 u (una caravana de 3 carros y 2 escuadras mide menos de 2 u)
                void Recolocar()
                {
                    double t = sesion.AhoraMundo;
                    for (int i = 0; i < 12; i++)
                    {
                        var p = (x: centro.x + (i % 3 - 1) * 3.6, y: centro.y + (i / 3 - 1.5) * 1.4);
                        var k = almacen.Buscar(TipoEntidad.Caravana, $"k-c{i}").Caravana;
                        var a0 = (x: p.x - 0.4, y: p.y);
                        k.Plan = new Plan { Vertices = { new Vertice { EnMs = (long)(t - 20_000), P = Pu(a0.x, a0.y) },
                                                         new Vertice { EnMs = (long)(t - 20_000 + 50 / 0.02 * 1000), P = Pu(a0.x + 50, a0.y) } } };
                        k.Posicion = Pu(p.x, p.y);
                    }
                }
                yield return Llegar(mapa, almacen, centro);
                EsconderLaColumna();
                Recolocar();
                foreach (var (zoom, z) in new[] { (ZoomDelMapa.Normal, "habitual"), (ZoomDelMapa.Cerca, "cerca") })
                {
                    foreach (var modelos in new[] { false, true })
                    {
                        ModelosDeFiguras.Activos = modelos;
                        yield return Vista(mapa, zoom, 30, $"tk_coste_{z}_{(modelos ? "modelos" : "bloques")}");
                        var fot = new List<double>();
                        int figurasN = 0, llamadas = 0, conModelo = 0;
                        long tris = 0;
                        for (int i = 0; i < 180; i++)
                        {
                            yield return null;
                            fot.Add(Time.unscaledDeltaTime * 1000);
                            figurasN = Math.Max(figurasN, FigurasDelMapa.Figuras + ModelosDeFiguras.Instancias);
                            conModelo = Math.Max(conModelo, ModelosDeFiguras.Instancias);
                            llamadas = Math.Max(llamadas, FigurasDelMapa.Llamadas + ModelosDeFiguras.Llamadas);
                            tris = Math.Max(tris, ModelosDeFiguras.TriangulosPintados);
                        }
                        fot.Sort();
                        Debug.Log($"[Coste caravanas] {z} · {(modelos ? "con modelos" : "de bloques")}: fotograma mediana {fot[fot.Count / 2]:0.00} ms (p95 {fot[fot.Count * 95 / 100]:0.00}); " +
                                  $"figuras {figurasN} (con modelo {conModelo}), llamadas de figuras {llamadas}, triángulos de modelos {tris}; con todo {ModelosDeFiguras.Completos}, reducidas {ModelosDeFiguras.Reducidos}");
                    }
                }
            }
            finally
            {
                (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, ModelosDeFiguras.Activos) = antes;
                CosasEnElMapa.SigiloDe = sigiloAntes;
                MapaDeMundo.PintarMiColumna = true;
                ModelosDeFiguras.PixelesMinimos = 14;
                ModelosDeFiguras.Reiniciar();
            }
        }
    }
}
