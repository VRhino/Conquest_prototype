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

        /// <summary>El banco de caminos y territorio, sobre el mundo de pruebas y sin tocar datos reales: una red verosímil trazada como la manda el
        /// motor (nodos cada 4 u, ocho direcciones, por A* sobre el relieve, sin agua ni cauces salvo en un vado real) con un sendero por pradera, un
        /// camino por bosque denso, un cruce de tres ramas, un camino que pasa por una plaza y un pueblo, uno que sube una ladera y uno que cruza el
        /// río por el vado; y el territorio: tu Facción con dos plazas cuyas zonas se tocan y su zona fusionada, una Facción ajena pegada, otra de
        /// color oscuro (sigilo negro), una plaza sin Facción y una recordada (ficha con radio). Capturas (`MUNDO_CAPTURAS`) de cada sitio a los tres
        /// zooms, antes (las rayas de siempre) y después (como el prototipo), y las medidas: el fotograma con y sin, el recentrado del territorio y
        /// el rehacer la vegetación por un camino nuevo.</summary>
        [Category("Mapa"), Explicit("banco y capturas de caminos y territorio")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator LosCaminosYElTerritorio()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "caminos-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var banco = BancoDeCaminos.Hacer(relieve, artefacto);
            foreach (var l in banco.Registro) Debug.Log("[Caminos] " + l);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");

            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (PinturaDelMapa.CaminosYTerritorio, MapaDeMundo.AclararCopas);
            MapaDeMundo.AclararCopas = false;
            try
            {
                foreach (var con in new[] { false, true })
                {
                    PinturaDelMapa.CaminosYTerritorio = con;
                    if (go != null) { UnityEngine.Object.Destroy(go); yield return null; }
                    var (almacen, sesion) = Almacen(url, banco.Sitios["vado"], null);
                    almacen.Aplicar(new MensajeDeServidor { Foto = banco.Vista() });
                    var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                    mapa.RadioVisionFijo = 15;
                    foreach (var kv in banco.Sitios)
                    {
                        yield return Llegar(mapa, almacen, kv.Value);
                        yield return new WaitForSecondsRealtime(1);
                        foreach (var (zoom, z) in new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                            yield return Vista(mapa, zoom, 30, $"caminos_{kv.Key}_{(con ? "despues" : "antes")}_{z}");
                    }
                    // el coste, a zoom habitual, en el cruce (caminos, fronteras y relleno a la vista)
                    yield return Llegar(mapa, almacen, banco.Sitios["cruce"]);
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"caminos_medir_{(con ? "despues" : "antes")}");
                    yield return Medir(mapa, $"caminos: {(con ? "como el prototipo" : "las rayas de antes")}, zoom habitual; {mapa.VerticesDeTrazos} vértices de cintas");
                    // el recentrado del territorio: cruzar un borde de celda de 128 u a 0,15 u por fotograma (y, sin la capa, el mismo paseo)
                    var (cx, cy) = banco.Sitios["cruce"];
                    double borde = Math.Round(cx / 128) * 128 + 64;
                    var ejercito = almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito;
                    var tiempos = new List<float>();
                    for (int k = 0; k <= 200; k++)
                    {
                        ejercito.Posicion = Pu(borde - 15 + k * 0.15, cy);
                        yield return null;
                        tiempos.Add(Time.unscaledDeltaTime * 1000);
                    }
                    tiempos.Sort();
                    if (!con) { Debug.Log($"[Caminos] el mismo paseo sin la capa: peor fotograma {tiempos[200]:0.0} ms, segundo {tiempos[199]:0.0}, p95 {tiempos[190]:0.0} (mediana {tiempos[100]:0.0})"); continue; }
                    Debug.Log($"[Caminos] recentrar el territorio: peor fotograma {tiempos[200]:0.0} ms, segundo {tiempos[199]:0.0}, p95 {tiempos[190]:0.0} (mediana {tiempos[100]:0.0}); el lienzo, como mucho {mapa.Territorio.PeorMsPintar:0} ms en su hilo y {mapa.Territorio.PeorMsSubir:0.0} ms al subir; {mapa.Territorio.Pintadas} pintadas; las cintas, {mapa.Trazos.Hechas} veces enteras (como mucho {mapa.Trazos.PeorMsHacer:0.0} ms) y {mapa.Trazos.TrozosRehechos} trozos por su suelo (como mucho {mapa.Trazos.PeorMsTrozos:0.0} ms en un fotograma)");
                    // un camino nuevo: la vegetación de sus teselas se rehace
                    yield return Llegar(mapa, almacen, banco.Sitios["camino_bosque"]);
                    tiempos.Clear();
                    almacen.Aplicar(new MensajeDeServidor { Foto = banco.Vista(conCaminoNuevo: true) });
                    for (int k = 0; k < 240; k++) { yield return null; tiempos.Add(Time.unscaledDeltaTime * 1000); }
                    Debug.Log($"[Caminos] un camino nuevo por el bosque: peor fotograma {tiempos.Max():0.0} ms (mediana {tiempos.OrderBy(t => t).ElementAt(120):0.0})");
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, "caminos_camino_nuevo_despues_2_habitual");
                }
            }
            finally { (PinturaDelMapa.CaminosYTerritorio, MapaDeMundo.AclararCopas) = antes; }
        }

        /// <summary>La etapa 2 de caminos (estilo «Mejorado», `TrazosEnElMapa.Estilo`) y el vado como trecho somero (`PinturaDelMapa.VadoComoTrecho`),
        /// lado a lado con el prototipo (etapa 1, con la franja del vado de antes), sobre el banco de `LosCaminosYElTerritorio`: cada sitio de caminos
        /// y el vado a los tres zooms, otro vado sin camino, los recortes de cada suelo de cerca, y un río sin vado y una costa (que deben salir
        /// iguales: fuera de los vados el agua no cambia). Las medidas: el fotograma con cada estilo, el paseo y un camino nuevo.</summary>
        [Category("Mapa"), Explicit("banco y capturas de la etapa 2 de caminos")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator LosCaminosMejorados()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "caminos2-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var banco = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            foreach (var l in banco.Registro) Debug.Log("[Caminos] " + l);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            var caminos = new[] { "vado", "sendero", "camino_bosque", "cruce", "plaza_pueblo", "ladera",
                "frontera_propia", "frontera_vecino", "provincias", "faccion_oscura", "sin_faccion", "recordada", "anillo_mercenarios", "territorio_pradera" };

            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (TrazosEnElMapa.Estilo, TerritorioDelMapa.Estilo, PinturaDelMapa.VadoComoTrecho, MapaDeMundo.AclararCopas);
            MapaDeMundo.AclararCopas = false;
            try
            {
                foreach (var mejorado in new[] { false, true })
                {
                    string estilo = mejorado ? "mejorado" : "prototipo";
                    TrazosEnElMapa.Estilo = mejorado ? TrazosEnElMapa.EstiloDeTrazos.Mejorado : TrazosEnElMapa.EstiloDeTrazos.Prototipo;
                    TerritorioDelMapa.Estilo = mejorado ? TerritorioDelMapa.EstiloDeTerritorio.Mejorado : TerritorioDelMapa.EstiloDeTerritorio.Prototipo;
                    PinturaDelMapa.VadoComoTrecho = mejorado;
                    if (go != null) { UnityEngine.Object.Destroy(go); yield return null; }
                    var (almacen, sesion) = Almacen(url, banco.Sitios["vado"], null);
                    almacen.Aplicar(new MensajeDeServidor { Foto = banco.Vista() });
                    var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                    mapa.RadioVisionFijo = 15;
                    var zooms = new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") };
                    foreach (var k in caminos)
                    {
                        if (!banco.Sitios.ContainsKey(k)) continue;
                        yield return Llegar(mapa, almacen, banco.Sitios[k]);
                        yield return new WaitForSecondsRealtime(1);
                        foreach (var (zoom, z) in zooms) { EsconderLaColumna(); yield return Vista(mapa, zoom, 30, $"e2_{k}_{estilo}_{z}"); }
                    }
                    // Una frontera azul (tu zona sobre la pradera) con un río de verdad en el mismo encuadre: que no se confundan.
                    if (banco.Medidas.Any(m => m.nombre == "azul sobre pradera") && mapa.Cauces != null)
                    {
                        var (_, cAzul, rAzul, _) = banco.Medidas.First(m => m.nombre == "azul sobre pradera");
                        (double x, double y) rio = cAzul;
                        double dRio = double.MaxValue;
                        for (double dy = -40; dy <= 40; dy += 2)
                            for (double dx = -40; dx <= 40; dx += 2)
                            {
                                double d = mapa.Cauces.ACauce(cAzul.x + dx, cAzul.y + dy, 3);
                                if (d < 0 && dx * dx + dy * dy < dRio) { dRio = dx * dx + dy * dy; rio = (cAzul.x + dx, cAzul.y + dy); }
                            }
                        if (dRio < double.MaxValue)
                        {
                            double l = Math.Sqrt(dRio);
                            var bordeRio = (x: cAzul.x + (rio.x - cAzul.x) / l * rAzul, y: cAzul.y + (rio.y - cAzul.y) / l * rAzul);
                            yield return Llegar(mapa, almacen, ((bordeRio.x + rio.x) / 2, (bordeRio.y + rio.y) / 2));
                            yield return new WaitForSecondsRealtime(1);
                            foreach (var (zoom, z) in new[] { (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                            { EsconderLaColumna(); yield return Vista(mapa, zoom, 30, $"e2_frontera_y_rio_{estilo}_{z}"); }
                        }
                    }
                    // El territorio, medido: el color (sRGB: tono, saturación y luminancia) a 0,5, 1,5 y 3 u dentro del borde y a 3 u fuera, a zoom
                    // habitual (la media de 9 puntos a lo largo del borde, ±1 u).
                    foreach (var (nombreM, centroM, radioM, fueraM) in banco.Medidas)
                    {
                        var bordeM = (x: centroM.x + fueraM.x * radioM, y: centroM.y + fueraM.y * radioM);
                        yield return Llegar(mapa, almacen, bordeM);
                        yield return new WaitForSecondsRealtime(1);
                        EsconderLaColumna();
                        yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"e2_medida_{nombreM.Replace(' ', '_')}_{estilo}");
                        string Medir((double x, double y) p0, double ancho)
                        {
                            var c = Color.black;
                            int n = 0;
                            for (int i = -4; i <= 4; i++)
                            {
                                double x = p0.x - fueraM.y * i * ancho / 4, y = p0.y + fueraM.x * i * ancho / 4;
                                var w = mapa.Vista.WorldToScreenPoint(EscalaDelMapa.AEscena(x, y, (float)relieve.AlturaEn(x, y)));
                                int px = (int)(w.x * rt.width / mapa.Vista.pixelWidth), py = (int)(w.y * rt.height / mapa.Vista.pixelHeight);
                                if (px < 1 || py < 1 || px > rt.width - 2 || py > rt.height - 2 || w.z <= 0) continue;
                                c += ultima.GetPixel(px, py); n++;
                            }
                            if (n == 0) return "—";
                            c /= n;
                            Color.RGBToHSV(c, out var h, out var sa, out _);
                            return $"tono {h * 360:0}°, sat {sa:0.00}, lum {0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b:0.00}";
                        }
                        string Dentro(double d) => Medir((bordeM.x - fueraM.x * d, bordeM.y - fueraM.y * d), 1);
                        Debug.Log($"[Territorio] {nombreM} {estilo}: a 0,5 u {Dentro(0.5)}; a 1,5 u {Dentro(1.5)}; a 3 u {Dentro(3)}; fuera (3 u) {Medir((bordeM.x + fueraM.x * 3, bordeM.y + fueraM.y * 3), 1)}");
                    }
                    if (banco.SitiosDeAgua.TryGetValue("vado_rio", out var otroVado))
                    {
                        yield return Llegar(mapa, almacen, otroVado);
                        yield return new WaitForSecondsRealtime(1);
                        foreach (var (zoom, z) in zooms) yield return Vista(mapa, zoom, 30, $"e2_vado_rio_{estilo}_{z}");
                    }
                    // Los recortes de cerca de cada suelo: la columna en el camino y escondida; el recorte, centrado en él. Y el color
                    // medio del camino (por su eje) y del suelo de al lado (a 1,2 anchos), en sRGB.
                    foreach (var kv in banco.Recortes)
                    {
                        var (rx, ry) = kv.Value;
                        // la columna escondida y corrida lo que la cámara mira por delante de ella: el centro de la pantalla cae en el camino
                        yield return Llegar(mapa, almacen, (rx, ry));
                        yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"e2_{kv.Key}_{estilo}_previa");
                        var rayo = mapa.Vista.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                        float hSuelo = (float)relieve.AlturaEn(rx, ry);
                        var g = rayo.origin + rayo.direction * ((hSuelo - rayo.origin.y) / rayo.direction.y);
                        double e2 = EscalaDelMapa.MetrosPorUnidad, ox = g.x / e2 - rx, oy = -g.z / e2 - ry;
                        yield return Llegar(mapa, almacen, (rx - ox, ry - oy));
                        yield return new WaitForSecondsRealtime(1);
                        EsconderLaColumna();
                        yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"e2_{kv.Key}_{estilo}_entero");
                        double ancho = kv.Key.EndsWith("sendero") ? TrazosYTerritorio.AnchoSendero : TrazosYTerritorio.AnchoCamino;
                        Vector2 Pantalla(double x, double y)
                        {
                            var w = mapa.Vista.WorldToScreenPoint(EscalaDelMapa.AEscena(x, y, (float)relieve.AlturaEn(x, y)));
                            return new Vector2(w.x * rt.width / mapa.Vista.pixelWidth, w.y * rt.height / mapa.Vista.pixelHeight);
                        }
                        Color Media(IEnumerable<(double x, double y)> puntos)
                        {
                            var c = Color.black;
                            int n = 0;
                            foreach (var (x, y) in puntos)
                            {
                                var s2 = Pantalla(x, y);
                                if (s2.x < 2 || s2.y < 2 || s2.x > rt.width - 3 || s2.y > rt.height - 3) continue;
                                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { c += ultima.GetPixel((int)s2.x + dx, (int)s2.y + dy); n++; }
                            }
                            return n > 0 ? c / n : Color.black;
                        }
                        // el camino, por sus bandas pisadas (no por el eje, que en el camino es el lomo de matas): a ±0,3 anchos del eje; el sendero, por su eje
                        double banda = kv.Key.EndsWith("sendero") ? 0 : ancho * 0.3;
                        var eje = Enumerable.Range(0, 31).SelectMany(i => new[] { (rx - 1.5 + i * 0.1, ry + banda), (rx - 1.5 + i * 0.1, ry - banda) }).Distinct().ToList();
                        var lado = eje.Select(q => (q.Item1, q.Item2 + ancho * 1.2)).Concat(eje.Select(q => (q.Item1, q.Item2 - ancho * 1.2))).ToList();
                        Color cc = Media(eje), cs = Media(lado);
                        Color.RGBToHSV(cc, out var hc, out var sc, out var vc);
                        float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                        Debug.Log($"[Caminos] color {kv.Key} {estilo}: camino ({cc.r * 255:0}, {cc.g * 255:0}, {cc.b * 255:0}) tono {hc * 360:0}° sat {sc:0.00}; " +
                                  $"suelo ({cs.r * 255:0}, {cs.g * 255:0}, {cs.b * 255:0}); camino/suelo en luminancia {Lum(cc) / Math.Max(1e-4f, Lum(cs)):0.00}");
                        var centro = Pantalla(rx, ry);
                        int x0 = Mathf.Clamp((int)centro.x - 500, 0, rt.width - 1000), y0 = Mathf.Clamp((int)centro.y - 281, 0, rt.height - 562);
                        var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
                        if (!string.IsNullOrEmpty(carpeta))
                        {
                            var rec = new Texture2D(1000, 562, TextureFormat.RGB24, false);
                            rec.SetPixels(ultima.GetPixels(x0, y0, 1000, 562));
                            rec.Apply();
                            File.WriteAllBytes(Path.Combine(carpeta, $"e2_recorte_{kv.Key}_{estilo}.png"), rec.EncodeToPNG());
                            UnityEngine.Object.Destroy(rec);
                        }
                    }
                    foreach (var k in new[] { "rio_sin_vado", "costa" })
                    {
                        if (!banco.SitiosDeAgua.TryGetValue(k, out var sitio)) continue;
                        yield return Llegar(mapa, almacen, sitio);
                        yield return new WaitForSecondsRealtime(1);
                        yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"e2_{k}_{estilo}_2_habitual");
                        if (!mejorado) continue;
                        // Fuera de los vados el agua no cambia: el sombreado del río y del suelo, con lo somero a 0, hace lo mismo que antes; aquí, que
                        // a menos de 40 u no haya ni un vértice de río, de orilla ni de suelo con algo de somero (las capturas no se pueden comparar
                        // píxel a píxel: el agua y las nubes se mueven).
                        int conSomero = 0, vistos = 0;
                        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                        {
                            if (!(mf.name == "Rios" || mf.name == "Orillas" || mf.name.StartsWith("tesela"))) continue;
                            var malla = mf.sharedMesh;
                            var vs = malla.vertices;
                            var u1 = new List<Vector2>();
                            malla.GetUVs(1, u1);
                            if (u1.Count != vs.Length) continue;
                            for (int i = 0; i < vs.Length; i++)
                            {
                                var w = mf.transform.TransformPoint(vs[i]);
                                double ux = w.x / EscalaDelMapa.MetrosPorUnidad, uy = -w.z / EscalaDelMapa.MetrosPorUnidad;
                                if ((ux - sitio.x) * (ux - sitio.x) + (uy - sitio.y) * (uy - sitio.y) > 40 * 40) continue;
                                vistos++;
                                float so = mf.name.StartsWith("tesela") ? u1[i].y : u1[i].x;
                                if (so > 0) conSomero++;
                            }
                        }
                        Debug.Log($"[Caminos] {k}: {vistos} vértices de agua y suelo a menos de 40 u, {conSomero} con algo de somero");
                        Assert.AreEqual(0, conSomero, $"{k}: lejos de los vados no hay nada somero");
                    }
                    // el coste, en el cruce, a zoom habitual
                    yield return Llegar(mapa, almacen, banco.Sitios["cruce"]);
                    EsconderLaColumna();
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"e2_medir_{estilo}");
                    yield return Medir(mapa, $"caminos {estilo}, zoom habitual; {mapa.VerticesDeTrazos} vértices de cintas");
                    var (cx, cy) = banco.Sitios["cruce"];
                    double borde = Math.Round(cx / 128) * 128 + 64;
                    var ejercito = almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito;
                    var tiempos = new List<float>();
                    for (int k = 0; k <= 200; k++)
                    {
                        ejercito.Posicion = Pu(borde - 15 + k * 0.15, cy);
                        yield return null;
                        tiempos.Add(Time.unscaledDeltaTime * 1000);
                    }
                    tiempos.Sort();
                    Debug.Log($"[Caminos] el paseo, {estilo}: peor fotograma {tiempos[200]:0.0} ms, segundo {tiempos[199]:0.0}, p95 {tiempos[190]:0.0} (mediana {tiempos[100]:0.0}); " +
                              $"las cintas, {mapa.Trazos.Hechas} veces enteras (como mucho {mapa.Trazos.PeorMsHacer:0.0} ms) y {mapa.Trazos.TrozosRehechos} trozos por su suelo (como mucho {mapa.Trazos.PeorMsTrozos:0.0} ms en un fotograma)");
                    yield return Llegar(mapa, almacen, banco.Sitios["camino_bosque"]);
                    tiempos.Clear();
                    almacen.Aplicar(new MensajeDeServidor { Foto = banco.Vista(conCaminoNuevo: true) });
                    for (int k = 0; k < 240; k++) { yield return null; tiempos.Add(Time.unscaledDeltaTime * 1000); }
                    Debug.Log($"[Caminos] un camino nuevo por el bosque, {estilo}: peor fotograma {tiempos.Max():0.0} ms (mediana {tiempos.OrderBy(t => t).ElementAt(120):0.0})");
                    EsconderLaColumna();
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"e2_camino_nuevo_{estilo}_2_habitual");
                }
            }
            finally { (TrazosEnElMapa.Estilo, TerritorioDelMapa.Estilo, PinturaDelMapa.VadoComoTrecho, MapaDeMundo.AclararCopas) = antes; }
        }

        /// <summary>Esconde el cubo de tu columna (para que no tape lo que se mira).</summary>
        void EsconderLaColumna()
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (r.name == "Tu columna" || r.transform.parent?.name == "Tu columna") r.enabled = false;
        }

        /// <summary>El vado es un punto (el motor cruza por un nodo en él; `mundo::RADIO_VADO` 0,75 u): en el banco de caminos, todo camino que cruza
        /// un río pasa a menos de 0,05 u del punto de su vado. Y las tres variantes del arte del vado sobre los mismos dos vados (el del banco, con el
        /// camino cruzando, y otro sin camino), a los tres zooms y de cerca en el punto: A la franja del tema del agua (1,6 u), B el río poco hondo
        /// corto y centrado en el punto (núcleo de 1,5 u y 2,5 u de transición) y C el trecho largo (20 u), como referencia.</summary>
        [Category("Mapa"), Explicit("capturas de las variantes del vado")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator LosVadosPorElPunto()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "vados-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var banco = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            foreach (var l in banco.Registro) Debug.Log("[Vados] " + l);
            Debug.Log($"[Vados] el mundo: {artefacto.Manifiesto.Hash.Substring(0, 8)}…, {artefacto.Manifiesto.Generador}; {artefacto.Navegacion.Nodos.Count} nodos, {artefacto.Navegacion.Aristas.Count} aristas, " +
                      $"{artefacto.Navegacion.Aristas.Count(a => a.Vado)} de vado; {artefacto.Rios.Sum(r => r.Vados.Count)} vados");
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (TrazosEnElMapa.Estilo, TerritorioDelMapa.Estilo, PinturaDelMapa.VadoComoTrecho, PinturaDelMapa.VadoMedioLargo, PinturaDelMapa.VadoTransicion, MapaDeMundo.AclararCopas);
            MapaDeMundo.AclararCopas = false;
            TrazosEnElMapa.Estilo = TrazosEnElMapa.EstiloDeTrazos.Mejorado;
            TerritorioDelMapa.Estilo = TerritorioDelMapa.EstiloDeTerritorio.Mejorado;
            try
            {
                var variantes = new (string nombre, bool trecho, float medio, float transicion)[] { ("A", false, 0.75f, 2.5f), ("B", true, 0.75f, 2.5f), ("C", true, 10, 3) };
                bool comprobado = false;
                foreach (var (nombre, trecho, medio, transicion) in variantes)
                {
                    (PinturaDelMapa.VadoComoTrecho, PinturaDelMapa.VadoMedioLargo, PinturaDelMapa.VadoTransicion) = (trecho, medio, transicion);
                    if (go != null) { UnityEngine.Object.Destroy(go); yield return null; }
                    var (almacen, sesion) = Almacen(url, banco.Sitios["vado"], null);
                    almacen.Aplicar(new MensajeDeServidor { Foto = banco.Vista() });
                    var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                    mapa.RadioVisionFijo = 15;
                    yield return Llegar(mapa, almacen, banco.Sitios["vado"]);
                    if (!comprobado)
                    {
                        // Todo camino que cruza un río (alguna muestra dentro de un cauce) pasa a menos de 0,05 u del punto de un vado.
                        comprobado = true;
                        var cauces = mapa.Cauces;
                        var (trazos, _) = TrazosYTerritorio.Reunir(almacen, "heroe-1", 0, cauces.Vados);
                        int cruzan = 0;
                        foreach (var t in trazos.Where(t => t.Tipo == TipoDeTrazo.Camino))
                        {
                            bool cruza = false;
                            for (int i = 1; i < t.Puntos.Count && !cruza; i++)
                                for (double f = 0; f <= 1; f += 0.1)
                                {
                                    double x = t.Puntos[i - 1].x + (t.Puntos[i].x - t.Puntos[i - 1].x) * f, y = t.Puntos[i - 1].y + (t.Puntos[i].y - t.Puntos[i - 1].y) * f;
                                    if (cauces.ACauce(x, y) < -0.05) { cruza = true; break; }
                                }
                            if (!cruza) continue;
                            cruzan++;
                            double alVado = double.MaxValue;
                            foreach (var v in cauces.Vados)
                                for (int i = 1; i < t.Puntos.Count; i++)
                                {
                                    var a = t.Puntos[i - 1];
                                    var b = t.Puntos[i];
                                    double dx = b.x - a.x, dy = b.y - a.y, l = dx * dx + dy * dy;
                                    double u = l > 0 ? Math.Max(0, Math.Min(1, ((v.x - a.x) * dx + (v.y - a.y) * dy) / l)) : 0;
                                    alVado = Math.Min(alVado, Math.Sqrt(Math.Pow(v.x - a.x - u * dx, 2) + Math.Pow(v.y - a.y - u * dy, 2)));
                                }
                            Debug.Log($"[Vados] un camino que cruza un río ({t.Puntos.Count} puntos) pasa a {alVado:0.000} u del punto del vado");
                            Assert.Less(alVado, 0.05, "el camino cruza el río por el punto del vado");
                        }
                        Assert.Greater(cruzan, 0, "en el banco hay algún camino que cruza un río");
                    }
                    var zooms = new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") };
                    var cauces2 = mapa.Cauces;
                    // el otro vado: el punto de `Rio.vados` más cercano al grupo de aristas de vado que eligió el banco
                    var otro = banco.SitiosDeAgua.TryGetValue("vado_rio", out var o) ? cauces2.Vados.OrderBy(v => Math.Pow(v.x - o.x, 2) + Math.Pow(v.y - o.y, 2)).First() : banco.Sitios["vado"];
                    foreach (var (k, sitio) in new[] { ("vado", banco.Sitios["vado"]), ("vado_rio", otro) })
                    {
                        // la columna, en el vado (la cámara lo centra) y escondida, para que no tape el cruce
                        yield return Llegar(mapa, almacen, sitio);
                        yield return new WaitForSecondsRealtime(1);
                        foreach (var (zoom, z) in zooms)
                        {
                            EsconderLaColumna();
                            yield return Vista(mapa, zoom, 30, $"v_{k}_{nombre}_{z}");
                            if (nombre == "C") continue;
                            // la luminancia media del núcleo del vado (a menos de 0,6 u del punto) y la del agua honda de al lado (el eje del río a 6 u)
                            var (dx, dy) = cauces2.DireccionEn(sitio.x, sitio.y);
                            float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                            float MediaLum(IEnumerable<(double x, double y)> pts)
                            {
                                double suma = 0;
                                int n = 0;
                                foreach (var (x, y) in pts)
                                {
                                    var w2 = mapa.Vista.WorldToScreenPoint(EscalaDelMapa.AEscena(x, y, (float)relieve.AlturaEn(x, y)));
                                    int px = (int)(w2.x * rt.width / mapa.Vista.pixelWidth), py = (int)(w2.y * rt.height / mapa.Vista.pixelHeight);
                                    if (px < 1 || py < 1 || px > rt.width - 2 || py > rt.height - 2 || w2.z <= 0) continue;
                                    suma += Lum(ultima.GetPixel(px, py)); n++;
                                }
                                return n > 0 ? (float)(suma / n) : float.NaN;
                            }
                            var nucleo = new List<(double, double)>();
                            var honda = new List<(double, double)>();
                            for (double t = -0.6; t <= 0.6; t += 0.15)
                                for (double u = -0.6; u <= 0.6; u += 0.15)
                                {
                                    nucleo.Add((sitio.x + dx * t - dy * u, sitio.y + dy * t + dx * u));
                                    foreach (var lejos in new[] { -6.0, 6.0 }) honda.Add((sitio.x + dx * (lejos + t) - dy * u * 0.4, sitio.y + dy * (lejos + t) + dx * u * 0.4));
                                }
                            float ln = MediaLum(nucleo), lh = MediaLum(honda);
                            Debug.Log($"[Vados] luminancia {k} {nombre} {z}: núcleo del vado {ln:0.000}, agua honda de al lado {lh:0.000}, contraste {ln / Math.Max(1e-4f, lh):0.00}");
                        }
                    }
                    // de cerca, el camino pasando por el punto: la columna 4 u al lado, el recorte centrado en el punto del vado
                    var (vx, vy) = banco.Sitios["vado"];
                    yield return Llegar(mapa, almacen, (vx + 4, vy + 1));
                    yield return new WaitForSecondsRealtime(1);
                    EsconderLaColumna();
                    yield return Vista(mapa, ZoomDelMapa.Cerca, 35, $"v_punto_{nombre}_entero");
                    var w = mapa.Vista.WorldToScreenPoint(EscalaDelMapa.AEscena(vx, vy, (float)relieve.AlturaEn(vx, vy)));
                    int cx0 = Mathf.Clamp((int)(w.x * rt.width / mapa.Vista.pixelWidth) - 500, 0, rt.width - 1000), cy0 = Mathf.Clamp((int)(w.y * rt.height / mapa.Vista.pixelHeight) - 281, 0, rt.height - 562);
                    var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
                    if (!string.IsNullOrEmpty(carpeta))
                    {
                        var rec = new Texture2D(1000, 562, TextureFormat.RGB24, false);
                        rec.SetPixels(ultima.GetPixels(cx0, cy0, 1000, 562));
                        rec.Apply();
                        File.WriteAllBytes(Path.Combine(carpeta, $"v_punto_{nombre}.png"), rec.EncodeToPNG());
                        UnityEngine.Object.Destroy(rec);
                    }
                }
            }
            finally
            {
                (TrazosEnElMapa.Estilo, TerritorioDelMapa.Estilo, PinturaDelMapa.VadoComoTrecho, PinturaDelMapa.VadoMedioLargo, PinturaDelMapa.VadoTransicion, MapaDeMundo.AclararCopas) = antes;
            }
        }

        /// <summary>La roca de los nodos «Prototipo» en Unity: cada cara, hacia fuera (con el espejo de z, el sentido de las caras se invierte).</summary>
        [Test, Category("Mapa")]
        public void LaRocaDeLosNodosMiraHaciaFuera()
        {
            var m = NodosDelMapa.MallaDeRoca();
            var v = m.vertices;
            var n = m.normals;
            var t = m.triangles;
            Assert.AreEqual(108, t.Length);
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]], centro = (a + b + c) / 3;
                Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), centro), 0, $"cara {i / 3}");
                Assert.Greater(Vector3.Dot(n[t[i]], centro), 0, $"normal {i / 3}");
            }
            UnityEngine.Object.DestroyImmediate(m);
        }

        /// <summary>El coste de los nodos en el peor caso del mundo: la vista alejada con más nodos alrededor (1536, 384: 687 nodos y 2.755 rocas
        /// en 320 u) con sombras, con las cajas de antes y con «Prototipo»: el fotograma, lo que cuesta mandar a dibujar y armar todas las teselas
        /// de golpe, y la tesela más cara.</summary>
        [Category("Mapa"), Explicit("coste de los nodos")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator ElCosteDeLosNodos()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "nodos-coste-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = NodosDelMapa.Estilo;
            try
            {
                var (almacen, sesion) = Almacen(url, (1536, 384), null);
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                var llena = mapa.Nodos.TeselaMasLlena();
                Debug.Log($"[Nodos] la tesela con más nodos: {llena.nodos} en ({llena.x}, {llena.y})");
                foreach (var (estilo, zoom) in new[] { (EstiloDeNodos.Cajas, ZoomDelMapa.Alejado), (EstiloDeNodos.Prototipo, ZoomDelMapa.Alejado),
                                                       (EstiloDeNodos.Mejorado, ZoomDelMapa.Alejado), (EstiloDeNodos.Cajas, ZoomDelMapa.Normal),
                                                       (EstiloDeNodos.Prototipo, ZoomDelMapa.Normal), (EstiloDeNodos.Mejorado, ZoomDelMapa.Normal) })
                {
                    NodosDelMapa.Estilo = estilo;
                    yield return Llegar(mapa, almacen, (1536, 384));
                    yield return Vista(mapa, zoom, 30, $"coste_{estilo}_{zoom}");
                    var fot = new List<double>();
                    var dib = new List<double>();
                    int rocas = 0;
                    for (int i = 0; i < 240; i++)
                    {
                        yield return null;
                        fot.Add(Time.unscaledDeltaTime * 1000);
                        dib.Add(mapa.Nodos.MsDibujar);
                        rocas = Math.Max(rocas, mapa.Nodos.RocasDibujadas + mapa.Nodos.ModelosDibujados);
                    }
                    fot.Sort();
                    dib.Sort();
                    double alcance = Math.Min(PinturaDelMapa.DistanciaCarga, mapa.Camara.Distancia * 7);
                    string armar = "";
                    if (estilo != EstiloDeNodos.Cajas)
                    {
                        armar = $"; modelos {mapa.Nodos.ModelosDibujados}, rocas o cascotes {mapa.Nodos.RocasDibujadas}";
                        mapa.Nodos.Soltar();
                        int antesT = mapa.Nodos.TeselasHechas;
                        mapa.Nodos.MsTeselaMax = 0;
                        var r = System.Diagnostics.Stopwatch.StartNew();
                        var presupuesto = NodosDelMapa.Presupuesto;
                        NodosDelMapa.Presupuesto = 1e9; // de golpe, para medir
                        mapa.Nodos.Actualizar(1536, 384, alcance);
                        NodosDelMapa.Presupuesto = presupuesto;
                        armar += $"; armar {mapa.Nodos.TeselasHechas - antesT} teselas de golpe {r.Elapsed.TotalMilliseconds:0.00} ms, la más cara {mapa.Nodos.MsTeselaMax:0.000} ms";
                    }
                    Debug.Log($"[Nodos] {estilo} {zoom} (alcance {alcance:0} u): fotograma mediana {fot[fot.Count / 2]:0.00} ms, p95 {fot[fot.Count * 95 / 100]:0.00} ms; " +
                              $"mandar a dibujar mediana {dib[dib.Count / 2]:0.000} ms, p95 {dib[dib.Count * 95 / 100]:0.000} ms; rocas dibujadas {rocas}{armar}");
                }
                // la tesela con más nodos, de cerca
                foreach (var estilo in new[] { EstiloDeNodos.Prototipo, EstiloDeNodos.Mejorado })
                {
                    NodosDelMapa.Estilo = estilo;
                    yield return Llegar(mapa, almacen, (llena.x, llena.y));
                    mapa.Nodos.Soltar();
                    mapa.Nodos.MsTeselaMax = 0;
                    mapa.Nodos.Actualizar(llena.x, llena.y, 1);
                    Debug.Log($"[Nodos] {estilo}: armar la tesela con más nodos ({llena.nodos}): {mapa.Nodos.MsTeselaMax:0.000} ms");
                }
            }
            finally { NodosDelMapa.Estilo = antes; }
        }

        /// <summary>Los sitios de las capturas de los nodos de recursos (tema 5), del mundo a98e65c7 (`tasks/referencias/nodos.cjs`): uno de cada
        /// tipo (el cobre, en montaña), tres de estaño de una veta juntos, uno de piedra en bosque cerrado (densidad 1) y tres junto
        /// al agua.</summary>
        static readonly (string nombre, double x, double y)[] SitiosDeNodos =
        {
            ("piedra", 2492.977, 768.842), ("cobre", 2395.008, 490.963), ("estano", 557.442, 187.906), ("oro", 2046.372, 1901.544),
            ("hierro", 2329.106, 617.327), ("veta", 2552.421, 494.308), ("bosque", 1554.961, 468.823),
            ("agua_cobre", 2316.501, 1571.321), ("agua_piedra", 1746.806, 1995.871), ("agua_hierro", 2171.743, 581.642),
        };

        /// <summary>Los dos nodos más empinados de cada tipo (lejos del borde del mundo).</summary>
        static readonly (string nombre, double x, double y)[] LaderasDeNodos =
        {
            ("piedra_1", 1170.018, 1834.136), ("piedra_2", 2280.894, 594.874), ("cobre_1", 2270.137, 203.501), ("cobre_2", 1995.994, 278.639),
            ("estano_1", 2181.599, 796.408), ("estano_2", 557.442, 187.906), ("oro_1", 2333.567, 1603.658), ("oro_2", 1147.906, 1837.479),
            ("hierro_1", 2042.354, 1154.377), ("hierro_2", 556.216, 143.522),
        };

        /// <summary>Lo que cambia entre dos capturas iguales salvo por lo que se mide: los píxeles (máscara) donde el color cambia más de 18 (suma
        /// de RGB), sin las sombras (el mismo color, solo más oscuro). Devuelve la máscara y su recuento.</summary>
        static (bool[] mascara, int n) Diferencia(Color32[] con, Color32[] sin)
        {
            var m = new bool[con.Length];
            int n = 0;
            for (int i = 0; i < con.Length; i++)
            {
                var a = con[i];
                var b = sin[i];
                if (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b) <= 18) continue;
                double ra = (a.r + 1.0) / (b.r + 1), ga = (a.g + 1.0) / (b.g + 1), ba = (a.b + 1.0) / (b.b + 1);
                double mx = Math.Max(ra, Math.Max(ga, ba)), mn = Math.Min(ra, Math.Min(ga, ba));
                if (mx < 1 && mx - mn < 0.1) continue; // sombra
                m[i] = true;
                n++;
            }
            return (m, n);
        }

        /// <summary>El color medio (sRGB) de los píxeles de la máscara cerca de (px, py) (radio r píxeles); y cuántos son.</summary>
        static (Color32 c, int n) ColorMedio(Color32[] px, bool[] m, int ancho, int px0, int py0, int r)
        {
            long sr = 0, sg = 0, sb = 0;
            int n = 0;
            int alto = px.Length / ancho;
            for (int y = Math.Max(0, py0 - r); y < Math.Min(alto, py0 + r); y++)
                for (int x = Math.Max(0, px0 - r); x < Math.Min(ancho, px0 + r); x++)
                {
                    int i = y * ancho + x;
                    if (!m[i] || (x - px0) * (x - px0) + (y - py0) * (y - py0) > r * r) continue;
                    sr += px[i].r; sg += px[i].g; sb += px[i].b; n++;
                }
            return n == 0 ? (new Color32(0, 0, 0, 255), 0) : (new Color32((byte)(sr / n), (byte)(sg / n), (byte)(sb / n), 255), n);
        }

        Color32[] Pixeles() => ultima.GetPixels32();

        /// <summary>Qué parte de los píxeles de la máscara cerca de (px, py) cumple `es` (el azul del estaño, el dorado del oro).</summary>
        static double Fraccion(Color32[] px, bool[] m, int ancho, int px0, int py0, int r, Func<Color32, bool> es)
        {
            int n = 0, si = 0, alto = px.Length / ancho;
            for (int y = Math.Max(0, py0 - r); y < Math.Min(alto, py0 + r); y++)
                for (int x = Math.Max(0, px0 - r); x < Math.Min(ancho, px0 + r); x++)
                {
                    int i = y * ancho + x;
                    if (!m[i] || (x - px0) * (x - px0) + (y - py0) * (y - py0) > r * r) continue;
                    n++;
                    if (es(px[i])) si++;
                }
            return n == 0 ? 0 : (double)si / n;
        }

        static bool EsAzul(Color32 c) => c.b > c.r + 25 && c.b > c.g + 8;
        static bool EsDorado(Color32 c) => c.r > 120 && c.r - c.b > 70 && c.g - c.b > 45;

        /// <summary>Los nodos de recursos en sus sitios, a los tres zooms y sin la columna, centrados en el nodo (la cámara mira a la columna, que va
        /// sobre él): `MUNDO_NODOS_ESTILO` da el estilo y el nombre de la tanda: «antes» (las cajas), «prototipo» o «mejorado». A zoom habitual y
        /// alejado, además, cuenta los píxeles del nodo (lo que cambia frente a la misma vista sin nodos; con «Mejorado», sin el modelo: solo el
        /// modelo). Con «Mejorado», también: de cerca, las dos laderas más fuertes de cada tipo; y el nodo en bosque cerrado sin el claro.</summary>
        [Category("Mapa"), Explicit("capturas de los nodos de recursos")]
        [UnityTest, Timeout(6_000_000)]
        public IEnumerator LosNodosDeRecursos()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            var tanda = Environment.GetEnvironmentVariable("MUNDO_NODOS_ESTILO");
            if (string.IsNullOrEmpty(tanda)) tanda = "antes";
            yield return Preparar(url, "nodos-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (MapaDeMundo.AclararCopas, NodosDelMapa.Estilo, NodosDelMapa.DibujarModelos, NodosDelMapa.ClaroEnLaVegetacion);
            MapaDeMundo.AclararCopas = false;
            var estilo = tanda == "antes" ? EstiloDeNodos.Cajas : tanda == "mejorado" ? EstiloDeNodos.Mejorado : EstiloDeNodos.Prototipo;
            NodosDelMapa.Estilo = estilo;
            try
            {
                var (almacen, sesion) = Almacen(url, (SitiosDeNodos[0].x, SitiosDeNodos[0].y), null);
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                // Los píxeles del nodo: la captura tal cual, y otra igual sin el nodo (o sin el modelo, con «Mejorado»).
                IEnumerator Contar(string k, ZoomDelMapa zoom, string z)
                {
                    // con el tiempo parado (el viento, las nubes y el agua no se mueven entre las dos capturas)
                    Time.timeScale = 0;
                    yield return Vista(mapa, zoom, 30, $"x_{k}_{tanda}_{z}_con");
                    var con = Pixeles();
                    if (estilo == EstiloDeNodos.Mejorado) NodosDelMapa.DibujarModelos = false; else NodosDelMapa.Estilo = EstiloDeNodos.Ninguno;
                    yield return Vista(mapa, zoom, 30, $"x_{k}_{tanda}_{z}_sin");
                    var sin = Pixeles();
                    NodosDelMapa.DibujarModelos = true;
                    NodosDelMapa.Estilo = estilo;
                    var (m, n) = Diferencia(con, sin);
                    var (c, nc) = ColorMedio(con, m, rt.width, rt.width / 2, rt.height / 2, rt.height / 3);
                    Time.timeScale = 1;
                    Debug.Log($"[Nodos] píxeles {k} {tanda} {z}: {n} (color medio sRGB {c.r},{c.g},{c.b} en {nc})");
                    yield return Vista(mapa, zoom, 30, $"n_{k}_{tanda}_{z}");
                }
                foreach (var (k, x, y) in SitiosDeNodos)
                {
                    // la columna, sobre el nodo (la cámara la sigue) y escondida en cada captura
                    yield return Llegar(mapa, almacen, (x, y));
                    yield return new WaitForSecondsRealtime(1);
                    foreach (var (zoom, z) in new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                    {
                        EsconderLaColumna();
                        yield return Vista(mapa, zoom, 30, $"n_{k}_{tanda}_{z}");
                        if (zoom != ZoomDelMapa.Cerca) yield return Contar(k, zoom, z);
                    }
                    if (k == "bosque" && estilo == EstiloDeNodos.Mejorado)
                    {
                        // el mismo nodo sin el claro en la vegetación (con el modelo y el parche)
                        NodosDelMapa.ClaroEnLaVegetacion = false;
                        yield return Llegar(mapa, almacen, (x + 0.001, y));
                        yield return new WaitForSecondsRealtime(3);
                        foreach (var (zoom, z) in new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                        {
                            EsconderLaColumna();
                            yield return Vista(mapa, zoom, 30, $"n_bosque_sinclaro_{z}");
                            if (zoom != ZoomDelMapa.Cerca) yield return Contar("bosque_sinclaro", zoom, z);
                        }
                        NodosDelMapa.ClaroEnLaVegetacion = true;
                    }
                }
                if (estilo == EstiloDeNodos.Mejorado)
                    foreach (var (k, x, y) in LaderasDeNodos)
                    {
                        yield return Llegar(mapa, almacen, (x, y));
                        yield return new WaitForSecondsRealtime(1);
                        EsconderLaColumna();
                        // más desde arriba (55°): de cerca, en la ladera, a 30° la cámara se metía en el monte
                        yield return Vista(mapa, ZoomDelMapa.Cerca, 55, $"l_{k}_{tanda}");
                        Debug.Log($"[Nodos] ladera {k}: pendiente media en la huella {Pendiente(mapa, x, y):0.00}");
                    }
            }
            finally { (MapaDeMundo.AclararCopas, NodosDelMapa.Estilo, NodosDelMapa.DibujarModelos, NodosDelMapa.ClaroEnLaVegetacion) = antes; Time.timeScale = 1; }
        }

        /// <summary>La pendiente de la malla de suelo (alto / horizontal) entre lo más alto y lo más bajo de la huella de un nodo, a 0,5 u.</summary>
        static double Pendiente(MapaDeMundo mapa, double x, double y)
        {
            double mx = double.MinValue, mn = double.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4, h = mapa.Terreno.AlturaPintada(x + Math.Cos(a) * 0.5, y + Math.Sin(a) * 0.5);
                mx = Math.Max(mx, h);
                mn = Math.Min(mn, h);
            }
            return (mx - mn) / (EscalaDelMapa.MetrosPorUnidad * 1.0);
        }

        /// <summary>Las láminas de los nodos «Mejorado» (con las de «Prototipo» al lado): sobre la pradera, la tierra seca y la montaña del banco,
        /// los cinco tipos en fila (a 3 u) puestos para la prueba (nodos de más en el artefacto); y en la pradera, las tres variantes de cada
        /// tipo. Y el color medio de cada tipo en cada suelo a zoom habitual y alejado (los píxeles del modelo, sin sombras), con la distancia
        /// entre pares.</summary>
        [Category("Mapa"), Explicit("láminas de los nodos de recursos")]
        [UnityTest, Timeout(6_000_000)]
        public IEnumerator LasLaminasDeNodos()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "nodos-laminas-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var banco = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            string[] tipos = { "piedra", "cobre", "estano", "oro", "hierro" };
            var suelos = new[] { ("pradera", "suelo_pradera"), ("seco", "suelo_seco"), ("montana", "suelo_roca") }.Where(s => banco.SitiosDeSuelo.ContainsKey(s.Item2)).ToArray();
            // los nodos de la prueba: en cada suelo, una fila de los cinco tipos 12 u al norte del sitio (lejos de sus caminos, que aquí no se ponen)
            var puestos = new List<NodoRecurso>();
            var filas = new Dictionary<string, (double x, double y)>();
            foreach (var (s, k) in suelos)
            {
                var (cx, cy) = banco.SitiosDeSuelo[k];
                filas[s] = (cx, cy + 12);
                for (int t = 0; t < 5; t++)
                    puestos.Add(new NodoRecurso { Id = $"prueba-{s}-{tipos[t]}", Tipo = tipos[t], Rareza = "comun", Posicion = Pu(cx + (t - 2) * 3, cy + 12) });
            }
            // las variantes, en la pradera: la fila de cada variante, 3 u más al norte; cada nodo, donde el hash le da esa variante
            (double x, double y) variantes = default;
            if (filas.TryGetValue("pradera", out var fp))
            {
                variantes = (fp.x, fp.y + 6);
                for (int v = 0; v < 3; v++)
                    for (int t = 0; t < 5; t++)
                    {
                        double x = fp.x + (t - 2) * 3, y = fp.y + 3 + v * 3;
                        for (int i = 0; i < 400 && Math.Min(2, (int)(RocasDeNodos.Azar(x + y * 0.37, 91) * 3)) != v; i++) x += 0.0625;
                        puestos.Add(new NodoRecurso { Id = $"prueba-v{v + 1}-{tipos[t]}", Tipo = tipos[t], Rareza = "comun", Posicion = Pu(x, y) });
                    }
            }
            artefacto.Nodos.AddRange(puestos);
            var antes = (MapaDeMundo.AclararCopas, NodosDelMapa.Estilo, NodosDelMapa.DibujarModelos);
            MapaDeMundo.AclararCopas = false;
            var colores = new List<string>();
            try
            {
                foreach (var (estilo, tanda) in new[] { (EstiloDeNodos.Mejorado, "mejorado"), (EstiloDeNodos.Prototipo, "prototipo") })
                {
                    NodosDelMapa.Estilo = estilo;
                    if (go != null) { UnityEngine.Object.Destroy(go); yield return null; }
                    var (almacen, sesion) = Almacen(url, filas.Values.First(), null);
                    var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                    mapa.RadioVisionFijo = 15;
                    foreach (var (s, _) in suelos)
                    {
                        var (fx, fy) = filas[s];
                        yield return Llegar(mapa, almacen, (fx, fy));
                        yield return new WaitForSecondsRealtime(1);
                        foreach (var (zoom, z) in new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                        {
                            EsconderLaColumna();
                            yield return Vista(mapa, zoom, 30, $"f_{s}_{tanda}_{z}");
                            if (zoom == ZoomDelMapa.Cerca) continue;
                            Time.timeScale = 0;
                            yield return Vista(mapa, zoom, 30, $"x_f_{s}_{tanda}_{z}_con");
                            var con = Pixeles();
                            if (estilo == EstiloDeNodos.Mejorado) NodosDelMapa.DibujarModelos = false; else NodosDelMapa.Estilo = EstiloDeNodos.Ninguno;
                            yield return Vista(mapa, zoom, 30, $"x_f_{s}_{tanda}_{z}_sin");
                            var sin = Pixeles();
                            NodosDelMapa.DibujarModelos = true;
                            NodosDelMapa.Estilo = estilo;
                            var (m, _) = Diferencia(con, sin);
                            Time.timeScale = 1;
                            // cada tipo: los píxeles de la máscara cerca de donde cae su nodo en la pantalla
                            var cam = mapa.Vista;
                            for (int t = 0; t < 5; t++)
                            {
                                double nx = fx + (t - 2) * 3;
                                var w = cam.WorldToScreenPoint(EscalaDelMapa.AEscena(nx, fy, mapa.Terreno.AlturaPintada(nx, fy) + 2));
                                // (la captura sale de abajo arriba, como la pantalla)
                                int r = (int)Math.Max(6, rt.width * (zoom == ZoomDelMapa.Normal ? 0.03 : 0.015));
                                var (c, n) = ColorMedio(con, m, rt.width, (int)w.x, (int)w.y, r);
                                colores.Add($"{tanda};{s};{z};{tipos[t]};{c.r};{c.g};{c.b};{n}");
                                if (tipos[t] == "estano" || tipos[t] == "oro")
                                    Debug.Log($"[Nodos] fracción {tanda} {s} {z} {tipos[t]}: {(tipos[t] == "estano" ? "azul" : "dorado")} " +
                                              $"{Fraccion(con, m, rt.width, (int)w.x, (int)w.y, r, tipos[t] == "estano" ? EsAzul : EsDorado):P0}");
                            }
                            yield return Vista(mapa, zoom, 30, $"f_{s}_{tanda}_{z}");
                        }
                    }
                    if (variantes != default)
                    {
                        yield return Llegar(mapa, almacen, variantes);
                        yield return new WaitForSecondsRealtime(1);
                        EsconderLaColumna();
                        yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"v_variantes_{tanda}_1_cerca");
                        yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"v_variantes_{tanda}_2_habitual");
                    }
                }
            }
            finally
            {
                foreach (var n in puestos) artefacto.Nodos.Remove(n);
                Time.timeScale = 1;
                (MapaDeMundo.AclararCopas, NodosDelMapa.Estilo, NodosDelMapa.DibujarModelos) = antes;
            }
            foreach (var l in colores) Debug.Log("[Nodos] color;" + l);
        }

        /// <summary>Los asentamientos vistos desde el mundo (tema 6), con el banco de asentamientos (`BancoDeAsentamientos`): cada cosa centrada,
        /// a los tres zooms y sin la columna, y las láminas de cada fila (plazas con muralla y sin ella, aldeas, avanzadas, y obras, campamentos,
        /// alijo y batallas) a zoom habitual y alejado. `MUNDO_ASENT_ESTILO` da el estilo y el nombre de la tanda: «antes» (las cajas) o
        /// «prototipo» o «mejorado» (la miniatura de Blender donde la hay).</summary>
        [Category("Mapa"), Explicit("capturas de los asentamientos")]
        [UnityTest, Timeout(6_000_000)]
        public IEnumerator LosAsentamientos()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            var tanda = Environment.GetEnvironmentVariable("MUNDO_ASENT_ESTILO");
            if (string.IsNullOrEmpty(tanda)) tanda = "antes";
            yield return Preparar(url, "asentamientos-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            var banco = BancoDeAsentamientos.Hacer(caminos, relieve, artefacto);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo);
            MapaDeMundo.AclararCopas = false;
            MaquetasDelMapa.Estilo = tanda == "antes" ? EstiloDeAsentamientos.Cajas : tanda.StartsWith("mejorado") ? EstiloDeAsentamientos.Mejorado : EstiloDeAsentamientos.Prototipo;
            try
            {
                var primero = banco.Sitios.Values.First();
                var (almacen, sesion) = Almacen(url, primero, null);
                almacen.Aplicar(new MensajeDeServidor { Foto = banco.Vista() });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                foreach (var kv in banco.Sitios)
                {
                    yield return Llegar(mapa, almacen, kv.Value);
                    yield return new WaitForSecondsRealtime(1);
                    foreach (var (zoom, z) in new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                    { EsconderLaColumna(); yield return Vista(mapa, zoom, 30, $"s_{kv.Key}_{tanda}_{z}"); }
                }
                foreach (var kv in banco.Filas)
                {
                    yield return Llegar(mapa, almacen, kv.Value);
                    yield return new WaitForSecondsRealtime(1);
                    foreach (var (zoom, z) in new[] { (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                    { EsconderLaColumna(); yield return Vista(mapa, zoom, 30, $"f_{kv.Key}_{tanda}_{z}"); }
                }
                if (mapa.Maquetas != null) Debug.Log($"[Asentamientos] {tanda}: {mapa.Maquetas.PiezasPuestas} piezas, {mapa.Maquetas.Llamadas} llamadas, {mapa.Maquetas.Triangulos} triángulos; rehacer {mapa.Maquetas.MsRehacer:0.00} ms, asentar {mapa.Maquetas.MsAsentar:0.00} ms");
            }
            finally { (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo) = antes; }
        }

        /// <summary>Las plazas modeladas («Mejorado») frente al prototipo, en el mapa (capturas de revisión, `MUNDO_CAPTURAS`), con la huella
        /// definitiva (`CosasDelMapa.HuellaDePlaza`: la del prototipo, nunca menos de 0,9 u):
        /// * nivel 1, en la hierba, en lo seco y en una ladera; en cada sitio el caserío recién fundado (zona de 12 u, Facción f-1) y, a 5 u (más
        ///   cerca de lo que deja el motor, 20 u, para que salgan los dos en el encuadre), el crecido (zona de 20 u, f-2): el prototipo y las dos
        ///   variantes a las tres distancias;
        /// * el adelanto del nivel 3 (zona de 35 u), con muralla y sin ella, a zoom habitual y de cerca;
        /// * el suelo pisado en lo seco con tres oscurecidos (`MaquetasDelMapa.OscuroEnSeco`), aparte, para elegir.
        /// Cada sitio, en tierra firme y al borde de lo despejado (con árboles cerca, para la proporción).</summary>
        [Category("Mapa"), Explicit("capturas de las plazas modeladas")]
        [UnityTest, Timeout(6_000_000)]
        public IEnumerator LasPlazasModeladas()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "plazas-modeladas-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            var bosque = new RasterDeMundo(artefacto.DensidadBosque);
            // el sitio de tierra firme más cercano sin agua ni bosque espeso a menos de `r`, pero con bosque a menos de `r` + 3
            var puestos = new List<(double x, double y)>();
            var caucesSitio = new CaucesDelMapa(artefacto, relieve);
            (double x, double y) Sitio((double x, double y) p, double r)
            {
                bool Libre(double x, double y, double rr, bool exigirArboles)
                {
                    bool arboles = false;
                    double hMin = double.MaxValue, hMax = double.MinValue;
                    for (double dy = -rr - 3; dy <= rr + 3; dy += 0.5)
                        for (double dx = -rr - 3; dx <= rr + 3; dx += 0.5)
                        {
                            double d = Math.Sqrt(dx * dx + dy * dy);
                            if (d > rr + 3) continue;
                            if (d <= rr && (relieve.AguaEn(x + dx, y + dy) || bosque.En(x + dx, y + dy) > 20)) return false;
                            // (ni ríos: los cauces no son agua del relieve)
                            if (d <= rr && dx % 2 == 0 && dy % 2 == 0 && caucesSitio.ACauce(x + dx, y + dy, 3) < 2) return false;
                            if (d <= rr && dx % 2 == 0 && dy % 2 == 0)
                            {
                                double hh = relieve.AlturaEn(x + dx, y + dy);
                                hMin = Math.Min(hMin, hh);
                                hMax = Math.Max(hMax, hh);
                                // (y llano: no más de 30 m de desnivel bajo la plaza)
                                if (hMax - hMin > 30) return false;
                            }
                            if (d > rr && bosque.En(x + dx, y + dy) > 40) arboles = true;
                        }
                    return arboles || !exigirArboles;
                }
                // (y lejos de los sitios ya puestos: al menos 14 u, para que no salgan en el encuadre de otro)
                bool Aparte(double x, double y) => puestos.All(q => Math.Sqrt((q.x - x) * (q.x - x) + (q.y - y) * (q.y - y)) > 14 + r);
                foreach (var exigir in new[] { true, false })
                    for (double d = 0; d <= 400; d += d < 60 ? 1.5 : 4)
                        for (int k = 0; k < Math.Max(1, (int)(d * 2)); k++)
                        {
                            double a = k * Math.PI * 2 / Math.Max(1, (int)(d * 2)), x = p.x + d * Math.Cos(a), y = p.y + d * Math.Sin(a);
                            if (Aparte(x, y) && Libre(x, y, r, exigir) && Libre(x + 5, y, r, false)) { puestos.Add((x, y)); return (x, y); }
                        }
                Debug.LogWarning($"[Asentamientos] sin sitio despejado de {r} u cerca de ({p.x:0}, {p.y:0})");
                puestos.Add(p);
                return p;
            }
            var sitios = new List<(string nombre, (double x, double y) donde, int nivel, bool muralla)>
            {
                ("hierba", Sitio(caminos.SitiosDeSuelo["suelo_pradera"], 2.5), 1, false),
                ("seco", Sitio(caminos.SitiosDeSuelo["suelo_seco"], 2.5), 1, false),
                ("ladera", caminos.Sitios["ladera"], 1, false),
            };
            puestos.Add(caminos.Sitios["ladera"]);
            var n3 = Sitio((caminos.SitiosDeSuelo["suelo_pradera"].x + 30, caminos.SitiosDeSuelo["suelo_pradera"].y), 4.5);
            var n3s = Sitio((n3.x, n3.y + 16), 4.5);
            sitios.Add(("n3_muralla", n3, 3, true));
            // el nivel 2 (zona de 25 u), en la pradera
            sitios.Add(("n2", Sitio((caminos.SitiosDeSuelo["suelo_pradera"].x - 20, caminos.SitiosDeSuelo["suelo_pradera"].y + 10), 4), 2, false));
            sitios.Add(("n3_sin", n3s, 3, false));
            // los niveles 4 (zona de 45 u) y 5 (55 u), sin muralla (la muralla es de la parte 3)
            // (sin agua ni bosque espeso dentro de la huella y de su suelo pisado: hasta unas 7 y 8,5 u)
            // (lejos de los ríos de la pradera: junto a la ladera seca)
            sitios.Add(("n4", Sitio((caminos.Sitios["ladera"].x + 40, caminos.Sitios["ladera"].y + 10), 7), 4, false));
            sitios.Add(("n5", Sitio((caminos.Sitios["ladera"].x - 40, caminos.Sitios["ladera"].y + 10), 8.5), 5, false));
            Punto Pu(double x, double y) => new Punto { X = (long)(x * 1000), Y = (long)(y * 1000) };
            // la zona de la plaza: un círculo de 24 puntos de radio `rz` u
            IEnumerable<Punto> Zona(double x, double y, double rz) =>
                Enumerable.Range(0, 24).Select(i => Pu(x + rz * Math.Cos(i * Math.PI / 12), y + rz * Math.Sin(i * Math.PI / 12)));
            var vista = new Foto { Ambito = new BronzeAge.Contract.V1.Ambito { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
            int k_ = 0;
            foreach (var (nombre, (x, y), nivel, muralla) in sitios)
            {
                var a = new Asentamiento { Id = $"as-{k_++}", Nombre = nombre, Nivel = (uint)nivel, FaccionId = "f-1", Posicion = Pu(x, y), TieneMuralla = muralla };
                a.Zona.AddRange(Zona(x, y, nivel == 1 ? 12 : nivel == 2 ? 25 : nivel == 3 ? 35 : nivel == 4 ? 45 : 55));
                vista.Entidades.Add(new Entidad { Asentamiento = a });
                if (nivel == 1)
                {
                    var b = new Asentamiento { Id = $"as-{k_++}", Nombre = nombre + " 2", Nivel = 1, FaccionId = "f-2", Posicion = Pu(x + 5, y), TieneMuralla = false };
                    b.Zona.AddRange(Zona(x + 5, y, 20));
                    vista.Entidades.Add(new Entidad { Asentamiento = b });
                }
                Debug.Log($"[Asentamientos] sitio {nombre}: ({x:0.0}, {y:0.0}), altura {relieve.AlturaEn(x, y):0} m");
            }
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, MaquetasDelMapa.VarianteForzada, MaquetasDelMapa.OscuroEnSeco);
            MapaDeMundo.AclararCopas = false;
            try
            {
                var (almacen, sesion) = Almacen(url, sitios[0].donde, null);
                almacen.Aplicar(new MensajeDeServidor { Foto = vista });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                IEnumerator Poner(EstiloDeAsentamientos estilo, int variante)
                {
                    (MaquetasDelMapa.Estilo, MaquetasDelMapa.VarianteForzada) = (estilo, variante);
                    mapa.Maquetas?.Olvidar();
                    yield return null;
                    yield return null;
                }
                var zooms = new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") };
                foreach (var (nombre, donde, nivel, _) in sitios)
                {
                    // el encuadre, en el medio de la pareja (nivel 1) o en la plaza (nivel 3)
                    var centro = nivel == 1 ? (donde.x + 2.5, donde.y) : donde;
                    yield return Llegar(mapa, almacen, centro);
                    yield return new WaitForSecondsRealtime(1);
                    var tandas = nivel <= 2
                        ? new[] { (EstiloDeAsentamientos.Prototipo, 0, "prototipo"), (EstiloDeAsentamientos.Mejorado, 1, "mejorado_v1"), (EstiloDeAsentamientos.Mejorado, 2, "mejorado_v2") }
                        : new[] { (EstiloDeAsentamientos.Prototipo, 0, "prototipo"), (EstiloDeAsentamientos.Mejorado, 0, "mejorado") };
                    foreach (var (estilo, variante, t) in tandas)
                    {
                        yield return Poner(estilo, variante);
                        foreach (var (zoom, z) in nivel != 3 ? zooms : zooms.Take(2).ToArray())
                        { EsconderLaColumna(); yield return Vista(mapa, zoom, 30, $"p_{nombre}_{t}_{z}"); }
                    }
                }
                // el suelo pisado en lo seco con tres oscurecidos (capturas aparte, para elegir)
                {
                    var seco = sitios.First(t => t.nombre == "seco");
                    yield return Llegar(mapa, almacen, (seco.donde.x + 2.5, seco.donde.y));
                    yield return new WaitForSecondsRealtime(1);
                    foreach (var o in new[] { 0.82f, 0.62f, 0.5f })
                    {
                        MaquetasDelMapa.OscuroEnSeco = o;
                        yield return Poner(EstiloDeAsentamientos.Mejorado, 1);
                        EsconderLaColumna();
                        yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"o_seco_{Mathf.RoundToInt(o * 100)}_2_habitual");
                    }
                }
            }
            finally { (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, MaquetasDelMapa.VarianteForzada, MaquetasDelMapa.OscuroEnSeco) = antes; }
        }

        /// <summary>Para medir lo claro que se ve lo construido frente al suelo (capturas de revisión, `MUNDO_CAPTURAS`): en la hierba y en lo
        /// seco, la pareja de caseríos de nivel 1 (zonas de 12 y 20 u) y, en la pradera, el nivel 3 (zona de 35 u, sin muralla), a zoom
        /// habitual: sin las miniaturas («l_…_suelo»), con ellas sin su suelo pisado («l_…_casas») y lo mismo sin su sombra en el suelo («l_…_molde»,
        /// para saber qué píxeles son lo construido). La cuenta se hace fuera, con las tres.</summary>
        [Category("Mapa"), Explicit("luminancia de las plazas")]
        [UnityTest, Timeout(1_800_000)]
        public IEnumerator LaLuminanciaDeLasPlazas()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "plazas-luz-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
            var sitios = new List<(string nombre, (double x, double y) donde)>();
            int k_ = 0;
            void Plaza(uint nivel, string faccion, double x, double y, double zona, int n2 = 0)
            {
                var a = new Asentamiento { Id = $"as-{k_++}", Nombre = "p", Nivel = nivel, FaccionId = faccion, Posicion = Pu(x, y), TieneMuralla = false };
                for (int i = 0; i < 24; i++) a.Zona.Add(Pu(x + zona * Math.Cos(i * Math.PI / 12), y + zona * Math.Sin(i * Math.PI / 12)));
                vista.Entidades.Add(new Entidad { Asentamiento = a });
            }
            foreach (var (nombre, suelo) in new[] { ("hierba", "suelo_pradera"), ("seco", "suelo_seco") })
            {
                var (x, y) = caminos.SitiosDeSuelo[suelo];
                Plaza(1, "f-1", x, y, 12);
                Plaza(1, "f-2", x + 5, y, 20);
                sitios.Add((nombre, (x + 2.5, y)));
            }
            {
                var (x, y) = caminos.SitiosDeSuelo["suelo_pradera"];
                Plaza(3, "f-1", x + 30, y, 35);
                sitios.Add(("n3", (x + 30, y)));
                // el nivel 2, en tierra firme cerca de la pradera (sin agua a menos de 4 u)
                var (x2, y2) = (x, y + 25.0);
                for (double d = 0; d < 80; d += 2)
                {
                    (x2, y2) = (x + d * Math.Cos(d), y + 25 + d * Math.Sin(d));
                    bool seco = true;
                    for (int i = 0; i < 16 && seco; i++) seco = !relieve.AguaEn(x2 + 4 * Math.Cos(i * Math.PI / 8), y2 + 4 * Math.Sin(i * Math.PI / 8));
                    if (seco && !relieve.AguaEn(x2, y2)) break;
                }
                Plaza(2, "f-2", x2, y2, 25);
                sitios.Add(("n2", (x2, y2)));
                // los niveles 4 y 5, en tierra firme, lejos de lo demás
                foreach (var (nivel, zona, nombre, oy) in new[] { (4u, 45.0, "n4", -35.0), (5u, 55.0, "n5", -70.0) })
                {
                    var (x4, y4) = (x, y + oy);
                    for (double d = 0; d < 120; d += 2)
                    {
                        (x4, y4) = (x + d * Math.Cos(d), y + oy + d * Math.Sin(d));
                        bool seco = true;
                        for (int i = 0; i < 16 && seco; i++) seco = !relieve.AguaEn(x4 + 6 * Math.Cos(i * Math.PI / 8), y4 + 6 * Math.Sin(i * Math.PI / 8));
                        if (seco && !relieve.AguaEn(x4, y4) && sitios.All(q => Math.Abs(q.donde.x - x4) + Math.Abs(q.donde.y - y4) > 25)) break;
                    }
                    Plaza(nivel, "f-1", x4, y4, zona);
                    sitios.Add((nombre, (x4, y4)));
                }
            }
            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, MaquetasDelMapa.DibujarMiniaturas, MaquetasDelMapa.DibujarSuelo);
            (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo) = (false, EstiloDeAsentamientos.Mejorado);
            try
            {
                var (almacen, sesion) = Almacen(url, sitios[0].donde, null);
                almacen.Aplicar(new MensajeDeServidor { Foto = vista });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                foreach (var (nombre, donde) in sitios)
                {
                    yield return Llegar(mapa, almacen, donde);
                    yield return new WaitForSecondsRealtime(1);
                    // (el tiempo parado: que el viento no mueva la hierba entre una captura y otra)
                    Time.timeScale = 0;
                    foreach (var (casas, sombra, t) in new[] { (false, true, "suelo"), (true, true, "casas"), (true, false, "molde") })
                    {
                        (MaquetasDelMapa.DibujarMiniaturas, MaquetasDelMapa.DibujarSuelo, MaquetasDelMapa.SombraDeMiniaturas) = (casas, false, sombra);
                        EsconderLaColumna();
                        yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"l_{nombre}_{t}");
                    }
                    MaquetasDelMapa.SombraDeMiniaturas = true;
                    // el prototipo, con sus cajas y sin ellas (para medir lo que ocupa en pantalla)
                    MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Prototipo;
                    mapa.Maquetas?.Olvidar();
                    yield return null;
                    foreach (var (cajas, t) in new[] { (false, "proto_vacio"), (true, "proto") })
                    {
                        MaquetasDelMapa.DibujarCajas = cajas;
                        EsconderLaColumna();
                        yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"l_{nombre}_{t}");
                    }
                    MaquetasDelMapa.DibujarCajas = true;
                    MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
                    mapa.Maquetas?.Olvidar();
                    yield return null;
                }
            }
            finally { (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, MaquetasDelMapa.DibujarMiniaturas, MaquetasDelMapa.DibujarSuelo) = antes; Time.timeScale = 1; }
        }

        /// <summary>El banco de las plazas modeladas para las pruebas sin capturas: junto a la pradera, cuatro plazas de nivel 1 (dos de la
        /// Facción f-1, con zona de 12 u; dos de la f-2, con 20 u) y dos de nivel 3 (zona de 35 u; f-1 con muralla, f-2 sin ella), todas a la
        /// vista a zoom habitual.</summary>
        static (Foto foto, (double x, double y) centro) BancoDePlazasModeladas(BancoDeCaminos caminos)
        {
            var (cx, cy) = caminos.SitiosDeSuelo["suelo_pradera"];
            var foto = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
            void Plaza(string id, uint nivel, string faccion, double x, double y, double zona, bool muralla)
            {
                var a = new Asentamiento { Id = id, Nombre = id, Nivel = nivel, FaccionId = faccion, Posicion = Pu(x, y), TieneMuralla = muralla };
                for (int i = 0; i < 24; i++) a.Zona.Add(Pu(x + zona * Math.Cos(i * Math.PI / 12), y + zona * Math.Sin(i * Math.PI / 12)));
                foto.Entidades.Add(new Entidad { Asentamiento = a });
            }
            Plaza("as-1a", 1, "f-1", cx - 5, cy - 3, 12, false);
            Plaza("as-1b", 1, "f-1", cx - 5, cy + 3, 12, false);
            Plaza("as-1c", 1, "f-2", cx + 5, cy - 3, 20, false);
            Plaza("as-1d", 1, "f-2", cx + 5, cy + 3, 20, false);
            Plaza("as-3m", 3, "f-1", cx - 1, cy - 9, 35, true);
            Plaza("as-3s", 3, "f-2", cx + 1, cy + 9, 35, false);
            return (foto, (cx, cy));
        }

        /// <summary>Lo que se manda a dibujar de las miniaturas («Mejorado»): una villa y un suelo por plaza; la muralla, solo en la que la tiene;
        /// cada plaza con el color de su Facción (distinto entre f-1 y f-2, el mismo dentro de cada una, y no el blanco); la variante de cada
        /// plaza de nivel 1, la de su semilla; el aro y el clic, con lo dibujado; la huella con su mínimo (0,60 → 0,90; 0,99 y 2,8, igual); y
        /// con «Prototipo», la del prototipo.</summary>
        [Category("Mapa")]
        [UnityTest, Timeout(600_000)]
        public IEnumerator LasMiniaturasQueSeDibujan()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "plazas-encargos-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var (foto, centro) = BancoDePlazasModeladas(BancoDeCaminos.Hacer(relieve, artefacto, datos));
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            var antes = (MaquetasDelMapa.Estilo, MaquetasDelMapa.VarianteForzada);
            (MaquetasDelMapa.Estilo, MaquetasDelMapa.VarianteForzada) = (EstiloDeAsentamientos.Mejorado, 0);
            try
            {
                var (almacen, sesion) = Almacen(url, centro, null);
                almacen.Aplicar(new MensajeDeServidor { Foto = foto });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                yield return Llegar(mapa, almacen, centro);
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(mapa.Maquetas.HayModelos, Is.True, "los modelos de «Mejorado» están en Resources");
                var en = mapa.Maquetas.Encargos(mapa.Vista);
                var villas = en.Where(e => !e.EsMuralla && !e.EsSuelo).ToList();
                Assert.That(villas.Count, Is.EqualTo(6), "una villa por plaza");
                Assert.That(en.Count(e => e.EsSuelo), Is.EqualTo(6), "un suelo pisado por plaza");
                Assert.That(en.Count(e => e.EsMuralla), Is.EqualTo(1), "la muralla, solo en la plaza de nivel 3 que la tiene");
                // el color de la Facción: el mismo en las de cada una (por la posición: las de f-1, a la izquierda o abajo), distinto entre ellas
                var f1 = villas.Where(v => v.M.GetColumn(3).x < centro.x * EscalaDelMapa.MetrosPorUnidad - 0.5 * EscalaDelMapa.MetrosPorUnidad).Select(v => v.Faccion).Distinct().ToList();
                var f2 = villas.Where(v => v.M.GetColumn(3).x > centro.x * EscalaDelMapa.MetrosPorUnidad + 0.5 * EscalaDelMapa.MetrosPorUnidad).Select(v => v.Faccion).Distinct().ToList();
                Assert.That(f1.Count, Is.EqualTo(1), "f-1, un solo color");
                Assert.That(f2.Count, Is.EqualTo(1), "f-2, un solo color");
                Assert.That(f1[0], Is.Not.EqualTo(f2[0]), "f-1 y f-2, de color distinto");
                Assert.That(f1[0], Is.Not.EqualTo(Vector4.one), "no es el blanco de la máscara");
                // la variante de cada plaza de nivel 1, la de su semilla (el material lleva el nombre del modelo)
                foreach (var e in foto.Entidades.Select(x => x.Asentamiento).Where(a => a.Nivel == 1))
                {
                    double x = e.Posicion.X / 1000.0, y = e.Posicion.Y / 1000.0;
                    var pos = EscalaDelMapa.AEscena(x, y, 0);
                    var suya = villas.OrderBy(v => Vector2.Distance(new Vector2(v.M.GetColumn(3).x, v.M.GetColumn(3).z), new Vector2(pos.x, pos.z))).First();
                    int v_ = MaquetasDelMapa.VarianteDe(MaquetasDelPrototipo.Semilla(x, y), 2);
                    StringAssert.StartsWith($"plaza1_{v_ + 1} ", suya.Material.name, $"{e.Id}: su variante");
                }
                // el aro y el clic, con lo dibujado: el radio de cada plaza (`CosaDelMapa.Radio`, del que salen los dos) es la escala de su
                // miniatura, y el clic, ese radio más la holgura
                foreach (var c in mapa.Cosas.Where(c => c.Clase == ClaseDeCosa.Plaza))
                {
                    var pos = EscalaDelMapa.AEscena(c.X, c.Y, 0);
                    var suya = villas.OrderBy(v => Vector2.Distance(new Vector2(v.M.GetColumn(3).x, v.M.GetColumn(3).z), new Vector2(pos.x, pos.z))).First();
                    double dibujado = suya.M.lossyScale.x / EscalaDelMapa.MetrosPorUnidad;
                    Assert.That(c.Radio, Is.EqualTo(dibujado).Within(1e-3), $"{c.Id}: el radio del aro, el de lo dibujado");
                    Assert.That(c.RadioClic, Is.EqualTo(c.Radio + 0.12).Within(1e-9), $"{c.Id}: el clic");
                }
                // la de nivel 1 recién fundada (zona de 12 u): de 0,60 a 0,90 u; la crecida (20 u), sin cambio (0,99); la de nivel 3 (35 u), igual
                double R(string id) => mapa.Cosas.First(c => c.Id == id).Radio;
                Assert.That(R("as-1a"), Is.EqualTo(0.9).Within(1e-3));
                Assert.That(R("as-1c"), Is.EqualTo(MaquetasDelPrototipo.RadioDePlaza(2.0, 1)).Within(1e-3));
                Assert.That(R("as-3m"), Is.EqualTo(MaquetasDelPrototipo.RadioDePlaza(3.5, 3)).Within(1e-3));
                // con «Prototipo», todo como el prototipo
                MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Prototipo;
                for (int i = 0; i < 70; i++) yield return null; // (las cosas se reúnen dos veces por segundo)
                Assert.That(R("as-1a"), Is.EqualTo(MaquetasDelPrototipo.RadioDePlaza(1.2, 1)).Within(1e-3), "«Prototipo»: la huella del prototipo");
                Assert.That(R("as-3m"), Is.EqualTo(MaquetasDelPrototipo.RadioDePlaza(3.5, 3)).Within(1e-3));
            }
            finally { (MaquetasDelMapa.Estilo, MaquetasDelMapa.VarianteForzada) = antes; }
        }

        /// <summary>El coste por fotograma de las plazas modeladas frente al prototipo, con sombras: una plaza de cada nivel del 1 al 5 (sin muralla,
        /// con zonas de 12, 25, 35, 45 y 55 u) a zoom habitual y de cerca; por estilo, la mediana del hilo principal (CPU) y de la GPU (FrameTimingManager)
        /// en 300 fotogramas, y lo que cuesta mandar a dibujar.</summary>
        [Category("Mapa"), Explicit("coste de las plazas modeladas")]
        [UnityTest, Timeout(1_800_000)]
        public IEnumerator ElCosteDeLasPlazasModeladas()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "plazas-coste-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var (cx, cy) = BancoDeCaminos.Hacer(relieve, artefacto, datos).SitiosDeSuelo["suelo_pradera"];
            var centro = (cx, cy);
            // una plaza de cada nivel, del 1 al 5, con su zona verosímil, todas a la vista a zoom habitual
            var foto = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
            foreach (var (nivel, zona, dx, dy) in new[] { (1u, 12.0, -9.0, 4.0), (2u, 25.0, -4.0, -6.0), (3u, 35.0, 3.0, 5.0), (4u, 45.0, 10.0, -5.0), (5u, 55.0, 0.0, 14.0) })
            {
                var a = new Asentamiento { Id = $"as-n{nivel}", Nombre = $"n{nivel}", Nivel = nivel, FaccionId = nivel % 2 == 0 ? "f-2" : "f-1", Posicion = Pu(cx + dx, cy + dy), TieneMuralla = false };
                for (int i = 0; i < 24; i++) a.Zona.Add(Pu(cx + dx + zona * Math.Cos(i * Math.PI / 12), cy + dy + zona * Math.Sin(i * Math.PI / 12)));
                foto.Entidades.Add(new Entidad { Asentamiento = a });
            }
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (MaquetasDelMapa.Estilo, PinturaDelMapa.SombrasReales);
            PinturaDelMapa.SombrasReales = true;
            try
            {
                var (almacen, sesion) = Almacen(url, centro, null);
                almacen.Aplicar(new MensajeDeServidor { Foto = foto });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                var tiempos = new FrameTiming[1];
                bool hayGpu = FrameTimingManager.IsFeatureEnabled();
                using var principal = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "Main Thread", 1);
                foreach (var zoom in new[] { ZoomDelMapa.Normal, ZoomDelMapa.Cerca })
                    foreach (var estilo in new[] { EstiloDeAsentamientos.Prototipo, EstiloDeAsentamientos.Mejorado })
                    {
                        MaquetasDelMapa.Estilo = estilo;
                        yield return Llegar(mapa, almacen, centro);
                        yield return Vista(mapa, zoom, 30, $"coste_plazas_{estilo}_{zoom}");
                        // a pantalla (a la textura) en cada fotograma, como se mide en los demás temas
                        mapa.Vista.targetTexture = rt;
                        for (int i = 0; i < 60; i++) yield return null;
                        var cpu = new List<double>();
                        var gpu = new List<double>();
                        var dib = new List<double>();
                        for (int i = 0; i < 300; i++)
                        {
                            if (hayGpu) FrameTimingManager.CaptureFrameTimings();
                            yield return null;
                            cpu.Add(principal.LastValue / 1e6);
                            if (hayGpu && FrameTimingManager.GetLatestTimings(1, tiempos) > 0 && tiempos[0].gpuFrameTime > 0) gpu.Add(tiempos[0].gpuFrameTime);
                            dib.Add(mapa.Maquetas.MsDibujar);
                        }
                        mapa.Vista.targetTexture = null;
                        cpu.Sort(); dib.Sort(); gpu.Sort();
                        string g = gpu.Count > 0 ? $"{gpu[gpu.Count / 2]:0.00} ms (p95 {gpu[gpu.Count * 95 / 100]:0.00})" : "sin medida";
                        var en = estilo == EstiloDeAsentamientos.Mejorado ? mapa.Maquetas.Encargos(mapa.Vista) : null;
                        string m = en == null ? $"{mapa.Maquetas.Llamadas} llamadas, {mapa.Maquetas.Triangulos} triángulos"
                            : $"{en.Count} llamadas, {en.Sum(e => (long)e.Malla.GetIndexCount(0) / 3)} triángulos, niveles {string.Join(",", en.Where(e => !e.EsSuelo && !e.EsMuralla).Select(e => e.Nivel))}";
                        Debug.Log($"[Asentamientos] coste {estilo} {zoom}: CPU (hilo principal) mediana {cpu[cpu.Count / 2]:0.00} ms (p95 {cpu[cpu.Count * 95 / 100]:0.00}); GPU {g}; " +
                                  $"mandar a dibujar {dib[dib.Count / 2]:0.000} ms; {m}");
                    }
            }
            finally { (MaquetasDelMapa.Estilo, PinturaDelMapa.SombrasReales) = antes; }
        }

        /// <summary>Las cuentas del banco de asentamientos con «Prototipo»: piezas, llamadas de dibujo y triángulos (sin capturas).</summary>
        [Category("Mapa"), Explicit("cuentas del banco de asentamientos")]
        [UnityTest, Timeout(1_200_000)]
        public IEnumerator LasCuentasDelBancoDeAsentamientos()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "asentamientos-cuentas-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var banco = BancoDeAsentamientos.Hacer(BancoDeCaminos.Hacer(relieve, artefacto, datos), relieve, artefacto);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            var antes = MaquetasDelMapa.Estilo;
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Prototipo;
            try
            {
                var (almacen, sesion) = Almacen(url, banco.Sitios.Values.First(), null);
                almacen.Aplicar(new MensajeDeServidor { Foto = banco.Vista() });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                yield return Llegar(mapa, almacen, banco.Sitios.Values.First());
                mapa.Maquetas.Olvidar();
                yield return null;
                yield return null;
                Debug.Log($"[Asentamientos] banco: {banco.Sitios.Count} cosas, {mapa.Maquetas.PiezasPuestas} piezas, {mapa.Maquetas.Llamadas} llamadas, {mapa.Maquetas.Triangulos} triángulos; rehacer {mapa.Maquetas.MsRehacer:0.00} ms, asentar {mapa.Maquetas.MsAsentar:0.00} ms");
            }
            finally { MaquetasDelMapa.Estilo = antes; }
        }

        /// <summary>El desnivel bajo la huella de una plaza en el mundo de pruebas: en cada punto de tierra (una rejilla de 6 u, sin agua a
        /// menos del radio) y en los campamentos de mercenarios, lo más alto menos lo más bajo del relieve en el centro y en dos anillos (medio
        /// radio y radio) de 12 puntos, para la plaza de nivel 3 con la zona inicial (radio de ciudad 1,2 u: huella 0,96 u) y a 1,4 veces. La
        /// mediana, el percentil 95 y el máximo, en m y en pendiente (desnivel / diámetro).</summary>
        [Category("Mapa"), Explicit("desnivel bajo las plazas")]
        [UnityTest, Timeout(1_200_000)]
        public IEnumerator ElDesnivelBajoLasPlazas()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "asentamientos-laderas-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            double rc = datos.Numero("reglas.territorio.radio_inicial") / 1000 * 0.1;
            foreach (var escala in new[] { 1.0, 1.4 })
            {
                double r = MaquetasDelPrototipo.RadioDePlaza(rc, 3) * escala;
                double Desnivel(double x, double y)
                {
                    double mx = relieve.AlturaEn(x, y), mn = mx;
                    foreach (var f in new[] { 0.5, 1.0 })
                        for (int i = 0; i < 12; i++)
                        {
                            double a = i * Math.PI / 6, h = relieve.AlturaEn(x + Math.Cos(a) * r * f, y + Math.Sin(a) * r * f);
                            mx = Math.Max(mx, h);
                            mn = Math.Min(mn, h);
                        }
                    return mx - mn;
                }
                var tierra = new List<double>();
                for (double y = 20; y < relieve.Filas * relieve.Paso - 20; y += 6)
                    for (double x = 20; x < relieve.Columnas * relieve.Paso - 20; x += 6)
                        if (!relieve.AguaEn(x, y) && !relieve.AguaEn(x + r, y) && !relieve.AguaEn(x - r, y) && !relieve.AguaEn(x, y + r) && !relieve.AguaEn(x, y - r))
                            tierra.Add(Desnivel(x, y));
                var camps = artefacto.Campamentos.Select(c => Desnivel(c.Posicion.X / 1000.0, c.Posicion.Y / 1000.0)).ToList();
                string Cifras(List<double> l)
                {
                    l.Sort();
                    double e = EscalaDelMapa.MetrosPorUnidad, d = 2 * r * e;
                    double P(double q) => l[Math.Min(l.Count - 1, (int)(l.Count * q))];
                    return $"{l.Count} sitios: mediana {P(0.5):0.0} m ({P(0.5) / d:0.00}), p95 {P(0.95):0.0} m ({P(0.95) / d:0.00}), máximo {l[l.Count - 1]:0.0} m ({l[l.Count - 1] / d:0.00})";
                }
                Debug.Log($"[Asentamientos] desnivel bajo una plaza de nivel 3 de huella {r:0.00} u ({r * EscalaDelMapa.MetrosPorUnidad:0.0} m, ×{escala}): tierra, {Cifras(tierra)}; campamentos, {Cifras(camps)}");
                yield return null;
            }
        }

        /// <summary>El coste de las maquetas en el peor caso: una plaza de nivel 5 grande (zona de 60 u: radio de ciudad 6 u) con 8 aldeas y 4
        /// avanzadas alrededor, con sombras, con las cajas de antes y con «Prototipo»: el fotograma, lo que cuesta rehacer las maquetas y
        /// asentarlas, y cuántas piezas son.</summary>
        [Category("Mapa"), Explicit("coste de los asentamientos")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator ElCosteDeLosAsentamientos()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "asentamientos-coste-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            var (cx, cy) = caminos.SitiosDeSuelo["suelo_pradera"];
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var foto = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
            var plaza = new Asentamiento { Id = "as-g", Nombre = "Grande", Nivel = 5, FaccionId = "f-1", Posicion = Pu(cx, cy), TieneMuralla = true };
            for (int i = 0; i < 24; i++) plaza.Zona.Add(Pu(cx + 60 * Math.Cos(i * Math.PI / 12), cy + 60 * Math.Sin(i * Math.PI / 12)));
            foto.Entidades.Add(new Entidad { Asentamiento = plaza });
            string[] cultivos = { "trigo", "vid", "oliva", "cebada", "sesamo", "ganado", "trigo", "vid" };
            for (int i = 0; i < 8; i++)
                foto.Entidades.Add(new Entidad { Pueblo = new Pueblo { Id = $"pu-{i}", AsentamientoMadreId = "as-g", TipoExtractor = "aldeaAgricola", Cultivo = cultivos[i],
                    Posicion = Pu(cx + 12 * Math.Cos(i * Math.PI / 4), cy + 12 * Math.Sin(i * Math.PI / 4)) } });
            for (int i = 0; i < 4; i++)
                foto.Entidades.Add(new Entidad { Avanzada = new Avanzada { Id = $"av-{i}", AsentamientoMadreId = "as-g", Etapa = (uint)(i % 3 + 1), Estado = EstadoAvanzada.AvanzadaActiva,
                    Posicion = Pu(cx + 18 * Math.Cos(i * Math.PI / 2 + 0.4), cy + 18 * Math.Sin(i * Math.PI / 2 + 0.4)) } });
            var antes = MaquetasDelMapa.Estilo;
            try
            {
                var (almacen, sesion) = Almacen(url, (cx, cy), null);
                almacen.Aplicar(new MensajeDeServidor { Foto = foto });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                foreach (var (estilo, zoom) in new[] { (EstiloDeAsentamientos.Cajas, ZoomDelMapa.Normal), (EstiloDeAsentamientos.Prototipo, ZoomDelMapa.Normal),
                                                       (EstiloDeAsentamientos.Cajas, ZoomDelMapa.Alejado), (EstiloDeAsentamientos.Prototipo, ZoomDelMapa.Alejado) })
                {
                    MaquetasDelMapa.Estilo = estilo;
                    yield return Llegar(mapa, almacen, (cx, cy));
                    yield return Vista(mapa, zoom, 30, $"coste_asent_{estilo}_{zoom}");
                    var fot = new List<double>();
                    var dib = new List<double>();
                    for (int i = 0; i < 240; i++)
                    {
                        yield return null;
                        fot.Add(Time.unscaledDeltaTime * 1000);
                        dib.Add(mapa.Maquetas.MsDibujar);
                    }
                    fot.Sort();
                    dib.Sort();
                    string rehacer = "";
                    if (estilo != EstiloDeAsentamientos.Cajas)
                    {
                        mapa.Maquetas.Olvidar();
                        yield return null;
                        rehacer = $"; {mapa.Maquetas.PiezasPuestas} piezas, {mapa.Maquetas.Llamadas} llamadas, {mapa.Maquetas.Triangulos} triángulos; rehacer {mapa.Maquetas.MsRehacer:0.00} ms, asentar {mapa.Maquetas.MsAsentar:0.00} ms";
                    }
                    Debug.Log($"[Asentamientos] {estilo} {zoom}: fotograma mediana {fot[fot.Count / 2]:0.00} ms, p95 {fot[fot.Count * 95 / 100]:0.00} ms; " +
                              $"mandar a dibujar mediana {dib[dib.Count / 2]:0.000} ms{rehacer}");
                }
            }
            finally { MaquetasDelMapa.Estilo = antes; }
        }

        /// <summary>Las tres opciones del territorio que se le ofrecen a Víctor, en los mismos sitios del banco, a zoom habitual y alejado: el
        /// prototipo, el filo («Mejorado», ronda 4) y «Suave» (tinte suave en todo el territorio y un filo más fino y menos vivo). Los caminos,
        /// «Mejorado».</summary>
        [Category("Mapa"), Explicit("capturas de las tres opciones del territorio")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator LasTresOpcionesDelTerritorio()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "territorio-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var banco = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var antes = (TrazosEnElMapa.Estilo, TerritorioDelMapa.Estilo, MapaDeMundo.AclararCopas);
            MapaDeMundo.AclararCopas = false;
            TrazosEnElMapa.Estilo = TrazosEnElMapa.EstiloDeTrazos.Mejorado;
            try
            {
                foreach (var (estilo, nombre) in new[] { (TerritorioDelMapa.EstiloDeTerritorio.Prototipo, "1_prototipo"), (TerritorioDelMapa.EstiloDeTerritorio.Mejorado, "2_filo"),
                                                         (TerritorioDelMapa.EstiloDeTerritorio.Suave, "3_suave") })
                {
                    TerritorioDelMapa.Estilo = estilo;
                    if (go != null) { UnityEngine.Object.Destroy(go); yield return null; }
                    var (almacen, sesion) = Almacen(url, banco.Sitios["frontera_vecino"], null);
                    almacen.Aplicar(new MensajeDeServidor { Foto = banco.Vista() });
                    var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                    mapa.RadioVisionFijo = 15;
                    foreach (var k in new[] { "frontera_vecino", "plaza_pueblo", "faccion_oscura" })
                    {
                        yield return Llegar(mapa, almacen, banco.Sitios[k]);
                        yield return new WaitForSecondsRealtime(1);
                        foreach (var (zoom, z) in new[] { (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                        { EsconderLaColumna(); yield return Vista(mapa, zoom, 30, $"t_{k}_{nombre}_{z}"); }
                    }
                }
            }
            finally { (TrazosEnElMapa.Estilo, TerritorioDelMapa.Estilo, MapaDeMundo.AclararCopas) = antes; }
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
