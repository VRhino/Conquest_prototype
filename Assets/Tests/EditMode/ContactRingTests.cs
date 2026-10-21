using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>Contact ring around melee targets (C11, Movimiento_Formaciones.md L-7).</summary>
public class ContactRingTests
{
    [Test]
    public void SpearmenRingHoldsEightAttackers()
    {
        // L-7 example: d_parada ≈ 1.6 m, s_arco = 2·0.5 + 0.2 = 1.2 m.
        Assert.That(ContactRing.SlotCount(1.6f, 1.2f), Is.EqualTo(8));
        Assert.That(ContactRing.SlotCount(0.1f, 1.2f), Is.EqualTo(1));
        Assert.That(ContactRing.SlotCount(100f, 0.1f), Is.EqualTo(ContactRing.MaxSlots));
    }

    [Test]
    public void SlotsSitOnTheRingAtFixedWorldAngles()
    {
        var target = new float2(3f, -2f);
        float2 east = ContactRing.SlotPosition(target, 0, 8, 1.5f);
        float2 north = ContactRing.SlotPosition(target, 2, 8, 1.5f);
        Assert.That(math.distance(east, target + new float2(1.5f, 0f)), Is.LessThan(1e-4f));
        Assert.That(math.distance(north, target + new float2(0f, 1.5f)), Is.LessThan(1e-4f));
    }

    [Test]
    public void AttackerTakesTheFreeSlotOnItsOwnSide()
    {
        float bearing = ContactRing.Bearing(float2.zero, new float2(0.1f, 2f)); // almost +z → slot 2 of 8
        Assert.That(ContactRing.NearestFreeSlot(bearing, 8, 0UL), Is.EqualTo(2));
        // Taken: the next closest, never the far side.
        int next = ContactRing.NearestFreeSlot(bearing, 8, 1UL << 2);
        Assert.That(next == 1 || next == 3, Is.True);
        Assert.That(ContactRing.NearestFreeSlot(bearing, 8, 0xFFUL), Is.EqualTo(-1));
        // Negative bearings wrap.
        Assert.That(ContactRing.NearestFreeSlot(-math.PI / 2f, 8, 0UL), Is.EqualTo(6));
    }

    [Test]
    public void AttackerNeverReservesRoundTheBackOfTheTarget()
    {
        // From +x, ±60° on an 8-slot ring allows slots 7, 0 and 1 only.
        float maxArc = math.radians(60f);
        ulong front = (1UL << 7) | (1UL << 0) | (1UL << 1);
        Assert.That(ContactRing.NearestFreeSlot(0f, 8, (1UL << 0), maxArc), Is.EqualTo(1).Or.EqualTo(7));
        Assert.That(ContactRing.NearestFreeSlot(0f, 8, front, maxArc), Is.EqualTo(-1));
        Assert.That(ContactRing.NearestFreeSlot(0f, 8, front), Is.EqualTo(2).Or.EqualTo(6));
    }

    [Test]
    public void WaitingAttackersStandOutsideTheResumeDistance()
    {
        Assert.That(ContactRing.WaitDistance(2f, 1.2f), Is.EqualTo(3.2f).Within(1e-5f));
    }

    [Test]
    public void SystemGivesEachAttackerItsOwnSlotKeepsItAndFreesItOnDeath()
    {
        using var world = new World("Contact ring");
        var em = world.EntityManager;
        em.CreateEntity(typeof(MovementTuningComponent));
        var tuning = MovementTuningComponent.Default;
        em.SetComponentData(em.CreateEntityQuery(typeof(MovementTuningComponent)).GetSingletonEntity(), tuning);

        Entity target = em.CreateEntity(typeof(LocalTransform));
        em.SetComponentData(target, LocalTransform.FromPosition(float3.zero));
        var attackers = new Entity[10];
        for (int i = 0; i < attackers.Length; i++)
        {
            attackers[i] = em.CreateEntity(typeof(UnitCombatComponent), typeof(UnitWeaponComponent),
                typeof(LocalTransform), typeof(MeleeContactSlotComponent));
            em.SetComponentData(attackers[i], new UnitCombatComponent { target = target });
            em.SetComponentData(attackers[i], new UnitWeaponComponent { attackRange = 1.5f });
            // All from the same side (+x), at increasing distance.
            em.SetComponentData(attackers[i], LocalTransform.FromPosition(new float3(2f + i, 0f, 0.01f * i)));
            em.SetComponentData(attackers[i], new MeleeContactSlotComponent { slot = -1 });
        }
        var system = world.GetOrCreateSystemManaged<MeleeContactRingSystem>();
        system.Update();

        var slots = new System.Collections.Generic.HashSet<int>();
        int waiting = 0, count = 0;
        foreach (var a in attackers)
        {
            var s = em.GetComponentData<MeleeContactSlotComponent>(a);
            Assert.That(s.target, Is.EqualTo(target));
            count = s.slotCount;
            if (s.slot < 0) { waiting++; continue; }
            Assert.That(slots.Add(s.slot), Is.True, "Two attackers share a slot.");
        }
        // All come from +x: only the slots within the arc facing them are handed out (3 of the 8), the rest wait.
        Assert.That(count, Is.EqualTo(8));
        Assert.That(slots.Count, Is.EqualTo(3));
        Assert.That(waiting, Is.EqualTo(attackers.Length - 3));
        // The nearest attacker takes the slot facing it.
        Assert.That(em.GetComponentData<MeleeContactSlotComponent>(attackers[0]).slot, Is.EqualTo(0));

        // Reservations hold while nothing changes.
        int kept = em.GetComponentData<MeleeContactSlotComponent>(attackers[3]).slot;
        system.Update();
        Assert.That(em.GetComponentData<MeleeContactSlotComponent>(attackers[3]).slot, Is.EqualTo(kept));

        // A death frees a slot for a waiting attacker.
        em.AddComponent<IsDeadComponent>(attackers[0]);
        system.Update();
        int waitingAfter = 0;
        foreach (var a in attackers)
        {
            if (em.HasComponent<IsDeadComponent>(a)) continue;
            if (em.GetComponentData<MeleeContactSlotComponent>(a).slot < 0) waitingAfter++;
        }
        Assert.That(waitingAfter, Is.EqualTo(waiting - 1));
    }
}
