using System;
using System.Collections.Generic;
using Data.Avatar;
using UnityEngine;

/// <summary>Mounts the final body once. Weapons and gameplay components remain on the rig.</summary>
public sealed class BattleHeroVisual : MonoBehaviour
{
    public BattleSkinCatalog catalog;
    public bool IsPrepared { get; private set; }

    public void Prepare(AvatarParts avatar, Equipment equipment, string gender,
        AvatarPartDatabase database, AvatarBasePartsDefinition baseParts)
    {
        if (IsPrepared) return;
        if (catalog == null || avatar == null || database == null || baseParts == null)
            throw new InvalidOperationException("Incomplete battle appearance data.");
        var selector = GetComponent<AvatarPartSelector>();
        if (selector == null) throw new InvalidOperationException("Missing avatar groups.");
        bool male = gender == "Male";
        selector.maleParts.SetActive(male);
        selector.femaleParts.SetActive(!male);
        var selected = new Dictionary<string, string>();
        void Select(string id, bool clearEmpty = false)
        {
            if (string.IsNullOrEmpty(id)) return;
            var part = AvatarVisualUtils.FindAvatarPartById(database, id)
                ?? throw new InvalidOperationException("Unknown visual part: " + id);
            foreach (var attachment in part.attachments)
            {
                string group = male ? attachment.boneTargetMale : attachment.boneTargetFemale;
                string name = male ? attachment.prefabPathMale : attachment.prefabPathFemale;
                if (string.IsNullOrEmpty(group) || (!clearEmpty && string.IsNullOrEmpty(name))) continue;
                var target = transform.Find(group) ?? AvatarVisualUtils.FindDeepChild(transform, group);
                // Legacy definitions can target a leaf mesh instead of a group. That operation
                // has no children to toggle in the authoring rig, and remains a no-op here.
                if (target == null)
                {
                    if (Array.Exists(catalog.parts, p => p.name == group || p.parentPath + "/" + p.name == group)) continue;
                    throw new InvalidOperationException("Missing attachment group: " + group);
                }
                selected[PathFromRoot(target)] = name ?? string.Empty;
            }
        }
        foreach (var id in baseParts.basePartIds) Select(id, true);
        Select(avatar.headId); Select(avatar.hairId); Select(avatar.beardId); Select(avatar.eyebrowId);
        if (equipment != null)
            foreach (var itemId in new HeroData { equipment = equipment }.GetEquipment())
            {
                if (string.IsNullOrEmpty(itemId)) continue;
                var item = ItemService.GetItemById(itemId)
                    ?? throw new InvalidOperationException("Unknown equipped item: " + itemId);
                Select(item.visualPartId);
            }
        var mounted = new List<GameObject>();
        try
        {
            foreach (var part in catalog.parts)
            {
                bool wanted = selected.TryGetValue(part.parentPath, out var name) ? name == part.name : part.active;
                var parent = transform.Find(part.parentPath);
                if (wanted && parent != null && IsActiveWithin(parent)) mounted.Add(part.Mount(transform).gameObject);
            }
            IsPrepared = true;
        }
        catch
        {
            foreach (var go in mounted) { go.SetActive(false); Destroy(go); }
            throw;
        }
    }

    bool IsActiveWithin(Transform node)
    {
        for (var current = node; current != transform; current = current.parent)
            if (current == null || !current.gameObject.activeSelf) return false;
        return true;
    }

    string PathFromRoot(Transform node)
    {
        var names = new Stack<string>();
        for (var current = node; current != transform; current = current.parent) names.Push(current.name);
        return string.Join("/", names);
    }
}
