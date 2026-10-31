using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Shared renderer recipes generated from the authoring rig. No prefab/scene references.</summary>
public sealed class BattleSkinCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Part
    {
        public string parentPath, name, rootBonePath, probeAnchorPath;
        public string[] bonePaths;
        public Mesh mesh;
        public Material[] materials;
        public Vector3 position, scale;
        public Quaternion rotation;
        public Bounds bounds;
        public bool active, enabled, updateWhenOffscreen, skinnedMotionVectors;
        public SkinQuality quality;
        public ShadowCastingMode shadows;
        public bool receiveShadows;
        public LightProbeUsage lightProbes;
        public ReflectionProbeUsage reflectionProbes;
        public MotionVectorGenerationMode motionVectors;
        public uint renderingLayerMask;
        public int sortingLayerID, sortingOrder;
        public float[] blendWeights;

        public SkinnedMeshRenderer Mount(Transform root)
        {
            Transform Resolve(string path) => string.IsNullOrEmpty(path) ? root : root.Find(path)
                ?? throw new InvalidOperationException("Missing rig path: " + path);
            var parent = Resolve(parentPath);
            var bones = new Transform[bonePaths.Length];
            for (int i = 0; i < bones.Length; i++) bones[i] = Resolve(bonePaths[i]);
            var rootBone = Resolve(rootBonePath);
            var go = new GameObject(name);
            go.SetActive(false);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            renderer.bones = bones;
            renderer.rootBone = rootBone;
            renderer.localBounds = bounds;
            renderer.quality = quality;
            renderer.enabled = enabled;
            renderer.updateWhenOffscreen = updateWhenOffscreen;
            renderer.skinnedMotionVectors = skinnedMotionVectors;
            renderer.shadowCastingMode = shadows;
            renderer.receiveShadows = receiveShadows;
            renderer.lightProbeUsage = lightProbes;
            renderer.reflectionProbeUsage = reflectionProbes;
            renderer.motionVectorGenerationMode = motionVectors;
            renderer.renderingLayerMask = renderingLayerMask;
            renderer.sortingLayerID = sortingLayerID;
            renderer.sortingOrder = sortingOrder;
            if (probeAnchorPath != null) renderer.probeAnchor = Resolve(probeAnchorPath);
            for (int i = 0; i < blendWeights.Length; i++) renderer.SetBlendShapeWeight(i, blendWeights[i]);
            go.SetActive(true);
            return renderer;
        }
    }

    public Part[] parts = Array.Empty<Part>();
}
