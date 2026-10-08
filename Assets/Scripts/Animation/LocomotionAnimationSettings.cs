using System;
using UnityEngine;

namespace ConquestTactics.Animation
{
    public enum LocomotionGait { Idle, Walk, Run, Sprint }

    /// <summary>Maps confirmed gameplay speed to the coordinates of the Synty locomotion tree.</summary>
    [Serializable]
    public sealed class LocomotionAnimationSettings
    {
        [Min(0f)] public float stoppedSpeed = 0.05f;
        [Range(0f, 1f)] public float walkToRunRatio = 0.6f;
        [Min(1f)] public float sprintSpeedRatio = 1.1f;
        [Min(0f)] public float walkBlendSpeed = 3f;
        [Min(0f)] public float runBlendSpeed = 5f;
        [Min(0f)] public float sprintBlendSpeed = 12f;

        public LocomotionGait GetGait(float speed, float baseSpeed, bool sprintAllowed)
        {
            if (speed <= stoppedSpeed || baseSpeed <= 0f) return LocomotionGait.Idle;
            if (sprintAllowed && speed > baseSpeed * sprintSpeedRatio) return LocomotionGait.Sprint;
            return speed > baseSpeed * walkToRunRatio ? LocomotionGait.Run : LocomotionGait.Walk;
        }

        public float GetBlendSpeed(float speed, float baseSpeed, float sprintSpeed, bool sprintAllowed)
        {
            if (speed <= stoppedSpeed || baseSpeed <= 0f) return 0f;
            float walkSpeed = baseSpeed * Mathf.Clamp01(walkToRunRatio);
            if (walkSpeed > 0f && speed < walkSpeed)
                return walkBlendSpeed * speed / walkSpeed;
            if (speed <= baseSpeed || !sprintAllowed || sprintSpeed <= baseSpeed)
                return Mathf.Lerp(walkBlendSpeed, runBlendSpeed,
                    Mathf.InverseLerp(walkSpeed, baseSpeed, speed));
            return Mathf.Lerp(runBlendSpeed, sprintBlendSpeed,
                Mathf.InverseLerp(baseSpeed, sprintSpeed, speed));
        }
    }
}
