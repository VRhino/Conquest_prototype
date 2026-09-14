using System;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

public static class ArchitectureBuildValidation
{
    public static void CompilePlayerScripts()
    {
        var settings = new ScriptCompilationSettings
        {
            group = BuildTargetGroup.Standalone,
            target = BuildTarget.StandaloneWindows64
        };
        var result = PlayerBuildInterface.CompilePlayerScripts(settings, "Temp/ArchitecturePlayerCompilation");
        if (result.assemblies == null || result.assemblies.Count == 0)
            throw new InvalidOperationException("Player script compilation produced no assemblies.");
        Debug.Log("ARCHITECTURE_PLAYER_SCRIPTS_OK: " + result.assemblies.Count + " assemblies");
    }
}
