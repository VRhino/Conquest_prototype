using Unity.Entities;
using UnityEngine.InputSystem;
using UnityEngine;
using Unity.Mathematics;
using Unity.Transforms;
using System.Collections.Generic;

/// <summary>
/// Reads player hotkeys and writes the corresponding commands to the
/// <see cref="SquadInputComponent"/> of the active squad.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(SquadOrderSystem))]
[UpdateBefore(typeof(OrderResolutionSystem))]
[UpdateBefore(typeof(FormationSystem))]
public partial class SquadControlSystem : SystemBase
{
    private Camera _mainCamera;

    // Variables para el doble clic de X
    private float _lastXPressTime = float.NegativeInfinity;
    private const float DOUBLE_CLICK_THRESHOLD = 0.5f; // Tiempo máximo entre clics para detectar doble clic
    private float _lastCPressTime = float.NegativeInfinity; // Para doble clic de C

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<MatchStateComponent>();
        _mainCamera = Camera.main;
    }

    protected override void OnUpdate()
    {
        // Timers must advance even while input devices are disconnected or UI owns input.
        float dt = SystemAPI.Time.DeltaTime;
        foreach (var heroSquadRef in SystemAPI.Query<RefRO<HeroSquadReference>>().WithAll<IsLocalPlayer>())
        {
            Entity tickSquad = heroSquadRef.ValueRO.squad;
            if (!SystemAPI.HasComponent<SquadPlayerOrderIntentComponent>(tickSquad)) continue;
            var intent = SystemAPI.GetComponent<SquadPlayerOrderIntentComponent>(tickSquad);
            bool intentChanged = false;
            if (intent.insistenceTimer > 0f) { intent.insistenceTimer -= dt; intentChanged = true; }
            if (intent.heroOrdenCooldownTimer > 0f)
            {
                intent.heroOrdenCooldownTimer -= dt;
                if (intent.heroOrdenCooldownTimer <= 0f)
                    intent.heroOrdenCooldownActive = false;
                intentChanged = true;
            }
            if (intentChanged) SystemAPI.SetComponent(tickSquad, intent);
        }

        if (_mainCamera == null)
            _mainCamera = Camera.main;
            
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        
        if (keyboard == null || mouse == null || DialogueUIState.IsDialogueOpen)
            return;

        bool orderIssued = false;
        bool formationChanged = false;
        int formationIndex = -1; // Índice de formación en lugar de tipo específico
        SquadOrderType newOrder = default;
        float3 mouseWorldPosition = float3.zero;
        bool isDoubleClickX = false;
        // Doble clic de C para toggle
        bool isDoubleClickC = false;
        if (keyboard.cKey.wasPressedThisFrame)
        {
            float currentTimeC = (float)SystemAPI.Time.ElapsedTime;
            if (currentTimeC - _lastCPressTime <= DOUBLE_CLICK_THRESHOLD)
            {
                isDoubleClickC = true;
            }
            _lastCPressTime = currentTimeC;
        }
        

        if (keyboard.cKey.wasPressedThisFrame)
        {
            newOrder = SquadOrderType.FollowHero;
            orderIssued = true;
        }
        else if (keyboard.xKey.wasPressedThisFrame)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            
            // Verificar si es un doble clic
            if (currentTime - _lastXPressTime <= DOUBLE_CLICK_THRESHOLD)
            {
                // Es un doble clic - cambiar formación
                isDoubleClickX = true;
                formationChanged = true;
            }
            else
            {
                // Es un clic simple - orden HoldPosition
                newOrder = SquadOrderType.HoldPosition;
                orderIssued = true;
                
                // Capturar la posición del mouse en el terreno
                mouseWorldPosition = GetMouseWorldPosition();
            }
            
            _lastXPressTime = currentTime;
        }
        else if (keyboard.vKey.wasPressedThisFrame)
        {
            newOrder = SquadOrderType.Attack;
            orderIssued = true;
        }

        if (keyboard.f1Key.wasPressedThisFrame)
        {
            formationIndex = 0;
            formationChanged = true;
        }
        else if (keyboard.f2Key.wasPressedThisFrame)
        {
            formationIndex = 1;
            formationChanged = true;
        }
        else if (keyboard.f3Key.wasPressedThisFrame)
        {
            formationIndex = 2;
            formationChanged = true;
        }
        else if (keyboard.f4Key.wasPressedThisFrame)
        {
            formationIndex = 3;
            formationChanged = true;
        }

        if (!orderIssued && !formationChanged)
            return;

        // Process input without debug logging

        // Collect all changes first, then apply them outside the iteration
        var squadChanges = new List<(Entity squadEntity, SquadInputComponent input, SquadPlayerOrderIntentComponent intent)>();
        var command = new SquadControlCommand
        {
            orderIssued = orderIssued,
            order = newOrder,
            holdPosition = mouseWorldPosition,
            toggleHurry = isDoubleClickC,
            formationRequested = formationChanged,
            formationIndex = formationIndex,
            cycleFormation = isDoubleClickX
        };

        // Encontrar el héroe local y su squad
        foreach (var heroSquadRef in SystemAPI.Query<RefRO<HeroSquadReference>>().WithAll<IsLocalPlayer>())
        {
            Entity squadEntity = heroSquadRef.ValueRO.squad;
            
            if (SystemAPI.HasComponent<SquadInputComponent>(squadEntity))
            {
                var input  = SystemAPI.GetComponent<SquadInputComponent>(squadEntity);
                var intent = SystemAPI.HasComponent<SquadPlayerOrderIntentComponent>(squadEntity)
                    ? SystemAPI.GetComponent<SquadPlayerOrderIntentComponent>(squadEntity)
                    : new SquadPlayerOrderIntentComponent();

                var definition = SystemAPI.HasComponent<SquadDefinitionComponent>(squadEntity)
                    ? SystemAPI.GetComponent<SquadDefinitionComponent>(squadEntity)
                    : default;
                if (!SquadPlayerCommandResolver.TryApply(command, definition, ref input, ref intent))
                    continue;
                squadChanges.Add((squadEntity, input, intent));
            }
        }

        // Apply after the hero query: no structural changes or command buffer are needed.
        foreach (var (squadEntity, input, intent) in squadChanges)
        {
            EntityManager.SetComponentData(squadEntity, input);
            if (EntityManager.HasComponent<SquadPlayerOrderIntentComponent>(squadEntity))
                EntityManager.SetComponentData(squadEntity, intent);
        }
    }

    /// <summary>
    /// Gets the world position of the mouse cursor projected onto the terrain.
    /// Returns the hero's position if the raycast fails.
    /// </summary>
    private float3 GetMouseWorldPosition()
    {
        if (_mainCamera == null)
            return float3.zero;

        var mouse = Mouse.current;
        if (mouse == null)
            return float3.zero;

        Vector2 mouseScreenPosition = mouse.position.ReadValue();
        Ray ray = _mainCamera.ScreenPointToRay(mouseScreenPosition);

        // Intentar hacer raycast con el terreno
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            // Verificar si el hit es con el terreno (puedes ajustar esto según tus layers)
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Default") || 
                hit.collider.CompareTag(GameTags.Terrain))
            {
                return new float3(hit.point.x, hit.point.y, hit.point.z);
            }
        }

        // Si no se encontró el terreno, usar un plano Y=0 como fallback
        float distance = 0f;
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            return new float3(hitPoint.x, 0f, hitPoint.z);
        }

        // Último recurso: devolver posición del héroe si está disponible
        foreach (var heroSquadRef in SystemAPI.Query<RefRO<HeroSquadReference>>().WithAll<IsLocalPlayer>())
        {
            Entity squadEntity = heroSquadRef.ValueRO.squad;
            var ownerLookup = GetComponentLookup<SquadOwnerComponent>(true);
            var transformLookup = GetComponentLookup<LocalTransform>(true);
            
            if (HeroPositionUtility.TryGetHeroPosition(squadEntity, ownerLookup, transformLookup, out float3 heroPosition))
            {
                return heroPosition;
            }
        }

        return float3.zero;
    }
}
