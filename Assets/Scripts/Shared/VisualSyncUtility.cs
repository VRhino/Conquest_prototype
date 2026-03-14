using ConquestTactics.Visual;
using UnityEngine;

public static class VisualSyncUtility
{
    /// <summary>
    /// Configura AnimatorCullingMode y obtiene o añade EntityVisualSync en el GameObject visual.
    /// </summary>
    public static EntityVisualSync SetupVisualSync(GameObject visualInstance)
    {
        var animator = visualInstance.GetComponent<Animator>();
        if (animator != null)
            animator.cullingMode = AnimatorCullingMode.CullCompletely;

        var syncScript = visualInstance.GetComponent<EntityVisualSync>();
        if (syncScript == null)
            syncScript = visualInstance.AddComponent<EntityVisualSync>();

        return syncScript;
    }
}
