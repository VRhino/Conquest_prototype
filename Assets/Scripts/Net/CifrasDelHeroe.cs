using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Las cifras de combate de un héroe del ticket (<see cref="HeroEngineStatsComponent"/>): sus <c>atributos_efectivos</c> y, pieza a pieza,
    /// lo que da lo que lleva puesto. Una pieza forjada trae sus cifras en <c>estadisticas</c> (ya por su calidad); una de tienda o de botín, las
    /// de su ficha de <see cref="CatalogoDelHeroe"/>. Cómo se vuelven vida, alivio y golpe lo dice <see cref="HeroEngineRules"/>.
    /// </summary>
    public static class CifrasDelHeroe
    {
        /// <summary>Las cifras del héroe del ticket. Una pieza que el catálogo no tiene no suma (el ticket ya se rechazó por ella).</summary>
        public static HeroEngineStatsComponent De(BronzeAge.Contract.V1.HeroSnapshot heroe)
        {
            var a = heroe?.AtributosEfectivos;
            var (pesoFuerza, pesoDestreza) = PesosDeDano(heroe?.ClassDefinitionId);
            var c = new HeroEngineStatsComponent
            {
                fromEngine = true,
                strength = a?.Fuerza ?? 0, dexterity = a?.Destreza ?? 0, armor = a?.Armadura ?? 0, vitality = a?.Vitalidad ?? 0,
                strengthDamageWeight = pesoFuerza, dexterityDamageWeight = pesoDestreza,
            };
            var e = heroe?.Equipamiento;
            if (e == null) return c;
            foreach (var pieza in new[] { e.Arma, e.Casco, e.Torso, e.Guantes, e.Pantalones, e.Botas })
            {
                var cifras = CombateDe(pieza);
                if (cifras == null) continue;
                c.equipmentStrength += cifras.atributos.fuerza;
                c.equipmentDexterity += cifras.atributos.destreza;
                c.equipmentArmor += cifras.atributos.armadura;
                c.equipmentVitality += cifras.atributos.vitalidad;
                c.pierceDamage += cifras.dano.perforante; c.slashDamage += cifras.dano.cortante; c.bluntDamage += cifras.dano.contundente;
                c.piercePenetration += cifras.penetracion.perforante; c.slashPenetration += cifras.penetracion.cortante; c.bluntPenetration += cifras.penetracion.contundente;
                c.pierceDefense += cifras.defensa.perforante; c.slashDefense += cifras.defensa.cortante; c.bluntDefense += cifras.defensa.contundente;
                c.armorHealth += cifras.salud;
            }
            return c;
        }

        /// <summary>El caballo de la montura del motor: su vida, su trote, su galope y su aguante; vacío sin montura o si el catálogo no la tiene.</summary>
        public static HeroHorseStatsComponent Montura(string itemDefinitionId)
        {
            var m = CatalogoDelHeroe.ObjetoDe(itemDefinitionId);
            if (m == null || m.tipo != "montura") return default;
            return new HeroHorseStatsComponent { health = m.combate.vida, trotSpeed = m.combate.trote, gallopSpeed = m.combate.galope, stamina = m.combate.aguante };
        }

        /// <summary>
        /// El atributo de daño de la clase (las reglas del héroe del motor): la fuerza en la lanza y el hacha, la destreza en el arco y las dagas,
        /// la media de las dos en la espada.
        /// </summary>
        public static (float fuerza, float destreza) PesosDeDano(string clase) => clase switch
        {
            "lanza_escudo_redondo" or "hacha_dos_manos" => (1f, 0f),
            "arco_lanza" or "dagas" => (0f, 1f),
            _ => (0.5f, 0.5f),
        };

        /// <summary>
        /// El héroe que el motor daría a una clase al nacer: sus atributos base, el arma de pacotilla de su clase, las cuatro piezas de inicio
        /// de su armadura inicial (sin casco) y la montura de tier I. El del capitán de una escuadra sin héroe, que el motor no tiene.
        /// </summary>
        public static BronzeAge.Contract.V1.HeroSnapshot DeInicio(string clase)
        {
            var c = CatalogoDelHeroe.ClaseDe(clase);
            var b = c?.atributosBase ?? new CatalogoDelHeroe.Atributos();
            string peso = c?.armaduraInicial ?? "media";
            ItemInstancia Pieza(string id) => CatalogoDelHeroe.ObjetoDe(id) == null ? null : new ItemInstancia { ItemDefinitionId = id };
            return new BronzeAge.Contract.V1.HeroSnapshot
            {
                ClassDefinitionId = clase,
                AtributosEfectivos = new Atributos { Fuerza = (uint)b.fuerza, Destreza = (uint)b.destreza, Armadura = (uint)b.armadura, Vitalidad = (uint)b.vitalidad },
                Equipamiento = new Equipamiento
                {
                    Arma = Pieza($"{clase}_inicio"),
                    Torso = Pieza($"torso_{peso}_inicio"), Guantes = Pieza($"guantes_{peso}_inicio"),
                    Pantalones = Pieza($"pantalones_{peso}_inicio"), Botas = Pieza($"botas_{peso}_inicio"),
                    Montura = Pieza(CatalogoDelHeroe.TodosLosObjetos.FirstOrDefault(o => o.tipo == "montura" && o.tier == 1)?.id),
                },
            };
        }

        /// <summary>Las cifras de una pieza: las de una forjada, si las trae; si no, las de su ficha. Null sin pieza o fuera del catálogo.</summary>
        public static CatalogoDelHeroe.Combate CombateDe(ItemInstancia pieza)
        {
            if (pieza == null || string.IsNullOrEmpty(pieza.ItemDefinitionId)) return null;
            if (pieza.Estadisticas.Count == 0) return CatalogoDelHeroe.ObjetoDe(pieza.ItemDefinitionId)?.combate;
            var v = new Dictionary<string, int>();
            foreach (var e in pieza.Estadisticas) v[e.Nombre] = (int)e.Valor;
            int D(string nombre) => v.TryGetValue(nombre, out var x) ? x : 0;
            CatalogoDelHeroe.PorTipo PorTipo(string que) => new CatalogoDelHeroe.PorTipo
            {
                perforante = D(que + "Perforante"), cortante = D(que + "Cortante"), contundente = D(que + "Contundente"),
            };
            return new CatalogoDelHeroe.Combate
            {
                atributos = new CatalogoDelHeroe.Atributos { fuerza = D("fuerza"), destreza = D("destreza"), armadura = D("armadura"), vitalidad = D("vitalidad") },
                dano = PorTipo("dano"), penetracion = PorTipo("penetracion"), defensa = PorTipo("defensa"),
                salud = D("salud"),
            };
        }
    }
}
