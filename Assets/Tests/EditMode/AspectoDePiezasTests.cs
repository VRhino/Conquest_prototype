// El aspecto de las piezas del motor (tanda 3 de las clases y los objetos): cada arma y cada armadura del catálogo tiene su parte del personaje
// y su icono, y las 5 clases de Conquest llevan el id de su clase del motor.
using System.Linq;
using ConquestTactics.Net;
using NUnit.Framework;
using UnityEngine;

public class AspectoDePiezasTests
{
    [Test]
    public void CadaPiezaDelMotorTieneSuParteYSuIcono()
    {
        var partes = Resources.Load<Data.Avatar.AvatarPartDatabase>("Data/Avatar/AvatarPartDatabase");
        foreach (var o in CatalogoDelHeroe.TodosLosObjetos.Where(o => o.tipo == "arma" || o.tipo == "armadura"))
        {
            Assert.IsTrue(AspectoDePiezas.DelMotor(o.id), $"{o.id}: con aspecto");
            var parte = AspectoDePiezas.Parte(o.id);
            Assert.IsNotNull(Data.Avatar.AvatarVisualUtils.FindAvatarPartById(partes, parte), $"{o.id}: su parte {parte} existe");
            Assert.IsNotNull(AspectoDePiezas.Icono(o.id), $"{o.id}: con icono");
        }
        Assert.IsNull(AspectoDePiezas.Parte(null));
    }

    [Test]
    public void LasCincoClasesLlevanElIdDelMotor()
    {
        foreach (var c in CatalogoDelHeroe.TodasLasClases)
        {
            var asset = HeroClassManager.GetClassDefinition(c.id);
            Assert.IsNotNull(asset, $"{c.id}: con su clase de Conquest");
            Assert.AreEqual((c.atributosBase.fuerza, c.atributosBase.destreza, c.atributosBase.armadura, c.atributosBase.vitalidad),
                (asset.baseStrength, asset.baseDexterity, asset.baseArmor, asset.baseVitality), $"{c.id}: los atributos base del motor");
        }
        Assert.AreEqual(HeroClass.SwordAndShield, HeroClassManager.GetClassDefinition("dagas").heroClass, "las dagas, con el aspecto de espada y escudo");
        Assert.AreSame(HeroClassManager.GetClassDefinition("espada_escudo_mediano"), HeroClassManager.GetClassDefinition(nameof(HeroClass.SwordAndShield)), "un nombre de antes, a su clase del motor");
    }
}
