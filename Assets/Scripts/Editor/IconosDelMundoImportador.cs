using UnityEditor;
using Unity.VectorGraphics.Editor;

/// <summary>Los iconos provisionales del mundo (`Assets/Resources/MundoUI/Iconos`, sacados de BronzeAgeClient por
/// `Tools/IconosDelPrototipo/generar.mjs`) se importan como VectorImage de UI Toolkit: nítidos a cualquier tamaño y teñibles con
/// `-unity-background-image-tint-color`.</summary>
public sealed class IconosDelMundoImportador : AssetPostprocessor
{
    const string Carpeta = "Assets/Resources/MundoUI/Iconos/";

    void OnPreprocessAsset()
    {
        if (!assetPath.StartsWith(Carpeta) || !(assetImporter is SVGImporter svg)) return;
        if (svg.SvgType == SVGType.VectorImage) return;
        svg.SvgType = SVGType.VectorImage;
    }
}
