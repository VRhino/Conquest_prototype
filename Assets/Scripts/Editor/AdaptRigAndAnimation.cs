using UnityEngine;
using UnityEditor;
using System.Collections.Generic;


// Extensión para buscar hijos recursivamente
public static class TransformExtensions
{
    public static Transform FindDeepChild(this Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = child.FindDeepChild(name);
            if (result != null) return result;
        }
        return null;
    }
}

public class AnimationRiggingTool : EditorWindow
{
    private Vector2 scrollPos;
    private string[] animationPaths;
    private int selectedIndex = -1;
    private string errorMessage = "";

    [MenuItem("Tools/Animation Rigging Tool")]
    public static void ShowWindow()
    {
        GetWindow<AnimationRiggingTool>("Animation Rigging Tool");
    }

    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle errorStyle;

    private GUIStyle scrollButtonStyle; // Estilo para los botones de la ScrollView
    private GUIStyle actionButtonStyle; // Estilo para los botones de acciones
    private GUIStyle centeredLabelStyle;

    private void OnEnable()
    {
        CreateStyles();
        LoadAnimations();
    }
    
    private void CreateStyles()
    {
        titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }, // Color del texto
            hover = { textColor = Color.white } // Color del texto
        };

        scrollButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            fontSize = 12,
            fixedHeight = 30,
            normal = { 
                textColor = Color.black, 
                background = MakeTex(2, 2, new Color(0.55f, 0.55f, 0.55f)) // Gris claro
            }
        };

        actionButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            fontSize = 14,
            fixedHeight = 30,
            normal = { 
                textColor = Color.black, 
                background = MakeTex(2, 2, new Color(0.6f, 0.6f, 0.6f)) // Fondo blanco
            },
            hover = { 
                textColor = Color.black, 
                background = MakeTex(2, 2, new Color(0.8f, 0.8f, 0.8f)) // Fondo blanco
            }
        };
        
        centeredLabelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter, // Centrar el texto
            normal = { textColor = Color.white } // Color del texto
        };
    }

    // Método para crear texturas de fondo
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Texture2D tex = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
    
    
    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(10); // Margen izquierdo

        GUILayout.BeginVertical();
        {
            // Título
            GUILayout.Label("🛠 Animation Rigging Tool", titleStyle);
            GUILayout.Space(10);

            // Lista de animaciones
            GUILayout.Label("📂 Select Animation", centeredLabelStyle);
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            if (animationPaths != null)
            {
                for (int i = 0; i < animationPaths.Length; i++)
                {
                    string fileName = System.IO.Path.GetFileName(animationPaths[i]);
                    bool isSelected = (selectedIndex == i);
                    GUIStyle style = isSelected ? scrollButtonStyle : EditorStyles.miniButton;
                    if (GUILayout.Button(fileName, style)) selectedIndex = i;
                }
            }
            GUILayout.EndScrollView();


            // Botones de acción
            GUILayout.Space(10);
            if (GUILayout.Button("✅ Adapt Rigging", actionButtonStyle)) AdaptRigging();
            
            GUILayout.Space(5);
            if (GUILayout.Button("🎞 Adapt Animation", actionButtonStyle)) AdaptAnimation();

            GUILayout.Space(20);
            if (GUILayout.Button("✅ 🎞 Both", actionButtonStyle))
            {
                AdaptRigging();
                AdaptAnimation();
            }
            
            // Mensajes de error
            if (!string.IsNullOrEmpty(errorMessage))
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
            }
        }
        GUILayout.EndVertical();

        GUILayout.Space(10); // Margen derecho
        GUILayout.EndHorizontal();
    }
    
    private void LoadAnimations()
    {
        string[] allGuids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Animations" });
        List<string> filteredPaths = new List<string>();

        foreach (string guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string nombre = System.IO.Path.GetFileName(path);

            if (!nombre.Contains("Mixamo_Unity"))
            {
                filteredPaths.Add(path);
            }
        }

        animationPaths = filteredPaths.ToArray();
    }

    private void AdaptRigging()
    {
        if (selectedIndex < 0) return;

        string path = animationPaths[selectedIndex];
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

        if (importer == null)
        {
            errorMessage = "❌ There was an error while importing the model.";
            Debug.LogError(errorMessage);
            return;
        }
        
        // Verify if the animactón is already humanoid
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.SaveAndReimport();
            Debug.Log("✅ Change type of animation to Humanoid.");
        }

        // Try loading model
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
        {
            errorMessage = "❌ Error loading the model from the jerarquie.";
            Debug.LogError(errorMessage);
            return;
        }

        // Try getting the Animator
        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
        {
            errorMessage = "❌ Error loading Animator of the model.";
            Debug.LogError(errorMessage);
            return;
        }

        // Try to load Avatar
        Avatar avatar = animator.avatar;
        if (avatar == null)
        {
            errorMessage = "❌ Error loading Avatar of the model.";
            Debug.LogError(errorMessage);
            return;
        }


        // Verify if the importer has a humanDescription
        HumanDescription humanDesc = importer.humanDescription;
        if (humanDesc.human == null || humanDesc.human.Length == 0)
        {
            errorMessage = "❌ Error loading the description of the esqueleton.";
            Debug.LogError(errorMessage);
            return;
        }

        List<string> huesos = new List<string>() {"Finger_031", "Finger_021", "Finger_011", "Finger_01", "Finger_02", "Finger_03" };
        foreach (var hueso in huesos)
        {
            if (!BoneExistsInModel(path, hueso))
            {
                errorMessage = $"❌ The bone: {hueso} was not found in the esqueleton.";
                Debug.LogError(errorMessage);
                return;
            }
        }
        
       
        RemoveBone(ref humanDesc, "Jaw");
        
        // Convertir el array humanDesc.human a una lista para modificarlo
        List<HumanBone> humanBones = new List<HumanBone>(humanDesc.human);
        
        AddOrModifyBone(ref humanBones, "Right Little Proximal", "");
        AddOrModifyBone(ref humanBones, "Right Little Intermediate", "");
        AddOrModifyBone(ref humanBones, "Right Little Distal", "");
        
        AddOrModifyBone(ref humanBones, "Left Little Proximal", "");
        AddOrModifyBone(ref humanBones, "Left Little Intermediate", "");
        AddOrModifyBone(ref humanBones, "Left Little Distal", "");
        
        AddOrModifyBone(ref humanBones, "Left Middle Proximal", "Finger_01");
        AddOrModifyBone(ref humanBones, "Left Middle Intermediate", "Finger_02");
        AddOrModifyBone(ref humanBones, "Left Middle Distal", "Finger_03");
        
        AddOrModifyBone(ref humanBones, "Right Middle Proximal", "Finger_011");
        AddOrModifyBone(ref humanBones, "Right Middle Intermediate", "Finger_021");
        AddOrModifyBone(ref humanBones, "Right Middle Distal", "Finger_031");

        // Asignar la lista modificada de vuelta al HumanDescription
        humanDesc.human = humanBones.ToArray();

        // Aplicar cambios
        importer.humanDescription = humanDesc;
        importer.SaveAndReimport();

        Debug.Log("✅ Rigging adapted correctly for " + path);
        errorMessage = "";
    }

    private bool BoneExistsInModel(string modelPath, string boneName)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            Debug.LogError("Model not found: " + modelPath);
            return false;
        }

        Transform bone = model.transform.FindDeepChild(boneName); // Necesitarás un método para buscar recursivamente
        return bone != null;
    }
    
    private void RemoveBone(ref HumanDescription humanDesc, string boneName)
    {
        // Convertir el array de HumanBone a una lista para facilitar la manipulación
        List<HumanBone> humanBones = new List<HumanBone>(humanDesc.human);

        // Buscar y eliminar el hueso
        for (int i = humanBones.Count - 1; i >= 0; i--)
        {
            if (humanBones[i].humanName == boneName)
            {
                humanBones.RemoveAt(i);
                Debug.Log($"✅ Bone '{boneName}' eliminated from HumanDescription.");
            }
        }

        // Asignar la lista modificada de vuelta al HumanDescription
        humanDesc.human = humanBones.ToArray();
    }

    
    private void AdaptAnimation()
    {
        if (selectedIndex < 0) return;

        string path = animationPaths[selectedIndex];
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

        if (importer == null)
        {
            errorMessage = "❌ There was an error while importing the model.";
            Debug.LogError(errorMessage);
            return;
        }
        
        // AssetPostprocessor preprocessor = new AssetPostprocessor().;

        // 1. Forzar configuración humana si es necesario
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.SaveAndReimport(); // ¡Reimportar antes de continuar!
            AssetDatabase.Refresh();
        }

        // 2. Obtener los clips y modificar
        ModelImporterClipAnimation[] clips = importer.clipAnimations;

        if (clips == null || clips.Length == 0)
        {
            ModelImporterClipAnimation[] defaultClips = importer.defaultClipAnimations;
            if (defaultClips == null || defaultClips.Length == 0)
            {
                errorMessage = "❌ No clips were found in the animation.";
                Debug.LogError(errorMessage);
                return;
            }
            
            clips = defaultClips;
        }

        foreach (ModelImporterClipAnimation clip in clips)
        {
            Debug.Log($"Modificando clip: {clip.name}");
            clip.loopTime = true;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
        }
        
        importer.clipAnimations = clips;

        
        importer.SaveAndReimport();
        AssetDatabase.Refresh();

        Debug.Log("✅ Animations adapted correctly!");
        errorMessage = "";
    }    
    
    private void AddOrModifyBone(ref List<HumanBone> humanBones, string humanName, string boneName)
    {
        // Buscar si el hueso humanoide ya existe
        int index = humanBones.FindIndex(b => b.humanName == humanName);
        if (boneName == null) boneName = "";

        if (index >= 0)
        {
            // Modificar el hueso existente
            HumanBone bone = humanBones[index];
            bone.boneName = boneName;
            humanBones[index] = bone;
            Debug.Log($"✅ Modified: {humanName} -> {boneName}");
        }
        else
        {
            // Agregar un nuevo hueso
            HumanBone newBone = new HumanBone
            {
                humanName = humanName,
                boneName = boneName,
                // Limitar la rotación/posición si es necesario (opcional)
                limit = new HumanLimit { useDefaultValues = true }
            };
            humanBones.Add(newBone);
            Debug.Log($"➕ New bone added: {humanName} -> {boneName}");
        }
    }
}