using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Crea AC_Unit_Combat.controller duplicando AC_Polygon_Masculine y añadiendo
/// un layer "Combat" con soporte para IsAttacking.
/// Menú: Conquest → Create Unit Combat Animator Controller
/// </summary>
public static class CreateUnitCombatController
{
    private const string SourceControllerPath =
        "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/AC_Polygon_Masculine.controller";
    private const string DestinationPath =
        "Assets/Resources/Squads/AC_Unit_Combat.controller";

    [MenuItem("Conquest/Create Unit Combat Animator Controller")]
    public static void Create()
    {
        // 1. Duplicar el controller base
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(DestinationPath) != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "Controller ya existe",
                    $"Ya existe un controller en:\n{DestinationPath}\n\n¿Sobreescribir?",
                    "Sí", "Cancelar"))
                return;
            AssetDatabase.DeleteAsset(DestinationPath);
        }

        if (!AssetDatabase.CopyAsset(SourceControllerPath, DestinationPath))
        {
            Debug.LogError($"[CreateUnitCombatController] No se pudo copiar desde:\n{SourceControllerPath}");
            return;
        }

        // 2. Cargar la copia
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DestinationPath);
        if (controller == null)
        {
            Debug.LogError("[CreateUnitCombatController] No se pudo cargar el controller copiado.");
            return;
        }

        // 3. Añadir parámetro IsAttacking si no existe
        bool hasParam = false;
        foreach (var p in controller.parameters)
        {
            if (p.name == "IsAttacking") { hasParam = true; break; }
        }
        if (!hasParam)
            controller.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);

        // 4. Añadir layer "Combat" (Override, peso 1)
        var stateMachine = new AnimatorStateMachine
        {
            name       = "Combat",
            hideFlags  = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(stateMachine, controller);

        var combatLayer = new AnimatorControllerLayer
        {
            name          = "Combat",
            defaultWeight = 1f,
            blendingMode  = AnimatorLayerBlendingMode.Override,
            stateMachine  = stateMachine
        };
        controller.AddLayer(combatLayer);

        // Obtener la state machine de la layer recién añadida
        var layers = controller.layers;
        var sm = layers[layers.Length - 1].stateMachine;

        // 5. Estado "Idle_Combat" — default, sin clip (la layer no hace nada en reposo)
        var idleState   = sm.AddState("Idle_Combat");
        sm.defaultState = idleState;

        // 6. Estado "Attack" — asignar clip de ataque en Unity Editor
        var attackState = sm.AddState("Attack");

        // 7. Any State → Attack cuando IsAttacking = true
        var toAttack = sm.AddAnyStateTransition(attackState);
        toAttack.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
        toAttack.hasExitTime         = false;
        toAttack.duration            = 0.1f;
        toAttack.canTransitionToSelf = false;

        // 8. Attack → Idle_Combat cuando IsAttacking = false
        var toIdle = attackState.AddTransition(idleState);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
        toIdle.hasExitTime = false;
        toIdle.duration    = 0.1f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CreateUnitCombatController] Controller creado en: {DestinationPath}\n" +
                  "Próximo paso: asignar clip de ataque al estado 'Attack' en la layer 'Combat'.");

        // Seleccionar el asset recién creado en el Project window
        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);
    }
}
