using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// MonoBehaviour that displays the desired squad formation while the
/// tactical camera mode is active.
/// </summary>
public class FormationVisualizer : MonoBehaviour
{
    EntityManager _entityManager;
    Entity _cameraEntity;
    Entity _squadEntity;

    /// <summary>Cached positions for the current formation.</summary>
    Vector3[] _positions = System.Array.Empty<Vector3>();

    /// <summary>Color used for valid target positions.</summary>
    public Color validColor = Color.green;

    /// <summary>Radius of the gizmo spheres.</summary>
    public float gizmoRadius = 0.25f;

    void Awake()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

   void Update()
    {
        if (_cameraEntity == Entity.Null || !_entityManager.Exists(_cameraEntity))
            FindCameraEntity();
        if (_squadEntity == Entity.Null || !_entityManager.Exists(_squadEntity))
            FindSquadEntity();

        bool active = false;
        if (_cameraEntity != Entity.Null && _entityManager.Exists(_cameraEntity))
        {
            var camState = _entityManager.GetComponentData<CameraStateComponent>(_cameraEntity);
            active = camState.state == CameraState.Tactical;
        }

        if (_squadEntity == Entity.Null || !_entityManager.Exists(_squadEntity))
            active = false;

        if (!active)
        {
            _positions = System.Array.Empty<Vector3>();
            return;
        }

        var units = _entityManager.GetBuffer<SquadUnitElement>(_squadEntity);
        if (units.Length == 0)
        {
            _positions = System.Array.Empty<Vector3>();
            return;
        }

        Entity leader = units[0].Value;
        if (!_entityManager.Exists(leader))
        {
            _positions = System.Array.Empty<Vector3>();
            return;
        }

        var leaderTransform = _entityManager.GetComponentData<LocalTransform>(leader);
        var state = _entityManager.GetComponentData<SquadStateComponent>(_squadEntity);
        var formationData = _entityManager.GetComponentData<SquadFormationDataComponent>(_squadEntity);
        if (!formationData.formationLibrary.IsCreated)
        {
            _positions = System.Array.Empty<Vector3>();
            return;
        }

        // ✅ Corrección: acceder a blob por referencia
        ref var formations = ref formationData.formationLibrary.Value.formations;
        ref BlobArray<float3> offsets = ref formations[0].localOffsets; 
        bool found = false;

        for (int i = 0; i < formations.Length; i++)
        {
            if (formations[i].formationType == state.currentFormation)
            {
                ref var formation = ref formations[i];
                offsets = ref formation.localOffsets; // ✅ acceso por ref
                found = true;
                break;
            }
        }

        if (!found)
        {
            _positions = System.Array.Empty<Vector3>();
            return;
        }

        int count = math.min(units.Length, offsets.Length);
        if (_positions.Length != count)
            _positions = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            _positions[i] = (Vector3)(leaderTransform.Position + offsets[i]);
        }
    }

    void OnDrawGizmos()
    {
        if (_positions == null)
            return;
        Gizmos.color = validColor;
        for (int i = 0; i < _positions.Length; i++)
            Gizmos.DrawSphere(_positions[i], gizmoRadius);
    }

    void FindCameraEntity()
    {
        var query = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CameraStateComponent>(),
            ComponentType.ReadOnly<IsLocalPlayer>());
        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        if (entities.Length > 0)
            _cameraEntity = entities[0];
    }

    void FindSquadEntity()
    {
        var query = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<SquadStateComponent>(),
            ComponentType.ReadOnly<SquadFormationDataComponent>(),
            ComponentType.ReadOnly<SquadUnitElement>(),
            ComponentType.ReadOnly<IsLocalPlayer>());
        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        if (entities.Length > 0)
            _squadEntity = entities[0];
    }
}

