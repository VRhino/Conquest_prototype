// Cómo se importan los nodos de recursos «Mejorado» del mapa de mundo (`Assets/Resources/MapaMundo/Nodos`, los saca
// `BronzeAgeArte/mapa/nodos/nodos.py`): un FBX por tipo y variante (objetos <tipo>_<v>_<cerca|medio|lejos>, de caras planas, con la huella de
// radio 1 y la base en 0) y, por tipo y variante, el color (sRGB) y la máscara (lineal: la oclusión) horneados. Al regenerarlos y copiarlos, se
// importan bien sin tocar nada.
using UnityEditor;

namespace ConquestTactics.Mundo.Mapa.Editor
{
    sealed class ImportarNodosDelMapa : AssetPostprocessor
    {
        const string Carpeta = "Assets/Resources/MapaMundo/Nodos/";

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
