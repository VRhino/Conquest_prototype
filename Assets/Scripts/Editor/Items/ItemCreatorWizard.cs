using UnityEngine;
using UnityEditor;
using Data.Items;
using BattleDrakeStudios.ModularCharacters;

namespace ItemSystem.Editor
{
    /// <summary>
    /// Herramienta para crear nuevos ítems con la estructura de carpetas organizada.
    /// Facilita la creación de ItemDataSO siguiendo las convenciones del sistema modular.
    /// </summary>
    public class ItemCreatorWizard : EditorWindow
    {
        #region Private Fields

        // Basic item info
        private string _itemId = "";
        private string _itemName = "";
        private string _description = "";
        private ItemType _itemType = ItemType.None;
        private ItemCategory _itemCategory = ItemCategory.None;
        private ItemRarity _rarity = ItemRarity.Common;
        private ArmorType _armorType = ArmorType.None;

        // Creation settings
        private string _basePath = "Assets/Resources/Items";
        private bool _createFolder = true;
        private bool _autoRegister = true;
        private EnhancedItemDatabase _targetDatabase;

        // UI state
        private Vector2 _scrollPosition;

        #endregion

        #region Menu Items

        [MenuItem("Tools/Items/Item Creator Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<ItemCreatorWizard>("Item Creator");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        [MenuItem("Assets/Create/Items/New Item (Wizard)", false, 1)]
        public static void ShowWindowFromAssets()
        {
            ShowWindow();
        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Item Creator Wizard", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawBasicInfo();
            EditorGUILayout.Space();

            DrawCreationSettings();
            EditorGUILayout.Space();

            DrawPreview();
            EditorGUILayout.Space();

            DrawCreateButton();

            EditorGUILayout.EndScrollView();
        }

        private void DrawBasicInfo()
        {
            EditorGUILayout.LabelField("Basic Information", EditorStyles.boldLabel);

            _itemId = EditorGUILayout.TextField("Item ID", _itemId);
            if (string.IsNullOrEmpty(_itemId))
            {
                EditorGUILayout.HelpBox("Item ID is required and should be unique.", MessageType.Warning);
            }
            else if (_itemId.Contains(" "))
            {
                EditorGUILayout.HelpBox("Item ID should not contain spaces. Use underscores or camelCase.", MessageType.Warning);
            }

            _itemName = EditorGUILayout.TextField("Item Name", _itemName);
            if (string.IsNullOrEmpty(_itemName))
            {
                _itemName = _itemId; // Auto-set name from ID
            }

            _description = EditorGUILayout.TextField("Description", _description, GUILayout.Height(60));

            _itemType = (ItemType)EditorGUILayout.EnumPopup("Item Type", _itemType);
            _itemCategory = (ItemCategory)EditorGUILayout.EnumPopup("Item Category", _itemCategory);
            _rarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity", _rarity);

            // Show armor type only for armor items
            if (_itemType == ItemType.Armor)
            {
                _armorType = (ArmorType)EditorGUILayout.EnumPopup("Armor Type", _armorType);
                if (_armorType == ArmorType.None)
                {
                    EditorGUILayout.HelpBox("Please select an Armor Type for armor items.", MessageType.Warning);
                }
            }
            else
            {
                _armorType = ArmorType.None; // Reset for non-armor items
            }

            // Show helpful information based on item type
            ShowItemTypeInfo();
        }

        private void ShowItemTypeInfo()
        {
            string info = "";
            MessageType messageType = MessageType.Info;

            switch (_itemType)
            {
                case ItemType.Weapon:
                    info = "Weapons require a StatGenerator for generating stats and a VisualPartId for appearance.";
                    break;
                case ItemType.Armor:
                    info = "Armor requires a StatGenerator for generating stats, VisualPartId for appearance, and ArmorType selection.";
                    break;
                case ItemType.Consumable:
                    info = "Consumables require ItemEffect arrays to define their functionality when used.";
                    break;
                case ItemType.None:
                    info = "Please select an Item Type to continue.";
                    messageType = MessageType.Warning;
                    break;
            }

            if (!string.IsNullOrEmpty(info))
            {
                EditorGUILayout.HelpBox(info, messageType);
            }
        }

        private void DrawCreationSettings()
        {
            EditorGUILayout.LabelField("Creation Settings", EditorStyles.boldLabel);

            _basePath = EditorGUILayout.TextField("Base Path", _basePath);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Browse"))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select Base Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    _basePath = FileUtil.GetProjectRelativePath(selectedPath);
                }
            }
            EditorGUILayout.EndHorizontal();

            _createFolder = EditorGUILayout.Toggle("Create Item Folder", _createFolder);
            _autoRegister = EditorGUILayout.Toggle("Auto-Register in Database", _autoRegister);

            if (_autoRegister)
            {
                _targetDatabase = (EnhancedItemDatabase)EditorGUILayout.ObjectField(
                    "Target Database", _targetDatabase, typeof(EnhancedItemDatabase), false);

                if (_targetDatabase == null)
                {
                    EditorGUILayout.HelpBox("Please select an EnhancedItemDatabase for auto-registration.", MessageType.Info);
                }
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(_itemId))
            {
                EditorGUILayout.HelpBox("Enter an Item ID to see preview.", MessageType.Info);
                return;
            }

            string targetPath = GetTargetPath();
            EditorGUILayout.LabelField("Will create at:", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(targetPath, EditorStyles.textField, GUILayout.Height(18));

            // Check if file already exists
            if (System.IO.File.Exists(targetPath))
            {
                EditorGUILayout.HelpBox("File already exists! Creation will overwrite the existing file.", MessageType.Warning);
            }

            // Validation
            var validation = ValidateInput();
            if (validation.Count > 0)
            {
                EditorGUILayout.LabelField("Issues:", EditorStyles.miniBoldLabel);
                foreach (string issue in validation)
                {
                    EditorGUILayout.HelpBox(issue, MessageType.Warning);
                }
            }
        }

        private void DrawCreateButton()
        {
            var validation = ValidateInput();
            bool canCreate = validation.Count == 0 && !string.IsNullOrEmpty(_itemId);

            EditorGUI.BeginDisabledGroup(!canCreate);

            if (GUILayout.Button("Create Item", GUILayout.Height(30)))
            {
                CreateItem();
            }

            EditorGUI.EndDisabledGroup();

            if (!canCreate && !string.IsNullOrEmpty(_itemId))
            {
                EditorGUILayout.HelpBox("Please resolve the issues above before creating the item.", MessageType.Error);
            }
        }

        #endregion

        #region Creation Logic

        private void CreateItem()
        {
            try
            {
                string targetPath = GetTargetPath();

                // Create directory if needed
                if (_createFolder)
                {
                    string directory = System.IO.Path.GetDirectoryName(targetPath);
                    if (!System.IO.Directory.Exists(directory))
                    {
                        System.IO.Directory.CreateDirectory(directory);
                    }
                }

                // Create ItemDataSO
                var itemDataSO = CreateInstance<ItemDataSO>();
                
                // Set basic properties through SerializedObject
                SetItemProperties(itemDataSO);

                // Create asset
                AssetDatabase.CreateAsset(itemDataSO, targetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Verificar que el ítem se configuró correctamente
                if (!VerifyItemCreation(itemDataSO))
                {
                    Debug.LogWarning($"[ItemCreatorWizard] Item created but some properties may not have been set correctly: {_itemId}");
                }

                // Auto-register if enabled
                if (_autoRegister && _targetDatabase != null)
                {
                    _targetDatabase.AddItem(itemDataSO);
                    EditorUtility.SetDirty(_targetDatabase);
                }

                // Select the created asset
                Selection.activeObject = itemDataSO;
                EditorGUIUtility.PingObject(itemDataSO);

                Debug.Log($"[ItemCreatorWizard] Created item: {_itemId} at {targetPath}");

                // Reset form
                if (EditorUtility.DisplayDialog("Item Created", 
                    $"Item '{_itemName}' created successfully!\n\nCreate another item?", 
                    "Yes", "Close"))
                {
                    ResetForm();
                }
                else
                {
                    Close();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ItemCreatorWizard] Failed to create item: {ex.Message}");
                EditorUtility.DisplayDialog("Creation Failed", $"Failed to create item: {ex.Message}", "OK");
            }
        }

        private void SetItemProperties(ItemDataSO itemDataSO)
        {
            // Usar SerializedObject para establecer los campos privados
            var serializedObject = new SerializedObject(itemDataSO);
            
            try
            {
                // Establecer propiedades básicas
                SetSerializedProperty(serializedObject, "_id", _itemId);
                SetSerializedProperty(serializedObject, "_name", _itemName);
                SetSerializedProperty(serializedObject, "_description", _description);
                SetSerializedProperty(serializedObject, "_rarity", (int)_rarity);
                SetSerializedProperty(serializedObject, "_itemType", (int)_itemType);
                SetSerializedProperty(serializedObject, "_itemCategory", (int)_itemCategory);
                SetSerializedProperty(serializedObject, "_armorType", (int)_armorType);
                
                // Configurar propiedades por defecto según el tipo
                bool isConsumable = _itemType == ItemType.Consumable;
                SetSerializedProperty(serializedObject, "_stackable", isConsumable);
                SetSerializedProperty(serializedObject, "_consumeOnUse", isConsumable);
                SetSerializedProperty(serializedObject, "_requiresConfirmation", false);
                SetSerializedProperty(serializedObject, "_useButtonText", "Use");
                
                // Limpiar campos opcionales (usuario los configurará manualmente)
                SetSerializedPropertyObject(serializedObject, "_Icon", null);
                SetSerializedProperty(serializedObject, "_visualPartId", "");
                SetSerializedPropertyObject(serializedObject, "_statGenerator", null);
                SetSerializedPropertyObject(serializedObject, "_pricingConfig", null);
                
                // Configurar array de efectos
                var effectsProperty = serializedObject.FindProperty("_effects");
                if (effectsProperty != null)
                {
                    effectsProperty.ClearArray();
                    // Para consumibles, el array queda vacío pero disponible para agregar efectos
                }
                
                // Aplicar cambios
                serializedObject.ApplyModifiedProperties();
                
                Debug.Log($"[ItemCreatorWizard] Properties set successfully for item: {_itemId} (Type: {_itemType})");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ItemCreatorWizard] Failed to set properties for item {_itemId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Helper method para establecer propiedades serializadas de forma segura
        /// </summary>
        private void SetSerializedProperty(SerializedObject serializedObject, string propertyName, object value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"[ItemCreatorWizard] Property '{propertyName}' not found in ItemDataSO");
                return;
            }

            switch (value)
            {
                case string stringValue:
                    property.stringValue = stringValue;
                    break;
                case int intValue:
                    property.enumValueIndex = intValue;
                    break;
                case bool boolValue:
                    property.boolValue = boolValue;
                    break;
                default:
                    Debug.LogWarning($"[ItemCreatorWizard] Unsupported property type for '{propertyName}': {value?.GetType()}");
                    break;
            }
        }

        /// <summary>
        /// Helper method para establecer referencias de objeto serializadas
        /// </summary>
        private void SetSerializedPropertyObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"[ItemCreatorWizard] Object property '{propertyName}' not found in ItemDataSO");
                return;
            }
            
            property.objectReferenceValue = value;
        }

        /// <summary>
        /// Verifica que el ItemDataSO se haya configurado correctamente
        /// </summary>
        private bool VerifyItemCreation(ItemDataSO itemDataSO)
        {
            bool isValid = true;

            // Verificar propiedades básicas
            if (itemDataSO.id != _itemId)
            {
                Debug.LogError($"[ItemCreatorWizard] ID verification failed. Expected: {_itemId}, Got: {itemDataSO.id}");
                isValid = false;
            }

            if (itemDataSO.name != _itemName)
            {
                Debug.LogError($"[ItemCreatorWizard] Name verification failed. Expected: {_itemName}, Got: {itemDataSO.name}");
                isValid = false;
            }

            if (itemDataSO.itemType != _itemType)
            {
                Debug.LogError($"[ItemCreatorWizard] ItemType verification failed. Expected: {_itemType}, Got: {itemDataSO.itemType}");
                isValid = false;
            }

            if (itemDataSO.rarity != _rarity)
            {
                Debug.LogError($"[ItemCreatorWizard] Rarity verification failed. Expected: {_rarity}, Got: {itemDataSO.rarity}");
                isValid = false;
            }

            // Verificar configuración específica por tipo
            bool expectedStackable = _itemType == ItemType.Consumable;
            if (itemDataSO.stackable != expectedStackable)
            {
                Debug.LogWarning($"[ItemCreatorWizard] Stackable property may not be set correctly. Expected: {expectedStackable}, Got: {itemDataSO.stackable}");
            }

            if (isValid)
            {
                Debug.Log($"[ItemCreatorWizard] Item verification passed for: {_itemId}");
            }

            return isValid;
        }

        private string GetTargetPath()
        {
            if (_createFolder)
            {
                string categoryFolder = GetCategoryFolder();
                return System.IO.Path.Combine(_basePath, categoryFolder, _itemId, $"{_itemId}.asset");
            }
            else
            {
                return System.IO.Path.Combine(_basePath, $"{_itemId}.asset");
            }
        }

        private string GetCategoryFolder()
        {
            switch (_itemType)
            {
                case ItemType.Weapon:
                    return "weapons";
                case ItemType.Armor:
                    return "armors";
                case ItemType.Consumable:
                    return "consumables";
                default:
                    return "misc";
            }
        }

        private System.Collections.Generic.List<string> ValidateInput()
        {
            var issues = new System.Collections.Generic.List<string>();

            // Validar ID
            if (string.IsNullOrEmpty(_itemId)) 
                issues.Add("Item ID is required");
            else if (_itemId.Contains(" ")) 
                issues.Add("Item ID should not contain spaces");
            else if (!System.Text.RegularExpressions.Regex.IsMatch(_itemId, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
                issues.Add("Item ID should start with a letter and contain only letters, numbers, and underscores");

            // Validar tipo
            if (_itemType == ItemType.None) 
                issues.Add("Please select an Item Type");

            // Validar categoría (no requerida para consumibles)
            if (_itemCategory == ItemCategory.None && _itemType != ItemType.Consumable) 
                issues.Add("Please select an Item Category");

            // Validar tipo de armadura para armaduras
            if (_itemType == ItemType.Armor && _armorType == ArmorType.None) 
                issues.Add("Please select an Armor Type for armor items");

            // Validar database para auto-registro
            if (_autoRegister && _targetDatabase == null) 
                issues.Add("Please select a target database for auto-registration");

            // Validar nombre
            if (string.IsNullOrEmpty(_itemName))
                issues.Add("Item Name cannot be empty");

            return issues;
        }

        private void ResetForm()
        {
            _itemId = "";
            _itemName = "";
            _description = "";
            _itemType = ItemType.None;
            _itemCategory = ItemCategory.None;
            _rarity = ItemRarity.Common;
            _armorType = ArmorType.None;
        }

        #endregion
    }
}