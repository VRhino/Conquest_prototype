using UnityEngine;

namespace Conquest.WorldMap
{
    [CreateAssetMenu(menuName = "BronzeAge/World map art profile")]
    public sealed class WorldMapArtSettings : ScriptableObject
    {
        [Header("Eastern Mediterranean campaign palette")]
        public Color fertileGround = new Color(.48f, .50f, .29f);
        public Color dryGround = new Color(.69f, .58f, .40f);
        public Color limestone = new Color(.60f, .57f, .47f);
        public Color forestFloor = new Color(.30f, .36f, .21f);
        [Range(0, 1)] public float textureContrast = .28f;
        [Min(10)] public float groundTextureSize = 125;
        [Min(10)] public float rockTextureSize = 160;
        [Min(500)] public float terrainSurfaceDistance = 6500;
        [Min(100)] public float landscapeVariationSize = 950;
        [Range(0, .4f)] public float landscapeVariation = .14f;
        [Range(0, 1)] public float mountainFaceting = .85f;

        [Header("Sun and ambient light")]
        public Vector3 sunRotation = new Vector3(38, -40, 0);
        public Color sunlight = new Color(1, .94f, .81f);
        [Min(0)] public float sunIntensity = 1.2f;
        public Color skyLight = new Color(.48f, .55f, .61f);
        public Color horizonLight = new Color(.36f, .37f, .30f);
        public Color groundLight = new Color(.22f, .20f, .16f);

        [Header("Sea, lakes and sediment-bearing rivers")]
        public Color seaDeep = new Color(.055f, .16f, .24f);
        public Color seaShallow = new Color(.18f, .40f, .39f);
        public Color lakeDeep = new Color(.075f, .23f, .26f);
        public Color lakeShallow = new Color(.25f, .43f, .36f);
        public Color riverDeep = new Color(.15f, .31f, .29f);
        public Color riverShallow = new Color(.36f, .46f, .32f);
        [Range(0, .25f)] public float waterHighlights = .065f;
        [Min(10)] public float waterWaveSize = 95;
        [Min(1)] public float coastalDepth = 28;

        public Vector4 TerrainLook => new Vector4(rockTextureSize, landscapeVariationSize,
            landscapeVariation, mountainFaceting);
    }
}
