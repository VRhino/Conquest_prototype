using Unity.Mathematics;

/// <summary>
/// Formation destination with lead (Movimiento_Formaciones.md L-3). The agent is sent slightly ahead of a
/// moving slot instead of braking against a target that jumps every few centimetres; with a still slot the
/// prediction collapses to the slot itself.
/// </summary>
public static class FormationDestinationPrediction
{
    /// <summary>
    /// Smoothed horizontal slot velocity. A jump larger than <paramref name="resetDistance"/> in one frame
    /// (formation change, teleport) resets it to zero instead of producing a spike.
    /// </summary>
    public static float3 UpdateSlotVelocity(ref float3 previousSlot, ref bool hasPrevious, ref float3 velocity,
        float3 requestedSlot, float dt, float smoothingTime, float resetDistance)
    {
        float3 delta = requestedSlot - previousSlot;
        delta.y = 0f;
        if (!hasPrevious || dt <= 0f || math.lengthsq(delta) > resetDistance * resetDistance)
            velocity = float3.zero;
        else
        {
            float alpha = 1f - math.exp(-dt / math.max(1e-4f, smoothingTime));
            velocity += (delta / dt - velocity) * alpha;
        }
        previousSlot = requestedSlot;
        hasPrevious = true;
        return velocity;
    }

    /// <summary>Slot advanced by <c>velocity * leadTime</c>, clamped to <paramref name="maxLead"/> metres.</summary>
    public static float3 Predict(float3 slot, float3 velocity, float leadTime, float maxLead)
    {
        float3 lead = velocity * leadTime;
        lead.y = 0f;
        float length = math.length(lead);
        if (length > maxLead && length > 0f) lead *= maxLead / length;
        return slot + lead;
    }

    /// <summary>Command re-issue threshold: grows with slot speed so fast marches do not resend every frame.</summary>
    public static float ChangeThreshold(float3 velocity, float minimum, float speedFactor) =>
        math.max(minimum, math.length(velocity.xz) * speedFactor);
}
