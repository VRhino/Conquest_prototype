using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Conquest.SettlementPreview
{
    // Reads the current player projection, not the future battle contract.
    public sealed class SettlementPreview : MonoBehaviour
    {
        public TextAsset source;
        [Tooltip("Optional. Leave empty to select the only settlement in a projection automatically.")]
        public string settlementId = "";
        [Tooltip("Combat scale: 3 local units per street cell become 10.5 Unity units. Hero stays at scale 1.")]
        [Min(0.01f)] public float unitsPerLocalUnit = 3.5f;
        [Tooltip("House_Small_01 is 7.31 units tall; use an 8-unit blockout building.")]
        [Min(1)] public float buildingHeight = 8;
        [Tooltip("Fraction of each authoritative footprint occupied by its building cube. The centre stays unchanged.")]
        [Range(0.5f, 1f)] public float buildingFootprintFill = 0.99f;
        [Min(.1f)] public float wallHeight = 6;
        [Min(.1f)] public float towerHeight = 10;
        [Min(.01f)] public float gateMarkerHeight = .08f;
        [Min(0)] public float groundPadding = 15;
        public Material surfaceMaterial;
        public Camera viewCamera;
        public Transform generated;
        [SerializeField] private string summary;
        private string selection = "Haz clic en un edificio para consultar sus datos.";
        private Vector3 focus;
        private float fitSize = 100;
        private bool angled;
        private readonly Dictionary<Collider, string> buildings = new Dictionary<Collider, string>();
        private readonly Dictionary<string, Color> colors = new Dictionary<string, Color>
        {
            ["centroUrbano"] = new Color(.56f,.29f,.73f),
            ["vivienda"] = new Color(.88f,.88f,.69f),
            ["granja"] = new Color(.94f,.72f,.08f),
            ["lenera"] = new Color(.64f,.26f,.20f),
            ["almacen"] = new Color(.65f,.45f,.22f),
            ["granero"] = new Color(.86f,.61f,.16f),
            ["armeria"] = new Color(.27f,.43f,.64f),
            ["fundicion"] = new Color(.79f,.32f,.12f),
            ["patioDeGremios"] = new Color(.35f,.65f,.68f),
            ["pozo"] = new Color(.22f,.65f,.91f),
            ["corral"] = new Color(.67f,.54f,.31f),
            ["cantera"] = new Color(.48f,.50f,.52f),
            ["curtiduria"] = new Color(.50f,.31f,.18f),
            ["plaza"] = new Color(.72f,.68f,.58f),
            ["granFundicion"] = new Color(.67f,.19f,.10f),
            ["plazaDeArmas"] = new Color(.45f,.48f,.53f),
            ["barracon"] = new Color(.35f,.39f,.30f),
            ["galeriaDeTiro"] = new Color(.36f,.52f,.27f),
            ["mercado"] = new Color(.86f,.50f,.18f),
            ["carpinteria"] = new Color(.58f,.38f,.18f),
            ["puestoMercado"] = new Color(.91f,.62f,.26f),
            ["tallerCarpinteria"] = new Color(.46f,.29f,.13f)
        };

        private void Start() { Rebuild(); }

        [ContextMenu("Reconstruir desde JSON")]
        public void Rebuild()
        {
            var walk = GetComponent<SettlementWalkMode>();
            if (walk && walk.Requested) throw new InvalidOperationException("Vuelve al visor antes de reconstruir el suelo.");
            // Validate the complete local geometry before touching the existing preview.
            if (!source || !surfaceMaterial || !viewCamera) throw new InvalidOperationException("Faltan JSON, material o cámara.");
            if (unitsPerLocalUnit <= 0 || float.IsNaN(unitsPerLocalUnit) || float.IsInfinity(unitsPerLocalUnit)) throw new InvalidOperationException("Escala inválida.");
            if (buildingFootprintFill <= 0 || buildingFootprintFill > 1 || float.IsNaN(buildingFootprintFill)) throw new InvalidOperationException("Ocupación de huella inválida.");
            if (generated && generated.parent != transform) throw new InvalidOperationException("El contenido generado debe pertenecer a este visor.");
            var json = JObject.Parse(source.text);
            var settlements = (json["asentamientos"] as JArray)?.OfType<JObject>().ToList()
                ?? throw new InvalidOperationException("La proyección no contiene el array asentamientos.");
            if (settlements.Count == 0) throw new InvalidOperationException("La proyección no contiene asentamientos.");
            JObject city;
            if (string.IsNullOrWhiteSpace(settlementId))
            {
                if (settlements.Count != 1) throw new InvalidOperationException("La proyección contiene varios asentamientos; indica settlementId en el Inspector.");
                city = settlements[0];
            }
            else
            {
                city = settlements.FirstOrDefault(a => (string)a["id"] == settlementId);
                if (city == null) throw new InvalidOperationException("No existe settlementId '" + settlementId + "' en la proyección. Disponibles: " + string.Join(", ", settlements.Select(a => (string)a["id"])));
            }
            string resolvedSettlementId = (string)city["id"];
            if (string.IsNullOrWhiteSpace(resolvedSettlementId)) throw new InvalidOperationException("El asentamiento seleccionado no tiene id.");
            var layout = json["trazadoPorAsentamiento"]?[resolvedSettlementId] as JObject;
            if (layout == null) throw new InvalidOperationException("No hay trazado para " + resolvedSettlementId + ".");
            var local = ((JArray)city["edificios"]).OfType<JObject>().Where(e => (string)e["ambito"] != "mapa").ToList();
            if (local.Count == 0) throw new InvalidOperationException("Asentamiento sin edificios locales.");
            var footprints = layout["huellas"] as JObject ?? throw new InvalidOperationException("El trazado no contiene huellas.");
            var streets = layout["calles"] as JArray ?? throw new InvalidOperationException("El trazado no contiene calles.");
            var roads = layout["caminos"] as JArray ?? throw new InvalidOperationException("El trazado no contiene caminos.");
            var enclosures = layout["murallas"] as JArray ?? new JArray();
            var ids = new HashSet<string>();
            foreach (var e in local)
            {
                string id = (string)e["id"];
                if (string.IsNullOrEmpty(id) || !ids.Add(id)) throw new InvalidOperationException("ID de edificio ausente/duplicado.");
                Rect r = ReadRect(footprints?[id]);
                if (Mathf.Abs(r.center.x - (float)e["posicion"]["x"]) > .001f || Mathf.Abs(r.center.y - (float)e["posicion"]["y"]) > .001f)
                    throw new InvalidOperationException("Centro y huella no coinciden: " + id);
            }
            foreach (var item in streets.Concat(roads)) ReadRect(item);
            foreach (var enclosure in enclosures.OfType<JObject>())
                foreach (string key in new[] { "muro", "puertas", "torres" })
                    foreach (var item in enclosure[key] as JArray ?? new JArray()) ReadRect(item);

            buildings.Clear();
            if (generated)
            {
                generated.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(generated.gameObject); else DestroyImmediate(generated.gameObject);
            }
            generated = new GameObject("Generated settlement").transform;
            generated.SetParent(transform, false);
            var bounds = ReadRect(footprints[(string)local[0]["id"]]);
            foreach (var p in footprints.Properties()) bounds = Union(bounds, ReadRect(p.Value));
            foreach (var r in streets.Concat(roads)) bounds = Union(bounds, ReadRect(r));
            foreach (var enclosure in enclosures.OfType<JObject>())
                foreach (string key in new[] { "muro", "puertas", "torres" })
                    foreach (var r in enclosure[key] as JArray ?? new JArray()) bounds = Union(bounds, ReadRect(r));
            float pad = groundPadding;
            Box("Suelo", new Rect(bounds.xMin-pad, bounds.yMin-pad, bounds.width+pad*2, bounds.height+pad*2), .2f, -.2f, new Color(.25f,.38f,.20f), generated, true);
            // Physical outer limits prevent walking off the finite preview floor. No navigation mesh.
            var limits = Group("Limites fisicos");
            var floor = new Rect(bounds.xMin-pad, bounds.yMin-pad, bounds.width+pad*2, bounds.height+pad*2);
            foreach (var edge in new[] {
                new Rect(floor.xMin-1,floor.yMin-1,1,floor.height+2), new Rect(floor.xMax,floor.yMin-1,1,floor.height+2),
                new Rect(floor.xMin,floor.yMin-1,floor.width,1), new Rect(floor.xMin,floor.yMax,floor.width,1) })
                Box("Limite del visor", edge, 5, 0, Color.clear, limits, true).GetComponent<Renderer>().enabled = false;
            foreach (string key in new[] { "caminos", "calles" })
            {
                var group = Group(key);
                int i = 0;
                foreach (var r in (JArray)layout[key])
                    Box(key + " " + i++, ReadRect(r), .035f, .005f, key == "calles" ? new Color(.62f,.58f,.44f) : new Color(.44f,.37f,.24f), group, false);
            }
            int wallCount = 0, gateCount = 0, towerCount = 0, enclosureIndex = 0;
            var wallGroup = Group("Murallas");
            foreach (var enclosure in enclosures.OfType<JObject>())
            {
                var enclosureGroup = Group("Recinto " + enclosureIndex++, wallGroup);
                foreach (var r in enclosure["muro"] as JArray ?? new JArray())
                    Box("Muro " + wallCount++, ReadRect(r), wallHeight, .05f, new Color(.43f,.44f,.46f), enclosureGroup, true);
                foreach (var r in enclosure["puertas"] as JArray ?? new JArray())
                    Box("Puerta " + gateCount++, ReadRect(r), gateMarkerHeight, .04f, new Color(.39f,.22f,.10f), enclosureGroup, false);
                foreach (var r in enclosure["torres"] as JArray ?? new JArray())
                    Box("Torre " + towerCount++, ReadRect(r), towerHeight, .05f, new Color(.32f,.34f,.37f), enclosureGroup, true);
            }
            var buildingGroup = Group("Edificios");
            foreach (var e in local)
            {
                string id = (string)e["id"], type = (string)e["tipo"], state = (string)e["estado"];
                Rect r = ReadRect(footprints[id]);
                Color color = colors.TryGetValue(type, out var c) ? c : Color.gray;
                float height = type == "centroUrbano" ? buildingHeight * 2 : type == "granja" ? .5f : buildingHeight;
                if (state != "activo") { height *= .35f; color = Color.Lerp(color, Color.gray, .5f); }
                var cubeRect = new Rect(r.center - r.size * buildingFootprintFill * .5f, r.size * buildingFootprintFill);
                var cube = Box(type + " | " + id, cubeRect, height, .05f, color, buildingGroup, true);
                buildings[cube.GetComponent<Collider>()] = type + " · " + state + "\n" + id + "\nHuella: " + r.width + " × " + r.height + " unidades locales\nNivel interno: " + (e["nivelInterno"]?.ToString() ?? "—");
            }
            int external = ((JArray)city["edificios"]).Count - local.Count;
            summary = resolvedSettlementId + " · vista local · versión " + json["version"] + "\n" + local.Count + " edificios · " + streets.Count + " tramos de calle · " + roads.Count + " de camino\n" + wallCount + " muros · " + gateCount + " puertas · " + towerCount + " torres · " + external + " edificios de mapa excluidos.";
            focus = transform.TransformPoint(new Vector3(bounds.center.x * unitsPerLocalUnit, 0, -bounds.center.y * unitsPerLocalUnit));
            fitSize = Mathf.Max(bounds.height, bounds.width / Mathf.Max(.1f, viewCamera.aspect)) * unitsPerLocalUnit * .6f + groundPadding;
            Frame();
        }

        public static Rect ReadRect(JToken token)
        {
            if (token == null) throw new InvalidOperationException("Falta una huella.");
            var values = new float[4]; int i = 0;
            foreach (string key in new[] { "x", "y", "ancho", "alto" })
            {
                if (token[key] == null || (token[key].Type != JTokenType.Integer && token[key].Type != JTokenType.Float)) throw new InvalidOperationException("Rectángulo inválido: " + key);
                values[i] = (float)token[key];
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) throw new InvalidOperationException("Coordenada no finita.");
                i++;
            }
            if (values[2] <= 0 || values[3] <= 0) throw new InvalidOperationException("Huella sin superficie.");
            return new Rect(values[0], values[1], values[2], values[3]);
        }
        private static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin,b.xMin), Mathf.Min(a.yMin,b.yMin), Mathf.Max(a.xMax,b.xMax), Mathf.Max(a.yMax,b.yMax));
        private Transform Group(string title, Transform parent = null) { var go = new GameObject(title); go.transform.SetParent(parent ? parent : generated, false); return go.transform; }
        private GameObject Box(string title, Rect r, float height, float bottom, Color color, Transform parent, bool selectable)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = title;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(r.center.x * unitsPerLocalUnit, bottom + height/2, -r.center.y * unitsPerLocalUnit);
            go.transform.localScale = new Vector3(r.width * unitsPerLocalUnit, height, r.height * unitsPerLocalUnit);
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = surfaceMaterial;
            var props = new MaterialPropertyBlock(); props.SetColor("_BaseColor", color); props.SetColor("_Color", color); renderer.SetPropertyBlock(props);
            // Property blocks are runtime/editor preview state, rebuilt on Start or via the editor menu.
            go.GetComponent<Collider>().enabled = selectable;
            return go;
        }
        public void Frame()
        {
            viewCamera.orthographic = true; viewCamera.orthographicSize = fitSize;
            viewCamera.transform.rotation = Quaternion.Euler(angled ? 55 : 90, 0, 0);
            viewCamera.transform.position = focus - viewCamera.transform.forward * 400;
            viewCamera.farClipPlane = 1500;
        }
        private void Update()
        {
            var walk = GetComponent<SettlementWalkMode>();
            if (walk && walk.Requested) return;
            if (!viewCamera || Mouse.current == null) return;
            var mouse = Mouse.current;
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) Frame();
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) { angled = !angled; Frame(); }
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0) viewCamera.orthographicSize = Mathf.Clamp(viewCamera.orthographicSize * Mathf.Exp(-scroll * .001f), 5, fitSize * 3);
            if (mouse.middleButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue() * (viewCamera.orthographicSize * 2 / Mathf.Max(1, Screen.height));
                viewCamera.transform.position += new Vector3(-d.x, 0, -d.y);
            }
            Vector2 pointer = mouse.position.ReadValue();
            bool overPanel = new Rect(12,12,350,390).Contains(new Vector2(pointer.x, Screen.height-pointer.y));
            if (mouse.leftButton.wasPressedThisFrame && !overPanel && Physics.Raycast(viewCamera.ScreenPointToRay(pointer), out var hit, 1500))
                if (buildings.TryGetValue(hit.collider, out var info)) selection = info;
        }
        private void OnGUI()
        {
            var walk = GetComponent<SettlementWalkMode>();
            if (walk && walk.Requested)
            {
                GUI.Box(new Rect(12,12,650,55), "RECORRER ASENTAMIENTO\n" + walk.Status);
                return;
            }
            GUILayout.BeginArea(new Rect(12,12,350,390), GUI.skin.box);
            GUILayout.Label("BRONZEAGE · ASENTAMIENTO 3D"); GUILayout.Label(summary);
            GUILayout.Space(8); GUILayout.Label("Rueda: zoom · botón central: mover\nF: encuadrar · Tab: cenital / inclinada");
            if (GUILayout.Button("Encuadrar ciudad")) Frame();
            if (GUILayout.Button("Cambiar perspectiva")) { angled = !angled; Frame(); }
            if (walk && GUILayout.Button("Recorrer asentamiento")) walk.Enter();
            if (walk && !string.IsNullOrEmpty(walk.Status)) GUILayout.Label(walk.Status);
            GUILayout.Space(8); GUILayout.Label(selection);
            GUILayout.EndArea();
        }
    }
}
