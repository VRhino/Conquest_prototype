using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Conquest.WorldMap
{
    // Opt-in diagnostic; absent during normal gameplay.
    public sealed class WorldMapFpsProbe : MonoBehaviour
    {
        const double WarmupSeconds = 5, SampleSeconds = 15;
        readonly string[] views = { "Anatolia", "Bosques y valles", "Escarpes", "Mundo completo" };
        readonly List<double> samples = new List<double>();
        readonly List<string> rows = new List<string>();
        readonly List<string> raw = new List<string> { "view,frame_ms" };
        WorldMapPreviewController preview;
        double began;
        int view, oldVsync, oldTarget;
        bool oldBackground, initialized, restored;
        string outputFolder;
        int renderedFrames, lastRenderedFrame = -1;
        bool sampling, valid = true;
        static string N(double n) => n.ToString("F2", CultureInfo.InvariantCulture);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (!Environment.GetCommandLineArgs().Contains("--worldmap-fps-benchmark")) return;
            new GameObject("WorldMap FPS probe").AddComponent<WorldMapFpsProbe>();
        }

        void Start()
        {
            preview = UnityEngine.Object.FindAnyObjectByType<WorldMapPreviewController>();
            if (!preview) { Destroy(gameObject); return; }
            outputFolder = Path.Combine(Application.persistentDataPath, "WorldMapFps");
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args,"--worldmap-fps-output");
            if (i >= 0 && i + 1 < args.Length) outputFolder = Path.GetFullPath(args[i + 1]);
            Directory.CreateDirectory(outputFolder);
            oldVsync = QualitySettings.vSyncCount; oldTarget = Application.targetFrameRate;
            oldBackground = Application.runInBackground; initialized = true;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Application.runInBackground = true;
            RenderPipelineManager.endCameraRendering += AfterCamera;
            BeginView();
        }
        void BeginView()
        {
            samples.Clear();
            sampling = false; renderedFrames = 0; lastRenderedFrame = -1;
            if (view == 0) preview.FocusArea(new Vector2(.475f,.21f),1300,-20,37);
            if (view == 1) preview.FocusArea(new Vector2(.21f,.245f),1200,-50,35);
            if (view == 2) preview.FocusArea(new Vector2(.69f,.94f),1000,-20,32);
            if (view == 3) preview.FitWorld();
            began = Time.realtimeSinceStartupAsDouble;
        }
        void LateUpdate()
        {
            if (!initialized) return;
            double elapsed = Time.realtimeSinceStartupAsDouble - began;
            if (elapsed < WarmupSeconds) return;
            if (!sampling) { sampling = true; renderedFrames = 0; }
            double ms = Time.unscaledDeltaTime * 1000.0;
            if (ms <= 0) return;
            samples.Add(ms); raw.Add(views[view] + "," + N(ms));
            if (elapsed < WarmupSeconds + SampleSeconds) return;
            var sorted = samples.OrderBy(x => x).ToArray();
            int tail = Math.Max(1,(int)Math.Ceiling(sorted.Length*.01));
            double low = 1000 / sorted.Skip(sorted.Length-tail).Average();
            double p95 = sorted[Math.Min(sorted.Length-1,(int)Math.Ceiling(sorted.Length*.95)-1)];
            bool rendered = renderedFrames >= Math.Max(1,samples.Count-2);
            valid &= rendered;
            rows.Add("| " + views[view] + " | " + (rendered ? N(1000/samples.Average()) : "INVALID: mapa sin renderizar") + " | " + (rendered ? N(low) : "—") + " | " + (rendered ? N(p95) : "—") + " | " + samples.Count + " | " + renderedFrames + " | " + Screen.width + " × " + Screen.height + " |");
            WriteReport(); view++;
            if (view < views.Length) { BeginView(); return; }
            Restore(); preview.FocusCampaign();
            File.WriteAllText(Path.Combine(outputFolder,"Complete.txt"),valid ? "Benchmark completed: rendered frames verified" : "INVALID: no rendering, discard timings");
            if (!Application.isEditor) Application.Quit();
            Destroy(gameObject);
        }
        void WriteReport()
        {
            string text = "# " + preview.gameObject.scene.name + " FPS — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n\n" +
                "Unity " + Application.unityVersion + "; " + (Application.isEditor ? "Editor Play Mode" : "Standalone Player") + ".\n" +
                "GPU: " + SystemInfo.graphicsDeviceName + "; CPU: " + SystemInfo.processorType + "; RAM: " + SystemInfo.systemMemorySize + " MB.\n" +
                "Preview completa (48 terrenos). Por vista: 5 s de calentamiento + 15 s medidos en LateUpdate. VSync=0, targetFrameRate=-1, runInBackground=true solo durante la prueba.\n" +
                "FPS medio = 1000 / duración media en ms. 1% bajo = inverso de la media del 1% de frames más lentos; P95 = percentil 95 del tiempo de frame. Datos CPU/GPU por separado no medidos.\n" +
                "No incluye carga inicial, streaming, red ni niebla de guerra. Se verifica endCameraRendering de la cámara del mapa para rechazar ejecuciones sin renderizar.\n\n" +
                "| Vista | FPS medio | 1% bajo | P95 ms | Frames | Frames renderizados | Resolución |\n|---|---:|---:|---:|---:|---:|---|\n" + string.Join("\n",rows) + "\n";
            File.WriteAllText(Path.Combine(outputFolder,"FpsBenchmark.md"),text);
            File.WriteAllLines(Path.Combine(outputFolder,"FpsBenchmark.csv"),raw);
        }
        void Restore()
        {
            if (!initialized || restored) return;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            restored = true; QualitySettings.vSyncCount = oldVsync;
            Application.targetFrameRate = oldTarget; Application.runInBackground = oldBackground;
        }
        void OnDestroy() => Restore();
        void AfterCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!sampling || !preview || camera != preview.mapCamera || lastRenderedFrame == Time.frameCount) return;
            lastRenderedFrame = Time.frameCount; renderedFrames++;
        }
    }
}

