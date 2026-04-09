using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Data.Items;

/// <summary>
/// Aplica la customización de avatar (partes de cabeza, pelo, barba) y equipamiento visual
/// a cada héroe recién instanciado (local y remoto).
/// Se ejecuta una única vez por héroe, marcando la entidad con HeroVisualAppearanceApplied.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HeroVisualInstantiationSystem))]
public partial class HeroVisualAppearanceSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // Aplicar apariencia a héroes locales recién instanciados
        foreach (var (visualInstance, entity) in
                 SystemAPI.Query<RefRO<HeroVisualInstance>>()
                          .WithAll<IsLocalPlayer>()
                          .WithNone<HeroVisualAppearanceApplied>()
                          .WithEntityAccess())
        {
            var go = FindGameObjectById(visualInstance.ValueRO.visualInstanceId);
            if (go == null) continue;

            try { ApplyHeroVisualCustomization(go); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HeroVisualAppearanceSystem] Error aplicando apariencia local: {ex.Message}\n{ex.StackTrace}");
            }

            ecb.AddComponent<HeroVisualAppearanceApplied>(entity);
        }

        // Aplicar apariencia a héroes remotos recién instanciados
        foreach (var (visualInstance, entity) in
                 SystemAPI.Query<RefRO<HeroVisualInstance>>()
                          .WithNone<IsLocalPlayer, HeroVisualAppearanceApplied>()
                          .WithEntityAccess())
        {
            if (!EntityManager.HasComponent<HeroAppearanceComponent>(entity)) continue;

            var go = FindGameObjectById(visualInstance.ValueRO.visualInstanceId);
            if (go == null) continue;

            var appearance = EntityManager.GetComponentObject<HeroAppearanceComponent>(entity);
            try { ApplyHeroVisualCustomization(go, appearance); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HeroVisualAppearanceSystem] Error aplicando apariencia remota: {ex.Message}\n{ex.StackTrace}");
            }

            ecb.AddComponent<HeroVisualAppearanceApplied>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    /// <summary>
    /// Aplica partes de avatar y equipamiento al GameObject visual.
    /// Si remoteAppearance es null, usa el héroe local (PlayerSessionService.SelectedHero).
    /// </summary>
    private static void ApplyHeroVisualCustomization(GameObject visualInstance, HeroAppearanceComponent remoteAppearance = null)
    {
        AvatarParts avatar;
        Equipment   equipment;
        string      gender;

        if (remoteAppearance != null)
        {
            avatar    = remoteAppearance.avatar;
            equipment = remoteAppearance.equipment;
            gender    = remoteAppearance.gender;
        }
        else
        {
            var heroData = PlayerSessionService.SelectedHero;
            if (heroData == null) return;
            avatar    = heroData.avatar;
            equipment = heroData.equipment;
            gender    = heroData.gender;
        }

        if (avatar == null) return;

        var avatarPartDatabase = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
        if (avatarPartDatabase == null) return;

        var baseVisualPartIds = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(avatar.headId))    baseVisualPartIds.Add(avatar.headId);
        if (!string.IsNullOrEmpty(avatar.hairId))    baseVisualPartIds.Add(avatar.hairId);
        if (!string.IsNullOrEmpty(avatar.beardId))   baseVisualPartIds.Add(avatar.beardId);
        if (!string.IsNullOrEmpty(avatar.eyebrowId)) baseVisualPartIds.Add(avatar.eyebrowId);

        var genderEnum = gender == "Male" ? Gender.Male : Gender.Female;

        Data.Avatar.AvatarVisualUtils.ResetModularDummyToBase(
            visualInstance.transform, avatarPartDatabase, baseVisualPartIds, genderEnum);

        if (equipment != null)
        {
            var tempHero = new HeroData { gender = gender, equipment = equipment };
            foreach (var itemId in tempHero.GetEquipment())
            {
                if (string.IsNullOrEmpty(itemId)) continue;
                var itemData = ItemService.GetItemById(itemId);
                if (itemData != null && !string.IsNullOrEmpty(itemData.visualPartId))
                {
                    Data.Avatar.AvatarVisualUtils.ToggleArmorVisibilityByAvatarPartId(
                        visualInstance.transform, avatarPartDatabase, itemData.visualPartId, genderEnum);
                }
            }
        }
    }

    private static GameObject FindGameObjectById(int instanceId)
    {
        var allObjects = Object.FindObjectsOfType<GameObject>();
        return System.Array.Find(allObjects, obj => obj.GetInstanceID() == instanceId);
    }
}
