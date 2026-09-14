using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Single conversion path for standalone and database squad definitions.</summary>
public abstract class SquadDefinitionBaker<T> : Baker<T> where T : MonoBehaviour
{
        protected void BakeSquad(Entity entity, SquadData d)
        {
            DependsOn(d);
            if (d.meleeData != null) DependsOn(d.meleeData);
            if (d.rangedData != null) DependsOn(d.rangedData);
            if (d.progressionData != null) DependsOn(d.progressionData);
            var melee  = d.meleeData;
            var ranged = d.rangedData;

            Entity prefabEntity = d.prefab != null
                ? GetEntity(d.prefab, TransformUsageFlags.Dynamic)
                : Entity.Null;

            // Bake formation library
            var formationLibrary = BakeFormationLibrary(d.gridFormations);
            AddBlobAsset(ref formationLibrary, out _);
            var curves = SquadProgressionCurves.Create(d.progressionData);
            AddBlobAsset(ref curves, out _);
            var abilityEntities = new System.Collections.Generic.List<AbilityByLevelElement>();
            if (d.abilitiesByLevel != null)
                foreach (var ability in d.abilitiesByLevel)
                {
                    Entity abilityEntity = Entity.Null;
                    if (ability != null)
                    {
                        DependsOn(ability);
                        abilityEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                        AddComponent(abilityEntity, new SquadAbilityDefinitionComponent
                        {
                            name = new FixedString64Bytes(ability.abilityName ?? string.Empty),
                            cooldown = ability.cooldown
                        });
                    }
                    // Preserve empty slots: index determines unlock level.
                    abilityEntities.Add(new AbilityByLevelElement { Value = abilityEntity });
                }
            var abilities = AddBuffer<AbilityByLevelElement>(entity);
            foreach (var ability in abilityEntities) abilities.Add(ability);

            float slashDmg  = melee != null ? melee.slashingDamage        : ranged?.slashingDamage        ?? 0f;
            float pierceDmg = melee != null ? melee.piercingDamage        : ranged?.piercingDamage        ?? 0f;
            float bluntDmg  = melee != null ? melee.bluntDamage           : ranged?.bluntDamage           ?? 0f;
            float slashPen  = melee != null ? melee.slashingPenetration   : ranged?.slashingPenetration   ?? 0f;
            float piercePen = melee != null ? melee.piercingPenetration   : ranged?.piercingPenetration   ?? 0f;
            float bluntPen  = melee != null ? melee.bluntPenetration      : ranged?.bluntPenetration      ?? 0f;

            bool   isRanged      = ranged != null;
            string poolKey       = ranged?.projectilePoolKey ?? string.Empty;

            AddComponent(entity, new SquadDataComponent
            {
                baseHealth = d.baseHealth,
                baseSpeed = d.baseSpeed,
                mass = d.massValue,
                weight = d.totalWeight,
                block = d.block,
                blockRegenRate = d.blockRegenRate,
                shieldBreakStunDuration = d.shieldBreakStunDuration,
                slashingDefense = d.slashingDefense,
                piercingDefense = d.piercingDefense,
                bluntDefense = d.bluntDefense,
                slashingDamage = slashDmg,
                piercingDamage = pierceDmg,
                bluntDamage = bluntDmg,
                slashingPenetration = slashPen,
                piercingPenetration = piercePen,
                bluntPenetration = bluntPen,
                isRangedUnit = isRanged,
                range            = ranged?.range        ?? 0f,
                accuracy         = ranged?.accuracy     ?? 0f,
                fireRate         = ranged?.fireRate     ?? 0f,
                reloadSpeed      = ranged?.reloadSpeed  ?? 0f,
                ammoCapacity     = ranged?.ammo         ?? 0,
                projectilePoolKey = string.IsNullOrEmpty(poolKey)
                    ? default
                    : new FixedString32Bytes(poolKey),
                projectileTrajectory = ranged?.projectileTrajectory ?? default,
                curves = curves,
                attackRange             = melee?.attackRange             ?? 2f,
                attackInterval          = melee?.attackInterval          ?? 1.5f,
                criticalChance          = melee?.criticalChance          ?? 0.05f,
                criticalMultiplier      = melee?.criticalMultiplier      ?? 1.5f,
                strikeWindowStart       = melee?.strikeWindowStart       ?? 0.35f,
                strikeWindowDuration    = melee?.strikeWindowDuration    ?? 0.15f,
                attackAnimationDuration = melee?.attackAnimationDuration ?? 1.0f,
                kineticMultiplier       = melee?.kineticMultiplier       ?? 0.3f
            });

            AddComponent(entity, new SquadDefinitionComponent
            {
                squadType        = d.type,
                behaviorProfile  = d.behaviorProfile,
                formationLibrary = formationLibrary,
                unitPrefab       = prefabEntity,
                unitCount        = d.unitCount,
                GridSize         = default,
                leadershipCost   = d.leadershipCost,
                detectionRange   = d.detectionRange
            });

            AddComponent(entity, new SquadDataIDComponent { id = new FixedString64Bytes(d.id) });

            AddComponent(entity, new SquadStatsComponent
            {
                squadType = d.type,
                behaviorProfile = d.behaviorProfile
            });

            var statsBuffer = AddBuffer<UnitStatsBufferElement>(entity);
            statsBuffer.Add(new UnitStatsBufferElement
            {
                health = (int)d.baseHealth,
                speed = (int)d.baseSpeed,
                mass = (int)d.massValue,
                weightClass = (int)d.totalWeight,
                blockValue = d.block,
                slashingDamage = slashDmg,
                piercingDamage = pierceDmg,
                bluntDamage = bluntDmg,
                slashingDefense = d.slashingDefense,
                piercingDefense = d.piercingDefense,
                bluntDefense = d.bluntDefense,
                leadershipCost = d.leadershipCost
            });
        }

        private BlobAssetReference<FormationLibraryBlob> BakeFormationLibrary(GridFormationScriptableObject[] gridFormations)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<FormationLibraryBlob>();

            if (gridFormations == null || gridFormations.Length == 0)
            {
                // No grid formations assigned to squad data
                builder.Allocate(ref root.formations, 0);
            }
            else
            {
                var formationArray = builder.Allocate(ref root.formations, gridFormations.Length);

                for (int i = 0; i < gridFormations.Length; i++)
                {
                    var gridForm = gridFormations[i];
                    if (gridForm == null)
                    {
                        // Grid formation is null, skipping
                        continue;
                    }

                    DependsOn(gridForm);
                    formationArray[i].formationType = gridForm.formationType;

                    // Store original grid positions from ScriptableObject
                    Vector2Int[] originalPositions = gridForm.gridPositions ?? System.Array.Empty<Vector2Int>();
                    var gridPositions = builder.Allocate(ref formationArray[i].gridPositions, originalPositions.Length);
                    for (int j = 0; j < originalPositions.Length; j++)
                    {
                        gridPositions[j] = new int2(originalPositions[j].x, originalPositions[j].y);
                    }
                }
            }

            var blob = builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }
}
