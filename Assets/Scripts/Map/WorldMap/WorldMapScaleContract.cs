using UnityEngine;

namespace Conquest.WorldMap
{
    public enum CampaignTravelKind { Light, SoloHero, Mounted, HorseCaravan, CamelCaravan, Medium, Heavy, ReturningFoundation }

    // Strategic units and economic clock belong to BronzeAge; Unity only converts them.
    [CreateAssetMenu(menuName = "BronzeAge/World map scale contract")]
    public sealed class WorldMapScaleContract : ScriptableObject
    {
        public string revision = "calibracion-2026-10-12-factor12";
        [Min(.01f)] public float unityPerStrategicUnit = 12;
        [Min(1)] public float secondsPerEconomicTick = 60;
        public float lightPerTick = 30, soloHeroPerTick = 33, mountedPerTick = 42;
        public float horseCaravanPerTick = 36, camelCaravanPerTick = 28;
        public float mediumPerTick = 24, heavyPerTick = 18, returningFoundationPerTick = 15;
        public float initialInfluence = 6;
        public float[] influenceByLevel = { 10, 15, 20, 25, 30 };
        public float settlementSeparation = 30, visibleGroundWidth = 15;
        public float doorRadius = 2, encounterRadius = 3;
        public float focusRadius = 150, frontierRadius = 250, campSeparation = 600, tinExclusion = 200;
        public Vector2 tinDistrictRadius = new Vector2(20, 30);
        public float citySymbolRadius = .8f;
        [Min(.1f)] public float armySymbolHeightUnity = 10;
        [Tooltip("Approved scale for the animated Feudo hero; independent of strategic distances.")]
        [Min(.01f)] public float heroSymbolScale = 6;
        public float minimumVisibleWidth = 7.5f, maximumVisibleWidth = 150;
        public float ToUnity(float strategic) => strategic * unityPerStrategicUnit;
        public float ToStrategic(float unity) => unity / unityPerStrategicUnit;
        public Vector2 StrategicWorldSize(Vector2 unitySize) => unitySize / unityPerStrategicUnit;
        public float InfluenceUnity(int index) => ToUnity(index == 0 ? initialInfluence : influenceByLevel[index - 1]);
        public float SpeedUnity(CampaignTravelKind kind)
        {
            float perTick;
            switch (kind)
            {
                case CampaignTravelKind.SoloHero: perTick = soloHeroPerTick; break;
                case CampaignTravelKind.Mounted: perTick = mountedPerTick; break;
                case CampaignTravelKind.HorseCaravan: perTick = horseCaravanPerTick; break;
                case CampaignTravelKind.CamelCaravan: perTick = camelCaravanPerTick; break;
                case CampaignTravelKind.Medium: perTick = mediumPerTick; break;
                case CampaignTravelKind.Heavy: perTick = heavyPerTick; break;
                case CampaignTravelKind.ReturningFoundation: perTick = returningFoundationPerTick; break;
                default: perTick = lightPerTick; break;
            }
            return ToUnity(perTick) / secondsPerEconomicTick;
        }
    }
}
