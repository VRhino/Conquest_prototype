// La previa del mapa (`PreviaDelMapa`, su escena), sin ventana: abre la escena, entra en Play y espera a que se monte; comprueba que están todas
// las clases (lo fijo, los nodos y lo que se mueve) y que no sale ningún error en el registro (el marco de pruebas falla con cualquier error);
// y que cada deslizador cambia el tamaño de lo suyo y no el de lo demás. Necesita el mundo en disco de una partida (`persistentDataPath/mundo`).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConquestTactics.Mundo;
using ConquestTactics.Mundo.Mapa;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ConquestTactics.Mundo.Mapa.EditorTests
{
    public sealed class PreviaDelMapaEditorTests
    {
        const string Escena = "Assets/Scenes/BronzeAge/PreviaDelMapa.unity";

        static Type Tipo() => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ConquestTactics.Mundo.UI.PreviaDelMapa")).First(t => t != null);

        [UnityTest, Category("Mapa")]
        public IEnumerator LaPreviaDelMapaSeMontaYSusDeslizadoresMandan()
        {
            if (!Directory.Exists(Path.Combine(Application.persistentDataPath, "mundo", "artefacto")))
                Assert.Ignore("no hay mundo en disco: entra antes una vez al mundo con el servidor");
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            // (en otro iterador: con la recarga del dominio al entrar en Play, las variables capturadas de este no siguen)
            yield return SeMonta();
            yield return new ExitPlayMode();
            Assert.That(MaquetasDelMapa.Estilo, Is.EqualTo(EstiloDeAsentamientos.Prototipo), "fuera de Play, el estilo vuelve al del código");
            Assert.That(CosasDelMapa.FactorDeFijos, Is.EqualTo(2), "y los factores, a los del código");
            Assert.That(ColumnasDelMapa.FactorDeFiguras, Is.EqualTo(1.5));
        }

        static IEnumerator SeMonta()
        {
            var tipo = Tipo();
            Component previa = null;
            var hasta = Time.realtimeSinceStartup + 120;
            while (Time.realtimeSinceStartup < hasta)
            {
                previa = previa != null ? previa : UnityEngine.Object.FindAnyObjectByType(tipo) as Component;
                if (previa != null && (bool)tipo.GetProperty("Lista").GetValue(previa)) break;
                yield return null;
            }
            Assert.That(previa, Is.Not.Null, "la previa, en su escena");
            Assert.That((bool)tipo.GetProperty("Lista").GetValue(previa), "se montó");
            var mapa = (MapaDeMundo)tipo.GetProperty("Mapa").GetValue(previa);
            Assert.That(MaquetasDelMapa.Estilo, Is.EqualTo(EstiloDeAsentamientos.Mejorado), "«Mejorado» en la previa, aunque la casilla esté desmarcada");
            for (int i = 0; i < 10; i++) yield return null;
            var clases = mapa.Cosas.Select(c => c.Clase).Distinct().ToList();
            foreach (var c in new[] { ClaseDeCosa.Plaza, ClaseDeCosa.Pueblo, ClaseDeCosa.Aldea, ClaseDeCosa.Avanzada, ClaseDeCosa.Mercenarios, ClaseDeCosa.Bandidos,
                         ClaseDeCosa.Obra, ClaseDeCosa.Alijo, ClaseDeCosa.Batalla, ClaseDeCosa.Ejercito, ClaseDeCosa.EjercitoAjeno, ClaseDeCosa.Caravana, ClaseDeCosa.Aeda })
                Assert.That(clases, Does.Contain(c), $"está {c}");
            var recursos = mapa.Cosas.Where(c => c.Clase == ClaseDeCosa.Nodo).Select(c => c.Recurso).Distinct().Count();
            Debug.Log($"[Previa del mapa] {mapa.Cosas.Count} cosas, {recursos} recursos de nodo; {tipo.GetMethod("Lectura").Invoke(previa, null)}");

            // con los factores de partida, nada sobre agua ni encima de otra cosa (menos la familia real, que se pisa adrede)
            var banco = (BancoDelMapa)tipo.GetProperty("Banco").GetValue(previa);
            var fijas = new[] { ClaseDeCosa.Plaza, ClaseDeCosa.Pueblo, ClaseDeCosa.Aldea, ClaseDeCosa.Avanzada, ClaseDeCosa.Mercenarios, ClaseDeCosa.Bandidos,
                                ClaseDeCosa.Obra, ClaseDeCosa.Alijo, ClaseDeCosa.Batalla };
            var familia = banco.Sitios.First(k => k.nombre.StartsWith("familia real"));
            var puestas = mapa.Cosas.Where(c => fijas.Contains(c.Clase)
                && (c.Clase == ClaseDeCosa.Plaza || Math.Sqrt((c.X - familia.x) * (c.X - familia.x) + (c.Y - familia.y) * (c.Y - familia.y)) > 4)).ToList();
            foreach (var c in mapa.Cosas.Where(c => fijas.Contains(c.Clase)))
                for (int k = 0; k <= 16; k++)
                {
                    double r = k == 16 ? 0 : c.Radio, x = c.X + r * Math.Cos(k * Math.PI / 8), y = c.Y + r * Math.Sin(k * Math.PI / 8);
                    Assert.That(banco.Relieve.AguaEn(x, y) || banco.Cauces.ACauce(x, y, 1) < 0, Is.False, $"{c.Clase} {c.Id} ({c.X:0.0}, {c.Y:0.0}): sin agua bajo su huella ({c.Radio:0.00} u)");
                }
            for (int i = 0; i < puestas.Count; i++)
                for (int j = i + 1; j < puestas.Count; j++)
                {
                    var (p, q) = (puestas[i], puestas[j]);
                    double d = Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));
                    Assert.That(d, Is.GreaterThanOrEqualTo(p.Radio + q.Radio), $"{p.Clase} {p.Id} y {q.Clase} {q.Id}: no se pisan");
                }

            // las dos rutas como la tuya: su cinta, y la marca del destino donde toca (a campo abierto, en su destino; contra la plaza de nivel 5,
            // fuera de su huella con el aro tangente al borde, no en el centro al que va la columna)
            foreach (var id in new[] { "previa-ruta-campo", "previa-ruta-plaza" })
            {
                Assert.That(mapa.PuntosDeRutaDe(id), Is.GreaterThan(1), $"{id}: con su cinta");
                var (dx, dy) = ((double x, double y))tipo.GetMethod("DestinoDe").Invoke(previa, new object[] { id });
                var marca = mapa.MarcaDeDestino(id);
                Assert.That(marca, Is.Not.Null, $"{id}: con su marca");
                double d = Math.Sqrt((marca.Value.x - dx) * (marca.Value.x - dx) + (marca.Value.y - dy) * (marca.Value.y - dy));
                if (id == "previa-ruta-campo") Assert.That(d, Is.LessThan(0.01), "a campo abierto, la marca en su destino");
                else
                {
                    var plaza = mapa.Cosas.Where(c => c.Clase == ClaseDeCosa.Plaza).OrderBy(c => Math.Abs(c.X - dx) + Math.Abs(c.Y - dy)).First();
                    double aro = RutasDelMapa.RadioDelAro * RutasDelMapa.TamanoDeMarca * RutasDelMapa.Factor;
                    Assert.That(d, Is.EqualTo(plaza.Radio + aro).Within(0.02), $"contra la plaza, la marca a su huella ({plaza.Radio:0.00} u) más el radio del aro ({aro:0.00} u)");
                }
            }
            StringAssert.Contains("cinta=0.12; marca=1.00", (string)tipo.GetMethod("Valores").Invoke(previa, null), "«Copiar valores», con la ruta");

            // cada deslizador: lo suyo crece × 1,5 y lo demás no cambia
            var fijar = tipo.GetMethod("Fijar");
            bool Fija(ClaseDeCosa c) => c == ClaseDeCosa.Plaza || c == ClaseDeCosa.Pueblo || c == ClaseDeCosa.Avanzada || c == ClaseDeCosa.Mercenarios || c == ClaseDeCosa.Bandidos
                                         || c == ClaseDeCosa.Alijo;
            Dictionary<ClaseDeCosa, double> Radios() => mapa.Cosas.Where(c => c.Clase != ClaseDeCosa.Nodo).GroupBy(c => c.Clase).ToDictionary(g => g.Key, g => g.Max(c => c.Radio));
            var suyo = new Dictionary<string, ClaseDeCosa[]>
            {
                ["plazas"] = new[] { ClaseDeCosa.Plaza },
                ["pueblos"] = new[] { ClaseDeCosa.Pueblo, ClaseDeCosa.Aldea },
                ["avanzadas"] = new[] { ClaseDeCosa.Avanzada },
                ["campamentos"] = new[] { ClaseDeCosa.Mercenarios, ClaseDeCosa.Bandidos },
                ["menudo"] = new[] { ClaseDeCosa.Obra, ClaseDeCosa.Alijo, ClaseDeCosa.Batalla },
                ["figuras"] = new[] { ClaseDeCosa.Ejercito, ClaseDeCosa.EjercitoAjeno, ClaseDeCosa.Caravana, ClaseDeCosa.Aeda },
                ["nodos"] = new ClaseDeCosa[0],
            };
            foreach (var (clave, propias) in suyo.Select(k => (k.Key, k.Value)))
            {
                var antes = Radios();
                double soldado = ModelosDeFiguras.MetrosAEscena, piedra = NodosDelMapa.Huella("piedra");
                double inicial = clave == "figuras" ? ColumnasDelMapa.FactorDeFiguras : clave == "nodos" ? NodosDelMapa.FactorDeNodos : clave == "plazas" ? CosasDelMapa.FactorPlazas : 1;
                fijar.Invoke(previa, new object[] { clave, inicial * 1.5 });
                yield return null;
                yield return null;
                var despues = Radios();
                foreach (var c in antes.Keys)
                {
                    // (las plazas, los pueblos y las avanzadas hechos a la medida de la choza, y los campamentos y los alijos (25-11), van a su escala
                    // fija: no los mueven los factores de lo fijo)
                    double esperado = propias.Contains(c) && !Fija(c) ? 1.5 : 1;
                    Assert.That(despues[c] / antes[c], Is.EqualTo(esperado).Within(0.02), $"«{clave}»: el radio de {c}");
                }
                Assert.That(ModelosDeFiguras.MetrosAEscena / soldado, Is.EqualTo(clave == "figuras" ? 1.5 : 1).Within(1e-4), $"«{clave}»: el soldado");
                Assert.That(NodosDelMapa.Huella("piedra") / piedra, Is.EqualTo(clave == "nodos" ? 1.5 : 1).Within(1e-4), $"«{clave}»: el nodo");
                fijar.Invoke(previa, new object[] { clave, inicial });
                yield return null;
                yield return null;
            }
            // y «Lo fijo», todo lo fijo a la vez
            {
                var antes = Radios();
                double f = CosasDelMapa.FactorDeFijos;
                fijar.Invoke(previa, new object[] { "fijos", f * 1.5 });
                yield return null;
                yield return null;
                var despues = Radios();
                foreach (var c in new[] { ClaseDeCosa.Plaza, ClaseDeCosa.Pueblo, ClaseDeCosa.Aldea, ClaseDeCosa.Avanzada, ClaseDeCosa.Mercenarios, ClaseDeCosa.Bandidos,
                             ClaseDeCosa.Obra, ClaseDeCosa.Alijo, ClaseDeCosa.Batalla })
                    // (lo que va a escala fija no lo mueve lo fijo)
                    Assert.That(despues[c] / antes[c], Is.EqualTo(Fija(c) ? 1 : 1.5).Within(0.02), $"«Lo fijo»: el radio de {c}");
                Assert.That(despues[ClaseDeCosa.Ejercito] / antes[ClaseDeCosa.Ejercito], Is.EqualTo(1).Within(1e-6), "«Lo fijo»: no lo que se mueve");
                tipo.GetMethod("Restablecer").Invoke(previa, null);
            }
        }

        /// <summary>Las capturas de la página «Escalas y ruta» (en `MUNDO_CAPTURAS`): la vista general con el panel, la fila de plazas a × 1 y a × 2,
        /// la familia real a × 2 y lo que se mueve a × 1 y a × 1,5 junto al campamento y el nodo.</summary>
        [UnityTest, Category("Mapa"), Explicit("capturas para la página")]
        public IEnumerator LaPreviaEnCapturas()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MUNDO_CAPTURAS"))) Assert.Ignore("sin MUNDO_CAPTURAS");
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            // (en otro iterador: con la recarga del dominio al entrar en Play, las variables capturadas de este no siguen)
            yield return Capturas();
        }

        static IEnumerator Capturas()
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            Directory.CreateDirectory(carpeta);
            var tipo = Tipo();
            Component previa = null;
            var hasta = Time.realtimeSinceStartup + 120;
            while (Time.realtimeSinceStartup < hasta)
            {
                previa = previa != null ? previa : UnityEngine.Object.FindAnyObjectByType(tipo) as Component;
                if (previa != null && (bool)tipo.GetProperty("Lista").GetValue(previa)) break;
                yield return null;
            }
            var mapa = (MapaDeMundo)tipo.GetProperty("Mapa").GetValue(previa);
            var banco = (BancoDelMapa)tipo.GetProperty("Banco").GetValue(previa);
            var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            mapa.Vista.targetTexture = rt;
            var doc = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UIElements.UIDocument>();
            void Mirar(double x, double y) => tipo.GetMethod("Mirar").Invoke(previa, new object[] { x, y });
            void Fijar(string k, double v) => tipo.GetMethod("Fijar").Invoke(previa, new object[] { k, v });
            IEnumerator Foto(string nombre, ZoomDelMapa zoom, double inclinacion, bool panel = false)
            {
                mapa.Camara.Zoom = zoom;
                mapa.Camara.Inclinacion = inclinacion;
                if (doc != null) doc.panelSettings.targetTexture = rt;
                tipo.GetMethod("VerPanel").Invoke(previa, new object[] { panel });
                var fin = Time.realtimeSinceStartup + 60;
                yield return null;
                while (!mapa.SueloCompleto && Time.realtimeSinceStartup < fin) yield return null;
                yield return new WaitForSecondsRealtime(3f);
                var antes = RenderTexture.active;
                RenderTexture.active = rt;
                var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                t.Apply();
                RenderTexture.active = antes;
                File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), t.EncodeToPNG());
                UnityEngine.Object.Destroy(t);
            }
            (double x, double y) Donde(string empieza) { var s = banco.Sitios.First(k => k.nombre.StartsWith(empieza)); return (s.x, s.y); }
            var a = ((double x, double y))tipo.GetProperty("CaminoA").GetValue(previa);
            var b = ((double x, double y))tipo.GetProperty("CaminoB").GetValue(previa);
            (double x, double y) Junto(double t, double lado)
            {
                double dx = b.x - a.x, dy = b.y - a.y, l = Math.Sqrt(dx * dx + dy * dy);
                return (a.x + dx * t - dy / l * lado, a.y + dy * t + dx / l * lado);
            }
            // 1. la vista general con el panel
            var o = Junto(0.35, 0);
            Mirar(o.x, o.y);
            yield return Foto("pv_general", ZoomDelMapa.Normal, 35, true);
            // 2. cada plaza, a × 1 y a × 2
            var plazas = banco.Sitios.Where(k => k.nombre.StartsWith("plaza de nivel")).OrderBy(k => k.nombre).ToList();
            foreach (int i in new[] { 0, 2, 4 })
            {
                Mirar(plazas[i].x, plazas[i].y);
                Fijar("fijos", 1);
                yield return Foto($"pv_plaza_{i + 1}_x1", ZoomDelMapa.Normal, 35);
                Fijar("fijos", 2);
                yield return Foto($"pv_plaza_{i + 1}_x2", ZoomDelMapa.Normal, 35);
            }
            // 3. la familia real a × 2, y a × 1 para comparar
            var f = Donde("familia real");
            Mirar(f.x, f.y);
            yield return Foto("pv_familia_x2", ZoomDelMapa.Normal, 35);
            Fijar("fijos", 1);
            yield return Foto("pv_familia_x1", ZoomDelMapa.Normal, 35);
            Fijar("fijos", 2);
            // 4. lo que se mueve, parado junto al campamento y el nodo, a × 1 y a × 1,5
            tipo.GetMethod("Aparcar").Invoke(previa, new object[] { 0.27, 0.018, 1.2 });
            var m = Junto(0.34, 1);
            Mirar(m.x, m.y);
            foreach (var (zoom, z) in new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
            {
                Fijar("figuras", 1);
                yield return Foto($"pv_moviles_{z}_x1", zoom, 35);
                Fijar("figuras", 1.5);
                yield return Foto($"pv_moviles_{z}_x15", zoom, 35);
            }
            mapa.Vista.targetTexture = null;
            yield return new ExitPlayMode();
        }

        /// <summary>Las capturas del repaso del 23-11 (en `MUNDO_CAPTURAS`): las plazas de nivel 2, 4 y 5 en llano y en ladera, a las tres
        /// distancias y por los cuatro lados; los estandartes de cerca; y las cinco plazas en fila.</summary>
        [UnityTest, Category("Mapa"), Explicit("capturas para la página"), Timeout(3600000)]
        public IEnumerator LasPlazasDelRepasoEnCapturas()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MUNDO_CAPTURAS"))) Assert.Ignore("sin MUNDO_CAPTURAS");
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return CapturasDelRepaso();
        }

        static IEnumerator CapturasDelRepaso()
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            Directory.CreateDirectory(carpeta);
            var tipo = Tipo();
            Component previa = null;
            var hasta = Time.realtimeSinceStartup + 120;
            while (Time.realtimeSinceStartup < hasta)
            {
                previa = previa != null ? previa : UnityEngine.Object.FindAnyObjectByType(tipo) as Component;
                if (previa != null && (bool)tipo.GetProperty("Lista").GetValue(previa)) break;
                yield return null;
            }
            var mapa = (MapaDeMundo)tipo.GetProperty("Mapa").GetValue(previa);
            var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            mapa.Vista.targetTexture = rt;
            var doc = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UIElements.UIDocument>();
            if (doc != null) doc.panelSettings.targetTexture = rt;
            tipo.GetMethod("VerPanel").Invoke(previa, new object[] { false });
            tipo.GetMethod("AndarOParar").Invoke(previa, new object[] { false });
            void Mirar(double x, double y) => tipo.GetMethod("Mirar").Invoke(previa, new object[] { x, y });
            void Mover(string n, double x, double y) => tipo.GetMethod("MoverPlaza").Invoke(previa, new object[] { n, x, y });
            (double x, double y)? Donde(string n) => ((double x, double y)?)tipo.GetMethod("DondePlaza").Invoke(previa, new object[] { n });
            IEnumerator Foto(string nombre, ZoomDelMapa zoom, double giro)
            {
                mapa.Camara.Zoom = zoom;
                mapa.Camara.Inclinacion = 35;
                mapa.Camara.Giro = giro;
                var fin = Time.realtimeSinceStartup + 60;
                yield return null;
                while (!mapa.SueloCompleto && Time.realtimeSinceStartup < fin) yield return null;
                yield return new WaitForSecondsRealtime(1.2f);
                var antes = RenderTexture.active;
                RenderTexture.active = rt;
                var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                t.Apply();
                RenderTexture.active = antes;
                File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), t.EncodeToPNG());
                UnityEngine.Object.Destroy(t);
                Debug.Log($"[Previa del mapa] captura {nombre}: {mapa.Maquetas.EstandartesPuestos} estandartes de plaza");
            }
            // (con la cámara puesta a mano: a 3, 6 y 12 radios de la plaza, a 35° sobre el horizonte, desde los cuatro lados)
            var e = EscalaDelMapa.MetrosPorUnidad;
            IEnumerator FotoFija(string nombre, double x, double y, double radio, double veces, double rumbo, double alzado = 35)
            {
                double d = radio * veces, h = mapa.AlturaVisible(x, y);
                double a = alzado * Math.PI / 180;
                MapaDeMundo.CamaraFija = (EscalaDelMapa.AEscena(x + Math.Cos(rumbo) * d * Math.Cos(a), y + Math.Sin(rumbo) * d * Math.Cos(a), h + d * Math.Sin(a) * e),
                                          EscalaDelMapa.AEscena(x, y, h + radio * 0.15 * e));
                yield return Foto(nombre, ZoomDelMapa.Normal, 0);
            }
            double RadioDe(string nombre) => mapa.Cosas.Where(c => c.Clase == ClaseDeCosa.Plaza && c.Nombre != null && c.Nombre.StartsWith(nombre)).Select(c => c.Radio).DefaultIfEmpty(3).First();
            var distancias = new[] { (3.0, "1cerca"), (6.0, "2habitual"), (12.0, "3alejado") };
            var lados = new[] { (-Math.PI / 2, "a"), (0.0, "b"), (Math.PI / 2, "c"), (Math.PI, "d") };
            // (con MUNDO_SOLO_FILA, solo la fila)
            foreach (var n in string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MUNDO_SOLO_FILA")) ? new[] { 2, 4, 5 } : new int[0])
            {
                string nombre = $"plaza de nivel {n}";
                var llano = Donde(nombre).Value;
                var ladera = ((double x, double y)?)tipo.GetMethod("Ladera").Invoke(previa, new object[] { 6.0 });
                foreach (var (sitio, cual) in new[] { (llano, "llano"), (ladera ?? llano, "ladera") })
                {
                    Mover(nombre, sitio.x, sitio.y);
                    Mirar(sitio.x, sitio.y);
                    for (int i = 0; i < 4; i++) yield return null;
                    double r = RadioDe(nombre);
                    foreach (var (v, vn) in distancias)
                        foreach (var (g, gn) in lados)
                            yield return FotoFija($"rp_{n}_{cual}_{vn}_{gn}", sitio.x, sitio.y, r, v, g);
                    if (n == 4 && cual == "llano")
                    {
                        // el borde este, de cerca: desde dentro de la ciudad y algo en picado, entre las casas del borde y la muralla
                        double hc = mapa.AlturaVisible(sitio.x, sitio.y) / e;
                        MapaDeMundo.CamaraFija = (EscalaDelMapa.AEscena(sitio.x + r * 0.45, sitio.y - r * 0.15, (hc + r * 0.55) * e),
                                                  EscalaDelMapa.AEscena(sitio.x + r * 0.95, sitio.y + r * 0.05, hc * e));
                        yield return Foto("rp_4_llano_borde_este", ZoomDelMapa.Normal, 0);
                    }
                    if (cual == "ladera")
                    {
                        // de cerca por el lado de abajo de la cuesta
                        double gx = mapa.AlturaVisible(sitio.x + r, sitio.y) - mapa.AlturaVisible(sitio.x - r, sitio.y);
                        double gy = mapa.AlturaVisible(sitio.x, sitio.y + r) - mapa.AlturaVisible(sitio.x, sitio.y - r);
                        yield return FotoFija($"rp_{n}_ladera_abajo", sitio.x, sitio.y, r, 3, Math.Atan2(-gy, -gx));
                    }
                    // los estandartes, de cerca
                    var pies = mapa.Maquetas.PiesDeEstandarte.ToList();
                    int k = 0;
                    foreach (var pie in pies)
                    {
                        var (px, ph, py) = EscalaDelMapa.AlMapa(pie);
                        if (Math.Abs(px - sitio.x) > r * 1.5 || Math.Abs(py - sitio.y) > r * 1.5 || k >= 3) continue;
                        double d = 1.6, a = 30 * Math.PI / 180;
                        MapaDeMundo.CamaraFija = (EscalaDelMapa.AEscena(px - d * Math.Cos(a) * 0.7, py - d * Math.Cos(a) * 0.7, (ph + d * Math.Sin(a)) * e),
                                                  EscalaDelMapa.AEscena(px, py, (ph + 0.35) * e));
                        yield return Foto($"rp_{n}_{cual}_estandarte_{k++}", ZoomDelMapa.Normal, 0);
                    }
                }
                Mover(nombre, llano.x, llano.y);
            }
            // las cinco plazas en fila (en un llano)
            var fila = ((double x, double y)?)tipo.GetMethod("Llano").Invoke(previa, new object[] { 52.0 });
            if (fila is (double fx, double fy))
            {
                for (int n = 1; n <= 5; n++) Mover($"plaza de nivel {n}", fx + (n - 1) * 13, fy);
                Mirar(fx + 26, fy);
                for (int i = 0; i < 4; i++) yield return null;
                yield return FotoFija("rp_fila_alejado", fx + 26, fy, 6, 10, -Math.PI / 2, 32);
                yield return FotoFija("rp_fila_habitual", fx + 26, fy, 6, 7, -Math.PI / 2, 22);
            }
            MapaDeMundo.CamaraFija = null;
            mapa.Vista.targetTexture = null;
            yield return new ExitPlayMode();
        }

        /// <summary>Las capturas de la ruta y el destino (ronda 2 de «Escalas y ruta», en `MUNDO_CAPTURAS`): a campo abierto y contra la plaza de
        /// nivel 5, de cerca (sobre la marca), a zoom habitual y alejado (sobre la mitad de la ruta); y la previa con el panel.</summary>
        [UnityTest, Category("Mapa"), Explicit("capturas para la página")]
        public IEnumerator LaRutaEnCapturas()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MUNDO_CAPTURAS"))) Assert.Ignore("sin MUNDO_CAPTURAS");
            EditorSceneManager.OpenScene(Escena, OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return CapturasDeRuta();
        }

        static IEnumerator CapturasDeRuta()
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            Directory.CreateDirectory(carpeta);
            var tipo = Tipo();
            Component previa = null;
            var hasta = Time.realtimeSinceStartup + 120;
            while (Time.realtimeSinceStartup < hasta)
            {
                previa = previa != null ? previa : UnityEngine.Object.FindAnyObjectByType(tipo) as Component;
                if (previa != null && (bool)tipo.GetProperty("Lista").GetValue(previa)) break;
                yield return null;
            }
            var mapa = (MapaDeMundo)tipo.GetProperty("Mapa").GetValue(previa);
            var rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            mapa.Vista.targetTexture = rt;
            var doc = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UIElements.UIDocument>();
            void Mirar(double x, double y) => tipo.GetMethod("Mirar").Invoke(previa, new object[] { x, y });
            IEnumerator Foto(string nombre, ZoomDelMapa zoom, bool panel = false)
            {
                mapa.Camara.Zoom = zoom;
                mapa.Camara.Inclinacion = 35;
                if (doc != null) doc.panelSettings.targetTexture = rt;
                tipo.GetMethod("VerPanel").Invoke(previa, new object[] { panel });
                var fin = Time.realtimeSinceStartup + 60;
                yield return null;
                while (!mapa.SueloCompleto && Time.realtimeSinceStartup < fin) yield return null;
                yield return new WaitForSecondsRealtime(3f);
                var antes = RenderTexture.active;
                RenderTexture.active = rt;
                var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                t.Apply();
                RenderTexture.active = antes;
                File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), t.EncodeToPNG());
                UnityEngine.Object.Destroy(t);
                Debug.Log($"[Previa del mapa] captura {nombre}: {tipo.GetMethod("Valores").Invoke(previa, null)}");
            }
            foreach (var (id, nombre) in new[] { ("previa-ruta-campo", "campo"), ("previa-ruta-plaza", "plaza") })
            {
                // la columna a un tercio de su camino: la ruta, larga
                tipo.GetMethod("PonerRuta").Invoke(previa, new object[] { id, 0.35 });
                for (int i = 0; i < 3; i++) yield return null;
                var marca = mapa.MarcaDeDestino(id) ?? ((double x, double y))tipo.GetMethod("DestinoDe").Invoke(previa, new object[] { id });
                var columna = mapa.Cosas.First(c => c.Id == id);
                Mirar(marca.x, marca.y);
                yield return Foto($"rd_{nombre}_1_cerca", ZoomDelMapa.Cerca);
                Mirar((marca.x + columna.X) / 2, (marca.y + columna.Y) / 2);
                yield return Foto($"rd_{nombre}_2_habitual", ZoomDelMapa.Normal);
                yield return Foto($"rd_{nombre}_3_alejado", ZoomDelMapa.Alejado);
                if (nombre == "campo") yield return Foto("rd_previa", ZoomDelMapa.Normal, true);
            }
            mapa.Vista.targetTexture = null;
            yield return new ExitPlayMode();
        }
    }
}
