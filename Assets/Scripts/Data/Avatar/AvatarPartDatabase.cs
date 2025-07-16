using System.Collections.Generic;
using UnityEngine;

namespace Data.Avatar
{
    [CreateAssetMenu(menuName = "Avatar/Avatar Part Database")]
    public class AvatarPartDatabase : ScriptableObject
    {
        //body parts
        public List<AvatarPartDefinition> faceParts;
        public List<AvatarPartDefinition> hairParts;
        public List<AvatarPartDefinition> eyebrowsParts;
        public List<AvatarPartDefinition> beardParts;

        //equipment parts
        public List<AvatarPartDefinition> torsoParts;
        public List<AvatarPartDefinition> glovesParts;
        public List<AvatarPartDefinition> pantsParts;
        public List<AvatarPartDefinition> headParts;
        public List<AvatarPartDefinition> bootsParts;
    }
}
