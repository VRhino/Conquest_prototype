using ConquestTactics.Visual;
using UnityEngine;
using Unity.Entities;
using System.Collections.Generic;

public static class VisualSyncUtility
{
    private static readonly Dictionary<(World world, Entity entity), EntityVisualSync> Instances = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstances() => Instances.Clear();

    public static void Register(World world, Entity entity, EntityVisualSync visual)
    {
        if (world != null && world.IsCreated && entity != Entity.Null)
            Instances[(world, entity)] = visual;
    }

    public static void Unregister(World world, Entity entity, EntityVisualSync visual)
    {
        if (world != null && Instances.TryGetValue((world, entity), out var current) && current == visual)
            Instances.Remove((world, entity));
    }

    public static bool TryGetVisual(World world, Entity entity, out EntityVisualSync visual)
    {
        visual = null;
        return world != null && world.IsCreated && Instances.TryGetValue((world, entity), out visual)
            && visual != null;
    }

    /// <summary>
    /// Configura AnimatorCullingMode y obtiene o añade EntityVisualSync en el GameObject visual.
    /// </summary>
    public static EntityVisualSync SetupVisualSync(GameObject visualInstance)
    {
        foreach (var animator in visualInstance.GetComponentsInChildren<Animator>(true))
        {
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            // CharacterController/NavMesh owns the root. Animation only poses the skeleton.
            animator.applyRootMotion = false;
        }

        var syncScript = visualInstance.GetComponent<EntityVisualSync>();
        if (syncScript == null)
            syncScript = visualInstance.AddComponent<EntityVisualSync>();

        return syncScript;
    }
}
