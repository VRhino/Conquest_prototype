// El sigilo de Facción como textura (paso 8 del bloque 4, tanda 1): el SVG de `Sigilos` (el mismo del prototipo) leído con el paquete de gráficos
// vectoriales de Unity, teselado y pintado a una Texture2D. Vale igual para UI Toolkit (fondo de un elemento), para la UI de siempre (Sprite) y para la
// tela de un estandarte en 3D. Se guarda por sus 8 ids y su tamaño: cada sigilo se dibuja una sola vez.
using System.Collections.Generic;
using System.IO;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo;
using Unity.VectorGraphics;
using UnityEngine;

public static class TexturaDeSigilo
{
    static readonly Dictionary<string, Texture2D> texturas = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    static Material material;

    /// <summary>Carga el catálogo, los emblemas y las insignias del prototipo (`Resources/MundoUI/Sigilo`).</summary>
    public static void Asegurar()
    {
        if (Sigilos.Cargado) return;
        string Texto(string n) => Resources.Load<TextAsset>("MundoUI/Sigilo/" + n)?.text;
        var catalogo = Texto("catalogoSigilos");
        if (catalogo == null) { Debug.LogWarning("[Sigilo] sin catálogo en Resources/MundoUI/Sigilo"); return; }
        Sigilos.Cargar(catalogo, Texto("emblemas") ?? "{}", Texto("insignias") ?? "{}");
    }

    /// <summary>La textura del sigilo de `ancho` píxeles (el alto, el del lienzo: ×1,2, o ×1,5 con la corona del Gran Rey). Sin sigilo, el neutro.</summary>
    public static Texture2D De(Sigilo s, int ancho, bool granRey = false)
    {
        Asegurar();
        var clave = $"{Sigilos.Clave(s)}|{ancho}|{granRey}";
        if (texturas.TryGetValue(clave, out var hecha) && hecha != null) return hecha;
        var alto = Mathf.RoundToInt(ancho * (granRey ? 1.5f : 1.2f));
        // El lienzo entero del SVG (100×120, o 100×150 con la corona): sin él, el sprite toma el tamaño de lo dibujado y las formas estrechas (el
        // ocho, el rombo) salen estiradas.
        var tex = Pintar(Sigilos.Svg(s, ancho, granRey), new Rect(0, 0, 100, granRey ? 150 : 120), ancho, alto);
        tex.name = "Sigilo " + clave;
        texturas[clave] = tex;
        return tex;
    }

    static Texture2D Pintar(string svg, Rect lienzo, int ancho, int alto)
    {
        var escena = SVGParser.ImportSVG(new StringReader(svg)).Scene;
        var geometria = VectorUtils.TessellateScene(escena, new VectorUtils.TessellationOptions
        {
            StepDistance = 100f, MaxCordDeviation = 0.05f, MaxTanAngleDeviation = 0.05f, SamplingStepSize = 0.01f,
        });
        var sprite = VectorUtils.BuildSprite(geometria, lienzo, 100f, VectorUtils.Alignment.SVGOrigin, Vector2.zero, 128, true);
        if (material == null) material = new Material(Shader.Find("Unlit/Vector") ?? Shader.Find("Sprites/Default"));
        var tex = VectorUtils.RenderSpriteToTexture2D(sprite, ancho, alto, material, 4);
        // En espacio lineal los colores del SVG (sRGB) salen dos veces convertidos, más claros: se devuelven a los del prototipo.
        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
        {
            var px = tex.GetPixels();
            for (var i = 0; i < px.Length; i++) { var c = px[i]; px[i] = new Color(Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b), c.a); }
            tex.SetPixels(px);
            tex.Apply();
        }
        if (Application.isPlaying) Object.Destroy(sprite); else Object.DestroyImmediate(sprite);
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    /// <summary>La insignia de un título (`svgInsignia`): el medallón de bronce con su icono de `insignias.json`, de `lado` píxeles.</summary>
    public static Texture2D Insignia(string tituloId, int lado)
    {
        Asegurar();
        var clave = $"insignia|{tituloId}|{lado}";
        if (texturas.TryGetValue(clave, out var hecha) && hecha != null) return hecha;
        var tex = Pintar(Sigilos.SvgInsignia(tituloId, lado), new Rect(0, 0, 100, 100), lado, lado);
        tex.name = clave;
        texturas[clave] = tex;
        return tex;
    }

    /// <summary>El mismo, como Sprite (para un `Image` de la UI de siempre).</summary>
    public static Sprite SpriteDe(Sigilo s, int ancho, bool granRey = false)
    {
        var tex = De(s, ancho, granRey);
        if (sprites.TryGetValue(tex.name, out var hecho) && hecho != null) return hecho;
        var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        sp.name = tex.name;
        sprites[tex.name] = sp;
        return sp;
    }
}
