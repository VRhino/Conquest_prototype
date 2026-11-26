using System;
using System.Collections.Generic;
using System.Linq;
using ConquestTactics.Mundo;
using UnityEngine;

/// <summary>
/// La ciudad por dentro con su arte, montada desde la planta del motor (<see cref="SelectorDePersonaje.InteriorParaVer"/>): cada edificio que
/// tiene receta se compone con sus modelos (`Resources/Ciudad/Modelos`) dentro de su huella y con el frente a la calle; el suelo es uno, con
/// la tierra pisada pintada en una máscara (calles, sendas de cada puerta, patios); y lo libre se rellena con <see cref="RellenoDeCiudad"/>
/// y se viste (huertos, patios, leña, árboles, hierba). Todo sale de la planta y de la posición: la misma planta da la misma ciudad.
///
/// Las recetas están escritas en metros, en el marco en que se compusieron en el banco de pruebas (x a la derecha, z al fondo), cada una con
/// las medidas de su huella y hacia dónde mira su frente; al montar se giran, de cuarto en cuarto de vuelta, para caber en la huella que
/// manda el motor con el frente a la calle más cercana.
/// </summary>
public static class CiudadPorDentro
{
    /// <summary>Metros por unidad local de la planta: la escala la manda el héroe (Víctor, 2026-11-25); una celda de 3 u.l. son 9 m.</summary>
    public const float Metros = 3f;
    const int LadoPisado = 2048;
    const string Modelos = "Ciudad/Modelos/", DeCaravana = "MapaMundo/";

    public sealed class Montada
    {
        public GameObject Raiz;
        public Renderer Suelo;
        public Texture2D Pisado;
        /// <summary>Los edificios que quedaron montados con su arte (por su índice en la planta): los demás, a cuenta de quien llama.</summary>
        public readonly HashSet<int> Hechos = new HashSet<int>();
        public readonly Dictionary<string, List<Renderer>> PiezasDe = new Dictionary<string, List<Renderer>>();

        // Lo que hace falta para ponerla al día sin rehacerla (`Actualizar`): cada pieza viva por su clave (qué, dónde y hacia dónde), los
        // trazos con que se pintó el suelo y sus capas, y la red por la que anda la gente.
        internal readonly Dictionary<(string, Vector3Int, int), (GameObject go, string id)> Vivas = new Dictionary<(string, Vector3Int, int), (GameObject, string)>();
        internal readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
        internal HashSet<(int tipo, Vector3 a, Vector3 b, float ancho)> Trazos = new HashSet<(int, Vector3, Vector3, float)>();
        internal float[] V, G, B;
        internal Color32[] Pixeles;
        internal Vector2 Centro;
        internal float Lado = -1;
        internal Quaternion? AlFrente;
        internal string FirmaDeLaRed;
        internal int Vecinos;
        internal bool ConVida;
    }

    /// <summary>Lo que no estorba al andar: lo verde menudo, los sembrados y los paños.</summary>
    static readonly HashSet<string> SinChoque = new HashSet<string> { "hierba_a", "hierba_b", "flores", "retama", "mata", "campo", "era", "bancal_coles", "bancal_habas", "bancal_barbecho", "bancal_cebollas", "bancal_lino", "estandarte", "cordel" };
    static readonly HashSet<string> Verdes = new HashSet<string> { "encina_b", "cipres", "olivo", "mata", "retama", "hierba_a", "hierba_b", "flores" };

    static readonly Vector3 Izq = Vector3.left, Der = Vector3.right, Norte = Vector3.forward, Sur = Vector3.back;

    /// <summary>Un solar mientras se compone: su centro, su giro, y adónde va lo que la receta pone.</summary>
    sealed class Obra
    {
        public Vector3 Centro;
        public Quaternion Giro;
        public int Ix, Iz;                                       // su casilla, para el azar
        public List<(Vector3 a, Vector3 b, float ancho)> Calles;
        public readonly List<(string modelo, Vector3 pos, Vector3 mira)> Piezas = new List<(string, Vector3, Vector3)>();
        public readonly List<(Vector3, Vector3)> Sendas = new List<(Vector3, Vector3)>();
        public readonly List<(Vector3, Vector3, float)> Explanadas = new List<(Vector3, Vector3, float)>();
        public readonly List<Vector3> Brasas = new List<Vector3>(), Fuegos = new List<Vector3>();
        public readonly List<(Vector3 centro, float radio)> Norias = new List<(Vector3, float)>();

        public Vector3 L(float x, float z, float y = 0) => Centro + Giro * new Vector3(x, y, z);
        public Vector3 D(Vector3 d) => Giro * d;
        public float Azar(int k) => RellenoDeCiudad.Azar(Ix, Iz, k);
        public void P(string modelo, float x, float z, Vector3 mira, float y = 0) => Piezas.Add((modelo, L(x, z, y), D(mira)));
        public void Senda(float x0, float z0, float x1, float z1) => Sendas.Add((L(x0, z0), L(x1, z1)));
        public void Explanada(float x0, float z0, float x1, float z1, float ancho) => Explanadas.Add((L(x0, z0), L(x1, z1), ancho));
        public void Ruedo(float x, float z, float radio) => Explanadas.Add((L(x, z), L(x, z), radio));
        /// <summary>La senda desde la calle más cercana hasta ese punto del solar.</summary>
        public void SendaDesdeCalle(float x, float z)
        {
            var p = L(x, z);
            if (Calles.Count > 0) Sendas.Add((MasCerca(Calles, p), p));
        }
        public bool CercaDeSenda(Vector3 p, float d) => Sendas.Any(s => DistanciaATramo(p, s.Item1, s.Item2) < d);
        /// <summary>Una choza con su ruedo pisado y la senda de su puerta.</summary>
        public void Choza(string modelo, float x, float z, Vector3 puerta, float hx, float hz)
        {
            P(modelo, x, z, puerta);
            Ruedo(x, z, 3.5f);
            Senda(x + puerta.x * 3, z + puerta.z * 3, hx, hz);
        }
        public void Cerca(float x, float z, Vector3 mira, string modelo = "cerca") => P(modelo, x, z, mira);
    }

    sealed class Receta
    {
        public float Ancho, Fondo;
        public Vector3 Frente;
        public Action<Obra> Poner;
    }

    /// <summary>
    /// El estilo (el nivel de la plaza) y la época (la Era de la Facción) de un edificio, de lo que manda el motor (`EstiloDeCiudad`).
    /// </summary>
    static (int estilo, int epoca) EstiloDe(SelectorDePersonaje.InteriorParaVer planta, SelectorDePersonaje.PiezaDePlanta e)
    {
        // ponytail: el estilo se mira al montar y al cambiar la planta, no cada fotograma: en un ascenso, las casas cambian con el siguiente
        // cambio de planta. Si se quiere continuo, volver a llamar a `Actualizar` cada cierto avance del ascenso.
        double ahora = planta.AhoraMundo?.Invoke() ?? 0;
        return (EstiloDeCiudad.EstiloDe(e.Id, e.Tipo, planta.NivelDePlaza, planta.NivelObjetivo, planta.AscensoDesdeMs, planta.AscensoHastaMs, ahora),
            EstiloDeCiudad.EpocaDe(e.Id, e.Tipo, planta.Era, planta.Grado));
    }

    /// <summary>
    /// La variante de algo (una receta, un modelo) para un estilo y una época: se busca «nombre@estilo.época» y «nombre@estilo», del estilo
    /// pedido hacia abajo (y, en cada uno, de la época pedida hacia abajo), y si no hay ninguna, el nombre a secas, que es el estilo 1 de la
    /// Era I. Así una plaza de nivel 3 se viste con lo del 2 mientras no exista lo suyo: nunca más pobre que el nivel anterior.
    /// </summary>
    static string Variante(string nombre, int estilo, int epoca, Func<string, bool> hay)
    {
        for (int s = estilo; s >= 1; s--)
        {
            for (int e = epoca; e >= 2; e--) if (hay($"{nombre}@{s}.{e}")) return $"{nombre}@{s}.{e}";
            if (s >= 2 && hay($"{nombre}@{s}")) return $"{nombre}@{s}";
        }
        return nombre;
    }

    static bool RecetaDe(string tipo, int estilo, int epoca, out Receta receta) => Recetas.TryGetValue(Variante(tipo, estilo, epoca, Recetas.ContainsKey), out receta);

    /// <summary>
    /// Las mudas: en qué se convierte una pieza del relleno, de los adornos o de una receta al subir de estilo o de época (Víctor,
    /// 2026-11-26: el relleno y los adornos mutan con el nivel y la Era, como los edificios). Cada pieza se viste con el estilo y la época
    /// del edificio al que pertenece; lo que no tiene dueño, con los de la plaza. Clave «pieza@estilo» o «pieza@estilo.época».
    /// </summary>
    static readonly Dictionary<string, string> Mudas = new Dictionary<string, string>
    {
        // estilo 2, Era I: del zarzo al barro
        ["cerca@2"] = "tapia_baja", ["cerca_palos@2"] = "tapia_baja", ["cerca_vieja@2"] = "tapia_rota", ["portada@2"] = "tapia_porton",
        ["banco@2"] = "poyo_barro", ["gallinero@2"] = "palomar",
    };

    static string Vestir(string modelo, int estilo, int epoca)
    {
        if (estilo <= 1 && epoca <= 1 || modelo.Length == 0 || modelo[0] == '@' || modelo.IndexOf('|') >= 0) return modelo;
        var clave = Variante(modelo, estilo, epoca, Mudas.ContainsKey);
        return Mudas.TryGetValue(clave, out var muda) ? muda : modelo;
    }

    static readonly Dictionary<string, Receta> Recetas = new Dictionary<string, Receta>
    {
        // El sitio de reunión (Centro Urbano, 4×4 celdas): la casa larga del jefe al fondo con su porche al corro de la asamblea; el corro de
        // piedras hincadas con el hogar encendido en medio, los bancos delante y el asiento del jefe de espaldas al porche; dos estandartes;
        // la encina grande y el pozo en las esquinas de delante; el altar junto a la casa; y lo del clan: dos chozas, el secadero, los cestos.
        ["centroUrbano"] = new Receta { Ancho = 36, Fondo = 36, Frente = Sur, Poner = o =>
        {
            const float Corro = 6.9f;
            o.P("casa_jefe", 0, 10.5f, Sur); o.P("hogar", 0, -6, Der); o.P("asiento", 0, -0.7f, Sur);
            o.P("estandarte", -5.4f, 1.3f, Sur); o.P("estandarte", 5.4f, 1.3f, Sur);
            o.P("encina", 13, -12.5f, Sur); o.P("pozo", -13.8f, -13.8f, Sur); o.P("altar", 11.5f, 12.5f, Izq);
            o.P("secadero", -8.5f, -6.5f, new Vector3(1, 0, 0.4f).normalized); o.P("cestos", -3.6f, 0.6f, Sur);
            o.P("cestos", -9.6f, 6.6f, Der); o.P("molino_de_mano", -8.8f, 0.5f, Der);
            o.P("banco", 11.2f, -9.6f, new Vector3(-1, 0, -0.6f).normalized);
            for (int k = 0; k < 5; k++)
            {
                float a = Mathf.Deg2Rad * (200 + 35 * k);
                var fuera = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                o.P("banco", fuera.x * 4.7f, -6 + fuera.z * 4.7f, -fuera);
            }
            // el corralito de zarzo al borde y la cerca del huerto delante
            foreach (var dz in new[] { -1.5f, 1.5f }) foreach (var dx in new[] { -1.5f, 1.5f }) o.Cerca(15.2f + dx, 1.5f + dz, Der);
            foreach (var dz in new[] { -3f, 3f }) o.Cerca(15.2f, 1.5f + dz, Sur);
            foreach (var dx in new[] { -3f, 0f, 3f }) o.Cerca(3.5f + dx, -16.3f + 1.3f, Sur);
            foreach (var dx in new[] { -4.5f, 4.5f }) o.Cerca(3.5f + dx, -16.3f - 0.2f, Der);
            o.Choza("choza_b", -12.8f, 10.5f, Der, -5.5f, 4f);
            o.Choza("choza_c", -13.2f, -2.5f, Der, 0, -6);
            o.SendaDesdeCalle(0, -6);
            o.Senda(0, -6, 11.5f, 10.5f); o.Senda(0, -6, -12.4f, -12.4f); o.Senda(0, -6, 12.7f, 1.5f); o.Senda(0, -6, 11, -10.5f);
            o.Explanada(0, 1, 0, 17, 4.6f);
            o.Ruedo(0, -6, Corro - 0.4f);
            // las piedras hincadas del corro, salvo donde pasa una senda o está el porche
            for (int k = 0; k < 16; k++)
            {
                float a = 2 * Mathf.PI * (k + 0.5f) / 16;
                float x = Mathf.Cos(a) * (Corro + 0.5f), z = -6 + Mathf.Sin(a) * (Corro + 0.5f);
                if (o.CercaDeSenda(o.L(x, z), 2.1f) || Mathf.Abs(x) < 5.2f && z > -6) continue;
                o.P("menhir_" + "abc"[k % 3], x, z, new Vector3(-x, 0, -6 - z));
            }
            o.Fuegos.Add(o.L(0, -6, 0.35f));
        } },
        // La vivienda (1×1): una choza de las tres, con la puerta a la calle.
        ["vivienda"] = new Receta { Ancho = 9, Fondo = 9, Frente = Der, Poner = o =>
        {
            o.P("choza_" + "abc"[(int)(o.Azar(41) * 2.999f)], 0, 0, Der);
            o.Ruedo(0, 0, 3.5f);
            o.SendaDesdeCalle(3, 0);
        } },
        // La vivienda del estilo 2 (la plaza de nivel 2, Era I): como en el prototipo, algo más de la mitad son casas largas de barro (una de
        // cada tres, con ábside), una cuarta parte siguen siendo chozas y una de cada cinco es la primera casa de adobe, de terrado.
        ["vivienda@2"] = new Receta { Ancho = 9, Fondo = 9, Frente = Der, Poner = o =>
        {
            float k = o.Azar(43);
            if (k < 0.36f) o.P("casa_barro", -0.5f, -0.6f, Der);
            else if (k < 0.55f) o.P("casa_abside", 1.0f, 0, Der);
            else if (k < 0.8f) o.P("choza_" + "abc"[(int)(o.Azar(41) * 2.999f)], 0, 0, Der);
            else o.P("casa_adobe", -1.0f, 0, Der);
            o.Ruedo(0, 0, 3.5f);
            o.SendaDesdeCalle(3.6f, 0);
        } },
        // La granja (2×2): el campo labrado a medio segar; en la franja de la entrada, la choza del campo, el almiar y la era; el arado, el
        // espantapájaros, y la cerca de zarzo alrededor, con su paso.
        ["granja"] = new Receta { Ancho = 18, Fondo = 18, Frente = Izq, Poner = o =>
        {
            o.P("campo", 2.5f, 0, Izq); o.P("almiar", -6.4f, -6.2f, Sur); o.P("era", -6.2f, 0.4f, Sur);
            o.P("arado", -4.6f, 4.2f, new Vector3(0.3f, 0, 1).normalized); o.P("espantapajaros", 1.9f, -1.5f, Izq, 0.15f); o.P("cestos", -5.0f, 7.6f, Izq);
            for (int k = 0; k < 6; k++)
            {
                float d = -7.5f + 3 * k;
                o.Cerca(d, -9, Sur); o.Cerca(d, 9, Sur); o.Cerca(9, d, Der);
                if (k != 3) o.Cerca(-9, d, Der);
            }
            o.Choza("choza_b", -5.6f, 5.6f, Sur, -6.2f, 0.4f);
            o.SendaDesdeCalle(-9, 1.5f); o.Senda(-9, 1.5f, -6.2f, 0.4f);
            o.Explanada(-6.6f, -7, -6.6f, 7.5f, 1.6f);
        } },
        // El patio de gremios (2×2): dos soportales, el horno en medio y los bancos de trabajo debajo.
        ["patioDeGremios"] = new Receta { Ancho = 18, Fondo = 18, Frente = Der, Poner = o =>
        {
            foreach (var dx in new[] { -6.3f, 6.3f }) o.P("portico", dx, 0, Sur);
            o.P("horno_pan", 0, 1.5f, Der); o.Brasas.Add(o.L(1.3f, 1.5f, 1.0f));
            o.P("mesa_taller", -6.3f, -3.5f, Der); o.P("mesa_taller", -6.3f, 3.8f, Der); o.P("mesa_taller", 6.3f, -4.2f, Izq);
            o.P("cestos", 6.2f, 2.5f, Izq); o.P("sacos", -5.8f, 0.2f, Der); o.P("pila_lena", -0.5f, -3.2f, Sur);
            o.SendaDesdeCalle(4, 0);
            o.Explanada(0, -5.5f, 0, 5.5f, 7.6f);
        } },
        // El molino (2×2): la casa del molinero al fondo y, en el patio, la muela con el camino redondo que pisa el buey, y los sacos.
        ["molino"] = new Receta { Ancho = 18, Fondo = 18, Frente = Der, Poner = o =>
        {
            o.P("taller", -4.2f, -4.6f, Der); o.P("muela", 2.2f, 3.2f, new Vector3(0.4f, 0, -1).normalized);
            o.P("sacos", 2.4f, -5.6f, Der); o.P("cestos", 1.6f, -7.4f, Sur); o.P("cobertizo", -5.6f, 4.8f, Der); o.P("sacos", -5.8f, 4.6f, Sur);
            o.Norias.Add((o.L(2.2f, 3.2f), 4.1f));
            for (int k = 0; k < 12; k++)
                o.Senda(2.2f + Mathf.Cos(k * Mathf.PI / 6) * 4.1f, 3.2f + Mathf.Sin(k * Mathf.PI / 6) * 4.1f, 2.2f + Mathf.Cos((k + 1) * Mathf.PI / 6) * 4.1f, 3.2f + Mathf.Sin((k + 1) * Mathf.PI / 6) * 4.1f);
            o.SendaDesdeCalle(2.2f, 3.2f); o.SendaDesdeCalle(-1.2f, -4.6f);
            o.Explanada(-1, -5.5f, 3, -5.5f, 1.6f);
        } },
        // El horno (2×1): la tahona al fondo, los dos hornos en el patio con la boca a la calle, la mesa del pan y la leña.
        ["horno"] = new Receta { Ancho = 18, Fondo = 9, Frente = Izq, Poner = o =>
        {
            o.P("taller", 3.9f, 0.2f, Izq);
            foreach (var dz in new[] { -2.0f, 2.0f }) { o.P("horno_pan", -4.2f, dz, Izq); o.Brasas.Add(o.L(-5.5f, dz, 1.0f)); }
            o.P("mesa_pan", -7.2f, 0, Izq); o.P("pila_lena", -0.6f, 3.4f, Sur); o.P("sacos", -0.2f, -3.2f, Izq);
            o.SendaDesdeCalle(-3, 0);
            o.Explanada(-6.5f, 0, 1, 0, 3.2f);
        } },
        // La leñera (1×1): el cobertizo con la leña debajo, el tajo y otra pila fuera.
        ["lenera"] = new Receta { Ancho = 9, Fondo = 9, Frente = Izq, Poner = o =>
        {
            o.P("cobertizo", 0.6f, -1.6f, Der); o.P("pila_lena", 0.2f, -2.3f, Sur); o.P("pila_lena", 0.9f, -0.9f, Sur);
            o.P("tajo", -0.6f, 2.6f, Sur); o.P("pila_lena", 3.2f, 2.6f, Der);
            o.SendaDesdeCalle(0, 0);
            o.Explanada(-2, 1.5f, 2.5f, 1.5f, 2.2f);
        } },
        // El almacén (2×1): la nave de tablas, con el portón en el costado largo.
        ["almacen"] = new Receta { Ancho = 18, Fondo = 9, Frente = Norte, Poner = o =>
        {
            o.P("almacen", 0, -0.6f, Izq);
            o.SendaDesdeCalle(0, 4.2f);
            o.Explanada(-6, 4, 6, 4, 1.0f);
        } },
        // El mercado (3×2): doce puestos en corro, mirando adentro, y en medio dos carros y un estandarte.
        ["mercado"] = new Receta { Ancho = 27, Fondo = 18, Frente = Izq, Poner = o =>
        {
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 3; j++)
                {
                    if (i > 0 && i < 4 && j == 1) continue;
                    o.P("puesto_" + "abc"[(i * 2 + j) % 3], -10.8f + 5.4f * i, -6 + 6 * j, j == 1 ? (i == 0 ? Der : Izq) : (j == 0 ? Norte : Sur));
                }
            o.P("Caravana/Cart_Basic", -3.5f, 0.6f, new Vector3(1, 0, 0.3f).normalized); o.P("Caravana/Cart_Basic", 4.2f, -0.4f, new Vector3(-0.3f, 0, -1).normalized);
            o.P("estandarte", 0, 0, Izq); o.P("sacos", 1.6f, 1.6f, Sur);
            o.SendaDesdeCalle(0, 0);
            o.Explanada(-9, 0, 9, 0, 6.5f);
        } },
        // El granero (4×2): el solar cercado de zarzo con su portada; dentro, el granero mayor en medio con su corredor y su escalera, uno
        // pequeño a cada lado, las tinajas enterradas, el secadero de la mies y el carro a medio descargar.
        ["granero"] = new Receta { Ancho = 36, Fondo = 18, Frente = Izq, Poner = o =>
        {
            for (int k = 0; k < 12; k++) { o.Cerca(-16.5f + 3 * k, -9, Sur); o.Cerca(-16.5f + 3 * k, 9, Sur); }
            for (int k = 0; k < 6; k++) { o.Cerca(18, -7.5f + 3 * k, Der); o.Cerca(-18, -7.5f + 3 * k, Izq, k == 2 ? "portada" : "cerca"); }
            o.P("granero_grande", 0, 3.6f, Sur);
            foreach (var dx in new[] { -10.5f, 10.5f }) o.P("granero_pilotes", dx, 4.2f, Sur);
            o.P("tinajas_enterradas", -10, -4.6f, Sur); o.P("secadero_mies", 10.5f, -4.8f, Sur);
            o.P("Caravana/Cart_Basic", 3.2f, -4.6f, new Vector3(-0.5f, 0, -1).normalized);
            o.P("sacos", 5.6f, -1.2f, Sur); o.P("cestos", -4.2f, -0.6f, Sur); o.P("almiar", 15, 0.5f, Sur);
            o.SendaDesdeCalle(-18, -1.5f); o.Senda(-18, -1.5f, -6, -1.5f);
            o.Explanada(-13.5f, -1.5f, 13.5f, -1.5f, 4.2f);
        } },
        // El patio de caravanas (2×3: 18 m a la calle y 27 de fondo): el solar cercado con su portada; el cobertizo de los carros contra una
        // cerca, la casa de los arrieros al fondo, el abrevadero y el atadero con los asnos, lo descargado y un carro con su lona.
        ["albergueCaravanas"] = new Receta { Ancho = 27, Fondo = 18, Frente = Der, Poner = o =>
        {
            for (int k = 0; k < 9; k++) { o.Cerca(-12 + 3 * k, -9, Sur); o.Cerca(-12 + 3 * k, 9, Sur); }
            for (int k = 0; k < 6; k++) { o.Cerca(-13.5f, -7.5f + 3 * k, Izq); o.Cerca(13.5f, -7.5f + 3 * k, Der, k == 3 ? "portada" : "cerca"); }
            o.P("cobertizo_carros", -0.5f, -6.1f, Izq);
            o.P("Caravana/Cart_Basic", -9, -5.6f, new Vector3(0.08f, 0, 1).normalized); o.P("Caravana/Cart_Basic", -3, -5.4f, Norte);
            o.P("Caravana/Cart_Basic", 6, -5.7f, new Vector3(-0.06f, 0, 1).normalized);
            o.P("casa_arrieros", -9.9f, 3.5f, Norte); o.P("fardos", -4.4f, 1.6f, Der);
            o.P("abrevadero", -2.5f, 7.4f, Sur); o.P("Caravana/Donkey_Pack", -3.1f, 5.9f, new Vector3(0.1f, 0, 1).normalized);
            o.P("atadero", 3.6f, 7.6f, Sur); o.P("Caravana/Donkey_Pack", 3.1f, 6.2f, new Vector3(-0.15f, 0, 1).normalized);
            o.P("Caravana/Donkey_Pack", 6.2f, 1.3f, new Vector3(1, 0, 0.25f).normalized);
            o.P("Caravana/Cart_Reinforced", 10.2f, -3.4f, new Vector3(1, 0, 0.6f).normalized); o.P("Caravana/Ox_Draught", 5.2f, -2.2f, new Vector3(1, 0, 0.6f).normalized);
            o.P("Caravana/Cart_Basic", 1.8f, 0.2f, new Vector3(1, 0, 0.25f).normalized); o.P("sacos", 0.6f, 2.3f, Der);
            o.P("pesebre", 7.6f, 7.5f, Sur); o.P("almiar", 11, 6.6f, Sur); o.P("pila_lena", -6.6f, 7.4f, Sur);
            o.P("banco", -3.6f, -1.6f, new Vector3(-0.3f, 0, 1).normalized);
            o.SendaDesdeCalle(13.5f, 1.5f); o.Senda(13.5f, 1.5f, 4, 0.5f);
            o.Explanada(-5, 1.4f, 10.5f, 1.4f, 6.6f); o.Explanada(-11, -5.6f, 10.5f, -5.6f, 3.0f);
        } },
        // El barracón (2×2): la casa larga de la tropa a un lado y, en el patio de armas, los postes de práctica, los armeros y el estandarte.
        ["barracon"] = new Receta { Ancho = 18, Fondo = 18, Frente = Der, Poner = o =>
        {
            o.P("barracon_casa", -4.7f, 1.2f, Sur);
            for (int k = 0; k < 4; k++) o.P("poste_practica", 2.6f + (k % 2) * 1.6f, -5.6f + k * 3.6f, k % 2 == 0 ? Der : Izq);
            o.P("armero", 7.3f, 2.5f, Izq); o.P("armero", 7.3f, -3.0f, Izq); o.P("estandarte", 7.4f, 7.3f, Izq); o.P("banco", 1.2f, -7.6f, Norte);
            o.SendaDesdeCalle(8.5f, -0.3f);
            o.Explanada(4.2f, -7, 4.2f, 7, 3.6f);
        } },
        // La galería de tiro (2×4, a lo largo): la calle de tiro cercada de estacas, el puesto del tirador en una punta y las dianas en la otra.
        ["galeriaDeTiro"] = new Receta { Ancho = 18, Fondo = 36, Frente = Sur, Poner = o =>
        {
            for (int k = 0; k < 12; k++) { o.Cerca(-8.7f, -16.5f + 3 * k, Izq, "cerca_palos"); o.Cerca(8.7f, -16.5f + 3 * k, Der, "cerca_palos"); }
            for (int k = 0; k < 6; k++) o.Cerca(-7.2f + 3 * k, 17.6f, Norte, "cerca_palos");
            foreach (var x in new[] { -5.4f, -1.8f, 1.8f, 5.4f }) o.P("diana", x, 15.2f, Sur);
            o.P("cobertizo", 0, -13.6f, Norte); o.P("armero", -5.6f, -13.2f, Norte); o.P("haces_paja", 6.3f, 12.2f, Sur); o.P("banco", 5.2f, -14.4f, Norte);
            o.SendaDesdeCalle(0, -17.5f);
            o.Explanada(0, -15, 0, 12.5f, 6.6f);
        } },
        // Las caballerizas (3×2): la cuadra larga al fondo, abierta al cercado, y en él el abrevadero, el heno y los caballos.
        ["caballerizas"] = new Receta { Ancho = 27, Fondo = 18, Frente = Sur, Poner = o =>
        {
            o.P("cuadra", 0, 5.6f, Der);
            for (int k = 0; k < 9; k++) if (k != 4) o.Cerca(-12 + 3 * k, -8.7f, Sur, "cerca_palos");
            o.Cerca(0, -8.7f, Sur, "portada");
            for (int k = 0; k < 4; k++) { o.Cerca(-13.3f, -7.2f + 3 * k, Izq, "cerca_palos"); o.Cerca(13.3f, -7.2f + 3 * k, Der, "cerca_palos"); }
            o.P("abrevadero", -7.5f, -5.5f, Sur); o.P("almiar", 10, -5.2f, Sur); o.P("pesebre", 4.5f, -6.8f, Sur);
            o.P(PseudoCaballo, -4, -1.5f, Der); o.P(PseudoCaballo, 3.5f, -3.2f, Izq); o.P(PseudoCaballo, 8, 0.2f, Sur);
            o.SendaDesdeCalle(0, -8.7f);
            o.Explanada(-10, -2.6f, 10, -2.6f, 5.2f);
        } },
        // La armería (2×3): el arsenal de tablas al fondo, con su porche de armas; a su lado el cobertizo del fundidor, con el horno de cuba
        // delante, al raso; y en el patio, frente a la puerta, la panoplia con lo hecho. (Antes: el taller genérico, una fragua de un metro
        // perdida en el patio y dos armeros; nada decía «armería». Víctor, 2026-11-26.)
        ["armeria"] = new Receta { Ancho = 18, Fondo = 27, Frente = Sur, Poner = o =>
        {
            o.P("arsenal", -4.3f, 6.6f, Sur);
            o.P("fundicion", 5.2f, 7.0f, Sur); o.Brasas.Add(o.L(5.2f, 1.7f, 1.8f));
            o.P("panoplia", -4.3f, -5.2f, Sur); o.P("estandarte", 0.4f, -1.6f, Sur);
            o.P("armero", 7.3f, -6.0f, Izq); o.P("pila_lena", 8.0f, -2.2f, Izq); o.P("tinajas", -8.0f, -0.8f, Der);
            o.SendaDesdeCalle(0, -13); o.Senda(0, -13, 0, -7);
            o.Explanada(-4.3f, -2.6f, 5.2f, -2.6f, 4.4f);
        } },
        // La fundición (2×2): el taller del fundidor al fondo, a un agua; delante, al raso, los dos hornos con su foso de colada; a un lado,
        // el carbón, el mineral y los lingotes.
        ["fundicion"] = new Receta { Ancho = 18, Fondo = 18, Frente = Sur, Poner = o =>
        {
            o.P("cobertizo_fundidor", -2.2f, 5.6f, Sur);
            o.P("hornos_fundicion", -2.2f, -1.4f, Sur); o.Brasas.Add(o.L(-5.1f, -1.0f, 1.8f)); o.Brasas.Add(o.L(0.7f, -1.0f, 1.8f));
            o.P("acopio_fundicion", 6.0f, 5.2f, Izq); o.P("machaqueo_mineral", 5.6f, -0.8f, Izq); o.P("muestra_fundidor", 3.6f, -6.6f, Sur);
            o.P("pila_lena", -7.9f, -3.4f, Der); o.P("tinajas", -7.6f, -6.8f, Der);
            o.SendaDesdeCalle(0, -8.5f);
            o.Explanada(-5, -3.6f, 3.5f, -3.6f, 3.6f);
        } },
        // La curtiduría (2×2): el secadero con las pieles colgadas, las seis tinas de los caldos y, hacia la calle, las pieles tensadas al sol.
        ["curtiduria"] = new Receta { Ancho = 18, Fondo = 18, Frente = Sur, Poner = o =>
        {
            o.P("secadero_pieles", -4.6f, 2.6f, Sur);
            o.P("tinas_curtir", 4.2f, 3.2f, Sur);
            o.P("piel_tensada", 1.6f, -5.8f, Sur); o.P("piel_tensada", 4.6f, -6.2f, Sur); o.P("piel_tensada", 7.4f, -5.6f, Sur);
            o.P("tinajas", 7.8f, 8.0f, Izq); o.P("pila_lena", -0.6f, 8.0f, Sur);
            o.SendaDesdeCalle(0, -8.5f);
            o.Explanada(-2, -3.6f, 5, -3.6f, 3.0f);
        } },
        // La taberna (2×2): media casa y medio porche hondo con las mesas debajo; fuera, más mesas, las tinajas enterradas y la lumbre.
        ["taberna"] = new Receta { Ancho = 18, Fondo = 18, Frente = Sur, Poner = o =>
        {
            o.P("taberna_casa", -3.2f, 5.2f, Sur);
            o.P("mesa_taberna", 4.6f, -4.6f, Sur); o.P("mesa_taberna", 5.6f, 0.4f, Der);
            o.P("tinajas_enterradas", 5.4f, 6.4f, Sur); o.P("pila_lena", -8.0f, -6.6f, Der);
            o.P("hogar", 1.4f, -6.6f, Sur); o.Fuegos.Add(o.L(1.4f, -6.6f, 0.25f));
            o.SendaDesdeCalle(-3.2f, -8.5f);
            o.Explanada(-1, -5.2f, 5, -3.0f, 3.4f);
        } },
        // La Sala del Consejo (3×3): la sala alta de tablas con sus galerías, al fondo; delante, el corro de los asientos de piedra con su
        // lumbre baja; a un lado, el tambor de llamar, y los estandartes de los clanes.
        ["salaConsejo"] = new Receta { Ancho = 27, Fondo = 27, Frente = Sur, Poner = o =>
        {
            o.P("sala_consejo", 0, 6.2f, Sur);
            o.P("corro_consejo", 0, -7.0f, Sur); o.Brasas.Add(o.L(0, -7.0f, 0.25f));
            o.P("tambor_llamada", 8.6f, -2.6f, new Vector3(-1, 0, -0.5f).normalized);
            o.P("estandarte", -7.6f, -2.2f, Sur); o.P("estandarte", -9.6f, -9.4f, Sur); o.P("estandarte", 9.6f, -9.4f, Sur);
            o.P("encina", -10.6f, 9.6f, Sur); o.P("banco", 10.6f, 4.0f, Izq); o.P("tinajas", 9.8f, 10.6f, Izq);
            o.SendaDesdeCalle(0, -12.8f);
            o.Ruedo(0, -7.0f, 5.9f);
            o.Explanada(0, -1.5f, 0, 0.5f, 4.2f);
        } },
        // El palacio (4×4), que en la Era I es la gran casa del señor en su recinto de estacas: la casa al fondo con su estrado en el porche,
        // el granero en pilotes, la choza de la servidumbre, el pozo, las tinajas y, en medio del patio, la lumbre con sus bancos.
        ["palacio"] = new Receta { Ancho = 36, Fondo = 36, Frente = Sur, Poner = o =>
        {
            o.P("palacio_casa", -1.5f, 6.5f, Sur);
            o.P("asiento", -1.5f, -3.0f, Sur); o.P("estandarte", -8.2f, -6.4f, Sur); o.P("estandarte", 5.2f, -6.4f, Sur);
            o.P("hogar", -1.5f, -10.6f, Sur); o.Fuegos.Add(o.L(-1.5f, -10.6f, 0.35f));
            for (int k = 0; k < 4; k++)
            {
                float a = Mathf.Deg2Rad * (215 + 37 * k);
                var fuera = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                o.P("banco", -1.5f + fuera.x * 4.4f, -10.6f + fuera.z * 4.4f, -fuera);
            }
            o.P("granero_pilotes", -13.0f, 9.5f, Der); o.P("choza_b", 12.6f, -8.6f, Izq); o.P("pozo", 13.0f, 3.2f, Sur);
            o.P("tinajas_enterradas", -13.2f, -2.0f, Der); o.P("pila_lena", -14.6f, -10.6f, Der); o.P("encina", 13.6f, 13.2f, Sur);
            // el recinto: la cerca de estacas todo alrededor, con su portada al frente
            for (int k = -5; k <= 5; k++)
            {
                o.Cerca(k * 3, 16.6f, Sur, "cerca_palos"); o.Cerca(-16.6f, k * 3, Der, "cerca_palos"); o.Cerca(16.6f, k * 3, Der, "cerca_palos");
                if (k != 0) o.Cerca(k * 3, -16.6f, Sur, "cerca_palos");
            }
            o.P("portada", 0, -16.6f, Sur);
            o.SendaDesdeCalle(0, -17.5f); o.Senda(0, -17.5f, -1.5f, -10.6f); o.Senda(-1.5f, -10.6f, 12.6f, -6); o.Senda(-1.5f, -10.6f, -12, 6);
            o.Ruedo(-1.5f, -10.6f, 5.4f);
            o.Explanada(-1.5f, -6, -1.5f, -2.5f, 5.2f);
        } },
        // La carpintería (4×2): el taller y el cobertizo, las pilas de troncos, los bancos de trabajo y la leña hecha.
        ["carpinteria"] = new Receta { Ancho = 36, Fondo = 18, Frente = Sur, Poner = o =>
        {
            o.P("taller", -10.5f, 4.4f, Sur); o.P("cobertizo", 1.5f, 4.6f, Sur);
            o.P("pila_troncos", 11, 5.4f, Sur); o.P("pila_troncos", 12, 0.6f, new Vector3(0.2f, 0, -1).normalized);
            o.P("mesa_taller", 1.2f, 4.2f, Sur); o.P("mesa_taller", -3.4f, -3.2f, Sur); o.P("tajo", 5.2f, -2.8f, Sur); o.P("pila_lena", -15.6f, -4.5f, Der);
            o.P("tocon", 8.5f, -5.2f, Sur);
            o.SendaDesdeCalle(0, -8.5f);
            o.Explanada(-9, -3, 9, -3, 4.4f);
        } },
        ["pozo"] = new Receta { Ancho = 9, Fondo = 9, Frente = Der, Poner = o => { o.P("pozo", 0, 0, Der); o.Ruedo(0, 0, 2.2f); o.SendaDesdeCalle(0, 0); } },
    };

    public static bool TieneReceta(string tipo) => Recetas.ContainsKey(tipo);

    public static float DistanciaATramo(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        float l = ab.sqrMagnitude;
        return (p - (l < 1e-6f ? a : a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / l))).magnitude;
    }

    /// <summary>El punto de las calles (por su eje) más cercano a `p`.</summary>
    static Vector3 MasCerca(List<(Vector3 a, Vector3 b, float ancho)> calles, Vector3 p)
    {
        Vector3 mejor = p;
        float dm = float.MaxValue;
        foreach (var (a, b, _) in calles)
        {
            var ab = b - a;
            float l = ab.sqrMagnitude;
            var q = l < 1e-6f ? a : a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / l);
            float d = (p - q).sqrMagnitude;
            if (d < dm) { dm = d; mejor = q; }
        }
        return mejor;
    }

    static readonly string[] Acopios = { "pila_troncos", "pila_piedras", "haces_paja", "monton_barro", "adobes" };

    /// <summary>Un solar con su edificio en cola o en construcción (sea del tipo que sea): replanteado con jalones y cordel por el borde de
    /// su huella, con el acopio dentro; y, si ya está en obra, el armazón a medio vestir (el de una choza en un solar pequeño, el de una nave
    /// en uno grande), el andamio y más acopio, sobre la tierra ya pisada.</summary>
    static void EnObra(SelectorDePersonaje.PiezaDePlanta e, Func<double, double, float, Vector3> A, int k, List<(string, Vector3, Vector3, string)> piezas, List<(Vector3, Vector3, float)> explanadas)
    {
        var c = A(e.X, e.Y, 0);
        float mx = (float)e.Ancho * Metros / 2, mz = (float)e.Alto * Metros / 2;
        int sx = Mathf.RoundToInt((float)e.X * 2), sz = Mathf.RoundToInt((float)e.Y * 2);
        float H(int n) => RellenoDeCiudad.Azar(sx, sz, 900 + n);
        void Pon(string modelo, Vector3 pos, Vector3 mira) => piezas.Add((modelo, pos, mira, e.Id));
        Vector3 Giro(int n) { float a = H(n) * 6.2832f; return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); }
        // el replanteo: un tramo de cordel cada 3 m por los cuatro lados (falta alguno: por ahí entran los carros)
        int nx = Mathf.Max(1, Mathf.RoundToInt(mx * 2 / 3)), nz = Mathf.Max(1, Mathf.RoundToInt(mz * 2 / 3));
        for (int i = 0; i < nx; i++)
            foreach (var lado in new[] { -1, 1 })
                if (H(i * 2 + lado + 3) > 0.14f) Pon("cordel", c + new Vector3(-mx + (i + 0.5f) * (2 * mx / nx), 0, lado * mz), Vector3.forward * lado);
        for (int j = 0; j < nz; j++)
            foreach (var lado in new[] { -1, 1 })
                if (H(40 + j * 2 + lado) > 0.14f) Pon("cordel", c + new Vector3(lado * mx, 0, -mz + (j + 0.5f) * (2 * mz / nz)), Vector3.right * lado);
        bool obra = e.Estado == "obra";
        bool grande = Mathf.Max(mx, mz) > 5.5f;
        if (obra)
        {
            explanadas.Add((c + Vector3.left * Mathf.Max(0, mx - 3), c + Vector3.right * Mathf.Max(0, mx - 3), Mathf.Min(mx, mz) - 0.5f));
            // el armazón, hacia el fondo del solar, y el andamio a su lado
            var eje = mx >= mz ? Vector3.right : Vector3.forward;
            Pon(grande ? "armazon_nave" : "armazon_choza", c + (grande ? eje * (Mathf.Max(mx, mz) * 0.2f) : Vector3.zero), grande ? Vector3.Cross(Vector3.up, eje) : Giro(80));
            if (grande) Pon("andamio", c - eje * (Mathf.Max(mx, mz) * 0.35f) + Vector3.Cross(Vector3.up, eje) * (Mathf.Min(mx, mz) * 0.3f), eje);
        }
        // el acopio: en las esquinas de dentro (en cola, dos montones; en obra, tres o cuatro)
        int cuantos = obra ? (grande ? 4 : 2) : 2;
        for (int n = 0; n < cuantos; n++)
        {
            var esquina = new Vector3((n % 2 == 0 ? -1 : 1) * Mathf.Max(0.5f, mx - 2.4f), 0, (n < 2 ? -1 : 1) * Mathf.Max(0.5f, mz - 2.2f));
            if (!grande) esquina *= obra ? 1.0f : 0.5f;
            Pon(Acopios[(int)(H(60 + n) * Acopios.Length * 0.999f)], c + esquina, Giro(70 + n));
        }
    }

    /// <summary>Lo mismo, con la planta en tuplas (centro y medidas en unidades locales): para quien no ve los tipos del mundo (el banco de pruebas).</summary>
    public static Montada MontarDeTuplas(IEnumerable<(string tipo, double x, double y, double ancho, double alto)> edificios,
        IEnumerable<(string tipo, double x, double y, double ancho, double alto)> calles, Func<double, double, float, Vector3> A, Transform padre, bool alDia = false,
        IEnumerable<(string clase, double x, double y, string estado)> muralla = null, int nivel = 1)
    {
        var planta = new SelectorDePersonaje.InteriorParaVer();
        foreach (var e in edificios)
            {
            // (el estado, tras dos puntos: «vivienda:cola», «almacen:obra»)
            var partes = e.tipo.Split(':');
            planta.Edificios.Add(new SelectorDePersonaje.PiezaDePlanta { Tipo = partes[0], Estado = partes.Length > 1 ? partes[1] : null, Id = "ed-" + planta.Edificios.Count, X = e.x, Y = e.y, Ancho = e.ancho, Alto = e.alto });
        }
        foreach (var c in calles) planta.Calles.Add(new SelectorDePersonaje.PiezaDePlanta { Tipo = c.tipo, X = c.x, Y = c.y, Ancho = c.ancho, Alto = c.alto });
        if (muralla != null)
            foreach (var c in muralla) planta.Muralla.Add(new SelectorDePersonaje.PiezaDePlanta { Tipo = c.clase, X = c.x, Y = c.y, Ancho = 3, Alto = 3, Estado = c.estado, Nivel = Math.Min(nivel, NivelDeMurallaConArte) });
        planta.NivelDePlaza = nivel;
        var m = Montar(planta, A, padre);
        if (!alDia) return m;
        // (para comprobar la puesta al día: la misma planta otra vez no cambia nada; luego, la primera obra se acaba)
        Actualizar(m, planta, A);
        for (int k = 0; k < planta.Edificios.Count; k++)
            if (planta.Edificios[k].Estado == "obra") { var e = planta.Edificios[k]; e.Estado = null; planta.Edificios[k] = e; break; }
        Actualizar(m, planta, A);
        return m;
    }

    /// <summary>
    /// Monta la ciudad bajo `padre`. `A(x, y, alto)`: de unidades locales de la planta a metros del mundo (x a +x, y a −z, a
    /// <see cref="Metros"/> por unidad). Los edificios en cola o en obra, y los que aún no tienen receta, no se montan aquí.
    /// </summary>
    public static Montada Montar(SelectorDePersonaje.InteriorParaVer planta, Func<double, double, float, Vector3> A, Transform padre, int semilla = 1)
    {
        var m = new Montada { Raiz = new GameObject("Ciudad por dentro") };
        m.Raiz.transform.SetParent(padre, false);
        Actualizar(m, planta, A, semilla);
        return m;
    }

    const string PseudoFuego = "@fuego", PseudoBrasa = "@brasa", PseudoNoria = "@noria", PseudoCaballo = "@caballo";
    /// <summary>Hasta qué nivel de muralla hay arte (1, la empalizada): las de más nivel las pinta quien llama, como pueda.</summary>
    public const int NivelDeMurallaConArte = 2;

    /// <summary>
    /// La empalizada, celda a celda (cada una de 3 u.l.: muro, torre o puerta). El lienzo dobla en esquina y en diagonal, así que cada celda
    /// en pie lleva su poste en el centro y MEDIO tramo hacia cada vecina (las de lado y, en diagonal, solo las que no une ya una de lado; el
    /// medio tramo en diagonal se estira a su largo). La torre va justo por dentro; la puerta, con sus alas, ocupa su celda entera. Lo
    /// comprometido sin levantar se ve replanteado, con cordel. Fuera es lo que se aleja del centro de la plaza.
    /// </summary>
    static void Muralla(SelectorDePersonaje.InteriorParaVer planta, Func<double, double, float, Vector3> A, List<(string, Vector3, Vector3, string)> piezas)
    {
        var celdas = planta.Muralla.Where(c => c.Nivel <= NivelDeMurallaConArte).ToList();
        if (celdas.Count == 0) return;
        var centro = A(0, 0, 0);
        var donde = celdas.Select(c => A(c.X, c.Y, 0)).ToList();
        var indice = new Dictionary<(int, int), int>();
        for (int i = 0; i < celdas.Count; i++) indice[(Mathf.RoundToInt((float)(celdas[i].X / celdas[i].Ancho)), Mathf.RoundToInt((float)(celdas[i].Y / celdas[i].Alto)))] = i;
        Vector3 AlLado(Vector3 d) => new Vector3(-d.z, 0, d.x);          // un cuarto de vuelta a la izquierda (visto desde arriba)
        for (int i = 0; i < celdas.Count; i++)
        {
            var c = celdas[i];
            int ci = Mathf.RoundToInt((float)(c.X / c.Ancho)), cj = Mathf.RoundToInt((float)(c.Y / c.Alto));
            var vecinas = new List<int>();
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                if (indice.TryGetValue((ci + di, cj + dj), out int j)) vecinas.Add(j);
            foreach (var (di, dj) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                if (indice.TryGetValue((ci + di, cj + dj), out int j) && !indice.ContainsKey((ci + di, cj)) && !indice.ContainsKey((ci, cj + dj))) vecinas.Add(j);
            // por dónde va el muro en esta celda: entre sus dos vecinas más opuestas; fuera, la perpendicular que se aleja del centro
            Vector3 t = Vector3.right;
            float mas = -1;
            foreach (var x in vecinas) foreach (var y in vecinas) if ((donde[x] - donde[y]).sqrMagnitude > mas) { mas = (donde[x] - donde[y]).sqrMagnitude; t = (donde[x] - donde[y]).normalized; }
            if (vecinas.Count == 1) t = (donde[vecinas[0]] - donde[i]).normalized;
            var fuera = AlLado(t);
            if (Vector3.Dot(fuera, donde[i] - centro) < 0) fuera = -fuera;
            // (la puerta y la torre, a escuadra con su celda aunque el lienzo llegue en diagonal)
            var recto = Mathf.Abs(fuera.x) > Mathf.Abs(fuera.z) ? new Vector3(Mathf.Sign(fuera.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(fuera.z));
            if (c.Estado == "plan")
            {
                var eje = AlLado(recto);
                // (cada celda de muralla deja UNA pieza con el nombre de lo que es, tras la barra: por él la cuentan las pruebas y quien la busque)
                for (int k = -1; k <= 1; k++) piezas.Add((k == 0 ? "cordel|Muralla (sin levantar)" : "cordel", donde[i] + eje * (k * 3f), recto, null));
                continue;
            }
            // las piezas de su nivel: la empalizada (1) o el muro de adobe sobre zócalo de piedra (2), con los mismos ejes
            bool adobe = c.Nivel >= 2;
            string medioTramo = adobe ? "muro2_medio" : "empalizada_medio";
            if (c.Tipo == "puerta") { piezas.Add(((adobe ? "muro2_puerta" : "puerta_empalizada") + "|Puerta de la muralla", donde[i], recto, null)); continue; }
            if (adobe)
            {
                // el bastión ocupa su celda y sale hacia fuera; el machón tapa la junta de los lienzos
                piezas.Add((c.Tipo == "torre" ? "muro2_torre|Torre" : "muro2_poste|Muralla", donde[i], c.Tipo == "torre" ? recto : fuera, null));
                if (c.Tipo == "torre") piezas.Add(("muro2_poste", donde[i], fuera, null));   // (en una esquina, el bastión mira a un lado: el machón cierra el otro)
            }
            else
            {
                piezas.Add((c.Tipo == "torre" ? "empalizada_poste" : "empalizada_poste|Muralla", donde[i], fuera, null));
                if (c.Tipo == "torre") piezas.Add(("torre_vigia|Torre", donde[i] - recto * 2.6f, recto, null));
            }
            foreach (var j in vecinas)
            {
                var d = donde[j] - donde[i];
                float medio = d.magnitude / 2;
                d /= medio * 2;
                var n = AlLado(d);
                if (Vector3.Dot(n, (donde[i] + donde[j]) / 2 - centro) < 0) n = -n;
                // el modelo va de x = 0 a 4,5 con «fuera» hacia su frente; su +X cae un cuarto de vuelta a la izquierda de adonde mira: si eso
                // es hacia la vecina, nace en esta celda; si no, nace en el punto medio y viene hacia aquí
                bool haciaLaVecina = Vector3.Dot(AlLado(n), d) > 0;
                piezas.Add((medio > 5 ? medioTramo + "*" : medioTramo, haciaLaVecina ? donde[i] : donde[i] + d * medio, n, null));
            }
        }
    }

    /// <summary>
    /// Pone al día una ciudad ya montada con la planta nueva, SIN rehacerla (Víctor, 2026-11-26): se calcula entera lo que tendría que haber
    /// (es barato: son listas) y se compara con lo que hay. Las piezas que siguen igual, se quedan; las que sobran, se quitan; solo se crean las
    /// nuevas. El suelo se repinta solo en la caja de los trazos que han cambiado (una calle, una senda, un patio). Los vecinos y los animales
    /// siguen vivos donde estaban; si cambian las calles o las sendas, toman la red nueva. El primer montaje es el mismo camino, con todo nuevo.
    /// </summary>
    public static void Actualizar(Montada m, SelectorDePersonaje.InteriorParaVer planta, Func<double, double, float, Vector3> A, int semilla = 1)
    {
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        float y0 = A(0, 0, 0).y;
        m.Hechos.Clear();
        m.PiezasDe.Clear();

        // las calles, por su eje (a lo largo de su lado largo)
        var calles = new List<(Vector3 a, Vector3 b, float ancho)>();
        foreach (var c in planta.Calles)
        {
            bool aLoLargoDeX = c.Ancho >= c.Alto;
            double medio = (aLoLargoDeX ? c.Ancho : c.Alto) / 2 - Math.Min(c.Ancho, c.Alto) / 2;
            calles.Add(aLoLargoDeX ? (A(c.X - medio, c.Y, 0), A(c.X + medio, c.Y, 0), (float)c.Alto * Metros / 2) : (A(c.X, c.Y - medio, 0), A(c.X, c.Y + medio, 0), (float)c.Ancho * Metros / 2));
        }

        var piezas = new List<(string modelo, Vector3 pos, Vector3 mira, string id)>();
        var sendas = new List<(Vector3, Vector3)>();
        var explanadas = new List<(Vector3, Vector3, float)>();
        var brasas = new List<Vector3>();
        var fuegos = new List<Vector3>();
        var norias = new List<(Vector3, float)>();
        for (int k = 0; k < planta.Edificios.Count; k++)
        {
            var e = planta.Edificios[k];
            if (e.Estado != null)
            {
                EnObra(e, A, k, piezas, explanadas);
                m.Hechos.Add(k);
                continue;
            }
            var (estilo, epoca) = EstiloDe(planta, e);
            if (!RecetaDe(e.Tipo, estilo, epoca, out var receta)) continue;
            var o = new Obra { Centro = A(e.X, e.Y, 0), Calles = calles, Ix = Mathf.RoundToInt((float)e.X * 2), Iz = Mathf.RoundToInt((float)e.Y * 2) };
            // el giro: de los cuartos de vuelta con los que la receta cabe en la huella, el que deja el frente más de cara a la calle
            var aLaCalle = calles.Count > 0 ? MasCerca(calles, o.Centro) - o.Centro : Vector3.back;
            float ancho = (float)e.Ancho * Metros, fondo = (float)e.Alto * Metros, mejor = float.MinValue;
            for (int pasada = 0; pasada < 2 && mejor == float.MinValue; pasada++)
                for (int q = 0; q < 4; q++)
                {
                    bool cabe = q % 2 == 0 ? Mathf.Abs(receta.Ancho - ancho) < 0.6f && Mathf.Abs(receta.Fondo - fondo) < 0.6f : Mathf.Abs(receta.Fondo - ancho) < 0.6f && Mathf.Abs(receta.Ancho - fondo) < 0.6f;
                    if (!cabe && pasada == 0) continue;
                    var giro = Quaternion.Euler(0, 90 * q, 0);
                    float nota = Vector3.Dot(giro * receta.Frente, aLaCalle.normalized) - q * 1e-4f;
                    if (nota > mejor) { mejor = nota; o.Giro = giro; }
                }
            receta.Poner(o);
            foreach (var p in o.Piezas) piezas.Add((Vestir(p.modelo, estilo, epoca), p.pos, p.mira, e.Id));
            sendas.AddRange(o.Sendas); explanadas.AddRange(o.Explanadas); brasas.AddRange(o.Brasas); fuegos.AddRange(o.Fuegos); norias.AddRange(o.Norias);
            m.Hechos.Add(k);
        }

        Muralla(planta, A, piezas);
        var porDondeSeAnda = new List<(Vector3, Vector3)>(sendas);   // las sendas de las puertas (el relleno no añade)
        var prados = new List<Vector3>();
        var patios = new List<Vector3>();
        Rellenar(planta, A, calles, sendas, piezas, explanadas, semilla, prados, patios);
        // los fuegos, las brasas y el buey de la muela van con las piezas: también se conservan o se cambian por su sitio
        foreach (var f in fuegos) piezas.Add((PseudoFuego, f, Vector3.forward, null));
        foreach (var b in brasas) piezas.Add((PseudoBrasa, b, Vector3.forward, null));
        foreach (var (c, r) in norias) piezas.Add((PseudoNoria, c, new Vector3(r, 0, 0.001f), null));
        long tCalculo = reloj.ElapsedMilliseconds;

        // ── el suelo: uno, con la tierra pisada en su máscara; se repinta solo donde algo ha cambiado ──
        var todo = planta.Edificios.Concat(planta.Calles).Concat(planta.Muralla).ToList();
        double minX = todo.Min(r => r.X - r.Ancho / 2), maxX = todo.Max(r => r.X + r.Ancho / 2), minY = todo.Min(r => r.Y - r.Alto / 2), maxY = todo.Max(r => r.Y + r.Alto / 2);
        var centro3 = A((minX + maxX) / 2, (minY + maxY) / 2, 0);
        // (la zona de la máscara, a saltos de 30 m: una casa nueva en el borde no la mueve)
        var centro = new Vector2(Mathf.Round(centro3.x / 30) * 30, Mathf.Round(centro3.z / 30) * 30);
        float lado = Mathf.Ceil(((float)Math.Max(maxX - minX, maxY - minY) * Metros + 90) / 30) * 30;
        var trazos = new HashSet<(int, Vector3, Vector3, float)>();
        foreach (var (a, b, ancho) in calles) trazos.Add((0, a, b, ancho));
        foreach (var (a, b, ancho) in explanadas) trazos.Add((1, a, b, ancho));
        foreach (var (a, b) in sendas) trazos.Add((2, a, b, 0));
        bool todoElSuelo = m.Pisado == null || centro != m.Centro || lado != m.Lado;
        var cambiados = todoElSuelo ? null : trazos.Where(t => !m.Trazos.Contains(t)).Concat(m.Trazos.Where(t => !trazos.Contains(t))).ToList();
        m.Trazos = trazos;
        m.Centro = centro;
        m.Lado = lado;
        if (m.Suelo == null)
        {
            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo";
            suelo.transform.SetParent(m.Raiz.transform, false);
            m.Suelo = suelo.GetComponent<Renderer>();
            m.Suelo.sharedMaterial = new Material(Resources.Load<Material>("Ciudad/Banco/suelo"));
        }
        int pintados = 0;
        if (todoElSuelo || cambiados.Count > 0) pintados = PintarPisado(m, cambiados);
        m.Suelo.transform.position = new Vector3(centro.x, y0, centro.y);
        m.Suelo.transform.localScale = Vector3.one * Mathf.Max(60, lado / 10 * 3);
        m.Suelo.sharedMaterial.SetTexture("_Pisado", m.Pisado);
        m.Suelo.sharedMaterial.SetVector("_Zona", new Vector4(centro.x, centro.y, lado, 0));
        long tSuelo = reloj.ElapsedMilliseconds - tCalculo;

        // ── las piezas: las que siguen, se quedan; las que sobran, fuera; las nuevas, se crean ──
        if (m.AlFrente == null)
        {
            var patron = Resources.Load<GameObject>(Modelos + "choza_a").GetComponentInChildren<MeshFilter>();
            var frente = patron.transform.TransformPoint(patron.sharedMesh.bounds.center);   // en los modelos, la puerta mira adonde más se sale el porche
            frente.y = 0;
            m.AlFrente = Quaternion.Inverse(Quaternion.LookRotation(frente.normalized));
        }
        (string, Vector3Int, int) Clave(string modelo, Vector3 pos, Vector3 mira) =>
            (modelo, Vector3Int.RoundToInt(pos * 50), modelo == PseudoNoria ? Mathf.RoundToInt(mira.x * 50) : Mathf.RoundToInt(Mathf.Atan2(mira.x, mira.z) * Mathf.Rad2Deg));
        var quedan = new HashSet<(string, Vector3Int, int)>();
        int nuevas = 0, quitadas = 0;
        foreach (var (modelo, pos, mira, id) in piezas)
        {
            var clave = Clave(modelo, pos, mira);
            if (!quedan.Add(clave)) continue;
            if (!m.Vivas.TryGetValue(clave, out var viva) || viva.go == null)
            {
                var go = Crear(m, modelo, pos, mira);
                if (go == null) { quedan.Remove(clave); continue; }
                m.Vivas[clave] = viva = (go, id);
                nuevas++;
            }
            else if (viva.id != id) m.Vivas[clave] = viva = (viva.go, id);
            if (id == null) continue;
            if (!m.PiezasDe.TryGetValue(id, out var lista)) m.PiezasDe[id] = lista = new List<Renderer>();
            lista.AddRange(viva.go.GetComponentsInChildren<Renderer>());
        }
        foreach (var clave in m.Vivas.Keys.Where(k => !quedan.Contains(k)).ToList())
        {
            var go = m.Vivas[clave].go;
            if (go != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go); }
            m.Vivas.Remove(clave);
            quitadas++;
        }
        long tPiezas = reloj.ElapsedMilliseconds - tCalculo - tSuelo;

        // ── la vida: los animales sueltos, una vez; los vecinos, los que falten; y si cambian las calles o las sendas, la red nueva para todos ──
        var firma = string.Join("|", calles.Select(c => $"{c.a.x:0.0},{c.a.z:0.0},{c.b.x:0.0},{c.b.z:0.0}").Concat(porDondeSeAnda.Select(c => $"{c.Item1.x:0.0},{c.Item1.z:0.0},{c.Item2.x:0.0},{c.Item2.z:0.0}")));
        if (firma != m.FirmaDeLaRed)
        {
            m.FirmaDeLaRed = firma;
            var red = new VidaDeLaPlaza.Red(calles, porDondeSeAnda);
            foreach (var v in m.Raiz.GetComponentsInChildren<Vecino>()) v.CambiarRed(red);
            int cuantos = Mathf.Min(16, 3 + planta.Edificios.Count(e => e.Tipo == "vivienda" && e.Estado == null) * 2);
            VidaDeLaPlaza.Poblar(m.Raiz.transform, red, cuantos - m.Vecinos, !m.ConVida, semilla + m.Vecinos * 7);
            m.Vecinos = Mathf.Max(m.Vecinos, cuantos);
        }
        if (!m.ConVida)
        {
            VidaDeLaPlaza.Soltar(m.Raiz.transform, prados, patios, semilla);
            m.ConVida = true;
        }
        Debug.Log($"[Ciudad] {(todoElSuelo ? "montada" : "puesta al día")}: {m.Hechos.Count} de {planta.Edificios.Count} edificios con su arte, {quedan.Count} piezas ({nuevas} nuevas, {quitadas} quitadas), "
            + $"suelo {(todoElSuelo ? "entero" : pintados + " píxeles")}; {reloj.ElapsedMilliseconds} ms (cálculo {tCalculo}, suelo {tSuelo}, piezas {tPiezas}, vida {reloj.ElapsedMilliseconds - tCalculo - tSuelo - tPiezas})");
    }

    /// <summary>Crea una pieza: un modelo de la ciudad, un animal o un carro de las caravanas, o lo que no es modelo (un fuego, una brasa, el
    /// buey que da vueltas a la muela).</summary>
    static GameObject Crear(Montada m, string modelo, Vector3 pos, Vector3 mira)
    {
        string nombre = null;
        int barra = modelo.IndexOf('|');
        if (barra >= 0) { nombre = modelo.Substring(barra + 1); modelo = modelo.Substring(0, barra); }
        if (modelo == PseudoFuego) return Fuego(m.Raiz.transform, pos);
        if (modelo == PseudoBrasa) return Brasa(m.Raiz.transform, pos);
        if (modelo == PseudoNoria) return VidaDeLaPlaza.Noria(m.Raiz.transform, pos, mira.x);
        if (modelo == PseudoCaballo) return VidaDeLaPlaza.Caballo(m.Raiz.transform, pos, mira);
        // (el medio tramo de empalizada en diagonal: el mismo modelo, estirado a su largo)
        bool estirado = modelo.EndsWith("*");
        if (estirado) modelo = modelo.Substring(0, modelo.Length - 1);
        bool ajeno = modelo.StartsWith("Caravana/");
        if (!m.Prefabs.TryGetValue(modelo, out var prefab)) m.Prefabs[modelo] = prefab = Resources.Load<GameObject>((ajeno ? DeCaravana : Modelos) + modelo);
        if (prefab == null) { Debug.LogWarning($"[Ciudad] falta el modelo {modelo}"); return null; }
        var go = UnityEngine.Object.Instantiate(prefab, m.Raiz.transform);
        go.name = nombre ?? modelo;
        // los animales y los carros son los de las caravanas del mapa: miran a +Z y traen su giro en el prefab
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mira) * (ajeno ? prefab.transform.rotation : m.AlFrente.Value));
        if (ajeno && !modelo.Contains("Cart_")) VidaDeLaPlaza.Animal(go);
        if (estirado) go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(1.4142f, 1, 1));
        if (Verdes.Contains(modelo)) go.transform.localScale *= 0.85f + 0.35f * RellenoDeCiudad.Azar(Mathf.RoundToInt(pos.x * 7), Mathf.RoundToInt(pos.z * 7), 5);
        if (!ajeno && !SinChoque.Contains(modelo))
            foreach (var f in go.GetComponentsInChildren<MeshFilter>()) f.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh;
        return go;
    }

    static readonly string[] Cultivos = { "bancal_coles", "bancal_habas", "bancal_cebollas", "bancal_lino", "bancal_barbecho" };
    static readonly string[] DelPatio = { "banco", "tinajas", "mortero", "telar", "tendedero", "molino_de_mano", "cestos", "secadero", "piel_tensada", "horno_casa" };
    static readonly string[] DeLaTrasera = { "lenero", "gallinero", "colmenas", "emparrado", "tocon", "estercolero", "tinajas", "pila_lena" };
    static readonly string[] DeLabor = { "almiar", "estercolero", "pila_lena", "lenero", "tocon", "tajo" };

    /// <summary>
    /// Lo libre alrededor de lo construido, por parcelas con dueño (Víctor, 2026-11-25: sorteado casilla a casilla salía «muy pocas piezas,
    /// todo muy parecido y no repartido de una manera que parezca natural»). Cada vivienda tiene lo suyo, colocado respecto de ella: delante,
    /// entre la puerta y la calle, el patio pisado con un par de enseres; detrás (o a un lado, si detrás no cabe), UN huerto de una pieza, con
    /// su cierre (cada casa el suyo: zarzo, estacas, murete o seto) abierto hacia la casa y sus cultivos por tablas; y arrimado a sus costados,
    /// lo de la trasera (la leña a cubierto, el gallinero, las colmenas, la parra). Las granjas y los graneros, lo de la labor. Lo que queda
    /// sin dueño es monte y arboleda en manchas (ahí sí <see cref="RellenoDeCiudad"/>) y, más allá, el campo. Las casillas son de 1 u.l. (3 m).
    /// </summary>
    static void Rellenar(SelectorDePersonaje.InteriorParaVer planta, Func<double, double, float, Vector3> A, List<(Vector3 a, Vector3 b, float ancho)> calles,
        List<(Vector3, Vector3)> sendas, List<(string, Vector3, Vector3, string)> piezas, List<(Vector3, Vector3, float)> explanadas, int semilla, List<Vector3> prados, List<Vector3> patios)
    {
        const int Margen = 9;
        var ocupan = planta.Edificios.Concat(planta.Calles).Concat(planta.Muralla).ToList();
        if (ocupan.Count == 0) return;
        int x0 = (int)Math.Floor(ocupan.Min(r => r.X - r.Ancho / 2)) - Margen, y0 = (int)Math.Floor(ocupan.Min(r => r.Y - r.Alto / 2)) - Margen;
        int nx = (int)Math.Ceiling(ocupan.Max(r => r.X + r.Ancho / 2)) + Margen - x0, ny = (int)Math.Ceiling(ocupan.Max(r => r.Y + r.Alto / 2)) + Margen - y0;
        var vecindad = new Vecindad[nx, ny];
        var cercaDeCasa = new bool[nx, ny];
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                double cx = x0 + i + 0.5, cy = y0 + j + 0.5, cerca = 999, aCasa = 999;
                bool ocupada = false;
                foreach (var r in ocupan)
                {
                    double dx = Math.Max(0, Math.Abs(cx - r.X) - r.Ancho / 2), dy = Math.Max(0, Math.Abs(cy - r.Y) - r.Alto / 2), d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < 0.1) { ocupada = true; break; }
                    if (r.Tipo == "calle" || r.Tipo == "camino") continue;
                    cerca = Math.Min(cerca, d);
                    if (r.Tipo == "vivienda") aCasa = Math.Min(aCasa, d);
                }
                var c = A(cx, cy, 0);
                ocupada |= sendas.Any(s => DistanciaATramo(c, s.Item1, s.Item2) < 2.0f);
                vecindad[i, j] = ocupada ? Vecindad.Ocupada : cerca > 5.4 ? Vecindad.Campo : cerca > 2.5 ? Vecindad.Afueras : Vecindad.Nada;
                cercaDeCasa[i, j] = aCasa < 4;
            }
        bool Libre(int i, int j) => i >= 0 && j >= 0 && i < nx && j < ny && vecindad[i, j] != Vecindad.Ocupada;
        Vector3 Centro(int i, int j) => A(x0 + i + 0.5, y0 + j + 0.5, 0);
        float Az(int i, int j, int k) => RellenoDeCiudad.Azar(x0 + i, y0 + j, semilla * 100 + k);
        // cada pieza se viste con el estilo y la época de su edificio (y lo que no tiene dueño, con los de la plaza)
        int estilo = planta.NivelDePlaza, epoca = planta.Era;
        void Pon(string modelo, Vector3 pos, Vector3 mira) => piezas.Add((Vestir(modelo, estilo, epoca), pos, mira, null));
        // de la planta (y hacia abajo) al mundo (z hacia arriba): un paso de casilla (di, dj) es (di, 0, −dj)
        Vector3 EnMundo(int di, int dj) => new Vector3(di, 0, -dj);

        for (int k = 0; k < planta.Edificios.Count; k++)
        {
            var e = planta.Edificios[k];
            if (e.Estado != null || !Recetas.ContainsKey(e.Tipo)) continue;
            (estilo, epoca) = EstiloDe(planta, e);
            // las casillas que pisa el solar (aunque sea a medias: una planta que no caiga en casillas enteras no deja huecos)
            int i0 = (int)Math.Floor(e.X - e.Ancho / 2 + 0.01) - x0, i1 = (int)Math.Ceiling(e.X + e.Ancho / 2 - 0.01) - x0 - 1, j0 = (int)Math.Floor(e.Y - e.Alto / 2 + 0.01) - y0, j1 = (int)Math.Ceiling(e.Y + e.Alto / 2 - 0.01) - y0 - 1;
            int si = Mathf.RoundToInt((float)e.X * 2), sj = Mathf.RoundToInt((float)e.Y * 2);
            float H(int n) => RellenoDeCiudad.Azar(si, sj, semilla * 100 + n);
            // las casillas pegadas a un lado del solar (0 derecha, 1 izquierda, 2 abajo, 3 arriba, en la planta), a `fuera` casillas de él
            IEnumerable<(int, int)> Lado(int lado, int fuera) => lado < 2
                ? Enumerable.Range(j0, j1 - j0 + 1).Select(j => (lado == 0 ? i1 + fuera : i0 - fuera, j))
                : Enumerable.Range(i0, i1 - i0 + 1).Select(i => (i, lado == 2 ? j1 + fuera : j0 - fuera));
            (int, int) Paso(int lado) => lado == 0 ? (1, 0) : lado == 1 ? (-1, 0) : lado == 2 ? (0, 1) : (0, -1);
            // el frente: el lado que mira a la calle más cercana
            var centro = A(e.X, e.Y, 0);
            var aLaCalle = calles.Count > 0 ? MasCerca(calles, centro) - centro : Vector3.back;
            int frente = Mathf.Abs(aLaCalle.x) > Mathf.Abs(aLaCalle.z) ? (aLaCalle.x > 0 ? 0 : 1) : (aLaCalle.z < 0 ? 2 : 3);
            int detras = frente ^ 1;
            var costados = frente < 2 ? new[] { 2, 3 } : new[] { 0, 1 };

            void Arrimar(string[] deQue, int cuantos, int[] lados, int tirada)
            {
                // enseres arrimados al solar por esos lados: cada uno en una casilla libre, mirando hacia fuera
                var sitios = lados.SelectMany(l => Lado(l, 1).Select(c => (c, l))).Where(x => Libre(x.c.Item1, x.c.Item2)).ToList();
                for (int n = 0; n < cuantos && sitios.Count > 0; n++)
                {
                    int cual = (int)(H(tirada + n * 2) * sitios.Count * 0.999f);
                    var ((ci, cj), lado) = sitios[cual];
                    sitios.RemoveAt(cual);
                    var (pi, pj) = Paso(lado);
                    var modelo = deQue[(int)(H(tirada + n * 2 + 1) * deQue.Length * 0.999f)];
                    Pon(modelo, Centro(ci, cj) - EnMundo(pi, pj) * 0.6f, EnMundo(pi, pj));
                    vecindad[ci, cj] = Vecindad.Ocupada;
                }
            }

            if (e.Tipo != "vivienda")
            {
                if (e.Tipo == "granja" || e.Tipo == "granero") Arrimar(DeLabor, 2, new[] { detras, costados[0], costados[1] }, 70);
                else if (e.Tipo == "lenera") Arrimar(new[] { "pila_lena", "tocon", "lenero" }, 2, new[] { detras, costados[0], costados[1] }, 70);
                else if (H(69) < 0.4f) Arrimar(new[] { "banco", "tinajas", "tocon" }, 1, costados, 70);
                continue;
            }

            // ── el patio: dentro de su solar, en las esquinas que la choza redonda deja libres. En las dos de delante (a los lados de la puerta),
            // los enseres del patio; en una o las dos de detrás, lo de la trasera. Y si entre el solar y la calle queda sitio, pisado. ──
            var (fi, fj) = Paso(frente);
            Vector3 alFrente = EnMundo(fi, fj), alLado = new Vector3(-alFrente.z, 0, alFrente.x);
            float medio = (float)Math.Min(e.Ancho, e.Alto) * Metros / 2;
            for (int n = 0; n < 4; n++)
            {
                bool delantera = n < 2;
                if (H(10 + n) < (delantera ? 0.2f : 0.45f)) continue;
                var lista = delantera ? DelPatio : DeLaTrasera;
                var sitio = centro + alFrente * ((delantera ? 1 : -1) * (medio - 1.3f)) + alLado * ((n % 2 == 0 ? 1 : -1) * (medio - 1.2f));
                Pon(lista[(int)(H(14 + n) * lista.Length * 0.999f)], sitio, delantera ? (alFrente + alLado * (n % 2 == 0 ? 0.5f : -0.5f)).normalized : -alFrente);
            }
            patios.Add(centro + alFrente * (medio - 0.8f));
            foreach (var (ci, cj) in Lado(frente, 1).Where(c => Libre(c.Item1, c.Item2))) explanadas.Add((Centro(ci, cj), Centro(ci, cj), 1.7f));

            // ── el huerto: de una pieza, detrás (o a un costado si detrás no cabe); tan hondo y tan ancho como quepa, hasta 3 × 4 ──
            int hondoQuerido = 2 + (int)(H(20) * 2.999f);
            List<(int, int)> huerto = null;
            int ladoHuerto = -1;
            foreach (var lado in new[] { detras, costados[H(21) < 0.5f ? 0 : 1], costados[H(21) < 0.5f ? 1 : 0] })
            {
                var (pi, pj) = Paso(lado);
                var pegadas = Lado(lado, 1).ToList();
                for (int hondo = hondoQuerido; hondo >= 1 && huerto == null; hondo--)
                    for (int ancho = pegadas.Count; ancho >= 2 && huerto == null; ancho--)
                        for (int desde = 0; desde + ancho <= pegadas.Count && huerto == null; desde++)
                        {
                            var casillas = (from q in pegadas.Skip(desde).Take(ancho) from h in Enumerable.Range(0, hondo) select (q.Item1 + pi * h, q.Item2 + pj * h)).ToList();
                            if (casillas.All(c => Libre(c.Item1, c.Item2))) { huerto = casillas; ladoHuerto = lado; }
                        }
                if (huerto != null) break;
            }
            if (huerto != null)
            {
                var deEste = new HashSet<(int, int)>(huerto);
                var (pi, pj) = Paso(ladoHuerto);
                // el cierre de esta casa, y el cultivo de cada tabla (una tabla = una fila a lo hondo)
                float t = H(22);
                string cierre = t < 0.34f ? "cerca" : t < 0.6f ? "cerca_palos" : t < 0.78f ? "murete" : "seto";
                foreach (var (ci, cj) in huerto)
                {
                    int fila = (ci - (pi != 0 ? (pi > 0 ? i1 + 1 : i0 - 1) : 0)) * pi + (cj - (pj != 0 ? (pj > 0 ? j1 + 1 : j0 - 1) : 0)) * pj;
                    Pon(Cultivos[(int)(H(30 + fila) * Cultivos.Length * 0.999f)], Centro(ci, cj), pi != 0 ? Vector3.right : Vector3.back);
                    vecindad[ci, cj] = Vecindad.Ocupada;
                    for (int d = 0; d < 4; d++)
                    {
                        var (qi, qj) = Paso(d);
                        if (deEste.Contains((ci + qi, cj + qj))) continue;
                        if (qi == -pi && qj == -pj) continue;                     // hacia la casa, abierto: por ahí se entra
                        Pon(cierre == "cerca" && Az(ci, cj, 80 + d) < 0.12f ? "cerca_vieja" : cierre, Centro(ci, cj) + EnMundo(qi, qj) * (Metros / 2), EnMundo(qi, qj));
                    }
                }
            }

            // ── la trasera: arrimado a los costados que queden libres, lo de cada casa ──
            if (H(40) < 0.5f) Arrimar(DeLaTrasera, 1, new[] { costados[0], costados[1], detras }, 41);
        }

        (estilo, epoca) = (planta.NivelDePlaza, planta.Era);
        // ── lo que queda sin dueño: monte y arboleda en manchas, y el campo más allá; mucho más ralo que antes (pesaba miles de piezas) ──
        var relleno = RellenoDeCiudad.Generar(vecindad, x0, y0, semilla);
        Casilla Que(int i, int j) => i >= 0 && j >= 0 && i < nx && j < ny ? relleno[i, j] : Casilla.Campo;
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                var q = relleno[i, j];
                if (q == Casilla.Ocupada) continue;
                var c = Centro(i, j);
                float u = Az(i, j, 21), w = Az(i, j, 22), giro = Az(i, j, 26) * 6.2832f;
                var corrido = new Vector3(Az(i, j, 23) - 0.5f, 0, Az(i, j, 24) - 0.5f);
                var mira = new Vector3(Mathf.Cos(giro), 0, Mathf.Sin(giro));
                bool campo = q == Casilla.Campo;
                if (!campo && cercaDeCasa[i, j]) prados.Add(c);
                if (q == Casilla.Arboleda)
                {
                    // un árbol por casilla sería un bosque, y pegado a un tejado se lo come la copa
                    bool arrimada = false;
                    for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++) arrimada |= Que(i + a, j + b) == Casilla.Ocupada;
                    if (u < 0.24f && !arrimada) Pon(w < 0.55f ? "encina_b" : w < 0.8f ? "cipres" : "olivo", c + corrido * 1.6f, mira);
                    else if (u < 0.5f) Pon(w < 0.75f ? "mata" : "retama", c + corrido * 2, mira);
                    continue;
                }
                if (campo && u < 0.012f) { Pon(w < 0.5f ? "encina_b" : w < 0.75f ? "olivo" : "cipres", c + corrido, mira); continue; }
                if (u > (campo ? 0.05f : 0.16f)) continue;
                // un corro de hierba alta, a veces con su mata y sus flores
                if (w < 0.25f) Pon(w < 0.18f ? "mata" : "retama", c + corrido, mira);
                int n = 2 + (int)(Az(i, j, 27) * 2.999f);
                for (int k = 0; k < n; k++)
                {
                    float a = Az(i, j, 40 + k) * 6.2832f, r = 0.7f + Az(i, j, 50 + k) * 2.0f, t = Az(i, j, 60 + k);
                    Pon(t < 0.45f ? "hierba_a" : t < 0.8f ? "hierba_b" : "flores", c + corrido + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r, new Vector3(Mathf.Sin(a * 3), 0, Mathf.Cos(a * 3)));
                }
            }
    }

    /// <summary>
    /// La máscara del suelo, desde los trazos de la planta (`m.Trazos`: calles, explanadas y sendas): R la tierra pisada, G lo hundido (las
    /// rodadas de los carros y la huella de las sendas) y B el lomo con hierba entre las rodadas. Los bordes no son rectas: se miden desde el
    /// punto apartado por dos escalas de ruido. Con `cambiados` (los trazos que entran o salen) solo se rehace la caja que los abarca: se borra
    /// y se vuelven a pintar en ella todos los trazos que la tocan. Devuelve los píxeles repintados.
    /// </summary>
    // ponytail: en el hilo principal (un par de segundos a 2048 la primera vez); si pesa al entrar, a un Job con Burst o a media resolución.
    static int PintarPisado(Montada m, List<(int tipo, Vector3 a, Vector3 b, float ancho)> cambiados)
    {
        const int N = LadoPisado;
        float Suave(float a, float b, float x) { var t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        float R(float x, float y) => Mathf.PerlinNoise(x + 1000, y + 1000) - 0.5f;
        float px = m.Lado / N;
        var origen = m.Centro - Vector2.one * m.Lado / 2;
        Vector2 En2(Vector3 p) => new Vector2(p.x, p.z);
        float Margen((int tipo, Vector3 a, Vector3 b, float ancho) t) => t.tipo == 0 ? t.ancho * 2 + 5 : t.tipo == 1 ? t.ancho + 5 : 5;
        // la caja a repintar (en píxeles): todo, o lo que abarcan los trazos cambiados
        int r0 = 0, r1 = N - 1, s0 = 0, s1 = N - 1;
        if (cambiados != null)
        {
            r0 = s0 = N; r1 = s1 = -1;
            foreach (var t in cambiados)
            {
                float mg = Margen(t);
                r0 = Mathf.Min(r0, (int)((Mathf.Min(t.a.x, t.b.x) - mg - origen.x) / px)); r1 = Mathf.Max(r1, (int)((Mathf.Max(t.a.x, t.b.x) + mg - origen.x) / px));
                s0 = Mathf.Min(s0, (int)((Mathf.Min(t.a.z, t.b.z) - mg - origen.y) / px)); s1 = Mathf.Max(s1, (int)((Mathf.Max(t.a.z, t.b.z) + mg - origen.y) / px));
            }
            r0 = Mathf.Max(0, r0); s0 = Mathf.Max(0, s0); r1 = Mathf.Min(N - 1, r1); s1 = Mathf.Min(N - 1, s1);
            if (r1 < r0 || s1 < s0) return 0;
        }
        if (m.V == null) { m.V = new float[N * N]; m.G = new float[N * N]; m.B = new float[N * N]; m.Pixeles = new Color32[N * N]; }
        float[] v = m.V, g = m.G, bl = m.B;
        if (cambiados == null) { Array.Clear(v, 0, v.Length); Array.Clear(g, 0, g.Length); Array.Clear(bl, 0, bl.Length); }
        else for (int j = s0; j <= s1; j++) { Array.Clear(v, j * N + r0, r1 - r0 + 1); Array.Clear(g, j * N + r0, r1 - r0 + 1); Array.Clear(bl, j * N + r0, r1 - r0 + 1); }
        // de un tramo, los píxeles de su caja (con margen) que caen en la caja a repintar; a cada uno, su punto y su punto apartado por el ruido
        void Caja(Vector2 a, Vector2 b, float margen, Action<int, Vector2, Vector2> pinta)
        {
            int i0 = Mathf.Max(r0, (int)((Mathf.Min(a.x, b.x) - margen - origen.x) / px)), i1 = Mathf.Min(r1, (int)((Mathf.Max(a.x, b.x) + margen - origen.x) / px));
            int j0 = Mathf.Max(s0, (int)((Mathf.Min(a.y, b.y) - margen - origen.y) / px)), j1 = Mathf.Min(s1, (int)((Mathf.Max(a.y, b.y) + margen - origen.y) / px));
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    var p = origen + new Vector2(i + 0.5f, j + 0.5f) * px;
                    var q = p + new Vector2(R(p.x / 7, p.y / 7), R(p.x / 7 + 31, p.y / 7 + 17)) * 2.4f + new Vector2(R(p.x / 1.9f, p.y / 1.9f), R(p.x / 1.9f + 5, p.y / 1.9f + 9)) * 0.7f;
                    pinta(j * N + i, p, q);
                }
        }
        float Tramo(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float l = ab.sqrMagnitude;
            return (p - (l < 1e-6f ? a : a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / l))).magnitude;
        }

        foreach (var (tipo, a3, b3, ancho) in m.Trazos)
        {
            Vector2 a = En2(a3), b = En2(b3);
            if (tipo == 0)
            {
                var eje = (b - a).sqrMagnitude < 1e-6f ? Vector2.up : (b - a).normalized;
                var alLado = new Vector2(-eje.y, eje.x);
                float largo = (b - a).magnitude;
                bool conRodadas = ancho >= 3;
                // la banda pisada no llena la calle: dos tercios de su medio ancho, y cambia al andarla; por sus cabos sale lo que su ancho
                Caja(a - eje * ancho, b + eje * ancho, ancho + 4, (k, p, q) =>
                {
                    float s = Vector2.Dot(p - a, eje), lat = Vector2.Dot(p - a, alLado);
                    float medio = ancho * 0.67f + R(s / 9, a.x + a.y) * ancho * 0.36f;
                    v[k] = Mathf.Max(v[k], 1 - Suave(medio - 0.7f, medio + 0.6f, Tramo(q, a - eje * ancho * 0.5f, b + eje * ancho * 0.5f)));
                    if (!conRodadas || s < 0 || s > largo) return;
                    // las rodadas de los carros: dos, a 1,5 m una de otra, que serpean y a trechos se pierden; y el lomo entre ellas
                    float centroCalle = R(s / 14, 7 + a.x) * Mathf.Min(2.2f, ancho * 0.5f);
                    float dr = Mathf.Abs(Mathf.Abs(lat - centroCalle) - 0.75f);
                    float hay = Suave(0.3f, 0.5f, Mathf.PerlinNoise(s / 11 + 40, 2.5f + a.y));
                    g[k] = Mathf.Max(g[k], (1 - Suave(0.13f, 0.24f, dr + R(p.x * 2, p.y * 2) * 0.06f)) * hay);
                    bl[k] = Mathf.Max(bl[k], (1 - Suave(0.15f, 0.5f, Mathf.Abs(lat - centroCalle))) * hay * Suave(0.4f, 0.6f, Mathf.PerlinNoise(s / 6 + 80, 9.5f)));
                });
            }
            else if (tipo == 1) Caja(a, b, ancho + 5, (k, p, q) => v[k] = Mathf.Max(v[k], 1 - Suave(ancho, ancho + 1.8f, Tramo(q, a, b))));
            else Caja(a, b, 5, (k, p, q) =>
            {
                float d = Tramo(q, a, b);
                v[k] = Mathf.Max(v[k], 1 - Suave(0.5f, 1.5f, d));
                g[k] = Mathf.Max(g[k], 0.4f * (1 - Suave(0.15f, 0.6f, d)));
            });
        }
        var pixeles = m.Pixeles;
        for (int j = s0; j <= s1; j++)
            for (int i = r0; i <= r1; i++)
            {
                int k = j * N + i;
                pixeles[k] = new Color32((byte)(v[k] * 255), (byte)(g[k] * 255), (byte)(bl[k] * 255), 255);
            }
        if (m.Pisado == null) m.Pisado = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "Pisado" };
        m.Pisado.SetPixels32(pixeles);
        m.Pisado.Apply(true);
        return (r1 - r0 + 1) * (s1 - s0 + 1);
    }

    /// <summary>La brasa de un horno: solo su luz, que tiembla en la boca.</summary>
    static GameObject Brasa(Transform padre, Vector3 donde)
    {
        var luz = new GameObject("Brasa del horno").AddComponent<Light>();
        luz.transform.SetParent(padre, false);
        luz.transform.position = donde;
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.45f, 0.15f);
        luz.intensity = 5;
        luz.range = 4.5f;
        luz.shadows = LightShadows.None;
        luz.gameObject.AddComponent<LuzDeFuego>();
        return luz.gameObject;
    }

    /// <summary>El fuego del hogar: la llama y el humo (partículas) y su luz, que tiembla.</summary>
    static GameObject Fuego(Transform padre, Vector3 donde)
    {
        var raiz = new GameObject("Fuego del hogar");
        raiz.transform.SetParent(padre, false);
        raiz.transform.position = donde;
        var luz = new GameObject("Luz").AddComponent<Light>();
        luz.transform.SetParent(raiz.transform, false);
        luz.transform.localPosition = Vector3.up * 0.9f;
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.55f, 0.22f);
        luz.intensity = 9;
        luz.range = 13;
        luz.shadows = LightShadows.None;
        luz.gameObject.AddComponent<LuzDeFuego>();

        ParticleSystem Sistema(string nombre, string material)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(raiz.transform, false);
            go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Resources.Load<Material>("Ciudad/Banco/" + material);
            return ps;
        }
        Gradient Tinte(params (Color c, float a, float t)[] claves) => new Gradient
        {
            colorKeys = claves.Select(k => new GradientColorKey(k.c, k.t)).ToArray(),
            alphaKeys = claves.Select(k => new GradientAlphaKey(k.a, k.t)).ToArray(),
        };
        var llama = Sistema("Llama", "llama");
        var ml = llama.main;
        ml.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        ml.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        ml.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        ml.maxParticles = 120;
        var el = llama.emission;
        el.rateOverTime = 55;
        var fl = llama.shape;
        fl.shapeType = ParticleSystemShapeType.Cone;
        fl.angle = 9;
        fl.radius = 0.45f;
        var cl = llama.colorOverLifetime;
        cl.enabled = true;
        cl.color = Tinte((new Color(1f, 0.85f, 0.45f), 0f, 0f), (new Color(1f, 0.6f, 0.15f), 0.85f, 0.15f), (new Color(0.9f, 0.25f, 0.05f), 0.5f, 0.6f), (new Color(0.4f, 0.1f, 0.05f), 0f, 1f));
        var tl = llama.sizeOverLifetime;
        tl.enabled = true;
        tl.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0.25f));

        var humo = Sistema("Humo", "humo");
        humo.transform.localPosition = Vector3.up * 1.2f;
        var mh = humo.main;
        mh.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        mh.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        mh.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
        mh.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
        mh.maxParticles = 90;
        var eh = humo.emission;
        eh.rateOverTime = 7;
        var fh = humo.shape;
        fh.shapeType = ParticleSystemShapeType.Cone;
        fh.angle = 6;
        fh.radius = 0.35f;
        var ch = humo.colorOverLifetime;
        ch.enabled = true;
        ch.color = Tinte((new Color(0.32f, 0.3f, 0.28f), 0f, 0f), (new Color(0.45f, 0.43f, 0.4f), 0.3f, 0.12f), (new Color(0.62f, 0.6f, 0.58f), 0.14f, 0.6f), (new Color(0.7f, 0.7f, 0.7f), 0f, 1f));
        var th = humo.sizeOverLifetime;
        th.enabled = true;
        th.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.6f, 1, 3.2f));
        var vh = humo.velocityOverLifetime;
        vh.enabled = true;
        vh.space = ParticleSystemSimulationSpace.World;
        vh.x = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        vh.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vh.z = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        return raiz;
    }
}
