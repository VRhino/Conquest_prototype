using Unity.Entities;
using UnityEngine.InputSystem;
using UnityEngine;

/// <summary>
/// Reads player hotkeys and writes the corresponding commands to the
/// <see cref="SquadInputComponent"/> of the active squad.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SquadControlSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool orderIssued = false;
        bool formationChanged = false;
        int formationIndex = -1; // Índice de formación en lugar de tipo específico
        SquadOrderType newOrder = default;

        if (keyboard.cKey.wasPressedThisFrame)
        {
            newOrder = SquadOrderType.FollowHero;
            orderIssued = true;
        }
        else if (keyboard.xKey.wasPressedThisFrame)
        {
            newOrder = SquadOrderType.HoldPosition;
            orderIssued = true;
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
            Debug.Log("F1 pressed - Formation change requested to index 0");
        }
        else if (keyboard.f2Key.wasPressedThisFrame)
        {
            formationIndex = 1;
            formationChanged = true;
            Debug.Log("F2 pressed - Formation change requested to index 1");
        }
        else if (keyboard.f3Key.wasPressedThisFrame)
        {
            formationIndex = 2;
            formationChanged = true;
            Debug.Log("F3 pressed - Formation change requested to index 2");
        }
        else if (keyboard.f4Key.wasPressedThisFrame)
        {
            formationIndex = 3;
            formationChanged = true;
            Debug.Log("F4 pressed - Formation change requested to index 3");
        }

        if (!orderIssued && !formationChanged)
            return;

        Debug.Log($"Processing input - Order: {orderIssued}, Formation: {formationChanged}, Index: {formationIndex}");

        // Encontrar el héroe local y su squad
        int heroCount = 0;
        foreach (var heroSquadRef in SystemAPI.Query<RefRO<HeroSquadReference>>().WithAll<IsLocalPlayer>())
        {
            heroCount++;
            Entity squadEntity = heroSquadRef.ValueRO.squad;
            Debug.Log($"Found hero #{heroCount} with squad entity: {squadEntity}");
            
            if (SystemAPI.HasComponent<SquadInputComponent>(squadEntity))
            {
                Debug.Log("Squad has SquadInputComponent");
                var input = SystemAPI.GetComponentRW<SquadInputComponent>(squadEntity);
                
                if (orderIssued)
                {
                    input.ValueRW.orderType = newOrder;
                }

                if (formationChanged)
                {
                    Debug.Log("Processing formation change");
                    // Obtener la biblioteca de formaciones del squad data
                    if (SystemAPI.HasComponent<SquadDataComponent>(squadEntity))
                    {
                        Debug.Log("Squad has SquadDataComponent");
                        var squadData = SystemAPI.GetComponent<SquadDataComponent>(squadEntity);
                        if (squadData.formationLibrary.IsCreated)
                        {
                            ref var formations = ref squadData.formationLibrary.Value.formations;
                            Debug.Log($"Squad has {formations.Length} formations available");
                            
                            // Verificar que el índice solicitado existe
                            if (formationIndex >= 0 && formationIndex < formations.Length)
                            {
                                FormationType newFormation = formations[formationIndex].formationType;
                                FormationType currentFormation = input.ValueRO.desiredFormation;
                                input.ValueRW.desiredFormation = newFormation;
                                Debug.Log($"Formation changed from {currentFormation} to {newFormation} (index {formationIndex})");
                            }
                            else
                            {
                                Debug.LogWarning($"Índice de formación no válido: {formationIndex}. Debe estar entre 0 y {formations.Length - 1}.");
                                continue; // No procesar este cambio
                            }
                        }
                        else
                        {
                            Debug.LogWarning("La biblioteca de formaciones no está creada.");
                            continue;
                        }
                    }
                    else
                    {
                        Debug.LogWarning("El componente SquadData no está presente en la entidad del squad.");
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
}
