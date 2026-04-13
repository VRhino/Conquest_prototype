using UnityEngine;

/// <summary>
/// Centralizes all Animator parameter hashes used across the project.
/// Avoids magic strings and ensures a single source of truth for parameter names.
/// </summary>
public static class AnimationHashes
{
    // Movement state
    public static readonly int MoveSpeed              = Animator.StringToHash("MoveSpeed");
    public static readonly int CurrentGait            = Animator.StringToHash("CurrentGait");
    public static readonly int IsWalking              = Animator.StringToHash("IsWalking");
    public static readonly int IsStopped              = Animator.StringToHash("IsStopped");
    public static readonly int IsStarting             = Animator.StringToHash("IsStarting");
    public static readonly int IsGrounded             = Animator.StringToHash("IsGrounded");
    public static readonly int ForceGroundedTransition = Animator.StringToHash("ForceGroundedTransition");
    public static readonly int ForceLocomotion        = Animator.StringToHash("ForceLocomotion");
    public static readonly int ForceIdle              = Animator.StringToHash("ForceIdle");
    public static readonly int LocomotionStartDirection = Animator.StringToHash("LocomotionStartDirection");

    // Input
    public static readonly int MovementInputTapped    = Animator.StringToHash("MovementInputTapped");
    public static readonly int MovementInputPressed   = Animator.StringToHash("MovementInputPressed");
    public static readonly int MovementInputHeld      = Animator.StringToHash("MovementInputHeld");

    // Strafe / Shuffle
    public static readonly int StrafeDirectionX       = Animator.StringToHash("StrafeDirectionX");
    public static readonly int StrafeDirectionZ       = Animator.StringToHash("StrafeDirectionZ");
    public static readonly int ShuffleDirectionX      = Animator.StringToHash("ShuffleDirectionX");
    public static readonly int ShuffleDirectionZ      = Animator.StringToHash("ShuffleDirectionZ");
    public static readonly int ForwardStrafe          = Animator.StringToHash("ForwardStrafe");
    public static readonly int IsStrafing             = Animator.StringToHash("IsStrafing");
    public static readonly int IsTurningInPlace       = Animator.StringToHash("IsTurningInPlace");

    // Camera / Look
    public static readonly int CameraRotationOffset   = Animator.StringToHash("CameraRotationOffset");
    public static readonly int LeanValue              = Animator.StringToHash("LeanValue");
    public static readonly int HeadLookX              = Animator.StringToHash("HeadLookX");
    public static readonly int HeadLookY              = Animator.StringToHash("HeadLookY");
    public static readonly int BodyLookX              = Animator.StringToHash("BodyLookX");
    public static readonly int BodyLookY              = Animator.StringToHash("BodyLookY");

    // Combat
    public static readonly int IsAttacking            = Animator.StringToHash("IsAttacking");
    public static readonly int TriggerAttack          = Animator.StringToHash("TriggerAttack");

    // Ranged combat
    public static readonly int TriggerShoot           = Animator.StringToHash("TriggerShoot");
    public static readonly int IsReloading            = Animator.StringToHash("IsReloading");

    // Interactables
    public static readonly int DoorOpen               = Animator.StringToHash("Open");
    public static readonly int DoorClose              = Animator.StringToHash("Close");
}
