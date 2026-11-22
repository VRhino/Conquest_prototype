// La prueba técnica B del tema 7: la columna con su modelo de verdad en el mapa («Mejorado»). Una columna personal con el héroe a caballo vestido
// y cuatro escuadras con su modelo de batalla (milicia_lanceros, lenadores, mineros, granjeros), andando por la pradera: capturas de cerca, a
// zoom habitual y de lejos, la vuelta en cuatro vistas y cuatro instantes andando («tm_…», `MUNDO_CAPTURAS`); y el coste con 30 columnas (la
// mitad personales de 6 escuadras, la mitad ejércitos de 12): el fotograma con las cajas de hoy, con las figuras de bloques y con los modelos.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo;
using ConquestTactics.Mundo.Mapa;
using ConquestTactics.Mundo.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.UI.Tests
{
    public partial class SueloDelMapaTests
    {
        static readonly string[] TropasConModelo = { "milicia_lanceros", "lenadores", "mineros", "granjeros" };

        /// <summary>Una columna (personal o ejército) con sus héroes y escuadrones de la Facción, andando de `a` a `b` a `velocidad` u/s.</summary>
        static void Columna(Foto vista, string id, IList<string> heroes, IList<string> tropas, bool ejercito, (double x, double y) a, (double x, double y) b,
            double velocidad, double ahora, string faccion = "f-1")
        {
            var c = new Ejercito { Id = id, FaccionId = faccion, Tipo = ejercito ? TipoColumna.ColumnaEjercito : TipoColumna.ColumnaPersonal, LiderId = heroes[0],
                Estado = EstadoColumna.ColumnaMarchando };
            foreach (var h in heroes)
            {
                c.Participantes.Add(new Participante { HeroeId = h });
                vista.Entidades.Add(new Entidad { Heroe = new Heroe { Id = h, DisplayName = h, FaccionId = faccion, ClassDefinitionId = "" } });
            }
            for (int i = 0; i < tropas.Count; i++)
            {
                var sid = $"{id}-s{i}";
                c.EscuadronIds.Add(sid);
                vista.Entidades.Add(new Entidad { Escuadron = new Escuadron { Id = sid, HeroeId = heroes[i % heroes.Count], TropaId = tropas[i], Cantidad = 10,
                    Contenedor = new ContenedorEscuadron { EjercitoId = id } } });
            }
            double d = Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y));
            double t0 = ahora - 20_000, t1 = t0 + d / velocidad * 1000;
            c.Plan = new Plan { Vertices = { new Vertice { EnMs = (long)t0, P = Pu(a.x, a.y) }, new Vertice { EnMs = (long)t1, P = Pu(b.x, b.y) } } };
            c.Posicion = Pu(a.x, a.y);
            vista.Entidades.Add(new Entidad { Ejercito = c });
        }

        static HornoDeFiguras Horno(MapaDeMundo mapa, DatosDeJuego datos, Func<string, Dictionary<string, string>> piezasDe = null)
        {
#if UNITY_EDITOR
            var prefabs = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>("Assets/ScriptableObjects/VisualPrefabConfig.asset");
#else
            VisualPrefabConfiguration prefabs = null;
#endif
            Assert.That(prefabs, Is.Not.Null, "falta VisualPrefabConfig.asset");
            var clase = ClaseDeHeroe.Leer(datos).FirstOrDefault();
            var h = mapa.gameObject.AddComponent<HornoDeFiguras>();
            // el héroe de la prueba: masculino, con la armadura media de inicio, el arma de su clase y su pelo y barba (los primeros del catálogo de
            // aspecto; sin ellos salía calvo)
            var piezas = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
            string Primera(List<Data.Avatar.AvatarPartDefinition> l) => l?.FirstOrDefault(p => !string.IsNullOrEmpty(p.id))?.id;
            h.Iniciar(prefabs, id => new SelectorDePersonaje.HeroeParaVer { Nombre = id, Clase = clase, Armadura = "media", Femenino = false, Piezas = piezasDe?.Invoke(id),
                Cabeza = Primera(piezas?.faceParts), Pelo = Primera(piezas?.hairParts), Barba = Primera(piezas?.beardParts), Cejas = Primera(piezas?.eyebrowsParts) });
            return h;
        }

        static IEnumerator EsperarHorneado(params string[] claves)
        {
            var fin = Time.realtimeSinceStartup + 240;
            while (claves.Any(k => !ModelosDeFiguras.Listos.ContainsKey(k)) && Time.realtimeSinceStartup < fin) yield return null;
        }

        [Category("Mapa"), Explicit("prueba técnica B del tema 7: la columna con su modelo")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator LaColumnaConSuModelo()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "columnas-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            var sitio = Buscador(relieve, artefacto);
            var donde = sitio(caminos.SitiosDeSuelo["suelo_pradera"], 6, null);
            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, NodosDelMapa.Estilo);
            MapaDeMundo.AclararCopas = false;
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
            NodosDelMapa.Estilo = EstiloDeNodos.Mejorado;
            try
            {
                var (almacen, sesion) = Almacen(url, donde, null);
                var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
                // la columna de la prueba: de cola a cabeza hacia el este, a 0,15 u/s; la propia (ej-1), a un lado y escondida
                double ahora = sesion.AhoraMundo;
                Columna(vista, "ej-b", new[] { "heroe-b" }, TropasConModelo, false, (donde.x - 4, donde.y), (donde.x + 60, donde.y), 0.15, ahora);
                // una plaza de nivel 2 al lado del camino, para juzgar la escala (la columna pasa junto a ella al poco de empezar)
                vista.Entidades.Add(new Entidad { Asentamiento = new Asentamiento { Id = "as-escala", Nombre = "Escala", Nivel = 2, FaccionId = "f-1", Posicion = Pu(donde.x + 6, donde.y - 1.6) } });
                almacen.Aplicar(new MensajeDeServidor { Foto = vista });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                // (la columna propia de la prueba, sin pintar: era el jinete de más, de costado)
                MapaDeMundo.PintarMiColumna = false;
                Horno(mapa, datos);
                HornoDeFiguras.Diagnostico = Environment.GetEnvironmentVariable("MUNDO_HORNO_DIAGNOSTICO") == "1";
                (double x, double y) Cabeza() { var p = Instante.Posicion(almacen.Buscar(TipoEntidad.Ejercito, "ej-b").Ejercito.Plan, null, sesion.AhoraMundo); return (p.x / 1000, p.y / 1000); }
                yield return Llegar(mapa, almacen, Cabeza());
                EsconderLaColumna();
                // de cerca, para que se pidan los modelos; luego, hasta que estén horneados
                yield return Vista(mapa, ZoomDelMapa.Cerca, 30, "tm_0_pidiendo");
                var claves = new List<string> { ModelosDeFiguras.ClaveDeHeroe("heroe-b") };
                yield return EsperarHorneado(claves.ToArray());
                var fin = Time.realtimeSinceStartup + 240;
                while (HornoDeFiguras.Bitacora.Count < 5 && Time.realtimeSinceStartup < fin) yield return null;
                yield return new WaitForSecondsRealtime(4);
                Debug.Log("[Columnas] horneado: " + string.Join("; ", HornoDeFiguras.Bitacora.Where(b => !b.StartsWith("  "))));
                foreach (var b in HornoDeFiguras.Bitacora.Where(b => b.StartsWith("  ")).Distinct()) Debug.Log("[Columnas] " + b);
                void Seguir() { var c = Cabeza(); almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito.Posicion = Pu(c.x - 0.6, c.y); }
                foreach (var (zoom, z) in new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                {
                    Seguir();
                    yield return Vista(mapa, zoom, 30, $"tm_{z}");
                    Debug.Log($"[Columnas] {z}: soldado de {ModelosDeFiguras.PixelesSoldado:0} px de alto a {rt.width}×{rt.height}; llamadas {ModelosDeFiguras.Llamadas}, figuras con modelo {ModelosDeFiguras.Instancias}, triángulos {ModelosDeFiguras.TriangulosPintados}");
                }
                foreach (var (giro, g) in new[] { (Math.PI / 2, "4_este"), (Math.PI, "5_detras"), (Math.PI * 1.5, "6_oeste") })
                {
                    mapa.Camara.Giro = giro;
                    Seguir();
                    yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"tm_{g}");
                }
                mapa.Camara.Giro = 0;
                for (int k = 1; k <= 4; k++)
                {
                    Seguir();
                    yield return Vista(mapa, ZoomDelMapa.Cerca, 30, $"tm_7_andando_{k}");
                }
                // la escala: cuando la cabeza pasa junto a la plaza, de cerca
                var fin2 = Time.realtimeSinceStartup + 120;
                while (Cabeza().x < donde.x + 5.5 && Time.realtimeSinceStartup < fin2) yield return null;
                Seguir();
                yield return Vista(mapa, ZoomDelMapa.Cerca, 30, "tm_8_escala");
            }
            finally
            {
                (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, NodosDelMapa.Estilo) = antes;
                MapaDeMundo.PintarMiColumna = true;
                ModelosDeFiguras.Reiniciar();
            }
        }

        /// <summary>El estandarte desde los cuatro lados a zoom habitual («te_estandarte_…») y las lanzas de la milicia en marcha, tendidas (como en
        /// batalla) y al hombro (el porte de marcha del horneado), de lado y por detrás, de cerca («te_lanzas_…»).</summary>
        [Category("Mapa"), Explicit("ronda 3 de la columna: estandarte y lanzas")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator ElEstandarteYLasLanzas()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "estandarte-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            var sitio = Buscador(relieve, artefacto);
            var donde = sitio(caminos.SitiosDeSuelo["suelo_pradera"], 6, null);
            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, NodosDelMapa.Estilo, HornoDeFiguras.PorteDeMarcha);
            MapaDeMundo.AclararCopas = false;
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
            NodosDelMapa.Estilo = EstiloDeNodos.Mejorado;
            try
            {
                var (almacen, sesion) = Almacen(url, donde, null);
                var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
                double ahora = sesion.AhoraMundo;
                // tres Facciones con su sigilo (la de quien mira y dos ajenas), cada una con su columna, una al lado de otra
                var azar = new System.Random(11);
                foreach (var (fid, nombreF) in new[] { ("f-1", "Propia"), ("f-2", "Segunda"), ("f-3", "Tercera") })
                    vista.Entidades.Add(new Entidad { Faccion = new Faccion { Id = fid, Nombre = nombreF, Sigilo = Sigilos.Aleatorio(azar) } });
                Columna(vista, "ej-b", new[] { "heroe-b" }, TropasConModelo, false, (donde.x - 4, donde.y), (donde.x + 60, donde.y), 0.05, ahora);
                Columna(vista, "ej-c", new[] { "heroe-c" }, TropasConModelo, false, (donde.x - 4.6, donde.y + 1.3), (donde.x + 59.4, donde.y + 1.3), 0.05, ahora, "f-2");
                Columna(vista, "ej-d", new[] { "heroe-d" }, TropasConModelo, false, (donde.x - 5.2, donde.y - 1.3), (donde.x + 58.8, donde.y - 1.3), 0.05, ahora, "f-3");
                almacen.Aplicar(new MensajeDeServidor { Foto = vista });
                var sigiloAntes = CosasEnElMapa.SigiloDe;
                CosasEnElMapa.SigiloDe ??= (fid, ancho) => Sigilos.DeFaccion(almacen, fid) is Sigilo sg ? TexturaDeSigilo.De(sg, ancho * 2) : null;
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                MapaDeMundo.PintarMiColumna = false;
                Horno(mapa, datos);
                (double x, double y) Cabeza() { var p = Instante.Posicion(almacen.Buscar(TipoEntidad.Ejercito, "ej-b").Ejercito.Plan, null, sesion.AhoraMundo); return (p.x / 1000, p.y / 1000); }
                void Seguir() { var c = Cabeza(); almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito.Posicion = Pu(c.x - 0.6, c.y); }
                yield return Llegar(mapa, almacen, Cabeza());
                EsconderLaColumna();
                foreach (var porte in new[] { false, true })
                {
                    HornoDeFiguras.PorteDeMarcha = porte;
                    foreach (var k in ModelosDeFiguras.Listos.Keys.Where(k => k.StartsWith("tropa|")).ToList()) ModelosDeFiguras.Olvidar(k);
                    HornoDeFiguras.Bitacora.Clear();
                    Seguir();
                    yield return Vista(mapa, ZoomDelMapa.Cerca, 30, "te_pidiendo");
                    var fin = Time.realtimeSinceStartup + 240;
                    while (HornoDeFiguras.Bitacora.Count(b => b.StartsWith("tropa|")) < 4 && Time.realtimeSinceStartup < fin) yield return null;
                    yield return new WaitForSecondsRealtime(3);
                    var nombre = porte ? "hombro" : "tendidas";
                    Seguir();
                    yield return Vista(mapa, ZoomDelMapa.Cerca, 30, "te_pidiendo");
                    // de cerca, con la cámara puesta a mano: de lado (desde el sur) y por detrás (desde el oeste); la columna va hacia el este
                    var c = Cabeza();
                    var cx = c.x - 0.45 * ColumnasDelMapa.Tropa;
                    var objetivo = EscalaDelMapa.AEscena(cx, c.y, mapa.AlturaVisible(cx, c.y)) + Vector3.up * (0.9f * ModelosDeFiguras.MetrosAEscena);
                    float hombre = 1.75f * ModelosDeFiguras.MetrosAEscena;
                    yield return DeCerca(mapa, objetivo, new Vector3(0, 2.2f, -11f) * hombre, $"te_lanzas_{nombre}_detras");
                    yield return DeCerca(mapa, objetivo, new Vector3(-6f, 3.5f, 1.5f) * hombre, $"te_lanzas_{nombre}_lado");
                }
                foreach (var (giro, g) in new[] { (0.0, "1_sur"), (Math.PI / 2, "2_este"), (Math.PI, "3_norte"), (Math.PI * 1.5, "4_oeste") })
                {
                    mapa.Camara.Giro = giro;
                    Seguir();
                    yield return Vista(mapa, ZoomDelMapa.Normal, 30, $"te_estandarte_{g}");
                }
                mapa.Camara.Giro = 0;
                foreach (var (zoom, z) in new[] { (ZoomDelMapa.Cerca, "1_cerca"), (ZoomDelMapa.Normal, "2_habitual"), (ZoomDelMapa.Alejado, "3_alejado") })
                {
                    Seguir();
                    yield return Vista(mapa, zoom, 30, $"te_estandarte_zoom_{z}");
                }
                // el mismo, una vez y media mayor (es un marcador), a zoom habitual y de cerca
                FigurasDelMapa.EstandarteEscala = 1.5f;
                Seguir();
                yield return Vista(mapa, ZoomDelMapa.Normal, 30, "te_estandarte_grande_habitual");
                yield return Vista(mapa, ZoomDelMapa.Cerca, 30, "te_estandarte_grande_cerca");
                FigurasDelMapa.EstandarteEscala = 1.5f;
                // el estandarte de cerca, de frente y de tres cuartos (la columna va hacia el este: de frente es desde el este)
                {
                    var c = Cabeza();
                    var cx = c.x - 0.3 * ColumnasDelMapa.Tropa;
                    float metro = ModelosDeFiguras.MetrosAEscena;
                    var objetivo = EscalaDelMapa.AEscena(cx, c.y, mapa.AlturaVisible(cx, c.y)) + Vector3.up * (2.6f * metro);
                    yield return DeCerca(mapa, objetivo, new Vector3(9f, 0.6f, 0) * metro, "te_estandarte_cerca_frente");
                    yield return DeCerca(mapa, objetivo, new Vector3(6.5f, 1.8f, -6.5f) * metro, "te_estandarte_cerca_tres_cuartos");
                }
                CosasEnElMapa.SigiloDe = sigiloAntes;
                Debug.Log("[Columnas] horneado: " + string.Join("; ", HornoDeFiguras.Bitacora));
            }
            finally
            {
                (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo, NodosDelMapa.Estilo, HornoDeFiguras.PorteDeMarcha) = antes;
                MapaDeMundo.PintarMiColumna = true;
                ModelosDeFiguras.Reiniciar();
            }
        }

        /// <summary>Una captura con la cámara del mapa puesta a mano en `objetivo + desde`, mirando a `objetivo` (`MapaDeMundo.CamaraFija`).</summary>
        IEnumerator DeCerca(MapaDeMundo mapa, Vector3 objetivo, Vector3 desde, string nombre)
        {
            MapaDeMundo.CamaraFija = (objetivo + desde, objetivo);
            try
            {
                yield return new WaitForSecondsRealtime(1f);
                var carpeta = Environment.GetEnvironmentVariable("MUNDO_CAPTURAS");
                if (string.IsNullOrEmpty(carpeta)) yield break;
                var antes = RenderTexture.active;
                RenderTexture.active = rt;
                var t = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                t.Apply();
                RenderTexture.active = antes;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(carpeta, nombre + ".png"), t.EncodeToPNG());
                UnityEngine.Object.Destroy(t);
            }
            finally { MapaDeMundo.CamaraFija = null; }
        }

        /// <summary>El color de bando por relación, las cuatro juntas: la columna propia (azul), una aliada de la misma Facción (verde), una de una
        /// Facción ajena (en guerra, rojo) y una sin Facción (neutral), una al lado de otra, de cerca y a zoom habitual («tb_…»).</summary>
        [Category("Mapa"), Explicit("color de bando por relación")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator ElColorDeBando()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "bando-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            var sitio = Buscador(relieve, artefacto);
            var donde = sitio(caminos.SitiosDeSuelo["suelo_pradera"], 6, null);
            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo);
            MapaDeMundo.AclararCopas = false;
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
            try
            {
                var (almacen, sesion) = Almacen(url, donde, null);
                var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
                double ahora = sesion.AhoraMundo;
                var azar = new System.Random(3);
                foreach (var fid in new[] { "f-1", "f-2" })
                    vista.Entidades.Add(new Entidad { Faccion = new Faccion { Id = fid, Nombre = fid, Sigilo = Sigilos.Aleatorio(azar) } });
                // la propia (ej-1, la del héroe de la prueba), con sus cuatro escuadras y andando hacia el este
                var propia = almacen.Buscar(TipoEntidad.Ejercito, "ej-1").Ejercito;
                for (int i = 0; i < 4; i++)
                {
                    var sid = $"ej-1-s{i}";
                    propia.EscuadronIds.Add(sid);
                    vista.Entidades.Add(new Entidad { Escuadron = new Escuadron { Id = sid, HeroeId = "heroe-1", TropaId = TropasConModelo[i], Cantidad = 10,
                        Contenedor = new ContenedorEscuadron { EjercitoId = "ej-1" } } });
                }
                propia.Estado = EstadoColumna.ColumnaMarchando;
                propia.Plan = new Plan { Vertices = { new Vertice { EnMs = (long)(ahora - 20_000), P = Pu(donde.x - 1, donde.y) },
                                                      new Vertice { EnMs = (long)(ahora - 20_000 + 60 / 0.05 * 1000), P = Pu(donde.x + 59, donde.y) } } };
                // la aliada (misma Facción), la de una Facción ajena y la neutral (sin Facción), en filas al lado
                Columna(vista, "ej-al", new[] { "heroe-al" }, TropasConModelo, false, (donde.x - 1.2, donde.y + 2.2), (donde.x + 58.8, donde.y + 2.2), 0.05, ahora, "f-1");
                Columna(vista, "ej-en", new[] { "heroe-en" }, TropasConModelo, false, (donde.x - 1.4, donde.y - 2.2), (donde.x + 58.6, donde.y - 2.2), 0.05, ahora, "f-2");
                Columna(vista, "ej-ne", new[] { "heroe-ne" }, TropasConModelo, false, (donde.x - 1.6, donde.y + 4.4), (donde.x + 58.4, donde.y + 4.4), 0.05, ahora, "");
                almacen.Aplicar(new MensajeDeServidor { Foto = vista });
                var sigiloAntes = CosasEnElMapa.SigiloDe;
                CosasEnElMapa.SigiloDe ??= (fid, ancho) => Sigilos.DeFaccion(almacen, fid) is Sigilo sg ? TexturaDeSigilo.De(sg, ancho * 2) : null;
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                MapaDeMundo.PintarMiColumna = true;
                var horno = Horno(mapa, datos);
                (double x, double y) Propia() { var p = Instante.Posicion(propia.Plan, null, sesion.AhoraMundo); return (p.x / 1000, p.y / 1000); }
                yield return Llegar(mapa, almacen, Propia());
                yield return Vista(mapa, ZoomDelMapa.Cerca, 30, "tb_pidiendo");
                var fin = Time.realtimeSinceStartup + 240;
                while (HornoDeFiguras.Bitacora.Count < 5 && Time.realtimeSinceStartup < fin) yield return null;
                yield return new WaitForSecondsRealtime(4);
                void Seguir() { var c = Propia(); propia.Posicion = Pu(c.x, c.y); }
                Seguir();
                yield return Vista(mapa, ZoomDelMapa.Normal, 30, "tb_habitual");
                Seguir();
                yield return Vista(mapa, ZoomDelMapa.Cerca, 30, "tb_cerca");
                // (la ruta propia, una cinta sobre el suelo, fuera de las capturas)
                foreach (var lr in mapa.GetComponentsInChildren<LineRenderer>(true)) if (lr.name == "Ruta") lr.widthMultiplier = 0;
                // cada columna de cerca, de lado (desde el sur): la propia, la aliada, la de la Facción en guerra y la neutral
                float metro = ModelosDeFiguras.MetrosAEscena;
                foreach (var (dy, quien, atras) in new[] { (0.0, "1_propia", 0.0), (2.2, "2_aliada", 0.2), (-2.2, "3_en_guerra", 0.4), (4.4, "4_neutral", 0.6) })
                {
                    var c0 = Propia();
                    var cx = c0.x - atras - 0.3 * ColumnasDelMapa.Tropa;
                    var objetivo = EscalaDelMapa.AEscena(cx, c0.y + dy, mapa.AlturaVisible(cx, c0.y + dy)) + Vector3.up * metro;
                    yield return DeCerca(mapa, objetivo, new Vector3(-2f, 4f, -13f) * metro, $"tb_de_cerca_{quien}");
                }
                // el héroe del juego (vivo, con sus materiales y sus Animators) al lado del horneado de la columna propia, de lado y de tres cuartos
                var cabeza = Propia();
                var pieVivo = EscalaDelMapa.AEscena(cabeza.x, cabeza.y - 0.35, mapa.AlturaVisible(cabeza.x, cabeza.y - 0.35));
                var vivo = horno.HeroeVivo("heroe-1", pieVivo, Quaternion.LookRotation(Vector3.right, Vector3.up), ModelosDeFiguras.MetrosAEscena, true);
                Assert.That(vivo, Is.Not.Null, "no se pudo montar el héroe vivo");
                var medio = EscalaDelMapa.AEscena(cabeza.x, cabeza.y - 0.18, mapa.AlturaVisible(cabeza.x, cabeza.y - 0.18)) + Vector3.up * (1.4f * metro);
                yield return DeCerca(mapa, medio, new Vector3(0, 1.2f, -7f) * metro, "th_heroe_de_lado");
                yield return DeCerca(mapa, medio, new Vector3(5f, 2.2f, -5f) * metro, "th_heroe_tres_cuartos");
                UnityEngine.Object.Destroy(vivo);
                CosasEnElMapa.SigiloDe = sigiloAntes;
                Debug.Log("[Bando] horneado: " + string.Join("; ", HornoDeFiguras.Bitacora.Where(b => !b.StartsWith("  "))));
            }
            finally
            {
                (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo) = antes;
                ModelosDeFiguras.Reiniciar();
            }
        }

        [Category("Mapa"), Explicit("coste de 30 columnas con su modelo")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator ElCosteDeLasColumnas()
        {
            // 30 columnas (la mitad personales de 6 escuadras, la mitad ejércitos de 12) DENTRO del encuadre en cada zoom: se recolocan alrededor del
            // centro en un radio que cabe en pantalla. Por zoom, el fotograma con las cajas de hoy, con las figuras de bloques y con los modelos, y
            // cuántas figuras, llamadas y triángulos.
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            yield return Preparar(url, "columnas-coste-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            var (cx, cy) = caminos.SitiosDeSuelo["suelo_pradera"];
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            var antes = (MaquetasDelMapa.Estilo, ModelosDeFiguras.Activos);
            try
            {
                var (almacen, sesion) = Almacen(url, (cx, cy), null);
                var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
                double ahora = sesion.AhoraMundo;
                var azarF = new System.Random(5);
                foreach (var fid in new[] { "f-1", "f-2", "f-3" })
                    vista.Entidades.Add(new Entidad { Faccion = new Faccion { Id = fid, Nombre = fid, Sigilo = Sigilos.Aleatorio(azarF) } });
                for (int i = 0; i < 30; i++)
                {
                    bool ejercito = i % 2 == 1;
                    int nh = ejercito ? 1 + i % 4 : 1, ne = ejercito ? 12 : 6;
                    var heroes = Enumerable.Range(0, nh).Select(k => $"h{i}-{k}").ToList();
                    var tropas = Enumerable.Range(0, ne).Select(k => TropasConModelo[(i + k) % 4]).ToList();
                    Columna(vista, $"ej-c{i}", heroes, tropas, ejercito, (cx, cy), (cx + 1, cy), 0.02, ahora, new[] { "f-1", "f-2", "f-3" }[i % 3]);
                }
                almacen.Aplicar(new MensajeDeServidor { Foto = vista });
                var sigiloAntes = CosasEnElMapa.SigiloDe;
                CosasEnElMapa.SigiloDe ??= (fid, ancho) => Sigilos.DeFaccion(almacen, fid) is Sigilo sg ? TexturaDeSigilo.De(sg, ancho * 2) : null;
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                MapaDeMundo.PintarMiColumna = false;
                Horno(mapa, datos);
                yield return Llegar(mapa, almacen, (cx, cy));
                EsconderLaColumna();
                // las columnas, repartidas en un radio `r` alrededor del centro, cada una andando despacio hacia un lado
                void Recolocar(double r, int cuantas = 30)
                {
                    var rng = new System.Random(7);
                    double t = sesion.AhoraMundo;
                    for (int i = 0; i < 30; i++)
                    {
                        double a = rng.NextDouble() * Math.PI * 2, d = r * Math.Sqrt(rng.NextDouble()), rumbo = rng.NextDouble() * Math.PI * 2;
                        var p = (x: cx + d * Math.Cos(a), y: cy + d * Math.Sin(a));
                        if (i >= cuantas) p = (cx + 60 + i, cy + 60);   // (las que sobran, fuera del encuadre)
                        var c = almacen.Buscar(TipoEntidad.Ejercito, $"ej-c{i}").Ejercito;
                        double v = 0.02, largo = 50;
                        // (el plan empezó hace 20 s con la cabeza 0,4 u más atrás: la cola queda detrás y en el encuadre)
                        var a0 = (x: p.x - 0.4 * Math.Cos(rumbo), y: p.y - 0.4 * Math.Sin(rumbo));
                        c.Plan = new Plan { Vertices = { new Vertice { EnMs = (long)(t - 20_000), P = Pu(a0.x, a0.y) },
                                                         new Vertice { EnMs = (long)(t - 20_000 + largo / v * 1000), P = Pu(a0.x + largo * Math.Cos(rumbo), a0.y + largo * Math.Sin(rumbo)) } } };
                        c.Posicion = Pu(p.x, p.y);
                    }
                }
                // primero, todo horneado
                MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
                Recolocar(3);
                yield return Vista(mapa, ZoomDelMapa.Cerca, 30, "tm_coste_pidiendo");
                var fin = Time.realtimeSinceStartup + 300;
                while (HornoDeFiguras.Bitacora.Count < 20 && Time.realtimeSinceStartup < fin) yield return null;
                yield return new WaitForSecondsRealtime(3);
                var px0 = ModelosDeFiguras.PixelesMinimos;
                foreach (var (zoom, radio, z, cuantas) in new[] { (ZoomDelMapa.Cerca, 2.6, "cerca", 30), (ZoomDelMapa.Normal, 4.6, "habitual", 30), (ZoomDelMapa.Alejado, 7.0, "alejado", 30),
                                                                  (ZoomDelMapa.Cerca, 2.6, "cerca realista (8 columnas)", 8) })
                {
                    Recolocar(radio, cuantas);
                    var sombra0 = ModelosDeFiguras.PixelesSombra;
                    foreach (var (nombre, estilo, modelos, sinSombras) in new[] { ("cajas de hoy", EstiloDeAsentamientos.Prototipo, false, false),
                                                                      ("figuras de bloques", EstiloDeAsentamientos.Mejorado, false, false),
                                                                      ("con su modelo", EstiloDeAsentamientos.Mejorado, true, false),
                                                                      ("con su modelo, sin sombras", EstiloDeAsentamientos.Mejorado, true, true) })
                    {
                        MaquetasDelMapa.Estilo = estilo;
                        ModelosDeFiguras.Activos = modelos;
                        ModelosDeFiguras.PixelesMinimos = px0;
                        ModelosDeFiguras.PixelesSombra = sinSombras ? 1e6f : sombra0;
                        yield return Vista(mapa, zoom, 30, $"tm_coste_{z}_{(modelos ? "modelos" : estilo.ToString())}");
                        var fot = new List<double>();
                        var hiloPrincipal = new List<double>();
                        var hiloDibujo = new List<double>();
                        var sacar = new List<double>();
                        var poner = new List<double>();
                        var mandar = new List<double>();
                        int figuras = 0, llamadas = 0, modelos_ = 0;
                        long tris = 0;
                        using var rPrincipal = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "Main Thread", 1);
                        using var rDibujo = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "Render Thread", 1);
                        for (int i = 0; i < 240; i++)
                        {
                            yield return null;
                            fot.Add(Time.unscaledDeltaTime * 1000);
                            if (rPrincipal.Valid) hiloPrincipal.Add(rPrincipal.LastValue / 1e6);
                            if (rDibujo.Valid) hiloDibujo.Add(rDibujo.LastValue / 1e6);
                            sacar.Add(CosasDelMapa.MsFiguras);
                            poner.Add(CosasEnElMapa.MsFiguras);
                            mandar.Add(CosasEnElMapa.MsModelos);
                            figuras = Math.Max(figuras, FigurasDelMapa.Figuras + ModelosDeFiguras.Instancias);
                            modelos_ = Math.Max(modelos_, ModelosDeFiguras.Instancias);
                            llamadas = Math.Max(llamadas, FigurasDelMapa.Llamadas + ModelosDeFiguras.Llamadas);
                            tris = Math.Max(tris, ModelosDeFiguras.TriangulosPintados);
                        }
                        fot.Sort();
                        hiloPrincipal.Sort();
                        hiloDibujo.Sort(); sacar.Sort(); poner.Sort(); mandar.Sort();
                        string Med(List<double> l) => l.Count == 0 ? "—" : $"{l[l.Count / 2]:0.00} ms";
                        Debug.Log($"[Coste] {z} · {nombre}: hilo de dibujo {Med(hiloDibujo)}; figuras: sacarlas (estela) {Med(sacar)}, ponerlas (matrices) {Med(poner)}, mandar los modelos {Med(mandar)}");
                        Debug.Log($"[Coste] {z} · {nombre}: con todo {ModelosDeFiguras.Completos}, reducidas {ModelosDeFiguras.Reducidos}");
                        Debug.Log($"[Coste] {z} · {nombre}: fotograma mediana {fot[fot.Count / 2]:0.00} ms (p95 {fot[fot.Count * 95 / 100]:0.00}), hilo principal {Med(hiloPrincipal)}; " +
                                  $"figuras {figuras} (con modelo {modelos_}), llamadas de figuras {llamadas}, triángulos de modelos {tris}");
                    }
                }
                ModelosDeFiguras.PixelesMinimos = px0;
                ModelosDeFiguras.PixelesSombra = 40;
                CosasEnElMapa.SigiloDe = sigiloAntes;
            }
            finally
            {
                (MaquetasDelMapa.Estilo, ModelosDeFiguras.Activos) = antes;
                MapaDeMundo.PintarMiColumna = true;
                ModelosDeFiguras.Reiniciar();
            }
        }

        /// <summary>Los caballos de guerra en el mapa: seis columnas andando, una por montura del motor (tiro, estepa, escita, Tesalia, nisea) y
        /// una sin montura, con el caballo de antes; el héroe de cerca, de lado y de tres cuartos («tg_{montura}_lado|tres_cuartos»), y todas a
        /// zoom habitual («tg_habitual»).</summary>
        [Category("Mapa"), Explicit("tema 7: los caballos de guerra del héroe")]
        [UnityTest, Timeout(3_000_000)]
        public IEnumerator LosCaballosDeGuerra()
        {
            var url = Environment.GetEnvironmentVariable("BRONZEAGE_PRUEBA_URL");
            if (string.IsNullOrEmpty(url)) Assert.Ignore("sin BRONZEAGE_PRUEBA_URL: no hay servidor de prueba");
            Assert.That(ConquestTactics.Visual.CaballosDeGuerra.Cargar(), Is.Not.Null, "falta Resources/Mounts/CaballosDeGuerra");
            yield return Preparar(url, "caballos-");
            var (relieve, artefacto, datos) = (pRelieve, pArtefacto, pDatos);
            var caminos = BancoDeCaminos.Hacer(relieve, artefacto, datos);
            Sigilos.Cargar(Resources.Load<TextAsset>("MundoUI/Sigilo/catalogoSigilos").text, "{}", "{}");
            rt = new RenderTexture(1920, 1080, 24) { antiAliasing = 4 };
            var sitio = Buscador(relieve, artefacto);
            var donde = sitio(caminos.SitiosDeSuelo["suelo_pradera"], 6, null);
            var antes = (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo);
            MapaDeMundo.AclararCopas = false;
            MaquetasDelMapa.Estilo = EstiloDeAsentamientos.Mejorado;
            var monturas = new[] { "antes", "montura_tiro", "montura_estepa", "montura_escita", "montura_tesalia", "montura_nisea" };
            try
            {
                var (almacen, sesion) = Almacen(url, donde, null);
                var vista = new Foto { Ambito = new AmbitoPb { Tipo = AmbitoTipo.AmbitoVista, Id = "f-1" }, Version = 2 };
                double ahora = sesion.AhoraMundo;
                for (int i = 0; i < monturas.Length; i++)
                    Columna(vista, $"ej-g{i}", new[] { $"h-{monturas[i]}" }, new[] { "milicia_lanceros" }, false, (donde.x, donde.y + 1.6 * i - 4),
                            (donde.x + 60, donde.y + 1.6 * i - 4), 0.05, ahora);
                almacen.Aplicar(new MensajeDeServidor { Foto = vista });
                var mapa = Montar(almacen, sesion, relieve, artefacto, datos);
                mapa.RadioVisionFijo = 15;
                MapaDeMundo.PintarMiColumna = false;
                // (vestido con las piezas de inicio de la armadura media, como sin piezas, y su montura)
                Horno(mapa, datos, id => id.StartsWith("h-montura_") ? new Dictionary<string, string> { ["torso"] = "torso_media_inicio", ["guantes"] = "guantes_media_inicio",
                    ["pantalones"] = "pantalones_media_inicio", ["botas"] = "botas_media_inicio", ["montura"] = id.Substring(2) } : null);
                (double x, double y) Cabeza(int i) { var p = Instante.Posicion(almacen.Buscar(TipoEntidad.Ejercito, $"ej-g{i}").Ejercito.Plan, null, sesion.AhoraMundo); return (p.x / 1000, p.y / 1000); }
                yield return Llegar(mapa, almacen, Cabeza(2));
                EsconderLaColumna();
                // (a zoom habitual se ven las seis y se piden todas)
                yield return Vista(mapa, ZoomDelMapa.Normal, 30, "tg_pidiendo");
                ModelosDeFiguras.PixelesMinimos = 1;
                for (int k = 0; k < 8 && !monturas.All(m => ModelosDeFiguras.Listos.ContainsKey(ModelosDeFiguras.ClaveDeHeroe($"h-{m}"))); k++)
                {
                    yield return Vista(mapa, k % 2 == 0 ? ZoomDelMapa.Normal : ZoomDelMapa.Cerca, 30, "tg_pidiendo");
                    var fin = Time.realtimeSinceStartup + 30;
                    while (!monturas.All(m => ModelosDeFiguras.Listos.ContainsKey(ModelosDeFiguras.ClaveDeHeroe($"h-{m}"))) && Time.realtimeSinceStartup < fin) yield return null;
                }
                ModelosDeFiguras.PixelesMinimos = 14;
                yield return new WaitForSecondsRealtime(3);
                foreach (var b in HornoDeFiguras.Bitacora.Distinct().Take(60)) Debug.Log("[Caballos] " + b);
                Assert.That(monturas.All(m => ModelosDeFiguras.Listos.ContainsKey(ModelosDeFiguras.ClaveDeHeroe($"h-{m}"))),
                            "sin hornear: " + string.Join(", ", monturas.Where(m => !ModelosDeFiguras.Listos.ContainsKey(ModelosDeFiguras.ClaveDeHeroe($"h-{m}")))));
                float metro = ModelosDeFiguras.MetrosAEscena;
                for (int i = 0; i < monturas.Length; i++)
                {
                    var c = Cabeza(i);
                    var objetivo = EscalaDelMapa.AEscena(c.x, c.y, mapa.AlturaVisible(c.x, c.y)) + Vector3.up * (1.3f * metro);
                    yield return DeCerca(mapa, objetivo, new Vector3(0.4f, 0.9f, -5.5f) * metro, $"tg_{monturas[i]}_lado");
                    yield return DeCerca(mapa, objetivo, new Vector3(4f, 1.6f, -4f) * metro, $"tg_{monturas[i]}_tres_cuartos");
                }
                yield return Llegar(mapa, almacen, Cabeza(2));
                yield return Vista(mapa, ZoomDelMapa.Normal, 30, "tg_habitual");
            }
            finally
            {
                (MapaDeMundo.AclararCopas, MaquetasDelMapa.Estilo) = antes;
                MapaDeMundo.PintarMiColumna = true;
                ModelosDeFiguras.Reiniciar();
            }
        }
    }
}
