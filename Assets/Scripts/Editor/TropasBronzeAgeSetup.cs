using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the squads of the BronzeAge engine troops (CQ-003): one <see cref="SquadData"/> per engine <c>tropaId</c>, with
/// that id, in Resources/Squads/BronzeAge/&lt;tropaId&gt;. Each one starts from a Conquest base (prefab, animations,
/// formations, abilities) with its own melee/ranged module copied from the base, the engine's soldiers per squad, a
/// rarity from the engine tier and its own rest formation. A troop that already exists is left alone.
/// They are not in the SquadDatabase: the game does not offer them yet; the battle server reads them by id.
/// Batch: <c>-executeMethod TropasBronzeAgeSetup.CrearTodas</c>.
/// </summary>
public static class TropasBronzeAgeSetup
{
    const string Carpeta = "Assets/Resources/Squads/BronzeAge";

    struct Tropa
    {
        public string id, nombre, baseId;
        public int unidades, escalon;
        public RestFormationSetup.Profile reposo;
    }

    /// <summary>Units and tier from the engine catalogue (Docs/Mechanics/Heroe_Y_Tropa.md, TROPAS_RECLUTABLES).</summary>
    static readonly Tropa[] Tropas =
    {
        new Tropa { id = "granjeros", nombre = "Granjeros", baseId = "spm01", unidades = 30, escalon = 1,
            reposo = RestFormationSetup.Loose(2.4f, 0.8f, 35f) },
        new Tropa { id = "milicia_lanceros", nombre = "Milicia de lanceros", baseId = "spm01", unidades = 25, escalon = 2,
            reposo = RestFormationSetup.Block(5, 1.25f, 0.2f, 10f) },
        new Tropa { id = "hoplitas_ciudadanos", nombre = "Hoplitas ciudadanos", baseId = "spm01", unidades = 18, escalon = 3,
            reposo = RestFormationSetup.Block(6, 1.05f, 0.03f, 2f) },
        new Tropa { id = "arqueros", nombre = "Arqueros", baseId = "arc01", unidades = 18, escalon = 3,
            reposo = RestFormationSetup.Block(6, 1.4f, 0.15f, 6f) },
    };

    /// <summary>CQ-003: tier 1 levy … 5 elite, as an initial rarity only (it never decides strategic values).</summary>
    static SquadRarity Rareza(int escalon) => escalon switch
    {
        1 => SquadRarity.levy_tier,
        2 => SquadRarity.trained_tier,
        3 => SquadRarity.seasoned_tier,
        4 => SquadRarity.veteran_tier,
        _ => SquadRarity.elite_tier,
    };

    [MenuItem("Tools/BronzeAge/Crear tropas del motor")]
    public static void CrearTodas()
    {
        var bases = Resources.LoadAll<SquadData>("Squads");
        foreach (var t in Tropas)
        {
            string carpeta = $"{Carpeta}/{t.id}";
            string ruta = $"{carpeta}/{t.id}.asset";
            if (AssetDatabase.LoadAssetAtPath<SquadData>(ruta) != null)
                continue;
            var origen = System.Array.Find(bases, s => s.id == t.baseId);
            if (origen == null)
            {
                Debug.LogError($"[TropasBronzeAge] {t.id}: no existe la base {t.baseId}.");
                continue;
            }
            Directory.CreateDirectory(carpeta);
            AssetDatabase.Refresh();

            var tropa = Object.Instantiate(origen);
            tropa.name = t.id;
            tropa.id = t.id;
            tropa.squadName = t.nombre;
            tropa.unitCount = t.unidades;
            tropa.rarity = Rareza(t.escalon);
            tropa.availableForRecruitmentByDefault = false;
            tropa.restFormation = null;
            AssetDatabase.CreateAsset(tropa, ruta);
            // Own modules (CQ-003): same values as the base for now, free to diverge.
            if (origen.meleeData != null) tropa.meleeData = Copiar(origen.meleeData, $"{carpeta}/{t.id}_Melee.asset");
            if (origen.rangedData != null) tropa.rangedData = Copiar(origen.rangedData, $"{carpeta}/{t.id}_Ranged.asset");
            tropa.restFormation = RestFormationSetup.Create(tropa, ruta, t.reposo);
            EditorUtility.SetDirty(tropa);
            Debug.Log($"[TropasBronzeAge] {t.id}: {t.unidades} soldados, base {t.baseId}, reposo {t.reposo.layout}");
        }
        AssetDatabase.SaveAssets();
    }

    static T Copiar<T>(T origen, string ruta) where T : ScriptableObject
    {
        var copia = Object.Instantiate(origen);
        AssetDatabase.CreateAsset(copia, ruta);
        return copia;
    }
}
