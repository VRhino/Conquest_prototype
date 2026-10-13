using UnityEngine;

namespace Conquest.WorldMap
{
    // Presentation-only calibration: economic ticks and resource ownership stay in BronzeAge.
    [RequireComponent(typeof(WorldMapCampaignController))]
    public sealed class WorldMapCalibrationTrial : MonoBehaviour
    {
        public WorldMapCampaignController campaign;
        public Vector3[] baselineRoute;
        public LineRenderer influenceA, influenceB;
        [Range(0, 5)] public int influenceLevel = 2;
        [Range(32, 256)] public int ringSamples = 128;
        public float ringLift = 1.5f;
        float ringRefresh;
        bool previousBackground;
        GUIStyle label;
        public WorldMapScaleContract Contract => campaign.scaleContract;
        public float BaselineDistance => Vector2.Distance(new Vector2(baselineRoute[0].x, baselineRoute[0].z), new Vector2(baselineRoute[baselineRoute.Length-1].x, baselineRoute[baselineRoute.Length-1].z));

        void OnEnable() { if (!Application.isPlaying) return; previousBackground=Application.runInBackground; Application.runInBackground=true; }
        void OnDisable() { if (Application.isPlaying) Application.runInBackground=previousBackground; }
        void Start() { ResetTrial(); SetLevel(influenceLevel); }
        void Update()
        {
            ringRefresh -= Time.unscaledDeltaTime;
            if (ringRefresh <= 0) { RefreshRings(); ringRefresh = .5f; }
        }
        public void ResetTrial()
        {
            campaign.StopTravel(); campaign.demoRoute = (Vector3[])baselineRoute.Clone();
            campaign.segment = 1; campaign.travelled = 0;
            campaign.actor.position = baselineRoute[0] + Vector3.up * campaign.actorLift;
            campaign.follow = true; campaign.focus = campaign.actor.position;
            campaign.streamer.focus = campaign.focus;
            campaign.SetVisibleGroundWidth(Contract.ToUnity(Contract.visibleGroundWidth));
        }
        public void BeginTrial() { ResetTrial(); campaign.moving = true; campaign.travelStatus = "Prueba en curso"; }
        public void SetLevel(int level)
        {
            influenceLevel = Mathf.Clamp(level, 0, 5);
            foreach (var city in new[] { campaign.cityA, campaign.cityB })
            {
                int model = influenceLevel < 2 ? 0 : influenceLevel < 4 ? 1 : 2;
                for (int i = 0; i < city.childCount; i++) city.GetChild(i).gameObject.SetActive(i == model);
            }
            RefreshRings();
        }
        void RefreshRings()
        {
            DrawRing(influenceA, campaign.cityA.position, campaign.cityB.position);
            DrawRing(influenceB, campaign.cityB.position, campaign.cityA.position);
        }
        void DrawRing(LineRenderer line, Vector3 center, Vector3 rival)
        {
            if (!line) return;
            var axis = rival - center; axis.y = 0; float halfDistance = axis.magnitude * .5f; axis.Normalize();
            float radius = Contract.InfluenceUnity(influenceLevel);
            line.positionCount = ringSamples; line.loop = true; line.widthMultiplier = 1;
            for (int i = 0; i < ringSamples; i++)
            {
                float angle = i * Mathf.PI * 2 / ringSamples;
                var offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                // Equal-level rivals divide overlap at the perpendicular bisector.
                float beyond = Vector3.Dot(offset, axis) - halfDistance;
                if (beyond > 0) offset -= axis * beyond;
                var p = center + offset; p.y = campaign.streamer.SurfaceHeight(p) + ringLift; line.SetPosition(i, p);
            }
        }
        void OnGUI()
        {
            if (label == null) label = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14 };
            var panel = new Rect(12, 12, 315, Mathf.Min(Screen.height - 24, 590));
            GUI.color = new Color(.06f, .08f, .10f, .95f); GUI.DrawTexture(panel, Texture2D.whiteTexture); GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(panel.x+12, panel.y+10, panel.width-24, panel.height-20));
            GUILayout.Label("CALIBRACIÓN · FACTOR 12", label);
            GUILayout.Label($"Mundo: {Contract.StrategicWorldSize(campaign.streamer.manifest.worldSize).x:F1} × {Contract.StrategicWorldSize(campaign.streamer.manifest.worldSize).y:F0} estratégicas", label);
            GUILayout.Label($"Velocidad: {campaign.movementSpeed:F1} u/s · {campaign.travelKind}", label);
            GUILayout.BeginHorizontal(); SpeedButton("Ligera", CampaignTravelKind.Light); SpeedButton("Héroe", CampaignTravelKind.SoloHero); SpeedButton("Montada", CampaignTravelKind.Mounted); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); SpeedButton("Caballo", CampaignTravelKind.HorseCaravan); SpeedButton("Camello", CampaignTravelKind.CamelCaravan); SpeedButton("Buey", CampaignTravelKind.Medium); GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); SpeedButton("Pesada", CampaignTravelKind.Heavy); SpeedButton("Fundación vuelta", CampaignTravelKind.ReturningFoundation); GUILayout.EndHorizontal();
            GUILayout.Label($"Vecinas: {BaselineDistance:F0} u · {BaselineDistance/campaign.movementSpeed:F1} s sin obstáculos", label);
            GUILayout.BeginHorizontal(); if (GUILayout.Button("Viajar A → B")) BeginTrial(); if (GUILayout.Button("Reiniciar")) ResetTrial(); GUILayout.EndHorizontal();
            if (GUILayout.Button(campaign.moving ? "Pausar viaje" : "Continuar viaje")) campaign.moving = !campaign.moving;
            GUILayout.Label($"Recorrido: {campaign.travelled:F1} u · {campaign.travelled/campaign.movementSpeed:F1} s de movimiento", label);
            GUILayout.Label($"Ancho medido: {campaign.VisibleGroundWidth():F1} u · cruce {campaign.VisibleGroundWidth()/campaign.movementSpeed:F1} s", label);
            GUILayout.BeginHorizontal(); if (GUILayout.Button("Cámara habitual")) { campaign.follow=true; campaign.focus=campaign.actor.position; campaign.SetVisibleGroundWidth(Contract.ToUnity(Contract.visibleGroundWidth)); }
            if (GUILayout.Button("Ver ambas ciudades")) { campaign.FocusAt((campaign.cityA.position+campaign.cityB.position)*.5f); campaign.SetVisibleGroundWidth(BaselineDistance+Contract.InfluenceUnity(influenceLevel)*2+80); } GUILayout.EndHorizontal();
            GUILayout.Label($"Influencia: {(influenceLevel == 0 ? "Inicial" : "Nivel "+influenceLevel)} · radio {Contract.InfluenceUnity(influenceLevel):F0} u", label);
            GUILayout.BeginHorizontal(); for (int i=0; i<=5; i++) if (GUILayout.Button(i==0 ? "Inicio" : "N"+i)) SetLevel(i); GUILayout.EndHorizontal();
            GUILayout.Label("Anillos de referencia, con recorte entre dos rivales del mismo nivel. No simulan propiedad del servidor.", label);
            GUILayout.Label("WASD: cámara · rueda: zoom · botón derecho: girar", label);
            GUILayout.Label($"Sectores {campaign.streamer.LoadedCount} · pendientes {campaign.streamer.PendingCount}", label);
            if (!string.IsNullOrEmpty(campaign.streamer.Error)) GUILayout.Label(campaign.streamer.Error, label);
            GUILayout.EndArea();
            var e = Event.current; if (panel.Contains(e.mousePosition)) return;
            if (e.type==EventType.ScrollWheel) { campaign.ZoomBy(Mathf.Exp(e.delta.y*.08f)); e.Use(); }
            if (e.type==EventType.MouseDrag && e.button==1) { campaign.yaw+=e.delta.x*.25f; campaign.pitch=Mathf.Clamp(campaign.pitch+e.delta.y*.25f,35,70); e.Use(); }
        }
        void SpeedButton(string caption, CampaignTravelKind kind)
        {
            if (!GUILayout.Button(caption)) return;
            campaign.SetTravelKind(kind); ResetTrial();
        }
    }
}
