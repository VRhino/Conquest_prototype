// Tema 7: el Aeda en el mapa con su modelo (estilo «Mejorado»), con su burro de alforjas un paso detrás. Un Aeda andando hacia el este, con
// una columna cerca (en otra línea, sin pisarse) para comparar el tamaño; capturas («ta_…» en `MUNDO_CAPTURAS`) de cerca, a zoom habitual y
// alejado y la vuelta por los cuatro lados; y el coste con los tres Aedas de un mundo dentro del encuadre, con modelo y de bloques.
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
        static void UnAeda(Foto vista, string id, (double x, double y) a, (double x, double y) b, double velocidad, double ahora)
        {
            double d = Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y));
            double t0 = ahora - 20_000, t1 = t0 + d / velocidad * 1000;
            vista.Entidades.Add(new Entidad { AedaItinerante = new AedaItinerante { Id = id, Posicion = Pu(a.x, a.y),
                Plan = new Plan { Vertices = { new Vertice { EnMs = (long)t0, P = Pu(a.x, a.y) }, new Vertice { EnMs = (long)t1, P = Pu(b.x, b.y) } } } } });
        }

        /// <summary>El reloj del mundo en marcha, a ritmo 1, desde donde está: sin servidor, la sesión lo tiene parado (lo arranca el motor con su
        /// primera muestra de reloj) y nada avanza por su plan.</summary>
        static void RelojEnMarcha(Sesion sesion)
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var t = typeof(Sesion);
            double mundo = sesion.AhoraMundo, local = (double)t.GetProperty("AhoraLocal", F).GetValue(sesion);
            t.GetField("relojMundoMs", F).SetValue(sesion, mundo);
            t.GetField("relojLocalMs", F).SetValue(sesion, local);
            t.GetField("relojFactor", F).SetValue(sesion, 1.0);
            t.GetField("relojPausado", F).SetValue(sesion, false);
        }

        [Category("Mapa"), Explicit("tema 7: el Aeda con su modelo")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator ElAedaConSuModelo()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "aeda-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            var sitio = Buscador(relieve, artefacto);
            var donde = sitio(caminos.SitiosDeSuelo["suelo_pradera"], 8, null);
            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, ModelosDeFiguras.Activos);
            MapaDeMundo.AclararCopas = false;
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
            try
            {
                var (almacen, sesion) = Almacen(url, donde, null);
                var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
                // a la velocidad del motor: `itinerantes.velocidad` = 20 u por minuto de mundo
                double ahora = sesion.AhoraMundo, v = 20.0 / 60;
                UnAeda(vista, "ae-1", (donde.x - 2, donde.y), (donde.x + 400, donde.y), v, ahora);
                // la columna para comparar, en su línea a 2,5 u; y los otros dos Aedas del mundo, cada uno en la suya
                Columna(vista, "ej-a", new[] { "h-tiro" }, new[] { "milicia_lanceros" }, false, (donde.x - 2, donde.y - 2.5), (donde.x + 400, donde.y - 2.5), v, ahora);
                UnAeda(vista, "ae-2", (donde.x - 2, donde.y + 2.5), (donde.x + 400, donde.y + 2.5), v, ahora);
                UnAeda(vista, "ae-3", (donde.x - 2, donde.y + 5), (donde.x + 400, donde.y + 5), v, ahora);
                almacen.Aplicar(new MensajeDeServidor { Foto = vista });
                RelojEnMarcha(sesion);
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                MapaDeMundo.PintarMiColumna = false;
                Horno(mapa, datos, id => id == "h-tiro" ? new Dictionary<string, string> { ["torso"] = "torso_media_inicio", ["guantes"] = "guantes_media_inicio",
                    ["pantalones"] = "pantalones_media_inicio", ["botas"] = "botas_media_inicio", ["montura"] = "montura_tiro" } : null);
                (double x, double y) Cabeza(string id)
                {
                    var p = Instante.Posicion(almacen.Buscar(TipoEntidad.Aeda, id).AedaItinerante.Plan, null, sesion.AhoraMundo);
                    return (p.x / 1000, p.y / 1000);
                }
                yield return Llegar(mapa, almacen, Cabeza("ae-1"));
                EsconderLaColumna();
                var claves = new[] { "aeda", "animal|burro" };
                ModelosDeFiguras.PixelesMinimos = 1;
                for (int k = 0; k < 6 && !claves.All(ModelosDeFiguras.Listos.ContainsKey); k++)
                {
                    yield return Vista(mapa, k % 2 == 0 ? ZoomDelMapa.Normal : ZoomDelMapa.Cerca, 30, "ta_pidiendo");
                    var fin = Time.realtimeSinceStartup + 40;
                    while (!claves.All(ModelosDeFiguras.Listos.ContainsKey) && Time.realtimeSinceStartup < fin) yield return null;
                }
                ModelosDeFiguras.PixelesMinimos = 14;
                yield return new WaitForSecondsRealtime(3);
                foreach (var b in HornoDeFiguras.Bitacora.Where(b => !b.StartsWith("  ")).Distinct()) Debug.Log("[Aeda] " + b);
                Assert.That(claves.All(ModelosDeFiguras.Listos.ContainsKey), "sin hornear: " + string.Join(", ", claves.Where(k => !ModelosDeFiguras.Listos.ContainsKey(k))));
                var t = ModelosDeFiguras.Listos["aeda"];
                Debug.Log($"[Aeda] modelo: {t.Triangulos} triángulos, {t.Materiales.Length} materiales ({string.Join(", ", t.Materiales.Select(m => m.name))}), alto {t.Alto:0.00} m");

                // a su velocidad real, el Aeda y su burro andan: fotogramas de andar, no el reposo
                yield return Llegar(mapa, almacen, Cabeza("ae-1"));
                EsconderLaColumna();
                yield return Vista(mapa, ZoomDelMapa.Cerca, 30, "ta_andando");
                var c0 = Cabeza("ae-1"); var m0 = FigurasDelMapa.CabezaMedida("ae-1");
                yield return new WaitForSecondsRealtime(2);
                var c1 = Cabeza("ae-1"); var m1 = FigurasDelMapa.CabezaMedida("ae-1");
                Debug.Log($"[Aeda] plan: de {c0} a {c1} en 2 s; medida en el mapa: de {m0} a {m1}; Time.time {Time.time:0.0}");
                int andando = 0, parados = 0;
                for (int i = 0; i < 60; i++) { yield return null; andando += ModelosDeFiguras.ultimosAedaAndando; parados += ModelosDeFiguras.ultimosAedaParado; }
                Debug.Log($"[Aeda] a {v:0.000} u/s: {andando} poses andando y {parados} parados en 60 fotogramas; anda a {ModelosDeFiguras.VelocidadAeda:0.00} m/s del modelo");
                Assert.That(andando, Is.GreaterThan(0), "el Aeda no anda a su velocidad");
                Assert.That(parados, Is.EqualTo(0), "el Aeda sale parado mientras avanza");
                float metro = ModelosDeFiguras.MetrosAEscena;
                Vector3 Centro(string id)
                {
                    var l = FigurasDelMapa.Pies(id, _ => true);
                    Assert.That(l.Count, Is.GreaterThan(0), $"sin figuras puestas de {id}");
                    var c = Vector3.zero;
                    foreach (var p in l) c += p;
                    return c / l.Count + Vector3.up * (0.9f * metro);
                }
                // (lo que avanza en el segundo que espera `DeCerca` antes de la foto: se encuadra donde va a estar)
                var avance = Vector3.right * (float)(v * EscalaDelMapa.MetrosPorUnidad);
                (double x, double y) EnMapa(Vector3 q) => (q.x / EscalaDelMapa.MetrosPorUnidad, -q.z / EscalaDelMapa.MetrosPorUnidad);
                // de cerca (con la columna detrás, para el tamaño), a zoom habitual y alejado
                yield return Llegar(mapa, almacen, EnMapa(Centro("ae-1")));
                EsconderLaColumna();
                yield return DeCerca(mapa, Centro("ae-1") + avance, new Vector3(2f, 4f, -9f) * metro, "ta_cerca");
                yield return Vista(mapa, ZoomDelMapa.Normal, 30, "ta_habitual");
                yield return Vista(mapa, ZoomDelMapa.Alejado, 30, "ta_alejado");
                // la vuelta por los cuatro lados (de frente, por la derecha, por detrás y por la izquierda)
                foreach (var (desde, n) in new[] { (new Vector3(7f, 3f, -1f), "1_frente"), (new Vector3(1f, 3f, -7f), "2_derecha"),
                                                    (new Vector3(-7f, 3f, 1f), "3_detras"), (new Vector3(-1f, 3f, 7f), "4_izquierda") })
                    yield return DeCerca(mapa, Centro("ae-1") + avance, desde * metro, $"ta_vuelta_{n}");

                // el coste: los tres Aedas (y la columna) en el encuadre a zoom habitual, con modelo y de bloques
                yield return Llegar(mapa, almacen, (Cabeza("ae-2").x, Cabeza("ae-2").y));
                EsconderLaColumna();
                foreach (var modelos in new[] { false, true })
                {
                    ModelosDeFiguras.Activos = modelos;
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"ta_coste_{(modelos ? "modelos" : "bloques")}");
                    var fot = new List<double>();
                    int llamadas = 0, conModelo = 0;
                    long tris = 0;
                    for (int i = 0; i < 180; i++)
                    {
                        yield return null;
                        fot.Add(Time.unscaledDeltaTime * 1000);
                        llamadas = Math.Max(llamadas, FigurasDelMapa.Llamadas + ModelosDeFiguras.Llamadas);
                        conModelo = Math.Max(conModelo, ModelosDeFiguras.Instancias);
                        tris = Math.Max(tris, ModelosDeFiguras.TriangulosPintados);
                    }
                    fot.Sort();
                    Debug.Log($"[Coste Aeda] habitual · {(modelos ? "con modelos" : "de bloques")}: fotograma mediana {fot[fot.Count / 2]:0.00} ms (p95 {fot[fot.Count * 95 / 100]:0.00}); " +
                              $"figuras con modelo {conModelo}, llamadas de figuras {llamadas}, triángulos de modelos {tris}");
                }
            }
            finally
            {
                (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, ModelosDeFiguras.Activos) = antes;
                MapaDeMundo.PintarMiColumna = true;
                ModelosDeFiguras.PixelesMinimos = 14;
                ModelosDeFiguras.Reiniciar();
            }
        }
    }
}
