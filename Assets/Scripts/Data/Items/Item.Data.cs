using System;

namespace Data.Items
{
    [Serializable]
    public class ItemData
    {
        public string id;
        public string name;
        public string visualPartId; // Referencia a AvatarPartDefinition por ID
        // ...otros campos...
    }
}
