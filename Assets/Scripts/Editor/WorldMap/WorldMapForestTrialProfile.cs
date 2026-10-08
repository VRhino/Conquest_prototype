using UnityEngine;

namespace Conquest.WorldMap.Editor
{
    /// <summary>Visual authoring only: does not change forest resources or worldgen fields.</summary>
    public sealed class WorldMapForestTrialProfile : ScriptableObject
    {
        public int seed = 42;
        [Range(0, 1)] public float minimumDensity = .18f;
        [Min(4)] public float candidateCellSize = 12;
        [Min(4)] public float minimumSpacing = 17;
        [Min(4)] public float maximumSpacing = 75;
        public AnimationCurve densityToSpacing = AnimationCurve.Linear(0, 75, 1, 17);
        public Vector2 scaleRange = new Vector2(.85f, 1.15f);
        [Header("Bordes naturales (solo representación)")]
        [Min(10)] public float edgeNoiseScale = 180;
        [Min(5)] public float edgeDetailScale = 45;
        [Range(0, .5f)] public float edgeDensityVariation = .24f;
        [Header("Lectura del relieve (solo representación)")]
        [Range(0, 60)] public float slopeThinningStart = 32;
        [Range(1, 90)] public float slopeThinningEnd = 45;
        [Range(0, .5f)] public float clearingStrength;
        [Min(50)] public float clearingScale = 480;
        [Header("Detalle según tamaño en pantalla")]
        [Min(1000)] public float forestViewDistance = 50000;
        [Range(.01f, .5f)] public float lodNearTransition = .10f;
        [Range(.001f, .1f)] public float lodMiddleTransition = .025f;
        [Range(.0001f, .02f)] public float lodCullTransition = .001f;
        [Range(.01f, .3f)] public float middleMeshCell = .06f;
        [Range(.01f, .4f)] public float farMeshCell = .14f;

        public float Spacing(float density) => Mathf.Clamp(densityToSpacing.Evaluate(density), minimumSpacing, maximumSpacing);

        public float VisualDensity(float canonical, Vector2 worldPoint)
        {
            // Erode only the transition zone; dense cores and canonical forest/resource boundaries remain intact.
            float edge = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, .9f, canonical));
            float broad = Mathf.PerlinNoise(worldPoint.x / Mathf.Max(10, edgeNoiseScale) + seed * .37f,
                worldPoint.y / Mathf.Max(10, edgeNoiseScale) + seed * .19f);
            float detail = Mathf.PerlinNoise(worldPoint.x / Mathf.Max(5, edgeDetailScale) + seed * .53f,
                worldPoint.y / Mathf.Max(5, edgeDetailScale) + seed * .71f);
            float clearing = Mathf.PerlinNoise(worldPoint.x / Mathf.Max(50,clearingScale) + seed,
                worldPoint.y / Mathf.Max(50,clearingScale) + 19);
            clearing = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,.78f,clearing));
            return Mathf.Max(0, canonical - edge * edgeDensityVariation * (broad * .7f + detail * .3f)
                - clearingStrength * clearing);
        }

        public void SetDefaults()
        {
            densityToSpacing = new AnimationCurve(new Keyframe(0, 75), new Keyframe(.18f, 65),
                new Keyframe(.35f, 50), new Keyframe(.60f, 32), new Keyframe(.80f, 23), new Keyframe(1, 17));
            for (int i = 0; i < densityToSpacing.length; i++) densityToSpacing.SmoothTangents(i, 0);
        }
    }
}
