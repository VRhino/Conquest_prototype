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
            public bool Muralla, BajoNiebla;
            public Vector4 Faccion;
            public Modelo Modelo;
            public Matrix4x4 M, MMuralla;
            public Mesh Parche;
        }
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
        }
        /// <summary>El tope del radio de una muralla (en huellas): el aro, el clic y el claro no crecen más.</summary>
        public const double RadioMaximoDeMuralla = 1.12;
        /// <summary>Los tramos de la red de caminos (id y puntos, u), para girar cada plaza con su puerta hacia el suyo (`RumboDelCamino`).</summary>
        public IReadOnlyList<(string id, IReadOnlyList<(double x, double y)> puntos)> Caminos = Array.Empty<(string, IReadOnlyList<(double x, double y)>)>();
        readonly List<Miniatura> miniaturas = new List<Miniatura>();
        /// <summary>«Mejorado»: los modelos por nivel de plaza (sus variantes).</summary>
        readonly Dictionary<int, List<Modelo>> modelos = new Dictionary<int, List<Modelo>>();
        readonly MaterialPropertyBlock propiedadesMiniatura = new MaterialPropertyBlock();
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
                    m.Puerta = Json(Resources.Load<TextAsset>("MapaMundo/Asentamientos/" + nombre), "puerta", double.NaN);
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
                        m.MurallaBajo = new Material(m.MurallaEncima) { name = nm + " (bajo la niebla)" };
                        m.MurallaBajo.SetFloat("_ConNiebla", 1);
                        m.RadioMuralla = Math.Min(RadioMaximoDeMuralla, Json(Resources.Load<TextAsset>("MapaMundo/Asentamientos/" + nm), "radio", 1));
                    }
                    if (nivel < 2) for (int n = 0; n < 3; n++) m.Muralla[n] = null;
                    m.Encima = new Material(sombreadoVilla) { name = nombre + " (encima)" };
                    m.Encima.SetTexture("_ColorTex", Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nombre}_color"));
                    m.Encima.SetTexture("_Mascara", Resources.Load<Texture2D>($"MapaMundo/Asentamientos/{nombre}_mascara"));
                    // (niveles 3 a 5: el encalado, con las luces comprimidas para que al sol no se queme; el 1 y el 2, como estaban)
                    m.Encima.SetFloat("_Rodilla", nivel >= 3 ? 1 : 0);
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
                        m.Parche.SetFloat("_Pintados", nivel >= 4 ? 1 : 0);
                        m.ParcheBajo = new Material(m.Parche) { name = nombre + " (suelo, bajo la niebla)" };
                        m.ParcheBajo.SetFloat("_ConNiebla", 1);
                    }
                    if (!modelos.TryGetValue(nivel, out var lista)) modelos[nivel] = lista = new List<Modelo>();
                    lista.Add(m);
                }
            CosasDelMapa.FactorDeMuralla = (nivel, x, y) => nivel >= 2 && modelos.ContainsKey((int)nivel) ? ModeloDe(nivel, P.Semilla(x, y)).RadioMuralla : 1;
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
            public Mesh Malla;
            public Material Material;
            public Matrix4x4 M;
            public Vector4 Faccion;
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
                l.Add(new Encargo { Malla = mo.Villa[nivel], Material = mat, M = mi.M, Faccion = mi.Faccion, Nivel = nivel });
                if (mi.Muralla && mo.Muralla[nivel] != null)
                    l.Add(new Encargo { Malla = mo.Muralla[nivel], Material = (mi.BajoNiebla ? mo.MurallaBajo : mo.MurallaEncima) ?? mat, M = mi.MMuralla, Faccion = mi.Faccion, EsMuralla = true, Nivel = nivel });
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
                    .Append(c.Color).Append(c.BajoNiebla).Append(c.Detalle).Append(c.Entidad?.Asentamiento?.TieneMuralla).Append(c.Entidad?.Obra?.HasTerminaEnMs).Append('|');
            foreach (var (id, p) in Caminos) sb.Append(id).Append(p.Count).Append(p.Count > 0 ? p[0].x + p[p.Count - 1].y : 0).Append(';');
            var nueva = Estilo + sb.ToString();
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
            foreach (var mi in miniaturas) if (mi.Parche != null) UnityEngine.Object.Destroy(mi.Parche);
            miniaturas.Clear();
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
                            double huella = Huella(rc, nivel);
                            var modelo = ModeloDe(nivel, sem);
                            // la muralla, solo si se sabe que la tiene (las recordadas, sin ella) y desde el nivel 2; con ella, la huella llega a su radio
                            bool conMuralla = muralla == true && nivel >= 2;
                            double fuera = huella * (conMuralla ? modelo.RadioMuralla : 1);
                            miniaturas.Add(new Miniatura { X = x, Y = y, Rc = rc, Semilla = sem, Nivel = nivel, Huella = huella, Muralla = conMuralla, BajoNiebla = c.BajoNiebla, Faccion = Lineal(c.Color), Modelo = modelo,
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
                        P.Pueblo(arriba, x, y, aldea, color, sem, q != null && q.HasCultivo ? q.Cultivo : null);
                        Claros.Add((x, y, P.ClaroDePueblo(aldea)));
                        break;
                    }
                    case ClaseDeCosa.Avanzada:
                    {
                        var v = e.Avanzada;
                        bool ruinas = v.Estado == EstadoAvanzada.AvanzadaEnRuinas;
                        P.Avanzada(arriba, x, y, v.Etapa, ruinas, color);
                        Claros.Add((x, y, P.ClaroDeAvanzada(v.Etapa)));
                        break;
                    }
                    case ClaseDeCosa.Obra:
                    {
                        var o = e.Obra;
                        if (o.Tipo == TipoObra.ObraMejoraAldea) break; // la mejora de una aldea no se pinta aparte: la aldea ya está
                        P.Obra(arriba, x, y, o.HasTerminaEnMs, color);
                        Claros.Add((x, y, P.ClaroDeObra()));
                        break;
                    }
                    case ClaseDeCosa.Mercenarios:
                        P.CampamentoDeMercenarios(arriba, x, y, sem);
                        Claros.Add((x, y, P.ClaroDeCampamento()));
                        break;
                    case ClaseDeCosa.Bandidos:
                        P.Bandidos(arriba, x, y, e.CampamentoBandidos.Nivel, sem);
                        Claros.Add((x, y, P.ClaroDeBandidos()));
                        break;
                    case ClaseDeCosa.Alijo:
                        P.Alijo(arriba, x, y);
                        break;
                    case ClaseDeCosa.Batalla:
                    {
                        var t = e.Batalla.Ticket;
                        bool propia = heroeId != null && t != null && (t.Atacante?.Participantes.Any(p => p.HeroeId == heroeId) == true || t.Defensor?.Participantes.Any(p => p.HeroeId == heroeId) == true);
                        P.Batalla(arriba, x, y, propia);
                        break;
                    }
                }
            }
            ralos = miniaturas.Select(mi => (mi.X, mi.Y, mi.Huella)).ToArray();
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
            // las miniaturas: a la altura media de su huella, inclinadas la mitad de la pendiente (hasta 8,5°), giradas por su semilla
            foreach (var mi in miniaturas)
            {
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
                // la normal del suelo, en la escena (z = −y), y la mitad de su inclinación, con tope
                var normal = new Vector3((float)-gx, 1, (float)gy).normalized;
                float angulo = Mathf.Min(Vector3.Angle(Vector3.up, normal) * 0.5f, (float)InclinacionMaxima);
                var eje = Vector3.Cross(Vector3.up, normal);
                var inclina = eje.sqrMagnitude > 1e-8 ? Quaternion.AngleAxis(angulo, eje.normalized) : Quaternion.identity;
                // girada con la puerta hacia su camino (en el mapa, el ángulo de la puerta más el giro, en el sentido de Unity), o por su semilla
                double grados = double.IsNaN(mi.Rumbo) ? P.Azar(mi.Semilla, 3, 3) * 360 : (mi.Rumbo - mi.Modelo.Puerta) * 180 / Math.PI;
                var giro = Quaternion.Euler(0, (float)grados, 0);
                float escala = (float)(r * e);
                var centro = EscalaDelMapa.AEscena(mi.X, mi.Y, suma / n);
                mi.M = Matrix4x4.TRS(centro, inclina * giro, Vector3.one * escala);
                // la muralla sigue la pendiente entera (la villa, la mitad): sesgada, no inclinada (los muros, a plomo), con la pendiente del
                // suelo bajo su anillo; así el lienzo no se entierra cuesta arriba ni vuela cuesta abajo
                double rm = r * mi.Modelo.RadioMuralla;
                double mx = (terreno.AlturaPintada(mi.X + rm, mi.Y) - terreno.AlturaPintada(mi.X - rm, mi.Y)) / (2 * rm * e);
                double my = (terreno.AlturaPintada(mi.X, mi.Y + rm) - terreno.AlturaPintada(mi.X, mi.Y - rm)) / (2 * rm * e);
                var sesgo = Matrix4x4.identity;
                sesgo.m10 = (float)mx;
                sesgo.m12 = (float)-my; // (z de la escena = −y del mapa)
                mi.MMuralla = Matrix4x4.Translate(centro) * sesgo * Matrix4x4.TRS(Vector3.zero, giro, Vector3.one * escala);
                if (mi.Parche != null) UnityEngine.Object.Destroy(mi.Parche);
                mi.Parche = mi.Modelo.Parche != null ? Parche(mi.X, mi.Y, r * LadoDelSuelo * 1.42, mi.M.inverse) : null;
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
        Mesh Parche(double x, double y, double r, Matrix4x4 local)
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
                uv.Add(new Vector2(l.x / LadoDelSuelo * 0.5f + 0.5f, -l.z / LadoDelSuelo * 0.5f + 0.5f));
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
                propiedadesMiniatura.SetVector("_Faccion", en.Faccion);
                Graphics.RenderMesh(new RenderParams(en.Material) { shadowCastingMode = SombraDeMiniaturas ? sombras : ShadowCastingMode.Off, receiveShadows = true, matProps = propiedadesMiniatura }, en.Malla, 0, en.M);
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
            return (float)(2 * mi.Huella * EscalaDelMapa.MetrosPorUnidad) * pxPorM / d;
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
            foreach (var mi in miniaturas) if (mi.Parche != null) UnityEngine.Object.Destroy(mi.Parche);
            foreach (var mo in modelos.Values.SelectMany(l => l))
                foreach (var mat in new[] { mo.Encima, mo.Bajo, mo.Parche, mo.ParcheBajo })
                    if (mat != null) UnityEngine.Object.Destroy(mat);
        }
    }
}
