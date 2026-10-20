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

        // Slot targets already follow the anchor (GridFormationUpdateSystem); show them as they are.
        if (_positions.Length != units.Length)
            _positions = new Vector3[units.Length];
        for (int i = 0; i < units.Length; i++)
        {
            Entity unit = units[i].Value;
            _positions[i] = _entityManager.Exists(unit) && _entityManager.HasComponent<UnitTargetPositionComponent>(unit)
                ? (Vector3)_entityManager.GetComponentData<UnitTargetPositionComponent>(unit).position
                : Vector3.zero;
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
            ComponentType.ReadOnly<SquadDataComponent>(),
            ComponentType.ReadOnly<SquadUnitElement>(),
            ComponentType.ReadOnly<IsLocalPlayer>());
        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        if (entities.Length > 0)
            _squadEntity = entities[0];
    }
}

