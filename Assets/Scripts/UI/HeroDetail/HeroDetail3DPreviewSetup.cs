using UnityEngine;

/// <summary>
/// Utilidad de configuración para el sistema de preview 3D del héroe.
/// Contiene configuraciones recomendadas y helpers para setup inicial.
/// </summary>
[System.Serializable]
public static class HeroDetail3DPreviewSetup
{
    /// <summary>
    /// Configuraciones recomendadas para el RenderTexture.
    /// </summary>
    public static class RenderTextureSettings
    {
        public const int RECOMMENDED_WIDTH = 512;
        public const int RECOMMENDED_HEIGHT = 512;
        public const int RECOMMENDED_DEPTH = 16;
        public const RenderTextureFormat RECOMMENDED_FORMAT = RenderTextureFormat.ARGB32;
        
        /// <summary>
        /// Crea un RenderTexture con configuraciones recomendadas.
        /// </summary>
        public static RenderTexture CreateRecommended()
        {
            var renderTexture = new RenderTexture(RECOMMENDED_WIDTH, RECOMMENDED_HEIGHT, RECOMMENDED_DEPTH);
            renderTexture.format = RECOMMENDED_FORMAT;
            renderTexture.Create();
            return renderTexture;
        }
    }
    
    /// <summary>
    /// Configuraciones recomendadas para la cámara de preview.
    /// </summary>
    public static class CameraSettings
    {
        public const string RECOMMENDED_LAYER_NAME = "hero_preview";
        public const float RECOMMENDED_FIELD_OF_VIEW = 60f;
        public const float RECOMMENDED_NEAR_PLANE = 0.1f;
        public const float RECOMMENDED_FAR_PLANE = 10f;
        
        /// <summary>
        /// Configura una cámara con los settings recomendados.
        /// </summary>
        public static void ConfigureCamera(Camera camera)
        {
            if (camera == null) return;
            
            camera.fieldOfView = RECOMMENDED_FIELD_OF_VIEW;
            camera.nearClipPlane = RECOMMENDED_NEAR_PLANE;
            camera.farClipPlane = RECOMMENDED_FAR_PLANE;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            
            // Configurar layer mask
            int heroLayer = LayerMask.NameToLayer(RECOMMENDED_LAYER_NAME);
            if (heroLayer != -1)
            {
                camera.cullingMask = 1 << heroLayer;
            }
            else
            {
                Debug.LogWarning($"[HeroDetail3DPreviewSetup] Layer '{RECOMMENDED_LAYER_NAME}' not found");
                camera.cullingMask = -1; // Render everything as fallback
            }
        }
    }
    
    /// <summary>
    /// Configuraciones recomendadas para rotación.
    /// </summary>
    public static class RotationSettings
    {
        public const float DEFAULT_SENSITIVITY = 1.0f;
        public const float VERTICAL_LIMIT = 80f;
        public const float DEFAULT_DISTANCE = 3f;
        
        public static readonly Vector3 DEFAULT_INITIAL_POSITION = new Vector3(0, 1.5f, 2f);
        public static readonly Vector3 DEFAULT_TARGET_OFFSET = new Vector3(0, 1f, 0);
    }

    /// <summary>
    /// Verifica si el setup del preview está configurado correctamente.
    /// </summary>
    public static bool ValidateSetup(HeroDetail3DPreview previewSystem)
    {
        if (previewSystem == null)
        {
            Debug.LogError("[HeroDetail3DPreviewSetup] Preview system is null");
            return false;
        }
        
        bool isValid = true;
        
        // Verificar cámara
        var camera = previewSystem.GetComponentInChildren<Camera>();
        if (camera == null)
        {
            Debug.LogError("[HeroDetail3DPreviewSetup] No Camera component found");
            isValid = false;
        }
        
        // Verificar RawImage
        var rawImage = previewSystem.GetComponent<UnityEngine.UI.RawImage>();
        if (rawImage == null)
        {
            Debug.LogWarning("[HeroDetail3DPreviewSetup] No RawImage component found on the same GameObject");
        }
        
        // Verificar layer
        int heroLayer = LayerMask.NameToLayer(CameraSettings.RECOMMENDED_LAYER_NAME);
        if (heroLayer == -1)
        {
            Debug.LogWarning($"[HeroDetail3DPreviewSetup] Recommended layer '{CameraSettings.RECOMMENDED_LAYER_NAME}' not found in project");
        }
        
        return isValid;
    }
}

/// <summary>
/// Editor helper para configuración automática del preview system.
/// </summary>
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(HeroDetail3DPreview))]
public class HeroDetail3DPreviewEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        GUILayout.Space(10);
        GUILayout.Label("Setup Helpers", UnityEditor.EditorStyles.boldLabel);
        
        if (GUILayout.Button("Validate Setup"))
        {
            var previewSystem = target as HeroDetail3DPreview;
            bool isValid = HeroDetail3DPreviewSetup.ValidateSetup(previewSystem);
            
            if (isValid)
            {
                UnityEditor.EditorUtility.DisplayDialog("Setup Validation", "Preview system setup is valid!", "OK");
            }
            else
            {
                UnityEditor.EditorUtility.DisplayDialog("Setup Validation", "Issues found with preview system setup. Check console for details.", "OK");
            }
        }
        
        if (GUILayout.Button("Create Recommended RenderTexture"))
        {
            var renderTexture = HeroDetail3DPreviewSetup.RenderTextureSettings.CreateRecommended();
            var path = UnityEditor.EditorUtility.SaveFilePanelInProject(
                "Save RenderTexture",
                "HeroPreviewRenderTexture",
                "renderTexture",
                "Save the render texture asset"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                UnityEditor.AssetDatabase.CreateAsset(renderTexture, path);
                UnityEditor.AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif
