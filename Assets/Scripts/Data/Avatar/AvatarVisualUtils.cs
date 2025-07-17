using System.Collections.Generic;
using UnityEngine;

namespace Data.Avatar
{
    public static class AvatarVisualUtils
    {
        // Desactiva todas las piezas de armadura y deja solo las piezas base activas según la lista basePartIds
        public static void ResetModularDummyToBase(Transform modularDummy, AvatarPartDatabase avatarPartDatabase, List<string> basePartIds, global::Gender currentGender)
        {
            if (modularDummy == null || avatarPartDatabase == null) return;

            // 1. Reunir todos los boneTargets únicos de las piezas base
            var boneTargets = new HashSet<string>();
            var baseAttachments = new List<(string boneTarget, string prefabName)>();
            foreach (var id in basePartIds)
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
        public static void ActivateArmorPieceById(Transform modularDummy, AvatarPartDatabase avatarPartDatabase, string partId, global::Gender currentGender)
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
    }
}
