using Unity.Entities;
using UnityEngine.InputSystem;
using UnityEngine;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Reads player hotkeys and writes the corresponding commands to the
/// <see cref="SquadInputComponent"/> of the active squad.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadControlSystem : SystemBase
{
    private Camera _mainCamera;
    
    // Variables para el doble clic de X
    private float _lastXPressTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.5f; // Tiempo máximo entre clics para detectar doble clic
    
    protected override void OnCreate()
    {
        base.OnCreate();
        _mainCamera = Camera.main;
    }

    protected override void OnUpdate()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;
            
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        
        if (keyboard == null || mouse == null)
            return;

        bool orderIssued = false;
        bool formationChanged = false;
        int formationIndex = -1; // Índice de formación en lugar de tipo específico
        SquadOrderType newOrder = default;
        float3 mouseWorldPosition = float3.zero;
        bool isDoubleClickX = false;

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

        // Encontrar el héroe local y su squad
        int heroCount = 0;
        foreach (var heroSquadRef in SystemAPI.Query<RefRO<HeroSquadReference>>().WithAll<IsLocalPlayer>())
        {
            heroCount++;
            Entity squadEntity = heroSquadRef.ValueRO.squad;
            
            if (SystemAPI.HasComponent<SquadInputComponent>(squadEntity))
            {
                var input = SystemAPI.GetComponentRW<SquadInputComponent>(squadEntity);
                
                if (orderIssued)
                {
                    input.ValueRW.orderType = newOrder;
                    if (newOrder == SquadOrderType.HoldPosition)
                    {
                        input.ValueRW.holdPosition = mouseWorldPosition;
                    }
                }

                if (formationChanged)
                {
                    // Obtener la biblioteca de formaciones del squad data
                    if (SystemAPI.HasComponent<SquadDataComponent>(squadEntity))
                    {
                        var squadData = SystemAPI.GetComponent<SquadDataComponent>(squadEntity);
                        if (squadData.formationLibrary.IsCreated)
                        {
                            ref var formations = ref squadData.formationLibrary.Value.formations;
                            
                            if (isDoubleClickX)
                            {
                                // Lógica para doble clic X - cambiar a la siguiente formación disponible
                                FormationType currentFormation = input.ValueRO.desiredFormation;
                                
                                // Encontrar el índice actual de la formación
                                int currentIndex = -1;
                                for (int i = 0; i < formations.Length; i++)
                                {
                                    if (formations[i].formationType == currentFormation)
                                    {
                                        currentIndex = i;
                                        break;
                                    }
                                }
                                
                                // Calcular el siguiente índice (circular)
                                int nextIndex = (currentIndex + 1) % formations.Length;
                                FormationType nextFormation = formations[nextIndex].formationType;
                                
                                input.ValueRW.desiredFormation = nextFormation;
                                
                                // Cambio cíclico de formación con doble clic X
                            }
                            else
                            {
                                // Lógica original para F1-F4
                                // Verificar que el índice solicitado existe
                                if (formationIndex >= 0 && formationIndex < formations.Length)
                                {
                                    FormationType newFormation = formations[formationIndex].formationType;
                                    FormationType currentFormation = input.ValueRO.desiredFormation;
                                    input.ValueRW.desiredFormation = newFormation;
                                }
                                else
                                {
                                    // Índice de formación no válido
                                    continue; // No procesar este cambio
                                }
                            }
                        }
                        else
                        {
                            // La biblioteca de formaciones no está creada
                            continue;
                        }
                    }
                    else
                    {
                        // El componente SquadData no está presente
                        continue;
                    }
                }

                input.ValueRW.hasNewOrder = true;
            }
            else
            {
            }
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
                hit.collider.CompareTag("Terrain"))
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
            if (SystemAPI.HasComponent<SquadOwnerComponent>(squadEntity))
            {
                var squadOwner = SystemAPI.GetComponent<SquadOwnerComponent>(squadEntity);
                if (SystemAPI.HasComponent<LocalTransform>(squadOwner.hero))
                {
                    var heroTransform = SystemAPI.GetComponent<LocalTransform>(squadOwner.hero);
                    return heroTransform.Position;
                }
            }
        }

        return float3.zero;
    }
}
