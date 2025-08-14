using System.Collections.Generic;
using UnityEngine;
using Data.Items; // Para acceder a ItemType

namespace Data.Avatar
{
    public static class AvatarVisualUtils
    {
        // Desactiva todas las piezas de armadura y deja solo las piezas base activas según la lista basePartIds
        public static void ResetModularDummyToBase(
            Transform modularDummy,
            AvatarPartDatabase avatarPartDatabase,
            List<string> extraBasePartIds,
            global::Gender currentGender)
        {
            modularDummy.GetComponent<AvatarPartSelector>().maleParts.SetActive(currentGender == Gender.Male);
            modularDummy.GetComponent<AvatarPartSelector>().femaleParts.SetActive(currentGender == Gender.Female);
            var basePartsDef = Resources.Load<AvatarBasePartsDefinition>("Data/Avatar/DefaultAvatarBasePartsDefinition");

            if (modularDummy == null || avatarPartDatabase == null || basePartsDef == null) return;
            var allBasePartIds = new List<string>(basePartsDef.basePartIds);
            if (extraBasePartIds != null)
                allBasePartIds.AddRange(extraBasePartIds);

            // 1. Reunir todos los boneTargets únicos de las piezas base
            var boneTargets = new HashSet<string>();
            var baseAttachments = new List<(string boneTarget, string prefabName)>();
            foreach (var id in allBasePartIds)
            {
                var part = FindAvatarPartById(avatarPartDatabase, id);
                if (part == null || part.attachments == null) continue;
                foreach (var att in part.attachments)
                {
                    string boneTarget = currentGender == Gender.Male ? att.boneTargetMale : att.boneTargetFemale;
                    string prefabName = currentGender == Gender.Male ? att.prefabPathMale : att.prefabPathFemale;
                    if (!string.IsNullOrEmpty(boneTarget)) boneTargets.Add(boneTarget);
                    if (!string.IsNullOrEmpty(boneTarget) && !string.IsNullOrEmpty(prefabName))
                        baseAttachments.Add((boneTarget, prefabName));
                }
            }

            // 2. Desactivar todos los hijos de los boneTargets relevantes
            foreach (var boneTarget in boneTargets)
            {
                var boneTransform = modularDummy.Find(boneTarget) ?? FindDeepChild(modularDummy, boneTarget);
                if (boneTransform == null) continue;
                foreach (Transform child in boneTransform)
                    child.gameObject.SetActive(false);
            }

            // 3. Activar solo los visualAttachments de las piezas base
            foreach (var (boneTarget, prefabName) in baseAttachments)
            {
                var boneTransform = modularDummy.Find(boneTarget) ?? FindDeepChild(modularDummy, boneTarget);
                if (boneTransform == null) continue;
                foreach (Transform child in boneTransform)
                {
                    child.gameObject.SetActive(child.name == prefabName);
                }
            }
        }
         // Busca una pieza en la base de datos por id (en todas las listas)
        public static AvatarPartDefinition FindAvatarPartById(AvatarPartDatabase avatarPartDatabase, string id)
        {
            if (avatarPartDatabase == null || string.IsNullOrEmpty(id)) return null;
            // Buscar en todas las listas relevantes
            List<List<AvatarPartDefinition>> allLists = new() {
                avatarPartDatabase.torsoParts,
                avatarPartDatabase.pantsParts,
                avatarPartDatabase.bootsParts,
                avatarPartDatabase.glovesParts,
                avatarPartDatabase.headParts,
                avatarPartDatabase.faceParts,
                avatarPartDatabase.hairParts,
                avatarPartDatabase.eyebrowsParts,
                avatarPartDatabase.beardParts,
                avatarPartDatabase.weaponParts
            };
            foreach (var list in allLists)
            {
                if (list == null) continue;
                foreach (var part in list)
                {
                    if (part != null && part.id == id)
                        return part;
                }
            }
            return null;
        }

        // Búsqueda recursiva de un hijo por nombre
        public static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name)) return null;
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                var result = FindDeepChild(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        // Activa la pieza de armadura por id usando avatarPartDatabase, boneTarget y prefabPath
        public static void ToggleArmorVisibilityByAvatarPartId(Transform modularDummy, AvatarPartDatabase avatarPartDatabase, string partId, global::Gender currentGender)
        {
            if (modularDummy == null)
            {
                Debug.LogWarning("No se asignó la referencia a modularDummy en AvatarVisualUtils");
                return;
            }
            if (avatarPartDatabase == null)
            {
                Debug.LogWarning("No se asignó la referencia a avatarPartDatabase en AvatarVisualUtils");
                return;
            }

            var partDef = FindAvatarPartById(avatarPartDatabase, partId);
            if (partDef == null)
            {
                Debug.LogWarning($"No se encontró la pieza {partId} en la base de datos");
                return;
            }
            if (partDef.attachments == null || partDef.attachments.Count == 0)
            {
                Debug.LogWarning($"La pieza {partId} no tiene visualAttachments");
                return;
            }

            foreach (var attachment in partDef.attachments)
            {
                string boneTarget = currentGender == global::Gender.Male ? attachment.boneTargetMale : attachment.boneTargetFemale;
                string prefabName = currentGender == global::Gender.Male ? attachment.prefabPathMale : attachment.prefabPathFemale;
                if (string.IsNullOrEmpty(boneTarget) || string.IsNullOrEmpty(prefabName))
                {
                    Debug.LogWarning($"Attachment de {partId} no tiene boneTarget o prefabPath para el género actual");
                    continue;
                }
                var boneTransform = modularDummy.Find(boneTarget) ?? FindDeepChild(modularDummy, boneTarget);
                if (boneTransform == null)
                {
                    Debug.LogWarning($"No se encontró el boneTarget '{boneTarget}' en modularDummy para {partId}");
                    continue;
                }
                bool found = false;
                foreach (Transform child in boneTransform)
                {
                    bool activate = child.name == prefabName;
                    child.gameObject.SetActive(activate);
                    if (activate) found = true;
                }
                if (!found)
                {
                    Debug.LogWarning($"No se encontró el prefab '{prefabName}' dentro de '{boneTarget}' para {partId}");
                }
            }
        }

        /// <summary>
        /// Desequipa visualmente un slot específico, dejándolo en estado base o vacío.
        /// </summary>
        /// <param name="modularDummy">Transform del modelo modular</param>
        /// <param name="avatarPartDatabase">Base de datos de partes del avatar</param>
        /// <param name="slotType">Tipo de slot a desequipar</param>
        /// <param name="currentGender">Género actual</param>
        /// <param name="heroData">Datos del héroe para obtener partes base</param>
        public static void UnequipSlotVisual(Transform modularDummy, 
            AvatarPartDatabase avatarPartDatabase, ItemType slotType, 
            global::Gender currentGender, HeroData heroData)
        {
            if (modularDummy == null || avatarPartDatabase == null || heroData == null)
            {
                Debug.LogWarning("UnequipSlotVisual: Parámetros nulos detectados");
                return;
            }
                
            // Obtener el ítem base para este slot según el héroe
            string basePartId = GetBasePartIdForSlot(slotType, heroData);
            
            if (!string.IsNullOrEmpty(basePartId))
            {
                // Activar la parte base del avatar
                Debug.Log($"Activating base part for slot {slotType}: {basePartId}");
                ToggleArmorVisibilityByAvatarPartId(modularDummy, avatarPartDatabase, basePartId, currentGender);
            }
            else
            {
                // No hay parte base específica, desactivar todas las partes del slot
                Debug.Log($"No base part found for slot {slotType}, disabling all parts in slot");
                DisableAllPartsInSlot(modularDummy, slotType, avatarPartDatabase, currentGender);
            }
        }
        
        /// <summary>
        /// Obtiene el ID de la parte base para un slot específico desde los datos del héroe.
        /// </summary>
        /// <param name="slotType">Tipo de slot</param>
        /// <param name="heroData">Datos del héroe</param>
        /// <returns>ID de la parte base o string vacío</returns>
        private static string GetBasePartIdForSlot(ItemType slotType, HeroData heroData)
        {
            // Mapear slots de equipamiento a las partes base del avatar del héroe
            return slotType switch
            {
                ItemType.Helmet => heroData.avatar.headId, // Puede ser la cabeza base
                ItemType.Torso => "", // Torso base suele estar integrado en el modelo
                ItemType.Gloves => "", // Manos base suelen estar integradas
                ItemType.Pants => "", // Piernas base suelen estar integradas  
                ItemType.Boots => "", // Pies base suelen estar integrados
                ItemType.Weapon => "", // Las armas no tienen "base", simplemente se desactivan
                _ => ""
            };
        }
        
        /// <summary>
        /// Desactiva todas las partes visuales de un slot específico.
        /// </summary>
        /// <param name="modularDummy">Transform del modelo modular</param>
        /// <param name="slotType">Tipo de slot</param>
        /// <param name="database">Base de datos de partes</param>
        /// <param name="gender">Género actual</param>
        private static void DisableAllPartsInSlot(Transform modularDummy, ItemType slotType, 
            AvatarPartDatabase database, global::Gender gender)
        {
            // Obtener todas las partes del slot y desactivarlas
            var partsForSlot = GetPartsListForSlot(database, slotType);
            if (partsForSlot == null) 
            {
                Debug.LogWarning($"No parts list found for slot type: {slotType}");
                return;
            }
            
            foreach (var part in partsForSlot)
            {
                if (part?.attachments == null) continue;
                
                foreach (var attachment in part.attachments)
                {
                    string boneTarget = gender == global::Gender.Male ? attachment.boneTargetMale : attachment.boneTargetFemale;
                    string prefabName = gender == global::Gender.Male ? attachment.prefabPathMale : attachment.prefabPathFemale;
                    
                    if (string.IsNullOrEmpty(boneTarget) || string.IsNullOrEmpty(prefabName)) continue;
                    
                    var boneTransform = modularDummy.Find(boneTarget) ?? FindDeepChild(modularDummy, boneTarget);
                    if (boneTransform == null) continue;
                    
                    foreach (Transform child in boneTransform)
                    {
                        if (child.name == prefabName)
                        {
                            child.gameObject.SetActive(false);
                            Debug.Log($"Disabled part: {prefabName} in bone: {boneTarget}");
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Obtiene la lista de partes correspondiente a un tipo de slot.
        /// </summary>
        /// <param name="database">Base de datos de partes</param>
        /// <param name="slotType">Tipo de slot</param>
        /// <returns>Lista de partes o null</returns>
        private static List<AvatarPartDefinition> GetPartsListForSlot(AvatarPartDatabase database, ItemType slotType)
        {
            return slotType switch
            {
                ItemType.Helmet => database.headParts,
                ItemType.Torso => database.torsoParts,
                ItemType.Gloves => database.glovesParts,
                ItemType.Pants => database.pantsParts,
                ItemType.Boots => database.bootsParts,
                ItemType.Weapon => database.weaponParts,
                _ => null
            };
        }
    }
}
