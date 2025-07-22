
public enum UnitFormationState
{
    Formed,   // Unit is in its assigned cell and hero is within grid radius
    Waiting,  // Hero leaves grid radius, unit waits a random delay before moving
    Moving    // Unit is moving to its slot; returns to Formed when it arrives and hero is in range
}