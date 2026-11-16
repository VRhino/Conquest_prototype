// El héroe del ticket en la batalla, en la escala del motor (tanda 2 de las clases y los objetos): sus cifras pieza a pieza (forjada o de
// ficha), la montura y las reglas del héroe que propuso el autor del motor (HeroEngineRules), con las que el héroe queda a la par de las tropas.
using BronzeAge.Contract.V1;
using ConquestTactics.Net;
using NUnit.Framework;
using Unity.Entities;

public class HeroeDelMotorEnBatallaTests
{
    [Test]
    public void UnaPiezaForjadaTraeSusCifrasYUnaDeTiendaLasDeSuFicha()
    {
        var forjada = new ItemInstancia { ItemDefinitionId = "dagas_bronce", ItemInstanceId = "obj-7" };
        foreach (var (nombre, valor) in new[] { ("destreza", 1L), ("danoPerforante", 68L), ("danoCortante", 66L), ("penetracionPerforante", 11L), ("penetracionCortante", 11L) })
            forjada.Estadisticas.Add(new Estadistica { Nombre = nombre, Valor = valor });
        var heroe = new BronzeAge.Contract.V1.HeroSnapshot
        {
            ClassDefinitionId = "dagas",
            AtributosEfectivos = new Atributos { Fuerza = 3, Destreza = 9, Armadura = 0, Vitalidad = 2 },
            Equipamiento = new Equipamiento { Arma = forjada, Torso = new ItemInstancia { ItemDefinitionId = "torso_media_bronce" } },
        };
        var c = CifrasDelHeroe.De(heroe);
        Assert.IsTrue(c.fromEngine);
        Assert.AreEqual((3f, 9f, 0f, 2f), (c.strength, c.dexterity, c.armor, c.vitality), "atributos_efectivos, sin el equipo");
        Assert.AreEqual((68f, 66f, 0f, 11f), (c.pierceDamage, c.slashDamage, c.bluntDamage, c.piercePenetration), "el arma forjada, con sus cifras");
        var torso = CatalogoDelHeroe.ObjetoDe("torso_media_bronce").combate;
        Assert.AreEqual(torso.salud, c.armorHealth, "el torso de tienda, con las de su ficha");
        Assert.AreEqual(torso.defensa.cortante, c.slashDefense);
        Assert.AreEqual(1 + torso.atributos.destreza, c.equipmentDexterity, "lo que suman el arma y el torso");
        Assert.AreEqual((0f, 1f), (c.strengthDamageWeight, c.dexterityDamageWeight), "las dagas pegan con la destreza");
    }

    [Test]
    public void ElHeroeDeInicioQuedaComoLaReferenciaDelMotor()
    {
        // Las dagas con su equipo de tier I y sin puntos: 1979 de vida efectiva y 111 de golpe en la referencia del autor.
        var e = CifrasDelHeroe.De(CifrasDelHeroe.DeInicio("dagas"));
        using var world = new World("Héroe del motor");
        var em = world.EntityManager;
        var hero = em.CreateEntity();
        em.AddComponentData(hero, new HeroClassComponent { heroClass = HeroClass.SwordAndShield });
        em.AddComponentData(hero, new HeroHealthComponent { maxHealth = 100f, currentHealth = 100f });
        em.AddComponentData(hero, new StaminaComponent { maxStamina = 100f, currentStamina = 100f });
        em.AddComponentData(hero, e);
        world.GetOrCreateSystemManaged<HeroInitializationSystem>().Update();
        world.GetOrCreateSystemManaged<StatResolveSystem>().Update();

        var r = HeroEngineRules.Default;
        float vitalidad = e.vitality + e.equipmentVitality, destreza = e.dexterity + e.equipmentDexterity, armadura = e.armor + e.equipmentArmor;
        float vida = em.GetComponentData<HeroHealthComponent>(hero).maxHealth;
        Assert.AreEqual(r.baseHealth + r.healthPerVitality * vitalidad + e.armorHealth, vida, 0.01f, "1000 + 30 por vitalidad + la salud de la armadura");
        var golpe = em.GetComponentData<DamageProfileComponent>(hero);
        Assert.AreEqual(e.pierceDamage * (1f + r.damagePerAttribute * destreza), golpe.piercingDamage, 0.01f, "el arma por 1 + 1 % por punto de destreza");
        Assert.AreEqual(e.piercePenetration, golpe.piercingPenetration, 0.01f, "la penetración del arma");
        var defensa = em.GetComponentData<DefenseComponent>(hero);
        Assert.AreEqual(0f, defensa.slashDefense, "sin defensa plana: la defensa es alivio");
        Assert.AreEqual(r.reliefPerDefense * e.slashDefense + r.reliefPerArmor * armadura, defensa.slashRelief, 1e-4f);

        float alivio = (defensa.pierceRelief + defensa.slashRelief + defensa.bluntRelief) / 3f;
        Assert.AreEqual(1979f, vida / (1f - alivio), 5f, "vida efectiva de la referencia");
        Assert.AreEqual(111f, golpe.piercingDamage + golpe.slashingDamage, 1.5f, "golpe de la referencia");
    }

    [Test]
    public void LaMonturaDelMotorEsElCaballoDelHeroe()
    {
        var caballo = CifrasDelHeroe.Montura("montura_estepa");
        Assert.AreEqual((1500f, 6f, 11.5f, 120f), (caballo.health, caballo.trotSpeed, caballo.gallopSpeed, caballo.stamina));
        var t = MountTuning.Default.With(caballo);
        Assert.AreEqual((1500f, 11.5f, 120f), (t.horseHealth, t.gallopSpeed, t.stamina));
        Assert.AreEqual(MountTuning.Default.turnRate, t.turnRate, "el giro y lo demás, del ajuste");
        Assert.AreEqual(MountTuning.Default.horseHealth, MountTuning.Default.With(CifrasDelHeroe.Montura(null)).horseHealth, "sin montura, el caballo del ajuste");
    }

    [Test]
    public void UnHeroeSinMonturaVaAPie()
    {
        // Víctor, 2026-11-16 (22a): sin montura en el ticket no hay caballo (HeroOnFootTag); con ella, sí.
        Assert.AreEqual(0f, CifrasDelHeroe.Montura(null).health, "sin montura no hay caballo del motor");
        Assert.IsFalse(HeroMountRules.CanMount(alive: true, inBattle: true, immobilised: false, busy: false, hasHorse: false), "a pie");
        Assert.IsTrue(HeroMountRules.CanMount(alive: true, inBattle: true, immobilised: false, busy: false, hasHorse: true));
    }
}
