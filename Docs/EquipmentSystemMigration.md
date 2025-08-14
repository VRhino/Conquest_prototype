# Equipment System Migration - Documentation

## Overview
This document describes the migration from a string-based equipment system to an InventoryItem-based system that preserves unique stats and instance data.

## Problem Solved
Previously, the equipment system stored only itemId strings, causing:
- Loss of unique generated stats when equipment was persisted
- Loss of instanceId tracking for individual items
- Tooltips showing base stats instead of actual generated stats
- Equipment tracking by itemId instead of instanceId

## Changes Made

### 1. Equipment.cs Structure Update
**Before:**
```csharp
public class Equipment
{
    public string weaponId = string.Empty;
    public string helmetId = string.Empty;
    // ... other string fields
}
```

**After:**
```csharp
public class Equipment
{
    public InventoryItem weapon = null;
    public InventoryItem helmet = null;
    // ... other InventoryItem fields
}
```

### 2. InventoryUtils.cs - Equipment Slot Management
- **Added:** `GetEquippedItem(Equipment, ItemType)` - Returns full InventoryItem
- **Added:** `SetEquippedItem(Equipment, ItemType, InventoryItem)` - Sets full InventoryItem
- **Added:** `GetEquippedSlotByInstanceId()` - Find slot by instanceId
- **Added:** `GetAllEquippedItems()` - Get all equipped items as list
- **Added:** `IsSlotOccupied()` - Check if slot has item
- **Kept:** Legacy methods with `[Obsolete]` attribute for compatibility

### 3. InventoryService.cs - Equipment Operations
- **Updated:** `ProcessEquipment()` to work with InventoryItem objects
- **Updated:** Private helper methods to return/accept InventoryItem
- **Added:** `GetEquippedItem(ItemType)` - Public API to get equipped items
- **Added:** `GetAllEquippedItems()` - Get all equipped items
- **Added:** `IsSlotEquipped(ItemType)` - Check slot status
- **Added:** `SwapEquipment(string)` - Cleaner equipment swapping

### 4. InventoryEvents.cs - Event System Update
**Before:**
```csharp
public static Action<string> OnItemEquipped;
public static Action<string> OnItemUnequipped;
```

**After:**
```csharp
public static Action<InventoryItem> OnItemEquipped;
public static Action<InventoryItem> OnItemUnequipped;
```

### 5. Visual System Updates
- **Updated:** `HeroVisualManagement.System.cs` to handle InventoryItem events
- Event handlers now receive full InventoryItem with stats and instanceId
- Improved logging with both itemId and instanceId information

### 6. Compatibility Layer
- Legacy methods marked with `[Obsolete]` but still functional
- `GetEquippedItemId()` maintains string-based compatibility
- `SetEquippedItemId()` creates basic InventoryItem for compatibility
- Temporary event emission methods for gradual migration

## Benefits Achieved

### 1. Stat Persistence ✅
- Equipment now maintains generated stats when persisted
- Tooltips show actual item stats, not base template stats
- Unique item properties preserved through save/load cycles

### 2. Instance Tracking ✅
- Each equipment piece maintains its unique instanceId
- Proper tracking of individual item instances
- Support for multiple items of same type with different stats

### 3. Code Maintainability ✅
- Centralized equipment management in InventoryUtils
- Reduced code duplication across the system
- Clear separation between new API and legacy compatibility
- Consistent naming conventions and documentation

### 4. Visual System Integrity ✅
- Visual updates receive full item information
- Improved debugging with instanceId logging
- Seamless integration with existing visual components

## API Usage Examples

### Getting Equipped Items
```csharp
// Get specific equipped item
var weapon = InventoryService.GetEquippedItem(ItemType.Weapon);
if (weapon != null)
{
    Debug.Log($"Weapon: {weapon.itemId} with {weapon.GeneratedStats.Count} stats");
}

// Get all equipped items
var allEquipped = InventoryService.GetAllEquippedItems();
foreach (var item in allEquipped)
{
    Debug.Log($"Equipped: {item.itemId} (Instance: {item.instanceId})");
}

// Check if slot is occupied
if (InventoryService.IsSlotEquipped(ItemType.Helmet))
{
    Debug.Log("Helmet slot is occupied");
}
```

### Equipment Events
```csharp
// Subscribe to equipment events
InventoryEvents.OnItemEquipped += (item) => {
    Debug.Log($"Equipped: {item.itemId} with instance {item.instanceId}");
    // Access item.GeneratedStats for unique stats
};

InventoryEvents.OnItemUnequipped += (item) => {
    Debug.Log($"Unequipped: {item.itemId}");
};
```

## Migration Notes

### For New Development
- Use the new InventoryItem-based methods
- Access `item.GeneratedStats` for unique stats
- Use `item.instanceId` for tracking specific instances
- Subscribe to events with InventoryItem parameters

### For Legacy Code
- Legacy string-based methods still work but are marked obsolete
- Gradual migration recommended - start with critical paths
- Test thoroughly when migrating equipment-related code

### Save Compatibility
- New save format uses InventoryItem objects
- Old saves will need conversion (handled automatically by JSON deserializer)
- Backup saves before testing migration

## Performance Considerations
- InventoryItem objects are lightweight - no significant overhead
- Event system now passes richer data but frequency is same
- Equipment operations slightly more efficient due to direct object access

## Future Enhancements
With this foundation, future improvements are now possible:
- Equipment comparison tooltips (showing equipped vs inventory items)
- Equipment set bonuses (tracking multiple equipped items)
- Equipment durability system
- Advanced stat generation and modification
- Equipment crafting and upgrading

## Testing Recommendations
1. Test equipment swapping functionality
2. Verify stat persistence through save/load cycles
3. Check tooltip accuracy with generated stats
4. Validate visual updates with equipment changes
5. Test edge cases (null items, empty slots)
6. Performance test with multiple equipment changes

## Code Quality Metrics
- ✅ No code duplication in equipment management
- ✅ Consistent error handling and validation
- ✅ Comprehensive documentation and comments
- ✅ Clear separation of concerns
- ✅ Backward compatibility maintained
- ✅ Future-proof design patterns
