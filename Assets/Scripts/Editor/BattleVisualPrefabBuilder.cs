using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Produces fixed troop visuals while preserving referenced rig/gameplay nodes.</summary>
public static class BattleVisualPrefabBuilder
{
    const string OutputFolder = "Assets/Prefabs/BattleVisuals/";

    [MenuItem("Tools/Characters/Generate Selected Battle Visual Configuration")]
    static void GenerateSelected()
    {
        if (!(Selection.activeObject is VisualPrefabConfiguration config))
            throw new InvalidOperationException("Select a VisualPrefabConfiguration asset first.");
        Debug.Log(GenerateConfiguration(AssetDatabase.GetAssetPath(config)));
    }

    public static string GenerateConfiguration(string path)
    {
        var config = AssetDatabase.LoadAssetAtPath<VisualPrefabConfiguration>(path);
        if (config == null) throw new ArgumentException("Missing visual configuration: " + path);
        var assignments = new List<Action>();
        var reports = new List<string>();
        foreach (var entry in config.HeroPrefabs)
        {
            if (entry.visualPrefab == null) continue;
            string source = AssetDatabase.GetAssetPath(entry.visualPrefab);
            string destination = OutputFolder + entry.heroId + ".prefab";
            bool modular = entry.visualPrefab.GetComponent<AvatarPartSelector>() != null;
            reports.Add(Generate(source, destination, modular));
            var generated = AssetDatabase.LoadAssetAtPath<GameObject>(destination);
            assignments.Add(() => entry.battleVisualPrefab = generated);
        }
        foreach (var entry in config.UnitPrefabs)
        {
            if (entry.visualPrefab == null) continue;
            string source = AssetDatabase.GetAssetPath(entry.visualPrefab);
            string destination = OutputFolder + entry.unitId.Replace("UnitVisual_", "") + ".prefab";
            reports.Add(Generate(source, destination));
            reports.Add(Validate(source, destination));
            var generated = AssetDatabase.LoadAssetAtPath<GameObject>(destination);
            assignments.Add(() => entry.battleVisualPrefab = generated);
        }
        foreach (var assign in assignments) assign();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        return string.Join("\n", reports);
    }

    public static string Validate(string sourcePath, string destinationPath)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        var target = AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath);
        if (source == null || target == null) throw new ArgumentException("Both prefab assets are required.");
        int checkedRenderers = 0;
        foreach (var renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!IsActiveWithin(renderer.transform, source.transform)) continue;
            var path = AnimationUtility.CalculateTransformPath(renderer.transform, source.transform);
            var node = target.transform.Find(path);
            var other = node != null ? node.GetComponent<SkinnedMeshRenderer>() : null;
            if (other == null || other.sharedMesh != renderer.sharedMesh || !other.sharedMaterials.SequenceEqual(renderer.sharedMaterials)
                || other.transform.localPosition != renderer.transform.localPosition || other.transform.localRotation != renderer.transform.localRotation
                || other.transform.localScale != renderer.transform.localScale || other.bones.Length != renderer.bones.Length
                || !IsActiveWithin(other.transform, target.transform))
                throw new InvalidOperationException("Visible geometry mismatch: " + path);
            for (int i = 0; i < renderer.bones.Length; i++)
                if (AnimationUtility.CalculateTransformPath(renderer.bones[i], source.transform)
                    != AnimationUtility.CalculateTransformPath(other.bones[i], target.transform))
                    throw new InvalidOperationException("Bone mismatch: " + path);
            checkedRenderers++;
        }
        if (source.GetComponentsInChildren<Collider>(true).Length != target.GetComponentsInChildren<Collider>(true).Length
            || source.GetComponentsInChildren<WeaponHitboxBehaviour>(true).Length != target.GetComponentsInChildren<WeaponHitboxBehaviour>(true).Length
            || source.GetComponentsInChildren<ShieldHitboxBehaviour>(true).Length != target.GetComponentsInChildren<ShieldHitboxBehaviour>(true).Length)
            throw new InvalidOperationException("Gameplay component count mismatch.");
        return destinationPath + ": geometry/bones/materials matched for " + checkedRenderers + " active skin renderers; collider/hitbox counts matched.";
    }

    public static string Generate(string sourcePath, string destinationPath, bool hero = false)
    {
        if (sourcePath == destinationPath) throw new ArgumentException("Source must remain an authoring asset.");
        var root = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            int before = root.GetComponentsInChildren<Transform>(true).Length;
            // These authoring lists reference every alternative and must not ship on a fixed troop.
            foreach (var manager in root.GetComponentsInChildren<BattleDrakeStudios.ModularCharacters.ModularCharacterManager>(true))
                Object.DestroyImmediate(manager);

            var candidates = new HashSet<GameObject>(root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => hero || !IsActiveWithin(r.transform, root.transform))
                .Where(r => r.transform.childCount == 0 && r.GetComponents<Component>().All(c => c is Transform || c is SkinnedMeshRenderer))
                .Select(r => r.gameObject));

            // A script, rig, animation controller or another renderer may need an inactive node.
            var protectedObjects = new HashSet<GameObject>();
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("Missing script in source prefab.");
                if (component is Transform || candidates.Contains(component.gameObject)) continue;
                using var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var referenced = property.objectReferenceValue;
                    if (referenced is GameObject go) protectedObjects.Add(go);
                    else if (referenced is Component target) protectedObjects.Add(target.gameObject);
                }
            }
            // Animation bindings can target inactive geometry by hierarchy path.
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController == null) continue;
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                foreach (var binding in AnimationUtility.GetCurveBindings(clip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)))
                {
                    var target = animator.transform.Find(binding.path);
                    if (target != null) protectedObjects.Add(target.gameObject);
                }
            }
            var recipes = new List<BattleSkinCatalog.Part>();
            foreach (var go in candidates)
                if (!protectedObjects.Contains(go))
                {
                    if (hero) recipes.Add(Capture(go.GetComponent<SkinnedMeshRenderer>(), root.transform));
                    Object.DestroyImmediate(go);
                }

            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.sharedMesh != null && (renderer.rootBone == null || renderer.bones.Any(b => b == null)))
                    throw new InvalidOperationException("Invalid skin bindings: " + renderer.name);

            var folder = System.IO.Path.GetDirectoryName(destinationPath).Replace('\\', '/');
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            if (hero)
            {
                string catalogPath = System.IO.Path.ChangeExtension(destinationPath, ".Skin.asset");
                var catalog = AssetDatabase.LoadAssetAtPath<BattleSkinCatalog>(catalogPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<BattleSkinCatalog>();
                    AssetDatabase.CreateAsset(catalog, catalogPath);
                }
                catalog.parts = recipes.ToArray();
                EditorUtility.SetDirty(catalog);
                var assembler = root.GetComponent<BattleHeroVisual>() ?? root.AddComponent<BattleHeroVisual>();
                assembler.catalog = catalog;
                AssetDatabase.SaveAssets();
            }
            var saved = PrefabUtility.SaveAsPrefabAsset(root, destinationPath);
            if (saved == null) throw new InvalidOperationException("Prefab could not be saved.");
            return sourcePath + " -> " + destinationPath + ": transforms " + before + " -> " + saved.GetComponentsInChildren<Transform>(true).Length
                + ", skinned " + saved.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static BattleSkinCatalog.Part Capture(SkinnedMeshRenderer r, Transform root)
    {
        string Path(Transform node)
        {
            if (node == null || !node.IsChildOf(root)) throw new InvalidOperationException("Missing/external rig binding: " + r.name);
            return AnimationUtility.CalculateTransformPath(node, root);
        }
        if (r.sharedMesh == null) throw new InvalidOperationException("Missing mesh: " + r.name);
        return new BattleSkinCatalog.Part
        {
            parentPath = Path(r.transform.parent), name = r.name,
            rootBonePath = Path(r.rootBone), bonePaths = r.bones.Select(Path).ToArray(),
            probeAnchorPath = r.probeAnchor == null ? null : Path(r.probeAnchor),
            mesh = r.sharedMesh, materials = r.sharedMaterials,
            position = r.transform.localPosition, rotation = r.transform.localRotation, scale = r.transform.localScale,
            bounds = r.localBounds, active = r.gameObject.activeSelf, enabled = r.enabled,
            quality = r.quality, updateWhenOffscreen = r.updateWhenOffscreen, skinnedMotionVectors = r.skinnedMotionVectors,
            shadows = r.shadowCastingMode, receiveShadows = r.receiveShadows, lightProbes = r.lightProbeUsage,
            reflectionProbes = r.reflectionProbeUsage, motionVectors = r.motionVectorGenerationMode,
            renderingLayerMask = r.renderingLayerMask, sortingLayerID = r.sortingLayerID, sortingOrder = r.sortingOrder,
            blendWeights = Enumerable.Range(0, r.sharedMesh.blendShapeCount).Select(r.GetBlendShapeWeight).ToArray()
        };
    }

    static bool IsActiveWithin(Transform node, Transform root)
    {
        for (var current = node; current != null; current = current.parent)
        {
            if (!current.gameObject.activeSelf) return false;
            if (current == root) return true;
        }
        return false;
    }
}
