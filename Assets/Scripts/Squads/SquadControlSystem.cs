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
                            
                            // Verificar que el índice solicitado existe
                            if (formationIndex >= 0 && formationIndex < formations.Length)
                            {
                                FormationType newFormation = formations[formationIndex].formationType;
                                FormationType currentFormation = input.ValueRO.desiredFormation;
                                input.ValueRW.desiredFormation = newFormation;
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
