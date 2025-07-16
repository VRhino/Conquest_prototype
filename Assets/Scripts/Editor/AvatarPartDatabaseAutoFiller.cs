using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Data.Avatar;

public class AvatarPartDatabaseAutoFiller : EditorWindow
{
    private TextAsset bodyPartsFile;
    private AvatarPartDatabase databaseAsset;

    [MenuItem("Tools/Auto-Fill Avatar Part Database")]
    public static void ShowWindow()
    {
        GetWindow<AvatarPartDatabaseAutoFiller>("Auto-Fill AvatarPartDatabase");
    }

    void OnGUI()
    {
        GUILayout.Label("Auto-Fill AvatarPartDatabase.asset", EditorStyles.boldLabel);
        bodyPartsFile = (TextAsset)EditorGUILayout.ObjectField("bodyPartsAttachments.txt", bodyPartsFile, typeof(TextAsset), false);
        databaseAsset = (AvatarPartDatabase)EditorGUILayout.ObjectField("AvatarPartDatabase.asset", databaseAsset, typeof(AvatarPartDatabase), false);

        if (GUILayout.Button("Fill Database") && bodyPartsFile && databaseAsset)
        {
            FillDatabase();
            EditorUtility.SetDirty(databaseAsset);
            AssetDatabase.SaveAssets();
            Debug.Log("AvatarPartDatabase filled!");
        }
    }

    void FillDatabase()
    {
        var lines = bodyPartsFile.text.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l));
        var grouped = new Dictionary<string, AvatarPartDefinition>();

        foreach (var line in lines)
        {
            var parts = line.Split('/');
            string prefab = parts.Last();
            string slot = GetSlotFromPath(line);
            int slotIndex = GetSlotIndex(slot);
            string bone = parts.Length > 2 ? parts[parts.Length - 2] : "";
            bool isMale = line.Contains("Male_");
            bool isFemale = line.Contains("Female_");
            bool isAll = line.Contains("All_");

            // Extraer id base: head_00, eyebrow_01, etc.
            string idBase = ExtractIdBase(prefab, slot);

            if (!grouped.TryGetValue(idBase, out var def))
            {
                def = new AvatarPartDefinition
                {
                    id = idBase,
                    displayName = idBase,
                    slot = (AvatarSlot)slotIndex,
                    attachments = new List<VisualAttachment>
                    {
                        new VisualAttachment()
                    }
                };
                grouped[idBase] = def;
            }

            var att = def.attachments[0];
            att.visualID = idBase;

            if (isMale)
            {
                att.boneTargetMale = bone;
                att.prefabPathMale = prefab;
            }
            else if (isFemale)
            {
                att.boneTargetFemale = bone;
                att.prefabPathFemale = prefab;
            }
            else if (isAll)
            {
                att.boneTargetMale = bone;
                att.boneTargetFemale = bone;
                att.prefabPathMale = prefab;
                att.prefabPathFemale = prefab;
            }
        }

        databaseAsset.faceParts = new List<AvatarPartDefinition>();
        databaseAsset.hairParts = new List<AvatarPartDefinition>();
        databaseAsset.eyebrowsParts = new List<AvatarPartDefinition>();
        databaseAsset.beardParts = new List<AvatarPartDefinition>();
        databaseAsset.torsoParts = new List<AvatarPartDefinition>();
        databaseAsset.glovesParts = new List<AvatarPartDefinition>();
        databaseAsset.pantsParts = new List<AvatarPartDefinition>();
        databaseAsset.headParts = new List<AvatarPartDefinition>();
        databaseAsset.bootsParts = new List<AvatarPartDefinition>();

        foreach (var def in grouped.Values)
        {
            switch (def.slot)
            {
                case AvatarSlot.Hair: databaseAsset.hairParts.Add(def); break;
                case AvatarSlot.Head: databaseAsset.headParts.Add(def); break;
                case AvatarSlot.Eyebrows: databaseAsset.eyebrowsParts.Add(def); break;
                case AvatarSlot.Beard: databaseAsset.beardParts.Add(def); break;
                case AvatarSlot.Face: databaseAsset.faceParts.Add(def); break;
                case AvatarSlot.Torso: databaseAsset.torsoParts.Add(def); break;
                case AvatarSlot.Gloves: databaseAsset.glovesParts.Add(def); break;
                case AvatarSlot.Pants: databaseAsset.pantsParts.Add(def); break;
                case AvatarSlot.Boots: databaseAsset.bootsParts.Add(def); break;
            }
        }
    }

    static string GetSlotFromPath(string path)
    {
        // Orden específico para evitar colisiones
        if (path.Contains("FacialHair")) return "Beard"; // beardParts
        if (path.Contains("Eyebrow")) return "Eyebrows"; // eyebrowsParts
        if (path.Contains("Hair") && !path.Contains("FacialHair")) return "Hair"; // hairParts
        if (path.Contains("Head")) return "Face"; // faceParts
        if (path.Contains("Torso")) return "Torso";
        if (path.Contains("Gloves")) return "Gloves";
        if (path.Contains("Pants")) return "Pants";
        if (path.Contains("Boots")) return "Boots";
        if (path.Contains("Face")) return "Face";
        return "Face";
    }

    static int GetSlotIndex(string slot)
    {
        switch (slot)
        {
            case "Head": return (int)AvatarSlot.Head;
            case "Torso": return (int)AvatarSlot.Torso;
            case "Gloves": return (int)AvatarSlot.Gloves;
            case "Pants": return (int)AvatarSlot.Pants;
            case "Boots": return (int)AvatarSlot.Boots;
            case "Hair": return (int)AvatarSlot.Hair;
            case "Eyebrows": return (int)AvatarSlot.Eyebrows;
            case "Beard": return (int)AvatarSlot.Beard;
            case "Face": return (int)AvatarSlot.Face;
            default: return (int)AvatarSlot.Head;
        }
    }

    // Extrae el id base agrupando por tipo y número, ignorando el género
    static string ExtractIdBase(string prefab, string slot)
    {
        // Ejemplo: Chr_Head_Female_00, Chr_Head_Male_00, Chr_Eyebrow_Female_01
        string s = prefab;
        if (s.StartsWith("Chr_")) s = s.Substring(4);
        // Quitar prefijo de género si existe
        s = s.Replace("Male_", "").Replace("Female_", "");
        // Para casos como Head_00, Eyebrow_01, etc.
        // Si slot está en el nombre, lo dejamos, si no, lo anteponemos
        if (!s.ToLower().StartsWith(slot.ToLower()))
        {
            // Si es solo un número, anteponer el slot
            if (int.TryParse(s, out _))
                s = slot.ToLower() + "_" + s;
        }
        return s.ToLower();
    }
}
