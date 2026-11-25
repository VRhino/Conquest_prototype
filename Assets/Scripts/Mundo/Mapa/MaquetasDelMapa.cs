// Los asentamientos vistos desde el mundo (tema 6): plazas, pueblos y aldeas, avanzadas, obras, campamentos de mercenarios y de bandidos,
// alijos y batallas. Tres estilos, conmutables (`Estilo`):
//  * «Cajas»: lo de antes, la caja de su color de `CosasEnElMapa` (aquí no se pinta nada).
//  * «Prototipo» (etapa 1): las maquetas del prototipo tal cual (`MaquetasDelPrototipo`: cajas y tiendas por material, banderas del color de su
//    Facción), instanciadas por material con el sombreado de color plano del mapa (`Conquest/MapaColor`: el sol con algo de cielo, la sombra
//    real y la bruma); las plazas que solo se recuerdan, bajo la niebla de guerra y las nubes; lo demás, encima, como el prototipo.
//  * «Mejorado» (etapa 2): las plazas de nivel 1 (dos variantes, con su suelo pisado y sus sendas en un parche, `Conquest/MapaParcheAsentamiento`)
//    y la de nivel 3 son miniaturas de Blender (`BronzeAgeArte/mapa/asentamientos`, `Conquest/MapaAsentamiento`:
//    una malla con su color y su oclusión horneados, tres niveles de detalle, la muralla aparte para cuando no la tiene y las banderas del color
//    de su Facción), a la huella de la plaza (`CosasDelMapa.HuellaDePlaza`: la del prototipo, con un mínimo de 0,9 u); el nivel de detalle,
//    por su ancho en pantalla; asentada a la altura media de su huella, inclinada la mitad de la pendiente
//    (hasta 8,5°) y con su zócalo de tierra y piedra. Lo demás, como el prototipo.
// Se rehacen cuando cambia lo que hay (la clave de lo que se pinta); a la altura de la malla de suelo pintada como el prototipo (suelo − 0,02 u +
// la base de la pieza), y se vuelven a asentar cuando cambia el suelo de debajo (`TerrenoDelMapa.Version`). Cada maqueta deja su claro sin
// árboles con los radios del prototipo (`Claros`, por `MapaDeMundo.Claros`).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BronzeAge.Contract.V1;
using UnityEngine;
using UnityEngine.Rendering;
using P = ConquestTactics.Mundo.MaquetasDelPrototipo;

namespace ConquestTactics.Mundo.Mapa
{
    public enum EstiloDeAsentamientos { Cajas, Prototipo, Mejorado }

    public sealed class MaquetasDelMapa
    {
        /// <summary>El estilo; «Mejorado» también le da a las plazas la huella con mínimo (`CosasDelMapa.HuellaDePlaza`: aro y clic incluidos).</summary>
        public static EstiloDeAsentamientos Estilo
        {
            get => estilo;
            set { estilo = value; CosasDelMapa.HuellaMejorada = value == EstiloDeAsentamientos.Mejorado; }
        }
        static EstiloDeAsentamientos estilo = EstiloDeAsentamientos.Prototipo;
        /// <summary>«Mejorado»: el nivel del modelo por su ancho en pantalla (px): el cerca, de `AnchoCerca` para arriba; el medio, hasta
        /// `AnchoMedio`; más pequeña, el lejano.</summary>
        public static float AnchoCerca = 80, AnchoMedio = 40;
        /// <summary>«Mejorado»: lo que queda del color de la tierra del suelo pisado en lo seco (en la pradera, 0,82).</summary>
        public static float OscuroEnSeco = 0.82f;
        /// <summary>Para las pruebas (la luminancia de lo construido frente al suelo): dibujar las miniaturas y su suelo pisado.</summary>
        public static bool DibujarMiniaturas = true, DibujarSuelo = true, SombraDeMiniaturas = true, DibujarCajas = true;
        /// <summary>Lo más que se inclina la miniatura hacia la ladera (la mitad de la pendiente, como mucho esto).</summary>
        public const double InclinacionMaxima = 8.5;
        const int PorLlamada = 1023;

        /// <summary>¿Pinta esta clase de cosa (si no, la pinta `CosasEnElMapa` con su caja)?</summary>
        public static bool Pinta(ClaseDeCosa c) => Estilo != EstiloDeAsentamientos.Cajas && (c == ClaseDeCosa.Plaza || c == ClaseDeCosa.Pueblo || c == ClaseDeCosa.Aldea
            || c == ClaseDeCosa.Avanzada || c == ClaseDeCosa.Obra || c == ClaseDeCosa.Mercenarios || c == ClaseDeCosa.Bandidos || c == ClaseDeCosa.Alijo
            || c == ClaseDeCosa.Batalla);

        /// <summary>Un lote: la malla (caja, tienda o bandera), el material (encima o bajo la niebla) y las piezas, con su color.</summary>
        sealed class Lote
        {
            public Mesh Malla;
            public bool BajoNiebla;
            public List<P.Pieza> Piezas = new List<P.Pieza>();
            public List<Vector4> Colores = new List<Vector4>();
            public Matrix4x4[] Matrices;
        }

        readonly TerrenoDelMapa terreno;
        readonly Material encima, bajoNiebla;
        readonly Mesh caja, tienda, bandera;
        readonly List<Lote> lotes = new List<Lote>();
        readonly MaterialPropertyBlock propiedades = new MaterialPropertyBlock();
        readonly Matrix4x4[] tanda = new Matrix4x4[PorLlamada];
        readonly Vector4[] tandaColores = new Vector4[PorLlamada];
        string clave = "";
        int version = -1;

        /// <summary>«Mejorado»: las miniaturas puestas (la plaza de nivel 3): dónde, su radio de ciudad, si tiene muralla, si va bajo la niebla y el
        /// color de su Facción; y su matriz, al asentarla.</summary>
        sealed class Miniatura
        {
            public double X, Y, Rc, Semilla, Nivel, Huella, Rumbo;
            /// <summary>Lo que mide en u una unidad del modelo: en las plazas, su huella (NaN); en los pueblos, la del caserío de nivel 1 (0,9).</summary>
            public double Escala = double.NaN;
            public bool Muralla, BajoNiebla, Espejo;
            /// <summary>Las plazas, asentadas por vértice: la rejilla (centro x, z de la escena, medio lado, m; lado en puntos) y lo que sube o
            /// baja cada punto sobre el plano del sesgo (m) para quedar en el suelo pintado (`Residuo`, el mismo cálculo que el sombreado).</summary>
            public Vector4 Rejilla;
            public float[] Residuos;
            /// <summary>Asentada por vértice (las plazas y los pueblos hechos a la medida de la choza).</summary>
            public bool Asentar;
            /// <summary>Una pieza suelta: su escala frente a la de su miniatura (1, la misma).</summary>
            public double EscalaPieza = 1;
            /// <summary>En las aldeas: sus parcelas, su capa en `celdas`, su cultivo (índice de `Cultivos`) y la malla de sus campos.</summary>
            public ParcelasDeAldea Campos;
            public int Capa, Cultivo;
            public Mesh MallaCampos;
            /// <summary>En las aldeas: su plaza madre (la primera senda va hacia ella) y sus sendas (u, en el mapa).</summary>
            public string Madre;
            public List<List<(double x, double y)>> Sendas;
            /// <summary>En las aldeas: las cabezas del rebaño y las piezas sueltas sobre los campos (tipo de `NombresDePiezas`, matriz).</summary>
            public long Cabezas;
            /// <summary>En las ganaderas: si el rebaño es vacuno (vacas) o de otra especie (ovejas); y las calvas de tierra pisada de las
            /// entradas de sus cercados (u, en el mapa).</summary>
            public bool Vacuno;
            public List<(double x, double y)> Calvas;
            public List<(int tipo, Matrix4x4 m)> Piezas;
            /// <summary>El color del recurso de su nodo (los montones de mineral de las minas; alfa 0 si no tiene).</summary>
            public Vector4 Faccion, Recurso;
            /// <summary>Las plazas: su Facción (el blasón de sus estandartes).</summary>
            public string FaccionId;
            public Modelo Modelo;
            public Matrix4x4 M, MMuralla;
            public Mesh Parche;
            /// <summary>Lo menudo: una pieza suelta va pegada a su miniatura (`Padre`), en su sitio del modelo (x, y de Blender, en sus unidades)
            /// y con su giro (rad, el de Blender); se asienta con ella.</summary>
            public Miniatura Padre;
            public double Lx, Ly, LGiro;
            /// <summary>Lo menudo: la batalla lleva su humo y su llama.</summary>
            public bool Humo;
            /// <summary>Tu batalla (tema 10, punto 10): el estandarte de las columnas, con el blasón de `FaccionId` y este color, junto al carro.</summary>
            public Vector4? EstandarteDeBando;
            /// <summary>Lo menudo: la huella (u) con que se elige su nivel de detalle (no menos de `HuellaDeDetalleMinima`: lo pequeño, como
            /// la atalaya o el alijo, pasaba al medio ya de cerca); NaN, la suya.</summary>
            public double HuellaDetalle = double.NaN;
        }

        /// <summary>El eje x del modelo en Unity frente al de Blender (el FBX lo importa vuelto: x → −x; y de Blender → −z). Lo comprueba
        /// `MenudoDelMapaTests.ElEjeDeLosModelos`.</summary>
        public const float EjeX = -1;

        /// <summary>Para las pruebas: la escala del mapa de lo menudo por el id de la cosa (la tira de comparación de las avanzadas).</summary>
        public static readonly Dictionary<string, double> EscalaForzada = new Dictionary<string, double>();

        /// <summary>Lo menudo: el claro sin árboles ni matas (u): la huella y su margen (`MargenDeClaro`: que las copas y las matas del borde no
        /// pisen la cerca ni el muro), y el alijo, como poco `ClaroDeAlijo` (que se vea entre los árboles desde los cuatro lados).</summary>
        public static double MargenDeClaro = 0.35, ClaroDeAlijo = 2.0; // (con 0,9 y 1,3 u, las copas que quedan entre la cámara y el alijo lo tapaban desde un lado: con la cámara a 30°, unas copas de 1,2 u tapan 2 u por delante)
        public static double ClaroDeMenudo(ClaseDeCosa clase, double huella) => clase == ClaseDeCosa.Alijo ? Math.Max(ClaroDeAlijo * CosasDelMapa.Fijos(clase), huella + MargenDeClaro * CosasDelMapa.Fijos(clase)) : huella + MargenDeClaro * CosasDelMapa.Fijos(clase);

        /// <summary>Lo menudo: lo menos que cuenta su huella para el nivel de detalle (u): el cambio al medio y al lejos, más lejos.</summary>
        public static double HuellaDeDetalleMinima = 0.6;

        /// <summary>Del marco de Blender (x, y en el suelo, z arriba, en unidades del modelo) al local de la malla importada en Unity.</summary>
        public static Vector3 DeBlender(double x, double y, double z = 0) => new Vector3((float)(EjeX * x), (float)z, (float)-y);

        /// <summary>La matriz local de una pieza en (x, y) de Blender, girada `giro` (rad, el sentido de Blender) sobre el eje vertical.</summary>
        public static Matrix4x4 LocalDeBlender(double x, double y, double giro) =>
            Matrix4x4.TRS(DeBlender(x, y), Quaternion.Euler(0, (float)(EjeX * giro * 180 / Math.PI), 0), Vector3.one);
        /// <summary>Un modelo de Blender: sus tres niveles de detalle, la muralla aparte (si la tiene), sus materiales (encima y bajo la niebla) y
        /// la máscara del suelo pisado (si la tiene, con el material de su parche).</summary>
        sealed class Modelo
        {
            public readonly Mesh[] Villa = new Mesh[3], Muralla = new Mesh[3];
            public Material Encima, Bajo, Parche, ParcheBajo;
            /// <summary>La muralla en su propio FBX (`muralla<nivel>_<v>`, niveles 2, 4 y 5), con su material; si no, la de la villa (el 3).</summary>
            public Material MurallaEncima, MurallaBajo;
            /// <summary>El radio de su muralla (en huellas, con tope `RadioMaximoDeMuralla`) y el ángulo de su puerta (rad, en el marco de la villa:
            /// x, y de Blender; NaN si no lo sabe).</summary>
            public double RadioMuralla = 1, Puerta = double.NaN;
            /// <summary>El lado (en unidades del modelo) que cubre la máscara del suelo pisado: [−LadoSuelo, LadoSuelo]; y, en los pueblos, su
            /// radio en u (el del json: huella, aro y claro).</summary>
            public double LadoSuelo = LadoDelSuelo, RadioU = double.NaN;
            /// <summary>En las plazas hechas a la medida de la choza: u por unidad del modelo (escala fija, su json `unidad_u`); NaN en las de
            /// antes, que se estiran a su huella.</summary>
            public double UnidadU = double.NaN;
            /// <summary>(Lo menudo a la medida de la choza: las avanzadas) va a su `unidad_u`, sin lo fijo ni `escala_mapa`.</summary>
            public bool EscalaFija;
            /// <summary>En los núcleos de aldea: lo construido [(x, y, r)], en unidades del modelo y en el marco de la máscara del suelo.</summary>
            public (double x, double y, double r)[] Ocupado;
            /// <summary>Lo menudo (parte 5): lo que Conquest multiplica además su unidad en el mapa (`escala_mapa` del json: 2 los campamentos y
            /// los alijos); sus sitios, en unidades del modelo (x, y de Blender): el solar del edificio militar (x, y, giro), el de la mercancía
            /// del alijo, y dónde van el humo y la llama de la batalla (x, y, z).</summary>
            public double EscalaMapa = 1;
            public double[] SolarMilitar, SitioMercancia, HumoEn, LlamaEn;
            /// <summary>Las plazas (repaso del 23-11): las anclas de sus estandartes, el de las columnas en vez del paño de la malla (pie x, y, z de
            /// Blender y alto del mástil, en huellas), de la villa y de su muralla aparte.</summary>
            public (double x, double y, double z, double alto)[] Estandartes = new (double, double, double, double)[0],
                EstandartesMuralla = new (double, double, double, double)[0];
        }

        /// <summary>Las anclas de estandarte de un json de plaza o muralla («estandartes»: [[x, y, z, alto], …]); vacío si no las trae.</summary>
        static (double x, double y, double z, double alto)[] Anclas(TextAsset t)
        {
            if (t == null) return new (double, double, double, double)[0];
            var j = Newtonsoft.Json.Linq.JObject.Parse(t.text)["estandartes"];
            return j == null ? new (double, double, double, double)[0]
                : j.Select(a => ((double)a[0], (double)a[1], (double)a[2], (double)a[3])).ToArray();
        }

        /// <summary>«Mejorado»: con los estandartes de las plazas en sus anclas.</summary>
        public static bool EstandartesEnPlazas = true;
        /// <summary>Dónde se planta el estandarte de tu batalla: en el marco del carro (x, y de Blender), en radios de su huella.</summary>
        public static Vector2 SitioDelEstandarteDeBatalla = new Vector2(0.75f, -0.45f);
        /// <summary>Estandartes de plaza puestos en el último `Dibujar` y dónde (la escena), para las pruebas.</summary>
        public int EstandartesPuestos { get; private set; }
        public readonly List<Vector3> PiesDeEstandarte = new List<Vector3>();
        /// <summary>«Mejorado», parte 5 y lo menudo: los modelos por nombre (los de `hornear5.py`).</summary>
        public static readonly string[] NombresMenudos =
        {
            "atalaya", "fuerte", "fuerte_ruina", "fuerte_obras", "ciudadela_1", "ciudadela_1_ruina", "ciudadela_1_obras", "ciudadela_2", "ciudadela_2_ruina",
            "ciudadela_2_obras", "mercenarios", "bandidos_1_1", "bandidos_1_2", "bandidos_1_3", "bandidos_2_1", "bandidos_2_2", "bandidos_2_3", "obra_estacas",
            "obra_mitad", "alijo_1", "alijo_2", "batalla",
        };
        /// <summary>Las piezas del kit de obra (`obra_kit`), para la reparación de una ruina y la mejora pendiente de materiales.</summary>
        public static readonly string[] PiezasDelKit = { "andamio", "cabria", "adobes", "sillares", "sombrajo" };
        readonly Dictionary<string, Modelo> menudos = new Dictionary<string, Modelo>();
        /// <summary>Las piezas sueltas de lo menudo por «atlas/pieza» (el kit de obra, el edificio militar y la mercancía): cerca, medio y medio.</summary>
        readonly Dictionary<string, Modelo> piezasMenudas = new Dictionary<string, Modelo>();
        Material materialHumo, materialLlama, materialResplandor;
        Mesh mallaHumo, mallaLlama, mallaResplandor;
        /// <summary>Para las pruebas: cuántas columnas de humo (y llamas) de batalla se mandaron a dibujar.</summary>
        public int Humaredas { get; private set; }
        /// <summary>Para las pruebas: ¿están los modelos de lo menudo?</summary>
        public bool HayMenudos => menudos.Count > 0;
        /// <summary>«Mejorado»: los núcleos de aldea por (labor o ganado, nivel 1 a 3).</summary>
        readonly Dictionary<(string, int), Modelo> nucleos = new Dictionary<(string, int), Modelo>();
        /// <summary>Las posiciones de los nodos de recursos (u), para no labrar sobre ellos.</summary>
        public IReadOnlyList<(double x, double y)> PosicionesDeNodos = Array.Empty<(double, double)>();
        Material materialCampos, materialCamposBajo;
        Texture2DArray celdas;
        /// <summary>Los cultivos de los campos, en el orden de `_Paleta` (4 estados cada uno).</summary>
        static readonly string[] Cultivos = { "trigo", "cebada", "vid", "oliva", "sesamo", "ganado" };
        /// <summary>Las piezas sueltas de los campos (`piezas.fbx`, de `hornear.py piezas`): se dibujan instanciadas, todas las de la vista juntas.</summary>
        static readonly string[] NombresDePiezas = { "olivo", "cepas", "gavillas", "hacina", "cerca", "res", "oveja", "abrevadero", "res_crema", "res_parda" };
        readonly Mesh[] piezas = new Mesh[NombresDePiezas.Length];
        Material materialPiezas;
        readonly List<Matrix4x4>[] porPieza = Enumerable.Range(0, NombresDePiezas.Length).Select(_ => new List<Matrix4x4>()).ToArray();
        /// <summary>Para las pruebas: cuántas piezas sueltas de los campos se mandaron a dibujar.</summary>
        public int PiezasDeCampos { get; private set; }
        /// <summary>«Mejorado»: los pueblos extractores por (cantera o mina, nivel 1 a 3).</summary>
        readonly Dictionary<(string, int), Modelo> pueblos = new Dictionary<(string, int), Modelo>();
        /// <summary>Lo que mide en u una unidad de los modelos de pueblo (la del caserío de nivel 1).</summary>
        public static double UnidadDe(ClaseDeCosa clase) => 0.9 * CosasDelMapa.Fijos(clase);
        /// <summary>La de los pueblos y las aldeas (con su factor de lo fijo).</summary>
        public static double UnidadDePueblo => UnidadDe(ClaseDeCosa.Pueblo);

        /// <summary>La huella (u) de un pueblo extractor por su modelo: a la medida de la choza (con `unidad_u`), su `radio_u` tal cual; si no, por
        /// lo fijo, como antes.</summary>
        static double HuellaDePuebloDe(Modelo mp) => double.IsNaN(mp.UnidadU) ? mp.RadioU * CosasDelMapa.Fijos(ClaseDeCosa.Pueblo) : mp.RadioU;
        /// <summary>Los nodos por su id (dónde y de qué tipo), para poner cada pueblo sobre el suyo y teñir su mineral.</summary>
        public Func<string, (double x, double y, string tipo)?> NodoPorId;
        /// <summary>El giro (grados, en Unity) de los nodos que tienen pueblo, por su id: `NodosDelMapa` los dibuja con su variante 1 y ese giro.
        /// `VersionGiros` cambia cuando cambian.</summary>
        public readonly Dictionary<string, float> GirosDeNodos = new Dictionary<string, float>();
        public int VersionGiros;
        /// <summary>El color de cada recurso para el mineral de las minas (el de `NodosDelMapa`).</summary>
        public Func<string, Vector4> ColorDeRecurso;
        /// <summary>El tope del radio de una muralla (en huellas): el aro, el clic y el claro no crecen más.</summary>
        public const double RadioMaximoDeMuralla = 1.12;
        /// <summary>Para las pruebas: el radio (en huellas) medido en la malla de las murallas que van dentro del FBX de su plaza.</summary>
        public static readonly Dictionary<string, double> RadiosMedidos = new Dictionary<string, double>();
        /// <summary>Los tramos de la red de caminos (id y puntos, u), para girar cada plaza con su puerta hacia el suyo (`RumboDelCamino`).</summary>
        public IReadOnlyList<(string id, IReadOnlyList<(double x, double y)> puntos)> Caminos = Array.Empty<(string, IReadOnlyList<(double x, double y)>)>();
        readonly List<Miniatura> miniaturas = new List<Miniatura>();
        /// <summary>«Mejorado»: los modelos por nivel de plaza (sus variantes).</summary>
        readonly Dictionary<int, List<Modelo>> modelos = new Dictionary<int, List<Modelo>>();
        readonly MaterialPropertyBlock propiedadesMiniatura = new MaterialPropertyBlock(), propiedadesHumo = new MaterialPropertyBlock();
        static readonly float[] SinResiduos = new float[LadoDeRejilla * LadoDeRejilla];
        /// <summary>Las plazas, asentadas por vértice: los puntos por lado de su rejilla (el sombreado admite hasta 13).</summary>
        public const int LadoDeRejilla = 13;

        /// <summary>Lo que sube o baja el punto `w` de la escena sobre el plano del sesgo de la miniatura (m): la rejilla, interpolada (como
        /// `Residuo` de `MapaAsentamiento`).</summary>
        static float ResiduoEn(Miniatura mi, Vector3 w)
        {
            if (mi.Residuos == null) return 0;
            int n = LadoDeRejilla;
            float tx = Mathf.Clamp01((w.x - mi.Rejilla.x) / (2 * mi.Rejilla.z) + 0.5f) * (n - 1), tz = Mathf.Clamp01((w.z - mi.Rejilla.y) / (2 * mi.Rejilla.z) + 0.5f) * (n - 1);
            int ix = Math.Min((int)tx, n - 2), iz = Math.Min((int)tz, n - 2);
            float fx = tx - ix, fz = tz - iz;
            int a = iz * n + ix, b = a + n;
            return Mathf.Lerp(Mathf.Lerp(mi.Residuos[a], mi.Residuos[a + 1], fx), Mathf.Lerp(mi.Residuos[b], mi.Residuos[b + 1], fx), fz);
        }

        /// <summary>Las bocanadas del humo (o de la llama) de una batalla: `n` caras cuadradas, cada una con sus cuatro esquinas en el mismo
        /// punto (el sombreado las abre de frente a la cámara): uv0 la esquina (−1 a 1), uv1 (su fase, su tamaño). Por eso los límites, a mano.</summary>
        static Mesh Bocanadas(int n, string nombre)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var uv1 = new List<Vector2>();
            var t = new List<int>();
            var rng = new System.Random(n * 31 + 7);
            for (int i = 0; i < n; i++)
            {
                int b = v.Count;
                float fase = (float)i / n, tam = (float)(0.8 + 0.4 * rng.NextDouble());
                foreach (var (u, w) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    v.Add(Vector3.zero);
                    uv.Add(new Vector2(u, w));
                    uv1.Add(new Vector2(fase, tam));
                }
                t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var m = new Mesh { name = nombre };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetUVs(1, uv1);
            m.SetTriangles(t, 0);
            m.bounds = new Bounds(new Vector3(0, 0.8f, 0), new Vector3(3.2f, 3.2f, 3.2f));
            return m;
        }
        /// <summary>Para las pruebas: la variante de cada nivel (1, 2…; 0 = la de la semilla).</summary>
        public static int VarianteForzada;
        /// <summary>El lado (en radios de la huella) que cubre la máscara del suelo pisado: [−1,25, 1,25] (como `SUELO` en `villa1.py`).</summary>
        const float LadoDelSuelo = 1.25f;
        /// <summary>¿Están los modelos de «Mejorado»?</summary>
        public bool HayModelos => modelos.Count > 0;

        /// <summary>«Mejorado»: dónde ralea la capa baja (la huella de cada miniatura, u); se cambia entero (lo lee el hilo de la vegetación).</summary>
        volatile (double x, double y, double huella)[] ralos = Array.Empty<(double, double, double)>();

        /// <summary>Los claros sin árboles de las maquetas (u), los del prototipo.</summary>
        public readonly List<(double x, double y, double r)> Claros = new List<(double, double, double)>();
        /// <summary>Lo que costó lo último (ms): rehacer las maquetas, asentarlas, mandarlas a dibujar; y cuántas piezas hay.</summary>
        public double MsRehacer, MsAsentar, MsDibujar;
        public int PiezasPuestas;
        /// <summary>Lo que se manda a dibujar: las llamadas (por material y lado de la niebla, en tandas de 1023) y los triángulos.</summary>
        public int Llamadas, Triangulos;

        public MaquetasDelMapa(TerrenoDelMapa terreno)
        {
            this.terreno = terreno;
            var sombreado = Resources.Load<Shader>("MapaMundo/MapaColor");
            encima = new Material(sombreado) { name = "maquetas (encima)", enableInstancing = true };
            encima.SetFloat("_ConNiebla", 0);
            bajoNiebla = new Material(sombreado) { name = "maquetas (bajo la niebla)", enableInstancing = true };
            caja = Caja(new Vector3(0, 0.5f, 0), "caja de maqueta");
            bandera = Caja(new Vector3(0.5f, -0.5f, 0), "bandera de maqueta");
            tienda = Tienda();
            // «Mejorado»: las plazas modeladas (plaza<nivel>_<variante>), las que haya
            string[] niveles = { "cerca", "medio", "lejos" };
            var sombreadoVilla = Resources.Load<Shader>("MapaMundo/MapaAsentamiento");
            var sombreadoParche = Resources.Load<Shader>("MapaMundo/MapaParcheAsentamiento");
            var suelo = terreno.Material;
            for (int nivel = 1; nivel <= 5; nivel++)
                for (int v = 1; ; v++)
                {
                    string nombre = $"plaza{nivel}_{v}";
                    var raiz = Resources.Load<GameObject>("MapaMundo/Asentamientos/" + nombre);
                    if (raiz == null) break;
                    var m = new Modelo();
                    bool completo = true;
                    for (int n = 0; n < 3; n++)
                    {
                        m.Villa[n] = raiz.transform.Find($"{nombre}_{niveles[n]}")?.GetComponent<MeshFilter>()?.sharedMesh;
                        m.Muralla[n] = raiz.transform.Find($"{nombre}_muralla_{niveles[n]}")?.GetComponent<MeshFilter>()?.sharedMesh;
                        if (m.Villa[n] == null) completo = false;
                    }
                    if (!completo) continue;
                    var jsonPlaza = Resources.Load<TextAsset>("MapaMundo/Asentamientos/" + nombre);
                    m.Puerta = Json(jsonPlaza, "puerta", double.NaN);
                    // (a la medida de la choza) escala fija y el radio que ocupa
                    m.UnidadU = Json(jsonPlaza, "unidad_u", double.NaN);
                    m.RadioU = double.IsNaN(m.UnidadU) ? double.NaN : Json(jsonPlaza, "radio_u", double.NaN);
                    m.Estandartes = Anclas(Resources.Load<TextAsset>("MapaMundo/Asentamientos/" + nombre));
                    // la muralla aparte, si la hay (el nivel 1 nunca la tiene; la del 3 va dentro de su FBX)
                    string nm = $"muralla{nivel}_{v}";
                    var rm = nivel >= 2 ? Resources.Load<GameObject>("MapaMundo/Asentamientos/" + nm) : null;
                    if (rm != null)
                    {
                        for (int n = 0; n < 3; n++) m.Muralla[n] = rm.transform.Find($"{nm}_{niveles[n]}")?.GetComponent<MeshFilter>()?.sharedMesh;
                        m.MurallaEncima = new Material(sombreadoVilla) { name = nm + " (encima)" };
                        m.MurallaEncima.SetTexture("_ColorTex", Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nm}_color"));
                        m.MurallaEncima.SetTexture("_Mascara", Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nm}_mascara"));
                        m.MurallaEncima.SetFloat("_Rodilla", nivel >= 3 ? 1 : 0);
                        // (el paño de la malla, si lo trae, no se dibuja: su estandarte va en el ancla)
                        m.MurallaEncima.SetFloat("_SinBandera", 1);
                        m.EstandartesMuralla = Anclas(Resources.Load<TextAsset>("MapaMundo/Asentamientos/" + nm));
                        m.MurallaBajo = new Material(m.MurallaEncima) { name = nm + " (bajo la niebla)" };
                        m.MurallaBajo.SetFloat("_ConNiebla", 1);
                        m.RadioMuralla = Math.Min(RadioMaximoDeMuralla, Json(Resources.Load<TextAsset>("MapaMundo/Asentamientos/" + nm), "radio", 1));
                    }
                    else if (nivel >= 2 && m.Muralla[0] != null)
                    {
                        // la muralla que va dentro del FBX de su plaza (la del nivel 3) no trae json: su radio, el de su malla (en huellas)
                        var b = m.Muralla[0].bounds;
                        RadiosMedidos[nombre] = Math.Max(Math.Max(-b.min.x, b.max.x), Math.Max(-b.min.z, b.max.z));
                        m.RadioMuralla = Math.Min(RadioMaximoDeMuralla, Math.Max(1, RadiosMedidos[nombre]));
                    }
                    if (nivel < 2) for (int n = 0; n < 3; n++) m.Muralla[n] = null;
                    m.Encima = new Material(sombreadoVilla) { name = nombre + " (encima)" };
                    m.Encima.SetTexture("_ColorTex", Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nombre}_color"));
                    m.Encima.SetTexture("_Mascara", Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nombre}_mascara"));
                    // (niveles 3 a 5: el encalado, con las luces comprimidas para que al sol no se queme; el 1 y el 2, como estaban)
                    m.Encima.SetFloat("_Rodilla", nivel >= 3 ? 1 : 0);
                    m.Encima.SetFloat("_SinBandera", 1);
                    m.Bajo = new Material(m.Encima) { name = nombre + " (bajo la niebla)" };
                    m.Bajo.SetFloat("_ConNiebla", 1);
                    var mascaraSuelo = Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nombre}_suelo");
                    if (mascaraSuelo != null && suelo != null)
                    {
                        m.Parche = new Material(sombreadoParche) { name = nombre + " (suelo)" };
                        m.Parche.SetTexture("_SueloColor", suelo.GetTexture("_SueloColor"));
                        m.Parche.SetTexture("_SueloRuido", suelo.GetTexture("_SueloRuido"));
                        m.Parche.SetVector("_Escalas", suelo.GetVector("_Escalas"));
                        m.Parche.SetTexture("_Suelo", mascaraSuelo);
                        // (niveles 4 y 5) su máscara trae también los huertos (G) y las eras (B) del borde
                        m.Parche.SetFloat("_Pintados", nivel >= 4 || !double.IsNaN(m.UnidadU) ? 1 : 0); // (y las rehechas a la medida de la choza: su huerto y su era, pintados)
                        m.ParcheBajo = new Material(m.Parche) { name = nombre + " (suelo, bajo la niebla)" };
                        m.ParcheBajo.SetFloat("_ConNiebla", 1);
                    }
                    if (!modelos.TryGetValue(nivel, out var lista)) modelos[nivel] = lista = new List<Modelo>();
                    lista.Add(m);
                }
            // «Mejorado»: los pueblos extractores (pueblo_<cantera|mina>_<nivel>, de `BronzeAgeArte/mapa/pueblos/hornear.py`)
            foreach (var tipo in new[] { "cantera", "mina" })
                for (int nivel = 1; nivel <= 3; nivel++)
                {
                    string nombre = $"pueblo_{tipo}_{nivel}";
                    var raiz = Resources.Load<GameObject>("MapaMundo/Asentamientos/" + nombre);
                    if (raiz == null) continue;
                    var m = new Modelo();
                    bool completo = true;
                    for (int n = 0; n < 3; n++)
                    {
                        m.Villa[n] = raiz.transform.Find($"{nombre}_{niveles[n]}")?.GetComponent<MeshFilter>()?.sharedMesh;
                        if (m.Villa[n] == null) completo = false;
                    }
                    if (!completo) continue;
                    m.Encima = new Material(sombreadoVilla) { name = nombre + " (encima)" };
                    m.Encima.SetTexture("_ColorTex", Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nombre}_color"));
                    m.Encima.SetTexture("_Mascara", Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nombre}_mascara"));
                    m.Bajo = new Material(m.Encima) { name = nombre + " (bajo la niebla)" };
                    m.Bajo.SetFloat("_ConNiebla", 1);
                    var mascaraSuelo = Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nombre}_suelo");
                    if (mascaraSuelo != null && suelo != null)
                    {
                        m.Parche = new Material(sombreadoParche) { name = nombre + " (suelo)" };
                        m.Parche.SetTexture("_SueloColor", suelo.GetTexture("_SueloColor"));
                        m.Parche.SetTexture("_SueloRuido", suelo.GetTexture("_SueloRuido"));
                        m.Parche.SetVector("_Escalas", suelo.GetVector("_Escalas"));
                        m.Parche.SetTexture("_Suelo", mascaraSuelo);
                        m.ParcheBajo = new Material(m.Parche) { name = nombre + " (suelo, bajo la niebla)" };
                        m.ParcheBajo.SetFloat("_ConNiebla", 1);
                    }
                    // (el modelo: el frente de trabajo a −y de Blender; la máscara del suelo cubre [−2,15, 2,15] unidades)
                    m.Puerta = -Math.PI / 2;
                    m.LadoSuelo = 2.15;
                    m.RadioU = Json(Resources.Load<TextAsset>("MapaMundo/Asentamientos/" + nombre), "radio_u", 1.0);
                    // (a la medida de la choza: escala fija, 1:1 con su nodo; sin ella, la del caserío por lo fijo, como antes)
                    m.UnidadU = Json(Resources.Load<TextAsset>("MapaMundo/Asentamientos/" + nombre), "unidad_u", double.NaN);
                    pueblos[(tipo, nivel)] = m;
                }
            // «Mejorado»: los núcleos de aldea (nucleo_<labor|ganado>_<nivel>) y lo construido de cada uno (del json, en el marco de la máscara:
            // x de Blender vuelta, como la máscara del suelo)
            foreach (var clase in new[] { "labor", "ganado" })
                for (int nivel = 1; nivel <= 3; nivel++)
                {
                    string nombre = $"nucleo_{clase}_{nivel}";
                    var m = CargarModelo(nombre, sombreadoVilla, sombreadoParche, suelo, niveles);
                    if (m == null) continue;
                    m.LadoSuelo = 2.15;
                    var json = Resources.Load<TextAsset>("MapaMundo/Asentamientos/" + nombre);
                    m.RadioU = Json(json, "radio_u", 0.5);
                    m.Ocupado = json == null ? new (double, double, double)[0] : Newtonsoft.Json.Linq.JObject.Parse(json.text)["ocupado"]
                        .Select(o => (-(double)o[0], (double)o[1], (double)o[2])).ToArray();
                    nucleos[(clase, nivel)] = m;
                }
            var raizPiezas = Resources.Load<GameObject>("MapaMundo/Asentamientos/piezas");
            if (raizPiezas != null)
            {
                for (int k = 0; k < NombresDePiezas.Length; k++)
                    piezas[k] = raizPiezas.transform.Find($"pieza_{NombresDePiezas[k]}_cerca")?.GetComponent<MeshFilter>()?.sharedMesh;
                materialPiezas = new Material(sombreadoVilla) { name = "piezas de los campos", enableInstancing = true };
                materialPiezas.SetTexture("_ColorTex", Resources.Load<Texture2D>("MapaMundo/Asentamientos/piezas_color"));
                materialPiezas.SetTexture("_Mascara", Resources.Load<Texture2D>("MapaMundo/Asentamientos/piezas_mascara"));
                materialPiezas.SetColor("_Color", Color.white);
            }
            // «Mejorado», parte 5 y lo menudo (`BronzeAgeArte/mapa/avanzadas/hornear5.py`): las avanzadas por etapa y estado, los campamentos,
            // las obras, los alijos y la batalla, cada uno con su json (radio, lado del suelo, escala del mapa y sus sitios); y sus piezas sueltas
            foreach (var nombre in NombresMenudos)
            {
                var m = CargarModelo(nombre, sombreadoVilla, sombreadoParche, suelo, niveles);
                if (m == null) continue;
                LeerJsonMenudo(m, nombre);
                menudos[nombre] = m;
            }
            // (los mercenarios: la misma malla con las otras dos telas)
            foreach (var tela in new[] { "rayado", "rubia" })
            {
                var m = CargarModelo("mercenarios", sombreadoVilla, sombreadoParche, suelo, niveles, tela);
                if (m == null) continue;
                LeerJsonMenudo(m, "mercenarios");
                menudos["mercenarios_" + tela] = m;
            }
            CargarPiezas("obra_kit", PiezasDelKit, sombreadoVilla);
            CargarPiezas("mercenarios_militar", new[] { "barracon", "tiro", "cuadras" }, sombreadoVilla);
            CargarPiezas("alijo_mercancia", new[] { "trigo", "madera", "piedra", "oro" }, sombreadoVilla);
            var sombreadoHumo = Resources.Load<Shader>("MapaMundo/MapaHumo");
            if (sombreadoHumo != null)
            {
                materialHumo = new Material(sombreadoHumo) { name = "humo de las batallas" };
                // (el humo, recortado y con profundidad, sin mezcla: las bocanadas se tapan bien entre sí; la llama y el resplandor, translúcidos)
                materialHumo.SetFloat("_Origen", (float)BlendMode.One);
                materialHumo.SetFloat("_Destino", (float)BlendMode.Zero);
                materialHumo.SetFloat("_Profundidad", 1);
                materialHumo.renderQueue = 2460;
                materialLlama = new Material(sombreadoHumo) { name = "llama de las batallas" };
                materialLlama.SetFloat("_Llama", 1);
                materialResplandor = new Material(sombreadoHumo) { name = "resplandor de las batallas" };
                materialResplandor.SetFloat("_Llama", 2);
                // (el humo con cuerpo: 22 bocanadas; la llama, 7 lenguas; el resplandor, un disco)
                mallaHumo = Bocanadas(22, "humo de una batalla");
                mallaLlama = Bocanadas(7, "llama de una batalla");
                mallaResplandor = Bocanadas(1, "resplandor de una batalla");
            }
            // (los pueblos, los núcleos y las piezas, después de los suelos pisados y los campos (cola 2454-2455), que van 1 m por encima del
            // suelo pintado y sin escribir profundidad: así no les tapan lo bajo, las cercas, los corrales ni los animales; las plazas, como estaban)
            foreach (var mm in pueblos.Values.Concat(nucleos.Values).Concat(menudos.Values).Concat(piezasMenudas.Values)) { mm.Encima.renderQueue = 2456; mm.Bajo.renderQueue = 2456; }
            if (materialPiezas != null) materialPiezas.renderQueue = 2456;
            // (las plazas a la medida de la choza, también: si no, su suelo pisado les tapa lo bajo)
            foreach (var mm in modelos.Values.SelectMany(x => x).Where(x => !double.IsNaN(x.UnidadU)))
                foreach (var mat in new[] { mm.Encima, mm.Bajo, mm.MurallaEncima, mm.MurallaBajo })
                    if (mat != null) mat.renderQueue = 2456;
            var sombreadoCampos = Resources.Load<Shader>("MapaMundo/MapaParcelas");
            if (sombreadoCampos != null && suelo != null)
            {
                materialCampos = new Material(sombreadoCampos) { name = "campos de las aldeas" };
                materialCampos.SetTexture("_SueloColor", suelo.GetTexture("_SueloColor"));
                materialCampos.SetTexture("_SueloRuido", suelo.GetTexture("_SueloRuido"));
                materialCampos.SetVector("_Escalas", suelo.GetVector("_Escalas"));
                materialCampos.SetFloat("_Celda", (float)ParcelasDeAldea.Celda);
                materialCampos.SetFloat("_Maximo", ParcelasDeAldea.Maximo);
                // (maduro, rastrojo, verde joven, arado; `aldea.py`; el sésamo, solo verde o en barbecho: la arena de sus hileras)
                var hex = new[] { "#cdad62", "#d6bf86", "#9fae58", "#8f7556", "#dccb86", "#cdb98a", "#a9bf6a", "#8f7556", "#b27a52", "#b98459", "#ad7a50", "#9a6e4c",
                                  "#bba77c", "#c2ad82", "#a9a274", "#977b5a", "#d9c79a", "#d9c79a", "#d9c79a", "#8f7556", "#a9ad5f", "#b2b268", "#a0a85a", "#9fa05e" };
                materialCampos.SetVectorArray("_Paleta", hex.Select(h => { ColorUtility.TryParseHtmlString(h, out var c); return (Vector4)c.linear; }).ToList());
                materialCamposBajo = new Material(materialCampos) { name = "campos de las aldeas (bajo la niebla)" };
                materialCamposBajo.SetFloat("_ConNiebla", 1);
                materialCamposBajo.SetVectorArray("_Paleta", hex.Select(h => { ColorUtility.TryParseHtmlString(h, out var c); return (Vector4)c.linear; }).ToList());
            }
            CosasDelMapa.HuellaDePueblo = (tipoExtractor, nivel) => ModeloDePueblo(tipoExtractor, nivel) is Modelo mp_ ? HuellaDePuebloDe(mp_) : double.NaN;
            CosasDelMapa.HuellaDeAvanzada = etapa => HuellaOperativa((int)etapa);
            CosasDelMapa.HuellaDeMenudo = clase => HuellaDe(clase, clase == ClaseDeCosa.Mercenarios ? new[] { "mercenarios" }
                : clase == ClaseDeCosa.Bandidos ? new[] { "bandidos_1_1", "bandidos_1_2", "bandidos_1_3", "bandidos_2_1", "bandidos_2_2", "bandidos_2_3" }
                : clase == ClaseDeCosa.Alijo ? new[] { "alijo_1", "alijo_2" } : new string[0]);
            CosasDelMapa.HuellaDeModelo = (nivel, x, y) => modelos.ContainsKey((int)nivel) ? ModeloDe(nivel, P.Semilla(x, y)).RadioU : double.NaN;
            CosasDelMapa.FactorDeMuralla = (nivel, x, y) =>
            {
                if (nivel < 2 || !modelos.ContainsKey((int)nivel)) return 1;
                var mo = ModeloDe(nivel, P.Semilla(x, y));
                return double.IsNaN(mo.UnidadU) || double.IsNaN(mo.RadioU) ? mo.RadioMuralla : Math.Max(1, mo.RadioMuralla * mo.UnidadU / mo.RadioU);
            };
            CosasDelMapa.HuellaMayorDePlaza = (nivel, muralla) =>
            {
                if (!modelos.TryGetValue((int)nivel, out var lista)) return double.NaN;
                double r = double.NaN;
                foreach (var mo in lista)
                {
                    if (double.IsNaN(mo.RadioU)) return double.NaN;
                    double f = !muralla || nivel < 2 ? 1 : double.IsNaN(mo.UnidadU) ? mo.RadioMuralla : Math.Max(1, mo.RadioMuralla * mo.UnidadU / mo.RadioU);
                    r = double.IsNaN(r) ? mo.RadioU * f : Math.Max(r, mo.RadioU * f);
                }
                return r;
            };
        }

        /// <summary>El modelo de un pueblo extractor (cantera, o mina para los demás tipos) de su nivel (1 a 3), si lo hay.</summary>
        Modelo ModeloDePueblo(string tipoExtractor, uint nivel, bool ganado = false)
        {
            if (Estilo != EstiloDeAsentamientos.Mejorado) return null;
            int n = (int)Math.Max(1, Math.Min(3, nivel == 0 ? 1 : nivel));
            if (tipoExtractor == "aldeaAgricola") return nucleos.TryGetValue((ganado ? "ganado" : "labor", n), out var a) ? a : null;
            return pueblos.TryGetValue((tipoExtractor == "cantera" ? "cantera" : "mina", n), out var m) ? m : null;
        }

        /// <summary>Lo menudo: su json (el radio en u, el lado del suelo, la escala del mapa y sus sitios).</summary>
        static void LeerJsonMenudo(Modelo m, string nombre)
        {
            var t = Resources.Load<TextAsset>("MapaMundo/Asentamientos/" + nombre);
            m.RadioU = Json(t, "radio_u", 0.5);
            m.LadoSuelo = Json(t, "lado_suelo", 1);
            m.EscalaMapa = Json(t, "escala_mapa", 1);
            m.Puerta = -Math.PI / 2; // (la puerta, a −y de Blender)
            m.EscalaFija = t != null && t.text.Contains("\"escala_fija\": true");
            if (m.EscalaFija) m.UnidadU = Json(t, "unidad_u", double.NaN);
            if (t == null) return;
            var j = Newtonsoft.Json.Linq.JObject.Parse(t.text);
            double[] Lista(string k) => j[k]?.Select(v => (double)v).ToArray();
            m.SolarMilitar = Lista("solar_militar");
            m.SitioMercancia = Lista("sitio_mercancia");
            m.HumoEn = Lista("humo_en");
            m.LlamaEn = Lista("llama_en");
        }

        /// <summary>Lo menudo: las piezas sueltas de un atlas (`<atlas>_<pieza>_cerca` y `_medio`), con su material (el del atlas).</summary>
        void CargarPiezas(string atlas, string[] nombres, Shader sombreadoVilla)
        {
            var raiz = Resources.Load<GameObject>("MapaMundo/Asentamientos/" + atlas);
            if (raiz == null) return;
            var encima = new Material(sombreadoVilla) { name = atlas + " (encima)" };
            encima.SetTexture("_ColorTex", Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{atlas}_color"));
            encima.SetTexture("_Mascara", Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{atlas}_mascara"));
            var bajo = new Material(encima) { name = atlas + " (bajo la niebla)" };
            bajo.SetFloat("_ConNiebla", 1);
            foreach (var n in nombres)
            {
                var cerca = raiz.transform.Find($"{atlas}_{n}_cerca")?.GetComponent<MeshFilter>()?.sharedMesh;
                var medio = raiz.transform.Find($"{atlas}_{n}_medio")?.GetComponent<MeshFilter>()?.sharedMesh;
                if (cerca == null) continue;
                var m = new Modelo { Encima = encima, Bajo = bajo };
                m.Villa[0] = cerca;
                m.Villa[1] = m.Villa[2] = medio ?? cerca;
                piezasMenudas[atlas + "/" + n] = m;
            }
        }

        Modelo Menudo(string nombre) => nombre != null && Estilo == EstiloDeAsentamientos.Mejorado && menudos.TryGetValue(nombre, out var m) ? m : null;
        Modelo PiezaMenuda(string atlas, string pieza) => piezasMenudas.TryGetValue(atlas + "/" + pieza, out var m) ? m : null;

        /// <summary>La huella (u) que cubre a todos esos modelos: el mayor radio; a escala fija (`escala_fija`), su `radio_u`; si no, por su escala
        /// del mapa y lo fijo de su clase (NaN si no hay ninguno).</summary>
        double HuellaDe(ClaseDeCosa clase, IEnumerable<string> nombres)
        {
            double r = double.NaN;
            foreach (var n in nombres)
                if (menudos.TryGetValue(n, out var m))
                {
                    double h = m.EscalaFija ? m.RadioU : m.RadioU * m.EscalaMapa * CosasDelMapa.Fijos(clase);
                    r = double.IsNaN(r) ? h : Math.Max(r, h);
                }
            return r;
        }

        /// <summary>La huella (u) del aro y el clic de una avanzada de esa etapa: la de su modelo operativo (las dos ciudadelas, la mayor), la misma
        /// en obras y en ruina, que no salte; a la medida de la choza, su `radio_u`; si no, como antes (por su escala del mapa y lo fijo).</summary>
        double HuellaOperativa(int etapa)
        {
            double r = double.NaN;
            foreach (var n in etapa <= 1 ? new[] { "atalaya" } : etapa == 2 ? new[] { "fuerte" } : new[] { "ciudadela_1", "ciudadela_2" })
                if (menudos.TryGetValue(n, out var m))
                {
                    double h = m.EscalaFija ? m.RadioU : m.RadioU * m.EscalaMapa * CosasDelMapa.Fijos(ClaseDeCosa.Avanzada);
                    r = double.IsNaN(r) ? h : Math.Max(r, h);
                }
            return r;
        }

        /// <summary>Los modelos de cada etapa de avanzada (todos sus estados: entera, en ruina y en obras, y las dos ciudadelas).</summary>
        public static string[] ModelosDeEtapa(int etapa) => etapa <= 1 ? new[] { "atalaya" }
            : etapa == 2 ? new[] { "fuerte", "fuerte_ruina", "fuerte_obras" }
            : new[] { "ciudadela_1", "ciudadela_1_ruina", "ciudadela_1_obras", "ciudadela_2", "ciudadela_2_ruina", "ciudadela_2_obras" };

        // ---------- las reglas de lo menudo (puras: las prueban `MenudoDelMapaTests`) ----------

        /// <summary>La ciudadela de un sitio (1 o 2), por su semilla: la misma entera, en obras y en ruina.</summary>
        public static int VarianteDeCiudadela(double semilla) => VarianteCiudadelaForzada > 0 ? VarianteCiudadelaForzada : P.Azar(semilla, 6, 1) < 0.5 ? 1 : 2;
        /// <summary>Para las escenas de banco: la ciudadela que se pone (1 o 2); 0, la de la semilla.</summary>
        public static int VarianteCiudadelaForzada;

        /// <summary>El modelo de una avanzada de esa etapa (1 atalaya, 2 fuerte, 3 ciudadela): en ruina, el de su ruina (la atalaya no tiene: null);
        /// con su mejora en marcha, el de la etapa siguiente en obras (la ciudadela no tiene siguiente: la suya).</summary>
        public static string ModeloDeAvanzada(uint etapa, bool ruinas, bool mejoraEnMarcha, double semilla)
        {
            int e = (int)Math.Max(1, Math.Min(3, etapa));
            string ciudadela = "ciudadela_" + VarianteDeCiudadela(semilla);
            if (ruinas) return e == 1 ? null : e == 2 ? "fuerte_ruina" : ciudadela + "_ruina";
            if (mejoraEnMarcha && e < 3) return e == 1 ? "fuerte_obras" : ciudadela + "_obras";
            return e == 1 ? "atalaya" : e == 2 ? "fuerte" : ciudadela;
        }

        /// <summary>La tela de un campamento de mercenarios por su Origen (0 crudo, 1 rayado, 2 rubia; los demás, en vuelta).</summary>
        public static string ModeloDeMercenarios(uint origen) => (origen % 3) switch { 0 => "mercenarios", 1 => "mercenarios_rayado", _ => "mercenarios_rubia" };

        /// <summary>El edificio militar de un campamento por sus Edificios (barracón, galería de tiro o caballerizas; si no se reconoce, barracón).</summary>
        public static string PiezaMilitar(IEnumerable<string> edificios)
        {
            foreach (var e in edificios ?? Enumerable.Empty<string>())
                switch (e)
                {
                    case "barracon": return "barracon";
                    case "galeriaDeTiro": return "tiro";
                    case "caballerizas": return "cuadras";
                }
            return "barracon";
        }

        /// <summary>El campamento de bandidos: el de la peña (7b) junto a una veta, el del claro (7a) en un bosque, y si no, por la semilla; de su
        /// nivel (1 a 3).</summary>
        public static string ModeloDeBandidos(string vetaId, string bosqueId, uint nivel, double semilla)
        {
            int v = !string.IsNullOrEmpty(vetaId) ? 2 : !string.IsNullOrEmpty(bosqueId) ? 1 : (P.Azar(semilla, 6, 2) < 0.5 ? 1 : 2);
            return $"bandidos_{v}_{Math.Max(1, Math.Min(3, nivel))}";
        }

        /// <summary>El alijo (1 o 2) por su semilla, y la pieza de su mercancía por el recurso (trigo, madera, piedra u oro; si no, trigo).</summary>
        public static string ModeloDeAlijo(double semilla) => AlijoForzado > 0 ? $"alijo_{AlijoForzado}" : P.Azar(semilla, 6, 3) < 0.5 ? "alijo_1" : "alijo_2";
        /// <summary>(Escenas de banco) el alijo que se pone (1 bajo la piel, 2 el hoyo); 0: el de su semilla.</summary>
        public static int AlijoForzado;
        public static string PiezaDeMercancia(string recurso) => recurso == "madera" || recurso == "piedra" || recurso == "oro" ? recurso : "trigo";

        /// <summary>La obra genérica (avanzada nueva, pueblo o aldea): el solar marcado si está pendiente; a media altura si ya arrancó.</summary>
        public static string ModeloDeObra(bool arrancada) => arrancada ? "obra_mitad" : "obra_estacas";

        /// <summary>El radio (unidades del modelo) de la cara de fuera de los muros de cada avanzada: el kit de obra se pone por fuera.</summary>
        public static double RadioDeMuro(string modelo) => modelo == null ? 0.3 : modelo.StartsWith("atalaya") ? 0.27 : modelo.StartsWith("fuerte") ? 0.38
            : modelo.StartsWith("ciudadela_2") ? 0.55 : 0.57;  // (la 2, rehecha al 83 %: la falda del podio llega a 0,55)

        /// <summary>Dónde va el kit de obra alrededor de una avanzada (unidades del modelo, x, y de Blender, y el giro): en la reparación de una
        /// ruina, todo (dos tramos de andamio contra el muro, la cabria, los montones de adobes y sillares y el sombrajo); en una mejora pendiente
        /// de materiales, solo los dos montones al borde. Por la semilla, sin pisar los muros (todo por fuera de `radioMuro`) ni la puerta (−y).</summary>
        public static List<(string pieza, double x, double y, double giro)> SitiosDelKit(double radioMuro, double semilla, bool completo)
        {
            var l = new List<(string, double, double, double)>();
            double a0 = P.Azar(semilla, 7, 1) * Math.PI * 2;
            const double Puerta = -Math.PI / 2, Hueco = 0.75;
            double Fuera(double a)
            {
                // (fuera del cono de la puerta)
                double d = Math.IEEERemainder(a - Puerta, Math.PI * 2);
                return Math.Abs(d) < Hueco ? Puerta + Math.Sign(d == 0 ? 1 : d) * Hueco : a;
            }
            void Poner(string pieza, double a, double r, double giro) { a = Fuera(a); l.Add((pieza, r * Math.Cos(a), r * Math.Sin(a), giro + a)); }
            if (completo)
            {
                // (el andamio: a lo largo de x, sus postes hacia +y; se pone con +y hacia fuera, contra la cara del muro)
                Poner("andamio", a0, radioMuro + 0.005, -Math.PI / 2);
                Poner("andamio", a0 + 0.42, radioMuro + 0.005, -Math.PI / 2);
                Poner("cabria", a0 + 1.25, radioMuro + 0.17, 0);
                Poner("sombrajo", a0 + 1.85, radioMuro + 0.19, 0.3);
            }
            Poner("adobes", a0 + 2.5, radioMuro + 0.14, 0.4);
            Poner("sillares", a0 + 3.1, radioMuro + 0.14, 1.2);
            return l;
        }

        /// <summary>Un modelo de pueblo o de núcleo: sus tres niveles de detalle, sus materiales (encima y bajo la niebla) y su suelo pisado.
        /// `tela`: otra textura de color de la misma malla (`<nombre>_color_<tela>`, los mercenarios).</summary>
        static Modelo CargarModelo(string nombre, Shader sombreadoVilla, Shader sombreadoParche, Material suelo, string[] niveles, string tela = null)
        {
            var raiz = Resources.Load<GameObject>("MapaMundo/Asentamientos/" + nombre);
            if (raiz == null) return null;
            var m = new Modelo();
            for (int n = 0; n < 3; n++)
            {
                m.Villa[n] = raiz.transform.Find($"{nombre}_{niveles[n]}")?.GetComponent<MeshFilter>()?.sharedMesh;
                if (m.Villa[n] == null) return null;
            }
            m.Encima = new Material(sombreadoVilla) { name = nombre + (tela != null ? " " + tela : "") + " (encima)" };
            m.Encima.SetTexture("_ColorTex", Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nombre}_color" + (tela != null ? "_" + tela : "")));
            m.Encima.SetTexture("_Mascara", Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nombre}_mascara"));
            m.Bajo = new Material(m.Encima) { name = nombre + " (bajo la niebla)" };
            m.Bajo.SetFloat("_ConNiebla", 1);
            var mascaraSuelo = Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nombre}_suelo");
            if (mascaraSuelo != null && suelo != null)
            {
                m.Parche = new Material(sombreadoParche) { name = nombre + " (suelo)" };
                m.Parche.SetTexture("_SueloColor", suelo.GetTexture("_SueloColor"));
                m.Parche.SetTexture("_SueloRuido", suelo.GetTexture("_SueloRuido"));
                m.Parche.SetVector("_Escalas", suelo.GetVector("_Escalas"));
                m.Parche.SetTexture("_Suelo", mascaraSuelo);
                m.ParcheBajo = new Material(m.Parche) { name = nombre + " (suelo, bajo la niebla)" };
                m.ParcheBajo.SetFloat("_ConNiebla", 1);
            }
            return m;
        }

        /// <summary>Un número de un json plano ({"radio": 1.1, "puerta": -1.4}); `siNo` si no está.</summary>
        static double Json(TextAsset t, string clave, double siNo)
        {
            if (t == null) return siNo;
            var m = System.Text.RegularExpressions.Regex.Match(t.text, "\"" + clave + "\"\\s*:\\s*(-?[0-9.eE+-]+)");
            return m.Success ? double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : siNo;
        }

        /// <summary>El rumbo (rad, en el mapa: atan2(y, x)) del camino que sale de la plaza en (x, y) de huella `r`: de los tramos que empiezan o
        /// acaban dentro de ella, el de menor id (`tramo-N`, por su número), hacia su primer punto fuera; NaN si no sale ninguno.</summary>
        public static double RumboDelCamino(double x, double y, double r, IEnumerable<(string id, IReadOnlyList<(double x, double y)> puntos)> caminos)
        {
            bool Dentro((double x, double y) q) => (q.x - x) * (q.x - x) + (q.y - y) * (q.y - y) < r * r;
            long Numero(string id) => long.TryParse(id?.Substring(id.LastIndexOf('-') + 1), out var n) ? n : long.MaxValue;
            foreach (var (_, p) in caminos.Where(c => c.puntos.Count >= 2).OrderBy(c => Numero(c.id)).ThenBy(c => c.id, StringComparer.Ordinal))
            {
                var recorrido = Dentro(p[0]) ? p : Dentro(p[p.Count - 1]) ? p.Reverse().ToList() : null;
                if (recorrido == null) continue;
                var q = recorrido.FirstOrDefault(c => !Dentro(c));
                if (Dentro(q)) q = recorrido[recorrido.Count - 1];
                if (q.x == x && q.y == y) continue;
                return Math.Atan2(q.y - y, q.x - x);
            }
            return double.NaN;
        }

        /// <summary>¿Se pinta esta plaza con la miniatura (y no con las cajas del prototipo)?</summary>
        bool ConMiniatura(double nivel) => Estilo == EstiloDeAsentamientos.Mejorado && modelos.ContainsKey((int)nivel);

        /// <summary>El radio de la huella de una miniatura (u): la de la plaza (`CosasDelMapa.HuellaDePlaza`, con su mínimo en «Mejorado»).</summary>
        public static double Huella(double rc, double nivel) => CosasDelMapa.HuellaDePlaza(rc, nivel);

        /// <summary>«Mejorado»: cuánta capa baja queda en (x, y) (de 0 a 1) junto a las miniaturas: nada hasta la huella y media (el claro, terreno
        /// pastado), un tercio y más baja hasta la huella y media más un radio, y entera desde ahí. Desde cualquier hilo.</summary>
        public double Ralo(double x, double y)
        {
            double r = 1;
            foreach (var (mx, my, h) in ralos)
            {
                double dx = x - mx, dy = y - my, fuera = 2.5 * h + 0.2;
                if (Math.Abs(dx) > fuera || Math.Abs(dy) > fuera) continue;
                double d = Math.Sqrt(dx * dx + dy * dy);
                r = Math.Min(r, d < 1.5 * h ? 0 : d < 2.5 * h ? 0.33 : d < fuera ? 0.33 + 0.67 * (d - 2.5 * h) / 0.2 : 1);
            }
            return r;
        }

        /// <summary>El modelo de una plaza de ese nivel: la variante, por su semilla (o la forzada).</summary>
        Modelo ModeloDe(double nivel, double semilla)
        {
            var lista = modelos[(int)nivel];
            return lista[VarianteForzada > 0 ? (VarianteForzada - 1) % lista.Count : VarianteDe(semilla, lista.Count)];
        }

        /// <summary>La variante (de 0 a `cuantas` − 1) de una plaza por su semilla: siempre la misma para el mismo sitio.</summary>
        public static int VarianteDe(double semilla, int cuantas) => (int)(P.Azar(semilla, 3, 4) * cuantas) % cuantas;

        /// <summary>El nivel de detalle (0 cerca, 1 medio, 2 lejos) por el ancho en pantalla (px).</summary>
        public static int NivelDeDetalle(float ancho) => ancho >= AnchoCerca ? 0 : ancho >= AnchoMedio ? 1 : 2;

        /// <summary>Lo que se manda a dibujar de una miniatura: la malla, su material, su matriz y el color de su Facción (y si es la muralla).</summary>
        public struct Encargo
        {
            public Vector4 Rejilla;
            public float[] Residuos;
            public Mesh Malla;
            public Material Material;
            public Matrix4x4 M;
            public Vector4 Faccion, Recurso;
            public bool EsMuralla, EsSuelo;
            public int Nivel;
        }

        /// <summary>Lo que se manda a dibujar de las miniaturas con la cámara `cam`: por cada una, la villa a su nivel de detalle, la muralla si la
        /// tiene y su suelo pisado (también para las pruebas).</summary>
        public List<Encargo> Encargos(Camera cam)
        {
            var l = new List<Encargo>();
            foreach (var mi in miniaturas)
            {
                int nivel = NivelDeDetalle(AnchoEnPantalla(mi, cam));
                var mo = mi.Modelo;
                var mat = mi.BajoNiebla ? mo.Bajo : mo.Encima;
                // (en espejo: la copia espejada de la malla con la matriz sin espejo; `mi.M` lleva el espejo, para el suelo pisado)
                l.Add(new Encargo { Malla = mi.Espejo ? Espejada(mo.Villa[nivel]) : mo.Villa[nivel], Material = mat, M = mi.Espejo ? mi.M * Matrix4x4.Scale(new Vector3(-1, 1, 1)) : mi.M,
                    Faccion = mi.Faccion, Recurso = mi.Recurso, Nivel = nivel, Rejilla = mi.Rejilla, Residuos = mi.Residuos });
                if (mi.Muralla && mo.Muralla[nivel] != null)
                    l.Add(new Encargo { Malla = mo.Muralla[nivel], Material = (mi.BajoNiebla ? mo.MurallaBajo : mo.MurallaEncima) ?? mat, M = mi.MMuralla, Faccion = mi.Faccion, EsMuralla = true, Nivel = nivel,
                        Rejilla = mi.Rejilla, Residuos = mi.Residuos });
                if (mi.MallaCampos != null && materialCampos != null)
                    l.Add(new Encargo { Malla = mi.MallaCampos, Material = mi.BajoNiebla ? materialCamposBajo : materialCampos, M = Matrix4x4.identity, Faccion = mi.Faccion, EsSuelo = true, Nivel = nivel });
                if (mi.Parche != null)
                    l.Add(new Encargo { Malla = mi.Parche, Material = mi.BajoNiebla ? mo.ParcheBajo : mo.Parche, M = Matrix4x4.identity, Faccion = mi.Faccion, EsSuelo = true, Nivel = nivel });
            }
            return l;
        }

        /// <summary>Una caja de lado 1, de caras planas y blancas (el color va por instancia), desplazada `centro` (la de las piezas, con la base en 0;
        /// la de la bandera, con el asta en x = 0 y colgando).</summary>
        static Mesh Caja(Vector3 centro, string nombre)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var m = UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            UnityEngine.Object.Destroy(go);
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++) v[i] += centro;
            m.vertices = v;
            m.colors = Enumerable.Repeat(Color.white, v.Length).ToArray();
            m.RecalculateBounds();
            m.name = nombre;
            return m;
        }

        /// <summary>La tienda: el `ConeGeometry(0.5, 1, 4)` de three con la base en 0 (las esquinas en sen θ, cos θ, con z cambiada de signo al
        /// pasar al marco de Unity), de caras planas.</summary>
        static Mesh Tienda()
        {
            var esq = Enumerable.Range(0, 4).Select(i => { double t = i * Math.PI / 2; return new Vector3((float)(Math.Sin(t) * 0.5), 0, (float)(-Math.Cos(t) * 0.5)); }).ToArray();
            var cima = new Vector3(0, 1, 0);
            var v = new List<Vector3>();
            for (int i = 0; i < 4; i++)
            {
                var a = esq[i];
                var b = esq[(i + 1) % 4];
                // la cara, hacia fuera en el sentido de las caras de Unity
                var n = Vector3.Cross(b - a, cima - a);
                if (Vector3.Dot(n, (a + b + cima) / 3) < 0) v.AddRange(new[] { a, b, cima }); else v.AddRange(new[] { a, cima, b });
            }
            // la base
            v.AddRange(new[] { esq[0], esq[1], esq[2], esq[0], esq[2], esq[3] });
            for (int k = 12; k < 18; k += 3)
                if (Vector3.Cross(v[k + 1] - v[k], v[k + 2] - v[k]).y > 0) (v[k + 1], v[k + 2]) = (v[k + 2], v[k + 1]);
            var m = new Mesh { name = "tienda de maqueta", vertices = v.ToArray() };
            m.SetTriangles(Enumerable.Range(0, v.Count).ToArray(), 0);
            m.colors = Enumerable.Repeat(Color.white, v.Count).ToArray();
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Vector4 Lineal(uint rgb) => (Vector4)new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255).ToColor().linear;
        static string Hex(uint rgb) => "#" + rgb.ToString("x6");
        static uint DeHex(string h) => Convert.ToUInt32(h.TrimStart('#'), 16);

        /// <summary>Rehace las maquetas si cambió lo que hay y las asienta si cambió el suelo. `radioZonaInicial` (u): el de las reglas.</summary>
        public void Actualizar(List<CosaDelMapa> cosas, string heroeId, double radioZonaInicial)
        {
            if (Estilo == EstiloDeAsentamientos.Cajas) { if (lotes.Count > 0) { lotes.Clear(); Claros.Clear(); clave = ""; } return; }
            var sb = new StringBuilder();
            foreach (var c in cosas)
                if (Pinta(c.Clase)) sb.Append(c.Clase).Append(c.Id).Append(':').Append(c.X.ToString("0.00")).Append(',').Append(c.Y.ToString("0.00")).Append(':')
                    .Append(c.Color).Append(c.BajoNiebla).Append(c.Detalle).Append(c.Entidad?.Asentamiento?.TieneMuralla).Append(c.Entidad?.Obra?.HasTerminaEnMs)
                    // (lo menudo: la etapa y el estado de la avanzada, la tela y el edificio militar del campamento, y el recurso del alijo)
                    .Append(c.Entidad?.Avanzada?.Etapa).Append(c.Entidad?.Avanzada?.Estado).Append(c.Entidad?.CampamentoMercenarios?.Origen)
                    .Append(c.Entidad?.CampamentoMercenarios != null ? string.Join(",", c.Entidad.CampamentoMercenarios.Edificios) : null).Append(c.Entidad?.Alijo?.Recurso)
                    // (el tipo y el nivel del pueblo o la aldea: cambian su maqueta sin moverla)
                    .Append(c.Entidad?.Pueblo?.TipoExtractor).Append(c.Entidad?.Pueblo?.Cultivo).Append(c.Entidad?.Pueblo?.RebanoEspecie).Append(c.Entidad?.Pueblo?.NivelAldea).Append('|');
            foreach (var (id, p) in Caminos) sb.Append(id).Append(p.Count).Append(p.Count > 0 ? p[0].x + p[p.Count - 1].y : 0).Append(';');
            var nueva = Estilo + CosasDelMapa.ClaveDeFactores + "|v" + VarianteForzada + "|c" + VarianteCiudadelaForzada + "|a" + AlijoForzado + sb.ToString();
            if (nueva != clave)
            {
                var reloj = Stopwatch.StartNew();
                clave = nueva;
                Rehacer(cosas, heroeId, radioZonaInicial);
                version = -1;
                MsRehacer = reloj.Elapsed.TotalMilliseconds;
            }
            if (terreno.Version != version)
            {
                var reloj = Stopwatch.StartNew();
                version = terreno.Version;
                Asentar();
                MsAsentar = reloj.Elapsed.TotalMilliseconds;
            }
        }

        void Rehacer(List<CosaDelMapa> cosas, string heroeId, double radioZonaInicial)
        {
            var arriba = new P.Piezas();
            var abajo = new P.Piezas();
            Claros.Clear();
            var giros = new Dictionary<string, float>();
            var aldeas = new List<(Miniatura mi, string id)>();
            foreach (var mi in miniaturas) { if (mi.Parche != null) UnityEngine.Object.Destroy(mi.Parche); if (mi.MallaCampos != null) UnityEngine.Object.Destroy(mi.MallaCampos); }
            miniaturas.Clear();
            // (lo menudo: la obra de cada avanzada que se ve, su mejora o su reparación, para pintarla con ella)
            var avanzadasVistas = new HashSet<string>(cosas.Where(k => k.Clase == ClaseDeCosa.Avanzada).Select(k => k.Id));
            var obrasPorAvanzada = new Dictionary<string, ObraDeMapa>();
            foreach (var k in cosas)
                if (k.Clase == ClaseDeCosa.Obra && k.Entidad?.Obra is ObraDeMapa ob && ob.HasDestinoId
                    && (ob.Tipo == TipoObra.ObraMejoraAvanzada || ob.Tipo == TipoObra.ObraReparacion))
                    obrasPorAvanzada[ob.DestinoId] = ob;
            // una miniatura de lo menudo en (x, y), con la puerta hacia el camino que sale de ella (o girada por su semilla), y su claro
            Miniatura PonerMenuda(CosaDelMapa c, Modelo mo, double x, double y, double sem, Vector4 faccion, bool conRumbo)
            {
                double escalaMapa = EscalaForzada.TryGetValue(c.Id, out var forzada) ? forzada : mo.EscalaMapa;
                double f = CosasDelMapa.Fijos(c.Clase), huella = mo.EscalaFija ? mo.RadioU : mo.RadioU * escalaMapa * f;
                var mi = new Miniatura
                {
                    X = x, Y = y, Semilla = sem, Huella = huella, Escala = mo.EscalaFija ? mo.UnidadU : UnidadDe(c.Clase) * escalaMapa, Asentar = mo.EscalaFija,
                    BajoNiebla = c.BajoNiebla, Faccion = faccion,
                    Modelo = mo, Recurso = Vector4.zero, Rumbo = conRumbo ? RumboDelCamino(x, y, huella, Caminos) : double.NaN,
                    // (el detalle, con la huella de antes de su factor: cambia de nivel a la misma distancia que antes, y no carga más)
                    HuellaDetalle = Math.Max(huella / f, HuellaDeDetalleMinima),
                };
                miniaturas.Add(mi);
                Claros.Add((x, y, ClaroDeMenudo(c.Clase, huella)));
                return mi;
            }
            // una pieza suelta pegada a su miniatura (si está la pieza)
            void PonerPieza(Miniatura padre, Modelo pieza, double lx, double ly, double giro, bool kitDeAvanzada = false)
            {
                if (pieza == null) return;
                miniaturas.Add(new Miniatura
                {
                    // (si su avanzada va a escala fija, el kit de obra a la de antes: no crece con ella; las demás piezas, el edificio militar y la
                    // mercancía, a la de su miniatura, que es la suya)
                    EscalaPieza = kitDeAvanzada && padre.Modelo.EscalaFija ? UnidadDe(ClaseDeCosa.Avanzada) * 1.35 / padre.Escala : 1,
                    Padre = padre, Lx = lx, Ly = ly, LGiro = giro, X = padre.X, Y = padre.Y, Semilla = padre.Semilla, Huella = padre.Huella, Escala = padre.Escala,
                    BajoNiebla = padre.BajoNiebla, Faccion = padre.Faccion, Modelo = pieza, Recurso = Vector4.zero, Rumbo = double.NaN,
                    HuellaDetalle = padre.HuellaDetalle,
                });
            }
            foreach (var c in cosas)
            {
                if (!Pinta(c.Clase) || c.Entidad == null) continue;
                double x = c.X, y = c.Y, sem = P.Semilla(x, y);
                string color = Hex(c.Color);
                var e = c.Entidad;
                switch (c.Clase)
                {
                    case ClaseDeCosa.Plaza:
                    {
                        double rc, nivel;
                        bool? muralla;
                        if (e.Asentamiento != null)
                        {
                            var a = e.Asentamiento;
                            double rz = a.Zona.Count >= 3 ? a.Zona.Max(z => Math.Sqrt(Math.Pow(z.X / 1000.0 - x, 2) + Math.Pow(z.Y / 1000.0 - y, 2))) : radioZonaInicial;
                            rc = rz * 0.1;
                            nivel = a.Nivel == 0 ? 1 : a.Nivel;
                            muralla = a.TieneMuralla;
                        }
                        else
                        {
                            var f = e.FichaConocida;
                            rc = (f.HasRadioPotencial ? f.RadioPotencial / 1000.0 : radioZonaInicial) * 0.1;
                            nivel = f.Nivel == 0 ? 1 : f.Nivel;
                            muralla = null; // sin saberlo, lo de su nivel
                        }
                        if (ConMiniatura(nivel))
                        {
                            double huella = CosasDelMapa.HuellaDePlaza(rc, nivel, x, y);
                            var modelo = ModeloDe(nivel, sem);
                            // la muralla, solo si se sabe que la tiene (las recordadas, sin ella) y desde el nivel 2; con ella, la huella llega a su radio
                            bool conMuralla = muralla == true && nivel >= 2;
                            double fuera = huella * (conMuralla ? modelo.RadioMuralla : 1);
                            miniaturas.Add(new Miniatura { X = x, Y = y, Rc = rc, Semilla = sem, Nivel = nivel, Huella = huella, HuellaDetalle = huella / CosasDelMapa.Fijos(ClaseDeCosa.Plaza), Muralla = conMuralla, BajoNiebla = c.BajoNiebla, Faccion = Lineal(c.Color), FaccionId = c.FaccionId, Modelo = modelo,
                                Escala = modelo.UnidadU, Asentar = true,
                                Rumbo = double.IsNaN(modelo.Puerta) ? double.NaN : RumboDelCamino(x, y, fuera, Caminos) });
                            // sin árboles ni matas hasta la huella y media (el terreno pastado); la capa baja, rala hasta un radio más (`Ralo`)
                            Claros.Add((x, y, fuera * 1.5));
                        }
                        else
                        {
                            P.Ciudad(c.BajoNiebla ? abajo : arriba, x, y, rc, nivel, color, sem, muralla);
                            Claros.Add((x, y, P.ClaroDePlaza(rc, nivel)));
                        }
                        break;
                    }
                    case ClaseDeCosa.Pueblo:
                    case ClaseDeCosa.Aldea:
                    {
                        var q = e.Pueblo;
                        bool aldea = c.Clase == ClaseDeCosa.Aldea;
                        bool ganado = q != null && (q.Cultivo == "ganado" || (!q.HasCultivo && q.HasRebanoEspecie));
                        var mn = aldea && q != null ? ModeloDePueblo(q.TipoExtractor, q.NivelAldea, ganado) : null;
                        if (mn != null)
                        {
                            // «Mejorado»: el núcleo de la aldea, girado por su semilla (sin espejo: los campos van en su marco), y sus campos
                            // (`ParcelasDeAldea`, después de ponerlo todo: no labran sobre lo de los demás)
                            int nivelA = Math.Max(1, Math.Min(3, (int)q.NivelAldea));
                            var mi = new Miniatura
                            {
                                X = x, Y = y, Semilla = sem, Nivel = nivelA, Huella = mn.RadioU * CosasDelMapa.Fijos(ClaseDeCosa.Aldea), HuellaDetalle = mn.RadioU, Escala = UnidadDePueblo, Rumbo = double.NaN,
                                BajoNiebla = c.BajoNiebla, Faccion = Lineal(c.Color), Modelo = mn, Recurso = Vector4.zero,
                                Cultivo = Math.Max(0, Array.IndexOf(Cultivos, ganado ? "ganado" : q.HasCultivo ? q.Cultivo : "trigo")), Madre = q.AsentamientoMadreId, Cabezas = q.HasRebanoCabezas ? q.RebanoCabezas / 1000 : 40, Vacuno = !q.HasRebanoEspecie || q.RebanoEspecie == "vacuno",
                            };
                            miniaturas.Add(mi);
                            aldeas.Add((mi, q.Id));
                            Claros.Add((x, y, mn.RadioU * CosasDelMapa.Fijos(ClaseDeCosa.Aldea)));
                            break;
                        }
                        var mp = !aldea && q != null ? ModeloDePueblo(q.TipoExtractor, q.NivelAldea) : null;
                        if (mp != null)
                        {
                            // «Mejorado»: el pueblo, sobre su nodo (en su centro), con la media luna hacia el camino que sale de él o, sin camino, hacia
                            // su plaza madre; en espejo según la semilla. El nodo se dibuja con su variante 1 y el mismo giro (`GirosDeNodos`).
                            var nodo = q.NodoId != "" ? NodoPorId?.Invoke(q.NodoId) : null;
                            var madre = cosas.Find(k => k.Clase == ClaseDeCosa.Plaza && k.Id == q.AsentamientoMadreId);
                            var (px, py, rumbo, espejo) = SitioDePueblo(x, y, nodo != null ? (nodo.Value.x, nodo.Value.y) : ((double, double)?)null,
                                madre != null ? (madre.X, madre.Y) : ((double, double)?)null, HuellaDePuebloDe(mp), Caminos);
                            var mi = new Miniatura
                            {
                                X = px, Y = py, Semilla = P.Semilla(px, py), Nivel = Math.Max(1, (int)q.NivelAldea), Huella = HuellaDePuebloDe(mp), HuellaDetalle = mp.RadioU,
                                Escala = double.IsNaN(mp.UnidadU) ? UnidadDePueblo : mp.UnidadU, Asentar = !double.IsNaN(mp.UnidadU), Rumbo = rumbo,
                                Espejo = espejo, BajoNiebla = c.BajoNiebla, Faccion = Lineal(c.Color), Modelo = mp,
                                Recurso = nodo != null && ColorDeRecurso != null ? ColorDeRecurso(nodo.Value.tipo) : Vector4.zero,
                            };
                            miniaturas.Add(mi);
                            if (nodo != null) giros[q.NodoId] = GradosDe(mi);
                            Claros.Add((px, py, HuellaDePuebloDe(mp)));
                            break;
                        }
                        P.Pueblo(arriba, x, y, aldea, color, sem, q != null && q.HasCultivo ? q.Cultivo : null);
                        Claros.Add((x, y, P.ClaroDePueblo(aldea)));
                        break;
                    }
                    case ClaseDeCosa.Avanzada:
                    {
                        var v = e.Avanzada;
                        bool ruinas = v.Estado == EstadoAvanzada.AvanzadaEnRuinas;
                        // «Mejorado»: por etapa y estado; su obra (mejora o reparación) se pinta con ella
                        obrasPorAvanzada.TryGetValue(v.Id, out var obra);
                        bool mejora = obra != null && obra.Tipo == TipoObra.ObraMejoraAvanzada, reparacion = obra != null && obra.Tipo == TipoObra.ObraReparacion;
                        string nombre = ModeloDeAvanzada(v.Etapa, ruinas, mejora && obra.HasTerminaEnMs, sem);
                        var mo = Menudo(nombre);
                        if (mo != null)
                        {
                            var mi = PonerMenuda(c, mo, x, y, sem, ruinas ? Vector4.zero : Lineal(c.Color), true);
                            // (la reparación de una ruina: el kit entero alrededor; la mejora pendiente de materiales: los montones al borde)
                            if ((reparacion && ruinas) || (mejora && !obra.HasTerminaEnMs))
                                foreach (var (pieza, px, py, pg) in SitiosDelKit(RadioDeMuro(nombre), sem, reparacion))
                                    PonerPieza(mi, PiezaMenuda("obra_kit", pieza), px, py, pg, true);
                            break;
                        }
                        P.Avanzada(arriba, x, y, v.Etapa, ruinas, color);
                        Claros.Add((x, y, P.ClaroDeAvanzada(v.Etapa)));
                        break;
                    }
                    case ClaseDeCosa.Obra:
                    {
                        var o = e.Obra;
                        if (o.Tipo == TipoObra.ObraMejoraAldea) break; // la mejora de una aldea no se pinta aparte: la aldea ya está
                        // («Mejorado»: la mejora y la reparación de una avanzada que se ve van pintadas en ella)
                        if ((o.Tipo == TipoObra.ObraMejoraAvanzada || o.Tipo == TipoObra.ObraReparacion) && o.HasDestinoId && avanzadasVistas.Contains(o.DestinoId)
                            && Menudo("fuerte") != null) break;
                        var mo = Menudo(ModeloDeObra(o.HasTerminaEnMs));
                        if (mo != null) { PonerMenuda(c, mo, x, y, sem, Lineal(c.Color), false); break; }
                        P.Obra(arriba, x, y, o.HasTerminaEnMs, color);
                        Claros.Add((x, y, P.ClaroDeObra()));
                        break;
                    }
                    case ClaseDeCosa.Mercenarios:
                    {
                        var k = e.CampamentoMercenarios;
                        var mo = Menudo(ModeloDeMercenarios(k.Origen));
                        if (mo != null)
                        {
                            var mi = PonerMenuda(c, mo, x, y, sem, Vector4.zero, false);
                            var s = mo.SolarMilitar;
                            if (s != null && s.Length >= 3) PonerPieza(mi, PiezaMenuda("mercenarios_militar", PiezaMilitar(k.Edificios)), s[0], s[1], s[2]);
                            break;
                        }
                        P.CampamentoDeMercenarios(arriba, x, y, sem);
                        Claros.Add((x, y, P.ClaroDeCampamento()));
                        break;
                    }
                    case ClaseDeCosa.Bandidos:
                    {
                        var b = e.CampamentoBandidos;
                        var mo = Menudo(ModeloDeBandidos(b.HasVetaId ? b.VetaId : null, b.BosqueId, b.Nivel, sem));
                        if (mo != null) { PonerMenuda(c, mo, x, y, sem, Vector4.zero, false); break; }
                        P.Bandidos(arriba, x, y, e.CampamentoBandidos.Nivel, sem);
                        Claros.Add((x, y, P.ClaroDeBandidos()));
                        break;
                    }
                    case ClaseDeCosa.Alijo:
                    {
                        var mo = Menudo(ModeloDeAlijo(sem));
                        if (mo != null)
                        {
                            var mi = PonerMenuda(c, mo, x, y, sem, Vector4.zero, false);
                            var s = mo.SitioMercancia ?? new double[] { 0, 0 };
                            PonerPieza(mi, PiezaMenuda("alijo_mercancia", PiezaDeMercancia(e.Alijo.Recurso)), s[0], s[1], 0);
                            break;
                        }
                        P.Alijo(arriba, x, y);
                        break;
                    }
                    case ClaseDeCosa.Batalla:
                    {
                        var mo = Menudo("batalla");
                        if (mo != null)
                        {
                            var mi = PonerMenuda(c, mo, x, y, sem, Vector4.zero, false);
                            mi.Humo = true;
                            if (c.Propia) { mi.FaccionId = c.FaccionId; mi.EstandarteDeBando = Lineal(c.ColorDeBando); }
                            break;
                        }
                        P.Batalla(arriba, x, y, c.Propia);
                        break;
                    }
                }
            }
            Campos(aldeas, cosas);
            ralos = miniaturas.Select(mi => (mi.X, mi.Y, mi.Huella)).ToArray();
            if (giros.Count != GirosDeNodos.Count || giros.Any(kv => !GirosDeNodos.TryGetValue(kv.Key, out var g) || g != kv.Value))
            {
                GirosDeNodos.Clear();
                foreach (var kv in giros) GirosDeNodos[kv.Key] = kv.Value;
                VersionGiros++;
            }
            lotes.Clear();
            PiezasPuestas = 0;
            void Poner(P.Piezas piezas, bool bajo)
            {
                foreach (var mat in piezas.Materiales)
                {
                    var l = new Lote { Malla = P.EsTienda(mat) ? tienda : caja, BajoNiebla = bajo };
                    var col = Lineal(P.Colores[mat]);
                    foreach (var p in piezas.Por[mat]) { l.Piezas.Add(p); l.Colores.Add(col); }
                    lotes.Add(l);
                }
                foreach (var color in piezas.Colores)
                {
                    var l = new Lote { Malla = bandera, BajoNiebla = bajo };
                    var col = Lineal(DeHex(color));
                    foreach (var p in piezas.Banderas[color]) { l.Piezas.Add(p); l.Colores.Add(col); }
                    lotes.Add(l);
                }
            }
            Poner(arriba, false);
            Poner(abajo, true);
            Llamadas = Triangulos = 0;
            foreach (var l in lotes)
            {
                PiezasPuestas += l.Piezas.Count;
                Llamadas += (l.Piezas.Count + PorLlamada - 1) / PorLlamada;
                Triangulos += l.Piezas.Count * (int)(l.Malla.GetIndexCount(0) / 3);
            }
        }

        /// <summary>Pone cada pieza a la altura del suelo pintado (− 0,02 u + su base) con su giro (Euler XYZ de three (0, −giro, inclina), pasado
        /// al marco de Unity) y su tamaño.</summary>
        void Asentar()
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            // las miniaturas: a la altura media de su huella, sesgadas con la pendiente entera (cada punto sube o baja con el suelo y las paredes
            // siguen a plomo; antes, inclinadas la mitad: en ladera, el pie de lo de abajo quedaba en el aire), giradas por su semilla
            foreach (var mi in miniaturas)
            {
                if (mi.Padre != null)
                {
                    // (una pieza suelta: en su sitio del modelo de su miniatura, que va antes en la lista y ya está asentada)
                    mi.M = mi.Padre.M * LocalDeBlender(mi.Lx, mi.Ly, mi.LGiro) * (mi.EscalaPieza != 1 ? Matrix4x4.Scale(Vector3.one * (float)mi.EscalaPieza) : Matrix4x4.identity);
                    // (asentada con su miniatura)
                    mi.Rejilla = mi.Padre.Rejilla;
                    mi.Residuos = mi.Padre.Residuos;
                    mi.MMuralla = mi.M;
                    continue;
                }
                double r = mi.Huella, suma = terreno.AlturaPintada(mi.X, mi.Y);
                int n = 1;
                foreach (var f in new[] { 0.5, 1.0 })
                    for (int i = 0; i < 12; i++)
                    {
                        double a = i * Math.PI / 6;
                        suma += terreno.AlturaPintada(mi.X + Math.Cos(a) * r * f, mi.Y + Math.Sin(a) * r * f);
                        n++;
                    }
                double gx = (terreno.AlturaPintada(mi.X + r, mi.Y) - terreno.AlturaPintada(mi.X - r, mi.Y)) / (2 * r * e);
                double gy = (terreno.AlturaPintada(mi.X, mi.Y + r) - terreno.AlturaPintada(mi.X, mi.Y - r)) / (2 * r * e);
                var giro = Quaternion.Euler(0, GradosDe(mi), 0);
                // (las plazas, a su huella; los pueblos, a la unidad del caserío de nivel 1, y en espejo según la semilla)
                double escalaU = double.IsNaN(mi.Escala) ? r : mi.Escala;
                float escala = (float)(escalaU * e);
                var centro = EscalaDelMapa.AEscena(mi.X, mi.Y, suma / n);
                var sesgoVilla = Matrix4x4.identity;
                sesgoVilla.m10 = (float)gx;
                sesgoVilla.m12 = (float)-gy; // (z de la escena = −y del mapa)
                mi.M = Matrix4x4.Translate(centro) * sesgoVilla * Matrix4x4.TRS(Vector3.zero, giro, Vector3.one * escala)
                       * (mi.Espejo ? Matrix4x4.Scale(new Vector3(-1, 1, 1)) : Matrix4x4.identity);
                // la muralla sigue la pendiente entera (la villa, la mitad): sesgada, no inclinada (los muros, a plomo), con la pendiente del
                // suelo bajo su anillo; así el lienzo no se entierra cuesta arriba ni vuela cuesta abajo
                double rm = r * mi.Modelo.RadioMuralla;
                double mx = (terreno.AlturaPintada(mi.X + rm, mi.Y) - terreno.AlturaPintada(mi.X - rm, mi.Y)) / (2 * rm * e);
                double my = (terreno.AlturaPintada(mi.X, mi.Y + rm) - terreno.AlturaPintada(mi.X, mi.Y - rm)) / (2 * rm * e);
                var sesgo = Matrix4x4.identity;
                sesgo.m10 = (float)mx;
                sesgo.m12 = (float)-my; // (z de la escena = −y del mapa)
                mi.MMuralla = Matrix4x4.Translate(centro) * sesgo * Matrix4x4.TRS(Vector3.zero, giro, Vector3.one * escala);
                mi.Residuos = null;
                if (mi.Asentar)
                {
                    // (las plazas, asentadas por vértice: la villa y la muralla, en el plano de la villa más lo que le falte a cada punto para
                    // quedar en el suelo pintado, en una rejilla que cubre la muralla)
                    mi.MMuralla = Matrix4x4.Translate(centro) * sesgoVilla * Matrix4x4.TRS(Vector3.zero, giro, Vector3.one * escala);
                    int nr = LadoDeRejilla;
                    float medio = (float)(Math.Max(r, r * mi.Modelo.RadioMuralla) * 1.2 * e);
                    mi.Rejilla = new Vector4(centro.x, centro.z, medio, nr);
                    mi.Residuos = new float[nr * nr];
                    for (int j = 0; j < nr; j++)
                        for (int i = 0; i < nr; i++)
                        {
                            float wx = centro.x + (2f * i / (nr - 1) - 1) * medio, wz = centro.z + (2f * j / (nr - 1) - 1) * medio;
                            var (px, _, py) = EscalaDelMapa.AlMapa(new Vector3(wx, 0, wz));
                            float plano = centro.y + (float)gx * (wx - centro.x) - (float)gy * (wz - centro.z);
                            mi.Residuos[j * nr + i] = (float)terreno.AlturaPintada(px, py) - plano;
                        }
                }
                if (mi.Parche != null) UnityEngine.Object.Destroy(mi.Parche);
                double alcance = double.IsNaN(mi.Escala) ? r * LadoDelSuelo * 1.42 : !double.IsNaN(mi.Modelo.UnidadU) ? escalaU * mi.Modelo.LadoSuelo * 1.42 : mi.Huella * 1.4;
                mi.Parche = mi.Modelo.Parche != null ? Parche(mi.X, mi.Y, alcance, mi.M.inverse, mi.Modelo.LadoSuelo) : null;
                if (mi.MallaCampos != null) UnityEngine.Object.Destroy(mi.MallaCampos);
                mi.MallaCampos = null;
                if (mi.Campos != null)
                {
                    // el disco de los campos: el del suelo pisado, hasta el borde de los campos; uv1 (capa, giro de la rejilla), uv2 (origen, cultivo)
                    var mc = Parche(mi.X, mi.Y, (mi.Campos.Radio + ParcelasDeAldea.Celda * 2) * UnidadDePueblo, mi.M.inverse, mi.Modelo.LadoSuelo);
                    int nv = mc.vertexCount;
                    var cv = new List<Vector3>(mc.vertices);
                    var cn = new List<Vector3>(mc.normals);
                    var cu = new List<Vector2>(mc.uv);
                    var u1 = Enumerable.Repeat(new Vector2(mi.Capa, (float)mi.Campos.Giro), nv).ToList();
                    var u2 = Enumerable.Repeat(new Vector2((float)mi.Campos.O, mi.Cultivo), nv).ToList();
                    var ct = new List<int>(mc.triangles);
                    // las sendas: cintas de tierra de 0,055 u pegadas al suelo pintado (uv1.x = −1: el sombreado las pinta de tierra; uv0.x, de
                    // un borde al otro)
                    foreach (var senda in mi.Sendas)
                        for (int i = 0; i + 1 < senda.Count; i++)
                        {
                            var (ax, ay) = senda[i];
                            var (bx, by) = senda[i + 1];
                            double tx = bx - ax, ty = by - ay, ll = Math.Sqrt(tx * tx + ty * ty);
                            if (ll < 1e-6) continue;
                            double nx = -ty / ll * 0.0275, ny = tx / ll * 0.0275;
                            int b0 = cv.Count;
                            foreach (var (px, py) in new[] { (ax + nx, ay + ny), (ax - nx, ay - ny), (bx + nx, by + ny), (bx - nx, by - ny) })
                            {
                                cv.Add(EscalaDelMapa.AEscena(px, py, terreno.AlturaPintada(px, py) + 1.2));
                                cn.Add(Vector3.up);
                                u1.Add(new Vector2(-1, 0));
                                u2.Add(Vector2.zero);
                            }
                            cu.Add(new Vector2(0, 0)); cu.Add(new Vector2(1, 0)); cu.Add(new Vector2(0, 0)); cu.Add(new Vector2(1, 0));
                            ct.AddRange(new[] { b0, b0 + 2, b0 + 1, b0 + 1, b0 + 2, b0 + 3 });
                        }
                    foreach (var (qx, qy) in mi.Calvas ?? new List<(double x, double y)>())
                    {
                        int b0 = cv.Count;
                        cv.Add(EscalaDelMapa.AEscena(qx, qy, terreno.AlturaPintada(qx, qy) + 1.2));
                        cu.Add(new Vector2(0.5f, 0));
                        for (int k = 0; k < 10; k++)
                        {
                            double a = k * Math.PI / 5, rr = 0.11 * (0.85 + 0.3 * ((k * 7) % 5) / 5.0);
                            double px = qx + Math.Cos(a) * rr, py = qy + Math.Sin(a) * rr;
                            cv.Add(EscalaDelMapa.AEscena(px, py, terreno.AlturaPintada(px, py) + 1.2));
                            cu.Add(new Vector2(0, 0));
                        }
                        for (int k = 0; k < 11; k++) { cn.Add(Vector3.up); u1.Add(new Vector2(-1, 0)); u2.Add(Vector2.zero); }
                        for (int k = 0; k < 10; k++) ct.AddRange(new[] { b0, b0 + 1 + (k + 1) % 10, b0 + 1 + k });
                    }
                    mc.SetVertices(cv);
                    mc.SetNormals(cn);
                    mc.SetUVs(0, cu);
                    mc.SetUVs(1, u1);
                    mc.SetUVs(2, u2);
                    mc.SetTriangles(ct, 0);
                    mc.RecalculateBounds();
                    mc.name = "campos de una aldea";
                    mi.MallaCampos = mc;
                }
            }
            foreach (var l in lotes)
            {
                l.Matrices = new Matrix4x4[l.Piezas.Count];
                for (int i = 0; i < l.Piezas.Count; i++)
                {
                    var p = l.Piezas[i];
                    var pos = EscalaDelMapa.AEscena(p.X, p.Y, terreno.AlturaPintada(p.X, p.Y) + (p.Base - 0.02) * e);
                    // q = qY(−giro) · qZ(inclina) (three, orden XYZ con x = 0), y al marco de Unity: (−x, −y, z, w)
                    double c2 = Math.Cos(-p.Giro / 2), s2 = Math.Sin(-p.Giro / 2), c3 = Math.Cos(p.Inclina / 2), s3 = Math.Sin(p.Inclina / 2);
                    double qx = s2 * s3, qy = s2 * c3, qz = c2 * s3, qw = c2 * c3;
                    var giro = new Quaternion((float)-qx, (float)-qy, (float)qz, (float)qw);
                    l.Matrices[i] = Matrix4x4.TRS(pos, giro, new Vector3((float)(p.Ancho * e), (float)(p.Alto * e), (float)(p.Fondo * e)));
                }
            }
        }

        /// <summary>El parche del suelo pisado de una miniatura: un disco (8 anillos de 32 puntos) de radio `r` (u) pegado a la malla de suelo
        /// pintada; su uv, la posición en las coordenadas locales del modelo (con `local`, la inversa de su matriz) llevada a la máscara.</summary>
        /// <summary>Los campos de las aldeas: las parcelas de cada una (`ParcelasDeAldea`) sin labrar el agua, los ríos, la pendiente fuerte, los
        /// caminos, los nodos, lo que pisan las demás cosas ni lo que está más cerca de otro asentamiento; y el mapa de casillas de todas, una capa
        /// por aldea (R el id de la parcela, G su estado ×64 + 32 si va de este a oeste, B su tono, A si hay parcela).</summary>
        void Campos(List<(Miniatura mi, string id)> aldeas, List<CosaDelMapa> cosas)
        {
            if (aldeas.Count == 0 || materialCampos == null) return;
            var otros = cosas.Where(k => k.Clase == ClaseDeCosa.Plaza || k.Clase == ClaseDeCosa.Pueblo || k.Clase == ClaseDeCosa.Aldea || k.Clase == ClaseDeCosa.Avanzada)
                .Select(k => (k.Id, k.X, k.Y)).ToList();
            int lado = ParcelasDeAldea.Maximo;
            var pixeles = new List<Color32[]>();
            foreach (var (mi, id) in aldeas)
            {
                double th = GradosDe(mi) * Math.PI / 180, cs = Math.Cos(th), sn = Math.Sin(th), u = UnidadDePueblo;
                // (del marco local del núcleo, en unidades del modelo, al mapa: el giro de Unity es el contrario al del mapa)
                (double, double) AlMapa(double lx, double ly) => (mi.X + u * (lx * cs - ly * sn), mi.Y + u * (lx * sn + ly * cs));
                (double, double) AlMarco(double mx, double my) { double dx = (mx - mi.X) / u, dy = (my - mi.Y) / u; return (dx * cs + dy * sn, -dx * sn + dy * cs); }
                double alcance = (ParcelasDeAldea.Radios[3] + 1) * u;
                var cerca = otros.Where(o => o.Id != id && Math.Abs(o.X - mi.X) < alcance * 2 && Math.Abs(o.Y - mi.Y) < alcance * 2).Select(o => AlMarco(o.X, o.Y)).ToList();
                var nodos = PosicionesDeNodos.Where(n => Math.Abs(n.x - mi.X) < alcance && Math.Abs(n.y - mi.Y) < alcance).ToList();
                var claros = Claros.Where(k => !(k.x == mi.X && k.y == mi.Y) && Math.Abs(k.x - mi.X) < alcance + k.r && Math.Abs(k.y - mi.Y) < alcance + k.r).ToList();
                var caminos = Caminos.Where(k => k.puntos.Any(p => Math.Abs(p.x - mi.X) < alcance + 1 && Math.Abs(p.y - mi.Y) < alcance + 1)).ToList();
                bool Vedado(double lx, double ly)
                {
                    var (x, y) = AlMapa(lx, ly);
                    if (terreno.AguaEn(x, y) || (terreno.Cauces != null && terreno.Cauces.ACauce(x, y, 0.5) < 0.15)) return true;
                    // (la pendiente fuerte: más de un 35 % en 0,2 u)
                    double e = EscalaDelMapa.MetrosPorUnidad * 0.2;
                    if (Math.Abs(terreno.AlturaPintada(x + 0.1, y) - terreno.AlturaPintada(x - 0.1, y)) > 0.35 * e ||
                        Math.Abs(terreno.AlturaPintada(x, y + 0.1) - terreno.AlturaPintada(x, y - 0.1)) > 0.35 * e) return true;
                    if (nodos.Any(n => (n.x - x) * (n.x - x) + (n.y - y) * (n.y - y) < 0.8 * 0.8)) return true;
                    if (claros.Any(k => (k.x - x) * (k.x - x) + (k.y - y) * (k.y - y) < (k.r + 0.1) * (k.r + 0.1))) return true;
                    foreach (var (_, pts) in caminos)
                        for (int i = 1; i < pts.Count; i++)
                        {
                            var (ax, ay) = pts[i - 1];
                            double bx = pts[i].x - ax, by = pts[i].y - ay, ll = Math.Max(bx * bx + by * by, 1e-9);
                            double t = Math.Max(0, Math.Min(1, ((x - ax) * bx + (y - ay) * by) / ll)), dx = x - ax - t * bx, dy = y - ay - t * by;
                            // (medio ancho del camino ancho, unos 0,25 u, y 0,15 u de margen)
                            if (dx * dx + dy * dy < 0.4 * 0.4) return true;
                        }
                    return false;
                }
                bool ganado = mi.Cultivo == Array.IndexOf(Cultivos, "ganado");
                var campos = ParcelasDeAldea.Hacer((int)mi.Nivel, ganado, (int)(mi.Semilla * 100000) % 100000, mi.Modelo.Ocupado, Vedado, cerca);
                // (el sésamo, solo verde o en barbecho)
                if (mi.Cultivo == Array.IndexOf(Cultivos, "sesamo"))
                    for (int k = 0; k < campos.Estados.Length; k++) if (campos.Estados[k] != ParcelasDeAldea.Estado.Arado) campos.Estados[k] = ParcelasDeAldea.Estado.Verde;
                mi.Campos = campos;
                mi.Capa = pixeles.Count;
                // (sin sendas propias: el camino real de la red es el que entra; 23-11)
                mi.Sendas = new List<List<(double x, double y)>>();
                mi.Piezas = PiezasDe(mi, campos, AlMapa, th);
                var px = new Color32[lado * lado];
                for (int i = 0; i < campos.N; i++)
                    for (int j = 0; j < campos.N; j++)
                    {
                        int p = campos.Parcela[i, j];
                        if (p < 0) continue;
                        px[j * lado + i] = new Color32((byte)(p & 255), (byte)((int)campos.Estados[p] * 64 + (campos.Horizontal[i, j] ? 32 : 0)),
                            (byte)Mathf.Clamp((campos.Tonos[p].valor - 0.92f) / 0.16f * 255, 0, 255), 255);
                    }
                pixeles.Add(px);
                Claros.Add((mi.X, mi.Y, campos.Radio * u));
            }
            if (celdas == null || celdas.depth != pixeles.Count)
            {
                if (celdas != null) UnityEngine.Object.Destroy(celdas);
                celdas = new Texture2DArray(lado, lado, pixeles.Count, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "casillas de las aldeas" };
            }
            for (int k = 0; k < pixeles.Count; k++) celdas.SetPixels32(pixeles[k], k);
            celdas.Apply(false);
            materialCampos.SetTexture("_Celdas", celdas);
            materialCamposBajo.SetTexture("_Celdas", celdas);
        }

        /// <summary>Lo que tiene volumen sobre los campos de una aldea (`cosas_de` de `aldea.py`): olivos al tresbolillo, dos hileras de cepas por
        /// casilla, gavillas en los rastrojos del trigo, hacinas en la cebada, y en la ganadera cercas en una de cada cuatro lindes y el rebaño en
        /// dos o tres hatos (una figura por cada 5 cabezas, de 8 a 24). `th`: el giro del núcleo (radianes, el del mapa).</summary>
        List<(int, Matrix4x4)> PiezasDe(Miniatura mi, ParcelasDeAldea p, Func<double, double, (double, double)> alMapa, double th)
        {
            var l = new List<(int, Matrix4x4)>();
            var rng = new System.Random((int)(mi.Semilla * 100000) % 100000 * 13 + 5);
            double c = ParcelasDeAldea.Celda, g = p.Giro, e = EscalaDelMapa.MetrosPorUnidad;
            float escala = (float)(UnidadDePueblo * e);
            // (de la rejilla al marco del núcleo y al mapa; `rumbo`: el de la pieza en la rejilla, radianes)
            void Poner(int tipo, double gx, double gy, double rumbo)
            {
                double lx = gx * Math.Cos(g) - gy * Math.Sin(g), ly = gx * Math.Sin(g) + gy * Math.Cos(g);
                var (x, y) = alMapa(lx, ly);
                // (en el suelo pintado: se dibujan después de los campos, cola 2456, y no los tapa el disco que va 1 m por encima)
                var pos = EscalaDelMapa.AEscena(x, y, terreno.AlturaPintada(x, y));
                l.Add((tipo, Matrix4x4.TRS(pos, Quaternion.Euler(0, (float)((th + g + rumbo) * 180 / Math.PI), 0), Vector3.one * escala)));
            }
            string cultivo = Cultivos[mi.Cultivo];
            var porParcela = new Dictionary<int, List<(int i, int j)>>();
            for (int i = 0; i < p.N; i++)
                for (int j = 0; j < p.N; j++)
                    if (p.Parcela[i, j] >= 0) { if (!porParcela.TryGetValue(p.Parcela[i, j], out var cs)) porParcela[p.Parcela[i, j]] = cs = new List<(int, int)>(); cs.Add((i, j)); }
            foreach (var kv in porParcela.OrderBy(k => k.Key))
            {
                var estado = p.Estados[kv.Key];
                var cs = kv.Value;
                bool horiz = p.Horizontal[cs[0].i, cs[0].j];
                double x0 = p.O + cs.Min(q => q.i) * c, x1 = p.O + (cs.Max(q => q.i) + 1) * c, y0 = p.O + cs.Min(q => q.j) * c, y1 = p.O + (cs.Max(q => q.j) + 1) * c;
                double Entre(double a, double b) => a + 0.04 + rng.NextDouble() * Math.Max(0, b - a - 0.08);
                if (cultivo == "oliva" && estado != ParcelasDeAldea.Estado.Arado)
                {
                    foreach (var (i, j) in cs)
                        if (j % 2 == 0 && (i + j / 2) % 2 == 0) Poner(0, p.O + (i + 0.5) * c, p.O + (j + 0.5) * c, rng.NextDouble() * 6.28);
                }
                else if (cultivo == "vid" && estado != ParcelasDeAldea.Estado.Arado)
                {
                    foreach (var (i, j) in cs)
                        foreach (var h in new[] { 0.25, 0.75 })
                            if (horiz) Poner(1, p.O + (i + 0.5) * c, p.O + (j + h) * c, 0);
                            else Poner(1, p.O + (i + h) * c, p.O + (j + 0.5) * c, Math.PI / 2);
                }
                else if (cultivo == "trigo" && estado == ParcelasDeAldea.Estado.Rastrojo)
                {
                    for (int k = 0; k < 2; k++) Poner(2, Entre(x0, x1), Entre(y0, y1), rng.NextDouble() * 6.28);
                }
                else if (cultivo == "cebada" && (estado == ParcelasDeAldea.Estado.Rastrojo || estado == ParcelasDeAldea.Estado.Maduro) && rng.NextDouble() < 0.6)
                    Poner(3, Entre(x0, x1), Entre(y0, y1), horiz ? 0 : Math.PI / 2);
            }
            var cercadoEntero = new HashSet<int>();
            void PonerRebano()
            {
                // el rebaño: dos o tres hatos, cada uno en un cercado distinto a 0,65 u o más del núcleo, con su abrevadero; una figura por
                // cada dos cabezas (de 8 a 30), vacas o, si el rebaño es de otra especie, ovejas. Cada cercado con hato, cercado entero y con
                // una calva de tierra pisada en la entrada (el lado que mira al núcleo)
                double Gauss() => Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())) * Math.Cos(2 * Math.PI * rng.NextDouble());
                (double x, double y) Centro(List<(int i, int j)> cs) => (cs.Average(q => p.O + (q.i + 0.5) * c), cs.Average(q => p.O + (q.j + 0.5) * c));
                // (los cercados grandes, de 10 casillas o más, a 0,65 u o más del núcleo; los mayores antes)
                var lejos = porParcela.Where(k => k.Value.Count >= 10).Select(k => (k.Key, Centro(k.Value)))
                    .Where(k => Math.Sqrt(k.Item2.x * k.Item2.x + k.Item2.y * k.Item2.y) >= 0.65 * CosasDelMapa.Fijos(ClaseDeCosa.Aldea) / UnidadDePueblo)
                    .OrderByDescending(k => porParcela[k.Key].Count).ThenBy(k => k.Key).ToList();
                int hatos = Math.Min(lejos.Count, rng.Next(2, 4));
                var elegidos = lejos.Take(hatos).ToList();
                int figuras = (int)Math.Max(8, Math.Min(30, mi.Cabezas / 2));
                mi.Calvas = new List<(double x, double y)>();
                for (int h = 0; h < elegidos.Count; h++)
                {
                    var (id, (cx, cy)) = elegidos[h];
                    // (cada figura en una casilla del cercado al azar, algo movida, sin solaparse: a 1,2 veces su largo de las demás como poco;
                    // las vacas, de tres capas por la semilla)
                    double largo = mi.Vacuno ? 0.09 : 0.06, minimo = largo * 1.2;
                    var puestas = new List<(double x, double y)>();
                    for (int t = h; t < figuras; t += elegidos.Count)
                        for (int intento = 0; intento < 24; intento++)
                        {
                            var (fi, fj) = porParcela[id][rng.Next(porParcela[id].Count)];
                            double fx = p.O + (fi + 0.5) * c + Gauss() * 0.03, fy = p.O + (fj + 0.5) * c + Gauss() * 0.03;
                            if (puestas.Any(q => (q.x - fx) * (q.x - fx) + (q.y - fy) * (q.y - fy) < minimo * minimo)) continue;
                            puestas.Add((fx, fy));
                            Poner(mi.Vacuno ? new[] { 5, 8, 9 }[rng.Next(3)] : 6, fx, fy, rng.NextDouble() * 6.28);
                            break;
                        }
                    Poner(7, cx + 0.07, cy - 0.05, rng.NextDouble() * 6.28);
                    // la entrada: la casilla del cercado más cercana al núcleo, en su lado
                    var (ei, ej) = porParcela[id].OrderBy(q => Math.Pow(p.O + (q.i + 0.5) * c, 2) + Math.Pow(p.O + (q.j + 0.5) * c, 2)).First();
                    double ex = p.O + (ei + 0.5) * c, ey = p.O + (ej + 0.5) * c, el = Math.Max(1e-6, Math.Sqrt(ex * ex + ey * ey));
                    ex -= ex / el * c * 0.5; ey -= ey / el * c * 0.5;
                    double lx = ex * Math.Cos(g) - ey * Math.Sin(g), ly = ex * Math.Sin(g) + ey * Math.Cos(g);
                    mi.Calvas.Add(alMapa(lx, ly));
            }
            cercadoEntero = new HashSet<int>(elegidos.Select(k => k.Key));
            }

            if (cultivo == "ganado")
            {
                PonerRebano();
                // las cercas: en una de cada tres lindes (entre parcelas o hacia fuera) y en todas las de los cercados con hato
                var rc = new System.Random((int)(mi.Semilla * 100000) % 100000 * 3 + 11);
                var cercada = new Dictionary<(int, int), bool>();
                for (int i = 0; i < p.N; i++)
                    for (int j = 0; j < p.N; j++)
                    {
                        int a = p.Parcela[i, j];
                        if (a < 0) continue;
                        foreach (var (di, dj) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) })
                        {
                            int ii = i + di, jj = j + dj, b = ii >= 0 && jj >= 0 && ii < p.N && jj < p.N ? p.Parcela[ii, jj] : -1;
                            if (b == a || (b > a)) continue;
                            if (!cercada.TryGetValue((a, b), out var si)) cercada[(a, b)] = si = rc.NextDouble() < 0.33;
                            if (si || cercadoEntero.Contains(a) || cercadoEntero.Contains(b)) Poner(4, p.O + (i + 0.5 + di * 0.5) * c, p.O + (j + 0.5 + dj * 0.5) * c, dj != 0 ? 0 : Math.PI / 2);
                        }
                    }

            }
            return l;
        }

        readonly Dictionary<Mesh, Mesh> espejadas = new Dictionary<Mesh, Mesh>();
        /// <summary>La copia de una malla en espejo (x → −x), con el orden de sus triángulos y la x de sus normales vueltos: se dibuja con la matriz
        /// sin espejo, porque una escala negativa da la vuelta a las caras y el sombreado (Cull Back) enseñaba las de dentro.</summary>
        Mesh Espejada(Mesh m)
        {
            if (espejadas.TryGetValue(m, out var e)) return e;
            e = UnityEngine.Object.Instantiate(m);
            e.name = m.name + " (espejo)";
            var v = e.vertices; for (int i = 0; i < v.Length; i++) v[i].x = -v[i].x;
            var n = e.normals; for (int i = 0; i < n.Length; i++) n[i].x = -n[i].x;
            e.vertices = v;
            e.normals = n;
            for (int k = 0; k < e.subMeshCount; k++)
            {
                var t = e.GetTriangles(k);
                for (int i = 0; i < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
                e.SetTriangles(t, k);
            }
            e.RecalculateBounds();
            return espejadas[m] = e;
        }

        /// <summary>El giro (grados, en Unity) de una miniatura: con la puerta (o el frente del pueblo) hacia su rumbo, o por su semilla.</summary>
        static float GradosDe(Miniatura mi) => GradosDe(mi.Rumbo, mi.Modelo.Puerta, mi.Semilla);

        /// <summary>El giro (grados, en Unity) que pone la puerta (ángulo `puerta` del modelo, en el mapa) hacia `rumbo`; sin rumbo, por la semilla.</summary>
        public static float GradosDe(double rumbo, double puerta, double semilla) =>
            (float)(double.IsNaN(rumbo) ? P.Azar(semilla, 3, 3) * 360 : (rumbo - puerta) * 180 / Math.PI);

        /// <summary>Dónde y cómo va un pueblo extractor: en el centro de su nodo (o en su posición, si no se conoce el nodo), con el frente hacia
        /// el camino que sale de su huella (`RumboDelCamino`) o, sin camino, hacia su plaza madre (NaN si tampoco), y en espejo según la semilla
        /// de su sitio.</summary>
        public static (double x, double y, double rumbo, bool espejo) SitioDePueblo(double x, double y, (double x, double y)? nodo, (double x, double y)? madre,
            double radio, IEnumerable<(string id, IReadOnlyList<(double x, double y)> puntos)> caminos)
        {
            double px = nodo?.x ?? x, py = nodo?.y ?? y;
            double rumbo = RumboDelCamino(px, py, radio, caminos);
            if (double.IsNaN(rumbo) && madre != null) rumbo = Math.Atan2(madre.Value.y - py, madre.Value.x - px);
            return (px, py, rumbo, P.Azar(P.Semilla(px, py), 5, 1) < 0.5);
        }

        Mesh Parche(double x, double y, double r, Matrix4x4 local, double lado)
        {
            const int Anillos = 8, Puntos = 32;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var col = new List<Color>();
            var tri = new List<int>();
            void Punto(double px, double py)
            {
                var pos = EscalaDelMapa.AEscena(px, py, terreno.AlturaPintada(px, py) + 1.0); // (1 m por encima del suelo pintado, de margen; sin escribir profundidad, no se nota)
                var l = local.MultiplyPoint3x4(pos);
                v.Add(pos);
                // (r: cuánto es pradera verde ahí; en lo seco, el sombreado oscurece más la tierra para que se lea)
                col.Add(new Color(terreno.SueloEn(px, py).verde, 0, 0, 1));
                // (u, v: la x y la −z locales del modelo, como sale la máscara de `villa1.py`; comprobado volcando la planta del FBX)
                uv.Add(new Vector2((float)(l.x / lado * 0.5 + 0.5), (float)(-l.z / lado * 0.5 + 0.5)));
            }
            Punto(x, y);
            for (int a = 1; a <= Anillos; a++)
                for (int p = 0; p < Puntos; p++)
                {
                    double ang = p * Math.PI * 2 / Puntos, f = (double)a / Anillos;
                    Punto(x + Math.Cos(ang) * r * f, y + Math.Sin(ang) * r * f);
                }
            // (z = −y: el sentido de las caras de Unity, en el del reloj visto desde arriba)
            for (int p = 0; p < Puntos; p++) { tri.Add(0); tri.Add(1 + (p + 1) % Puntos); tri.Add(1 + p); }
            for (int a = 1; a < Anillos; a++)
                for (int p = 0; p < Puntos; p++)
                {
                    int a0 = 1 + (a - 1) * Puntos, a1 = 1 + a * Puntos, q = (p + 1) % Puntos;
                    tri.Add(a0 + p); tri.Add(a0 + q); tri.Add(a1 + p);
                    tri.Add(a0 + q); tri.Add(a1 + q); tri.Add(a1 + p);
                }
            var m = new Mesh { name = "suelo de una miniatura" };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetColors(col);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            var n = m.normals;
            for (int k = 0; k < n.Length; k++) if (n[k].y < 0) n[k] = -n[k];
            m.normals = n;
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Manda a dibujar todas las piezas (por material y lado de la niebla, en tandas de 1023), con sombra. `camara`: la del mapa (para
        /// el nivel de detalle de las miniaturas por su ancho en pantalla); sin ella, la principal.</summary>
        public void Dibujar(Camera camara = null)
        {
            if (Estilo == EstiloDeAsentamientos.Cajas) { MsDibujar = 0; return; }
            var reloj = Stopwatch.StartNew();
            var sombras = PinturaDelMapa.SombrasReales ? ShadowCastingMode.On : ShadowCastingMode.Off;
            foreach (var l in lotes)
            {
                if (l.Matrices == null || l.Matrices.Length == 0 || !DibujarCajas) continue;
                var rp = new RenderParams(l.BajoNiebla ? bajoNiebla : encima) { shadowCastingMode = sombras, receiveShadows = true, matProps = propiedades };
                for (int i = 0; i < l.Matrices.Length; i += PorLlamada)
                {
                    int n = Math.Min(PorLlamada, l.Matrices.Length - i);
                    Array.Copy(l.Matrices, i, tanda, 0, n);
                    l.Colores.CopyTo(i, tandaColores, 0, n);
                    propiedades.SetVectorArray("_Color", tandaColores);
                    Graphics.RenderMeshInstanced(rp, l.Malla, 0, tanda, n);
                }
            }
            // las miniaturas: su nivel por su ancho en pantalla, la villa y (si la tiene) la muralla, con el color de su Facción
            var cam = camara != null ? camara : Camera.main;
            // los estandartes de las plazas (el de las columnas) en las anclas de su modelo: los de la villa con su matriz y los de la puerta con la
            // de su muralla; las que solo se recuerdan (bajo la niebla), sin él
            EstandartesPuestos = 0;
            PiesDeEstandarte.Clear();
            if (EstandartesEnPlazas && DibujarMiniaturas)
                foreach (var mi in miniaturas)
                {
                    var mo = mi.Modelo;
                    if (mo != null && !mi.BajoNiebla && mi.EstandarteDeBando is Vector4 bando)
                    {
                        // tu batalla: plantado junto al carro, al alto del de las columnas (se lee de lejos como ellas)
                        double r = mi.Huella / mi.Escala;
                        var pie = mi.M.MultiplyPoint3x4(DeBlender(r * SitioDelEstandarteDeBatalla.x, r * SitioDelEstandarteDeBatalla.y, 0));
                        pie.y += ResiduoEn(mi, pie);
                        FigurasDelMapa.EstandarteSuelto(pie, (float)(FigurasDelMapa.AltoDelEstandarte * EscalaDelMapa.MetrosPorUnidad), mi.FaccionId, bando);
                        PiesDeEstandarte.Add(pie);
                        EstandartesPuestos++;
                        continue;
                    }
                    if (mo == null || mi.BajoNiebla || mi.Padre != null || mo.Estandartes == null) continue;
                    foreach (var (anclas, m) in new[] { (mo.Estandartes, mi.M), (mi.Muralla ? mo.EstandartesMuralla : null, mi.MMuralla) })
                    {
                        if (anclas == null) continue;
                        foreach (var (x, y, z, alto) in anclas)
                        {
                            var pie = m.MultiplyPoint3x4(DeBlender(x, y, z));
                            pie.y += ResiduoEn(mi, pie);
                            float altoM = (m.MultiplyVector(Vector3.up) * (float)alto).magnitude;
                            FigurasDelMapa.EstandarteSuelto(pie, altoM, mi.FaccionId, mi.Faccion);
                            PiesDeEstandarte.Add(pie);
                            EstandartesPuestos++;
                        }
                    }
                }
            foreach (var en in Encargos(cam))
            {
                if (en.EsSuelo ? !DibujarSuelo : !DibujarMiniaturas) continue;
                if (en.EsSuelo)
                {
                    en.Material.SetFloat("_Seco", OscuroEnSeco);
                    Graphics.RenderMesh(new RenderParams(en.Material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true }, en.Malla, 0, en.M);
                    continue;
                }
                propiedadesMiniatura.SetVector("_Color", Vector4.one);
                propiedadesMiniatura.SetVector("_Rejilla", en.Residuos != null ? en.Rejilla : Vector4.zero);
                propiedadesMiniatura.SetFloatArray("_Residuos", en.Residuos ?? SinResiduos);
                propiedadesMiniatura.SetVector("_Faccion", en.Faccion);
                propiedadesMiniatura.SetVector("_Recurso", en.Recurso);
                Graphics.RenderMesh(new RenderParams(en.Material) { shadowCastingMode = SombraDeMiniaturas ? sombras : ShadowCastingMode.Off, receiveShadows = true, matProps = propiedadesMiniatura }, en.Malla, 0, en.M);
            }
            // el humo y la llama de las batallas (lo que hace visible la marca): una columna de bocanadas translúcidas y una llama pequeña que
            // parpadea, en sus sitios del modelo; caras siempre de frente a la cámara (se ven igual desde cualquier lado)
            Humaredas = 0;
            if (materialHumo != null && DibujarMiniaturas)
                foreach (var mi in miniaturas)
                {
                    if (!mi.Humo || mi.Modelo.HumoEn == null) continue;
                    float escala = (float)(mi.Escala * EscalaDelMapa.MetrosPorUnidad);
                    propiedadesHumo.SetFloat("_Fase", (float)(mi.Semilla % 1.0));
                    foreach (var (sitio, malla, mat) in new[] { (mi.Modelo.LlamaEn, mallaResplandor, materialResplandor), (mi.Modelo.HumoEn, mallaHumo, materialHumo),
                                                               (mi.Modelo.LlamaEn, mallaLlama, materialLlama) })
                    {
                        if (sitio == null || sitio.Length < 3) continue;
                        var pos = mi.M.MultiplyPoint3x4(DeBlender(sitio[0], sitio[1], sitio[2]));
                        Graphics.RenderMesh(new RenderParams(mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, matProps = propiedadesHumo,
                            worldBounds = new Bounds(pos + Vector3.up * escala * 0.8f, Vector3.one * escala * 4) }, malla, 0, Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * escala));
                    }
                    Humaredas++;
                }
            // las piezas sueltas de los campos, instanciadas por tipo
            PiezasDeCampos = 0;
            if (materialPiezas != null && DibujarMiniaturas)
            {
                foreach (var lp in porPieza) lp.Clear();
                foreach (var mi in miniaturas)
                    if (mi.Piezas != null && !mi.BajoNiebla)
                        foreach (var (tipo, m) in mi.Piezas) porPieza[tipo].Add(m);
                var rp = new RenderParams(materialPiezas) { shadowCastingMode = sombras, receiveShadows = true };
                for (int k = 0; k < piezas.Length; k++)
                {
                    if (piezas[k] == null || porPieza[k].Count == 0) continue;
                    for (int i = 0; i < porPieza[k].Count; i += PorLlamada)
                        Graphics.RenderMeshInstanced(rp, piezas[k], 0, porPieza[k], Math.Min(PorLlamada, porPieza[k].Count - i), i);
                    PiezasDeCampos += porPieza[k].Count;
                }
            }
            MsDibujar = reloj.Elapsed.TotalMilliseconds;
        }

        /// <summary>El ancho (px) de la huella de una miniatura en la pantalla de `cam` (en vertical, como lo que se ve de frente).</summary>
        static float AnchoEnPantalla(Miniatura mi, Camera cam)
        {
            if (cam == null) return AnchoCerca;
            float alto = cam.targetTexture != null ? cam.targetTexture.height : cam.pixelHeight;
            float pxPorM = alto / (2 * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float d = Mathf.Max(1, Vector3.Distance(cam.transform.position, mi.M.GetColumn(3)));
            double huella = double.IsNaN(mi.HuellaDetalle) ? mi.Huella : mi.HuellaDetalle;
            return (float)(2 * huella * EscalaDelMapa.MetrosPorUnidad) * pxPorM / d;
        }

        /// <summary>Para las pruebas: cada miniatura suelta (sin padre), con dónde está (u), el nombre de su modelo (el de su material) y su cultivo.</summary>
        public IEnumerable<(double x, double y, string malla, int cultivo)> Puestas =>
            miniaturas.Where(mi => mi.Padre == null).Select(mi => (mi.X, mi.Y, mi.Modelo?.Encima != null ? mi.Modelo.Encima.name : "", mi.Cultivo));

        /// <summary>Para las pruebas: lo más que queda el pie de una maqueta (lo más bajo de su malla: el zócalo enterrado o el talud) por
        /// encima del suelo pintado (u), mirado en 16 puntos a 0,8 de su huella; en ladera no debe pasar de un margen pequeño.</summary>
        /// <summary>Lo más alto del modelo que se dibuja en (x, y) (u del mapa, la miniatura de ese sitio): la altura en la escena de la cima de su
        /// malla de cerca y, si la lleva, de su muralla (a su escala, con su sesgo y asentada en el suelo), o null si ahí no hay modelo.</summary>
        public float? CimaEn(double x, double y)
        {
            foreach (var mi in miniaturas)
            {
                if (mi.Padre != null || mi.Modelo == null || Math.Abs(mi.X - x) > 1e-6 || Math.Abs(mi.Y - y) > 1e-6) continue;
                float? cima = null;
                void Cima(Mesh malla, Matrix4x4 m)
                {
                    if (malla == null) return;
                    var b = malla.bounds;
                    // (las cuatro esquinas de arriba de su caja: con el giro y el sesgo, la más alta)
                    foreach (var (u, w) in new[] { (b.min.x, b.min.z), (b.max.x, b.min.z), (b.min.x, b.max.z), (b.max.x, b.max.z) })
                    {
                        var p = m.MultiplyPoint(new Vector3(u, b.max.y, w));
                        p.y += ResiduoEn(mi, p);
                        cima = cima == null ? p.y : Math.Max(cima.Value, p.y);
                    }
                }
                Cima(mi.Modelo.Villa[0], mi.M);
                if (mi.Muralla) Cima(mi.Modelo.Muralla[0], mi.MMuralla);
                return cima;
            }
            return null;
        }

        public double AlAireMaximo()
        {
            double peor = 0, e = EscalaDelMapa.MetrosPorUnidad;
            foreach (var mi in miniaturas)
            {
                if (mi.Padre != null || mi.Modelo == null) continue;
                var inv = mi.M.inverse;
                // (en una rejilla de 21 × 21 sobre la huella, fuera de los puntos de la rejilla del asentado: lo que se interpola, también)
                for (int i = 0; i < 21; i++)
                    for (int j = 0; j < 21; j++)
                    {
                        double px = mi.X + (i / 10.0 - 1) * mi.Huella * 0.9, py = mi.Y + (j / 10.0 - 1) * mi.Huella * 0.9;
                        if ((px - mi.X) * (px - mi.X) + (py - mi.Y) * (py - mi.Y) > mi.Huella * mi.Huella * 0.81) continue;
                        var l = inv.MultiplyPoint(EscalaDelMapa.AEscena(px, py, 0));
                        // (las plazas, asentadas por vértice: su z = 0, el suelo del modelo, ha de quedar en el suelo pintado; lo demás, lo más bajo de
                        // su malla, el zócalo enterrado)
                        l.y = mi.Residuos != null || mi.Modelo.Villa[0] == null ? 0 : mi.Modelo.Villa[0].bounds.min.y;
                        var pie = mi.M.MultiplyPoint(l);
                        pie.y += ResiduoEn(mi, pie);
                        peor = Math.Max(peor, (pie.y - EscalaDelMapa.AEscena(px, py, terreno.AlturaPintada(px, py)).y) / e);
                    }
            }
            return peor;
        }

        /// <summary>Para las pruebas: el ancho en pantalla de cada miniatura.</summary>
        public IEnumerable<string> AnchosEnPantalla(Camera cam) => miniaturas.Select(mi => $"huella {mi.Huella:0.00} u: {AnchoEnPantalla(mi, cam):0} px");

        /// <summary>Para las pruebas: vuelve a rehacerlo todo en el próximo `Actualizar`.</summary>
        public void Olvidar() { clave = ""; }

        public void Destruir()
        {
            lotes.Clear();
            UnityEngine.Object.Destroy(encima);
            UnityEngine.Object.Destroy(bajoNiebla);
            UnityEngine.Object.Destroy(caja);
            UnityEngine.Object.Destroy(tienda);
            UnityEngine.Object.Destroy(bandera);
            foreach (var mi in miniaturas) { if (mi.Parche != null) UnityEngine.Object.Destroy(mi.Parche); if (mi.MallaCampos != null) UnityEngine.Object.Destroy(mi.MallaCampos); }
            foreach (var mo in modelos.Values.SelectMany(l => l))
                foreach (var mat in new[] { mo.Encima, mo.Bajo, mo.Parche, mo.ParcheBajo })
                    if (mat != null) UnityEngine.Object.Destroy(mat);
        }
    }
}
