using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the BronzeAge engine hero catalogues (<c>CLASES_HEROE</c> and <c>OBJETOS</c> in the engine's
/// <c>Docs/Mechanics/Objetos_Del_Heroe.md</c>, from the checkout next to this project) as they are, into
/// Resources/Heroe/BronzeAge/clases.json and objetos.json, each with its catalogue version (the one a battle ticket brings in
/// <c>Reglas.VersionCatalogoHeroe</c> and <c>VersionCatalogoObjetos</c>). Read at runtime by <c>CatalogoDelHeroe</c>.
/// Run it again whenever the engine catalogue changes, like <see cref="TropasDelMotor"/>.
/// Batch: <c>-executeMethod HeroeDelMotor.Importar</c>.
/// </summary>
public static class HeroeDelMotor
{
    const string Carpeta = "Assets/Resources/Heroe/BronzeAge";
    const string Catalogo = "../BronzeAgeFase0/Docs/Mechanics/Objetos_Del_Heroe.md";

    [MenuItem("Conquest/BronzeAge/Importar clases y objetos del motor")]
    public static void Importar()
    {
        if (!File.Exists(Catalogo)) { Debug.LogError($"[HeroeDelMotor] No está el catálogo del motor: {Path.GetFullPath(Catalogo)}"); return; }
        var doc = File.ReadAllText(Catalogo);
        Directory.CreateDirectory(Carpeta);
        foreach (var (bloque, fichero) in new[] { ("CLASES_HEROE", "clases"), ("OBJETOS", "objetos") })
        {
            var m = Regex.Match(doc, $@"<!-- parametros:{bloque} -->\s*```json\s*(.*?)```", RegexOptions.Singleline);
            if (!m.Success) { Debug.LogError($"[HeroeDelMotor] Falta el bloque {bloque} en {Catalogo}"); return; }
            File.WriteAllText($"{Carpeta}/{fichero}.json", m.Groups[1].Value.Trim() + "\n");
        }
        AssetDatabase.Refresh();
        Debug.Log($"[HeroeDelMotor] Importados {Carpeta}/clases.json y objetos.json");
    }
}
