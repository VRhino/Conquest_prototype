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
        public static string Clase(string claseDelMotor) => claseDelMotor switch
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
                (int)(participante.Heroe?.Nivel ?? 1), punto, aspectos, orden, participante.Heroe);
            foreach (var escuadra in participante.Escuadras)
                soldados += Escuadra(batalla, heroe, escuadra);
            Preparado(batalla.Heroes.Find(h => h.Id == participante.HeroeId), heroe);
            return heroe;
        }

        /// <summary>
        /// Lo que eligió su jugador en la preparación: la escuadra con la que entra va primera (Conquest saca la primera y deja las demás para
        /// los puntos de suministro) y cada una empieza en la formación elegida, si su tropa la tiene. El ticket no cambia: el resultado sigue
        /// trayendo todas sus escuadras.
        /// </summary>
        static void Preparado(HeroeEnJuego elegido, BattleHeroData heroe)
        {
            if (elegido == null)
                return;
            if (elegido.Orden.Count > 0)
            {
                int Puesto(SquadInstanceData s) { int i = elegido.Orden.IndexOf(s.id); return i < 0 ? int.MaxValue : i; }
                var ordenadas = new List<SquadInstanceData>(heroe.squadInstances);
                ordenadas.Sort((a, b) => Puesto(a) != Puesto(b) ? Puesto(a).CompareTo(Puesto(b)) : heroe.squadInstances.IndexOf(a).CompareTo(heroe.squadInstances.IndexOf(b)));
                heroe.squadInstances.Clear();
                heroe.squadInstances.AddRange(ordenadas);
            }
            foreach (var s in heroe.squadInstances)
                if (elegido.Formaciones.TryGetValue(s.id, out var familia) && Familia(familia) is FormationType tipo && s.allowedFormations.Contains(tipo))
                    s.startFormation = tipo;
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

        /// <summary>
        /// Un héroe de la batalla. El de un jugador lleva el aspecto que guardó el motor (`HeroSnapshot`: cabeza, pelo, barba, cejas y género;
        /// un hueco vacío se queda vacío) y la armadura de inicio de Conquest del tipo de la que lleva (ligera, media o pesada), como en el
        /// campamento (`VestidoDelMotor`). El capitán de una escuadra sin héroe, que el motor no tiene, sale de la reserva de héroes de prueba.
        /// ponytail: el arma (y las cifras de las piezas) siguen siendo las de la reserva hasta el equipo pieza a pieza (paso 6 del bloque 4).
        /// </summary>
        static BattleHeroData Heroe(string id, string nombre, string clase, int nivel, int punto, HeroData[] aspectos, int orden, BronzeAge.Contract.V1.HeroSnapshot delMotor = null)
        {
            var aspecto = aspectos.Length > 0 ? aspectos[orden % aspectos.Length] : null;
            var datos = new BattleHeroData
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
            if (delMotor == null)
                return datos;
            var a = delMotor.Avatar;
            datos.avatar = new AvatarParts { headId = a?.CabezaId ?? "", hairId = a?.PeloId ?? "", beardId = a?.BarbaId ?? "", eyebrowId = a?.CejasId ?? "" };
            datos.gender = delMotor.Femenino ? "Female" : "Male";
            var arma = datos.equipment?.weapon;
            datos.equipment = new Equipment { weapon = arma };
            var tipo = TipoDeArmadura(delMotor.Equipamiento?.Torso?.ItemDefinitionId);
            if (tipo != null)
            {
                datos.equipment.torso = ItemInstanceService.CreateItem($"Tor{tipo}ADef");
                datos.equipment.gloves = ItemInstanceService.CreateItem($"Glo{tipo}ADef");
                datos.equipment.pants = ItemInstanceService.CreateItem($"Pan{tipo}ADef");
                datos.equipment.boots = ItemInstanceService.CreateItem($"Boot{tipo}ADef");
            }
            return datos;
        }

        /// <summary>
        /// La letra de Conquest (L, M, H) del tipo de la armadura del motor, por el id de su pieza (`torso_ligera_bronce`: tipo, después del
        /// hueco). Nula sin pieza o con un tipo que no conoce.
        /// ponytail: el id lleva el tipo por convención del catálogo `OBJETOS`; el `tipoArmadura` de verdad no viaja en el ticket. Con el
        /// catálogo de objetos en Conquest (paso 6), de ahí.
        /// </summary>
        public static string TipoDeArmadura(string piezaDelMotor)
        {
            var partes = (piezaDelMotor ?? "").Split('_');
            return partes.Length < 2 ? null : partes[1] switch { "ligera" => "L", "media" => "M", "pesada" => "H", _ => null };
        }

        /// <summary>
        /// La escuadra del ticket como instancia de Conquest, con el id del ticket. Se piden sus efectivos autorizados, sin
        /// pasar de los de la tropa; devuelve los pedidos. Los que de verdad salen los dice el juego al crearla (menos, si
        /// su formación no tiene sitio para todos) y los apunta <see cref="ContarBajas"/>: los que no salen no pueden morir.
        /// </summary>
        /// <summary>La familia de formación del motor (`FORMACIONES`) como tipo de Conquest; nula si no la conoce.</summary>
        public static FormationType? Familia(string id) => id switch
        {
            "reposo" => FormationType.Rest,
            "linea" => FormationType.Line,
            "columna" => FormationType.Column,
            "dispersa" => FormationType.Dispersed,
            "muro_escudos" => FormationType.ShieldWall,
            "testudo" => FormationType.Testudo,
            "cuadro" => FormationType.Square,
            "cuna" => FormationType.Wedge,
            _ => null,
        };

        static int Escuadra(BatallaDeTicket batalla, BattleHeroData heroe, BronzeAge.Contract.V1.SquadSnapshot escuadra)
        {
            var instancia = SquadDataService.CreateSquadInstance(escuadra.TropaId, Math.Max(1, (int)escuadra.Nivel));
            instancia.id = escuadra.SquadId;
            instancia.unitsInSquad = Math.Min(instancia.unitsInSquad, (int)escuadra.EfectivosAutorizados);
            // Las formaciones que permite el ticket y con la que empieza (Heroe_Y_Tropa §10.4 del motor).
            foreach (var familia in escuadra.FormacionesPermitidas)
                if (Familia(familia) is FormationType tipo)
                    instancia.allowedFormations.Add(tipo);
            instancia.allowedFormations.Add(FormationType.Rest);
            instancia.hasStartFormation = true;
            instancia.startFormation = Familia(escuadra.Formacion) ?? FormationType.Rest;
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
