using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Data.Items;
using Data.Avatar;
using Object = UnityEngine.Object;

public static class ValidateBattleVisuals
{
    public static string Run()
    {
        var config = AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>("Assets/ScriptableObjects/VisualPrefabConfig.asset");
        var db = Resources.Load<AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
        var results = new List<string>();
        foreach (var entry in config.UnitPrefabs)
        {
            string name = entry.unitId == "UnitVisual_Pikemen" ? "Pikemen" : entry.unitId;
            results.Add(BattleVisualPrefabBuilder.Validate(AssetDatabase.GetAssetPath(entry.visualPrefab), "Assets/Prefabs/BattleVisuals/" + name + ".prefab"));
        }
        var items = Resources.LoadAll<ItemDataSO>("Items").Where(x => !string.IsNullOrEmpty(x.visualPartId)
            && (x.itemType == ItemType.Armor || x.itemType == ItemType.Weapon)).ToArray();
        foreach (var hero in config.HeroPrefabs)
        foreach (var gender in new[] { "Male", "Female" })
        {
            var avatar = new AvatarParts { headId = db.faceParts[0].id, hairId = db.hairParts[0].id,
                eyebrowId = db.eyebrowsParts[0].id, beardId = gender == "Male" ? db.beardParts[0].id : "" };
            var snapshots = new List<HeroAppearanceComponent> { HeroAppearanceComponent.Capture(avatar, new Equipment(), gender) };
            foreach (var item in items)
            {
                var equipment = new Equipment();
                var instance = new InventoryItem { itemId = item.id };
                if (item.itemType == ItemType.Weapon) equipment.weapon = instance;
                else switch (item.itemCategory)
                {
                    case ItemCategory.Helmet: equipment.helmet = instance; break;
                    case ItemCategory.Torso: equipment.torso = instance; break;
                    case ItemCategory.Gloves: equipment.gloves = instance; break;
                    case ItemCategory.Pants: equipment.pants = instance; break;
                    case ItemCategory.Boots: equipment.boots = instance; break;
                }
                snapshots.Add(HeroAppearanceComponent.Capture(avatar, equipment, gender));
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BattleVisuals/" + hero.heroId + ".prefab");
            foreach (var snapshot in snapshots)
            {
                var original = Object.Instantiate(hero.visualPrefab);
                var lightweight = Object.Instantiate(prefab);
                try
                {
                    if (!HeroVisualAppearanceSystem.ApplyHeroVisualCustomization(original, snapshot)
                        || !HeroVisualAppearanceSystem.ApplyHeroVisualCustomization(lightweight, snapshot)) throw new Exception("Appearance was not applied.");
                    Compare(original, lightweight);
                }
                finally { Object.DestroyImmediate(original); Object.DestroyImmediate(lightweight); }
            }
            results.Add(hero.heroId + " " + gender + ": " + snapshots.Count + " appearances matched");
        }
        System.IO.Directory.CreateDirectory("Logs/BattleVisuals");
        System.IO.File.WriteAllLines("Logs/BattleVisuals/validation.txt", results);
        return string.Join("\n", results);
    }

    static void Compare(GameObject original, GameObject lightweight)
    {
        string Path(Transform node, GameObject root) => AnimationUtility.CalculateTransformPath(node, root.transform);
        var expected = original.GetComponentsInChildren<Renderer>().Where(x => x.enabled).ToArray();
        var actual = lightweight.GetComponentsInChildren<Renderer>().Where(x => x.enabled).ToArray();
        if (expected.Length != actual.Length) throw new Exception("Visible renderer count differs: " + expected.Length + " vs " + actual.Length);
        foreach (var renderer in expected)
        {
            string path = Path(renderer.transform, original);
            var other = lightweight.transform.Find(path)?.GetComponent<Renderer>();
            if (other == null || !other.gameObject.activeInHierarchy || !other.enabled || !renderer.sharedMaterials.SequenceEqual(other.sharedMaterials))
                throw new Exception("Renderer mismatch: " + path);
            if (renderer is SkinnedMeshRenderer skin)
            {
                var actualSkin = (SkinnedMeshRenderer)other;
                if (skin.sharedMesh != actualSkin.sharedMesh || skin.localBounds != actualSkin.localBounds
                    || Path(skin.rootBone, original) != Path(actualSkin.rootBone, lightweight)
                    || !skin.bones.Select(x => Path(x, original)).SequenceEqual(actualSkin.bones.Select(x => Path(x, lightweight))))
                    throw new Exception("Skin mismatch: " + path);
            }
        }
        if (original.GetComponentsInChildren<Collider>(true).Length != lightweight.GetComponentsInChildren<Collider>(true).Length)
            throw new Exception("Collider count differs.");
    }
}
