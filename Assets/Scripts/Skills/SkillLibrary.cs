using System;
using System.Collections.Generic;
using Unity.Entities;

/// <summary>
/// Owns the runtime skill blobs, one per ability asset (or basic-attack timing), shared by every hero that uses it.
/// Its owner disposes it with its world.
/// </summary>
public sealed class SkillLibrary : IDisposable
{
    readonly Dictionary<HeroAbility, BlobAssetReference<SkillBlob>> _abilities = new();
    readonly Dictionary<(float, float, float, float, float), BlobAssetReference<SkillBlob>> _basics = new();

    public BlobAssetReference<SkillBlob> Get(HeroAbility ability)
    {
        if (!_abilities.TryGetValue(ability, out var blob))
            _abilities[ability] = blob = SkillBlobBuilder.Build(ability);
        return blob;
    }

    public BlobAssetReference<SkillBlob> GetBasicAttack(in HeroCombatComponent combat, float staminaCost, float cooldown)
    {
        var key = (combat.strikeWindowStart, combat.strikeWindowDuration, combat.attackAnimationDuration, staminaCost, cooldown);
        if (!_basics.TryGetValue(key, out var blob))
            _basics[key] = blob = SkillBlobBuilder.BuildBasicAttack(combat, staminaCost, cooldown);
        return blob;
    }

    public void Dispose()
    {
        foreach (var blob in _abilities.Values) blob.Dispose();
        foreach (var blob in _basics.Values) blob.Dispose();
        _abilities.Clear();
        _basics.Clear();
    }
}
