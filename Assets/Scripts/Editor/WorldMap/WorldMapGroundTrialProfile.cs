using System;
using UnityEngine;

namespace Conquest.WorldMap.Editor
{
    /// <summary>Regional terrain authoring; canonical masks and gameplay state remain read-only.</summary>
    public sealed class WorldMapGroundTrialProfile : ScriptableObject
    {
        [Serializable] public class Surface
        {
            public string name;
            public Texture2D color, normal;
            [ColorUsage(false,true)] public Color tint = Color.white;
            [Min(1)] public float tileMetres = 35;
            [Range(0, 1)] public float normalStrength = .15f;
        }
        [Serializable] public class Region
        {
            public int id;
            public string name;
            [Range(0, 1)] public float aridity;
            [Range(0, 1)] public float sand;
            [Range(0, 1)] public float alluvial;
        }
        [Header("Texturas originales de Yoge, sin recoloreado global")]
        public Surface[] surfaces = {
            new Surface{name="Hierba mediterránea"}, new Surface{name="Tierra seca"},
            new Surface{name="Roca clara",tileMetres=55}, new Surface{name="Suelo forestal"},
            new Surface{name="Limo y tierra fértil",tileMetres=25}, new Surface{name="Arena",tileMetres=45},
            new Surface{name="Grava y roca fragmentada",tileMetres=40}, new Surface{name="Pasto seco"}
        };
        [Header("Distribución espacial")]
        public int seed = 42;
        [Min(100)] public float broadPatchMetres = 850;
        [Min(20)] public float mediumPatchMetres = 180;
        [Min(20)] public float regionTransitionMetres = 250;
        [Range(0, .5f)] public float patchVariation = .28f;
        [Range(0,45)] public float rockStartSlope = 12;
        [Range(1,60)] public float rockFullSlope = 35;
        [Range(0,1)] public float dryPastureStrength = .5f;
        [Tooltip("Escala y amplitud UV de la variación grande y pequeña. Amplitud cero desactiva el efecto.")]
        public Vector4 textureWarp = new Vector4(180,1.8f,80,.7f);
        [Header("Roca de campaña (intensidad cero conserva la textura original)")]
        public Vector4 mountainMassTreatment = new Vector4(0,420,.18f,0);
        [Tooltip("Strength, aspect variation, pale upper rock, dark steep faces. Zero strength preserves previous trials.")]
        public Vector4 mountainDetail = Vector4.zero;
        [Tooltip("World X/Z centre and ellipse radii for the regional rock iteration.")]
        public Vector4 mountainDetailArea = new Vector4(0,0,1,1);
        public Color mountainMassColor = new Color(.62f,.59f,.50f);
        public int alphamapResolution = 256;
        public Region[] regions;

        public void SetRegionalDefaults()
        {
            string[] names={"Costa boscosa","Altiplano interior","Tierras altas secas","Corredor levantino",
                "Cuenca fértil","Gran llanura fluvial","Piedemonte oriental","Colinas boscosas",
                "Llanura costera meridional","Valle meridional","Estepa abierta","Península de los golfos",
                "Tierras del estrecho","Meseta del istmo","Delta interior","Valles orientales"};
            float[] aridity={.30f,.58f,.75f,.23f,.20f,.50f,.77f,.15f,.45f,.21f,.85f,.38f,.32f,.82f,.12f,.35f};
            regions=new Region[names.Length];
            for(int i=0;i<regions.Length;i++)regions[i]=new Region{id=i+1,name=names[i],aridity=aridity[i],
                sand=i==8||i==10||i==13?.55f:i==9||i==14?.25f:.08f,
                alluvial=i==5||i==9||i==14||i==15?.8f:i==3||i==4?.45f:.10f};
        }
    }
}
