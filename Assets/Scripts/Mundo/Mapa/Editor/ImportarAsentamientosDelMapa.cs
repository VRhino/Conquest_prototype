// Cómo se importan las maquetas de asentamientos «Mejorado» del mapa de mundo (`Assets/Resources/MapaMundo/Asentamientos`, las saca
// `BronzeAgeArte/mapa/asentamientos/plaza.py`): un FBX por maqueta (la villa y su muralla, <maqueta>_<v>_[muralla_]<cerca|medio|lejos>, de caras planas, con la huella de
// radio 1 y la base en 0) y su color (sRGB) y su máscara (lineal: R la oclusión, G las banderas) horneados; y, si la tiene, la máscara del suelo
// pisado (`_suelo`, lineal, R) para su parche. Al regenerarlos y copiarlos, se
// importan bien sin tocar nada.
using UnityEditor;

namespace ConquestTactics.Mundo.Mapa.Editor
{
    sealed class ImportarAsentamientosDelMapa : AssetPostprocessor
    {
        const string Carpeta = "Assets/Resources/MapaMundo/Asentamientos/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Carpeta)) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = 1;
            m.useFileScale = true;
            m.importNormals = ModelImporterNormals.Import; // las de cada cara (facetas)
            m.importTangents = ModelImporterTangents.None;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importAnimation = false;
            m.animationType = ModelImporterAnimationType.None;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.isReadable = false;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.addCollider = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Carpeta)) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.textureShape = TextureImporterShape.Texture2D;
            t.sRGBTexture = assetPath.EndsWith("_color.png");
            t.alphaSource = TextureImporterAlphaSource.None;
            t.mipmapEnabled = true;
            t.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            t.filterMode = UnityEngine.FilterMode.Trilinear;
            t.anisoLevel = 4;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
