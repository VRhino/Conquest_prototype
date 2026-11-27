// La vegetación del campo de batalla del lugar (Campo_De_Batalla_Del_Lugar.md §6.6 y §6.10): los modelos, las texturas y las reglas del mapa de
// mundo (`Resources/MapaMundo/Vegetacion`, `ReglasDeVegetacion`), a la escala del soldado y con `Conquest/CampoVegetacion`.
// - Los árboles son los que puso el generador. Su especie sale de `EspecieDe`: por la altura del mundo, el agua cercana y el rodal.
// - La capa baja (hierba, flores, matas, juncos y piedras), como la decoración de la mesa: un hueco cada 3 m en el área jugable y 40 m más,
//   con `Probabilidades`. Nada en el agua ni en las placas; en el paso del vado, cantos.
// Solo se ve: no decide nada del juego (por eso calcula en float). Cada planta, en tres niveles (cerca, medio, lejos) según la cámara; la capa
// baja solo hasta `HastaCapaBaja`.
using System;
using System.Collections.Generic;
using ConquestTactics.Mundo;
using ConquestTactics.Mundo.Campo;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;

public class VegetacionDelCampo : MonoBehaviour
{
    const float PasoDecoracion = 3f, MargenDecoracion = 40f;
    const float HastaCerca = 90f, HastaMedio = 350f, HastaCapaBaja = 160f;
    const double Costa = 420, Colina = 545, Montana = 600, NivelDelAgua = 375;
    const float MetrosPorU = 12f; // los de las reglas de la vegetación (el mapa de mundo)
    static readonly string[] Niveles = { "cerca", "medio", "lejos" };

    sealed class Especie
    {
        public string Nombre;
        public Mesh[,] Mallas; // [variante, nivel]
        public Material[,][] Materiales;
        /// <summary>El giro y la escala que trae cada malla del FBX (su hijo respecto a la raíz).</summary>
        public Matrix4x4[,] Local;
        public float Alto, Bajo; // m del modelo «cerca», variante 1: su alto y lo que queda por debajo de su origen
        public int Variantes;
    }

    struct Planta { public int Especie, Variante; public Matrix4x4 Matriz; public Vector3 Pos; public bool Baja; }

    readonly List<Especie> especies = new List<Especie>();
    readonly List<Planta> plantas = new List<Planta>();
    // las tandas a dibujar: (especie, variante, nivel) → matrices
    readonly Dictionary<(int, int, int), List<Matrix4x4>> tandas = new Dictionary<(int, int, int), List<Matrix4x4>>();
    Vector3 camaraAntes = new Vector3(float.MaxValue, 0, 0);
    float rehacerEn;

    int Indice(string nombre) => especies.FindIndex(e => e.Nombre == nombre);

    /// <summary>Lo planta: `alturaEn` (m, del campo en mm), `aAgua` (m al agua, por celda de zonas) y las placas (escena) donde no va nada.</summary>
    public void Montar(CampoGenerado campo, Func<long, long, float> alturaEn, float[] aAgua, Vector3[] placas, float radioPlaca)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return; // (el servidor de batalla no dibuja)
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        Cargar();
        if (especies.Count == 0) { Debug.LogError("[Campo] no se encontraron los modelos de la vegetación del mapa"); return; }
        DatosDelSuelo(campo);
        var lugar = campo.Lugar;
        int n = campo.NZonas;
        int Celda(float x, float y) => Mathf.Clamp((int)((y * 1000 + campo.Lado / 2) / campo.CeldaZonas), 0, n - 1) * n + Mathf.Clamp((int)((x * 1000 + campo.Lado / 2) / campo.CeldaZonas), 0, n - 1);
        // (las reglas van en u del mundo y metros de altura del mundo)
        double U(float m, long centro) => centro / 1000.0 + m / (double)Lugar.EscalaCampo;
        double AlturaMundo(float x, float y) => (campo.AlturaBase + alturaEn((long)(x * 1000), (long)(y * 1000)) * 1000 * 4 / 25) / 1000.0;
        bool EnPlaca(Vector3 p, float margen) => Array.Exists(placas, q => (q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z) < (radioPlaca + margen) * (radioPlaca + margen));
        Vector3 Escena(float x, float y) => new Vector3(x, alturaEn((long)(x * 1000), (long)(y * 1000)), -y);
        long absX = lugar.AbsolutoX, absY = lugar.AbsolutoY;

        // los árboles del generador, con su especie
        string[] arboles = { "encina", "olivo", "pino", "cipres", "abeto", "palmera", "alamo" };
        for (int k = 0; k + 1 < campo.Arboles.Length; k += 2)
        {
            float x = campo.Arboles[k] / 1000f, y = campo.Arboles[k + 1] / 1000f;
            double t = ReglasDeVegetacion.Azar((int)(campo.Arboles[k] / 100), (int)(campo.Arboles[k + 1] / 100), 41);
            var esp = ReglasDeVegetacion.EspecieDe(U(x, lugar.Centro.X), U(y, lugar.Centro.Y), AlturaMundo(x, y), aAgua[Celda(x, y)] / MetrosPorU, t, Costa, Colina, Montana);
            int e = Indice(arboles[(int)esp]);
            if (e < 0) e = Indice("encina");
            if (e < 0) continue;
            double r = ReglasDeVegetacion.Azar((int)(campo.Arboles[k] / 100), (int)(campo.Arboles[k + 1] / 100), 43);
            float alto = Mathf.Lerp(7f, 12f, (float)r) * Mathf.Lerp(0.9f, 1.2f, (float)t);
            Plantar(e, (int)(r * 997) % especies[e].Variantes, Escena(x, y), alto, (float)(t * 360), false);
        }

        // la capa baja: un hueco cada 3 m en el área jugable y un margen
        int iHierba = Indice("hierba"), iFlor = Indice("flor"), iPiedra = Indice("piedra"), iCanas = Indice("canas");
        int iLentisco = Indice("lentisco"), iRetama = Indice("retama"), iAdelfa = Indice("adelfa");
        float hasta = campo.Jugable / 2000f + MargenDecoracion;
        for (float y = -hasta + PasoDecoracion / 2; y < hasta; y += PasoDecoracion)
            for (float x = -hasta + PasoDecoracion / 2; x < hasta; x += PasoDecoracion)
            {
                long X = absX + (long)(x * 1000), Y = absY + (long)(y * 1000);
                int gi = (int)Math.Round(X / (PasoDecoracion * 1000.0)), gj = (int)Math.Round(Y / (PasoDecoracion * 1000.0));
                float ax = x + (float)(ReglasDeVegetacion.Azar(gi, gj, 5) - 0.5) * PasoDecoracion, ay = y + (float)(ReglasDeVegetacion.Azar(gi, gj, 7) - 0.5) * PasoDecoracion;
                int c = Celda(ax, ay);
                var zona = campo.Zonas[c];
                var p = Escena(ax, ay);
                double q = ReglasDeVegetacion.Azar(gi, gj, 13), s = ReglasDeVegetacion.Azar(gi, gj, 11);
                if (zona == Zona.Somero)
                {
                    // en el paso del vado, cantos sueltos que asoman del agua
                    if (iPiedra >= 0 && s < 0.35) Plantar(iPiedra, (int)(q * 97) % especies[iPiedra].Variantes, p, 0.5f * 1.8f * Mathf.Lerp(0.7f, 1.3f, (float)q), (float)(q * 360), true);
                    continue;
                }
                if (zona == Zona.Honda || (zona == Zona.Camino && ReglasDeVegetacion.Azar(gi, gj, 17) < 0.8) || EnPlaca(p, 0)) continue;
                float e2 = 2f;
                float pendiente = new Vector2(alturaEn((long)((ax + e2) * 1000), (long)(ay * 1000)) - alturaEn((long)((ax - e2) * 1000), (long)(ay * 1000)),
                    alturaEn((long)(ax * 1000), (long)((ay + e2) * 1000)) - alturaEn((long)(ax * 1000), (long)((ay - e2) * 1000))).magnitude / (2 * e2);
                double h = AlturaMundo(ax, ay), f = lugar.Fertilidad.Muestra((long)(ax * 1000), (long)(ay * 1000)) / 65536.0;
                double b = Mathf.Clamp01(campo.Bosque[c] / 65536f), dAgua = aAgua[c] / MetrosPorU;
                var pr = ReglasDeVegetacion.Probabilidades(h, pendiente, f, b, dAgua, ReglasDeVegetacion.Manchas(X / 1000.0, Y / 1000.0, 30, 77), NivelDelAgua, Costa, Colina, Montana);
                int tipo = -1;
                for (int t = 0; t < pr.Length; t++) if ((s -= pr[t]) < 0) { tipo = t; break; }
                if (tipo < 0) continue;
                float giro = (float)(q * 360), tam = Mathf.Lerp(0.7f, 1.3f, (float)ReglasDeVegetacion.Azar(gi, gj, 19));
                switch ((TipoDeMata)tipo)
                {
                    case TipoDeMata.Hierba: if (iHierba >= 0) Plantar(iHierba, (int)(q * 97) % especies[iHierba].Variantes, p, 0.35f * tam, giro, true); break;
                    case TipoDeMata.Flor: if (iFlor >= 0) Plantar(iFlor, (int)(q * 97) % especies[iFlor].Variantes, p, 0.3f * tam, giro, true); break;
                    case TipoDeMata.Junco: if (iCanas >= 0) Plantar(iCanas, (int)(q * 97) % especies[iCanas].Variantes, p, 1.3f * tam, giro, true); break;
                    case TipoDeMata.Piedra: if (iPiedra >= 0) Plantar(iPiedra, (int)(q * 97) % especies[iPiedra].Variantes, p, 0.5f * tam * (1 + 3 * pendiente), giro, true); break;
                    default:
                        // la mata: adelfa junto al agua en lo bajo, retama en lo seco y lentisco en lo demás
                        int m = dAgua < 2.5 && h > Costa && h < Colina && q < 0.6 && iAdelfa >= 0 ? iAdelfa : f < 0.35 && q < 0.4 && iRetama >= 0 ? iRetama : iLentisco;
                        if (m >= 0) Plantar(m, (int)(q * 97) % especies[m].Variantes, p, 0.9f * tam, giro, true);
                        break;
                }
            }
        Debug.Log($"[Campo] vegetación: {plantas.Count} plantas en {reloj.ElapsedMilliseconds} ms");
    }

    void Plantar(int e, int variante, Vector3 pos, float alto, float giro, bool baja)
    {
        var esp = especies[e];
        float escala = alto / Mathf.Max(0.01f, esp.Alto);
        plantas.Add(new Planta { Especie = e, Variante = variante, Pos = pos, Baja = baja, Matriz = Matrix4x4.TRS(pos - Vector3.up * (esp.Bajo * escala), Quaternion.Euler(0, giro, 0), Vector3.one * escala) });
    }

    // Los modelos y sus materiales, como `VegetacionDelMapa` (el hijo `<modelo>_<variante>_<nivel>` de cada FBX; qué es cada submalla, por el
    // nombre de su material importado o por el manifiesto).
    void Cargar()
    {
        var sombreado = Resources.Load<Shader>("Campo/CampoVegetacion");
        var texto = Resources.Load<TextAsset>("MapaMundo/Vegetacion/vegetacion");
        if (sombreado == null || texto == null) return;
        var manifiesto = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(texto.text);
        foreach (var (nombre, variantes) in new[] { ("encina", 3), ("olivo", 3), ("pino", 3), ("alamo", 3), ("cipres", 3), ("abeto", 3), ("palmera", 3),
                     ("lentisco", 2), ("retama", 3), ("adelfa", 3), ("canas", 3), ("hierba", 3), ("flor", 3), ("piedra", 3) })
        {
            var raiz = Resources.Load<GameObject>("MapaMundo/Vegetacion/" + nombre);
            if (raiz == null) continue;
            string Con(string tipo, string sinElla) => Resources.Load<Texture2D>($"MapaMundo/Vegetacion/{nombre}_{tipo}_color") != null ? nombre : sinElla;
            string corteza = Con("corteza", "encina"), hojas = Con("hojas", "encina"), masa = Con("masa", "encina");
            bool arbol = Array.IndexOf(new[] { "encina", "olivo", "pino", "alamo", "cipres", "abeto", "palmera" }, nombre) >= 0;
            float modo = nombre == "hierba" ? 1 : nombre == "flor" ? 2 : 0;
            var mats = new Dictionary<string, Material>
            {
                ["corteza"] = Material(sombreado, corteza + "_corteza", false, 0.0f, 2010, false, modo, arbol),
                ["hojas"] = Material(sombreado, hojas + "_hojas", true, 0.35f, 2450, false, modo, arbol),
                ["masa"] = Material(sombreado, masa + "_masa", false, 0.15f, 2010, false, modo, arbol),
                ["lejos"] = Material(sombreado, nombre + "_lejos", false, 0.1f, 2010, true, modo, arbol),
            };
            var e = new Especie { Nombre = nombre, Variantes = variantes, Mallas = new Mesh[variantes, 3], Materiales = new Material[variantes, 3][], Local = new Matrix4x4[variantes, 3] };
            for (int v = 0; v < variantes; v++)
                for (int n = 0; n < 3; n++)
                {
                    var clave = $"{nombre}_{v + 1}_{Niveles[n]}";
                    var hijo = raiz.transform.Find(clave);
                    e.Mallas[v, n] = hijo != null ? hijo.GetComponent<MeshFilter>()?.sharedMesh : null;
                    e.Local[v, n] = hijo != null ? raiz.transform.worldToLocalMatrix * hijo.localToWorldMatrix : Matrix4x4.identity;
                    var importados = hijo != null ? hijo.GetComponent<MeshRenderer>()?.sharedMaterials : null;
                    var tipos = importados != null && importados.Length > 0 && Array.TrueForAll(importados, x => x != null && mats.ContainsKey(Tipo(x.name)))
                        ? new List<string>(Array.ConvertAll(importados, x => Tipo(x.name)))
                        : manifiesto.TryGetValue(clave, out var k) ? k : new List<string>();
                    e.Materiales[v, n] = tipos.ConvertAll(x => mats[x]).ToArray();
                }
            if (e.Mallas[0, 0] == null) continue;
            float bajo = float.MaxValue, arriba = float.MinValue;
            var bb = e.Mallas[0, 0].bounds;
            for (int k = 0; k < 8; k++)
            {
                var esquina = e.Local[0, 0].MultiplyPoint3x4(bb.center + Vector3.Scale(bb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                bajo = Mathf.Min(bajo, esquina.y);
                arriba = Mathf.Max(arriba, esquina.y);
            }
            e.Alto = Mathf.Max(0.01f, arriba - bajo);
            e.Bajo = bajo;
            especies.Add(e);
        }
    }

    static string Tipo(string material) => material.Substring(material.LastIndexOf('_') + 1);

    static Material Material(Shader s, string textura, bool recorte, float translucidez, int cola, bool soloColor, float modo, bool arbol)
    {
        var m = new Material(s) { name = textura, enableInstancing = true, renderQueue = cola };
        m.SetTexture("_MainTex", Resources.Load<Texture2D>("MapaMundo/Vegetacion/" + textura + "_color"));
        if (!soloColor)
        {
            m.SetTexture("_Normal", Resources.Load<Texture2D>("MapaMundo/Vegetacion/" + textura + "_normal"));
            m.SetTexture("_Mascara", Resources.Load<Texture2D>("MapaMundo/Vegetacion/" + textura + "_mascara"));
        }
        bool corteza = textura.EndsWith("_corteza");
        m.SetFloat("_Recorte", recorte ? 1 : 0);
        m.SetFloat("_Cull", recorte ? (float)CullMode.Off : (float)CullMode.Back);
        m.SetFloat("_Translucidez", translucidez);
        m.SetFloat("_Viento", corteza ? 0.04f : 0.12f);
        m.SetFloat("_Variacion", corteza ? 0.25f : 1);
        m.SetFloat("_Modo", modo);
        m.SetFloat("_ConHondura", arbol ? 1 : 0);
        return m;
    }

    // El suelo bajo cada planta para el sombreado (`_CampoDatos`): fertilidad, humedad, bosque y arena por celda de zonas.
    static void DatosDelSuelo(CampoGenerado campo)
    {
        int n = campo.NZonas;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        var lugar = campo.Lugar;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                long x = -campo.Lado / 2 + i * campo.CeldaZonas + campo.CeldaZonas / 2, y = -campo.Lado / 2 + j * campo.CeldaZonas + campo.CeldaZonas / 2;
                int k = j * n + i;
                byte B(long q16) => (byte)Mathf.Clamp(q16 * 255 / 65536, 0, 255);
                // la textura va con la escena: fila 0 al sur (z mínima)
                px[(n - 1 - j) * n + i] = new Color32(B(lugar.Fertilidad.Muestra(x, y)), B(lugar.Humedad.Muestra(x, y)), B(campo.Bosque[k]), (byte)(campo.Zonas[k] == Zona.Arena ? 255 : 0));
            }
        tex.SetPixels32(px);
        tex.Apply();
        Shader.SetGlobalTexture("_CampoDatos", tex);
        float lado = campo.Lado / 1000f;
        Shader.SetGlobalVector("_CampoDatosCaja", new Vector4(-lado / 2, -lado / 2, lado, 0));
    }

    void Update()
    {
        if (plantas.Count == 0) return;
        var cam = Camera.main;
        if (cam == null) return;
        var c = cam.transform.position;
        // las tandas se rehacen cuando la cámara se ha movido (como mucho, cuatro veces por segundo)
        if ((c - camaraAntes).sqrMagnitude > 25f && Time.unscaledTime >= rehacerEn)
        {
            camaraAntes = c;
            rehacerEn = Time.unscaledTime + 0.25f;
            foreach (var l in tandas.Values) l.Clear();
            foreach (var p in plantas)
            {
                float d = Vector2.Distance(new Vector2(p.Pos.x, p.Pos.z), new Vector2(c.x, c.z));
                if (p.Baja && d > HastaCapaBaja) continue;
                int nivel = d < HastaCerca ? 0 : d < HastaMedio ? 1 : 2;
                if (especies[p.Especie].Mallas[p.Variante, nivel] == null) nivel = 0;
                var clave = (p.Especie, p.Variante, nivel);
                if (!tandas.TryGetValue(clave, out var lista)) tandas[clave] = lista = new List<Matrix4x4>();
                lista.Add(p.Matriz * especies[p.Especie].Local[p.Variante, nivel]);
            }
        }
        foreach (var kv in tandas)
        {
            var (e, v, n) = kv.Key;
            var malla = especies[e].Mallas[v, n];
            var mats = especies[e].Materiales[v, n];
            var lista = kv.Value;
            if (malla == null || lista.Count == 0) continue;
            for (int s = 0; s < Mathf.Min(mats.Length, malla.subMeshCount); s++)
            {
                var rp = new RenderParams(mats[s]) { shadowCastingMode = especies[e].Nombre == "hierba" || especies[e].Nombre == "flor" ? ShadowCastingMode.Off : ShadowCastingMode.On, receiveShadows = true };
                for (int desde = 0; desde < lista.Count; desde += 1023)
                    Graphics.RenderMeshInstanced(rp, malla, s, lista, Mathf.Min(1023, lista.Count - desde), desde);
            }
        }
    }
}
