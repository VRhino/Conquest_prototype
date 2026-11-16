// Los catálogos del héroe del motor importados en Conquest (CLASES_HEROE y OBJETOS, Resources/Heroe/BronzeAge): las 5 clases con sus atributos
// base, las piezas con sus cifras de combate y las versiones que trae el ticket.
using System.Linq;
using ConquestTactics.Net;
using NUnit.Framework;

public class CatalogoDelHeroeTests
{
    [Test]
    public void LasCincoClasesDelMotorConSusAtributosBase()
    {
        Assert.AreEqual("bronzeage-clases-1", CatalogoDelHeroe.VersionDeClases);
        CollectionAssert.AreEquivalent(new[] { "lanza_escudo_redondo", "espada_escudo_mediano", "arco_lanza", "hacha_dos_manos", "dagas" },
            CatalogoDelHeroe.TodasLasClases.Select(c => c.id).ToArray());
        foreach (var c in CatalogoDelHeroe.TodasLasClases)
        {
            var a = c.atributosBase;
            Assert.AreEqual(10, a.fuerza + a.destreza + a.armadura + a.vitalidad, $"{c.id}: los atributos base suman 10");
        }
        var dagas = CatalogoDelHeroe.ClaseDe("dagas");
        Assert.AreEqual((3, 5, 0, 2), (dagas.atributosBase.fuerza, dagas.atributosBase.destreza, dagas.atributosBase.armadura, dagas.atributosBase.vitalidad));
        Assert.IsNull(CatalogoDelHeroe.ClaseDe("no_existe"));
    }

    [Test]
    public void LosObjetosDelMotorConSusCifrasDeCombate()
    {
        Assert.AreEqual("bronzeage-objetos-1", CatalogoDelHeroe.VersionDeObjetos);
        var objetos = CatalogoDelHeroe.TodosLosObjetos;
        Assert.AreEqual(136, objetos.Count, "las 136 piezas del catálogo");
        Assert.AreEqual(30, objetos.Count(o => o.tipo == "arma"), "6 armas por clase");
        foreach (var arma in objetos.Where(o => o.tipo == "arma"))
            Assert.Greater(arma.combate.dano.perforante + arma.combate.dano.cortante + arma.combate.dano.contundente, 0, $"{arma.id}: con daño");
        foreach (var pieza in objetos.Where(o => o.tipo == "armadura"))
            Assert.Greater(pieza.combate.salud, 0, $"{pieza.id}: con salud");
        var lanza = CatalogoDelHeroe.ObjetoDe("lanza_escudo_redondo_inicio");
        Assert.AreEqual((71, 0, 20), (lanza.combate.dano.perforante, lanza.combate.dano.cortante, lanza.combate.dano.contundente));
        Assert.AreEqual(2, lanza.combate.atributos.armadura);
        var torso = CatalogoDelHeroe.ObjetoDe("torso_media_bronce");
        Assert.AreEqual(("media", 948, 12), (torso.tipoArmadura, torso.combate.salud, torso.combate.defensa.cortante));
        Assert.AreEqual(1200, CatalogoDelHeroe.ObjetoDe("montura_tiro").combate.vida, "la montura, con su vida");
    }

    [Test]
    public void UnaClaseOUnObjetoQueConquestNoTieneSeDicen()
    {
        var p = new BronzeAge.Contract.V1.BattleParticipantSnapshot { HeroeId = "heroe-1", Heroe = new BronzeAge.Contract.V1.HeroSnapshot
        {
            ClassDefinitionId = "dagas",
            Equipamiento = new BronzeAge.Contract.V1.Equipamiento
            {
                Arma = new BronzeAge.Contract.V1.ItemInstancia { ItemDefinitionId = "dagas_inicio" },
                Torso = new BronzeAge.Contract.V1.ItemInstancia { ItemDefinitionId = "torso_ligera_inicio" },
            },
        } };
        System.Func<string, bool> clase = id => CatalogoDelHeroe.ClaseDe(id) != null, objeto = id => CatalogoDelHeroe.ObjetoDe(id) != null;
        Assert.IsEmpty(BatallaDeTicket.HeroeDesconocido(p, clase, objeto), "las dagas y sus piezas, conocidas");
        p.Heroe.ClassDefinitionId = "honda_y_escudo";
        p.Heroe.Equipamiento.Casco = new BronzeAge.Contract.V1.ItemInstancia { ItemDefinitionId = "casco_de_oro" };
        CollectionAssert.AreEquivalent(new[] { "la clase honda_y_escudo", "el objeto casco_de_oro" }, BatallaDeTicket.HeroeDesconocido(p, clase, objeto));
        var t = new BronzeAge.Contract.V1.BattleTicket { Atacante = new BronzeAge.Contract.V1.BattleSide(), Defensor = new BronzeAge.Contract.V1.BattleSide() };
        t.Atacante.Participantes.Add(p);
        Assert.AreEqual(2, BatallaDeTicket.ClasesYObjetosDesconocidos(t, clase, objeto).Count, "con quién lo trae: el ticket se rechaza");
    }
}
