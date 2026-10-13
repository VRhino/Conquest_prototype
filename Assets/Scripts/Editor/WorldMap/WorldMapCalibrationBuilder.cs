using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Conquest.WorldMap.Editor
{
    public static class WorldMapCalibrationBuilder
    {
        public const string MainScene = "Assets/Scenes/GameScenes/WorldMapCampaign.unity";
        public const string Folder = "Assets/Scenes/WorldMapTools/Experiments/CalibrationFactor12";
        public const string TrialScene = Folder + "/CalibrationFactor12.unity";
        public const string ContractPath = "Assets/WorldMap/Campaign/ScaleContract.asset";
        static readonly Dictionary<string, TerrainData> heights = new Dictionary<string, TerrainData>();

        [MenuItem("BronzeAge/World map/Prepare factor 12 calibration")]
        public static void Build()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the scene and exit Play before preparing calibration.");
            Ensure(Folder);
            var contract = AssetDatabase.LoadAssetAtPath<WorldMapScaleContract>(ContractPath);
            if (!contract) { contract = ScriptableObject.CreateInstance<WorldMapScaleContract>(); AssetDatabase.CreateAsset(contract, ContractPath); }
            var scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            var c = UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();
            if (!c || !c.navigation) throw new InvalidOperationException("Campaign/navigation missing.");
            c.scaleContract = contract; c.SetTravelKind(CampaignTravelKind.Light);
            c.minZoom = 5; c.maxZoom = 2500; c.visibleWidthTarget = contract.ToUnity(contract.visibleGroundWidth);
            c.follow = true; c.moving = false; c.focus = c.demoRoute[0]; c.showDiagnostics = false;
            ScaleSymbols(c, contract); c.ApplyCamera();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            // Copy only the scene; both scenes reuse the existing geography and sector assets.
            EditorSceneManager.SaveScene(scene, TrialScene, true);
            scene = EditorSceneManager.OpenScene(TrialScene, OpenSceneMode.Single);
            c = UnityEngine.Object.FindAnyObjectByType<WorldMapCampaignController>();
            var route = FindFixture(c, contract.ToUnity(contract.settlementSeparation));
            var direction = route[1] - route[0]; direction.y=0; direction.Normalize();
            var offset = Vector3.Cross(Vector3.up, direction) * (contract.ToUnity(contract.citySymbolRadius) + 4);
            c.cityA.position = route[0] - offset; c.cityB.position = route[1] - offset;
            c.cityA.position = Ground(c, c.cityA.position); c.cityB.position = Ground(c, c.cityB.position);
            foreach (var city in new[] { c.cityA, c.cityB })
            {
                var target = city.GetComponent<WorldMapSettlementTarget>();
                if (target) target.entrance = city==c.cityA ? route[0] : route[1];
            }
            c.demoRoute = route; c.actor.position = route[0]+Vector3.up*c.actorLift; c.focus=c.actor.position;
            // Place the road horizontally across the camera's ground slice.
            c.yaw = Mathf.Atan2(-direction.z, direction.x)*Mathf.Rad2Deg; c.pitch=48;
            var hud=c.GetComponent<WorldMapCampaignHud>(); if(hud)hud.enabled=false;
            foreach(var root in scene.GetRootGameObjects())if(root.name=="Campaign scene bridge")root.SetActive(false);
            var trial=c.gameObject.AddComponent<WorldMapCalibrationTrial>();trial.campaign=c;trial.baselineRoute=(Vector3[])route.Clone();
            trial.influenceA=Ring("Influence A",c.transform,new Color(.96f,.72f,.32f));
            trial.influenceB=Ring("Influence B",c.transform,new Color(.40f,.78f,.98f));
            trial.SetLevel(2);trial.ResetTrial();c.ApplyCamera();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            heights.Clear();
            Debug.Log($"Factor12 ready: {Vector3.Distance(route[0],route[1]):F2} total / {trial.BaselineDistance:F2} horizontal units, light {c.movementSpeed} u/s, width {c.VisibleGroundWidth():F2}.");
        }
        static Vector3[] FindFixture(WorldMapCampaignController c,float distance)
        {
            for(int ring=0;ring<12;ring++)for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8;var start=c.demoRoute[0]+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*ring*80;
                var end=start+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*distance;
                if(!c.navigation.SegmentClear(start,end))continue;
                bool dry=true;float min=float.MaxValue,max=float.MinValue;
                for(int sample=0;sample<=36;sample++){var p=Ground(c,Vector3.Lerp(start,end,sample/36f));min=Mathf.Min(min,p.y);max=Mathf.Max(max,p.y);if(p.y<c.seaHeight+8){dry=false;break;}}
                if(dry && max-min<35)return new[]{Ground(c,start),Ground(c,end)};
            }
            throw new InvalidOperationException("No dry 360-unit route found near the existing fixture.");
        }
        static Vector3 Ground(WorldMapCampaignController c,Vector3 p)
        {
            foreach(var sector in c.streamer.manifest.sectors)
            {
                if(p.x<sector.origin.x||p.z<sector.origin.z||p.x>=sector.origin.x+sector.size.x||p.z>=sector.origin.z+sector.size.y)continue;
                string path="Assets/WorldMap/Campaign/Sectors/"+sector.id+".prefab";
                if(!heights.TryGetValue(path,out var data)){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);data=prefab.GetComponentInChildren<Terrain>(true).terrainData;heights.Add(path,data);}
                p.y=sector.origin.y+data.GetInterpolatedHeight((p.x-sector.origin.x)/sector.size.x,(p.z-sector.origin.z)/sector.size.y);return p;
            }
            throw new InvalidOperationException("Fixture outside world.");
        }
        static void ScaleSymbols(WorldMapCampaignController c,WorldMapScaleContract contract)
        {
            foreach(var city in new[]{c.cityA,c.cityB})
            {
                var b=LocalBounds(city);float radius=Mathf.Sqrt(Mathf.Pow(Mathf.Max(Mathf.Abs(b.min.x),Mathf.Abs(b.max.x)),2)+Mathf.Pow(Mathf.Max(Mathf.Abs(b.min.z),Mathf.Abs(b.max.z)),2));
                if(radius<=0)throw new InvalidOperationException("City has no geometry.");city.localScale=Vector3.one*(contract.ToUnity(contract.citySymbolRadius)/radius);
            }
            if(c.actor.GetComponentInChildren<Animator>())c.actor.localScale=Vector3.one*contract.heroSymbolScale;
            else{var actorBounds=LocalBounds(c.actor);c.actor.localScale=Vector3.one*(contract.armySymbolHeightUnity/actorBounds.size.y);}
        }
        static Bounds LocalBounds(Transform root)
        {
            bool found=false;var result=new Bounds();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.sharedMesh)continue;var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++){var p=root.InverseTransformPoint(filter.transform.TransformPoint(new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z)));if(!found){result=new Bounds(p,Vector3.zero);found=true;}else result.Encapsulate(p);}
            }
            if(!found)throw new InvalidOperationException("Symbol has no mesh bounds.");return result;
        }
        static LineRenderer Ring(string name,Transform parent,Color color)
        {
            string path=Folder+"/"+name.Replace(" ","")+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(material,path);}
            var go=new GameObject(name);go.transform.SetParent(parent);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }
        static void Ensure(string path){if(AssetDatabase.IsValidFolder(path))return;Ensure(path.Substring(0,path.LastIndexOf('/')));AssetDatabase.CreateFolder(path.Substring(0,path.LastIndexOf('/')),path.Substring(path.LastIndexOf('/')+1));}
    }
}
