#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using ConquestTactics.Net;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// La batalla de verdad (sistemas de Game.ECS, agentes, Animators y golpes por collider) corriendo en el mundo servidor
/// de Netcode, sin jugador local: paso 2b-2 del camino A (Docs/Arquitectura/22, §8). Lanzarla con
/// <c>-batchmode -nographics</c>; escribe <c>Logs/red/batalla-real-servidor.json</c>.
/// </summary>
public class BatallaRealServidorTests
{
    const int HeroesPorBando = 10;
    const float Segundos = 45f;

    static IEnumerator Hasta(Func<bool> listo, float tope, string fallo)
    {
        float fin = Time.realtimeSinceStartup + tope;
        while (!listo())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(fin), fallo);
            yield return null;
        }
    }

    [UnityTest, Explicit("Batalla completa en el mundo servidor de Netcode (servidor de batalla, camino A).")]
    public IEnumerator LaBatallaDeVerdadCorreEnElMundoServidor()
    {
        var mundoPrevio = World.DefaultGameObjectInjectionWorld;
        if (mundoPrevio != null && mundoPrevio.IsCreated)
            ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(mundoPrevio);
        World.DefaultGameObjectInjectionWorld = null;
        bool fondoAntes = Application.runInBackground;
        bool burstAntes = Unity.Burst.BurstCompiler.Options.EnableBurstCompilation;
        Application.runInBackground = true;
        Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = true;
        PlayerSessionService.Clear();
        OpcionesRed.ModoPedido = OpcionesRed.Modo.Servidor;
        OpcionesRed.BatallaReal = HeroesPorBando;
        World mundo = null;
        try
        {
            mundo = DefaultWorldInitialization.Initialize("Batalla real");
            Assert.That(mundo.Flags & WorldFlags.GameServer, Is.EqualTo(WorldFlags.GameServer), "El mundo de la batalla no es el servidor de Netcode.");
            var em = mundo.EntityManager;
            using var heroes = em.CreateEntityQuery(typeof(HeroSpawnComponent), typeof(HeroVisualInstance));
            using var unidades = em.CreateEntityQuery(typeof(UnitFormationStateComponent), typeof(NavAgentComponent));
            yield return Hasta(() => BatallaRealSystem.SoldadosEsperados > 0
                && heroes.CalculateEntityCount() == HeroesPorBando * 2
                && unidades.CalculateEntityCount() == BatallaRealSystem.SoldadosEsperados, 120f,
                "No se desplegaron todos los héroes y soldados en el mundo servidor.");
            using var locales = em.CreateEntityQuery(typeof(IsLocalPlayer));
            Assert.That(locales.CalculateEntityCount(), Is.Zero, "En el servidor no hay héroe local.");

            var bodyblock = mundo.GetExistingSystemManaged<UnitBodyblockSystem>();
            yield return Hasta(() => bodyblock.DebugContactPairs > 0, 240f, "Los bandos no llegaron a chocar.");

            string[] marcas = { "Animators.Update", "FixedUpdate.PhysicsFixedUpdate", "NavMeshManager", "Update.ScriptRunBehaviourUpdate" };
            var contadores = marcas.Select(m => Unity.Profiling.ProfilerRecorder.StartNew(new Unity.Profiling.ProfilerCategory("Scripts"), m, 1)).ToArray();
            var sumas = new double[marcas.Length];
            int alEmpezar = unidades.CalculateEntityCount(), fotogramas = 0;
            int ticksAntes = FinTickServidorSystem.TicksTotales;
            double msAntes = FinTickServidorSystem.MsTotales;
            double inicio = Time.realtimeSinceStartupAsDouble, ahora = inicio;
            while (ahora - inicio < Segundos)
            {
                yield return null;
                ahora = Time.realtimeSinceStartupAsDouble;
                fotogramas++;
                for (int k = 0; k < contadores.Length; k++)
                    if (contadores[k].Valid) sumas[k] += contadores[k].LastValue / 1000000d;
            }
            string reparto = string.Join(", ", marcas.Select((m, k) => contadores[k].Valid
                ? FormattableString.Invariant($"\"{m}\": {sumas[k] / fotogramas:F3}") : null).Where(x => x != null));
            foreach (var c in contadores) c.Dispose();
            int ticks = FinTickServidorSystem.TicksTotales - ticksAntes;
            double tickMs = (FinTickServidorSystem.MsTotales - msAntes) / Math.Max(1, ticks);
            int alAcabar = unidades.CalculateEntityCount();
            int heridos = 0;
            using (var conVida = em.CreateEntityQuery(typeof(UnitCombatComponent), typeof(HealthComponent)))
            using (var vidas = conVida.ToComponentDataArray<HealthComponent>(Unity.Collections.Allocator.Temp))
                foreach (var vida in vidas)
                    if (vida.currentHealth < vida.maxHealth) heridos++;

            string json = FormattableString.Invariant($@"{{
  ""mundo"": ""{mundo.Name}"", ""heroes"": {HeroesPorBando * 2}, ""soldadosDesplegados"": {BatallaRealSystem.SoldadosEsperados},
  ""soldadosAlEmpezarLaMedida"": {alEmpezar}, ""soldadosAlAcabar"": {alAcabar}, ""soldadosHeridosAlAcabar"": {heridos},
  ""vivosA"": {BatallaRealSystem.VivosA}, ""vivosB"": {BatallaRealSystem.VivosB},
  ""segundos"": {ahora - inicio:F1}, ""fotogramas"": {fotogramas}, ""fotogramasPorSegundo"": {fotogramas / (ahora - inicio):F1},
  ""ticks"": {ticks}, ""ticksPorSegundo"": {ticks / (ahora - inicio):F1}, ""tickMedioMs"": {tickMs:F3},
  ""repartoMediaMsPorFotograma"": {{ {reparto} }},
  ""nota"": ""Editor en modo batch a 60 fotogramas por segundo; tickMedioMs es el SimulationSystemGroup del servidor entero.""
}}");
            System.IO.Directory.CreateDirectory("Logs/red");
            System.IO.File.WriteAllText("Logs/red/batalla-real-servidor.json", json);
            TestContext.WriteLine(json);
            Assert.That(alAcabar < BatallaRealSystem.SoldadosEsperados || heridos > 0, Is.True,
                "Ni muertos ni heridos: en el mundo servidor no hubo combate de verdad.");
        }
        finally
        {
            OpcionesRed.ModoPedido = OpcionesRed.Modo.Ninguno;
            OpcionesRed.BatallaReal = 0;
            BattleBootstrapRequests.NoLocalHero = false;
            Application.runInBackground = fondoAntes;
            Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = burstAntes;
            if (mundo != null && mundo.IsCreated) mundo.Dispose();
            World.DefaultGameObjectInjectionWorld = mundoPrevio;
            if (mundoPrevio != null && mundoPrevio.IsCreated)
                ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(mundoPrevio);
        }
    }
}
#endif
