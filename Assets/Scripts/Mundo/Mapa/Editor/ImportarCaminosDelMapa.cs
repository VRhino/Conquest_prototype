// Las texturas de los caminos del mapa de mundo (`Assets/Resources/MapaMundo/Caminos`): `GenerarTexturas` las pinta con el pintor del
// prototipo portado (`TexturaDeCamino`) y las guarda como PNG; luego se pueden cambiar por otras sin tocar código. Al importarlas: sRGB, de lado
// a lado sin repetir (clamp) y a lo largo repitiendo, con mipmaps y anisotropía 8, como en el prototipo.
//   Unity.exe -batchmode -quit -projectPath . -executeMethod ConquestTactics.Mundo.Mapa.Editor.ImportarCaminosDelMapa.GenerarTexturas
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa.Editor
{
    sealed class ImportarCaminosDelMapa : AssetPostprocessor
    {
        const string Carpeta = "Assets/Resources/MapaMundo/Caminos/";

        [MenuItem("Conquest/Mapa/Generar las texturas de los caminos")]
        public static void GenerarTexturas()
        {
            Directory.CreateDirectory(Carpeta);
            foreach (var (nombre, escalon) in new[] { ("sendero", 0), ("camino", 1) })
            {
                var t = new Texture2D(TexturaDeCamino.AnchoPx, TexturaDeCamino.LargoPx, TextureFormat.RGBA32, false);
                t.SetPixelData(TexturaDeCamino.Pintar(escalon), 0); // fila 0 = v 0, como el prototipo (se repite a lo largo)
                t.Apply(false);
                File.WriteAllBytes(Carpeta + nombre + ".png", t.EncodeToPNG());
                Object.DestroyImmediate(t);
            }
            AssetDatabase.Refresh();
            Debug.Log("[Caminos] texturas de sendero y camino guardadas en " + Carpeta);
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Carpeta)) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            // el detalle y la normal del estilo «Mejorado» (BronzeAgeArte/mapa/caminos/generar.py) son datos, no color: lineales y con el alfa tal cual
            bool datos = assetPath.Contains("_detalle") || assetPath.Contains("_normal");
            t.sRGBTexture = !datos;
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = !datos;
            t.wrapModeU = TextureWrapMode.Clamp;
            t.wrapModeV = TextureWrapMode.Repeat;
            t.mipmapEnabled = true;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
