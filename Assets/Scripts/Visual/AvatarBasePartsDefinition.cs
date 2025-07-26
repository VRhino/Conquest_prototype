using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Avatar/Avatar Base Parts Definition", fileName = "DefaultAvatarBasePartsDefinition")]
public class AvatarBasePartsDefinition : ScriptableObject
{
    [Tooltip("Lista de IDs de partes base para el dummy/avatar.")]
    public List<string> basePartIds = new List<string>();
}
