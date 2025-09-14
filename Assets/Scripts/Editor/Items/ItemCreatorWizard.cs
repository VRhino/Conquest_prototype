using UnityEngine;
using UnityEditor;
using Data.Items;

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
                
                // Set basic properties through reflection or direct assignment
                SetItemProperties(itemDataSO);

                // Create asset
                AssetDatabase.CreateAsset(itemDataSO, targetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

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
            // Create a temporary ItemData to use the conversion method
            var tempItemData = new ItemDataSO
            {
                // id = _itemId,
                // name = _itemName,
                // description = _description,
                // itemType = _itemType,
                // itemCategory = _itemCategory,
                // rarity = _rarity,
                // armorType = _armorType,
                // stackable = _itemType == ItemType.Consumable, // Default stackable for consumables
                // consumeOnUse = _itemType == ItemType.Consumable,
                // iconPath = "", // User will need to set this manually
                // visualPartId = "", // User will need to set this manually
                // statGenerator = null, // User will need to set this manually
                // effects = null, // User will need to set this manually
                // requiresConfirmation = false,
                // useButtonText = "Use",
                // pricingConfig = null
            };

            EditorUtility.CopySerialized(tempItemData, itemDataSO);
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

            if (string.IsNullOrEmpty(_itemId))
            {
                issues.Add("Item ID is required");
            }
            else if (_itemId.Contains(" "))
            {
                issues.Add("Item ID should not contain spaces");
            }

            if (_itemType == ItemType.None)
            {
                issues.Add("Please select an Item Type");
            }

            if (_itemCategory == ItemCategory.None)
            {
                issues.Add("Please select an Item Category");
            }

            if (_itemType == ItemType.Armor && _armorType == ArmorType.None)
            {
                issues.Add("Please select an Armor Type for armor items");
            }

            if (_autoRegister && _targetDatabase == null)
            {
                issues.Add("Please select a target database for auto-registration");
            }

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