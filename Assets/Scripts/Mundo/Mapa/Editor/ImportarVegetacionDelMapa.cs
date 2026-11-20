// Cómo se importa la vegetación del mapa de mundo (`Assets/Resources/MapaMundo/Vegetacion`, la sacan `BronzeAgeArte/mapa/vegetacion/arboles.py`
// y `componer.py`): un FBX por especie (objetos <especie>_<variante>_<cerca|medio|lejos>, en metros, con las normales de la envolvente de la
// copa, que se respetan, y el color de vértice: oclusión en RGB y viento en A) y, por especie, color (sRGB), normal y máscara (lineal) de la
// corteza, los ramilletes (con alfa), el follaje de los grumos y la copa de lejos. Los ramilletes guardan la cobertura del alfa en los mipmaps
// (si no, de lejos las tarjetas se adelgazan hasta desaparecer). Así, al regenerarlos y copiarlos, se importan bien sin tocar nada.
using UnityEditor;
using UnityEngine;

namespace ConquestTactics.Mundo.Mapa.Editor
{
    sealed class ImportarVegetacionDelMapa : AssetPostprocessor
    {
        const string Carpeta = "Assets/Resources/MapaMundo/Vegetacion/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Carpeta)) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = 1;
            m.useFileScale = true;
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.CalculateMikk;
            // Con los materiales (no se usan: los pone VegetacionDelMapa) para que cada material quede en su submalla; sin ellos, Unity las junta.
            m.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            m.materialLocation = ModelImporterMaterialLocation.InPrefab;
            m.importAnimation = false;
            m.animationType = ModelImporterAnimationType.None;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.isReadable = false;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.weldVertices = false; // las tarjetas y los grumos llevan sus normales propias
            m.addCollider = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Carpeta)) return;
            var t = (TextureImporter)assetImporter;
            bool color = assetPath.EndsWith("_color.png"), normal = assetPath.EndsWith("_normal.png");
            bool hojas = assetPath.Contains("_hojas_"), lejos = assetPath.Contains("_lejos_");
            t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.textureShape = TextureImporterShape.Texture2D;
            t.sRGBTexture = color;
            t.alphaSource = (color && hojas) || assetPath.EndsWith("_mascara.png") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            t.alphaIsTransparency = color && hojas;
            t.mipmapEnabled = true;
            t.mipMapsPreserveCoverage = color && hojas;
            t.alphaTestReferenceValue = 0.5f;
            t.wrapMode = hojas || lejos ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
