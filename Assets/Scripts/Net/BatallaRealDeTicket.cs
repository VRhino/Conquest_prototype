using System;
using System.Collections.Generic;
using BronzeAge.Contract.V1;
using Unity.Collections;
using Unity.Entities;

namespace ConquestTactics.Net
{
    /// <summary>
    /// El puente entre un ticket del motor y la batalla de verdad de Conquest: monta su <see cref="BattleData"/> con los
    /// héroes y las escuadras del ticket (cada uno con su id) y, al acabar, cuenta las bajas de cada escuadra y qué héroes
    /// quedaron caídos.
    /// Lo que aún es de paso, a falta de los catálogos del motor en Conquest (Docs/Coordinacion/Trabajo_En_Conquest.md del
    /// motor): la clase del héroe se traduce con una tabla corta, su aspecto sale de la reserva de héroes de prueba, el
    /// mapa es el de siempre y una escuadra sin héroe recibe un capitán de la IA que no figura en el resultado.
    /// </summary>
    public static class BatallaRealDeTicket
    {
        /// <summary>El id del capitán de paso de una escuadra sin héroe: este prefijo y el id de la escuadra.</summary>
        public const string PrefijoCapitan = "capitan:";
        public const int MargenDelPlazo = 5;

        /// <summary>La batalla de verdad solo saca tropas que estén en la base de escuadras del juego (las que hornea la subescena).</summary>
        public static bool TropaConocida(string tropaId) => SquadDataService.GetSquadById(tropaId) != null;

        /// <summary>ponytail: las clases del motor que tienen pariente en Conquest; las demás, espada y escudo. Lo de verdad es el punto 3 del plan de Conquest.</summary>
        static string Clase(string claseDelMotor) => claseDelMotor switch
        {
            "lanza_escudo_redondo" => nameof(HeroClass.Spear),
            "arco_lanza" => nameof(HeroClass.Bow),
            "hacha_dos_manos" => nameof(HeroClass.TwoHandedSword),
            _ => nameof(HeroClass.SwordAndShield),
        };

        public static BattleData Montar(BatallaDeTicket batalla, out int soldados)
        {
            var ticket = batalla.Ticket;
            var datos = BattleDebugCreator.CreateTestBattle(0, 0);
            datos.battleID = ticket.BattleId;
            // El reloj del juego, unos segundos por debajo del plazo del ticket: el motor rechaza un resultado que lo pase.
            datos.BattleTimer = Math.Max(10, (int)batalla.DuracionSegundos - MargenDelPlazo);
            var aspectos = TestHeroPool.Load();
            soldados = 0;
            int orden = 0;
            foreach (var (lado, lista, primerPunto) in new[] { (ticket.Atacante, datos.attackers, 1), (ticket.Defensor, datos.defenders, 4) })
            {
                if (lado == null)
                    continue;
                int enElLado = 0;
                foreach (var participante in lado.Participantes)
                {
                    var heroe = Heroe(participante.HeroeId, participante.Heroe?.DisplayName, Clase(participante.Heroe?.ClassDefinitionId),
                        (int)(participante.Heroe?.Nivel ?? 1), primerPunto + enElLado++ % 3, aspectos, orden++);
                    foreach (var escuadra in participante.Escuadras)
                        soldados += Escuadra(batalla, heroe, escuadra);
                    lista.Add(heroe);
                }
                foreach (var escuadra in lado.EscuadrasSinHeroe)
                {
                    var capitan = Heroe(PrefijoCapitan + escuadra.SquadId, "Capitán", nameof(HeroClass.SwordAndShield), 1,
                        primerPunto + enElLado++ % 3, aspectos, orden++);
                    soldados += Escuadra(batalla, capitan, escuadra);
                    lista.Add(capitan);
                }
            }
            return datos;
        }

        static BattleHeroData Heroe(string id, string nombre, string clase, int nivel, int punto, HeroData[] aspectos, int orden)
        {
            var aspecto = aspectos.Length > 0 ? aspectos[orden % aspectos.Length] : null;
            return new BattleHeroData
            {
                heroId = id,
                heroName = string.IsNullOrEmpty(nombre) ? id : nombre,
                classID = clase,
                level = Math.Max(1, nivel),
                spawnPointId = punto.ToString(),
                avatar = aspecto?.avatar ?? new AvatarParts(),
                equipment = aspecto?.equipment ?? new Equipment(),
                gender = aspecto?.gender ?? "Male",
            };
        }

        /// <summary>
        /// La escuadra del ticket como instancia de Conquest, con el id del ticket. Salen sus efectivos autorizados, sin
        /// pasar de los que caben en la tropa; los que no salen no pueden morir. Devuelve los que salen.
        /// </summary>
        static int Escuadra(BatallaDeTicket batalla, BattleHeroData heroe, BronzeAge.Contract.V1.SquadSnapshot escuadra)
        {
            var instancia = SquadDataService.CreateSquadInstance(escuadra.TropaId, Math.Max(1, (int)escuadra.Nivel));
            instancia.id = escuadra.SquadId;
            instancia.unitsInSquad = Math.Min(instancia.unitsInSquad, (int)escuadra.EfectivosAutorizados);
            heroe.squadInstances.Add(instancia);
            var enJuego = batalla.Escuadras.Find(e => e.Id == escuadra.SquadId);
            if (enJuego != null)
                enJuego.Desplegados = instancia.unitsInSquad;
            return instancia.unitsInSquad;
        }

        /// <summary>
        /// Al cierre: los muertos de cada escuadra del ticket y los héroes que quedaron caídos, leídos de las entidades del
        /// juego. La escuadra en el campo cuenta los soldados que le quedan; una en reserva, los que tenía al guardarla.
        /// </summary>
        public static void ContarBajas(EntityManager em, BatallaDeTicket batalla)
        {
            using var consulta = em.CreateEntityQuery(ComponentType.ReadOnly<BattleParticipantComponent>());
            using var heroes = consulta.ToEntityArray(Allocator.Temp);
            foreach (var heroe in heroes)
            {
                string id = em.GetComponentData<BattleParticipantComponent>(heroe).heroId.ToString();
                var enJuego = batalla.Heroes.Find(h => h.Id == id);
                if (enJuego != null && em.HasComponent<HeroLifeComponent>(heroe))
                    enJuego.Caido = !em.GetComponentData<HeroLifeComponent>(heroe).isAlive;
                if (!em.HasBuffer<SquadIdMapElement>(heroe))
                    continue;

                int activa = em.HasComponent<HeroSquadSelectionComponent>(heroe) ? em.GetComponentData<HeroSquadSelectionComponent>(heroe).instanceId : -1;
                var mapa = em.GetBuffer<SquadIdMapElement>(heroe).ToNativeArray(Allocator.Temp);
                foreach (var entrada in mapa)
                {
                    var escuadra = batalla.Escuadras.Find(e => e.Id == entrada.persistentId.ToString());
                    if (escuadra == null)
                        continue;
                    int vivos = escuadra.Desplegados;
                    if (entrada.squadId == activa && em.HasComponent<HeroSquadReference>(heroe))
                    {
                        var entidad = em.GetComponentData<HeroSquadReference>(heroe).squad;
                        if (em.Exists(entidad) && em.HasBuffer<SquadUnitElement>(entidad))
                        {
                            vivos = 0;
                            foreach (var soldado in em.GetBuffer<SquadUnitElement>(entidad))
                                if (em.Exists(soldado.Value) && !em.HasComponent<IsDeadComponent>(soldado.Value))
                                    vivos++;
                        }
                    }
                    else if (em.HasBuffer<InactiveSquadElement>(heroe))
                    {
                        foreach (var reserva in em.GetBuffer<InactiveSquadElement>(heroe))
                            if (reserva.squadId == entrada.squadId)
                                vivos = reserva.aliveUnits;
                    }
                    escuadra.Muertos = Math.Clamp(escuadra.Desplegados - vivos, 0, escuadra.Autorizados);
                }
                mapa.Dispose();
            }
        }
    }
}
