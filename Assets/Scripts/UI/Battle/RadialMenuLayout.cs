using UnityEngine;

/// <summary>Geometry of a radial menu with equal sectors, clockwise from the first one (H10).</summary>
public static class RadialMenuLayout
{
    public const int NoSector = -1;

    /// <summary>Degrees clockwise from straight up to the centre of sector <paramref name="index"/>.</summary>
    public static float SectorAngle(int index, int count, float firstAngle) => firstAngle + index * 360f / count;

    /// <summary>Where sector <paramref name="index"/> sits, relative to the centre of the menu.</summary>
    public static Vector2 SectorCenter(int index, int count, float firstAngle, float radius)
    {
        float angle = SectorAngle(index, count, firstAngle) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
    }

    /// <summary>
    /// The sector under <paramref name="offset"/> from the centre, or <see cref="NoSector"/> inside the dead zone and
    /// outside the menu, where the pointer may be on the rest of the HUD.
    /// </summary>
    public static int SectorAt(Vector2 offset, int count, float firstAngle, float deadZone, float outerRadius)
    {
        float distance = offset.magnitude;
        if (count <= 0 || distance < deadZone || distance > outerRadius) return NoSector;
        float angle = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
        float step = 360f / count;
        return Mathf.FloorToInt(Mathf.Repeat(angle - firstAngle + step / 2f, 360f) / step) % count;
    }
}
