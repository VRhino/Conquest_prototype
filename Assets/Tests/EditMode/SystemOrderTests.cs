using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor;

/// <summary>
/// [UpdateBefore]/[UpdateAfter] must not form a cycle inside a group. Entities does not report one: its sorter
/// (ComponentSystemSorter.FindExactCycleInSystemGraph) spins forever when the world is created, so the editor freezes
/// on entering Play mode behind a half-finished Burst progress bar. StatResolveSystem after UnitStatScalingSystem and
/// before DamageCalculationSystem did exactly that (damage → respawn → spawn → scaling → resolve → damage).
/// </summary>
public class SystemOrderTests
{
    [Test]
    public void NoUpdateOrderCycleInsideAGroup()
    {
        var systems = TypeCache.GetTypesDerivedFrom<ComponentSystemBase>()
            .Concat(TypeCache.GetTypesDerivedFrom<ISystem>())
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
            .Distinct()
            .ToList();
        var group = systems.ToDictionary(t => t, GroupOf);
        var after = new Dictionary<Type, List<Type>>(); // edge a → b: a updates before b
        void Edge(Type a, Type b)
        {
            if (!group.TryGetValue(a, out var ga) || !group.TryGetValue(b, out var gb) || ga != gb) return;
            if (!after.TryGetValue(a, out var list)) after[a] = list = new List<Type>();
            list.Add(b);
        }
        foreach (var t in systems)
        {
            foreach (UpdateBeforeAttribute a in t.GetCustomAttributes(typeof(UpdateBeforeAttribute), false)) Edge(t, a.SystemType);
            foreach (UpdateAfterAttribute a in t.GetCustomAttributes(typeof(UpdateAfterAttribute), false)) Edge(a.SystemType, t);
        }

        var state = new Dictionary<Type, int>(); // 1 on the stack, 2 done
        var stack = new List<Type>();
        var cycles = new List<string>();
        void Visit(Type u)
        {
            state[u] = 1;
            stack.Add(u);
            if (after.TryGetValue(u, out var next))
                foreach (var v in next)
                {
                    state.TryGetValue(v, out int s);
                    if (s == 1) cycles.Add(string.Join(" → ", stack.Skip(stack.IndexOf(v)).Append(v).Select(x => x.Name)));
                    else if (s == 0) Visit(v);
                }
            stack.RemoveAt(stack.Count - 1);
            state[u] = 2;
        }
        foreach (var t in after.Keys.ToList())
            if (!state.ContainsKey(t)) Visit(t);

        Assert.That(cycles, Is.Empty, "Update order cycles (Entities hangs on them): " + string.Join("; ", cycles));
    }

    static Type GroupOf(Type system)
    {
        var attributes = system.GetCustomAttributes(typeof(UpdateInGroupAttribute), false);
        return attributes.Length > 0 ? ((UpdateInGroupAttribute)attributes[0]).GroupType : typeof(SimulationSystemGroup);
    }
}
