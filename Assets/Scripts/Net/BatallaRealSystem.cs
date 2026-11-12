using System;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace ConquestTactics.Net
{
    /// <summary>
    /// La batalla de verdad de Conquest en el mundo servidor (camino A, Docs/Arquitectura/22 §8): con
    /// <c>-batallaReal N</c> monta una batalla de N héroes por bando, todos llevados por la IA, y carga la escena de
    /// batalla. Los sistemas de Game.ECS y su capa de GameObjects (agentes, Animators, colliders de las armas) corren
    /// aquí, sin jugador local. Cada 10 s dice cómo va.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class BatallaRealSystem : SystemBase
    {
        /// <summary>ponytail: todos con la misma tropa; el ticket del motor dirá la de cada escuadra cuando se una con BatallaDeTicket.</summary>
        public const string Tropa = "sqd01";

        public static int SoldadosEsperados { get; private set; }
        public static int VivosA { get; private set; }
        public static int VivosB { get; private set; }

        bool _cargada;
        double _ultimoAviso;

        protected override void OnCreate() => Enabled = OpcionesRed.BatallaReal > 0;

        protected override void OnUpdate()
        {
            if (!_cargada)
            {
                _cargada = true;
                // Con ticket (de un fichero o del lanzador) la batalla la monta BatallaDeTicketSystem cuando empieza.
                if (string.IsNullOrEmpty(OpcionesRed.Ticket) && string.IsNullOrEmpty(OpcionesRed.Lanzador))
                    Cargar(OpcionesRed.BatallaReal);
                return;
            }

            int a = 0, b = 0;
            foreach (var bando in SystemAPI.Query<RefRO<TeamComponent>>().WithAll<UnitCombatComponent>())
            {
                if (bando.ValueRO.value == Team.TeamA) a++;
                else b++;
            }
            VivosA = a;
            VivosB = b;

            double ahora = SystemAPI.Time.ElapsedTime;
            if (ahora - _ultimoAviso < 10d)
                return;
            _ultimoAviso = ahora;
            // Lo que hace la IA de los héroes y lo cerca que están los bandos: si no chocan, aquí se ve por qué.
            var acciones = new int[8];
            float cerca = float.MaxValue;
            var heroes = SystemAPI.QueryBuilder().WithAll<HeroAIDecision, TeamComponent, Unity.Transforms.LocalTransform>().Build();
            using var bandos = heroes.ToComponentDataArray<TeamComponent>(Unity.Collections.Allocator.Temp);
            using var poses = heroes.ToComponentDataArray<Unity.Transforms.LocalTransform>(Unity.Collections.Allocator.Temp);
            using var decisiones = heroes.ToComponentDataArray<HeroAIDecision>(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < bandos.Length; i++)
            {
                acciones[Math.Min(7, (int)decisiones[i].action)]++;
                for (int j = i + 1; j < bandos.Length; j++)
                    if (bandos[i].value != bandos[j].value)
                        cerca = Math.Min(cerca, Unity.Mathematics.math.distance(poses[i].Position, poses[j].Position));
            }
            string estado = SystemAPI.TryGetSingleton<MatchStateComponent>(out var partida) ? partida.currentState.ToString() : "sin partida";
            Debug.Log(FormattableString.Invariant(
                $"[BatallaReal] t={ahora:F0} s, {estado}, soldados vivos A {a} B {b} de {SoldadosEsperados}, héroes {bandos.Length} por acción [{string.Join(",", acciones)}], bandos a {cerca:F0} m"));
        }

        /// <summary>La batalla de un ticket del motor, con sus héroes y sus escuadras (<see cref="BatallaRealDeTicket"/>).</summary>
        public static void CargarDeTicket(BatallaDeTicket batalla)
        {
            var datos = BatallaRealDeTicket.Montar(batalla, out int soldados);
            SoldadosEsperados = soldados;
            ReservaDeProyectiles.Asegurar();
            BattleBootstrapRequests.NoLocalHero = true;
            BattleTransitionData.Instance.SetBattleData(datos);
            SceneTransitionService.LoadScene(SceneTransitionService.SceneNames.Battle);
            Debug.Log($"[BatallaReal] Batalla {batalla.Ticket.BattleId} del ticket: {datos.attackers.Count} héroes atacantes, " +
                      $"{datos.defenders.Count} defensores, {soldados} soldados, {batalla.DuracionSegundos} s");
        }

        static void Cargar(int heroesPorBando)
        {
            // Con -tropas a,b,c las escuadras van por turno (el primer héroe de cada bando con la primera, y así).
            var tropas = (string.IsNullOrEmpty(OpcionesRed.Tropas) ? Tropa : OpcionesRed.Tropas).Split(',')
                .Select(id => SquadDataService.GetSquadById(id.Trim())).Where(t => t != null).ToArray();
            if (tropas.Length == 0)
                tropas = new[] { SquadDataService.GetSquadById(Tropa) };
            var batalla = BattleDebugCreator.CreateTestBattle(heroesPorBando, heroesPorBando);
            SoldadosEsperados = 0;
            foreach (var bando in new[] { batalla.attackers, batalla.defenders })
                for (int i = 0; i < bando.Count; i++)
                {
                    var heroe = bando[i];
                    var tropa = tropas[i % tropas.Length];
                    heroe.heroId = Guid.NewGuid().ToString();
                    var escuadra = SquadDataService.CreateSquadInstance(tropa.id);
                    escuadra.unitsInSquad = tropa.unitCount;
                    heroe.squadInstances.Clear();
                    heroe.squadInstances.Add(escuadra);
                    SoldadosEsperados += tropa.unitCount;
                }
            ReservaDeProyectiles.Asegurar();
            BattleBootstrapRequests.NoLocalHero = true;
            // Con -duracion la batalla dura eso en vez de lo que diga el mapa (para ver el final sin esperar 15 minutos).
            if (OpcionesRed.Duracion > 0f)
                batalla.BattleTimer = (int)OpcionesRed.Duracion;
            BattleTransitionData.Instance.SetBattleData(batalla);
            SceneTransitionService.LoadScene(SceneTransitionService.SceneNames.Battle);
            Debug.Log($"[BatallaReal] Batalla de {heroesPorBando} héroes por bando ({SoldadosEsperados} soldados) en el mundo servidor");
        }
    }
}
