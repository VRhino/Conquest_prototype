// El mapa de mundo con una cosa de cada tipo, para verlas: el relieve, el artefacto y las reglas de un servidor de prueba de verdad
// (`BRONZEAGE_PRUEBA_URL`), y un almacén con plazas, pueblos, avanzada, obra, campamentos, alijo, batalla, columnas, caravana, Aeda, un camino,
// la zona de una Facción y una Mirada junto al campamento 4 del mundo de prueba (que tiene río y agua cerca). Comprueba que las cintas miran a
// la cámara y que el río se ve con color de agua, y con `MUNDO_CAPTURAS` guarda lo que se ve con los nombres encima.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo;
using ConquestTactics.Mundo.Mapa;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.UI.Tests
{
    public class MapaConTodoTests
    {
        GameObject go, interfaz;
        RenderTexture rtMapa, rtNombres;
        string cache;

        [TearDown]
        public void Limpiar()
        {
            if (go != null) UnityEngine.Object.Destroy(go);
            if (interfaz != null) UnityEngine.Object.Destroy(interfaz);
            rtMapa?.Release();
            rtNombres?.Release();
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
        [UnityTest]
        public IEnumerator SeVeUnaCosaDeCadaTipo()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");

            // El mundo de verdad: una cuenta nueva solo para saber qué mundo y qué reglas tiene el servidor.
            var acceso = new Acceso(url);
            var nick = "todo-" + DateTime.UtcNow.Ticks % 100_000_000;
            Esperar(acceso.Registrar(nick, "clave-de-prueba-1"));
            var login = Esperar(acceso.Entrar(nick, "clave-de-prueba-1"));
            var s = new Sesion(acceso.UrlSesion, login.Token, "conquest-prueba");
            var abrir = s.Abrir();
            var hasta = Time.realtimeSinceStartup + 20;
            while (!abrir.IsCompleted && Time.realtimeSinceStartup < hasta) { s.Bombear(); yield return null; }
            var mundo = abrir.Result.Mundo;
            s.Cerrar();
            acceso.Anular(login.Token);
            cache = Path.Combine(Path.GetTempPath(), "conquest-prueba-todo-" + Guid.NewGuid().ToString("N"));
            var datos = Esperar(DatosDeJuego.Cargar(acceso, mundo, new CacheDeUno(Path.Combine(cache, "datos"))));
            var artefacto = Esperar(MundoEnDisco.CargarArtefacto(acceso, mundo, new CacheDeUno(Path.Combine(cache, "mundo"))));
            var relieve = new Relieve(Esperar(MundoEnDisco.CargarRelieve(acceso, mundo, new CacheDeUno(Path.Combine(cache, "relieve")))));

            // Junto al campamento 4: una cosa de cada tipo alrededor de tu columna.
            var camp = artefacto.Campamentos.First(c => c.Id == "campamento-4");
            double cx = camp.Posicion.X / 1000.0 + 4, cy = camp.Posicion.Y / 1000.0 - 6;
            var almacen = new Almacen();
            var heroe = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoHeroe, Id = "heroe-1" }, Version = 1 };
            heroe.Entidades.Add(new Entidad { Heroe = new Heroe { Id = "heroe-1", DisplayName = "Prueba", FaccionId = "f-1", Ubicacion = new UbicacionHeroe { EjercitoId = "ej-1" } } });
            var propia = new Ejercito { Id = "ej-1", FaccionId = "f-1", Tipo = TipoColumna.ColumnaEjercito, Posicion = Pu(cx, cy), LiderId = "heroe-1" };
            propia.Participantes.Add(new Participante { HeroeId = "heroe-1" });
            heroe.Entidades.Add(new Entidad { Ejercito = propia });
            almacen.Aplicar(new MensajeDeServidor { Foto = heroe });

            var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 1 };
            var micenas = new Asentamiento { Id = "as-1", Nombre = "Micenas", FaccionId = "f-1", Nivel = 2, Posicion = Pu(cx + 10, cy - 4) };
            for (int i = 0; i < 6; i++) micenas.Zona.Add(Pu(cx + 10 + 7 * Math.Cos(i * Math.PI / 3), cy - 4 + 7 * Math.Sin(i * Math.PI / 3)));
            vista.Entidades.Add(new Entidad { Asentamiento = micenas });
            vista.Entidades.Add(new Entidad { Asentamiento = new Asentamiento { Id = "as-2", Nombre = "Pilos", FaccionId = "f-2", Nivel = 1, Posicion = Pu(cx - 11, cy - 7) } });
            vista.Entidades.Add(new Entidad { FichaConocida = new FichaConocida { AsentamientoId = "as-3", Nombre = "Tirinto", FaccionId = "f-3", Nivel = 1, Tipo = TipoEntidad.Asentamiento, Posicion = Pu(cx - 2, cy - 15) } });
            vista.Entidades.Add(new Entidad { Pueblo = new Pueblo { Id = "pu-1", AsentamientoMadreId = "as-1", TipoExtractor = "aldeaAgricola", Posicion = Pu(cx + 16, cy + 2) } });
            vista.Entidades.Add(new Entidad { Pueblo = new Pueblo { Id = "pu-2", AsentamientoMadreId = "as-1", TipoExtractor = "cantera", Posicion = Pu(cx + 6, cy - 12) } });
            vista.Entidades.Add(new Entidad { Avanzada = new Avanzada { Id = "av-1", AsentamientoMadreId = "as-1", Estado = EstadoAvanzada.AvanzadaActiva, Posicion = Pu(cx + 14, cy - 10) } });
            vista.Entidades.Add(new Entidad { Obra = new ObraDeMapa { Id = "ob-1", AsentamientoMadreId = "as-1", Posicion = Pu(cx + 4, cy + 7) } });
            vista.Entidades.Add(new Entidad { CampamentoMercenarios = new CampamentoMercenarios { Id = camp.Id, Posicion = camp.Posicion } });
            vista.Entidades.Add(new Entidad { CampamentoBandidos = new CampamentoBandidos { Id = "bandidos-1", Nivel = 2, Posicion = Pu(cx - 9, cy + 6) } });
            vista.Entidades.Add(new Entidad { Alijo = new Alijo { Id = "al-1", Recurso = "madera", Posicion = Pu(cx + 2, cy - 3) } });
            vista.Entidades.Add(new Entidad { Batalla = new Batalla { Id = "bat-1", Estado = EstadoBatalla.BatallaEnCurso, Punto = Pu(cx - 5, cy - 2) } });
            var ajena = new Ejercito { Id = "ej-2", FaccionId = "f-2", Tipo = TipoColumna.ColumnaPersonal, Posicion = Pu(cx - 3, cy + 3) };
            ajena.Participantes.Add(new Participante { HeroeId = "heroe-9" });
            vista.Entidades.Add(new Entidad { Ejercito = ajena });
            vista.Entidades.Add(new Entidad { Caravana = new Caravana { Id = "car-1", FaccionId = "f-2", Escoltada = true, Posicion = Pu(cx + 7, cy + 2) } });
            vista.Entidades.Add(new Entidad { AedaItinerante = new AedaItinerante { Id = "aeda-1", Posicion = Pu(cx - 6, cy - 9) } });
            var red = new RedCaminos();
            var tramo = new TramoDeRed { Id = "tramo-1", Escalon = 1 };
            tramo.Puntos.Add(Pu(cx + 10, cy - 4));
            tramo.Puntos.Add(Pu(cx + 3, cy - 1));
            tramo.Puntos.Add(Pu(cx - 11, cy - 7));
            red.Tramos.Add(tramo);
            vista.Entidades.Add(new Entidad { RedCaminos = red });
            var zona = new ZonaFaccion { FaccionId = "f-1" };
            var contorno = new ContornoZona();
            for (int i = 0; i < 8; i++) contorno.Puntos.Add(Pu(cx + 12 + 11 * Math.Cos(i * Math.PI / 4), cy - 4 + 11 * Math.Sin(i * Math.PI / 4)));
            zona.Contornos.Add(contorno);
            vista.Entidades.Add(new Entidad { ZonaFaccion = zona });
            vista.Entidades.Add(new Entidad { Mirada = new MiradaIntel { Id = "mir-1", FaccionId = "f-1", Centro = Pu(cx - 8, cy - 14), Radio = 4000, ExpiraEnMs = long.MaxValue / 2 } });
            almacen.Aplicar(new MensajeDeServidor { Foto = vista });
            var sesion = new Sesion(url, "", "conquest-prueba", almacen);

            // Los nombres van en un panel de UI Toolkit, que pinta a su textura.
            interfaz = new GameObject("Interfaz");
            interfaz.SetActive(false);
            var doc = interfaz.AddComponent<UIDocument>();
            var ajustes = ScriptableObject.CreateInstance<PanelSettings>();
            ajustes.themeStyleSheet = Resources.Load<ThemeStyleSheet>("MundoUI/TemaMundo");
            ajustes.scaleMode = PanelScaleMode.ConstantPixelSize;
            rtNombres = new RenderTexture(1600, 900, 0);
            ajustes.targetTexture = rtNombres;
            ajustes.clearColor = true;
            ajustes.colorClearValue = new Color(0, 0, 0, 0);
            doc.panelSettings = ajustes;
            interfaz.SetActive(true);
            doc.rootVisualElement.styleSheets.Add(Resources.Load<StyleSheet>("MundoUI/Mundo"));
            var nombres = new VisualElement { pickingMode = PickingMode.Ignore };
            nombres.style.position = Position.Absolute;
            nombres.style.left = nombres.style.top = nombres.style.right = nombres.style.bottom = 0;
            doc.rootVisualElement.Add(nombres);

            go = new GameObject("Mapa");
            var mapa = go.AddComponent<MapaDeMundo>();
            mapa.Iniciar(sesion, relieve, artefacto, datos, () => "heroe-1", _ => false, (t, p) => { }, nombres);
            rtMapa = new RenderTexture(1600, 900, 24);
            mapa.Vista.targetTexture = rtMapa;
            mapa.Camara.PasoDeZoom(1);
            mapa.Camara.Inclinar(30);
            var fin = Time.realtimeSinceStartup + 60;
            while ((mapa.Centro == null || !mapa.SueloCompleto || mapa.TeselasCargadas < 4) && Time.realtimeSinceStartup < fin) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);

            // Lo que se pinta: una cosa de cada tipo, con su clase.
            var clases = mapa.Cosas.Select(c => c.Clase).Distinct().ToList();
            foreach (var k in new[] { ClaseDeCosa.Plaza, ClaseDeCosa.Pueblo, ClaseDeCosa.Aldea, ClaseDeCosa.Avanzada, ClaseDeCosa.Obra, ClaseDeCosa.Mercenarios,
                ClaseDeCosa.Bandidos, ClaseDeCosa.Alijo, ClaseDeCosa.Batalla, ClaseDeCosa.EjercitoAjeno, ClaseDeCosa.CaravanaAjena, ClaseDeCosa.Aeda, ClaseDeCosa.Nodo })
                CollectionAssert.Contains(clases, k);

            // Todas las cintas (ríos y vados, caminos, fronteras, zonas y anillos, que son «trazo», Miradas, lo seleccionado) miran hacia arriba, a la cámara: con el
            // orden de vértices del revés se pintaban de espaldas y no se veían.
            mapa.Seleccionar(mapa.Cosas.First(c => c.Clase == ClaseDeCosa.Plaza && c.Id == "as-1"));
            yield return null;
            var cintas = go.GetComponentsInChildren<MeshFilter>().Where(m => m.name == "Rios" || m.name.StartsWith("Rayas") || m.name == "Lo seleccionado" || m.name == "trazo").ToList();
            Assert.GreaterOrEqual(cintas.Count, 4, string.Join(", ", cintas.Select(m => m.name)));
            foreach (var m in cintas)
            {
                // Hacia arriba (las cuestas de un río que baja deprisa inclinan la normal, pero no la ponen bocabajo); las nulas son de tramos sin largo.
                var normales = m.sharedMesh.normals.Where(n => n.sqrMagnitude > 1e-6f).ToList();
                if (normales.Count == 0)
                {
                    // las cintas sin normales (las de `TrazosEnElMapa`): la de cada triángulo, por su orden de vértices
                    var v = m.sharedMesh.vertices;
                    var t = m.sharedMesh.triangles;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        var n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                        if (n.sqrMagnitude > 1e-8f) normales.Add(n.normalized);
                    }
                }
                var abajo = normales.Count(n => n.y <= 0);
                Assert.AreEqual(0, abajo, $"{m.name}: {abajo} de {normales.Count} normales miran hacia abajo (la más baja, {normales.Min(n => n.y):0.00})");
            }

            // El río cercano se ve con color de agua (azul por encima del rojo) en la imagen de la cámara.
            var rio = artefacto.Rios.SelectMany(r => r.Puntos).Select(p => (x: p.X / 1000.0, y: p.Y / 1000.0))
                .Where(p => !relieve.AguaEn(p.x, p.y)).OrderBy(p => CosasDelMapa.Distancia(p.x, p.y, cx, cy)).First();
            yield return null;
            var imagen = Leer(rtMapa);
            var enPantalla = mapa.Vista.WorldToScreenPoint(EscalaDelMapa.AEscena(rio.x, rio.y, relieve.AlturaEn(rio.x, rio.y)));
            Assert.Greater(enPantalla.z, 0, "el río queda delante de la cámara");
            var pixel = imagen.GetPixel((int)(enPantalla.x * 1600 / mapa.Vista.pixelWidth), (int)(enPantalla.y * 900 / mapa.Vista.pixelHeight));
            Assert.Greater(pixel.b, pixel.r + 0.03f, $"el píxel del río en ({rio.x:0.0}, {rio.y:0.0}) es de agua: {pixel}");

            // El agua de junto al campamento 4 es un lago. El mar (junto al campamento 7) y un vado (junto al campamento 2): la columna va allí y se
            // mira el píxel. Sin VISTA no hay niebla: todo se ve.
            var umbral = datos.Numero("documentos.Generacion_Del_Mundo.ELEVACION.umbralAgua") * Relieve.MetrosAltura;
            var c7 = artefacto.Campamentos.First(c => c.Id == "campamento-7");
            (double x, double y)? mar = null;
            for (double r = 1; r < 80 && mar == null; r += 1)
                for (int k = 0; k < 64 && mar == null; k++)
                {
                    double x = c7.Posicion.X / 1000.0 + r * Math.Cos(k * Math.PI / 32), y = c7.Posicion.Y / 1000.0 + r * Math.Sin(k * Math.PI / 32);
                    if (relieve.AguaEn(x, y) && relieve.AlturaEn(x, y) < umbral && relieve.AguaEn(x + 3, y) && relieve.AguaEn(x - 3, y)) mar = (x, y);
                }
            Assert.IsNotNull(mar, "hay mar cerca del campamento 7");
            // Se mira el agua más honda de la misma vista (a menos de 6 u del punto de mar), no la somera de la orilla: con el sol a 40° lo somero
            // pierde algo de azul (aceptado, 2026-11-20) y la orilla queda justa; la condición de color es la de siempre, con la media de 9 × 9.
            var hondo = mar.Value;
            for (double dy = -6; dy <= 6; dy += 0.5)
                for (double dx = -6; dx <= 6; dx += 0.5)
                {
                    double x = mar.Value.x + dx, y = mar.Value.y + dy;
                    if (relieve.AguaEn(x, y) && relieve.AlturaEn(x, y) < relieve.AlturaEn(hondo.x, hondo.y)) hondo = (x, y);
                }
            var mapaMar = yield_Pixel(mapa, almacen, mar.Value.x + 4, mar.Value.y, hondo.x, hondo.y);
            while (mapaMar.MoveNext()) yield return mapaMar.Current;
            Debug.Log($"[Mar] sol a {Math.Atan2(PinturaDelMapa.Sol.y, new Vector2(PinturaDelMapa.Sol.x, PinturaDelMapa.Sol.z).magnitude) * 180 / Math.PI:0}°, agua honda ({hondo.x:0.0}, {hondo.y:0.0}) a {umbral - relieve.AlturaEn(hondo.x, hondo.y):0} m bajo el nivel: píxel {ultimoPixel}, media de 9 × 9 {ultimaMedia}");
            Assert.Greater(ultimaMedia.b, ultimaMedia.r + 0.03f, $"el mar se ve con color de agua: {ultimaMedia}");
            Guardar("22_mar");
            var c2 = artefacto.Campamentos.First(c => c.Id == "campamento-2");
            var vado = artefacto.Rios.SelectMany(r => r.Vados).Select(p => (x: p.X / 1000.0, y: p.Y / 1000.0)).Where(p => !relieve.AguaEn(p.x, p.y))
                .OrderBy(p => CosasDelMapa.Distancia(p.x, p.y, c2.Posicion.X / 1000.0, c2.Posicion.Y / 1000.0)).First();
            var mapaVado = yield_Pixel(mapa, almacen, vado.x + 2, vado.y + 2, vado.x, vado.y);
            while (mapaVado.MoveNext()) yield return mapaVado.Current;
            Assert.Greater(ultimoPixel.r, ultimoPixel.b + 0.03f, $"el vado se ve con color de grava: {ultimoPixel}");
            Guardar("23_vado");

            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            if (!string.IsNullOrEmpty(carpeta))
            {
                Directory.CreateDirectory(carpeta);
                File.WriteAllBytes(Path.Combine(carpeta, "20_mapa_con_todo.png"), Componer(imagen, Leer(rtNombres)).EncodeToPNG());
                mapa.Camara.PasoDeZoom(-1);
                mapa.Camara.Inclinar(-20);
                yield return new WaitForSecondsRealtime(1.5f);
                File.WriteAllBytes(Path.Combine(carpeta, "21_mapa_con_todo_de_cerca.png"), Componer(Leer(rtMapa), Leer(rtNombres)).EncodeToPNG());
            }
        }

        Color ultimoPixel;
        Texture2D ultimaImagen;

        /// <summary>Lleva tu columna a (cx, cy), espera al suelo y lee el píxel de (px, py) en lo que ve la cámara.</summary>
        IEnumerator yield_Pixel(MapaDeMundo mapa, Almacen almacen, double cx, double cy, double px, double py)
        {
            almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito.Posicion = Pu(cx, cy);
            yield return null;
            var fin = Time.realtimeSinceStartup + 60;
            while (!mapa.SueloCompleto && Time.realtimeSinceStartup < fin) yield return null;
            yield return new WaitForSecondsRealtime(1.5f);
            ultimaImagen = Leer(rtMapa);
            var p = mapa.Vista.WorldToScreenPoint(EscalaDelMapa.AEscena(px, py, mapa.AlturaVisible(px, py)));
            Assert.Greater(p.z, 0);
            ultimoPixel = ultimaImagen.GetPixel((int)p.x, (int)p.y);
            var suma = Color.clear;
            for (int dj = -4; dj <= 4; dj++)
                for (int di = -4; di <= 4; di++) suma += ultimaImagen.GetPixel((int)p.x + di, (int)p.y + dj);
            ultimaMedia = suma / 81;
        }

        Color ultimaMedia;

        void Guardar(string nombre)
        {
            var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
            if (string.IsNullOrEmpty(carpeta)) return;
            Directory.CreateDirectory(carpeta);
            File.WriteAllBytes(Path.Combine(carpeta, nombre + ".png"), Componer(ultimaImagen, Leer(rtNombres)).EncodeToPNG());
        }

        static Texture2D Leer(RenderTexture rt)
        {
            var antes = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = antes;
            return t;
        }

        /// <summary>Los nombres (con su transparencia) encima de lo que ve la cámara.</summary>
        static Texture2D Componer(Texture2D mapa, Texture2D nombres)
        {
            var a = mapa.GetPixels();
            var b = nombres.GetPixels();
            for (int i = 0; i < a.Length; i++) a[i] = Color.Lerp(a[i], new Color(b[i].r, b[i].g, b[i].b, 1), b[i].a);
            var t = new Texture2D(mapa.width, mapa.height, TextureFormat.RGB24, false);
            t.SetPixels(a);
            t.Apply();
            return t;
        }
    }
}
