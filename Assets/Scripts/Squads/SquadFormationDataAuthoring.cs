// This file is now obsolete - formations are handled directly in SquadData.cs
// and baked in SquadDataAuthoring.cs for a unified, simpler system.
//
// Migration: Move your GridFormationScriptableObject[] from this component
// to the SquadData.gridFormations field instead.

/*
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// OBSOLETE: This component has been replaced by direct integration in SquadData.
/// Use SquadData.gridFormations instead.
/// </summary>
[System.Obsolete("Use SquadData.gridFormations instead of this separate component")]
public class SquadFormationDataAuthoring : MonoBehaviour
{
    [Header("Grid-Based Formations")]
    [Tooltip("This is now obsolete. Use SquadData.gridFormations instead.")]
    public GridFormationScriptableObject[] gridFormations;
}
*/
