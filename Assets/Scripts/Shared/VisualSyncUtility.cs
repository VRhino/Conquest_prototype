using ConquestTactics.Visual;
using UnityEngine;

public static class VisualSyncUtility
{
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
