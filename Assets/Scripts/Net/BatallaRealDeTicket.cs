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
    /// quedaron caídos. Una escuadra sin héroe recibe un capitán de la IA que no figura en el resultado (decisión del
    /// autor, 2026-11-12).
    /// Lo que aún es de paso, a falta de los catálogos del motor en Conquest (Docs/Coordinacion/Trabajo_En_Conquest.md del
    /// motor): la clase del héroe se traduce con una tabla corta, su aspecto sale de la reserva de héroes de prueba y el
    /// mapa es el de siempre.
    /// </summary>
    public static class BatallaRealDeTicket
    {
        /// <summary>El id del capitán de la IA de una escuadra sin héroe: este prefijo y el id de la escuadra.</summary>
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
                // Los del ticket y, si alguien se unió antes de empezar, también él.
                var participantes = new List<BattleParticipantSnapshot>(lado.Participantes);
                foreach (var unido in batalla.Incorporaciones)
                    if ((unido.Lado == LadoId.Defensor) == (primerPunto == 4))
                        participantes.Add(unido.Participante);
                foreach (var participante in participantes)
                {
                    var heroe = HeroeDe(batalla, participante, primerPunto + enElLado++ % 3, aspectos, orden++, out int suyos);
                    if (heroe == null)
                        continue;
                    soldados += suyos;
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

        /// <summary>
        /// El héroe de un participante con sus escuadras. Nulo si trae una tropa que Conquest no tiene: ese héroe no sale
        /// (en el ticket no pasa, porque se rechaza entero; en una incorporación no hay a quién rechazársela).
        /// </summary>
        static BattleHeroData HeroeDe(BatallaDeTicket batalla, BattleParticipantSnapshot participante, int punto, HeroData[] aspectos, int orden, out int soldados)
        {
            soldados = 0;
            foreach (var escuadra in participante.Escuadras)
                if (!TropaConocida(escuadra.TropaId))
                {
                    UnityEngine.Debug.LogError($"[BatallaReal] {participante.HeroeId} no sale: trae la tropa {escuadra.TropaId}, que Conquest no tiene");
                    batalla.DejarFuera(participante.HeroeId);
                    return null;
                }
            var heroe = Heroe(participante.HeroeId, participante.Heroe?.DisplayName, Clase(participante.Heroe?.ClassDefinitionId),
                (int)(participante.Heroe?.Nivel ?? 1), punto, aspectos, orden);
            foreach (var escuadra in participante.Escuadras)
                soldados += Escuadra(batalla, heroe, escuadra);
            return heroe;
        }

        /// <summary>
        /// Quien se une con la batalla en marcha: su héroe y sus escuadras salen junto a los de su bando. Falso mientras la
        /// escena no está lista para recibirlo (se reintenta).
        /// </summary>
        public static bool Incorporar(BatallaDeTicket batalla, IncorporacionBatalla incorporacion)
        {
            var controlador = UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();
            if (controlador == null)
                return false;
            bool defensor = incorporacion.Lado == LadoId.Defensor;
            int enElLado = batalla.Heroes.FindAll(h => h.Bando == (defensor ? BatallaDeTicket.Defensor : BatallaDeTicket.Atacante)).Count;
            var heroe = HeroeDe(batalla, incorporacion.Participante, (defensor ? 4 : 1) + enElLado % 3, TestHeroPool.Load(), batalla.Heroes.Count, out int soldados);
            if (heroe == null)
                return true;
            if (!controlador.SpawnLateHero(heroe, defensor ? 2 : 1))
                return false;
            UnityEngine.Debug.Log($"[BatallaReal] {heroe.heroId} se une al bando {(defensor ? "defensor" : "atacante")} con {soldados} soldados");
            return true;
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
        /// La escuadra del ticket como instancia de Conquest, con el id del ticket. Se piden sus efectivos autorizados, sin
        /// pasar de los de la tropa; devuelve los pedidos. Los que de verdad salen los dice el juego al crearla (menos, si
        /// su formación no tiene sitio para todos) y los apunta <see cref="ContarBajas"/>: los que no salen no pueden morir.
        /// </summary>
        static int Escuadra(BatallaDeTicket batalla, BattleHeroData heroe, BronzeAge.Contract.V1.SquadSnapshot escuadra)
        {
            var instancia = SquadDataService.CreateSquadInstance(escuadra.TropaId, Math.Max(1, (int)escuadra.Nivel));
            instancia.id = escuadra.SquadId;
            instancia.unitsInSquad = Math.Min(instancia.unitsInSquad, (int)escuadra.EfectivosAutorizados);
            heroe.squadInstances.Add(instancia);
            var enJuego = batalla.Escuadras.Find(e => e.Id == escuadra.SquadId);
            if (enJuego != null)
                enJuego.Desplegados = 0;
            return instancia.unitsInSquad;
        }

        /// <summary>
        /// Lo que lleva cada escuadra del ticket y qué héroes están caídos, leído de las entidades del juego; se llama en
        /// cada tick, para tenerlo al día cuando acabe. De la escuadra que está en el campo: los soldados que el juego
        /// sacó (<c>SquadInstanceComponent.initialUnitCount</c>) y los que le quedan. De una que ya no está en el campo (la
        /// cambiaron por otra, o se retiró al caer su héroe): los que tenía al guardarla. La que no ha salido nunca no tiene
        /// desplegados ni muertos. Si en un tick no se la encuentra en ningún sitio, se queda con la última cuenta.
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
                    bool contada = false;
                    if (entrada.squadId == activa && em.HasComponent<HeroSquadReference>(heroe))
                    {
                        var entidad = em.GetComponentData<HeroSquadReference>(heroe).squad;
                        if (em.Exists(entidad) && em.HasBuffer<SquadUnitElement>(entidad))
                        {
                            if (em.HasComponent<SquadInstanceComponent>(entidad))
                                escuadra.Desplegados = Math.Max(escuadra.Desplegados, em.GetComponentData<SquadInstanceComponent>(entidad).initialUnitCount);
                            vivos = 0;
                            contada = true;
                            foreach (var soldado in em.GetBuffer<SquadUnitElement>(entidad))
                                if (em.Exists(soldado.Value) && !em.HasComponent<IsDeadComponent>(soldado.Value))
                                    vivos++;
                        }
                    }
                    if (!contada && em.HasBuffer<InactiveSquadElement>(heroe))
                    {
                        foreach (var reserva in em.GetBuffer<InactiveSquadElement>(heroe))
                            if (reserva.squadId == entrada.squadId)
                            {
                                vivos = Math.Min(vivos, reserva.aliveUnits);
                                contada = true;
                            }
                    }
                    if (contada)
                        escuadra.Muertos = Math.Clamp(escuadra.Desplegados - vivos, 0, escuadra.Autorizados);
                }
                mapa.Dispose();
            }
        }
    }
}
