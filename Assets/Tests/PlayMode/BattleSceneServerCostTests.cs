#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Lo que cuesta en CPU una batalla entera de las de hoy (agentes de NavMesh, Animators y golpes por collider) cuando
/// nadie la mira: sin vsync ni tope de fotogramas, cada fotograma dura lo que cuesta simularlo. Es la medida del camino A
/// del servidor de batalla (Docs/Arquitectura/22, §8). Lanzarla con <c>-batchmode -nographics</c>; escribe
/// <c>Logs/red/coste-servidor.json</c>.
/// </summary>
public partial class BattleSceneIntegrationTests
{
    const float SegundosDeCoste = 45f;

    // El nombre empieza como el perfil de batalla completa para que SetUp monte la batalla de 10 contra 10.
    [UnityTest, Explicit("Coste de CPU de una batalla completa sin pantalla (servidor de batalla, camino A).")]
    public IEnumerator FullBattlePreparationRenderedPerformanceProfileSinPantalla()
    {
        int heroesEsperados = FullBattleHeroesPerSide * 2;
        using var heroes = em.CreateEntityQuery(typeof(HeroSpawnComponent), typeof(HeroVisualInstance));
        using var unidades = em.CreateEntityQuery(typeof(UnitFormationStateComponent), typeof(NavAgentComponent));
        yield return Until(() => heroes.CalculateEntityCount() == heroesEsperados
            && unidades.CalculateEntityCount() == expectedFullBattleUnits, ReadyTimeout,
            "No se desplegaron todos los héroes y soldados.");

        int vsyncAntes = QualitySettings.vSyncCount, topeAntes = Application.targetFrameRate;
        bool burstAntes = Unity.Burst.BurstCompiler.Options.EnableBurstCompilation;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        // Como el ejecutable: con Burst (el editor de este proyecto lo tiene apagado), salvo CONQUEST_SIN_BURST=1.
        Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = Environment.GetEnvironmentVariable("CONQUEST_SIN_BURST") != "1";
        try
        {
            var bodyblock = testWorld.GetExistingSystemManaged<UnitBodyblockSystem>();
            yield return Until(() => bodyblock.DebugContactPairs > 0, 120f, "Los bandos no llegaron a chocar.");

            var muestrasPared = new System.Collections.Generic.List<double>();
            var muestrasHilo = new System.Collections.Generic.List<double>();
            using var principal = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "Main Thread", 1);
            // En qué se va el fotograma: los marcadores de Unity que existan con estos nombres.
            string[] marcas = { "PlayerLoop", "SimulationSystemGroup", "Animators.Update", "Director.ProcessFrame", "Physics.Processing",
                "Physics.Simulate", "FixedUpdate.PhysicsFixedUpdate", "NavMeshManager", "AI.NavMeshManager", "Update.ScriptRunBehaviourUpdate",
                "PreLateUpdate.ScriptRunBehaviourLateUpdate", "WaitForTargetFPS" };
            var contadores = marcas.Select(m => Unity.Profiling.ProfilerRecorder.StartNew(new Unity.Profiling.ProfilerCategory("Scripts"), m, 1)).ToArray();
            var sumas = new double[marcas.Length];
            int alEmpezar = unidades.CalculateEntityCount();
            double inicio = Time.realtimeSinceStartupAsDouble, antes = inicio;
            while (antes - inicio < SegundosDeCoste)
            {
                yield return null;
                double ahora = Time.realtimeSinceStartupAsDouble;
                muestrasPared.Add((ahora - antes) * 1000d);
                antes = ahora;
                muestrasHilo.Add(principal.LastValue / 1000000d);
                for (int k = 0; k < contadores.Length; k++)
                    if (contadores[k].Valid) sumas[k] += contadores[k].LastValue / 1000000d;
            }
            string reparto = string.Join(", ", marcas.Select((m, k) => contadores[k].Valid
                ? FormattableString.Invariant($"\"{m}\": {sumas[k] / muestrasPared.Count:F3}") : null).Where(x => x != null));
            foreach (var c in contadores) c.Dispose();
            int alAcabar = unidades.CalculateEntityCount();
            int heridos = 0;
            using (var conVida = em.CreateEntityQuery(typeof(UnitCombatComponent), typeof(HealthComponent)))
            using (var vidas = conVida.ToComponentDataArray<HealthComponent>(Unity.Collections.Allocator.Temp))
                foreach (var vida in vidas)
                    if (vida.currentHealth < vida.maxHealth) heridos++;
            double[] pared = muestrasPared.ToArray(), hilo = muestrasHilo.ToArray();
            double media = pared.Average();

            string json = FormattableString.Invariant($@"{{
  ""heroes"": {heroesEsperados}, ""soldadosDesplegados"": {expectedFullBattleUnits},
  ""soldadosAlEmpezarLaMedida"": {alEmpezar}, ""soldadosAlAcabar"": {alAcabar}, ""soldadosHeridosAlAcabar"": {heridos},
  ""graficos"": ""{SystemInfo.graphicsDeviceType}"", ""burst"": {Unity.Burst.BurstCompiler.Options.EnableBurstCompilation.ToString().ToLowerInvariant()},
  ""segundos"": {SegundosDeCoste}, ""fotogramas"": {pared.Length}, ""mediaFotogramaMs"": {media},
  ""fotogramaMs"": {ProfilePercentiles(pared)}, ""hiloPrincipalMs"": {ProfilePercentiles(hilo)},
  ""repartoMediaMs"": {{ {reparto} }},
  ""nota"": ""Editor en modo batch: un ejecutable no lleva las comprobaciones del editor y cuesta menos.""
}}");
            System.IO.Directory.CreateDirectory("Logs/red");
            System.IO.File.WriteAllText("Logs/red/coste-servidor.json", json);
            TestContext.WriteLine(json);
            Assert.That(alAcabar < expectedFullBattleUnits || heridos > 0, Is.True, "Ni muertos ni heridos: sin pantalla no hubo combate de verdad.");
        }
        finally
        {
            QualitySettings.vSyncCount = vsyncAntes;
            Application.targetFrameRate = topeAntes;
            Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = burstAntes;
        }
    }
}
#endif
