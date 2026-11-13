// Los ríos y sus vados (del artefacto) y la niebla de guerra (de la sesión), en el mapa de mundo.
using System.Collections.Generic;
using BronzeAge.Contract.V1;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConquestTactics.Mundo.Mapa
{
    /// <summary>Los ríos como cintas planas de agua pegadas al relieve (3 u los navegables, 1,5 u los demás) y los vados como pasos de grava de
    /// orilla a orilla (BronzeAgeClient: `src/mundo3d/paisaje.ts`, sin su pintura de orillas). Donde el suelo ya es agua (un lago, el mar) el río
    /// se acaba. Se hacen una vez, todos: son pocos.</summary>
    public static class RiosDelMapa
    {
        const float Sobre = 0.3f, PasoMuestra = 1f, AnchoVado = 0.8f;

        public static GameObject Construir(ArtefactoDeMundo a, Relieve relieve, Material material, Transform padre)
        {
            var e = EscalaDelMapa.MetrosPorUnidad;
            var v = new List<Vector3>();
            var c = new List<Color32>();
            var t = new List<int>();
            void Cinta(List<Vector2> puntos, float ancho, Color color)
            {
                for (int i = 0; i < puntos.Count; i++)
                {
                    var p = puntos[i];
                    var dir = (puntos[Mathf.Min(i + 1, puntos.Count - 1)] - puntos[Mathf.Max(i - 1, 0)]).normalized;
                    var normal = new Vector2(-dir.y, dir.x) * (ancho / 2);
                    var h = (float)relieve.AlturaEn(p.x, p.y) + Sobre;
                    if (i > 0)
                    {
                        int b = v.Count - 2;
                        t.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                    }
                    v.Add(EscalaDelMapa.AEscena(p.x + normal.x, p.y + normal.y, h));
                    v.Add(EscalaDelMapa.AEscena(p.x - normal.x, p.y - normal.y, h));
                    c.Add(color.linear);
                    c.Add(color.linear);
                }
            }
            foreach (var rio in a.Rios)
            {
                var ancho = rio.Navegable ? PinturaDelMapa.RioNavegable : PinturaDelMapa.Rio;
                var tramo = new List<Vector2>();
                for (int i = 0; i + 1 < rio.Puntos.Count; i++)
                {
                    Vector2 p0 = Punto(rio.Puntos[i]), p1 = Punto(rio.Puntos[i + 1]);
                    int pasos = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p0, p1) / PasoMuestra));
                    for (int k = 0; k < pasos; k++)
                    {
                        var p = Vector2.Lerp(p0, p1, k / (float)pasos);
                        if (relieve.AguaEn(p.x, p.y)) { if (tramo.Count > 1) Cinta(tramo, ancho, PinturaDelMapa.AguaSomera); tramo.Clear(); }
                        else tramo.Add(p);
                    }
                }
                if (tramo.Count > 1) Cinta(tramo, ancho, PinturaDelMapa.AguaSomera);
                foreach (var vado in rio.Vados)
                {
                    var p = Punto(vado);
                    if (relieve.AguaEn(p.x, p.y)) continue;
                    var dir = Rumbo(rio, p);
                    var largo = (ancho * 1.4f + 0.6f) / 2;
                    var normal = new Vector2(-dir.y, dir.x);
                    Cinta(new List<Vector2> { p - normal * largo, p + normal * largo }, AnchoVado, PinturaDelMapa.Vado);
                }
            }
            var malla = new Mesh { name = "rios", indexFormat = IndexFormat.UInt32 };
            malla.SetVertices(v);
            malla.SetColors(c);
            malla.SetTriangles(t, 0);
            malla.RecalculateNormals();
            var go = new GameObject("Rios");
            go.transform.SetParent(padre, false);
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static Vector2 Punto(Punto p) => new Vector2(p.X / 1000f, p.Y / 1000f);

        /// <summary>La dirección del río junto al punto: la del tramo más cercano.</summary>
        static Vector2 Rumbo(Rio rio, Vector2 p)
        {
            var mejor = Vector2.right;
            var dmin = float.MaxValue;
            for (int i = 0; i + 1 < rio.Puntos.Count; i++)
            {
                Vector2 a = Punto(rio.Puntos[i]), b = Punto(rio.Puntos[i + 1]);
                var d = Vector2.Distance(p, (a + b) / 2);
                if (d < dmin && a != b) { dmin = d; mejor = (b - a).normalized; }
            }
            return mejor;
        }
    }

    /// <summary>La niebla de guerra (la `NieblaProyectada` de la VISTA, celdas de 5 u): una textura de la rejilla (R = explorado, G = a la vista)
    /// que el sombreado mezcla con su color en tres estados. Se rehace solo cuando cambia la VISTA.</summary>
    public sealed class NieblaDelMapa
    {
        static readonly int Tex = Shader.PropertyToID("_NieblaTex"), Rejilla = Shader.PropertyToID("_NieblaRejilla"),
            Tamano = Shader.PropertyToID("_NieblaTamano"), ColorNiebla = Shader.PropertyToID("_NieblaColor"), Alfas = Shader.PropertyToID("_NieblaAlfas");

        Texture2D textura;
        byte[] pixeles;

        public NieblaDelMapa()
        {
            Shader.SetGlobalVector(ColorNiebla, PinturaDelMapa.Niebla.linear);
            Shader.SetGlobalVector(Alfas, new Vector4(PinturaDelMapa.AlfaSinVer, PinturaDelMapa.AlfaRecordado, PinturaDelMapa.AlfaVisible, 0));
            Shader.SetGlobalVector(Rejilla, Vector4.zero); // sin niebla hasta que llegue la VISTA
        }

        public void Poner(NieblaProyectada n)
        {
            if (n == null || n.Columnas == 0) { Shader.SetGlobalVector(Rejilla, Vector4.zero); return; }
            int columnas = (int)n.Columnas, filas = (int)n.Filas;
            if (textura == null || textura.width != columnas || textura.height != filas)
            {
                if (textura != null) Object.Destroy(textura);
                textura = new Texture2D(columnas, filas, TextureFormat.RG16, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                pixeles = new byte[columnas * filas * 2];
            }
            var celdas = n.Celdas.Span;
            var visibles = n.Visibles.Span;
            for (int i = 0; i < columnas * filas; i++)
            {
                pixeles[2 * i] = Bit(celdas, i) ? (byte)255 : (byte)0;
                pixeles[2 * i + 1] = Bit(visibles, i) ? (byte)255 : (byte)0;
            }
            textura.SetPixelData(pixeles, 0);
            textura.Apply(false);
            Shader.SetGlobalTexture(Tex, textura);
            Shader.SetGlobalVector(Tamano, new Vector4(columnas, filas, 0, 0));
            Shader.SetGlobalVector(Rejilla, new Vector4(0, 0, n.TamanoCelda / 1000f * EscalaDelMapa.MetrosPorUnidad, 1));
        }

        static bool Bit(System.ReadOnlySpan<byte> m, int i) => (i >> 3) < m.Length && ((m[i >> 3] >> (i & 7)) & 1) == 1;

        public void Destruir()
        {
            if (textura != null) Object.Destroy(textura);
            Shader.SetGlobalVector(Rejilla, Vector4.zero);
        }
    }
}
