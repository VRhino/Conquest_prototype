using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using sc.terrain.vegetationspawner;

namespace Conquest.WorldMap.Editor
{
    /// <summary>Deterministic variable-radius sampling shared by all loaded trial sectors and species.</summary>
    internal static class WorldMapAdaptiveForest
    {
        internal sealed class Field { public Terrain terrain; public byte[] raw; public int n; }
        internal sealed class Candidate
        {
            public Field field; public Vector3 normalized; public Vector2 point;
            public float spacing, density; public int family; public uint priority;
        }
        struct RiverSegment { public Vector2 a, delta; public float radius; }
        internal sealed class Result
        {
            public readonly int[] densityBands = new int[4];
            public int trees, candidates, spacingRejected;
            public readonly Dictionary<Terrain, List<TreeInstance>> instances = new Dictionary<Terrain, List<TreeInstance>>();
        }

        internal static Result Generate(List<Field> fields, VegetationSpawner spawner,
            WorldMapForestTrialProfile profile, float maximumSlope, LineRenderer[] rivers, WorldMapWaterSettings water)
        {
            if (profile.candidateCellSize <= 0 || profile.minimumSpacing <= 0 || profile.maximumSpacing < profile.minimumSpacing)
                throw new ArgumentException("Perfil de bosque inválido.");
            const float bucketSize = 32;
            const float riverBucket = 128;
            var riverGrid = new Dictionary<Vector2Int, List<RiverSegment>>();
            foreach (var river in rivers)
                for (int i = 1; i < river.positionCount; i++)
                {
                    var a = river.GetPosition(i - 1); var b = river.GetPosition(i);
                    if (!river.useWorldSpace) { a = river.transform.TransformPoint(a); b = river.transform.TransformPoint(b); }
                    var segment = new RiverSegment { a = new Vector2(a.x, a.z), delta = new Vector2(b.x - a.x, b.z - a.z),
                        radius = Mathf.Max(water.minimumRiverWidth, river.widthMultiplier * water.riverWidthScale) * .5f + water.treeRiverClearance };
                    var lo = Vector2.Min(segment.a, segment.a + segment.delta) - Vector2.one * segment.radius;
                    var hi = Vector2.Max(segment.a, segment.a + segment.delta) + Vector2.one * segment.radius;
                    var first = Bucket(lo, riverBucket); var last = Bucket(hi, riverBucket);
                    for (int x = first.x; x <= last.x; x++) for (int z = first.y; z <= last.y; z++)
                    {
                        var key = new Vector2Int(x, z);
                        if (!riverGrid.TryGetValue(key, out var list)) riverGrid[key] = list = new List<RiverSegment>();
                        list.Add(segment);
                    }
                }
            var result = new Result(); var candidates = new List<Candidate>();
            var variantsByFamily = spawner.treeTypes.Select(type => type.prefabs.Where(p => p.prefab).ToArray()).ToArray();
            if (variantsByFamily.Any(variants => variants.Length == 0)) throw new InvalidOperationException("Familia sin modelos.");
            foreach (var field in fields)
            {
                var terrain = field.terrain; var data = terrain.terrainData; var origin = terrain.transform.position;
                result.instances[terrain] = new List<TreeInstance>();
                float cell = profile.candidateCellSize;
                int x0 = Mathf.FloorToInt(origin.x / cell), x1 = Mathf.CeilToInt((origin.x + data.size.x) / cell);
                int z0 = Mathf.FloorToInt(origin.z / cell), z1 = Mathf.CeilToInt((origin.z + data.size.z) / cell);
                for (int x = x0; x < x1; x++) for (int z = z0; z < z1; z++)
                {
                    uint h = Hash(x, z, profile.seed);
                    var point = new Vector2((x + Unit(h)) * cell, (z + Unit(Mix(h))) * cell);
                    float u = (point.x - origin.x) / data.size.x, v = (point.y - origin.z) / data.size.z;
                    if (u < 0 || u >= 1 || v < 0 || v >= 1) continue;
                    int ix = Mathf.RoundToInt(u * (field.n - 1)), iz = Mathf.RoundToInt(v * (field.n - 1));
                    int offset = (iz * field.n + ix) * 7;
                    float density = profile.VisualDensity(Density(field, u, v), point);
                    float slope = data.GetSteepness(u,v);
                    float thinning = Mathf.SmoothStep(0,1,Mathf.InverseLerp(profile.slopeThinningStart,
                        Mathf.Max(profile.slopeThinningStart+1,profile.slopeThinningEnd),slope));
                    density *= 1-thinning;
                    if (density < profile.minimumDensity || field.raw[offset + 3] == 255) continue;
                    float height = data.GetInterpolatedHeight(u, v);
                    if (origin.y + height < spawner.waterHeight || slope > maximumSlope) continue;
                    bool blocked = false;
                    if (riverGrid.TryGetValue(Bucket(point, riverBucket), out var nearby))
                        foreach (var segment in nearby)
                        {
                            float t = Mathf.Clamp01(Vector2.Dot(point - segment.a, segment.delta) / Mathf.Max(.001f, segment.delta.sqrMagnitude));
                            if ((point - segment.a - segment.delta * t).sqrMagnitude < segment.radius * segment.radius) { blocked = true; break; }
                        }
                    if (blocked) continue;
                    candidates.Add(new Candidate { field = field, point = point, normalized = new Vector3(u, height / data.size.y, v),
                        density = density, spacing = profile.Spacing(density), family = field.raw[offset + 6] == 4 ? 2 : field.raw[offset + 6] == 3 ? 1 : 0,
                        priority = Mix(Mix(h)) });
                }
            }
            result.candidates = candidates.Count;
            candidates.Sort((a, b) => { int order = a.priority.CompareTo(b.priority); if (order != 0) return order;
                order = a.point.x.CompareTo(b.point.x); return order != 0 ? order : a.point.y.CompareTo(b.point.y); });
            var occupied = new Dictionary<Vector2Int, List<Candidate>>();
            foreach (var candidate in candidates)
            {
                var first = Bucket(candidate.point - Vector2.one * profile.maximumSpacing, bucketSize);
                var last = Bucket(candidate.point + Vector2.one * profile.maximumSpacing, bucketSize); bool blocked = false;
                for (int x = first.x; x <= last.x && !blocked; x++) for (int z = first.y; z <= last.y && !blocked; z++)
                    if (occupied.TryGetValue(new Vector2Int(x, z), out var neighbors))
                        foreach (var other in neighbors)
                        {
                            float distance = Mathf.Max(candidate.spacing, other.spacing);
                            if ((candidate.point - other.point).sqrMagnitude < distance * distance) { blocked = true; break; }
                        }
                if (blocked) { result.spacingRejected++; continue; }
                var bucket = Bucket(candidate.point, bucketSize);
                if (!occupied.TryGetValue(bucket, out var entries)) occupied[bucket] = entries = new List<Candidate>();
                entries.Add(candidate);
                var variants = variantsByFamily[candidate.family];
                uint h = Mix(candidate.priority); float scale = Mathf.Lerp(profile.scaleRange.x, profile.scaleRange.y, Unit(Mix(h)));
                result.instances[candidate.field.terrain].Add(new TreeInstance { position = candidate.normalized,
                    prototypeIndex = variants[(int)(h % (uint)variants.Length)].index, widthScale = scale, heightScale = scale,
                    rotation = Unit(Mix(Mix(h))) * Mathf.PI * 2, color = Color.white, lightmapColor = Color.white });
                result.trees++; result.densityBands[candidate.density < .35f ? 0 : candidate.density < .6f ? 1 : candidate.density < .8f ? 2 : 3]++;
            }
            return result;
        }

        static float Density(Field field, float u, float v)
        {
            float x = u * (field.n - 1), z = v * (field.n - 1); int ix = (int)x, iz = (int)z;
            int xx = Mathf.Min(ix + 1, field.n - 1), zz = Mathf.Min(iz + 1, field.n - 1);
            float a = Mathf.Lerp(field.raw[(iz * field.n + ix) * 7], field.raw[(iz * field.n + xx) * 7], x - ix);
            float b = Mathf.Lerp(field.raw[(zz * field.n + ix) * 7], field.raw[(zz * field.n + xx) * 7], x - ix);
            return Mathf.Lerp(a, b, z - iz) / 255f;
        }
        static Vector2Int Bucket(Vector2 point, float size) => new Vector2Int(Mathf.FloorToInt(point.x / size), Mathf.FloorToInt(point.y / size));
        static uint Hash(int x, int z, int seed) => Mix(unchecked((uint)(x * 73856093 ^ z * 19349663 ^ seed)));
        static uint Mix(uint value) { unchecked { value ^= value >> 16; value *= 0x7feb352d; value ^= value >> 15; value *= 0x846ca68b; return value ^ (value >> 16); } }
        static float Unit(uint value) => (value & 0xffffff) / 16777216f;
    }
}
