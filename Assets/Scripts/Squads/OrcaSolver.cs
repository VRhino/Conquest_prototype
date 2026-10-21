using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Optimal reciprocal collision avoidance on the XZ plane (van den Berg, Guy, Lin and Manocha, 2011).
/// Each neighbour adds a half-plane of permitted velocities; the solver picks the velocity closest to the
/// preferred one inside every half-plane and the speed disc, relaxing the constraints evenly when they cannot
/// all hold. Lines are stored as float4 (point.xy, direction.xy); the permitted side is to the left of the
/// direction. Burst-compatible, no allocations.
/// </summary>
public static class OrcaSolver
{
    const float Epsilon = 1e-5f;

    static float Det(float2 a, float2 b) => a.x * b.y - a.y * b.x;

    /// <summary>
    /// Half-plane for agent A against neighbour B.
    /// </summary>
    /// <param name="relativePosition">pB − pA.</param>
    /// <param name="relativeVelocity">vA − vB.</param>
    /// <param name="velocity">vA, the current velocity of the agent.</param>
    /// <param name="combinedRadius">rA + rB.</param>
    /// <param name="timeHorizon">τ: collisions further away in time are ignored.</param>
    /// <param name="collisionResolveTime">Time to separate bodies that already overlap.</param>
    /// <param name="responsibility">Share of the avoidance A takes (0.5 reciprocal, 1 against a non-reacting body).</param>
    public static float4 Line(float2 relativePosition, float2 relativeVelocity, float2 velocity,
        float combinedRadius, float timeHorizon, float collisionResolveTime, float responsibility)
    {
        float distSq = math.lengthsq(relativePosition);
        float radiusSq = combinedRadius * combinedRadius;
        float2 direction, u;

        if (distSq > radiusSq)
        {
            float invTau = 1f / timeHorizon;
            // Vector from the cut-off circle centre to the relative velocity.
            float2 w = relativeVelocity - invTau * relativePosition;
            float wLenSq = math.lengthsq(w);
            float dot = math.dot(w, relativePosition);

            if (dot < 0f && dot * dot > radiusSq * wLenSq)
            {
                // Closest boundary point lies on the cut-off circle.
                float wLen = math.sqrt(wLenSq);
                float2 unitW = w / wLen;
                direction = new float2(unitW.y, -unitW.x);
                u = (combinedRadius * invTau - wLen) * unitW;
            }
            else
            {
                // Closest boundary point lies on one of the cone legs.
                float leg = math.sqrt(distSq - radiusSq);
                if (Det(relativePosition, w) > 0f)
                    direction = new float2(relativePosition.x * leg - relativePosition.y * combinedRadius,
                                           relativePosition.x * combinedRadius + relativePosition.y * leg) / distSq;
                else
                    direction = -new float2(relativePosition.x * leg + relativePosition.y * combinedRadius,
                                            -relativePosition.x * combinedRadius + relativePosition.y * leg) / distSq;
                u = math.dot(relativeVelocity, direction) * direction - relativeVelocity;
            }
        }
        else
        {
            // Already overlapping: leave the overlap within the resolve time.
            float invT = 1f / collisionResolveTime;
            float2 w = relativeVelocity - invT * relativePosition;
            float wLen = math.length(w);
            float2 unitW = wLen > Epsilon ? w / wLen : math.normalizesafe(-relativePosition, new float2(1f, 0f));
            direction = new float2(unitW.y, -unitW.x);
            u = (combinedRadius * invT - wLen) * unitW;
        }

        float2 point = velocity + responsibility * u;
        return new float4(point, direction);
    }

    /// <summary>Velocity inside every half-plane and the speed disc, closest to <paramref name="preferred"/>.</summary>
    public static float2 Solve(ref FixedList512Bytes<float4> lines, float2 preferred, float maxSpeed)
    {
        float2 result = float2.zero;
        int failed = LinearProgram2(ref lines, maxSpeed, preferred, false, ref result);
        if (failed < lines.Length)
            LinearProgram3(ref lines, failed, maxSpeed, ref result);
        return result;
    }

    /// <summary>Optimum on line <paramref name="lineNo"/> subject to the previous lines and the disc.</summary>
    static bool LinearProgram1(ref FixedList512Bytes<float4> lines, int lineNo, float radius, float2 optVelocity,
        bool directionOpt, ref float2 result)
    {
        float2 point = lines[lineNo].xy, dir = lines[lineNo].zw;
        float dot = math.dot(point, dir);
        float discriminant = dot * dot + radius * radius - math.lengthsq(point);
        if (discriminant < 0f) return false; // The disc does not reach this line.

        float sqrtD = math.sqrt(discriminant);
        float tLeft = -dot - sqrtD;
        float tRight = -dot + sqrtD;

        for (int i = 0; i < lineNo; i++)
        {
            float2 pointI = lines[i].xy, dirI = lines[i].zw;
            float denominator = Det(dir, dirI);
            float numerator = Det(dirI, point - pointI);
            if (math.abs(denominator) <= Epsilon)
            {
                if (numerator < 0f) return false; // Parallel and on the forbidden side.
                continue;
            }
            float t = numerator / denominator;
            if (denominator >= 0f) tRight = math.min(tRight, t);
            else tLeft = math.max(tLeft, t);
            if (tLeft > tRight) return false;
        }

        if (directionOpt)
            result = point + (math.dot(optVelocity, dir) > 0f ? tRight : tLeft) * dir;
        else
            result = point + math.clamp(math.dot(dir, optVelocity - point), tLeft, tRight) * dir;
        return true;
    }

    /// <summary>Returns the index of the first line that could not be satisfied, or lines.Length.</summary>
    static int LinearProgram2(ref FixedList512Bytes<float4> lines, float radius, float2 optVelocity,
        bool directionOpt, ref float2 result)
    {
        if (directionOpt)
            result = optVelocity * radius;
        else if (math.lengthsq(optVelocity) > radius * radius)
            result = math.normalize(optVelocity) * radius;
        else
            result = optVelocity;

        for (int i = 0; i < lines.Length; i++)
        {
            if (Det(lines[i].zw, lines[i].xy - result) > 0f)
            {
                float2 previous = result;
                if (!LinearProgram1(ref lines, i, radius, optVelocity, directionOpt, ref result))
                {
                    result = previous;
                    return i;
                }
            }
        }
        return lines.Length;
    }

    /// <summary>Infeasible case: minimise the largest violation across the remaining lines.</summary>
    static void LinearProgram3(ref FixedList512Bytes<float4> lines, int beginLine, float radius, ref float2 result)
    {
        float distance = 0f;
        var projected = new FixedList512Bytes<float4>();
        for (int i = beginLine; i < lines.Length; i++)
        {
            float2 pointI = lines[i].xy, dirI = lines[i].zw;
            if (Det(dirI, pointI - result) <= distance) continue;

            projected.Clear();
            for (int j = 0; j < i; j++)
            {
                float2 pointJ = lines[j].xy, dirJ = lines[j].zw;
                float determinant = Det(dirI, dirJ);
                float2 point;
                if (math.abs(determinant) <= Epsilon)
                {
                    if (math.dot(dirI, dirJ) > 0f) continue; // Same direction: no new constraint.
                    point = 0.5f * (pointI + pointJ);
                }
                else
                {
                    point = pointI + (Det(dirJ, pointI - pointJ) / determinant) * dirI;
                }
                projected.Add(new float4(point, math.normalizesafe(dirJ - dirI)));
            }

            float2 previous = result;
            if (LinearProgram2(ref projected, radius, new float2(-dirI.y, dirI.x), true, ref result) < projected.Length)
                result = previous; // Rounding: keep the previous best.
            distance = Det(dirI, pointI - result);
        }
    }
}
