using System.Collections.Generic;
using UnityEngine;

namespace Conquest.WorldMap
{
    // Pure presentation: neither the line nor the destination marker participates in navigation.
    [RequireComponent(typeof(WorldMapCampaignController))]
    public sealed class WorldMapLocalRouteVisual : MonoBehaviour
    {
        public Material lineMaterial;
        [Min(.1f)] public float width = .8f;
        [Min(.1f)] public float lift = 1.2f;
        [Min(1)] public float sampleSpacing = 15;
        [Min(1)] public float destinationRadius = 4;
        [Range(128, 8192)] public int maximumSamples = 4096;
        public Color travelling = new Color(1, .77f, .3f), paused = new Color(.7f, .65f, .5f);
        WorldMapCampaignController campaign;
        LineRenderer route, destination;
        float refresh;
        public int RoutePointCount => route ? route.positionCount : 0;

        void Awake()
        {
            campaign = GetComponent<WorldMapCampaignController>();
            route = CreateLine("Travel route", false);
            destination = CreateLine("Destination", true);
        }
        LineRenderer CreateLine(string name, bool loop)
        {
            var obj = new GameObject(name); obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = lineMaterial ? new Material(lineMaterial) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            line.useWorldSpace = true; line.loop = loop; line.widthMultiplier = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false; line.numCornerVertices = 2; line.numCapVertices = 2;
            return line;
        }
        void OnEnable() { campaign = GetComponent<WorldMapCampaignController>(); campaign.RouteChanged += Rebuild; }
        void OnDisable() { if (campaign) campaign.RouteChanged -= Rebuild; }
        void OnDestroy() { if(route&&route.sharedMaterial)Destroy(route.sharedMaterial);if(destination&&destination.sharedMaterial)Destroy(destination.sharedMaterial); }
        void Update() { refresh -= Time.unscaledDeltaTime; if (refresh <= 0) { Rebuild(); refresh = .15f; } }
        Vector3 Ground(Vector3 p)
        {
            p.y = campaign.streamer.TryHeight(p, out var h) ? h : campaign.navigation.Height(p);
            return p + Vector3.up * lift;
        }
        void Rebuild()
        {
            if (!route || !destination) return;
            bool visible = campaign.HasDestination && campaign.navigation && campaign.demoRoute != null;
            route.enabled = destination.enabled = visible;
            if (!visible) { route.positionCount = 0; return; }
            var color = campaign.moving ? travelling : paused;
            route.startColor = route.endColor = destination.startColor = destination.endColor = color;
            route.sharedMaterial.SetColor("_BaseColor",color);destination.sharedMaterial.SetColor("_BaseColor",color);
            var points = new List<Vector3>(); var previous = campaign.actor.position;
            points.Add(Ground(previous));
            // Limit work for an order across the whole world, without changing the logical route.
            float spacing = Mathf.Max(sampleSpacing, campaign.RemainingDistance / Mathf.Max(1,maximumSamples - campaign.demoRoute.Length - 1));
            for (int i = campaign.segment; i < campaign.demoRoute.Length; i++)
            {
                var next = campaign.demoRoute[i]; float distance = Vector2.Distance(new Vector2(previous.x,previous.z),new Vector2(next.x,next.z));
                int samples = Mathf.Max(1, Mathf.CeilToInt(distance/spacing));
                for (int j=1;j<=samples;j++) points.Add(Ground(Vector3.Lerp(previous,next,(float)j/samples)));
                previous = next;
            }
            route.positionCount = points.Count; route.SetPositions(points.ToArray());
            var goal = campaign.demoRoute[campaign.demoRoute.Length-1]; destination.positionCount = 48;
            for (int i=0;i<48;i++) { float angle=i*Mathf.PI*2/48; destination.SetPosition(i,Ground(goal+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*destinationRadius)); }
        }
    }
}
