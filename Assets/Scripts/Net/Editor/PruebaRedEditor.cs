using System.IO;
using Unity.NetCode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConquestTactics.Net.Editor
{
    /// <summary>
    /// Escena y ejecutable de la prueba de red. En modo batch:
    /// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod ConquestTactics.Net.Editor.PruebaRedEditor.Construir</c>.
    /// </summary>
    public static class PruebaRedEditor
    {
        public const string RutaEscena = "Assets/Scenes/NetTest/PruebaRed.unity";
        public const string RutaEjecutable = "Builds/PruebaRed/PruebaRed.exe";

        [MenuItem("Conquest/Red/Crear escena de prueba")]
        public static void CrearEscena()
        {
            var escena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            suelo.name = "Suelo";
            suelo.transform.localScale = new Vector3(40f, 1f, 40f);
            var red = new GameObject("Red");
            red.AddComponent<OverrideAutomaticNetcodeBootstrap>().ForceAutomaticBootstrapInScene =
                NetCodeConfig.AutomaticBootstrapSetting.EnableAutomaticBootstrap;
            Directory.CreateDirectory(Path.GetDirectoryName(RutaEscena));
            EditorSceneManager.SaveScene(escena, RutaEscena);
            Debug.Log($"[Red] Escena guardada en {RutaEscena}");
        }

        [MenuItem("Conquest/Red/Construir ejecutable de prueba")]
        public static void Construir()
        {
            if (!File.Exists(RutaEscena))
                CrearEscena();
            var informe = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { RutaEscena },
                locationPathName = RutaEjecutable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[Red] Construcción: {informe.summary.result}, {informe.summary.totalErrors} errores, " +
                      $"{informe.summary.totalTime.TotalSeconds:F0} s");
            if (Application.isBatchMode && informe.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
