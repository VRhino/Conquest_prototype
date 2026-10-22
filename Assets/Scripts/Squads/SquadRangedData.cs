using UnityEngine;

/// <summary>
/// Datos de combate a distancia para un squad.
/// Asignar este asset a <see cref="SquadData.rangedData"/> habilita el comportamiento ranged.
/// </summary>
[CreateAssetMenu(menuName = "Squads/Ranged Data")]
public class SquadRangedData : ScriptableObject
{
    [Header("Projectile Damage")]
    public float slashingDamage;
    public float piercingDamage;
    public float bluntDamage;

    [Header("Penetration")]
    public float slashingPenetration;
    public float piercingPenetration;
    public float bluntPenetration;

    [Header("Ranged Mechanics")]
    public float range;
    public float accuracy;
    public float fireRate;
    public float reloadSpeed;
    public int ammo;

    [Header("Projectile")]
    public string projectilePoolKey;
    public GameObject projectilePrefab;
    public ProjectileTrajectory projectileTrajectory;

    [Header("Launcher (L2-7)")]
    [Tooltip("Proyectiles por disparo; con más de uno salen en abanico.")]
    [Min(1)] public int projectilesPerShot = 1;
    [Tooltip("Apertura total del abanico en grados.")]
    public float fanAngleDegrees = 0f;
}
