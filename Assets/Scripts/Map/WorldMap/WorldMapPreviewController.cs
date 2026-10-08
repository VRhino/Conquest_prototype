using UnityEngine;
using BronzeAge.RegionalMapTool;

namespace Conquest.WorldMap
{
    // Presentation-only inspection. This component neither creates nor updates gameplay entities.
    public sealed class WorldMapPreviewController : MonoBehaviour
    {
        public Camera mapCamera;
        public Transform worldRoot;
        public WorldMapArtSettings artProfile;
        public Vector2 worldSize = new Vector2(32000, 24000);
        [Header("Vista inicial de campaña")]
        public Vector2 campaignFocusNormalized = new Vector2(.53f,.19f);
        [Min(300)] public float campaignViewSize = 2600;
        [Range(20,85)] public float campaignPitch = 43;
        public float campaignYaw = -20;
        [SerializeField] private float minimumViewSize = 180;
        [SerializeField] private float orbitSensitivity = .25f;
        [SerializeField] private float minimumPitch = 20;
        [SerializeField] private float maximumPitch = 85;
        [SerializeField] private float terrainClearance = 30;
        [SerializeField] private float detailViewSize = 700;
        [SerializeField] private float zoomSensitivity = .08f;
        [SerializeField] private float pickRadiusPixels = 16;
        private Vector3 focus;
        private float viewSize;
        private bool topDown;
        private float yaw;
        private float pitch = 55;
        private int dragButton;
        private Terrain[] terrains;
        private bool resourcesVisible = true;
        private string resourceFilter = "Todos";
        private string selection = "Selecciona un recurso para consultar su identidad.";
        private BronzeAgeRegionalMarker[] markers;
        private bool dragging;
        private float dragDistance;
        private GUIStyle titleStyle, textStyle, iconStyle;
        private readonly string[] filters = { "Todos", "piedra", "cobre", "estano", "oro", "ganaderia" };

        public void FitWorld()
        {
            if (!mapCamera || !worldRoot) return;
            if (terrains == null) terrains = worldRoot.GetComponentsInChildren<Terrain>();
            focus = new Vector3(worldSize.x * .5f, 0, worldSize.y * .5f);
            yaw = 0; pitch = 55;
            // Fit a bounding sphere, so the inclined perspective also includes the near corners.
            float halfFov = mapCamera.fieldOfView * .5f * Mathf.Deg2Rad;
            float limitingHalfFov = Mathf.Min(halfFov, Mathf.Atan(Mathf.Tan(halfFov) * mapCamera.aspect));
            viewSize = worldSize.magnitude * .55f / Mathf.Sin(limitingHalfFov) * Mathf.Tan(halfFov);
            ApplyCamera();
        }

        private void Start()
        {
            markers = worldRoot.GetComponentsInChildren<BronzeAgeRegionalMarker>(true);
            FocusCampaign();
        }

        public void FocusCampaign()
        {
            FocusArea(campaignFocusNormalized,campaignViewSize,campaignYaw,campaignPitch);
        }

        public void FocusArea(Vector2 normalized,float halfViewSize,float viewYaw,float viewPitch)
        {
            if (!mapCamera || !worldRoot) return;
            if (terrains == null) terrains = worldRoot.GetComponentsInChildren<Terrain>();
            focus = new Vector3(worldSize.x * normalized.x,0,worldSize.y * normalized.y);
            yaw = viewYaw; pitch = Mathf.Clamp(viewPitch,minimumPitch,maximumPitch);
            viewSize = Mathf.Max(minimumViewSize,halfViewSize); topDown = false;
            ApplyCamera();
        }

        private void ApplyCamera()
        {
            if (!mapCamera) return;
            focus.x = Mathf.Clamp(focus.x, 0, worldSize.x);
            focus.z = Mathf.Clamp(focus.z, 0, worldSize.y);
            focus.y = SurfaceHeight(focus);
            mapCamera.orthographic = topDown;
            mapCamera.orthographicSize = viewSize;
            mapCamera.transform.rotation = Quaternion.Euler(topDown ? 90 : pitch, yaw, 0);
            float distance = viewSize / Mathf.Tan(mapCamera.fieldOfView * .5f * Mathf.Deg2Rad);
            var position = focus - mapCamera.transform.forward * distance;
            position.y = Mathf.Max(position.y, SurfaceHeight(position) + terrainClearance);
            mapCamera.transform.position = position;
            mapCamera.farClipPlane = distance + worldSize.magnitude + 2000;
            mapCamera.nearClipPlane = Mathf.Clamp(distance * .004f, 2, 100);
        }

        private float SurfaceHeight(Vector3 point)
        {
            float height = 375; // Regional package sea elevation, in metres.
            if (terrains != null) foreach (var terrain in terrains) {
                var origin = terrain.transform.position;
                var size = terrain.terrainData.size;
                if (point.x >= origin.x && point.z >= origin.z && point.x <= origin.x + size.x && point.z <= origin.z + size.z)
                    height = Mathf.Max(height, terrain.SampleHeight(point) + origin.y);
            }
            return height;
        }

        private void ApplyFilter()
        {
            foreach (var marker in markers)
                marker.gameObject.SetActive(resourcesVisible && (resourceFilter == "Todos" || marker.resourceType == resourceFilter));
        }

        private void OnGUI()
        {
            if (!mapCamera || markers == null) return;
            if(titleStyle==null) {
                titleStyle=new GUIStyle(GUI.skin.label){fontSize=21,fontStyle=FontStyle.Bold};titleStyle.normal.textColor=new Color(.85f,.73f,.48f);
                textStyle=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=12};textStyle.normal.textColor=new Color(.80f,.79f,.69f);
                iconStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=21,fontStyle=FontStyle.Bold};
            }
            DrawResourceSymbols();
            var panel = new Rect(20, 20, 300, 290);
            GUI.color=new Color(.09f,.12f,.12f,.95f);GUI.DrawTexture(panel,Texture2D.whiteTexture);
            GUI.color=new Color(.67f,.52f,.28f);GUI.DrawTexture(new Rect(20,20,300,3),Texture2D.whiteTexture);GUI.color=Color.white;
            GUILayout.BeginArea(new Rect(34,32,272,268));
            GUILayout.Label("BRONZE AGE",titleStyle);
            GUILayout.Label("ATLAS DE CAMPAÑA  /  MEDITERRÁNEO",textStyle);
            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Mundo")) FitWorld();
            if (GUILayout.Button("Campaña")) FocusCampaign();
            if (GUILayout.Button(topDown ? "3D" : "Cenital")) { topDown = !topDown; ApplyCamera(); }
            GUILayout.EndHorizontal();
            bool show = GUILayout.Toggle(resourcesVisible, "Mostrar recursos");
            if (show != resourcesVisible) { resourcesVisible = show; ApplyFilter(); }
            int current = System.Array.IndexOf(filters, resourceFilter);
            int chosen = GUILayout.SelectionGrid(current, filters, 3);
            if (chosen != current) { resourceFilter = filters[chosen]; ApplyFilter(); }
            GUILayout.Space(10);
            GUILayout.Label(selection,textStyle);
            GUILayout.EndArea();
            GUI.Label(new Rect(24,Screen.height-30,Screen.width-48,24),"ARRASTRAR  desplazar     ·     DERECHO  orbitar     ·     RUEDA  acercar     ·     DOBLE CLIC  inspeccionar terreno",textStyle);

            var e = Event.current;
            if (e.type == EventType.MouseUp) {
                if (dragging && dragButton == 0 && dragDistance < 4 && !panel.Contains(e.mousePosition)) Pick(e.mousePosition);
                dragging = false;
            }
            if (panel.Contains(e.mousePosition)) return;
            if (e.type == EventType.ScrollWheel) {
                viewSize = Mathf.Clamp(viewSize * Mathf.Exp(e.delta.y * zoomSensitivity), minimumViewSize, Mathf.Max(worldSize.x, worldSize.y));
                ApplyCamera(); e.Use();
            }
            if (e.type == EventType.MouseDown) {
                dragging = true; dragDistance = 0; dragButton = e.button;
                if (e.button == 0 && e.clickCount == 2) {
                    var ray = mapCamera.ScreenPointToRay(new Vector3(e.mousePosition.x, Screen.height - e.mousePosition.y, 0));
                    if (Physics.Raycast(ray, out var hit, mapCamera.farClipPlane) && hit.collider is TerrainCollider) {
                        focus = hit.point; viewSize = detailViewSize; ApplyCamera();
                    }
                }
            }
            if (e.type == EventType.MouseDrag && dragging) {
                dragDistance += e.delta.magnitude;
                if (dragButton == 1) {
                    topDown = false;
                    yaw += e.delta.x * orbitSensitivity;
                    pitch = Mathf.Clamp(pitch + e.delta.y * orbitSensitivity, minimumPitch, maximumPitch);
                } else {
                    float scale = viewSize * 2 / Mathf.Max(1, Screen.height);
                    var rotation = Quaternion.Euler(0, yaw, 0);
                    focus += rotation * new Vector3(-e.delta.x * scale, 0, e.delta.y * scale / Mathf.Sin((topDown ? 90 : pitch) * Mathf.Deg2Rad));
                }
                ApplyCamera(); e.Use();
            }
        }

        private void DrawResourceSymbols()
        {
            foreach(var marker in markers) {
                var renderer=marker.GetComponent<Renderer>();if(renderer) renderer.enabled=false;
                if(!resourcesVisible || viewSize>6500 || !marker.gameObject.activeInHierarchy) continue;
                var p=mapCamera.WorldToScreenPoint(marker.transform.position);
                if(p.z<=0 || p.x<0 || p.x>Screen.width || p.y<0 || p.y>Screen.height) continue;
                iconStyle.normal.textColor=new Color(.08f,.11f,.10f,.9f);
                var rect=new Rect(p.x-12,Screen.height-p.y-12,24,24);GUI.Label(rect,"◆",iconStyle);
                iconStyle.normal.textColor=marker.resourceType=="estano"?new Color(.68f,.82f,.79f):new Color(.87f,.73f,.45f);
                iconStyle.fontSize=12;GUI.Label(rect,marker.resourceType=="estano"?"Sn":marker.resourceType=="cobre"?"Cu":marker.resourceType=="oro"?"Au":marker.resourceType=="piedra"?"P":"G",iconStyle);iconStyle.fontSize=21;
            }
        }

        private void Pick(Vector2 screenPoint)
        {
            BronzeAgeRegionalMarker best = null;
            float distance = pickRadiusPixels;
            foreach (var marker in markers) {
                if (!marker.gameObject.activeInHierarchy) continue;
                var p = mapCamera.WorldToScreenPoint(marker.transform.position);
                if (p.z <= 0) continue;
                float d = Vector2.Distance(screenPoint, new Vector2(p.x, Screen.height - p.y));
                if (d < distance) { distance = d; best = marker; }
            }
            if (best) selection = best.sourceId + "\n" + best.resourceType + " · región " + best.region +
                "\n" + best.canonicalPosition.x.ToString("F0") + ", " + best.canonicalPosition.z.ToString("F0") + " m" +
                (string.IsNullOrEmpty(best.tinCluster) ? "" : "\nCluster: " + best.tinCluster);
        }
    }
}
