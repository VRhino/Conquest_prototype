using UnityEngine;
using UnityEditor;
using System.Text;

public class HeroPrefabDotsValidator : EditorWindow
{
    [SerializeField] GameObject heroPrefab;
    string validationResult = "";

    [MenuItem("Tools/DOTS/Validar Prefab de Héroe")]
    public static void ShowWindow()
    {
        var window = GetWindow<HeroPrefabDotsValidator>("Validar Hero Prefab DOTS");
        window.minSize = new Vector2(400, 200);
    }

    void OnGUI()
    {
        GUILayout.Label("Arrastra aquí el prefab del héroe para validar:", EditorStyles.boldLabel);
        heroPrefab = (GameObject)EditorGUILayout.ObjectField("Hero Prefab", heroPrefab, typeof(GameObject), false);

        if (GUILayout.Button("Validar Prefab"))
        {
            validationResult = ValidateHeroPrefab(heroPrefab);
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(validationResult, MessageType.Info);
    }

    string ValidateHeroPrefab(GameObject prefab)
    {
        if (prefab == null)
            return "[ERROR] No se ha asignado ningún prefab.";

        var sb = new StringBuilder();
        // Validación SOLO de los componentes requeridos por HeroMovementSystem
        bool ok = true;

        if (prefab.GetComponent("HeroInputAuthoring") == null)
        {
            sb.AppendLine("[ERROR] Falta HeroInputAuthoring (para HeroInputComponent)");
            ok = false;
        }
        if (prefab.GetComponent("HeroStatsAuthoring") == null)
        {
            sb.AppendLine("[ERROR] Falta HeroStatsAuthoring (para HeroStatsComponent)");
            ok = false;
        }
        if (prefab.GetComponent("HeroLifeAuthoring") == null)
        {
            sb.AppendLine("[ERROR] Falta HeroLifeAuthoring (para HeroLifeComponent)");
            ok = false;
        }
        if (prefab.GetComponent("IsLocalPlayerAuthoring") == null)
        {
            sb.AppendLine("[ERROR] Falta IsLocalPlayerAuthoring (para IsLocalPlayer)");
            ok = false;
        }
        if (prefab.GetComponent("PhysicsVelocityAuthoring") == null)
        {
            sb.AppendLine("[ERROR] Falta PhysicsVelocityAuthoring (para PhysicsVelocity)");
            ok = false;
        }
        if (prefab.GetComponent("PhysicsMassAuthoring") == null)
        {
            sb.AppendLine("[ERROR] Falta PhysicsMassAuthoring (para PhysicsMass)");
            ok = false;
        }
        if (prefab.GetComponent("LocalTransformAuthoring") == null)
        {
            sb.AppendLine("[ERROR] Falta LocalTransformAuthoring (para LocalTransform)");
            ok = false;
        }

        if (ok)
            sb.AppendLine("[OK] El prefab tiene todos los componentes requeridos por HeroMovementSystem.");

        return sb.ToString();
    }
}
