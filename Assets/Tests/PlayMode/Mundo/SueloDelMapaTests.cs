// El suelo del mapa de mundo (`Conquest/MapaSuelo`) en sitios de verdad del mundo de un servidor de prueba (`BRONZEAGE_PRUEBA_URL`): la costa, un
// llano seco, uno fértil, el borde de un bosque, las colinas, una montaña con roca en la pendiente y el pico más alto (con nieve), cada uno de
// cerca, a media altura y desde arriba; el borde de la niebla de guerra, la capa de fertilidad y una junta entre teselas de distinto paso. Los
// sitios se buscan en los rasters del artefacto y en el relieve. Comprueba lo que se puede comprobar con un píxel (la nieve es blanca, el llano
// fértil más verde que el seco, las teselas llevan su faldón) y, con `MUNDO_CAPTURAS`, guarda las capturas.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo;
using ConquestTactics.Mundo.Mapa;
using Google.Protobuf;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.UI.Tests
{
    public class SueloDelMapaTests
    {
        GameObject go;
        RenderTexture rt;
        string cache;

        [TearDown]
        public void Limpiar()
        {
            if (go != null) UnityEngine.Object.Destroy(go);
            rt?.Release();
            if (cache != null && Directory.Exists(cache)) Directory.Delete(cache, true);
        }

        static T Esperar<T>(Task<T> t)
        {
            var fin = DateTime.UtcNow.AddSeconds(60);
            while (!t.IsCompleted) { if (DateTime.UtcNow > fin) Assert.Fail("sin respuesta"); System.Threading.Thread.Sleep(10); }
            return t.GetAwaiter().GetResult();
        }

        static Punto Pu(double x, double y) => new Punto { X = (long)(x * 1000), Y = (long)(y * 1000) };

        [Category("Mapa")]
        [UnityTest, Timeout(600_000)]
        public IEnumerator ElSueloEnCadaSitio()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");

            // El mundo de verdad: una cuenta nueva de prueba solo para saber qué mundo y qué reglas tiene el servidor.
            var acceso = new Acceso(url);
            var nick = "suelo-" + DateTime.UtcNow.Ticks % 100_000_000;
            Esperar(acceso.Registrar(nick, "clave-de-prueba-1"));
            var login = Esperar(acceso.Entrar(nick, "clave-de-prueba-1"));
            var s = new Sesion(acceso.UrlSesion, login.Token, "conquest-prueba");
            var abrir = s.Abrir();
            var hasta = Time.realtimeSinceStartup + 20;
            while (!abrir.IsCompleted && Time.realtimeSinceStartup < hasta) { s.Bombear(); yield return null; }
            var mundo = abrir.Result.Mundo;
            s.Cerrar();
            acceso.Anular(login.Token);
            cache = Path.Combine(Path.GetTempPath(), "conquest-prueba-suelo-" + Guid.NewGuid().ToString("N"));
            var datos = Esperar(DatosDeJuego.Cargar(acceso, mundo, new CacheDeUno(Path.Combine(cache, "datos"))));
            var artefacto = Esperar(MundoEnDisco.CargarArtefacto(acceso, mundo, new CacheDeUno(Path.Combine(cache, "mundo"))));
            var relieve = new Relieve(Esperar(MundoEnDisco.CargarRelieve(acceso, mundo, new CacheDeUno(Path.Combine(cache, "relieve")))));

            var sitios = BuscarSitios(relieve, artefacto, datos);
            foreach (var kv in sitios) Debug.Log($"[Suelo] sitio {kv.Key}: ({kv.Value.x:0.0}, {kv.Value.y:0.0}), {relieve.AlturaEn(kv.Value.x, kv.Value.y):0} m");

            var (almacen, sesion) = Almacen(url, sitios["llano_seco"], null);
            var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
            yield return Llegar(mapa, almacen, sitios["llano_seco"]);

            // El material es el nuevo y cada tesela lleva su faldón (4 lados de n − 1 tramos, dos triángulos cada uno).
            var teselas = go.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("tesela")).ToList();
            Assert.Greater(teselas.Count, 0);
            Assert.AreEqual("Conquest/MapaSuelo", teselas[0].sharedMaterial.shader.name);
            foreach (var t in teselas)
            {
                var m = t.GetComponent<MeshFilter>().sharedMesh;
                int n = (int)Math.Round(-2 + Math.Sqrt(4 + m.vertexCount));
                Assert.AreEqual(n * n + 4 * n, m.vertexCount, t.name);
                Assert.AreEqual(2 * (n - 1) * (n - 1) + 8 * (n - 1), m.triangles.Length / 3, t.name);
            }

            // Rendimiento quieto, con el suelo cargado.
            yield return Medir(mapa, "quieto en el llano");

            var colores = new Dictionary<string, Color>();
            foreach (var kv in sitios)
            {
                yield return Llegar(mapa, almacen, kv.Value);
                // Cerca (cámara baja), a media altura y desde arriba.
                yield return Vista(mapa, ZoomDelMapa.Cerca, 15, $"{kv.Key}_1_cerca");
                yield return Vista(mapa, ZoomDelMapa.Normal, 35, $"{kv.Key}_2_media");
                colores[kv.Key] = Centro();
                yield return Vista(mapa, ZoomDelMapa.Alejado, 90, $"{kv.Key}_3_arriba");
            }
            Debug.Log("[Suelo] centro de la vista media: " + string.Join("; ", colores.Select(kv => $"{kv.Key} {kv.Value}")));
            var nieve = colores["pico_nieve"];
            Assert.Greater(Mathf.Min(nieve.r, nieve.g, nieve.b), 0.55f, $"el pico más alto, con nieve: {nieve}");
            Assert.Greater(nieve.b, nieve.r - 0.02f, $"la nieve no es ocre: {nieve}");
            float Verdor(Color c) => c.g / Mathf.Max(0.01f, c.r);
            Assert.Greater(Verdor(colores["llano_fertil"]), Verdor(colores["llano_seco"]) + 0.05f,
                $"el llano fértil, más verde que el seco: {colores["llano_fertil"]} frente a {colores["llano_seco"]}");

            // Las juntas entre teselas de distinto paso: mirando lejos, con la cámara baja (las teselas cambian de paso a 80 y 160 u), sobre las
            // colinas y sobre la montaña.
            yield return Llegar(mapa, almacen, sitios["colinas"]);
            yield return Vista(mapa, ZoomDelMapa.Alejado, 10, "junta_de_teselas_colinas");
            yield return Llegar(mapa, almacen, sitios["montana_roca"]);
            mapa.Camara.Giro = 3.5;
            yield return Vista(mapa, ZoomDelMapa.Alejado, 12, "junta_de_teselas_montana");
            mapa.Camara.Giro = 0;

            // El cielo y la bruma: el horizonte con la cámara baja en el llano y en la costa, y el cénit (otra cámara en el mismo sitio, mirando
            // arriba: la del mapa nunca mira por encima del horizonte).
            yield return Llegar(mapa, almacen, sitios["llano_fertil"]);
            yield return Vista(mapa, ZoomDelMapa.Alejado, 10, "cielo_horizonte_llano");
            yield return Cenit(mapa, "cielo_cenit");
            yield return Llegar(mapa, almacen, sitios["costa"]);
            mapa.Camara.Giro = Math.PI;
            yield return Vista(mapa, ZoomDelMapa.Alejado, 10, "cielo_horizonte_costa");
            mapa.Camara.Giro = 0;

            // La capa de fertilidad, como hoy: la rampa de colores en vez de las texturas.
            yield return Llegar(mapa, almacen, sitios["llano_fertil"]);
            mapa.Fertilidad = true;
            yield return Llegar(mapa, almacen, sitios["llano_fertil"]);
            yield return Vista(mapa, ZoomDelMapa.Normal, 35, "capa_fertilidad");
            mapa.Fertilidad = false;

            // El borde de la niebla: otro mapa con la VISTA llevando niebla (explorado a 25 u, a la vista a 12 u de la columna).
            UnityEngine.Object.Destroy(go);
            yield return null;
            var centro = sitios["borde_bosque"];
            (almacen, sesion) = Almacen(url, centro, Niebla(relieve, centro, 25, 12));
            mapa = Montar(almacen, sesion, relieve, artefacto, datos);
            yield return Llegar(mapa, almacen, centro);
            yield return Vista(mapa, ZoomDelMapa.Cerca, 20, "niebla_borde_cerca");
            yield return Vista(mapa, ZoomDelMapa.Normal, 35, "niebla_explorado_sin_ver");
            yield return Vista(mapa, ZoomDelMapa.Alejado, 50, "niebla_borde");
            yield return Vista(mapa, ZoomDelMapa.Alejado, 90, "niebla_borde_arriba");
            var (tris, sinFaldon) = Triangulos();
            var (construidas, ms) = mapa.ConstruccionDelSuelo;
            Debug.Log($"[Suelo] {mapa.TeselasCargadas} teselas, {tris} triángulos ({sinFaldon} sin faldones); {construidas} teselas construidas en {ms:0} ms ({ms / Math.Max(1, construidas):0.0} ms cada una)");
        }

        (Almacen, Sesion) Almacen(string url, (double x, double y) donde, NieblaProyectada niebla)
        {
            var almacen = new Almacen();
            var heroe = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoHeroe, Id = "heroe-1" }, Version = 1 };
            heroe.Entidades.Add(new Entidad { Heroe = new Heroe { Id = "heroe-1", DisplayName = "Prueba", FaccionId = "f-1", Ubicacion = new UbicacionHeroe { EjercitoId = "ej-1" } } });
            var propia = new Ejercito { Id = "ej-1", FaccionId = "f-1", Tipo = TipoColumna.ColumnaEjercito, Posicion = Pu(donde.x, donde.y), LiderId = "heroe-1" };
            propia.Participantes.Add(new Participante { HeroeId = "heroe-1" });
            heroe.Entidades.Add(new Entidad { Ejercito = propia });
            almacen.Aplicar(new MensajeDeServidor { Foto = heroe });
            if (niebla != null)
                almacen.Aplicar(new MensajeDeServidor { Foto = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 1, Niebla = niebla } });
            return (almacen, new Sesion(url, "", "conquest-prueba", almacen));
        }

        MapaDeMundo Montar(Almacen almacen, Sesion sesion, Relieve relieve, ArtefactoDeMundo artefacto, DatosDeJuego datos)
        {
            go = new GameObject("Mapa");
            var mapa = go.AddComponent<MapaDeMundo>();
            mapa.RadioVisionFijo = 30; // la visión de un ejército: el encuadre más grande del juego
            mapa.Iniciar(sesion, relieve, artefacto, datos, () => "heroe-1", _ => false, (t, p) => { }, null);
            if (rt == null) rt = new RenderTexture(1600, 900, 24);
            mapa.Vista.targetTexture = rt;
            return mapa;
        }

        static IEnumerator Llegar(MapaDeMundo mapa, Almacen almacen, (double x, double y) donde)
        {
            almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito.Posicion = Pu(donde.x, donde.y);
            yield return null;
            var fin = Time.realtimeSinceStartup + 90;
            while ((mapa.Centro == null || !mapa.SueloCompleto) && Time.realtimeSinceStartup < fin) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
        }

        Texture2D ultima;

        IEnumerator Vista(MapaDeMundo mapa, ZoomDelMapa zoom, double inclinacion, string nombre)
        {
            mapa.Camara.Zoom = zoom;
            mapa.Camara.Inclinacion = inclinacion;
            var fin = Time.realtimeSinceStartup + 60;
            yield return null;
            while (!mapa.SueloCompleto && Time.realtimeSinceStartup < fin) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
            if (ultima != null) UnityEngine.Object.Destroy(ultima);
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            ultima = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            ultima.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            ultima.Apply();
            RenderTexture.active = antes;
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            if (string.IsNullOrEmpty(carpeta)) yield break;
            Directory.CreateDirectory(carpeta);
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), ultima.EncodeToPNG());
        }

        /// <summary>Una captura desde la cámara del mapa pero mirando 50° por encima del horizonte (para ver el cielo): el mapa se para un
        /// momento (si no, su Update vuelve a colocar la cámara) y se gira su cámara.</summary>
        IEnumerator Cenit(MapaDeMundo mapa, string nombre)
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            if (string.IsNullOrEmpty(carpeta)) yield break;
            mapa.enabled = false;
            var t0 = mapa.Vista.transform;
            var giro = t0.rotation;
            t0.rotation = Quaternion.Euler(-50, giro.eulerAngles.y + 160, 0);
            yield return null;
            yield return null;
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = antes;
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
            t0.rotation = giro;
            mapa.enabled = true;
        }

        /// <summary>El color medio de un cuadro en el centro de la última captura, algo por debajo de la columna (que tapa el centro justo).</summary>
        Color Centro()
        {
            var c = Color.black;
            int n = 0;
            for (int y = ultima.height / 2 - 160; y < ultima.height / 2 - 60; y += 4)
                for (int x = ultima.width / 2 - 200; x < ultima.width / 2 + 200; x += 4) { c += ultima.GetPixel(x, y); n++; }
            return c / n;
        }

        static IEnumerator Medir(MapaDeMundo mapa, string que)
        {
            var tiempos = new List<float>();
            for (int i = 0; i < 300; i++) { yield return null; tiempos.Add(Time.unscaledDeltaTime * 1000); }
            tiempos.Sort();
            Debug.Log($"[Suelo] {que}: {mapa.TeselasCargadas} teselas; fotograma mediana {tiempos[150]:0.0} ms, p95 {tiempos[285]:0.0} ms, peor {tiempos[299]:0.0} ms");
        }

        (int tris, int sinFaldon) Triangulos()
        {
            int tris = 0, sin = 0;
            foreach (var f in go.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("tesela")))
            {
                var m = f.sharedMesh;
                int n = (int)Math.Round(-2 + Math.Sqrt(4 + m.vertexCount));
                tris += (int)(m.GetIndexCount(0) / 3);
                sin += 2 * (n - 1) * (n - 1);
            }
            return (tris, sin);
        }

        /// <summary>Una niebla de la VISTA (celdas de 5 u) con lo explorado y lo que se ve en círculos alrededor de `centro`.</summary>
        static NieblaProyectada Niebla(Relieve relieve, (double x, double y) centro, double explorado, double visible)
        {
            const double celda = 5;
            int columnas = (int)Math.Ceiling(relieve.Columnas * relieve.Paso / celda), filas = (int)Math.Ceiling(relieve.Filas * relieve.Paso / celda);
            var celdas = new byte[(columnas * filas + 7) / 8];
            var visibles = new byte[celdas.Length];
            for (int j = 0; j < filas; j++)
                for (int i = 0; i < columnas; i++)
                {
                    double dx = (i + 0.5) * celda - centro.x, dy = (j + 0.5) * celda - centro.y, d = Math.Sqrt(dx * dx + dy * dy);
                    int k = j * columnas + i;
                    if (d <= explorado) celdas[k >> 3] |= (byte)(1 << (k & 7));
                    if (d <= visible) visibles[k >> 3] |= (byte)(1 << (k & 7));
                }
            return new NieblaProyectada { TamanoCelda = (uint)(celda * 1000), Columnas = (uint)columnas, Filas = (uint)filas, Celdas = ByteString.CopyFrom(celdas), Visibles = ByteString.CopyFrom(visibles) };
        }

        /// <summary>Los sitios, buscados en los rasters del artefacto (una muestra por celda de 4 u, tesela a tesela del relieve para no abrir
        /// cada tesela muchas veces) lejos del borde del mapa.</summary>
        static Dictionary<string, (double x, double y)> BuscarSitios(Relieve relieve, ArtefactoDeMundo artefacto, DatosDeJuego datos)
        {
            var fert = new RasterDeMundo(artefacto.Fertilidad);
            var hum = new RasterDeMundo(artefacto.Humedad);
            var bos = new RasterDeMundo(artefacto.DensidadBosque);
            double U(string k) => datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION." + k) * Relieve.MetrosAltura;
            double costa = U("umbralCosta"), colina = U("umbralColina"), montana = U("umbralMontana"), cima = U("umbralCima");
            var mejor = new Dictionary<string, (double puntos, double x, double y)>();
            void Probar(string k, double puntos, double x, double y)
            {
                if (!mejor.TryGetValue(k, out var m) || puntos > m.puntos) mejor[k] = (puntos, x, y);
            }
            double lado = relieve.LadoEnUnidades, margen = 120;
            for (int ty = 0; ty < relieve.TeselasY; ty++)
                for (int tx = 0; tx < relieve.TeselasX; tx++)
                    for (double y = ty * lado + 2; y < (ty + 1) * lado; y += 4)
                        for (double x = tx * lado + 2; x < (tx + 1) * lado; x += 4)
                        {
                            if (x < margen || y < margen || x > relieve.Columnas * relieve.Paso - margen || y > relieve.Filas * relieve.Paso - margen) continue;
                            if (relieve.AguaEn(x, y)) continue;
                            double h = relieve.AlturaEn(x, y);
                            double pend = Math.Max(Math.Abs(relieve.AlturaEn(x + 1, y) - relieve.AlturaEn(x - 1, y)), Math.Abs(relieve.AlturaEn(x, y + 1) - relieve.AlturaEn(x, y - 1)))
                                / (2 * EscalaDelMapa.MetrosPorUnidad);
                            double f = fert.En(x, y) / 255.0, u = hum.En(x, y) / 255.0, b = bos.En(x, y) / 255.0;
                            // Llanos parejos alrededor (8 u): lo mismo en las cuatro direcciones.
                            double fMin = Math.Min(Math.Min(fert.En(x + 8, y), fert.En(x - 8, y)), Math.Min(fert.En(x, y + 8), fert.En(x, y - 8))) / 255.0;
                            double fMax = Math.Max(Math.Max(fert.En(x + 8, y), fert.En(x - 8, y)), Math.Max(fert.En(x, y + 8), fert.En(x, y - 8))) / 255.0;
                            double uMax = Math.Max(Math.Max(hum.En(x + 8, y), hum.En(x - 8, y)), Math.Max(hum.En(x, y + 8), hum.En(x, y - 8))) / 255.0;
                            if (h > costa + 15 && h < colina - 20 && b < 0.05 && pend < 0.15)
                            {
                                Probar("llano_seco", -(fMax + uMax), x, y);
                                Probar("llano_fertil", Math.Min(fMin, u), x, y);
                            }
                            if (h > costa - 30 && h < costa - 5 && pend < 0.2 && (relieve.AguaEn(x + 6, y) || relieve.AguaEn(x - 6, y) || relieve.AguaEn(x, y + 6) || relieve.AguaEn(x, y - 6)))
                                Probar("costa", -Math.Abs(h - (costa - 18)), x, y);
                            if (h < colina && b > 0.7 && bos.En(x + 10, y) < 15)
                                Probar("borde_bosque", b - bos.En(x + 10, y) / 255.0, x, y);
                            if (h > colina + 10 && h < montana - 10)
                                Probar("colinas", Math.Min(pend, 0.25) - Math.Abs(h - (colina + montana) / 2) / 100, x, y);
                            if (h > montana + 10 && h < cima - 10)
                                Probar("montana_roca", pend, x, y);
                            Probar("pico_nieve", h, x, y);
                        }
            return mejor.ToDictionary(kv => kv.Key, kv => (kv.Value.x, kv.Value.y));
        }
    }
}
