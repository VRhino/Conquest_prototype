using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Jobs;
using Unity.AI.Navigation.LowLevel;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Conquest.Editor.Optimization
{
    /// <summary>
    /// Editor-only, opt-in URP experiment profiles and a runtime renderer census.
    /// The source pipeline is never edited; Begin switches only the current quality override
    /// and Restore returns that override and the global BRG stripping mode to their prior values.
    /// </summary>
    [InitializeOnLoad]
    public static class RenderOptimizationExperiments
    {
        private const string SessionActiveKey = "Conquest.RenderOptimization.Active";
        private const string SessionQualityKey = "Conquest.RenderOptimization.Quality";
        private const string SessionPipelineGuidKey = "Conquest.RenderOptimization.PipelineGuid";
        private const string SessionBrgModeKey = "Conquest.RenderOptimization.BrgMode";
        private const string VariantA = "AForward";
        private const string VariantB = "BForwardPlus";
        private const string VariantC = "CForwardPlusGRD";
        private const string VariantD = "DForwardPlusGRDOcclusion";
        private const string VariantSsaoOff = "SSAOOff";
        private const string VariantSsaoOn = "SSAOOn";

        private static readonly Dictionary<string, UniversalRenderPipelineAsset> Profiles =
            new Dictionary<string, UniversalRenderPipelineAsset>(StringComparer.Ordinal);
        private static string s_ProfileSourceGuid;
        private static string s_ProfileSourceDependencyHash;

        static RenderOptimizationExperiments()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += RestoreBeforeAssemblyReload;
            if (SessionState.GetBool(SessionActiveKey, false) &&
                !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Restore();
            }
        }

        /// <summary>
        /// Starts one isolated cloned profile in Play Mode. Valid variants:
        /// AForward, BForwardPlus, CForwardPlusGRD, DForwardPlusGRDOcclusion, SSAOOff, SSAOOn.
        /// </summary>
        public static void Begin(string variant)
        {
            ValidateVariantName(variant);
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Begin solo puede aplicarse durante Play Mode; crea los clones antes si hace falta.");
            if (SessionState.GetBool(SessionActiveKey, false))
                throw new InvalidOperationException("Ya hay un experimento de render activo. Llama Restore() antes de cambiar variante.");

            UniversalRenderPipelineAsset source = GetCurrentPipeline();
            if (source == null)
                throw new InvalidOperationException("No se encontró un UniversalRenderPipelineAsset activo para clonar.");

            EnsureProfiles(source);
            string sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
            if (!Profiles.TryGetValue(GetProfileKey(sourceGuid, variant), out UniversalRenderPipelineAsset profile) || profile == null)
                throw new ArgumentException($"Variante desconocida: {variant}", nameof(variant));

            if (variant == VariantC || variant == VariantD)
            {
                string support = ValidateVariant(variant);
                ValidationResult validation = JsonUtility.FromJson<ValidationResult>(support);
                if (validation == null || !validation.supported)
                    throw new InvalidOperationException("El entorno actual no satisface los requisitos básicos de GRD: " + support);
            }

            int qualityIndex = QualitySettings.GetQualityLevel();
            RenderPipelineAsset previousOverride = QualitySettings.renderPipeline;
            int previousBrgMode = GetBrgStrippingEnumIndex();
            string previousGuid = previousOverride == null
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(previousOverride));

            SessionState.SetInt(SessionQualityKey, qualityIndex);
            SessionState.SetString(SessionPipelineGuidKey, previousGuid);
            SessionState.SetInt(SessionBrgModeKey, previousBrgMode);
            SessionState.SetBool(SessionActiveKey, true);

            try
            {
                if (variant == VariantC || variant == VariantD)
                    SetBrgStrippingEnumIndex((int)BatchRendererGroupStrippingMode.KeepAll);
                QualitySettings.renderPipeline = profile;
                Debug.Log($"[RenderOptimizationExperiments] Activa {variant}: {AssetDatabase.GetAssetPath(profile)}. Llama Restore() al terminar la medición.");
            }
            catch
            {
                Restore();
                throw;
            }
        }

        /// <summary>Restores the prior quality override and global BRG shader-stripping setting.</summary>
        public static void Restore()
        {
            if (!SessionState.GetBool(SessionActiveKey, false))
                return;

            int savedQuality = SessionState.GetInt(SessionQualityKey, QualitySettings.GetQualityLevel());
            string pipelineGuid = SessionState.GetString(SessionPipelineGuidKey, string.Empty);
            int brgMode = SessionState.GetInt(SessionBrgModeKey, (int)BatchRendererGroupStrippingMode.KeepIfEntitiesGraphics);
            int currentQuality = QualitySettings.GetQualityLevel();

            if (savedQuality >= 0 && savedQuality < QualitySettings.names.Length && currentQuality != savedQuality)
                QualitySettings.SetQualityLevel(savedQuality, false);

            string pipelinePath = string.IsNullOrEmpty(pipelineGuid) ? null : AssetDatabase.GUIDToAssetPath(pipelineGuid);
            RenderPipelineAsset priorPipeline = string.IsNullOrEmpty(pipelinePath)
                ? null
                : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(pipelinePath);
            QualitySettings.renderPipeline = priorPipeline;

            SetBrgStrippingEnumIndex(brgMode);

            if (currentQuality != savedQuality && currentQuality >= 0 && currentQuality < QualitySettings.names.Length)
                QualitySettings.SetQualityLevel(currentQuality, false);

            ClearSavedState();
            Debug.Log("[RenderOptimizationExperiments] Estado previo restaurado.");
        }

        /// <summary>Returns a JSON census of loaded scene renderers; candidate flags are heuristics, not eligibility proof.</summary>
        public static string Census()
        {
            var rows = new List<RendererRow>();
            int meshCount = 0;
            int candidateCount = 0;
            int skinnedCount = 0;
            int terrainCount = 0;
            int terrainTreeInstances = 0;
            int manualPathComponents = 0;

            foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (renderer == null || !renderer.gameObject.scene.IsValid() || !renderer.gameObject.scene.isLoaded)
                    continue;

                if (renderer is SkinnedMeshRenderer)
                {
                    skinnedCount++;
                    rows.Add(new RendererRow
                    {
                        kind = "SkinnedMeshRenderer",
                        hierarchy = GetHierarchyPath(renderer.transform),
                        active = renderer.gameObject.activeInHierarchy,
                        enabled = renderer.enabled,
                        residentDrawerHeuristic = "excluded: skinned"
                    });
                    continue;
                }

                if (!(renderer is MeshRenderer))
                    continue;

                meshCount++;
                bool hasMpb = renderer.HasPropertyBlock();
                bool hasLppv = renderer.lightProbeProxyVolumeOverride != null || renderer.GetComponent<LightProbeProxyVolume>() != null;
                bool hasUnsupportedMaterial = false;
                var materialNames = new List<string>();
                var shaderNames = new List<string>();
                var materialFlags = new List<string>();
                Material[] sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials.Length == 0)
                    hasUnsupportedMaterial = true;
                foreach (Material material in sharedMaterials)
                {
                    if (material == null)
                    {
                        hasUnsupportedMaterial = true;
                        materialNames.Add("<null>");
                        continue;
                    }

                    materialNames.Add(material.name);
                    Shader shader = material.shader;
                    shaderNames.Add(shader == null ? "<null>" : shader.name);
                    bool declaresDotsInstancing = shader != null && shader.keywordSpace.FindKeyword("DOTS_INSTANCING_ON").isValid;
                    materialFlags.Add(declaresDotsInstancing ? "DOTS_INSTANCING_ON" : "no-DOTS-instancing-keyword");
                    if (!declaresDotsInstancing)
                        hasUnsupportedMaterial = true;
                }

                bool candidate = !hasMpb && !hasLppv && !hasUnsupportedMaterial;
                if (candidate)
                    candidateCount++;

                rows.Add(new RendererRow
                {
                    kind = "MeshRenderer",
                    hierarchy = GetHierarchyPath(renderer.transform),
                    active = renderer.gameObject.activeInHierarchy,
                    enabled = renderer.enabled,
                    materials = string.Join(";", materialNames),
                    shaders = string.Join(";", shaderNames),
                    materialFlags = string.Join(";", materialFlags),
                    hasMaterialPropertyBlock = hasMpb,
                    hasLightProbeProxyVolume = hasLppv,
                    residentDrawerHeuristic = candidate ? "potential candidate; verify in Frame Debugger" : "not candidate by conservative heuristic"
                });
            }

            foreach (Terrain terrain in Resources.FindObjectsOfTypeAll<Terrain>())
            {
                if (terrain == null || !terrain.gameObject.scene.IsValid() || !terrain.gameObject.scene.isLoaded)
                    continue;
                terrainCount++;
                if (terrain.terrainData != null)
                    terrainTreeInstances += terrain.terrainData.treeInstanceCount;
            }

            foreach (MonoBehaviour component in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (component == null || !component.gameObject.scene.IsValid() || !component.gameObject.scene.isLoaded)
                    continue;
                string typeName = component.GetType().Name;
                if (typeName.Contains("WorldMapSyntyRenderer", StringComparison.Ordinal) ||
                    typeName.Contains("WorldMapForestOverview", StringComparison.Ordinal))
                {
                    manualPathComponents++;
                    rows.Add(new RendererRow
                    {
                        kind = "manual-instancing component",
                        hierarchy = GetHierarchyPath(component.transform),
                        active = component.gameObject.activeInHierarchy,
                        enabled = component.enabled,
                        residentDrawerHeuristic = "custom draw path; not counted as MeshRenderer candidate"
                    });
                }
            }

            var report = new CensusReport
            {
                pipeline = GraphicsSettings.currentRenderPipeline == null ? "<null>" : GraphicsSettings.currentRenderPipeline.name,
                quality = QualitySettings.names.Length > QualitySettings.GetQualityLevel()
                    ? QualitySettings.names[QualitySettings.GetQualityLevel()]
                    : "<unknown>",
                meshRenderers = meshCount,
                conservativeMeshCandidates = candidateCount,
                skinnedMeshRenderers = skinnedCount,
                terrains = terrainCount,
                terrainTreeInstances = terrainTreeInstances,
                knownManualInstancingComponents = manualPathComponents,
                notes = "Candidate is only a conservative shader/MPB/proxy-volume heuristic; Unity's runtime eligibility and batching must be confirmed in Frame Debugger/Profiler. Terrain tree instances and manual draw calls are not MeshRenderer candidates.",
                renderers = rows.ToArray()
            };
            string json = JsonUtility.ToJson(report, true);
            Debug.Log($"[RenderOptimizationExperiments] Census: MeshRenderer={meshCount}, candidatos heurísticos={candidateCount}, SkinnedMeshRenderer={skinnedCount}, Terrain={terrainCount}, árboles={terrainTreeInstances}, componentes de instancing manual conocidos={manualPathComponents}. JSON detallado devuelto por Census().");
            return json;
        }

        /// <summary>Reports hard prerequisites for an experiment. Support is still confirmed by runtime diagnostics and profiling.</summary>
        public static string ValidateVariant(string variant)
        {
            ValidateVariantName(variant);
            UniversalRenderPipelineAsset source = GetCurrentPipeline();
            if (source == null)
                return JsonUtility.ToJson(new ValidationResult { supported = false, issues = new[] { "No active URP asset." } }, true);
            EnsureProfiles(source);
            string sourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
            if (!Profiles.TryGetValue(GetProfileKey(sourceGuid, variant), out UniversalRenderPipelineAsset profile) || profile == null)
                return JsonUtility.ToJson(new ValidationResult { supported = false, issues = new[] { "Unknown profile variant: " + variant } }, true);

            var issues = new List<string>();
            if (variant == VariantC || variant == VariantD)
            {
                if (!SystemInfo.supportsComputeShaders)
                    issues.Add("SystemInfo.supportsComputeShaders is false.");
                if (BatchRendererGroup.BufferTarget != BatchBufferTarget.RawBuffer)
                    issues.Add("BatchRendererGroup.BufferTarget is not RawBuffer.");
                if (profile.gpuResidentDrawerMode != GPUResidentDrawerMode.InstancedDrawing)
                    issues.Add("The clone does not have GPU Resident Drawer instanced drawing enabled.");
            }
            if (variant == VariantD && !profile.gpuResidentDrawerEnableOcclusionCullingInCameras)
                issues.Add("GPU occlusion is not enabled on this clone.");

            return JsonUtility.ToJson(new ValidationResult
            {
                supported = issues.Count == 0,
                issues = issues.ToArray(),
                profilePath = AssetDatabase.GetAssetPath(profile),
                note = "This checks compute, RawBuffer and cloned profile flags. Begin temporarily sets serialized GraphicsSettings m_BrgStripping to KeepAll for GRD variants; confirm runtime warnings and camera/depth path before interpreting timing."
            }, true);
        }

        /// <summary>
        /// Diagnostic-only comparison of NavMesh.SamplePosition against the installed LowLevel NavWorld.MapLocation job API.
        /// The two search regions differ (sphere radius versus axis-aligned extents), so this reports divergence and is not a replacement.
        /// </summary>
        public static string CompareLowLevelProjection(
            Vector3[] positions,
            int agentTypeId,
            int areaMask,
            float sampleRadius,
            Vector3 mapExtents,
            float positionTolerance,
            int batchSize = 64)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("CompareLowLevelProjection sólo se ejecuta durante Play Mode.");
            if (positions == null || positions.Length == 0)
                throw new ArgumentException("Se necesita al menos una posición", nameof(positions));
            if (sampleRadius <= 0f || mapExtents.x <= 0f || mapExtents.y <= 0f || mapExtents.z <= 0f)
                throw new ArgumentOutOfRangeException(nameof(sampleRadius), "El radio y las tres extensiones deben ser positivos.");
            if (positionTolerance < 0f || batchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(positionTolerance));

            var sampleValid = new bool[positions.Length];
            var samplePositions = new Vector3[positions.Length];
            NavMeshQueryFilter sampleFilter = new NavMeshQueryFilter
            {
                agentTypeID = agentTypeId,
                areaMask = areaMask
            };
            long sampleStart = Stopwatch.GetTimestamp();
            for (int index = 0; index < positions.Length; index++)
            {
                if (NavMesh.SamplePosition(positions[index], out NavMeshHit hit, sampleRadius, sampleFilter))
                {
                    sampleValid[index] = true;
                    samplePositions[index] = hit.position;
                }
            }
            double sampleMilliseconds = ElapsedMilliseconds(sampleStart);

            NativeArray<NavLocation> mapLocations = default;
            NativeArray<Vector3> nativePositions = default;
            var mapValid = new bool[positions.Length];
            var mapPositions = new Vector3[positions.Length];
            NavWorld world = default;
            bool worldAcquired = false;
            JobHandle jobHandle = default;
            bool scheduled = false;
            double mapMilliseconds = 0d;
            try
            {
                mapLocations = new NativeArray<NavLocation>(positions.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                nativePositions = new NativeArray<Vector3>(positions, Allocator.TempJob);
                world = NavWorld.GetDefaultWorld();
                worldAcquired = true;
                if (!world.IsValid())
                    throw new InvalidOperationException("NavWorld.GetDefaultWorld devolvió un mundo no válido.");

                var job = new MapLocationJob
                {
                    world = world,
                    positions = nativePositions,
                    locations = mapLocations,
                    extents = mapExtents,
                    agentTypeId = agentTypeId,
                    areaMask = areaMask
                };
                long mapStart = Stopwatch.GetTimestamp();
                jobHandle = job.ScheduleParallel(positions.Length, batchSize, default);
                scheduled = true;
                world.AddDependency(jobHandle);
                jobHandle.Complete();
                mapMilliseconds = ElapsedMilliseconds(mapStart);
                for (int index = 0; index < positions.Length; index++)
                {
                    mapValid[index] = world.IsValid(mapLocations[index]);
                    if (mapValid[index])
                        mapPositions[index] = mapLocations[index].position;
                }
            }
            finally
            {
                if (scheduled)
                    jobHandle.Complete();
                if (worldAcquired)
                    world.Dispose();
                if (nativePositions.IsCreated)
                    nativePositions.Dispose();
                if (mapLocations.IsCreated)
                    mapLocations.Dispose();
            }

            int sampleFound = 0;
            int mapFound = 0;
            int validityMismatches = 0;
            int positionMismatches = 0;
            float maxPositionDelta = 0f;
            for (int index = 0; index < positions.Length; index++)
            {
                bool mapIsValid = mapValid[index];
                if (sampleValid[index]) sampleFound++;
                if (mapIsValid) mapFound++;
                if (sampleValid[index] != mapIsValid)
                {
                    validityMismatches++;
                    continue;
                }
                if (!mapIsValid)
                    continue;
                float delta = Vector3.Distance(samplePositions[index], mapPositions[index]);
                maxPositionDelta = Mathf.Max(maxPositionDelta, delta);
                if (delta > positionTolerance)
                    positionMismatches++;
            }

            var result = new LowLevelComparison
            {
                samples = positions.Length,
                agentTypeId = agentTypeId,
                areaMask = areaMask,
                sampleRadius = sampleRadius,
                mapExtents = mapExtents,
                positionTolerance = positionTolerance,
                samplePositionFound = sampleFound,
                mapLocationFound = mapFound,
                validityMismatches = validityMismatches,
                positionMismatches = positionMismatches,
                maxPositionDelta = maxPositionDelta,
                samplePositionMilliseconds = sampleMilliseconds,
                mapLocationScheduleAndCompleteMilliseconds = mapMilliseconds,
                note = "No equivalence is assumed: SamplePosition searches a radius across available surfaces, while MapLocation searches axis-aligned extents for a specified agent type and has different surface selection semantics. Use only to assess divergence and job overhead; do not substitute runtime queries from this result."
            };
            return JsonUtility.ToJson(result, true);
        }

        private static double ElapsedMilliseconds(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) * 1000d / Stopwatch.Frequency;
        }

        [MenuItem("Tools/Conquest/Optimization/Render/Census loaded renderers")]
        private static void CensusMenu() => Census();

        [MenuItem("Tools/Conquest/Optimization/Render/Create isolated profile clones")]
        private static void CreateProfilesMenu()
        {
            UniversalRenderPipelineAsset source = GetCurrentPipeline();
            if (source == null)
            {
                Debug.LogError("[RenderOptimizationExperiments] No active URP asset to clone.");
                return;
            }
            EnsureProfiles(source);
            Debug.Log("[RenderOptimizationExperiments] Clones creados desde " + AssetDatabase.GetAssetPath(source) + ". No se han activado.");
        }

        [MenuItem("Tools/Conquest/Optimization/Render/Restore experiment state")]
        private static void RestoreMenu() => Restore();

        private static UniversalRenderPipelineAsset GetCurrentPipeline()
        {
            return QualitySettings.renderPipeline as UniversalRenderPipelineAsset ??
                   GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        }

        private static void EnsureProfiles(UniversalRenderPipelineAsset source)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (string.IsNullOrEmpty(sourceGuid))
                throw new InvalidOperationException("El pipeline origen debe ser un asset persistente.");

            string dependencyHash = AssetDatabase.GetAssetDependencyHash(sourcePath).ToString();
            if (string.Equals(s_ProfileSourceGuid, sourceGuid, StringComparison.Ordinal) &&
                string.Equals(s_ProfileSourceDependencyHash, dependencyHash, StringComparison.Ordinal) &&
                Profiles.TryGetValue(GetProfileKey(sourceGuid, VariantA), out UniversalRenderPipelineAsset cached) && cached != null)
                return;

            // Entries belong to one dependency snapshot; reload from the matching
            // asset folder whenever the source or any renderer dependency changes.
            Profiles.Clear();

            string root = "Assets/Settings/Experimental/U6RenderProfiles";
            EnsureAssetFolder("Assets/Settings");
            EnsureAssetFolder("Assets/Settings/Experimental");
            EnsureAssetFolder(root);
            string folder = root + "/" + source.name + "_" + sourceGuid.Substring(0, Math.Min(8, sourceGuid.Length)) + "_" + dependencyHash.Substring(0, Math.Min(12, dependencyHash.Length));
            EnsureAssetFolder(folder);

            var variants = new[] { VariantA, VariantB, VariantC, VariantD, VariantSsaoOff, VariantSsaoOn };
            foreach (string variant in variants)
            {
                string profileKey = GetProfileKey(sourceGuid, variant);
                if (!Profiles.TryGetValue(profileKey, out UniversalRenderPipelineAsset clone) || clone == null)
                {
                    string existingPipelinePath = folder + "/" + variant + "_Pipeline.asset";
                    clone = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(existingPipelinePath);
                    if (clone == null)
                    {
                        clone = ClonePipelineAndRenderers(source, folder, variant);
                        ConfigureVariant(clone, variant);
                        SaveOwnedAssets(clone);
                    }
                    Profiles[profileKey] = clone;
                }
                ValidateProfileRendererIsolation(source, clone, folder, variant);
            }

            s_ProfileSourceGuid = sourceGuid;
            s_ProfileSourceDependencyHash = dependencyHash;

            Debug.Log($"[RenderOptimizationExperiments] Seis perfiles experimentales listos desde {sourcePath} (GUID {sourceGuid}); no se han activado.");
        }

        private static UniversalRenderPipelineAsset ClonePipelineAndRenderers(UniversalRenderPipelineAsset source, string folder, string variant)
        {
            string pipelinePath = folder + "/" + variant + "_Pipeline.asset";
            UniversalRenderPipelineAsset clonedPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (clonedPipeline == null)
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), pipelinePath))
                    throw new InvalidOperationException("No se pudo clonar el pipeline URP a " + pipelinePath);
                clonedPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            }
            var sourceRenderers = source.rendererDataList;
            var clonedRenderers = new ScriptableRendererData[sourceRenderers.Length];
            for (int index = 0; index < sourceRenderers.Length; index++)
            {
                ScriptableRendererData rendererData = sourceRenderers[index];
                if (rendererData == null)
                    continue;
                string rendererPath = folder + "/" + variant + "_Renderer_" + index + ".asset";
                clonedRenderers[index] = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
                if (clonedRenderers[index] == null)
                {
                    if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(rendererData), rendererPath))
                        throw new InvalidOperationException("No se pudo clonar el renderer data a " + rendererPath);
                    clonedRenderers[index] = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
                }
                ValidateRendererFeatureIsolation(rendererData, clonedRenderers[index], rendererPath);
            }

            var serializedPipeline = new SerializedObject(clonedPipeline);
            SerializedProperty rendererList = serializedPipeline.FindProperty("m_RendererDataList");
            if (rendererList == null || !rendererList.isArray || rendererList.arraySize != clonedRenderers.Length)
                throw new InvalidOperationException("No se pudo sustituir m_RendererDataList del clon URP.");
            for (int index = 0; index < clonedRenderers.Length; index++)
                rendererList.GetArrayElementAtIndex(index).objectReferenceValue = clonedRenderers[index];
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clonedPipeline);
            return clonedPipeline;
        }

        private static void ConfigureVariant(UniversalRenderPipelineAsset pipeline, string variant)
        {
            if (variant == VariantA || variant == VariantB || variant == VariantC || variant == VariantD)
            {
                RenderingMode desiredMode = variant == VariantA ? RenderingMode.Forward : RenderingMode.ForwardPlus;
                foreach (ScriptableRendererData rendererData in pipeline.rendererDataList)
                {
                    if (rendererData is UniversalRendererData universalRendererData)
                        universalRendererData.renderingMode = desiredMode;
                }
            }

            if (variant == VariantC || variant == VariantD)
            {
                pipeline.gpuResidentDrawerMode = GPUResidentDrawerMode.InstancedDrawing;
                pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras = variant == VariantD;
            }
            else if (variant == VariantA || variant == VariantB)
            {
                pipeline.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
                pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras = false;
            }

            bool toggleSsao = variant == VariantSsaoOff || variant == VariantSsaoOn;
            if (toggleSsao)
            {
                bool active = variant == VariantSsaoOn;
                bool found = false;
                foreach (ScriptableRendererData rendererData in pipeline.rendererDataList)
                {
                    if (rendererData == null)
                        continue;
                    foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
                    {
                        if (!(feature is ScreenSpaceAmbientOcclusion))
                            continue;
                        feature.SetActive(active);
                        EditorUtility.SetDirty(feature);
                        found = true;
                    }
                    EditorUtility.SetDirty(rendererData);
                }
                if (!found)
                    throw new InvalidOperationException($"El pipeline {pipeline.name} no contiene ScreenSpaceAmbientOcclusion; no se creó un falso perfil {variant}.");
            }

            EditorUtility.SetDirty(pipeline);
        }

        private static void ValidateRendererFeatureIsolation(ScriptableRendererData source, ScriptableRendererData clone, string expectedPath)
        {
            if (clone == null)
                throw new InvalidOperationException("No se pudo cargar el ScriptableRendererData clonado: " + expectedPath);
            if (source.rendererFeatures.Count != clone.rendererFeatures.Count)
                throw new InvalidOperationException("El clon de renderer no preservó su número de features: " + expectedPath);
            for (int index = 0; index < source.rendererFeatures.Count; index++)
            {
                ScriptableRendererFeature originalFeature = source.rendererFeatures[index];
                ScriptableRendererFeature clonedFeature = clone.rendererFeatures[index];
                if (originalFeature == null || clonedFeature == null)
                    continue;
                string originalPath = AssetDatabase.GetAssetPath(originalFeature);
                string clonedFeaturePath = AssetDatabase.GetAssetPath(clonedFeature);
                if (ReferenceEquals(originalFeature, clonedFeature) ||
                    string.Equals(originalPath, clonedFeaturePath, StringComparison.Ordinal) ||
                    !string.Equals(clonedFeaturePath, expectedPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"La feature {originalFeature.name} no quedó aislada en el renderer clonado {expectedPath}.");
                }
            }
        }

        private static void ValidateProfileRendererIsolation(
            UniversalRenderPipelineAsset source,
            UniversalRenderPipelineAsset clone,
            string folder,
            string variant)
        {
            var sourceRenderers = source.rendererDataList;
            var clonedRenderers = clone.rendererDataList;
            if (sourceRenderers.Length != clonedRenderers.Length)
                throw new InvalidOperationException($"El perfil {variant} no conserva la lista de renderer data del origen.");
            for (int index = 0; index < sourceRenderers.Length; index++)
            {
                if (sourceRenderers[index] == null)
                    continue;
                string expectedPath = folder + "/" + variant + "_Renderer_" + index + ".asset";
                if (ReferenceEquals(sourceRenderers[index], clonedRenderers[index]) ||
                    clonedRenderers[index] == null ||
                    !string.Equals(AssetDatabase.GetAssetPath(clonedRenderers[index]), expectedPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"El perfil {variant} no referencia renderer assets aislados para el índice {index}.");
                }
                ValidateRendererFeatureIsolation(sourceRenderers[index], clonedRenderers[index], expectedPath);
            }
        }

        private static void SaveOwnedAssets(UniversalRenderPipelineAsset pipeline)
        {
            foreach (ScriptableRendererData rendererData in pipeline.rendererDataList)
            {
                if (rendererData == null)
                    continue;
                foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
                {
                    if (feature != null)
                        AssetDatabase.SaveAssetIfDirty(feature);
                }
                AssetDatabase.SaveAssetIfDirty(rendererData);
            }
            AssetDatabase.SaveAssetIfDirty(pipeline);
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int separator = path.LastIndexOf('/');
            string parent = separator < 0 ? "Assets" : path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string GetProfileKey(string sourceGuid, string variant)
        {
            return sourceGuid + ":" + variant;
        }

        private static void ValidateVariantName(string variant)
        {
            if (variant != VariantA && variant != VariantB && variant != VariantC && variant != VariantD
                && variant != VariantSsaoOff && variant != VariantSsaoOn)
                throw new ArgumentException("Unknown render experiment variant.", nameof(variant));
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var parts = new Stack<string>();
            for (Transform current = transform; current != null; current = current.parent)
                parts.Push(current.name);
            return string.Join("/", parts);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(SessionActiveKey, false))
                Restore();
        }

        private static void RestoreBeforeAssemblyReload()
        {
            if (SessionState.GetBool(SessionActiveKey, false))
                Restore();
        }

        private static void ClearSavedState()
        {
            SessionState.SetBool(SessionActiveKey, false);
            SessionState.EraseString(SessionPipelineGuidKey);
            SessionState.EraseInt(SessionQualityKey);
            SessionState.EraseInt(SessionBrgModeKey);
        }

        private static int GetBrgStrippingEnumIndex()
        {
            SerializedObject settings = GetGraphicsSettingsSerializedObject();
            SerializedProperty property = settings.FindProperty("m_BrgStripping");
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
                throw new InvalidOperationException("GraphicsSettings.asset no contiene el enum m_BrgStripping esperado en Unity 6000.5.");
            return property.enumValueIndex;
        }

        private static void SetBrgStrippingEnumIndex(int enumIndex)
        {
            SerializedObject settings = GetGraphicsSettingsSerializedObject();
            SerializedProperty property = settings.FindProperty("m_BrgStripping");
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
                throw new InvalidOperationException("GraphicsSettings.asset no contiene el enum m_BrgStripping esperado en Unity 6000.5.");
            property.enumValueIndex = enumIndex;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedObject GetGraphicsSettingsSerializedObject()
        {
            const string graphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(graphicsSettingsPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset == null)
                    continue;
                var serialized = new SerializedObject(asset);
                if (serialized.FindProperty("m_BrgStripping") != null)
                    return serialized;
            }
            throw new InvalidOperationException("No se pudo abrir m_BrgStripping en ProjectSettings/GraphicsSettings.asset.");
        }

        [Serializable]
        private sealed class CensusReport
        {
            public string pipeline;
            public string quality;
            public int meshRenderers;
            public int conservativeMeshCandidates;
            public int skinnedMeshRenderers;
            public int terrains;
            public int terrainTreeInstances;
            public int knownManualInstancingComponents;
            public string notes;
            public RendererRow[] renderers;
        }

        [Serializable]
        private sealed class RendererRow
        {
            public string kind;
            public string hierarchy;
            public bool active;
            public bool enabled;
            public string materials;
            public string shaders;
            public string materialFlags;
            public bool hasMaterialPropertyBlock;
            public bool hasLightProbeProxyVolume;
            public string residentDrawerHeuristic;
        }

        private struct MapLocationJob : IJobFor
        {
            [ReadOnly] public NativeArray<Vector3> positions;
            [WriteOnly] public NativeArray<NavLocation> locations;
            [ReadOnly] public NavWorld world;
            [ReadOnly] public Vector3 extents;
            [ReadOnly] public int agentTypeId;
            [ReadOnly] public int areaMask;

            public void Execute(int index)
            {
                locations[index] = world.MapLocation(positions[index], extents, agentTypeId, areaMask);
            }
        }

        [Serializable]
        private sealed class LowLevelComparison
        {
            public int samples;
            public int agentTypeId;
            public int areaMask;
            public float sampleRadius;
            public Vector3 mapExtents;
            public float positionTolerance;
            public int samplePositionFound;
            public int mapLocationFound;
            public int validityMismatches;
            public int positionMismatches;
            public float maxPositionDelta;
            public double samplePositionMilliseconds;
            public double mapLocationScheduleAndCompleteMilliseconds;
            public string note;
        }

        [Serializable]
        private sealed class ValidationResult
        {
            public bool supported;
            public string profilePath;
            public string[] issues;
            public string note;
        }
    }
}
