using Unity.Entities;
using UnityEngine;

public class SquadDatabaseAuthoring : MonoBehaviour
{
    public SquadDatabase database;

    class Baker : SquadDefinitionBaker<SquadDatabaseAuthoring>
    {
        public override void Bake(SquadDatabaseAuthoring authoring)
        {
            if (authoring.database == null) return;
            DependsOn(authoring.database);
            if (authoring.database.allSquads == null) return;
            foreach (var data in authoring.database.allSquads)
            {
                if (data == null || string.IsNullOrEmpty(data.id)) continue;
                BakeSquad(CreateAdditionalEntity(TransformUsageFlags.None, false, data.id), data);
            }
        }
    }
}
