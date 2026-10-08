using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using sc.terrain.vegetationspawner;

namespace Conquest.WorldMap.Editor
{
    /// <summary>A separate canonical generation, including hydrology, resources and forest masks.</summary>
    public static class WorldMapMountainTrial
    {
        public const string Folder = WorldMapLandscapeLab.Folder + "/MountainsV9";
        public const string ScenePath = Folder + "/WorldMapMountainsV9.unity";

        [MenuItem("Tools/BronzeAge/Pruebas/Abrir montañas regionales")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        }

        [MenuItem("Tools/BronzeAge/Pruebas/Montañas/Macizos occidentales")]
        public static void ViewMassifs() => Focus(new Vector2(.21f,.245f),1200,-50,35);
        [MenuItem("Tools/BronzeAge/Pruebas/Montañas/Cordillera de Anatolia")]
        public static void ViewAnatolia() => Focus(new Vector2(.475f,.21f),1300,-20,37);
        [MenuItem("Tools/BronzeAge/Pruebas/Montañas/Crestas del Levante")]
        public static void ViewLevant() => Focus(new Vector2(.81f,.57f),1200,65,35);
        [MenuItem("Tools/BronzeAge/Pruebas/Montañas/Escarpes meridionales")]
        public static void ViewEscarpments() => Focus(new Vector2(.69f,.94f),1000,-20,32);
        [MenuItem("Tools/BronzeAge/Pruebas/Montañas/Mesetas orientales")]
        public static void ViewPlateaus() => Focus(new Vector2(.932f,.12f),1100,50,38);
        [MenuItem("Tools/BronzeAge/Pruebas/Montañas/Cumbres occidentales")]
        public static void ViewWesternCrowns() => Focus(new Vector2(.225f,.09f),1100,-40,40);
        static void Focus(Vector2 point,float size,float yaw,float pitch)
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath)throw new InvalidOperationException("Abre WorldMapMountainsV9.");
            var preview=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldMapPreviewController>(true)).Single();
            preview.FocusArea(point,size,yaw,pitch);
        }

        public static void Build(string manifest)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play.");
            if(File.Exists(ScenePath)) throw new InvalidOperationException("La prueba ya existe; no se sobrescribe.");
            if(!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(WorldMapLandscapeLab.Folder,"MountainsV9");
            var oldScene=SceneManager.GetActiveScene();
            WorldMapPreviewBuilder.Build(manifest,true,ScenePath);
            var scene=SceneManager.GetSceneByPath(ScenePath); SceneManager.SetActiveScene(scene);
            var preview=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldMapPreviewController>(true)).Single();
            preview.mapCamera.name="Mountain Campaign Camera";
            preview.campaignFocusNormalized=new Vector2(.475f,.21f);
            preview.campaignViewSize=1300;preview.campaignPitch=37;
            var terrains=preview.worldRoot.GetComponentsInChildren<Terrain>();
            // Every editable surface belongs to this experiment. Keep earlier accepted soil/forest trial intact.
            AssetDatabase.StartAssetEditing();
            try { foreach(var t in terrains)
            {
                var original=AssetDatabase.GetAssetPath(t.terrainData);
                var data=UnityEngine.Object.Instantiate(t.terrainData);
                AssetDatabase.CreateAsset(data,Folder+"/"+t.name+".asset");
                File.Copy(Path.Combine(Path.GetDirectoryName(original),t.name+".layers.bytes"),Folder+"/"+t.name+".layers.bytes");
                t.terrainData=data; t.GetComponent<TerrainCollider>().terrainData=data;
                var mat=UnityEngine.Object.Instantiate(t.materialTemplate);
                AssetDatabase.CreateAsset(mat,Folder+"/"+t.name+".mat"); t.materialTemplate=mat;
                t.heightmapPixelError=2;
            } } finally { AssetDatabase.StopAssetEditing(); }
            var ground=UnityEngine.Object.Instantiate(WorldMapRegionalGroundTrial.Profile());
            ground.mountainMassTreatment=new Vector4(.88f,260,.42f,.10f);
            ground.mountainMassColor=new Color(.49f,.47f,.41f);
            ground.mountainDetail=new Vector4(.8f,.16f,.13f,.12f);
            ground.mountainDetailArea=new Vector4(20700,5600,8500,2600);
            ground.surfaces[2].normalStrength=.025f;ground.surfaces[2].tileMetres=140;
            AssetDatabase.CreateAsset(ground,Folder+"/GroundProfile.asset");
            WorldMapRegionalGroundTrial.Apply(scene,ground,Folder,Folder+"/RegionalGround",Folder);
            var art=UnityEngine.Object.Instantiate(preview.artProfile);
            art.mountainFaceting=1;
            AssetDatabase.CreateAsset(art,Folder+"/CampaignProfile.asset");preview.artProfile=art;
            var synty=UnityEngine.Object.Instantiate(WorldMapSyntyBaker.Settings());
            synty.rockProbability=.008f; synty.cliffProbability=.04f;
            synty.cliffHeight=90; synty.cliffEmbed=.5f; synty.cliffSlope=32; synty.rockSize=18;
            synty.anchorCliffsToFootSlope=true;
            AssetDatabase.CreateAsset(synty,Folder+"/MountainSyntySettings.asset");
            BakeRockModels(preview,terrains,synty);
            GenerateForest(preview,terrains,synty);
            WorldMapForestOverviewBaker.Install(preview,Folder);
            ApplyRockStyle();
            preview.FocusCampaign();
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Folder+"/Ready.txt","Worldgen regional-16\nSource="+manifest+"\nTerrain="+terrains.Length+"\nTrees="+terrains.Sum(t=>t.terrainData.treeInstanceCount));
            // Prevent overlapping terrains in captures. These temporary comparison changes are not saved.
            if(oldScene.IsValid() && oldScene.isLoaded)
                foreach(var root in oldScene.GetRootGameObjects()) root.SetActive(false);
            Selection.activeGameObject=preview.gameObject;
            Debug.Log("MOUNTAIN_TRIAL_READY: "+ScenePath);
        }

        [MenuItem("Tools/BronzeAge/Pruebas/Actualizar modelos de montaña")]
        public static void RefreshRockModels()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath)throw new InvalidOperationException("Abre WorldMapMountainsV9.");
            var preview=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WorldMapPreviewController>(true)).Single();
            var terrains=preview.worldRoot.GetComponentsInChildren<Terrain>();
            if(terrains.Any(t=>!AssetDatabase.GetAssetPath(t.terrainData).StartsWith(Folder+"/")))throw new InvalidOperationException("TerrainData compartido.");
            BakeRockModels(preview,terrains,AssetDatabase.LoadAssetAtPath<WorldMapSyntySettings>(Folder+"/MountainSyntySettings.asset"));
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        }

        static void BakeRockModels(WorldMapPreviewController preview,Terrain[] terrains,WorldMapSyntySettings settings)
        {
            WorldMapSyntyBaker.Apply(preview,terrains,true,settings);
            var renderer=preview.worldRoot.GetComponent<WorldMapSyntyRenderer>();renderer.artProfile=preview.artProfile;
            var next=UnityEngine.Object.Instantiate(renderer.instances);
            next.batches=next.batches.Where(b=>b.always).ToArray();
            next.overviewBatches=next.overviewBatches.Where(b=>b.always).ToArray();next.trees=0;
            var owned=AssetDatabase.LoadAssetAtPath<WorldMapSyntyInstances>(Folder+"/MountainSurroundings.asset");
            if(owned){EditorUtility.CopySerialized(next,owned);UnityEngine.Object.DestroyImmediate(next);}
            else {owned=next;AssetDatabase.CreateAsset(owned,Folder+"/MountainSurroundings.asset");}
            EditorUtility.SetDirty(owned);renderer.instances=owned;
        }

        static void GenerateForest(WorldMapPreviewController preview,Terrain[] terrains,WorldMapSyntySettings settings)
        {
            var profile=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<WorldMapForestTrialProfile>(WorldMapVegetationTrial.ProfilePath));
            if(!profile) throw new InvalidOperationException("Falta el perfil forestal aprobado.");
            profile.scaleRange=new Vector2(.68f,.92f);
            profile.slopeThinningStart=12;profile.slopeThinningEnd=30;
            profile.clearingStrength=.26f;profile.clearingScale=480;
            AssetDatabase.CreateAsset(profile,Folder+"/ForestProfile.asset");
            var spawner=preview.worldRoot.gameObject.AddComponent<VegetationSpawner>();
            spawner.seed=profile.seed;spawner.autoRespawnTrees=false;spawner.terrains=terrains.ToList();
            spawner.waterHeight=settings.seaElevation+4;spawner.visibleTerrainsOnly=false;
            spawner.treeTypes.Clear();spawner.grassPrefabs.Clear();
            foreach(var family in new[]{settings.broadleaf,settings.riverside,settings.mountain})
            {
                var type=SpawnerBase.TreeType.New();type.probability=100;type.distance=profile.Spacing(1);
                type.scaleRange=profile.scaleRange;type.slopeRange=new Vector2(0,settings.maximumForestSlope);
                type.heightRange=new Vector2(spawner.waterHeight,10000);type.rejectUnderwater=true;
                foreach(var prefab in family)
                {
                    var native=AssetDatabase.LoadAssetAtPath<GameObject>(WorldMapLandscapeLab.Folder+"/"+prefab.name+"_Terrain.prefab");
                    if(!native) throw new InvalidOperationException("Falta el prefab forestal "+prefab.name);
                    type.prefabs.Add(new SpawnerBase.TreePrefab{prefab=native,probability=100});
                }
                spawner.treeTypes.Add(type);
            }
            spawner.RefreshTreePrefabs();
            var fields=terrains.Select(t=>new WorldMapAdaptiveForest.Field{terrain=t,n=64,raw=File.ReadAllBytes(Folder+"/"+t.name+".layers.bytes")}).ToList();
            var state=UnityEngine.Random.state;
            try
            {
                var result=WorldMapAdaptiveForest.Generate(fields,spawner,profile,settings.maximumForestSlope,
                    preview.worldRoot.GetComponentsInChildren<LineRenderer>(true),WorldCampaignWater.Settings());
                foreach(var t in terrains)
                {
                    t.terrainData.SetTreeInstances(result.instances[t].ToArray(),false);
                    t.ignoreQualitySettings=true;t.drawTreesAndFoliage=true;
                    t.treeDistance=profile.forestViewDistance;t.treeBillboardDistance=profile.forestViewDistance;
                    t.treeMaximumFullLODCount=Mathf.Max(1,t.terrainData.treeInstanceCount);
                    EditorUtility.SetDirty(t.terrainData);
                }
            }
            finally{UnityEngine.Random.state=state;}
        }

        [MenuItem("Tools/BronzeAge/Pruebas/Actualizar roca de montañas")]
        public static void ApplyRockStyle()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath) throw new InvalidOperationException("Abre WorldMapMountainsV9.");
            var terrains=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Terrain>(true)).ToArray();
            var profile=AssetDatabase.LoadAssetAtPath<WorldMapGroundTrialProfile>(Folder+"/GroundProfile.asset");
            WorldMapRegionalGroundTrial.RefreshSurfaces(scene,profile,Folder,Folder+"/RegionalGround");
        }
    }
}

