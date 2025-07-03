# Grid-Based Formation System - Setup Guide

## Overview
The squad formation system now uses **exclusively grid-based formations**. All formations use a local grid system where each cell is 1x1 meters, providing consistent unit placement and eliminating overlapping issues.

## How to Setup Grid-Based Formations

### 1. Creating Grid Formation Assets

#### Using the Menu (Recommended)
1. Go to `Tools > Formations` in the Unity menu
2. Select one of the pre-made formation types:
   - **Create Line Formation (Grid)** - Units form a line behind the leader
   - **Create Column Formation (Grid)** - Units form a single file column
   - **Create Wedge Formation (Grid)** - Units form a triangle/wedge shape
   - **Create Square Formation (Grid)** - Units form a square pattern

#### Manual Creation
1. Right-click in Project window
2. Select `Create > Formations > Grid Formation Pattern`
3. Configure the formation:
   - **Formation Type**: Choose from Line, Column, Wedge, Square, Dispersed, Testudo
   - **Grid Positions**: Define where each unit should be positioned
     - Positions are absolute grid coordinates (e.g., X:2-8, Y:3-4)
     - System automatically calculates center where hero will be positioned
     - Units are positioned relative to the calculated center
     - Positive X = right, Negative X = left
     - Positive Y = behind, Negative Y = in front

### 2. Assigning Formations to Squads

1. Find your squad's `SquadFormationDataAuthoring` component
2. Configure the formation settings:
   - **Grid Formations**: Assign your `GridFormationScriptableObject` assets

### 3. Formation Array Order
The formations in the array correspond to keyboard inputs:
- **Index 0** (F1): First formation in the array (also default formation)
- **Index 1** (F2): Second formation in the array  
- **Index 2** (F3): Third formation in the array
- **Index 3** (F4): Fourth formation in the array

## Grid System Details

### Grid Cell Specifications
- **Cell Width**: 1.0 meters (X-axis)
- **Cell Depth**: 1.0 meters (Z-axis)
- **Hero Position**: Automatically calculated at the center of the formation
- **Unit Positions**: Relative to the formation center (where hero is positioned)

### Grid Coordinates and Formation Center
- **X-axis**: Left (-) to Right (+)
- **Y-axis**: Forward (-) to Back (+)
- **Hero Position**: Always at the true center of the formation
- **Center Calculation**: The center is calculated as the geometric midpoint of all unit positions
  - For grids with odd dimensions: Center is the exact middle cell
  - For grids with even dimensions: Center is rounded to the nearest integer coordinate
  - Example: Line formation X(2-8), Y(3-4) → Center = (5, 4)
  - Example: Testudo formation X(2-6), Y(4-6) → Center = (4, 5)

### Formation Design Guidelines
When creating formations, consider:
1. **Balance**: Units should be roughly balanced around where the hero will be positioned
2. **Tactical Purpose**: Formation shape should serve the intended tactical role
3. **Grid Efficiency**: Use available space efficiently without too much empty space
4. **Hero Safety**: Consider hero positioning relative to threat direction

#### Example Formations
**Line Formation (10x2 effective area):**
- Units at: X(2,3,4,6,7,8), Y(3,4) - skipping center X(5) for hero
- Hero positioned at center: (5, 4)
- Tactical purpose: Broad front, maximum firepower

**Testudo Formation (5x3 effective area):**
- Units at: X(2,3,5,6), Y(4,5,6) - skipping center for hero at (4,5)
- Hero positioned at center: (4, 5)
- Tactical purpose: Protection, defensive formation

### Formation Center Auto-Calculation Process
1. **System analyzes** all unit positions in the formation
2. **Calculates bounds**: min/max X and Y coordinates
3. **Computes center**: midpoint of bounds, rounded to nearest integer
4. **Hero positioned**: at this calculated center point
5. **Units positioned**: relative to hero's center position

### Formation Examples (After Center Calculation)

#### Line Formation (Grid positions X:2-8, Y:3-4 → Center: 5,4)
```
Hero at calculated center (5,4), units positioned relative:
- Hero:  (0, 0)    - At formation center
- Unit1: (-3, 0)   - 3m left of hero  (grid: 2,4)
- Unit2: (-2, 0)   - 2m left of hero  (grid: 3,4)  
- Unit3: (-1, 0)   - 1m left of hero  (grid: 4,4)
- Unit4: (1, 0)    - 1m right of hero (grid: 6,4)
- Unit5: (2, 0)    - 2m right of hero (grid: 7,4)
- Unit6: (3, 0)    - 3m right of hero (grid: 8,4)
- Unit7: (-3, -1)  - 3m left, 1m forward (grid: 2,3)
- ... etc
```

#### Testudo Formation (Grid positions X:2-6, Y:4-6 → Center: 4,5)
```
Hero at calculated center (4,5), units positioned relative:
- Hero:  (0, 0)    - At formation center
- Unit1: (-1, 1)   - 1m left, 1m back   (grid: 3,6)
- Unit2: (0, 1)    - At hero X, 1m back  (grid: 4,6)
- Unit3: (1, 1)    - 1m right, 1m back  (grid: 5,6)
- Unit4: (-2, 0)   - 2m left of hero    (grid: 2,5)
- Unit5: (-1, 0)   - 1m left of hero    (grid: 3,5)
- Unit6: (1, 0)    - 1m right of hero   (grid: 5,5)
- Unit7: (2, 0)    - 2m right of hero   (grid: 6,5)
- ... etc
```
- etc...
```

#### Testudo Formation (Original: X:3-6, Y:3-5)
```
After centering (Hero at calculated center):
- Hero:  (0, 0)    - At formation center
- Unit1: (-1, -1)  - Left front of hero
- Unit2: (0, -1)   - Front of hero
- Unit3: (1, -1)   - Right front of hero
- Unit4: (2, -1)   - Far right front
- Unit5: (-1, 0)   - Left of hero
- Unit6: (0, 0)    - Same position as hero (avoid this)
- Unit7: (1, 0)    - Right of hero
- Unit8: (2, 0)    - Far right of hero
- etc...
```

#### Column Formation
```
Grid Positions:
- Hero:  (0, 0)
- Unit1: (0, 1) - Directly behind
- Unit2: (0, 2) - Further behind
- Unit3: (0, 3) - Even further behind
```

#### Wedge Formation
```
Grid Positions:
- Hero:  (0, 0)  - At the tip
- Unit1: (-1, 1) - Left side
- Unit2: (1, 1)  - Right side
- Unit3: (-2, 2) - Far left
- Unit4: (2, 2)  - Far right
```

## Testing and Validation

### Using the Grid Formation Tester
1. Add the `GridFormationTester` component to any GameObject in your scene
2. Assign your grid formation assets to the **Test Formations** array
3. Check **Run Tests** to automatically test on scene start
4. Use the context menu "Run Grid System Tests" to test manually

### What the Tester Checks
- Grid to world coordinate conversions
- World to grid coordinate conversions (round-trip validation)
- Position snapping functionality
- Formation asset validation
- Unit positioning accuracy

### Visual Debugging
- Select the GameObject with `GridFormationTester` in the scene
- The grid will be visualized with cyan wireframes
- Formation positions will be shown as green spheres
- The center position (0,0) will be marked in red

## Integration with Existing Systems

### ECS Components Updated
- **FormationLibraryBlob**: Now stores only grid positions (simplified)
- **FormationDataBlob**: Contains only `formationType` and `gridPositions`
- **UnitGridSlotComponent**: Stores grid position for each unit
- **GridFormationUpdateSystem**: Maintains sync between grid and legacy components

### Backward Compatibility
**Traditional formations have been removed.** The system now uses exclusively grid-based formations, which provide:
- **Perfect unit alignment** - No overlapping or floating-point precision issues
- **Consistent spacing** - All formations use the same 1x1m grid cells
- **Simplified code** - No need to maintain two different positioning systems
- **Better performance** - Grid calculations are faster than complex offset calculations

### System Execution Order
1. **FormationSystem**: Updates formation when F1-F4 is pressed
2. **GridFormationUpdateSystem**: Syncs grid positions with offsets
3. **UnitFormationStateSystem**: Manages unit formation states (Formed, Waiting, Moving)
4. **UnitFollowFormationSystem**: Moves units to their assigned positions (only if in Moving state)

## Unit Formation States

The system includes a sophisticated state management system for natural unit behavior:

### State Types
- **Formed**: Unit is in its assigned cell and hero is within grid radius (≤5m)
- **Waiting**: Hero leaves grid radius, unit waits a random delay (0.5-1.5s) before moving
- **Moving**: Unit is moving to its slot; returns to Formed when it arrives and hero is in range

### State Transitions
- **Formed → Waiting**: When hero moves >5m from squad center
- **Waiting → Moving**: When random delay expires
- **Waiting → Formed**: When hero returns to radius and unit is already in slot
- **Moving → Formed**: When unit reaches slot and hero is within radius
- **Moving → Waiting**: When unit reaches slot but hero is still outside radius

### Benefits
- Creates more natural, less robotic unit movements
- Prevents constant micro-adjustments when hero is near radius boundary
- Adds visual variety with random delay timings
- Reduces unnecessary pathfinding calculations

## Best Practices

### Formation Design
1. **Design unit layout** - Place units where you want them tactically positioned
2. **Let system calculate center** - The hero will be positioned at the calculated formation center  
3. **Use consistent spacing** - The grid ensures units don't overlap
4. **Consider formation purpose**:
   - **Line**: Good for ranged units, maximum firepower
   - **Column**: Good for movement through narrow spaces
   - **Wedge**: Good for charges and breakthrough tactics
   - **Square**: Good for all-around defense
   - **Testudo**: Good for defensive formations with protective layout

### Performance Considerations
- Grid calculations are very fast (simple multiplication/division)
- The system automatically snaps positions to avoid floating-point precision issues
- Grid-based formations use slightly less memory than complex offset calculations

### Editor Workflow
1. Create formations using the menu tools first
2. Test formations with the GridFormationTester
3. Assign formations to squads via SquadFormationDataAuthoring
4. Test in-game with F1-F4 keys
5. Iterate and refine formation positions as needed

## Troubleshooting

### Common Issues

**Units not snapping to grid properly**
- Verify that grid positions in your formation asset are integers
- Check that formations are assigned to the `gridFormations` array in SquadData
- Ensure formation types match what you expect

**Formation not changing with F1-F4**
- Verify formations are assigned to the correct array indices in SquadData
- Check that the squad has proper input components
- Ensure FormationSystem is running before UnitFormationStateSystem

**Units spawning in wrong positions**
- Check that SquadSpawningSystem is using the first formation (index 0)
- Verify terrain height sampling is working correctly
- Ensure grid conversion calculations are correct

**Units not moving when expected**
- Check unit formation states using the Entity Debugger
- Verify that UnitFormationStateSystem is transitioning states correctly
- Ensure hero is properly detected as within/outside formation radius
- Check that UnitFollowFormationSystem only moves units in Moving state

**Units moving too frequently or not enough**
- Adjust formation radius in UnitFormationStateSystem (default: 5m)
- Modify random delay range in Waiting state (default: 0.5-1.5s)
- Check slot threshold distance for "in position" detection (default: 0.5m)

**Performance issues**
- Grid calculations should be very fast; if you see performance issues, check for infinite loops in formation systems
- Verify that formation change cooldowns are working properly
- Monitor state transition frequency in Entity Debugger

### Debug Logging
Enable debug logging by:
1. Adding the GridFormationTester to your scene
2. Enabling "Log Results" in the tester
3. Running tests to see detailed coordinate conversion information

## Migration from Traditional Formations

**Traditional formations are no longer supported.** If you have existing traditional formations:

1. **Create new GridFormationScriptableObject assets** using the menu tools
2. **Manually recreate your formations** using grid positions
3. **Update SquadFormationDataAuthoring** to use only grid formations
4. **Test thoroughly** with the GridFormationTester
5. **Remove old FormationScriptableObject references**

The new grid system provides better consistency and performance than the old offset system.
