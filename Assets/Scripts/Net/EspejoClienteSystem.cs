using ConquestTactics.Animation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ConquestTactics.Net
{
    /// <summary>Cliente: este espejo ya tiene su modelo en la escena.</summary>
    public struct ConVisual : IComponentData { }

    /// <summary>
    /// Cliente de la batalla de verdad (<c>-batallaReal</c> también en el cliente): carga la escena de batalla y pinta cada
    /// soldado espejo con el mismo modelo y los mismos Animators que el juego. Para eso pone en el fantasma los
    /// componentes que leen <c>EntityVisualSync</c> y <c>UnitAnimationAdapter</c> y los rellena con lo que llega por la
    /// red; el modelo no lleva agente de navegación ni colliders de golpe: aquí no se decide nada.
    /// ponytail: los héroes siguen siendo cápsulas (su modelo lee otros componentes y la apariencia de cada uno).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(DibujoRedSystem))]
    public partial class EspejoClienteSystem : SystemBase
    {
        bool _escenaPedida;
        VisualPrefabRegistry _registro;

        protected override void OnCreate() => Enabled = OpcionesRed.BatallaReal > 0 && !Application.isBatchMode;

        protected override void OnUpdate()
        {
            if (!_escenaPedida)
            {
                _escenaPedida = true;
                BattleBootstrapRequests.NoLocalHero = true;
                SceneManager.LoadScene(SceneTransitionService.SceneNames.Battle);
                return;
            }
            if (_registro == null)
                _registro = Object.FindFirstObjectByType<VisualPrefabRegistry>();
            if (_registro == null)
                return;

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            foreach (var (soldado, modelo, animacion, pose, entidad) in SystemAPI
                         .Query<RefRO<SoldadoRed>, RefRO<VisualRed>, RefRO<AnimacionRed>, RefRO<LocalTransform>>()
                         .WithNone<ConVisual>().WithEntityAccess())
            {
                // El modelo llega con el primer snapshot completo; hasta entonces el nombre está vacío.
                if (soldado.ValueRO.Tipo != SoldadoRed.Soldado || modelo.ValueRO.Prefab.IsEmpty)
                    continue;
                var prefab = _registro.GetPrefab(modelo.ValueRO.Prefab.ToString(), true)
                             ?? _registro.GetDefaultUnitPrefab((SquadType)modelo.ValueRO.TipoDeEscuadra, true);
                if (prefab == null)
                    continue;
                var visual = Object.Instantiate(prefab, pose.ValueRO.Position, pose.ValueRO.Rotation);
                visual.name = $"Espejo_{entidad.Index}";
                int capa = LayerMask.NameToLayer(soldado.ValueRO.Bando == 1 ? "Units_B" : "Units_A");
                foreach (var hijo in visual.GetComponentsInChildren<Transform>(true))
                    hijo.gameObject.layer = math.max(0, capa);
                // Sin autoridad: ni navega ni golpea. Los golpes los decide el servidor.
                foreach (var golpe in visual.GetComponentsInChildren<WeaponHitboxBehaviour>(true)) Object.Destroy(golpe);
                foreach (var escudo in visual.GetComponentsInChildren<ShieldHitboxBehaviour>(true)) Object.Destroy(escudo);
                var agente = visual.GetComponent<NavMeshAgent>();
                if (agente != null) agente.enabled = false;

                ecb.AddComponent<ConVisual>(entidad);
                ecb.AddComponent(entidad, new UnitAnimationMovementComponent { PreviousPosition = pose.ValueRO.Position });
                ecb.AddComponent<UnitCombatComponent>(entidad);
                ecb.AddComponent<RangedAttackStateComponent>(entidad);
                ecb.AddComponent<ResilienceComponent>(entidad);
                if ((animacion.ValueRO.Banderas & AnimacionRed.ConMarcha) != 0)
                    ecb.AddComponent<UnitMovementGearComponent>(entidad);
                VisualSyncUtility.SetupVisualSync(visual).SetHeroEntity(entidad);
            }
            ecb.Playback(EntityManager);

            foreach (var (animacion, pose, movimiento, combate, distancia, aguante, entidad) in SystemAPI
                         .Query<RefRO<AnimacionRed>, RefRO<LocalTransform>, RefRW<UnitAnimationMovementComponent>, RefRW<UnitCombatComponent>,
                             RefRW<RangedAttackStateComponent>, RefRW<ResilienceComponent>>()
                         .WithAll<ConVisual>().WithEntityAccess())
            {
                var a = animacion.ValueRO;
                bool seMueve = (a.Banderas & AnimacionRed.Moviendose) != 0;
                // La dirección no viaja: sale de cómo se mueve el fantasma ya interpolado.
                float3 paso = pose.ValueRO.Position - movimiento.ValueRO.PreviousPosition;
                paso.y = 0f;
                movimiento.ValueRW.PreviousPosition = pose.ValueRO.Position;
                if (math.lengthsq(paso) > 1e-8f)
                    movimiento.ValueRW.MovementDirection = math.normalize(paso);
                movimiento.ValueRW.CurrentSpeed = a.Velocidad;
                movimiento.ValueRW.MaxSpeed = a.VelocidadBase;
                movimiento.ValueRW.RequestedSpeed = a.VelocidadPedida;
                movimiento.ValueRW.IsMoving = seMueve;
                movimiento.ValueRW.CurrentStance = (UnitStance)a.Postura;
                movimiento.ValueRW.SlotRow = a.Fila;
                movimiento.ValueRW.SlotRole = a.Papel;
                combate.ValueRW.isAttacking = (a.Banderas & AnimacionRed.Atacando) != 0;
                distancia.ValueRW.isFiring = (a.Banderas & AnimacionRed.Disparando) != 0;
                distancia.ValueRW.isReloading = (a.Banderas & AnimacionRed.Recargando) != 0;
                aguante.ValueRW.reaction = (HitReaction)a.Reaccion;
                aguante.ValueRW.reactionRevision = a.RevisionDeReaccion;
                if (SystemAPI.HasComponent<UnitMovementGearComponent>(entidad))
                    SystemAPI.SetComponent(entidad, new UnitMovementGearComponent
                        { gear = (a.Banderas & AnimacionRed.Corre) != 0 ? MovementGear.Run : MovementGear.Walk });
            }
        }
    }
}
