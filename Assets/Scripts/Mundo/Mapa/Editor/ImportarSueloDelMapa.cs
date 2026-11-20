// Cómo se importan las texturas del suelo del mapa de mundo (`Assets/Resources/MapaMundo/Suelo`, las saca
// `BronzeAgeArte/mapa/suelo/generar.py`): cada tira de 8 capas, una debajo de otra, es un Texture2DArray de 8 cortes. El color va en sRGB; la
// normal y la máscara (altura y suavidad), en lineal. Y `suelo_ruido`, el ruido suave de la variación grande. Así, al regenerarlas y copiarlas, se importan bien sin tocar nada.
using UnityEditor;

namespace ConquestTactics.Mundo.Mapa.Editor
{
    sealed class ImportarSueloDelMapa : AssetPostprocessor
    {
        const string Carpeta = "Assets/Resources/MapaMundo/Suelo/";
        const int Capas = 8;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Carpeta)) return;
            var t = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("suelo_ruido.png"))
            {
                // El ruido suave de la variación grande: una textura normal, lineal, que se repite.
                t.textureType = TextureImporterType.Default;
                t.textureShape = TextureImporterShape.Texture2D;
                t.sRGBTexture = false;
                t.alphaSource = TextureImporterAlphaSource.FromInput;
                t.mipmapEnabled = true;
                t.wrapMode = UnityEngine.TextureWrapMode.Repeat;
                t.filterMode = UnityEngine.FilterMode.Bilinear;
                t.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            t.textureType = TextureImporterType.Default;
            t.textureShape = TextureImporterShape.Texture2DArray;
            var s = new TextureImporterSettings();
            t.ReadTextureSettings(s);
            s.flipbookRows = Capas;
            s.flipbookColumns = 1;
            t.SetTextureSettings(s);
            t.sRGBTexture = assetPath.EndsWith("suelo_color.png");
            t.alphaSource = assetPath.EndsWith("suelo_mascara.png") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            t.mipmapEnabled = true;
            t.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            t.filterMode = UnityEngine.FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.maxTextureSize = 8192; // la tira entera mide 1024 × 8192: con menos, Unity la encogería
            t.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
