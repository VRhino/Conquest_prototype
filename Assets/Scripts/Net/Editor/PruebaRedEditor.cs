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
        public const string RutaConSimulador = "Builds/PruebaRedSimulador/PruebaRed.exe";

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
        public static void Construir() => Construir(RutaEjecutable, null);

        /// <summary>
        /// El mismo ejecutable, en otra carpeta, con el simulador de red de Netcode (latencia, pérdida y cortes): solo
        /// existe con NETCODE_DEBUG. En el cliente: <c>--loadNetworkSimulatorJsonFile perfil.json</c> y <c>-apagon</c>.
        /// Las medidas de coste se hacen con el otro.
        /// </summary>
        [MenuItem("Conquest/Red/Construir ejecutable con simulador de red")]
        public static void ConstruirConSimulador() => Construir(RutaConSimulador, new[] { "NETCODE_DEBUG" });

        static void Construir(string ruta, string[] simbolos)
        {
            if (!File.Exists(RutaEscena))
                CrearEscena();
            var informe = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                // La de batalla va detrás: el servidor de la batalla de verdad (-batallaReal) la carga por su nombre.
                scenes = new[] { RutaEscena, "Assets/Scenes/GameScenes/BattleScene.unity" },
                locationPathName = ruta,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
                extraScriptingDefines = simbolos,
            });
            Debug.Log($"[Red] Construcción: {informe.summary.result}, {informe.summary.totalErrors} errores, " +
                      $"{informe.summary.totalTime.TotalSeconds:F0} s");
            if (Application.isBatchMode && informe.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
