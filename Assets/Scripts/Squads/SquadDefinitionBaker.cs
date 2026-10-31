using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Single conversion path for standalone and database squad definitions.</summary>
public abstract class SquadDefinitionBaker<T> : Baker<T> where T : MonoBehaviour
{
        /// <summary>Weapon reach beyond the body below which melee contact degenerates (C5).</summary>
        const float MinimumMeleeReach = 0.2f;

        protected void BakeSquad(Entity entity, SquadData d)
        {
            DependsOn(d);
            if (d.meleeData != null) DependsOn(d.meleeData);
            if (d.rangedData != null) DependsOn(d.rangedData);
            if (d.progressionData != null) DependsOn(d.progressionData);
            var melee  = d.meleeData;
            var ranged = d.rangedData;
            if (melee != null
                && MeleeReach.Reach(melee.attackRange, MovementTuningComponent.Default.bodyRadius) < MinimumMeleeReach)
                Debug.LogWarning($"[SquadDefinitionBaker] {d.name}: attackRange {melee.attackRange} leaves less than " +
                                 $"{MinimumMeleeReach} m of reach beyond the body radius; units will fight while touching.");

            // Bake formation library
            var formationLibrary = BakeFormationLibrary(d.formations, d.name);
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
                projectilesPerShot = ranged != null ? Unity.Mathematics.math.max(1, ranged.projectilesPerShot) : 1,
                fanAngleDegrees = ranged?.fanAngleDegrees ?? 0f,
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
                unitCount        = d.unitCount,
                GridSize         = default,
                leadershipCost   = d.leadershipCost,
                detectionRange   = d.detectionRange
            });

            AddComponent(entity, new SquadDataIDComponent { id = new FixedString64Bytes(d.id) });

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

        private BlobAssetReference<FormationLibraryBlob> BakeFormationLibrary(FormationDefinition[] definitions, string squadName)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<FormationLibraryBlob>();
            int count = definitions?.Length ?? 0;
            var formationArray = builder.Allocate(ref root.formations, count);
            float bodyDiameter = 2f * MovementTuningComponent.Default.bodyRadius;

            for (int i = 0; i < count; i++)
            {
                var definition = definitions[i];
                if (definition == null) continue;
                DependsOn(definition);
                string issue = definition.Validate();
                if (issue != null)
                    Debug.LogWarning($"[SquadDefinitionBaker] {squadName}: formation {definition.name}: {issue}");
                if (definition.HasLegacyGrid)
                    Debug.LogWarning($"[SquadDefinitionBaker] {squadName}: formation {definition.name} still holds the legacy grid; run Tools/Formations/Migrate Legacy Grid Formations.");
                if (definition.layout == FormationLayoutKind.Template && definition.EffectiveSpacing < bodyDiameter
                    && definition.formationType != FormationType.Testudo)
                    Debug.LogWarning($"[SquadDefinitionBaker] {squadName}: formation {definition.name} spacing {definition.EffectiveSpacing} m is below the body diameter ({bodyDiameter} m); bodies will overlap.");

                ref var formation = ref formationArray[i];
                formation.formationType = definition.formationType;
                formation.layout = definition.layout;
                formation.spacing = definition.EffectiveSpacing;
                formation.noiseRadius = Mathf.Max(0f, definition.noiseRadius);
                formation.noiseAngle = math.radians(Mathf.Max(0f, definition.noiseAngleDegrees));
                formation.useNormalPostureInFreeCombat = definition.useNormalPostureInFreeCombat;
                formation.supportsBrace = definition.supportsBrace;
                formation.braceHalfAngleRadians = math.radians(math.clamp(definition.braceHalfAngleDegrees, 0f, 90f));

                var postures = definition.postures ?? System.Array.Empty<FormationRolePosture>();
                var postureArray = builder.Allocate(ref formation.postures, postures.Length);
                for (int k = 0; k < postures.Length; k++)
                    postureArray[k] = new FormationPostureBlob
                    { role = postures[k].role, formed = postures[k].formed, moving = postures[k].moving };

                var templates = definition.layout == FormationLayoutKind.Template && definition.templates != null
                    ? definition.templates : System.Array.Empty<FormationTemplate>();
                var templateArray = builder.Allocate(ref formation.templates, templates.Length);
                for (int t = 0; t < templates.Length; t++)
                {
                    var template = templates[t];
                    var slots = template?.slots ?? System.Array.Empty<FormationSlot>();
                    templateArray[t].minUnits = template?.minUnits ?? 0;
                    var slotArray = builder.Allocate(ref templateArray[t].slots, slots.Length);
                    for (int k = 0; k < slots.Length; k++)
                    {
                        slotArray[k] = new FormationSlotBlob
                        {
                            position = slots[k].position,
                            row = math.max(0, slots[k].row),
                            role = slots[k].role,
                            heading = math.radians(slots[k].headingDegrees)
                        };
                    }
                }
            }

            var blob = builder.CreateBlobAssetReference<FormationLibraryBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }
}
