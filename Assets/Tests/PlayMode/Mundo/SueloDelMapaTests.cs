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

        DatosDeJuego pDatos;
        ArtefactoDeMundo pArtefacto;
        Relieve pRelieve;

        /// <summary>El mundo de verdad del servidor de prueba (cuenta nueva solo para leer el mundo): deja datos, artefacto y relieve en pDatos,
        /// pArtefacto y pRelieve.</summary>
        IEnumerator Preparar(string url, string prefijo)
        {
            var acceso = new Acceso(url);
            var nick = prefijo + DateTime.UtcNow.Ticks % 100_000_000;
            Esperar(acceso.Registrar(nick, "clave-de-prueba-1"));
            var login = Esperar(acceso.Entrar(nick, "clave-de-prueba-1"));
            var s = new Sesion(acceso.UrlSesion, login.Token, "conquest-prueba");
            var abrir = s.Abrir();
            var hasta = Time.realtimeSinceStartup + 20;
            while (!abrir.IsCompleted && Time.realtimeSinceStartup < hasta) { s.Bombear(); yield return null; }
            var mundo = abrir.Result.Mundo;
            s.Cerrar();
            acceso.Anular(login.Token);
            cache = Path.Combine(Path.GetTempPath(), "conquest-prueba-" + prefijo + Guid.NewGuid().ToString("N"));
            pDatos = Esperar(DatosDeJuego.Cargar(acceso, mundo, new CacheDeUno(Path.Combine(cache, "datos"))));
            pArtefacto = Esperar(MundoEnDisco.CargarArtefacto(acceso, mundo, new CacheDeUno(Path.Combine(cache, "mundo"))));
            pRelieve = new Relieve(Esperar(MundoEnDisco.CargarRelieve(acceso, mundo, new CacheDeUno(Path.Combine(cache, "relieve")))));
        }

        /// <summary>Los sitios de la vegetación, buscados en el raster de bosque y el relieve: una dehesa, un encinar denso, una ladera con el
        /// límite del arbolado y un bosque junto a la costa (y el borde de bosque de `BuscarSitios`).</summary>
        static Dictionary<string, (double x, double y)> SitiosDeVegetacion(Relieve relieve, ArtefactoDeMundo artefacto, DatosDeJuego datos)
        {
            var bos = new RasterDeMundo(artefacto.DensidadBosque);
            double U(string k) => datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION." + k) * Relieve.MetrosAltura;
            double costa = U("umbralCosta"), colina = U("umbralColina"), montana = U("umbralMontana");
            var mejor = new Dictionary<string, (double puntos, double x, double y)>();
            void Probar(string k, double puntos, double x, double y)
            {
                if (!mejor.TryGetValue(k, out var m) || puntos > m.puntos) mejor[k] = (puntos, x, y);
            }
            for (double y = 150; y < relieve.Filas * relieve.Paso - 150; y += 4)
                for (double x = 150; x < relieve.Columnas * relieve.Paso - 150; x += 4)
                {
                    double b = bos.En(x, y) / 255.0;
                    if (b < 0.25 || relieve.AguaEn(x, y)) continue;
                    double h = relieve.AlturaEn(x, y);
                    double pend = Math.Max(Math.Abs(relieve.AlturaEn(x + 1, y) - relieve.AlturaEn(x - 1, y)), Math.Abs(relieve.AlturaEn(x, y + 1) - relieve.AlturaEn(x, y - 1)))
                        / (2 * EscalaDelMapa.MetrosPorUnidad);
                    double vMin = 1, vMax = 0;
                    foreach (var (dx, dy) in new[] { (12, 0), (-12, 0), (0, 12), (0, -12), (8, 8), (-8, -8) })
                    {
                        double w = bos.En(x + dx, y + dy) / 255.0;
                        vMin = Math.Min(vMin, w); vMax = Math.Max(vMax, w);
                    }
                    if (h > costa + 15 && h < colina + 40)
                    {
                        if (b < 0.55 && vMin > 0.1 && vMax < 0.75) Probar("dehesa", -Math.Abs(b - 0.4) - (vMax - vMin) * 0.3, x, y);
                        if (b > 0.85) Probar("denso", vMin, x, y);
                    }
                    // La ladera: bosque en pendiente que se acaba subiendo (más arriba, roca o sin bosque).
                    if (b > 0.6 && h > colina && h < montana + 40 && pend > 0.12)
                    {
                        double arriba = 0;
                        foreach (var (dx, dy) in new[] { (10, 0), (-10, 0), (0, 10), (0, -10) })
                            if (relieve.AlturaEn(x + dx, y + dy) > h + 25 && bos.En(x + dx, y + dy) < 40) arriba = 1;
                        Probar("ladera", arriba + Math.Min(pend, 0.4), x, y);
                    }
                    // La costa: bosque a menos de 12 u del mar.
                    if (b > 0.45 && h < colina)
                    {
                        bool mar = relieve.AguaEn(x + 12, y) || relieve.AguaEn(x - 12, y) || relieve.AguaEn(x, y + 12) || relieve.AguaEn(x, y - 12);
                        if (mar) Probar("costa", b - Math.Abs(h - (costa + 25)) / 100, x, y);
                    }
                }
            var r = mejor.ToDictionary(kv => kv.Key, kv => (kv.Value.x, kv.Value.y));
            r["borde"] = BuscarSitios(relieve, artefacto, datos)["borde_bosque"];
            return r;
        }

        /// <summary>El reparto de la vegetación (`VegetacionDelMapa`, con las reglas del prototipo): la misma tesela del mismo mundo da las mismas
        /// plantas, y ninguna cae en el agua, en la cima, en la nieve ni en un cauce (la arena de la costa sí lleva árboles: el pinar de costa del
        /// prototipo). Cuenta cuántas plantas caen de cada especie.</summary>
        [Category("Mapa")]
        [UnityTest, Timeout(600_000)]
        public IEnumerator ElRepartoDeLaVegetacion()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "repart-");
            var relieve = pRelieve;
            float U(string k) => (float)(pDatos.Numero("documentos.Generacion_Del_Mundo.ELEVACION." + k) * Relieve.MetrosAltura);
            var umbrales = new Vector4(U("umbralCosta"), U("umbralColina"), U("umbralMontana"), U("umbralCima"));
            float nieve = umbrales.w + PinturaDelMapa.NieveSobreCima * ((float)Relieve.MetrosAltura - umbrales.w);
            var cauces = new CaucesDelMapa(pArtefacto, relieve);
            RasterDeMundo R(Raster r) => r != null && r.Encoding == EncodingRaster.EncodingU8 ? new RasterDeMundo(r) : null;
            VegetacionDelMapa Nueva() => new VegetacionDelMapa(relieve, R(pArtefacto.Fertilidad), R(pArtefacto.Humedad), R(pArtefacto.DensidadBosque), umbrales, nieve) { Cauces = cauces };
            var a = Nueva();
            var b = Nueva();
            var sitios = SitiosDeVegetacion(relieve, pArtefacto, pDatos);
            double lado = relieve.LadoEnUnidades, e = EscalaDelMapa.MetrosPorUnidad;
            int total = 0;
            foreach (var kv in sitios)
            {
                int tx = (int)(kv.Value.x / lado), ty = (int)(kv.Value.y / lado);
                var p1 = a.RepartirAhora(tx, ty);
                var p2 = b.RepartirAhora(tx, ty);
                var p3 = a.RepartirAhora(tx, ty);
                Assert.AreEqual(p1.Count, p2.Count, $"{kv.Key}: el mismo número de plantas");
                Assert.AreEqual(p1.Count, p3.Count, $"{kv.Key}: el mismo número de plantas, otra vez");
                for (int i = 0; i < p1.Count; i++)
                {
                    Assert.AreEqual(p1[i].Pos, p2[i].Pos, $"{kv.Key}: la planta {i}, en el mismo sitio");
                    Assert.AreEqual(p1[i].Esp, p2[i].Esp, $"{kv.Key}: la planta {i}, de la misma especie");
                    Assert.AreEqual(p1[i].Escala, p2[i].Escala, $"{kv.Key}: la planta {i}, del mismo tamaño");
                }
                foreach (var p in p1)
                {
                    double x = p.Pos.x / e, y = -p.Pos.z / e, h = relieve.AlturaEn(x, y);
                    Assert.IsFalse(relieve.AguaEn(x, y), $"{kv.Key}: ({x:0.0}, {y:0.0}) en el agua");
                    Assert.LessOrEqual(h, umbrales.w + 1, $"{kv.Key}: ({x:0.0}, {y:0.0}) en la cima ({h:0} m)");
                    Assert.Less(h, nieve - 38, $"{kv.Key}: ({x:0.0}, {y:0.0}) en la nieve ({h:0} m)");
                    Assert.GreaterOrEqual(cauces.ACauce(x, y), p.Mata ? 0.1 : 0.2, $"{kv.Key}: ({x:0.0}, {y:0.0}) en un cauce");
                }
                total += p1.Count;
                var porClase = p1.GroupBy(p => p.Clase).OrderBy(g => g.Key)
                    .Select(g => $"{VegetacionDelMapa.NombreDeClase(g.Key)} {g.Count()}");
                Debug.Log($"[Vegetacion] reparto de {kv.Key} (tesela {tx}, {ty}): {p1.Count} plantas, iguales las tres veces; " + string.Join(", ", porClase));
                yield return null;
            }
            Assert.Greater(total, 100, "hay vegetación en los sitios de bosque");
            a.Destruir();
            b.Destruir();
        }

        /// <summary>La vegetación (`Conquest/MapaVegetacion`) en el mapa, para las capturas de revisión (no comprueba nada: `MUNDO_CAPTURAS`): una
        /// dehesa, un encinar denso, el borde de un bosque, una ladera con el límite del arbolado y un bosque junto a la costa a los tres zooms
        /// (con la visión de un héroe, 15 u); el denso de cerca con la cámara baja; una plaza y una aldea junto al bosque (el tamaño); y una encina y
        /// un olivo sueltos a 35 m y 30°, para comparar con el render de Blender. Con MSAA 4× como el juego. Mide los fotogramas al cargar
        /// el bosque denso (llegando de lejos) y ya cargado.</summary>
        [Category("Mapa"), Explicit("capturas de revisión de la vegetación")]
        [UnityTest, Timeout(1_200_000)]
        public IEnumerator LaVegetacionEnElMapa()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "veget-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            // La copa (u) con que se reparte y se escala: `VEGETACION_COPA` (0,5 la del prototipo).
            var copaAntes = VegetacionDelMapa.Copa;
            if (double.TryParse(Environment.GetEnvironmentVariable("VEGETACION_COPA"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var copa))
                VegetacionDelMapa.Copa = copa;
            Debug.Log($"[Vegetacion] copa {VegetacionDelMapa.Copa:0.00} u, escala de los modelos {VegetacionDelMapa.Tamano:0.00}");
            var sitios = SitiosDeVegetacion(relieve, artefacto, datos);
            var bos = new RasterDeMundo(artefacto.DensidadBosque);
            foreach (var kv in sitios) Debug.Log($"[Vegetacion] sitio {kv.Key}: ({kv.Value.x:0.0}, {kv.Value.y:0.0}), bosque {bos.En(kv.Value.x, kv.Value.y) / 255.0:0.00}, {relieve.AlturaEn(kv.Value.x, kv.Value.y):0} m");

            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var (almacen, sesion) = Almacen(url, sitios["dehesa"], null);
            var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
            mapa.RadioVisionFijo = 15; // la visión de un héroe: los zooms de 132, 221 y 324 m
            foreach (var n in new[] { "dehesa", "denso", "borde", "ladera", "costa" })
            {
                if (!sitios.ContainsKey(n)) continue;
                yield return Llegar(mapa, almacen, sitios[n]);
                yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"veg_{n}_1_cerca");
                yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"veg_{n}_2_habitual");
                yield return Vista(mapa, ZoomDelMapa.Alejado, 30, $"veg_{n}_3_alejado");
            }
            // Una plaza y una aldea junto al borde del bosque: el tamaño de los árboles al lado (y los claros, que solo rehacen su tesela).
            var borde = sitios["borde"];
            mapa.Adicionales.Add(new CosaDelMapa { Clase = ClaseDeCosa.Plaza, Id = "plaza-prueba", Nombre = "Plaza de prueba", X = borde.x + 4, Y = borde.y + 2, Radio = 1.5, Alto = 1 });
            mapa.Adicionales.Add(new CosaDelMapa { Clase = ClaseDeCosa.Aldea, Id = "aldea-prueba", Nombre = "Aldea de prueba", X = borde.x - 5, Y = borde.y - 3, Radio = 0.8, Alto = 0.6 });
            mapa.RehacerRayas();
            yield return Llegar(mapa, almacen, borde);
            yield return Vista(mapa, ZoomDelMapa.Cerca, 30, "veg_plaza_1_cerca");
            yield return Vista(mapa, ZoomDelMapa.Normal, 30, "veg_plaza_2_habitual");
            mapa.Adicionales.Clear();

            // Al cargar: desde la dehesa al denso, midiendo mientras se carga.
            yield return Llegar(mapa, almacen, sitios.TryGetValue("costa", out var lejos) ? lejos : sitios["dehesa"]);
            almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito.Posicion = Pu(sitios["denso"].x, sitios["denso"].y);
            mapa.Camara.Zoom = ZoomDelMapa.Normal;
            yield return Medir(mapa, "llegando al bosque denso (cargando)");
            yield return Llegar(mapa, almacen, sitios["denso"]);
            yield return Vista(mapa, ZoomDelMapa.Cerca, 15, "veg_denso_4_cerca_baja");
            yield return Vista(mapa, ZoomDelMapa.Normal, 30, "veg_denso_5_habitual_otra_vez");
            yield return Medir(mapa, "en el bosque denso, zoom habitual (cargado)");
            var v = mapa.Vegetacion;
            Debug.Log($"[Vegetacion] {v.Plantas} plantas cargadas, {v.Dibujadas} dibujadas, {v.Triangulos} triángulos, {v.Llamadas} llamadas; "
                + $"{v.Repartidas} teselas repartidas en {v.MsRepartiendo:0} ms de hilo (+{v.MsMuestreando:0} ms de muestreo en el principal, el peor fotograma {v.PeorMsMuestreando:0.0} ms); "
                + $"peor Actualizar {v.PeorMsActualizar:0.0} ms, peor Dibujar {v.PeorMsDibujar:0.0} ms, peor tesela de suelo {mapa.PeorMsTeselaDeSuelo:0.0} ms");

            // A/B con Blender: una encina y un olivo sueltos (variante 1, de cerca), a 35 m y 30°, con la luz del mapa; muy por debajo del suelo
            // (la misma x, z: la niebla de guerra de alrededor de la columna) y con fondo del color del horizonte.
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            if (!string.IsNullOrEmpty(carpeta))
            {
                var centro = EscalaDelMapa.AEscena(sitios["denso"].x, sitios["denso"].y, -4000);
                var ab = new RenderTexture(1200, 900, 24) { antiAliasing = 4 };
                var cam = new GameObject("camara A/B").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = PinturaDelMapa.Horizonte;
                cam.fieldOfView = 35;
                cam.nearClipPlane = 1;
                cam.farClipPlane = 300;
                cam.targetTexture = ab;
                cam.enabled = false;
                foreach (var especie in new[] { "encina", "olivo" })
                {
                    var (malla, mats) = v.Pieza(especie, 0, 0);
                    var go = new GameObject("A/B " + especie);
                    go.transform.position = centro;
                    go.AddComponent<MeshFilter>().sharedMesh = malla;
                    go.AddComponent<MeshRenderer>().sharedMaterials = mats;
                    var alto = malla.bounds.size.y;
                    var mira = centro + Vector3.up * alto * 0.45f;
                    cam.transform.position = mira + new Vector3(0, Mathf.Sin(30 * Mathf.Deg2Rad), -Mathf.Cos(30 * Mathf.Deg2Rad)) * 35;
                    cam.transform.LookAt(mira);
                    yield return null;
                    cam.Render();
                    var antes = RenderTexture.active;
                    RenderTexture.active = ab;
                    var t = new Texture2D(ab.width, ab.height, TextureFormat.RGB24, false);
                    t.ReadPixels(new Rect(0, 0, ab.width, ab.height), 0, 0);
                    t.Apply();
                    RenderTexture.active = antes;
                    File.WriteAllBytes(Path.Combine(carpeta, $"ab_{especie}_unity.png"), t.EncodeToPNG());
                    UnityEngine.Object.Destroy(t);
                    UnityEngine.Object.Destroy(go);
                }
                UnityEngine.Object.Destroy(cam.gameObject);
                ab.Release();
            }
            VegetacionDelMapa.Copa = copaAntes;
        }

        /// <summary>Cada especie en su zona, para las capturas de revisión (`MUNDO_CAPTURAS`), a zoom habitual y de cerca: la ribera con álamos y
        /// cañas, la costa con palmeras y pinos, la montaña con abetos, la colina con algún ciprés, el monte bajo con retama y una rambla con
        /// adelfas. Los sitios los busca repartiendo una de cada dos teselas del mundo y contando cada especie en ventanas de 6 u.</summary>
        [Category("Mapa"), Explicit("capturas de revisión de las especies")]
        [UnityTest, Timeout(2_400_000)]
        public IEnumerator LasEspeciesEnElMapa()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "especies-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            float U(string k) => (float)(datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION." + k) * Relieve.MetrosAltura);
            var umbrales = new Vector4(U("umbralCosta"), U("umbralColina"), U("umbralMontana"), U("umbralCima"));
            float nieve = umbrales.w + PinturaDelMapa.NieveSobreCima * ((float)Relieve.MetrosAltura - umbrales.w);
            RasterDeMundo R(Raster r) => r != null && r.Encoding == EncodingRaster.EncodingU8 ? new RasterDeMundo(r) : null;
            var cuenta = new Dictionary<(int, int), int[]>();
            yield return ContarEspecies(relieve, artefacto, umbrales, nieve, cuenta);
            int N(int[] c, EspecieDeArbol x) => c[(int)x];
            int Arboles(int[] c) => c.Take(7).Sum();
            var buscar = new (string nombre, Func<int[], double> nota)[]
            {
                ("ribera", c => N(c, EspecieDeArbol.Alamo) > 0 && c[VegetacionDelMapa.Canas] > 0 ? Math.Min(N(c, EspecieDeArbol.Alamo), 8) + Math.Min(c[VegetacionDelMapa.Canas], 12) : -1),
                ("costa", c => N(c, EspecieDeArbol.Palmera) > 0 && N(c, EspecieDeArbol.Pino) > 0 ? Math.Min(N(c, EspecieDeArbol.Palmera), 6) * 2 + Math.Min(N(c, EspecieDeArbol.Pino), 8) : -1),
                ("montana", c => N(c, EspecieDeArbol.Abeto) > 0 ? Math.Min(N(c, EspecieDeArbol.Abeto), 20) + 0.1 * Arboles(c) : -1),
                ("colina", c => N(c, EspecieDeArbol.Cipres) > 0 ? Math.Min(N(c, EspecieDeArbol.Cipres), 3) * 3 + Math.Min(Arboles(c), 20) : -1),
                ("matorral", c => c[VegetacionDelMapa.Retama] > 0 ? Math.Min(c[VegetacionDelMapa.Retama], 30) - 0.5 * Arboles(c) : -1),
                ("rambla", c => c[VegetacionDelMapa.Adelfa] > 0 ? Math.Min(c[VegetacionDelMapa.Adelfa], 20) + 0.2 * Math.Min(Arboles(c), 10) : -1),
            };
            var sitios = MejoresSitios(cuenta, buscar);
            Assert.IsNotEmpty(sitios, "algún sitio con las especies nuevas");

            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var (almacen, sesion) = Almacen(url, (sitios[0].x, sitios[0].y), null);
            var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
            mapa.RadioVisionFijo = 15;
            MapaDeMundo.AclararCopas = false; // que el hueco del héroe no tape la especie
            try
            {
                foreach (var (nombre, x, y) in sitios)
                {
                    yield return Llegar(mapa, almacen, (x, y));
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"esp_{nombre}_1_habitual");
                    yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"esp_{nombre}_2_cerca");
                }
            }
            finally { MapaDeMundo.AclararCopas = true; }
        }

        const double VentanaDeEspecies = 6;

        /// <summary>Reparte una de cada dos teselas del mundo (con la capa baja) y cuenta cada clase de planta en ventanas de 6 u.</summary>
        static IEnumerator ContarEspecies(Relieve relieve, ArtefactoDeMundo artefacto, Vector4 umbrales, float nieve, Dictionary<(int, int), int[]> cuenta)
        {
            RasterDeMundo R(Raster r) => r != null && r.Encoding == EncodingRaster.EncodingU8 ? new RasterDeMundo(r) : null;
            var v = new VegetacionDelMapa(relieve, R(artefacto.Fertilidad), R(artefacto.Humedad), R(artefacto.DensidadBosque), umbrales, nieve)
                { Cauces = new CaucesDelMapa(artefacto, relieve) };
            double lado = relieve.LadoEnUnidades, e = EscalaDelMapa.MetrosPorUnidad;
            var total = new int[Clases];
            int nx = (int)(relieve.Columnas * relieve.Paso / lado), ny = (int)(relieve.Filas * relieve.Paso / lado);
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            for (int ty = 1; ty < ny - 1; ty += 2)
            {
                for (int tx = 1 + (ty / 2) % 2; tx < nx - 1; tx += 2)
                    foreach (var pl in v.RepartirAhora(tx, ty))
                    {
                        var k = ((int)(pl.Pos.x / e / VentanaDeEspecies), (int)(-pl.Pos.z / e / VentanaDeEspecies));
                        if (!cuenta.TryGetValue(k, out var c)) cuenta[k] = c = new int[Clases];
                        c[pl.Clase]++;
                        total[pl.Clase]++;
                    }
                yield return null;
            }
            Debug.Log($"[Especies] {nx}×{ny} teselas, la mitad repartidas en {reloj.ElapsedMilliseconds} ms; "
                + string.Join(", ", Enumerable.Range(0, Clases).Select(i => $"{VegetacionDelMapa.NombreDeClase((byte)i)} {total[i]}")));
        }

        const int Clases = VegetacionDelMapa.Piedra + 1;

        /// <summary>Para cada búsqueda, la ventana de mejor nota (las de nota negativa no valen).</summary>
        static List<(string nombre, double x, double y)> MejoresSitios(Dictionary<(int, int), int[]> cuenta, (string nombre, Func<int[], double> nota)[] buscar)
        {
            var sitios = new List<(string nombre, double x, double y)>();
            foreach (var (nombre, nota) in buscar)
            {
                var mejor = cuenta.Where(kv => nota(kv.Value) >= 0).OrderByDescending(kv => nota(kv.Value)).ThenBy(kv => kv.Key.Item1).ThenBy(kv => kv.Key.Item2).FirstOrDefault();
                if (mejor.Value == null) { Debug.Log($"[Especies] sin sitio para {nombre}"); continue; }
                double x = (mejor.Key.Item1 + 0.5) * VentanaDeEspecies, y = (mejor.Key.Item2 + 0.5) * VentanaDeEspecies;
                sitios.Add((nombre, x, y));
                Debug.Log($"[Especies] {nombre}: ({x:0}, {y:0}): " + string.Join(", ", Enumerable.Range(0, Clases).Where(i => mejor.Value[i] > 0).Select(i => $"{VegetacionDelMapa.NombreDeClase((byte)i)} {mejor.Value[i]}")));
            }
            return sitios;
        }

        /// <summary>La capa baja (hierba, flores y piedras, `matas.ts` del prototipo) y las matas, para las capturas de revisión (`MUNDO_CAPTURAS`):
        /// el monte bajo, una pradera fértil con flores, una ladera con piedras, una rambla con adelfas y una ribera con álamos, a zoom habitual y de
        /// cerca; la pradera y la ladera también sin la capa baja. Mide el fotograma en la pradera con y sin ella.</summary>
        [Category("Mapa"), Explicit("capturas de revisión de la capa baja")]
        [UnityTest, Timeout(2_400_000)]
        public IEnumerator LaCapaBajaEnElMapa()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "capa-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            float U(string k) => (float)(datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION." + k) * Relieve.MetrosAltura);
            var umbrales = new Vector4(U("umbralCosta"), U("umbralColina"), U("umbralMontana"), U("umbralCima"));
            float nieve = umbrales.w + PinturaDelMapa.NieveSobreCima * ((float)Relieve.MetrosAltura - umbrales.w);
            var cuenta = new Dictionary<(int, int), int[]>();
            yield return ContarEspecies(relieve, artefacto, umbrales, nieve, cuenta);
            int N(int[] c, EspecieDeArbol x) => c[(int)x];
            int Arboles(int[] c) => c.Take(7).Sum();
            const int H = VegetacionDelMapa.Hierba, F = VegetacionDelMapa.Flor, P = VegetacionDelMapa.Piedra;
            var sitios = MejoresSitios(cuenta, new (string, Func<int[], double>)[]
            {
                ("monte_bajo", c => c[VegetacionDelMapa.Retama] > 0 ? Math.Min(c[VegetacionDelMapa.Retama], 40) + Math.Min(c[VegetacionDelMapa.Lentisco], 30) - 0.5 * Arboles(c) : -1),
                ("pradera", c => c[F] > 0 && c[H] > 0 ? Math.Min(c[H], 200) + 4 * Math.Min(c[F], 40) - 2 * Arboles(c) - c[P] : -1),
                // una ladera con piedras y algo de hierba (no un cantil: la cámara se mete en la roca)
                ("ladera", c => c[P] > 0 && c[H] > 10 ? Math.Min(c[P], 60) + 0.5 * Math.Min(c[H], 60) : -1),
                ("rambla", c => c[VegetacionDelMapa.Adelfa] > 0 ? Math.Min(c[VegetacionDelMapa.Adelfa], 20) + 0.2 * Math.Min(Arboles(c), 10) : -1),
                ("ribera", c => N(c, EspecieDeArbol.Alamo) > 0 && c[VegetacionDelMapa.Canas] > 0 ? Math.Min(N(c, EspecieDeArbol.Alamo), 8) + Math.Min(c[VegetacionDelMapa.Canas], 12) : -1),
                ("costa", c => N(c, EspecieDeArbol.Palmera) > 0 && N(c, EspecieDeArbol.Pino) > 0 ? Math.Min(N(c, EspecieDeArbol.Palmera), 6) * 2 + Math.Min(N(c, EspecieDeArbol.Pino), 8) : -1),
            });
            Assert.IsNotEmpty(sitios, "algún sitio para la capa baja");
            var pradera = sitios.FirstOrDefault(q => q.nombre == "pradera");

            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            MapaDeMundo.AclararCopas = false; // que el hueco del héroe no tape lo que se mira
            try
            {
                foreach (var con in new[] { false, true })
                {
                    VegetacionDelMapa.CapaBaja = con;
                    if (go != null) { UnityEngine.Object.Destroy(go); yield return null; }
                    var (almacen, sesion) = Almacen(url, (sitios[0].x, sitios[0].y), null);
                    var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                    mapa.RadioVisionFijo = 15;
                    foreach (var (nombre, x, y) in sitios)
                    {
                        if (!con && nombre != "pradera" && nombre != "ladera") continue;
                        yield return Llegar(mapa, almacen, (x, y));
                        yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"capa_{nombre}_{(con ? "1_habitual" : "0_sin_capa")}");
                        if (con) yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"capa_{nombre}_2_cerca");
                    }
                    if (pradera.nombre == null) continue;
                    yield return Llegar(mapa, almacen, (pradera.x, pradera.y));
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"capa_pradera_medir_{(con ? "con" : "sin")}");
                    yield return Medir(mapa, $"en la pradera fértil, zoom habitual, {(con ? "con" : "sin")} la capa baja");
                }
            }
            finally { MapaDeMundo.AclararCopas = true; VegetacionDelMapa.CapaBaja = true; }
        }

        /// <summary>La vegetación, estado final: el coste (plantas, triángulos, llamadas, mediana y p95) en el bosque denso, la pradera fértil, la
        /// costa y la montaña a los tres zooms; los picos en marcha (el héroe cruzando de la pradera al bosque denso, fotograma a fotograma); y las
        /// capturas finales (`MUNDO_CAPTURAS`): cada paisaje a zoom habitual, dos panorámicas alejadas, el héroe en el bosque con el aclarado y
        /// el detalle de la hierba en la playa, las piedras de la ladera y la flor de la adelfa.</summary>
        [Category("Mapa"), Explicit("medidas y capturas del estado final de la vegetación")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator LaVegetacionEstadoFinal()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "final-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            float U(string k) => (float)(datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION." + k) * Relieve.MetrosAltura);
            var umbrales = new Vector4(U("umbralCosta"), U("umbralColina"), U("umbralMontana"), U("umbralCima"));
            float nieve = umbrales.w + PinturaDelMapa.NieveSobreCima * ((float)Relieve.MetrosAltura - umbrales.w);
            var cuenta = new Dictionary<(int, int), int[]>();
            yield return ContarEspecies(relieve, artefacto, umbrales, nieve, cuenta);
            int N(int[] c, EspecieDeArbol x) => c[(int)x];
            int Arboles(int[] c) => c.Take(7).Sum();
            const int H = VegetacionDelMapa.Hierba, F = VegetacionDelMapa.Flor, P = VegetacionDelMapa.Piedra;
            var lista = MejoresSitios(cuenta, new (string, Func<int[], double>)[]
            {
                ("ribera", c => N(c, EspecieDeArbol.Alamo) > 0 && c[VegetacionDelMapa.Canas] > 0 ? Math.Min(N(c, EspecieDeArbol.Alamo), 8) + Math.Min(c[VegetacionDelMapa.Canas], 12) : -1),
                ("costa", c => N(c, EspecieDeArbol.Palmera) > 0 && N(c, EspecieDeArbol.Pino) > 0 ? Math.Min(N(c, EspecieDeArbol.Palmera), 6) * 2 + Math.Min(N(c, EspecieDeArbol.Pino), 8) : -1),
                ("montana", c => N(c, EspecieDeArbol.Abeto) > 0 ? Math.Min(N(c, EspecieDeArbol.Abeto), 20) + 0.1 * Arboles(c) : -1),
                ("monte_bajo", c => c[VegetacionDelMapa.Retama] > 0 ? Math.Min(c[VegetacionDelMapa.Retama], 40) + Math.Min(c[VegetacionDelMapa.Lentisco], 30) - 0.5 * Arboles(c) : -1),
                ("pradera", c => c[F] > 0 && c[H] > 0 ? Math.Min(c[H], 200) + 4 * Math.Min(c[F], 40) - 2 * Arboles(c) - c[P] : -1),
                ("rambla", c => c[VegetacionDelMapa.Adelfa] > 0 ? Math.Min(c[VegetacionDelMapa.Adelfa], 20) + 0.2 * Math.Min(Arboles(c), 10) : -1),
                ("ladera", c => c[P] > 0 && c[H] > 10 ? Math.Min(c[P], 60) + 0.5 * Math.Min(c[H], 60) : -1),
                ("playa", c => c[H] > 0 && N(c, EspecieDeArbol.Palmera) > 0 ? c[H] : -1),
            });
            var sitios = lista.ToDictionary(q => q.nombre, q => (q.x, q.y));
            foreach (var kv in SitiosDeVegetacion(relieve, artefacto, datos)) sitios[kv.Key] = kv.Value; // denso y borde, de siempre

            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            if (go != null) { UnityEngine.Object.Destroy(go); yield return null; }
            var (almacen, sesion) = Almacen(url, sitios["denso"], null);
            var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
            mapa.RadioVisionFijo = 15;

            // 1. el coste, como se juega (con el aclarado)
            foreach (var zona in new[] { "denso", "pradera", "costa", "montana" })
            {
                if (!sitios.ContainsKey(zona)) continue;
                yield return Llegar(mapa, almacen, sitios[zona]);
                foreach (var (zoom, nombre) in new[] { (ZoomDelMapa.Cerca, "cerca"), (ZoomDelMapa.Normal, "habitual"), (ZoomDelMapa.Alejado, "alejado") })
                {
                    yield return Vista(mapa, zoom, 30, $"final_coste_{zona}_{nombre}");
                    yield return Medir(mapa, $"coste: {zona}, zoom {nombre}");
                }
            }

            // 2. los picos en marcha: de la pradera al bosque denso a 0,15 u por fotograma, a zoom habitual
            if (sitios.ContainsKey("pradera"))
            {
                mapa.Camara.Zoom = ZoomDelMapa.Normal;
                yield return Llegar(mapa, almacen, sitios["pradera"]);
                var (x0, y0) = sitios["pradera"];
                var (x1, y1) = sitios["denso"];
                double largo = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                int pasos = (int)Math.Min(1200, largo / 0.15);
                var tiempos = new List<float>();
                var ejercito = almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito;
                for (int k = 1; k <= pasos; k++)
                {
                    double t = (double)k / pasos;
                    ejercito.Posicion = Pu(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t);
                    yield return null;
                    tiempos.Add(Time.unscaledDeltaTime * 1000);
                }
                var orden = tiempos.OrderBy(v => v).ToList();
                Debug.Log($"[Final] en marcha ({pasos} fotogramas, {pasos * 0.15:0} u): mediana {orden[orden.Count / 2]:0.0} ms, p95 {orden[(int)(orden.Count * 0.95)]:0.0} ms, "
                    + $"p99 {orden[(int)(orden.Count * 0.99)]:0.0} ms, peor {orden[^1]:0.0} ms; {tiempos.Count(v => v > 16.7f)} por encima de 16,7 ms y {tiempos.Count(v => v > 33.3f)} de 33,3 ms");
            }

            // 3. las capturas finales (sin el aclarado, salvo la del héroe)
            MapaDeMundo.AclararCopas = false;
            try
            {
                foreach (var zona in new[] { "denso", "borde", "ribera", "costa", "montana", "monte_bajo", "pradera", "rambla" })
                {
                    if (!sitios.ContainsKey(zona)) continue;
                    yield return Llegar(mapa, almacen, sitios[zona]);
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"final_{zona}");
                }
                foreach (var zona in new[] { "borde", "costa" })
                {
                    yield return Llegar(mapa, almacen, sitios[zona]);
                    yield return Vista(mapa, ZoomDelMapa.Alejado, 50, $"final_panoramica_{zona}");
                }
                foreach (var (zona, nombre) in new[] { ("playa", "hierba_playa"), ("ladera", "piedras_ladera"), ("rambla", "flor_adelfa") })
                {
                    if (!sitios.ContainsKey(zona)) continue;
                    yield return Llegar(mapa, almacen, sitios[zona]);
                    yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"final_detalle_{nombre}");
                }
                MapaDeMundo.AclararCopas = true;
                yield return Llegar(mapa, almacen, sitios["denso"]);
                yield return Vista(mapa, ZoomDelMapa.Normal, 30, "final_heroe_en_el_bosque");
            }
            finally { MapaDeMundo.AclararCopas = true; }
        }

        /// <summary>El sol del mapa, por defecto: el acimut del prototipo con 40° de elevación (Víctor, 2026-11-20).</summary>
        [Test, Category("Mapa")]
        public void ElSolDelMapaEstaA40Grados()
        {
            var s = PinturaDelMapa.Sol;
            double elevacion = Math.Atan2(s.y, new Vector2(s.x, s.z).magnitude) * 180 / Math.PI;
            Assert.AreEqual(40, elevacion, 0.01, "40° sobre el horizonte");
            var p = PinturaDelMapa.SolDelPrototipo;
            Assert.AreEqual(Math.Atan2(p.z, p.x), Math.Atan2(s.z, s.x), 1e-5, "el acimut del prototipo");
            Assert.AreEqual(55.7, Math.Atan2(p.y, new Vector2(p.x, p.z).magnitude) * 180 / Math.PI, 0.5, "el del prototipo, unos 56°");
        }

        /// <summary>La sombra del relieve en el mundo entero: qué parte de la tierra queda a la sombra de las montañas con el sol a 56° y a 40° (en una
        /// rejilla de 6 u), las tres teselas con más, y (`MUNDO_CAPTURAS`) la de más, a zoom alejado con el sol a 40°, con y sin ella. Mide también
        /// el peor fotograma al cruzar un borde de tesela, con la sombra del relieve encendida y apagada.</summary>
        [Category("Mapa"), Explicit("medida y capturas de la sombra del relieve")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator LaSombraDelRelieveEnElMundo()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "relieve-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            double e = EscalaDelMapa.MetrosPorUnidad, lado = relieve.LadoEnUnidades, paso = 6;
            double ancho = relieve.Columnas * relieve.Paso, alto = relieve.Filas * relieve.Paso;
            double Altura(double x, double y) => relieve.AlturaEn(x, y);
            var porTesela = new Dictionary<(int, int), int>();
            foreach (var grados in new[] { 56.0, 40.0 })
            {
                var sol = PinturaDelMapa.SolConElevacion(grados);
                var (dx, dy, tan) = SombraDelRelieve.DelSol(sol.x, sol.y, sol.z);
                long tierra = 0, sombra = 0;
                var reloj = System.Diagnostics.Stopwatch.StartNew();
                for (double y = paso / 2; y < alto; y += paso)
                {
                    for (double x = paso / 2; x < ancho; x += paso)
                    {
                        if (relieve.AguaEn(x, y)) continue;
                        tierra++;
                        if (SombraDelRelieve.Visibilidad(Altura, x, y, dx, dy, tan, e) >= 0.5f) continue;
                        sombra++;
                        if (grados == 40)
                        {
                            var k = ((int)(x / lado), (int)(y / lado));
                            porTesela[k] = porTesela.TryGetValue(k, out var c) ? c + 1 : 1;
                        }
                    }
                    if (reloj.ElapsedMilliseconds > 200) { reloj.Restart(); yield return null; }
                }
                Debug.Log($"[Relieve] sol a {grados}°: {sombra * 100.0 / tierra:0.00} % de la tierra a la sombra del relieve ({sombra} de {tierra} celdas de {paso} u)");
            }
            var mejores = porTesela.OrderByDescending(kv => kv.Value).Take(3).ToList();
            foreach (var kv in mejores)
                Debug.Log($"[Relieve] tesela ({kv.Key.Item1}, {kv.Key.Item2}), centro ({(kv.Key.Item1 + 0.5) * lado:0}, {(kv.Key.Item2 + 0.5) * lado:0}) u: {kv.Value} celdas a la sombra ({kv.Value * 100.0 / ((lado / paso) * (lado / paso)):0} %)");

            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (PinturaDelMapa.SombraDelRelieve, PinturaDelMapa.Sol);
            try
            {
                PinturaDelMapa.Sol = PinturaDelMapa.SolConElevacion(40);
                var sitio = mejores.Count > 0 ? ((mejores[0].Key.Item1 + 0.5) * lado, (mejores[0].Key.Item2 + 0.5) * lado) : SitiosDeVegetacion(relieve, artefacto, datos)["denso"];
                foreach (var conRelieve in new[] { true, false })
                {
                    PinturaDelMapa.SombraDelRelieve = conRelieve;
                    if (go != null) { UnityEngine.Object.Destroy(go); yield return null; }
                    var (almacen, sesion) = Almacen(url, sitio, null);
                    var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                    mapa.RadioVisionFijo = 15;
                    yield return Llegar(mapa, almacen, sitio);
                    var fin = Time.realtimeSinceStartup + 60;
                    while (mapa.HorneandoRelieve > 0 && Time.realtimeSinceStartup < fin) yield return null;
                    yield return Vista(mapa, ZoomDelMapa.Alejado, 30, $"relieve_{(conRelieve ? "con" : "sin")}");
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"relieve_{(conRelieve ? "con" : "sin")}_habitual");
                    // Cruzar un borde de tesela: de 4 u antes a 12 u después, a 0,1 u por fotograma, a zoom habitual.
                    var (sx, sy) = sitio;
                    double bx = Math.Floor(sx / lado) * lado + lado;
                    var ejercito = almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito;
                    ejercito.Posicion = Pu(bx - 4, sy);
                    yield return new WaitForSecondsRealtime(2);
                    fin = Time.realtimeSinceStartup + 60;
                    while (mapa.HorneandoRelieve > 0 && Time.realtimeSinceStartup < fin) yield return null;
                    var tiempos = new List<float>();
                    for (int k = 1; k <= 160; k++)
                    {
                        ejercito.Posicion = Pu(bx - 4 + k * 0.1, sy);
                        yield return null;
                        tiempos.Add(Time.unscaledDeltaTime * 1000);
                    }
                    var orden = tiempos.OrderBy(v => v).ToList();
                    Debug.Log($"[Relieve] cruzar un borde de tesela, sombra del relieve {(conRelieve ? "encendida" : "apagada")}: mediana {orden[80]:0.0} ms, peor {orden[^1]:0.0} ms, "
                        + $"segundo peor {orden[^2]:0.0} ms; el relieve, como mucho {mapa.PeorMsRelieve:0.0} ms en un fotograma");
                }
            }
            finally { (PinturaDelMapa.SombraDelRelieve, PinturaDelMapa.Sol) = antes; }
        }

        /// <summary>La luz y las sombras del mapa, para las capturas de revisión (`MUNDO_CAPTURAS`): antes (sin sombras reales, la luz de siempre y
        /// la mancha) y con sombras reales con el sol del prototipo (56°) y con uno más bajo (40°, el mismo acimut), en el bosque denso, el borde de
        /// un bosque, una ribera, la costa, un valle de montaña y el héroe dentro del bosque, a los tres zooms. Mide el fotograma con y sin sombras
        /// en el bosque denso, la pradera, la costa y la montaña a zoom habitual. Comprueba que al irse el mapa el asset de URP vuelve a ser el
        /// original y con sus valores.</summary>
        [Category("Mapa"), Explicit("capturas de revisión de la luz y las sombras")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator LaLuzYLasSombras()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "luz-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            float U(string k) => (float)(datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION." + k) * Relieve.MetrosAltura);
            var umbrales = new Vector4(U("umbralCosta"), U("umbralColina"), U("umbralMontana"), U("umbralCima"));
            float nieve = umbrales.w + PinturaDelMapa.NieveSobreCima * ((float)Relieve.MetrosAltura - umbrales.w);
            var cuenta = new Dictionary<(int, int), int[]>();
            yield return ContarEspecies(relieve, artefacto, umbrales, nieve, cuenta);
            int N(int[] c, EspecieDeArbol x) => c[(int)x];
            int Arboles(int[] c) => c.Take(7).Sum();
            const int H = VegetacionDelMapa.Hierba, F = VegetacionDelMapa.Flor, P = VegetacionDelMapa.Piedra;
            var sitios = MejoresSitios(cuenta, new (string, Func<int[], double>)[]
            {
                ("ribera", c => N(c, EspecieDeArbol.Alamo) > 0 && c[VegetacionDelMapa.Canas] > 0 ? Math.Min(N(c, EspecieDeArbol.Alamo), 8) + Math.Min(c[VegetacionDelMapa.Canas], 12) : -1),
                ("costa", c => N(c, EspecieDeArbol.Palmera) > 0 && N(c, EspecieDeArbol.Pino) > 0 ? Math.Min(N(c, EspecieDeArbol.Palmera), 6) * 2 + Math.Min(N(c, EspecieDeArbol.Pino), 8) : -1),
                ("montana", c => N(c, EspecieDeArbol.Abeto) > 0 ? Math.Min(N(c, EspecieDeArbol.Abeto), 20) + 0.1 * Arboles(c) : -1),
                ("pradera", c => c[F] > 0 && c[H] > 0 ? Math.Min(c[H], 200) + 4 * Math.Min(c[F], 40) - 2 * Arboles(c) - c[P] : -1),
                // un árbol suelto (de los grandes) en campo abierto: para comparar el largo de la sombra con los dos soles
                ("suelto", c => Arboles(c) == 1 && N(c, EspecieDeArbol.Cipres) == 0 && N(c, EspecieDeArbol.Palmera) == 0 && c[VegetacionDelMapa.Lentisco] + c[VegetacionDelMapa.Retama] < 3 ? c[H] : -1),
            }).ToDictionary(q => q.nombre, q => (q.x, q.y));
            foreach (var kv in SitiosDeVegetacion(relieve, artefacto, datos)) sitios[kv.Key] = kv.Value;
            var sinSombras = new Dictionary<string, Color32[]>();
            int TexelesMasOscuros(Color32[] a, Color32[] b)
            {
                int n = 0;
                for (int k = 0; k < a.Length; k++)
                {
                    float la = 0.2126f * a[k].r + 0.7152f * a[k].g + 0.0722f * a[k].b, lb = 0.2126f * b[k].r + 0.7152f * b[k].g + 0.0722f * b[k].b;
                    if (la > 40 && lb < la * 0.75f) n++;
                }
                return n;
            }

            var original = QualitySettings.renderPipeline;
            var valores = original is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset u0 ? (u0.shadowDistance, u0.shadowCascadeCount, u0.shadowNormalBias) : default;
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (PinturaDelMapa.SombrasReales, PinturaDelMapa.Sol, PinturaDelMapa.SombraDelRelieve);
            try
            {
                foreach (var (nombre, sombras, grados) in new[] { ("antes", false, 56.0), ("sol56", true, 56.0), ("sol40", true, 40.0), ("sol40sinrelieve", true, 40.0) })
                {
                    // (la última vuelta, solo la montaña: el sol a 40° sin la sombra del relieve, para ver lo que pone)
                    bool soloMontana = nombre == "sol40sinrelieve";
                    PinturaDelMapa.SombraDelRelieve = !soloMontana;
                    PinturaDelMapa.SombrasReales = sombras;
                    PinturaDelMapa.Sol = PinturaDelMapa.SolConElevacion(grados);
                    if (go != null) { UnityEngine.Object.Destroy(go); yield return null; }
                    Assert.AreSame(original, QualitySettings.renderPipeline, "sin mapa, el asset de URP es el original");
                    var (almacen, sesion) = Almacen(url, sitios["denso"], null);
                    var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                    mapa.RadioVisionFijo = 15;
                    yield return null;
                    Assert.AreEqual(sombras, mapa.ConPipelineDelMapa, "con sombras, el mapa trabaja con su copia del asset de URP");
                    foreach (var zona in new[] { "denso", "borde", "ribera", "costa", "montana" })
                    {
                        if (!sitios.ContainsKey(zona) || (soloMontana && zona != "montana")) continue;
                        yield return Llegar(mapa, almacen, sitios[zona]);
                        foreach (var (zoom, z) in new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                        {
                            MapaDeMundo.AclararCopas = false;
                            yield return Vista(mapa, zoom, 30, $"luz_{zona}_{nombre}_{z}");
                            // Con sombras, en el borde del bosque de cerca hay suelo bajo las copas más oscuro que sin ellas (las copas arrojan).
                            if (zona == "borde" && zoom == ZoomDelMapa.Cerca && !soloMontana)
                            {
                                if (!sombras) sinSombras[zona] = ultima.GetPixels32();
                                else
                                {
                                    int oscuros = TexelesMasOscuros(sinSombras[zona], ultima.GetPixels32());
                                    Debug.Log($"[Luz] borde de cerca, {nombre}: {oscuros * 100.0 / (1600 * 900):0.0} % de la imagen más oscura que sin sombras");
                                    Assert.Greater(oscuros, 1600 * 900 / 20, "las copas dan sombra en el suelo (más del 5 % de la imagen)");
                                }
                            }
                        }
                        if (zona == "montana" && sombras && !soloMontana)
                        {
                            // El valle a la sombra de la cresta: la sombra horneada del relieve llega a las teselas (UV1.x, lo que tapa).
                            float masTapado = 0;
                            foreach (var f in go.GetComponentsInChildren<MeshFilter>().Where(f => f.name.StartsWith("tesela")))
                            {
                                var uv = new List<Vector2>();
                                f.sharedMesh.GetUVs(1, uv);
                                foreach (var t in uv) masTapado = Math.Max(masTapado, t.x);
                            }
                            Debug.Log($"[Luz] montaña, {nombre}: lo más tapado por el relieve, {masTapado:0.00}; la sombra del relieve cuesta como mucho {mapa.PeorMsRelieve:0.0} ms por tesela");
                            if (grados == 40) Assert.Greater(masTapado, 0.9f, "con el sol a 40°, en la montaña hay valles a la sombra del relieve");
                        }
                        if (nombre == "sol56" || soloMontana) continue; // el coste: antes y con el sol de ahora (40°)
                        if (zona == "denso" || zona == "costa" || zona == "montana")
                        {
                            yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"luz_medir_{zona}_{nombre}");
                            yield return Medir(mapa, $"luz: {zona}, zoom habitual, {nombre}");
                        }
                    }
                    if (soloMontana) continue;
                    if (sitios.ContainsKey("suelto"))
                    {
                        yield return Llegar(mapa, almacen, sitios["suelto"]);
                        yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"luz_suelto_{nombre}");
                    }
                    MapaDeMundo.AclararCopas = true;
                    yield return Llegar(mapa, almacen, sitios["denso"]);
                    foreach (var (zoom, z) in new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                        yield return Vista(mapa, zoom, 30, $"luz_heroe_{nombre}_{z}");
                    if (nombre != "sol56" && sitios.ContainsKey("pradera"))
                    {
                        yield return Llegar(mapa, almacen, sitios["pradera"]);
                        yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"luz_medir_pradera_{nombre}");
                        yield return Medir(mapa, $"luz: pradera, zoom habitual, {nombre}");
                    }
                }
                UnityEngine.Object.Destroy(go);
                go = null;
                yield return null;
                Assert.AreSame(original, QualitySettings.renderPipeline, "al irse el mapa, el asset de URP vuelve a ser el original");
                if (original is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset u1)
                    Assert.AreEqual(valores, (u1.shadowDistance, u1.shadowCascadeCount, u1.shadowNormalBias), "el original, con sus valores");
            }
            finally
            {
                (PinturaDelMapa.SombrasReales, PinturaDelMapa.Sol, PinturaDelMapa.SombraDelRelieve) = antes;
                MapaDeMundo.AclararCopas = true;
            }
        }

        /// <summary>El mosaico de reglas (`MosaicoDeVegetacion`) en el mapa, para las capturas de revisión (`MUNDO_CAPTURAS`): la misma vista con
        /// el mosaico apagado (el reparto del prototipo) y encendido en el borde de un bosque, el interior de un bosque denso, una ribera y el
        /// monte bajo, a zoom habitual y desde más arriba; y la vista de depuración (cada celda con el color de su estado). Cuenta los árboles de
        /// la tesela del bosque denso con y sin mosaico y mide el fotograma en él.</summary>
        [Category("Mapa"), Explicit("capturas de revisión del mosaico")]
        [UnityTest, Timeout(1_800_000)]
        public IEnumerator ElMosaicoEnElMapa()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "mosaico-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var sitios = SitiosDeVegetacion(relieve, artefacto, datos);
            // La ribera (bosque junto a un río) y el monte bajo (poco bosque y suelo pobre).
            var bos = new RasterDeMundo(artefacto.DensidadBosque);
            var fer = new RasterDeMundo(artefacto.Fertilidad);
            var cauces = new CaucesDelMapa(artefacto, relieve);
            (double p, double x, double y) ribera = (double.MinValue, 0, 0), monte = (double.MinValue, 0, 0);
            for (double y = 150; y < relieve.Filas * relieve.Paso - 150; y += 6)
                for (double x = 150; x < relieve.Columnas * relieve.Paso - 150; x += 6)
                {
                    if (relieve.AguaEn(x, y)) continue;
                    double b = bos.En(x, y) / 255.0, f = fer.En(x, y) / 255.0;
                    if (b > 0.4 && cauces.HayRioCerca(x - 3, y - 3, x + 3, y + 3) && cauces.ACauce(x, y) < 4) { var q = b - Math.Abs(cauces.ACauce(x, y) - 2) / 4; if (q > ribera.p) ribera = (q, x, y); }
                    if (b > 0.08 && b < 0.3 && f < 0.35) { var q = -Math.Abs(b - 0.18) - f; if (q > monte.p) monte = (q, x, y); }
                }
            sitios["ribera"] = (ribera.x, ribera.y);
            sitios["monte"] = (monte.x, monte.y);
            foreach (var kv in sitios) Debug.Log($"[Mosaico] sitio {kv.Key}: ({kv.Value.x:0.0}, {kv.Value.y:0.0}), bosque {bos.En(kv.Value.x, kv.Value.y) / 255.0:0.00}");

            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (VegetacionDelMapa.Mosaico, VegetacionDelMapa.VerMosaico);
            try
            {
                foreach (var (mosaico, ver, nombre) in new[] { (false, false, "apagado"), (true, false, "encendido"), (true, true, "depuracion") })
                {
                    VegetacionDelMapa.Mosaico = mosaico;
                    VegetacionDelMapa.VerMosaico = ver;
                    if (go != null) { UnityEngine.Object.Destroy(go); yield return null; }
                    var (almacen, sesion) = Almacen(url, sitios["borde"], null);
                    var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                    mapa.RadioVisionFijo = 15;
                    foreach (var n in new[] { "borde", "denso", "ribera", "monte" })
                    {
                        yield return Llegar(mapa, almacen, sitios[n]);
                        yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"mos_{n}_{nombre}_1_habitual");
                        yield return Vista(mapa, ZoomDelMapa.Alejado, 65, $"mos_{n}_{nombre}_2_arriba");
                    }
                    // Un cruce de cuatro teselas en el bosque, desde arriba: que no se vea el anillo de 64 u.
                    double ladoT = relieve.LadoEnUnidades;
                    var cruce = (Math.Round(sitios["denso"].x / ladoT) * ladoT, Math.Round(sitios["denso"].y / ladoT) * ladoT);
                    yield return Llegar(mapa, almacen, cruce);
                    yield return Vista(mapa, ZoomDelMapa.Alejado, 65, $"mos_cruce_{nombre}_arriba");
                    if (ver) continue;
                    // El héroe dentro del bosque cerrado: con el aclarado de las copas a su alrededor.
                    yield return Llegar(mapa, almacen, sitios["denso"]);
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"mos_heroe_{nombre}_1_habitual");
                    yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"mos_heroe_{nombre}_2_cerca");
                    yield return Vista(mapa, ZoomDelMapa.Alejado, 30, $"mos_heroe_{nombre}_3_alejado");
                    // Con una segunda columna cerca (ajena, a 3 u): su hueco, menor.
                    var denso = sitios["denso"];
                    mapa.Adicionales.Add(new CosaDelMapa { Clase = ClaseDeCosa.EjercitoAjeno, Id = "otra-columna", Nombre = "Otra columna", X = denso.x + 3, Y = denso.y + 1, Radio = 0.3, Alto = 0.35 });
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"mos_heroe_{nombre}_4_dos_columnas");
                    mapa.Adicionales.Clear();
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"mos_denso_{nombre}_3_medir");
                    yield return Medir(mapa, $"en el bosque denso, zoom habitual, mosaico {nombre}");
                    var v = mapa.Vegetacion;
                    double lado = relieve.LadoEnUnidades;
                    var plantas = v.RepartirAhora((int)(sitios["denso"].x / lado), (int)(sitios["denso"].y / lado));
                    int arboles = plantas.Count(p => !p.Mata), matas = plantas.Count(p => p.Mata);
                    Debug.Log($"[Mosaico] {nombre}: tesela del denso con {arboles} árboles y {matas} matas; a la vista {v.Dibujadas} plantas, {v.Triangulos} triángulos, {v.Llamadas} llamadas; "
                        + $"mosaico: {v.Mosaicos} teselas, {v.CaidasAlPrototipo} al prototipo, {v.IntentosDeMosaico} intentos, {v.MsMosaico / Math.Max(1, v.Mosaicos):0.0} ms por tesela (peor {v.PeorMsMosaico:0.0} ms)");
                }
            }
            finally
            {
                (VegetacionDelMapa.Mosaico, VegetacionDelMapa.VerMosaico) = antes;
            }
        }

        /// <summary>El agua (`Conquest/MapaAgua`): la costa (el mismo sitio que la del suelo), el mar hondo, un lago, un río navegable y otro no,
        /// un vado, una desembocadura, el borde de la niebla sobre el agua y el horizonte sobre el mar. Comprueba que el agua es una lámina aparte,
        /// que el mar hondo es agua y sale más oscuro que el lago (somero en la orilla).</summary>
        [Category("Mapa")]
        [UnityTest, Timeout(600_000)]
        public IEnumerator ElAguaEnCadaSitio()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            var acceso = new Acceso(url);
            var nick = "agua-" + DateTime.UtcNow.Ticks % 100_000_000;
            Esperar(acceso.Registrar(nick, "clave-de-prueba-1"));
            var login = Esperar(acceso.Entrar(nick, "clave-de-prueba-1"));
            var s = new Sesion(acceso.UrlSesion, login.Token, "conquest-prueba");
            var abrir = s.Abrir();
            var hasta = Time.realtimeSinceStartup + 20;
            while (!abrir.IsCompleted && Time.realtimeSinceStartup < hasta) { s.Bombear(); yield return null; }
            var mundo = abrir.Result.Mundo;
            s.Cerrar();
            acceso.Anular(login.Token);
            cache = Path.Combine(Path.GetTempPath(), "conquest-prueba-agua-" + Guid.NewGuid().ToString("N"));
            var datos = Esperar(DatosDeJuego.Cargar(acceso, mundo, new CacheDeUno(Path.Combine(cache, "datos"))));
            var artefacto = Esperar(MundoEnDisco.CargarArtefacto(acceso, mundo, new CacheDeUno(Path.Combine(cache, "mundo"))));
            var relieve = new Relieve(Esperar(MundoEnDisco.CargarRelieve(acceso, mundo, new CacheDeUno(Path.Combine(cache, "relieve")))));

            // Un río navegable sintético (el mundo de prueba no tiene ninguno): el más largo de los que no acaban en un lago.
            var largo = artefacto.Rios.Where(r => !r.TerminaEnLago).OrderByDescending(r => r.Puntos.Count).First();
            largo.Navegable = true;
            var sitios = BuscarSitios(relieve, artefacto, datos);
            var agua = SitiosDeAgua(relieve, artefacto, datos);
            agua["costa"] = sitios["costa"];
            foreach (var kv in agua) Debug.Log($"[Agua] sitio {kv.Key}: ({kv.Value.x:0.0}, {kv.Value.y:0.0}), {relieve.AlturaEn(kv.Value.x, kv.Value.y):0} m");

            var (almacen, sesion) = Almacen(url, agua["costa"], null);
            var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
            yield return Llegar(mapa, almacen, agua["costa"]);
            Assert.Greater(go.GetComponentsInChildren<MeshRenderer>().Count(r => r.name == "agua" && r.sharedMaterial.shader.name == "Conquest/MapaAgua"), 0, "la lámina de agua");
            yield return Vista(mapa, ZoomDelMapa.Cerca, 15, "agua_costa_1_cerca");
            yield return Vista(mapa, ZoomDelMapa.Normal, 35, "agua_costa_2_media");
            yield return Vista(mapa, ZoomDelMapa.Alejado, 90, "agua_costa_3_arriba");
            mapa.Camara.Giro = Math.PI;
            yield return Vista(mapa, ZoomDelMapa.Alejado, 10, "agua_horizonte_mar");
            mapa.Camara.Giro = 0;

            // Tiras de cuatro fotogramas (0,4 s entre uno y otro) de la rompiente y de la corriente.
            yield return Vista(mapa, ZoomDelMapa.Normal, 90, "agua_tira_orilla_0");
            for (int f = 1; f < 4; f++) yield return Fotograma("agua_tira_orilla_" + f, 0.4f);

            var colores = new Dictionary<string, Color>();
            foreach (var k in new[] { "mar_hondo", "lago", "rio_navegable", "rio", "aguas_blancas", "vado", "desembocadura" })
            {
                if (!agua.ContainsKey(k)) { Debug.Log($"[Agua] sin sitio: {k}"); continue; }
                // El vado, con la columna a un lado (si no, la caja lo tapa desde arriba).
                var donde = k == "vado" ? (agua[k].x + 3, agua[k].y + 3) : agua[k];
                yield return Llegar(mapa, almacen, donde);
                yield return Vista(mapa, ZoomDelMapa.Normal, 35, $"agua_{k}_2_media");
                colores[k] = Centro();
                if (k == "rio_navegable")
                    for (int f = 1; f < 4; f++) yield return Fotograma($"agua_tira_rio_{f}", 0.4f);
                yield return Vista(mapa, ZoomDelMapa.Cerca, 15, $"agua_{k}_1_cerca");
                yield return Vista(mapa, ZoomDelMapa.Alejado, 90, $"agua_{k}_3_arriba");
            }
            Debug.Log("[Agua] centro de la vista media: " + string.Join("; ", colores.Select(kv => $"{kv.Key} {kv.Value}")));
            float Luz(Color c) => 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
            Assert.Greater(colores["mar_hondo"].b, colores["mar_hondo"].r + 0.05f, $"el mar hondo es agua: {colores["mar_hondo"]}");
            Assert.Less(Luz(colores["mar_hondo"]), Luz(colores["lago"]) + 0.05f, $"el mar hondo, más oscuro que el lago: {colores["mar_hondo"]} frente a {colores["lago"]}");

            // El borde de la niebla de guerra sobre el agua: otro mapa con la VISTA llevando niebla, en la costa.
            UnityEngine.Object.Destroy(go);
            yield return null;
            (almacen, sesion) = Almacen(url, agua["costa"], Niebla(relieve, agua["costa"], 25, 12));
            mapa = Montar(almacen, sesion, relieve, artefacto, datos);
            yield return Llegar(mapa, almacen, agua["costa"]);
            yield return Vista(mapa, ZoomDelMapa.Alejado, 50, "agua_niebla_borde");
            var (construidas, ms) = mapa.ConstruccionDelSuelo;
            var (tris, _) = Triangulos();
            int trisAgua = go.GetComponentsInChildren<MeshFilter>().Where(f => f.name == "agua").Sum(f => (int)(f.sharedMesh.GetIndexCount(0) / 3));
            Debug.Log($"[Agua] {mapa.TeselasCargadas} teselas, {tris} triángulos de suelo y {trisAgua} de agua; {construidas} teselas en {ms:0} ms ({ms / Math.Max(1, construidas):0.0} ms cada una)");
        }

        /// <summary>Los sitios del agua: el mar hondo (más de 40 m bajo el nivel del mar, con agua a 20 u alrededor), el lago más grande (agua por
        /// encima del nivel del mar), un río navegable y otro no (a mitad de su tramo más largo), un vado y una desembocadura en el mar.</summary>
        static Dictionary<string, (double x, double y)> SitiosDeAgua(Relieve relieve, ArtefactoDeMundo artefacto, DatosDeJuego datos)
        {
            double mar = datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION.umbralAgua") * Relieve.MetrosAltura;
            var r = new Dictionary<string, (double x, double y)>();
            double margen = 120, mejorLago = 0;
            float mejorRapido = 0;
            for (double y = margen; y < relieve.Filas * relieve.Paso - margen; y += 8)
                for (double x = margen; x < relieve.Columnas * relieve.Paso - margen; x += 8)
                {
                    if (!relieve.AguaEn(x, y)) continue;
                    double h = relieve.AlturaEn(x, y);
                    if (!r.ContainsKey("mar_hondo") && h < mar - 40 && relieve.AguaEn(x + 20, y) && relieve.AguaEn(x - 20, y) && relieve.AguaEn(x, y + 20) && relieve.AguaEn(x, y - 20))
                        r["mar_hondo"] = (x, y);
                    if (h > mar + 2)
                    {
                        int cuantos = 0;
                        for (int dj = -3; dj <= 3; dj++) for (int di = -3; di <= 3; di++) if (relieve.AguaEn(x + di * 2, y + dj * 2)) cuantos++;
                        // Junto a la orilla del lago (para ver la orilla y lo somero): con tierra a menos de 6 u.
                        if (cuantos > mejorLago && cuantos < 49) { mejorLago = cuantos; r["lago"] = (x, y); }
                    }
                }
            foreach (var rio in artefacto.Rios)
            {
                var tramos = RiosDelMapa.Tramos(rio, relieve);
                if (tramos.Count == 0) continue;
                var largo = tramos.OrderByDescending(t => t.Puntos.Count).First();
                var medio = largo.Puntos[largo.Puntos.Count / 2];
                var clave = rio.Navegable ? "rio_navegable" : "rio";
                if (!r.ContainsKey(clave) && medio.x > margen && medio.y > margen) r[clave] = (medio.x, medio.y);
                var ultimo = tramos[tramos.Count - 1].Puntos;
                var fin = ultimo[ultimo.Count - 1];
                if (!r.ContainsKey("desembocadura") && relieve.AguaEn(fin.x, fin.y) && !rio.TerminaEnLago) r["desembocadura"] = (fin.x, fin.y);
                foreach (var v in rio.Vados)
                    if (!r.ContainsKey("vado") && !relieve.AguaEn(v.X / 1000.0, v.Y / 1000.0)) r["vado"] = (v.X / 1000.0, v.Y / 1000.0);
                // Las aguas blancas: el punto del río donde la lámina baja más deprisa (sobre 2 u).
                foreach (var t in tramos)
                    for (int i = 4; i + 4 < t.Puntos.Count; i++)
                    {
                        Vector2 a = t.Puntos[i - 4], b = t.Puntos[i + 4];
                        float caida = RiosDelMapa.NivelDelRio(relieve, a, Vector2.zero, 0) - RiosDelMapa.NivelDelRio(relieve, b, Vector2.zero, 0);
                        float pendiente = caida / Mathf.Max(0.1f, Vector2.Distance(a, b) * EscalaDelMapa.MetrosPorUnidad);
                        if (pendiente > mejorRapido && t.Puntos[i].x > margen && t.Puntos[i].y > margen) { mejorRapido = pendiente; r["aguas_blancas"] = (t.Puntos[i].x, t.Puntos[i].y); }
                    }
            }
            Debug.Log($"[Agua] la mayor pendiente de un río: {mejorRapido * 100:0.0} %");
            return r;
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

        /// <summary>Otro fotograma de la misma vista, `espera` s después (para ver cómo se mueve el agua).</summary>
        IEnumerator Fotograma(string nombre, float espera)
        {
            yield return new WaitForSecondsRealtime(espera);
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            if (string.IsNullOrEmpty(carpeta)) yield break;
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = antes;
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), t.EncodeToPNG());
            UnityEngine.Object.Destroy(t);
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
            int gc0 = GC.CollectionCount(0), gc2 = GC.CollectionCount(2);
            for (int i = 0; i < 300; i++) { yield return null; tiempos.Add(Time.unscaledDeltaTime * 1000); }
            Debug.Log($"[Suelo] {que}: recolecciones de basura {GC.CollectionCount(0) - gc0} (de generación 2, {GC.CollectionCount(2) - gc2})");
            tiempos.Sort();
            var v = mapa.Vegetacion;
            Debug.Log($"[Suelo] {que}: {mapa.TeselasCargadas} teselas; fotograma mediana {tiempos[150]:0.0} ms, p95 {tiempos[285]:0.0} ms, peor {tiempos[299]:0.0} ms"
                + (v == null ? "" : $"; vegetación: {v.Dibujadas} plantas, {v.Triangulos} triángulos, {v.Llamadas} llamadas ({v.LlamadasQueArrojan} arrojan sombra), {v.MsDibujar:0.00} ms de CPU al dibujar"));
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
