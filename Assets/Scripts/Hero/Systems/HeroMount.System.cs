using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// The hero's mount (M19); the only writer of <see cref="HeroMountComponent"/>, and owner of the horse's entity.
/// - G calls the horse beside the hero, on its right, and the hero gets on (Hero_Mount). Losing health or any reaction
///   while getting on interrupts it.
/// - While the hero rides, its body is the horse's: the horse's entity (its own health, team and stamina) stands where
///   the hero is. Hits below the saddle land on it (<see cref="HeroMountRules.HitsHorse"/>).
/// - G again: the hero gets off (Hero_Dismount) on the horse's left, and the horse goes.
/// - The horse dying, or a knock-down or a launch, throws the hero to the ground beside it; it goes on on foot.
/// - A dead hero is on foot (so it respawns on foot).
/// Getting on and off moves the hero's body between the ground beside the horse and the horse's centre: the motor
/// applies <see cref="HeroMountComponent.poseJumpPosition"/>, and the clips play the move in between.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroInputSystem))]
[UpdateBefore(typeof(HeroMovementSystem))]
public partial class HeroMountSystem : SystemBase
{
    EntityQuery _heroes;
    EntityQuery _withoutMount;
    double _now;
    MountTuning _tuning;

    protected override void OnCreate()
    {
        _heroes = GetEntityQuery(ComponentType.ReadWrite<HeroMountComponent>(), ComponentType.ReadOnly<HeroLifeComponent>(),
            ComponentType.ReadOnly<LocalTransform>());
        _withoutMount = GetEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<HeroLifeComponent>(), ComponentType.ReadOnly<LocalTransform>() },
            None = new[] { ComponentType.ReadOnly<HeroMountComponent>() },
        });
    }

    protected override void OnUpdate()
    {
        if (!_withoutMount.IsEmptyIgnoreFilter) EntityManager.AddComponent<HeroMountComponent>(_withoutMount);
        var tuning = SystemAPI.TryGetSingleton<HeroGameplayConfigComponent>(out var config) ? config.mount.OrDefault() : MountTuning.Default;
        // In battle, as the movement reads it (there may be two match states: subscene and system), and never in the
        // fief: its walking hero has no horse.
        bool inBattle = (!SystemAPI.TryGetSingleton<MatchStateComponent>(out var match) || match.currentState == MatchState.InBattle)
            && !(SystemAPI.TryGetSingleton<GameStateComponent>(out var game) && game.currentPhase == GamePhase.Feudo);
        float dt = SystemAPI.Time.DeltaTime;
        _now = SystemAPI.Time.ElapsedTime;
        _tuning = tuning;

        using var heroes = _heroes.ToEntityArray(Allocator.Temp);
        foreach (var hero in heroes)
        {
            var m = EntityManager.GetComponentData<HeroMountComponent>(hero);
            var before = m;
            bool alive = EntityManager.GetComponentData<HeroLifeComponent>(hero).isAlive;
            var pose = EntityManager.GetComponentData<LocalTransform>(hero);
            quaternion facing = Facing(pose.Rotation);
            float health = EntityManager.HasComponent<HeroHealthComponent>(hero) ? EntityManager.GetComponentData<HeroHealthComponent>(hero).currentHealth : 0f;
            var reaction = HitReaction.None;
            bool reacted = false;
            if (EntityManager.HasComponent<ResilienceComponent>(hero))
            {
                var resilience = EntityManager.GetComponentData<ResilienceComponent>(hero);
                reacted = resilience.reactionRevision != m.reactionRevisionSeen;
                reaction = resilience.reaction;
                m.reactionRevisionSeen = resilience.reactionRevision;
            }
            bool pressed = EntityManager.HasComponent<HeroInputComponent>(hero) && EntityManager.GetComponentData<HeroInputComponent>(hero).MountPressed;
            // An AI hero asks to be on horseback or on foot (HeroAIMountSystem).
            if (EntityManager.HasComponent<HeroMountRequestComponent>(hero))
                pressed |= EntityManager.GetComponentData<HeroMountRequestComponent>(hero).ride
                    ? m.state == HeroMountState.OnFoot : m.state == HeroMountState.Mounted;

            if (m.Riding)
            {
                if (!alive)
                    End(ref m, HeroMountEnd.HeroDied, false, pose.Position, facing);
                else if (!HorseAlive(m.horse))
                    End(ref m, HeroMountEnd.HorseKilled, true, pose.Position, facing);
                else if (reacted && HeroMountRules.ThrowsOff(reaction))
                    End(ref m, HeroMountEnd.ThrownOff, true, pose.Position, facing);
                else if (m.state == HeroMountState.Mounting && (reacted || health < m.healthAtStart - 0.01f))
                    End(ref m, HeroMountEnd.Interrupted, true, pose.Position, facing);
            }

            switch (m.state)
            {
                case HeroMountState.OnFoot:
                    if (pressed && _now >= m.nextMountTime && HeroMountRules.CanMount(alive, inBattle, Immobilised(hero), Busy(hero)))
                        Begin(hero, ref m, pose.Position, facing, health, tuning);
                    break;
                case HeroMountState.Mounting:
                    m.elapsed += dt;
                    if (m.elapsed >= tuning.mountSeconds) Enter(ref m, HeroMountState.Mounted);
                    break;
                case HeroMountState.Mounted:
                    if (pressed && inBattle) Enter(ref m, HeroMountState.Dismounting);
                    break;
                case HeroMountState.Dismounting:
                    m.elapsed += dt;
                    if (m.elapsed >= tuning.dismountSeconds) End(ref m, HeroMountEnd.Dismounted, true, pose.Position, facing);
                    break;
            }

            // The horse carries its rider. Getting on and off, neither moves (the body jumps to the horse's centre on
            // the motor's next step).
            if (m.state == HeroMountState.Mounted && EntityManager.Exists(m.horse))
                EntityManager.SetComponentData(m.horse, LocalTransform.FromPositionRotation(pose.Position, facing));

            if (!Same(before, m)) EntityManager.SetComponentData(hero, m);
        }
    }

    void Begin(Entity hero, ref HeroMountComponent m, float3 feet, quaternion facing, float health, in MountTuning tuning)
    {
        float3 at = HeroMountRules.HorseFor(feet, facing);
        var horse = EntityManager.CreateEntity(typeof(LocalTransform), typeof(HealthComponent), typeof(TeamComponent),
            typeof(StaminaComponent), typeof(HeroHorseComponent));
        EntityManager.SetComponentData(horse, LocalTransform.FromPositionRotation(at, facing));
        EntityManager.SetComponentData(horse, new HealthComponent { maxHealth = tuning.horseHealth, currentHealth = tuning.horseHealth });
        EntityManager.SetComponentData(horse, new TeamComponent
        {
            value = EntityManager.HasComponent<TeamComponent>(hero) ? EntityManager.GetComponentData<TeamComponent>(hero).value : Team.None,
        });
        EntityManager.SetComponentData(horse, new StaminaComponent
        {
            maxStamina = tuning.stamina, currentStamina = tuning.stamina, regenRate = tuning.staminaRegenPerSecond,
        });
        EntityManager.SetComponentData(horse, new HeroHorseComponent { rider = hero });

        m.horse = horse;
        m.healthAtStart = health;
        m.lastEnd = HeroMountEnd.None;
        Enter(ref m, HeroMountState.Mounting);
        Jump(ref m, at, facing);
    }

    void End(ref HeroMountComponent m, HeroMountEnd why, bool toGround, float3 at, quaternion facing)
    {
        if (m.horse != Entity.Null && EntityManager.Exists(m.horse)) EntityManager.DestroyEntity(m.horse);
        m.horse = Entity.Null;
        m.lastEnd = why;
        m.nextMountTime = HeroMountRules.NextMountTime(_now, why, _tuning);
        Enter(ref m, HeroMountState.OnFoot);
        if (toGround) Jump(ref m, HeroMountRules.GroundBeside(at, facing), facing);
    }

    static void Enter(ref HeroMountComponent m, HeroMountState state)
    {
        m.state = state;
        m.elapsed = 0f;
        m.revision++;
    }

    static void Jump(ref HeroMountComponent m, float3 position, quaternion rotation)
    {
        m.poseJumpPosition = position;
        m.poseJumpRotation = rotation;
        m.poseJumpRevision++;
    }

    bool HorseAlive(Entity horse)
        => horse != Entity.Null && EntityManager.Exists(horse) && !EntityManager.HasComponent<IsDeadComponent>(horse)
           && EntityManager.GetComponentData<HealthComponent>(horse).currentHealth > 0f;

    bool Immobilised(Entity hero)
        => EntityManager.HasComponent<MoveSpeedComponent>(hero) && EntityManager.GetComponentData<MoveSpeedComponent>(hero).value <= 0f;

    bool Busy(Entity hero)
        => (EntityManager.HasComponent<SkillInstanceComponent>(hero) && EntityManager.GetComponentData<SkillInstanceComponent>(hero).active)
           || (EntityManager.HasComponent<HeroCombatComponent>(hero) && EntityManager.GetComponentData<HeroCombatComponent>(hero).isAttacking);

    /// <summary>The hero's heading on the ground (its yaw only).</summary>
    static quaternion Facing(quaternion rotation)
    {
        float3 forward = math.mul(rotation, new float3(0f, 0f, 1f));
        forward.y = 0f;
        return quaternion.LookRotationSafe(math.normalizesafe(forward, new float3(0f, 0f, 1f)), new float3(0f, 1f, 0f));
    }

    static bool Same(in HeroMountComponent a, in HeroMountComponent b)
        => a.state == b.state && a.elapsed == b.elapsed && a.horse == b.horse && a.revision == b.revision && a.lastEnd == b.lastEnd
           && a.healthAtStart == b.healthAtStart && a.reactionRevisionSeen == b.reactionRevisionSeen
           && a.poseJumpRevision == b.poseJumpRevision && a.nextMountTime == b.nextMountTime;
}
