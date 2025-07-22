using System;
using System.Collections.Generic;

namespace Data.Avatar
{
    [Serializable]
    public class AvatarPartDefinition
    {
        public string id;
        public string displayName;
        public AvatarSlot slot;
        public List<VisualAttachment> attachments;
    }
}
