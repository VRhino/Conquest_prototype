using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace ConquestTactics.Net
{
    /// <summary>Lo único que el cliente manda: hacia dónde quiere ir su héroe, en el plano del mundo (largo ≤ 1).</summary>
    public struct EntradaHeroe : IInputComponentData
    {
        public float2 Mover;
    }

    /// <summary>
    /// Lo que manda el jugador que lleva un héroe en la batalla de verdad. Viaja en su entidad de mando
    /// (<see cref="PrefabsRed.Mando"/>) y el servidor lo convierte en la decisión de ese héroe (<see cref="MandoSystem"/>).
    /// </summary>
    public struct EntradaJugador : IInputComponentData
    {
        /// <summary>Hacia dónde quiere ir, en el plano del mundo (largo ≤ 1).</summary>
        public float2 Mover;
        public byte Correr;
        /// <summary>Mantiene el ataque básico.</summary>
        public byte Atacar;
        /// <summary>Lanza la habilidad de <see cref="Ranura"/> (un <c>SkillSlot</c>).</summary>
        public InputEvent Habilidad;
        public byte Ranura;
        /// <summary>Da a su escuadra la orden <see cref="TipoDeOrden"/> (un <c>SquadOrderType</c>).</summary>
        public InputEvent Orden;
        public byte TipoDeOrden;
        /// <summary>Para «mantener posición»: el punto del terreno al que apunta el ratón, como en el juego; (0,0,0) = donde esté el héroe.</summary>
        public float3 PuntoDeOrden;
    }

    /// <summary>
    /// Lo que el mando necesita para moverse igual en el cliente (predicción) y en el servidor: las velocidades de su
    /// héroe y si ahora puede moverse (no mientras está caído o aún no tiene héroe). Lo pone el servidor.
    /// </summary>
    public struct MandoMovil : IComponentData
    {
        [GhostField] public byte Activo;
        [GhostField(Quantization = 100)] public float Velocidad;
        [GhostField(Quantization = 100)] public float VelocidadAlCorrer;
    }

    /// <summary>
    /// Lo que la interfaz del juego enseña del héroe y la escuadra de un jugador (lo que UIBattleDataSystem saca del héroe
    /// local en el juego de un jugador). Va en su mando y solo le llega a él.
    /// </summary>
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct InterfazRed : IComponentData
    {
        [GhostField(Quantization = 10)] public float Vida;
        [GhostField(Quantization = 10)] public float VidaMaxima;
        [GhostField(Quantization = 10)] public float Resistencia;
        [GhostField(Quantization = 10)] public float ResistenciaMaxima;
        [GhostField] public byte Vivo;
        [GhostField(Quantization = 100)] public float Combo;
        [GhostField(Quantization = 100)] public float CosteDeDefinitiva;
        [GhostField] public ushort SinPagar;
        [GhostField] public int Soldados;
        [GhostField] public int SoldadosTotales;
        [GhostField] public byte Formacion;
    }

    /// <summary>Una casilla de la barra de habilidades del jugador (de Q a la tercera de escuadra), con su recarga.</summary>
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    [InternalBufferCapacity(UIHeroBattleDataComponent.SkillSlotCount)]
    public struct HabilidadRed : IBufferElementData
    {
        [GhostField] public int Id;
        [GhostField(Quantization = 1000)] public float Recarga;
        [GhostField(Quantization = 10)] public float Queda;
    }

    /// <summary>Héroe en red. La velocidad viene del prefab, igual en servidor y cliente; no viaja.</summary>
    public struct HeroeRed : IComponentData
    {
        public float Velocidad;
    }

    /// <summary>Héroe sin jugador: lo mueve el servidor para llenar la batalla en la medición.</summary>
    public struct HeroeSinJugador : IComponentData
    {
        public float Fase;
    }

    /// <summary>Soldado replicado. El cliente solo necesita su bando para pintarlo; su posición viaja en el LocalTransform.</summary>
    public struct SoldadoRed : IComponentData
    {
        public const byte Soldado = 0;
        public const byte Heroe = 1;

        [GhostField] public byte Bando;
        /// <summary><see cref="Soldado"/> o, en la batalla de verdad, <see cref="Heroe"/> (un héroe de la IA, que aún no lleva nadie).</summary>
        [GhostField] public byte Tipo;
        /// <summary>Id de red del jugador que lleva a este héroe de la batalla de verdad, o a la escuadra de este soldado; 0 = lo lleva la IA.</summary>
        [GhostField] public int Dueno;
    }

    /// <summary>
    /// Lo que solo sabe el servidor de cada soldado: a quién sigue y dónde va respecto a él (en su marco: +z delante),
    /// sacado de la formación de reposo de su tropa.
    /// </summary>
    public struct SoldadoEnServidor : IComponentData
    {
        public Entity Heroe;
        public float3 Desfase;
    }

    /// <summary>Espejo de la batalla de verdad: con qué modelo se pinta. No cambia, así que solo viaja al aparecer.</summary>
    public struct VisualRed : IComponentData
    {
        /// <summary>El <c>visualPrefabName</c> del soldado; si el registro no lo conoce, vale el de su tipo de escuadra.</summary>
        [GhostField] public FixedString64Bytes Prefab;
        [GhostField] public byte TipoDeEscuadra;
    }

    /// <summary>Espejo de la batalla de verdad: lo que el Animator del cliente necesita (lo que UnitAnimationAdapter lee del soldado).</summary>
    public struct AnimacionRed : IComponentData
    {
        public const byte Moviendose = 1, Atacando = 2, Disparando = 4, Recargando = 8, ConMarcha = 16, Corre = 32;
        /// <summary>Solo héroes: caído, y con el cuerpo ya retirado hasta que reaparezca.</summary>
        public const byte Muerto = 64, Oculto = 128;

        [GhostField(Quantization = 10)] public float Velocidad;
        [GhostField(Quantization = 10)] public float VelocidadBase;
        [GhostField(Quantization = 10)] public float VelocidadPedida;
        [GhostField(Quantization = 10)] public float VelocidadDeAtaque;
        [GhostField] public byte Banderas;
        [GhostField] public byte Postura;
        [GhostField] public byte Fila;
        [GhostField] public byte Papel;
        [GhostField] public byte Reaccion;
        [GhostField] public uint RevisionDeReaccion;
        /// <summary>Solo héroes: sube uno con cada golpe que lanza (el disparador de su animación de ataque).</summary>
        [GhostField] public byte Ataques;
        [GhostField] public uint RevisionDeMuerte;
    }

    /// <summary>
    /// Espejo de un héroe: su aspecto, que solo viaja al aparecer. Género, las cuatro partes del avatar y las seis piezas
    /// del equipo (<c>id:tipo</c>), separadas por <c>|</c>; ver <see cref="AparienciaRed.De"/>.
    /// </summary>
    public struct AparienciaRed : IComponentData
    {
        [GhostField] public FixedString512Bytes Texto;

        public static FixedString512Bytes De(HeroAppearanceComponent a)
        {
            var e = a.equipment ?? new Equipment();
            var v = a.avatar ?? new AvatarParts();
            return string.Join("|", a.gender, v.headId, v.hairId, v.beardId, v.eyebrowId,
                Pieza(e.weapon), Pieza(e.helmet), Pieza(e.torso), Pieza(e.gloves), Pieza(e.pants), Pieza(e.boots));
        }

        public static HeroAppearanceComponent Leer(string texto)
        {
            var p = texto.Split('|');
            if (p.Length != 11)
                return null;
            return new HeroAppearanceComponent
            {
                gender = p[0],
                avatar = new AvatarParts { headId = p[1], hairId = p[2], beardId = p[3], eyebrowId = p[4] },
                equipment = new Equipment
                {
                    weapon = Pieza(p[5]), helmet = Pieza(p[6]), torso = Pieza(p[7]),
                    gloves = Pieza(p[8]), pants = Pieza(p[9]), boots = Pieza(p[10]),
                },
            };
        }

        static string Pieza(InventoryItem pieza) =>
            pieza == null || string.IsNullOrEmpty(pieza.itemId) ? "" : pieza.itemId + ":" + (int)pieza.itemType;

        static InventoryItem Pieza(string texto)
        {
            var p = texto.Split(':');
            return p.Length == 2 ? new InventoryItem { itemId = p[0], itemType = (ItemType)int.Parse(p[1]) } : null;
        }
    }

    /// <summary>
    /// Espejo de un soldado de la escuadra de un jugador: el punto donde va a formar mientras cumple «mantener posición»
    /// y aún no ha llegado. El cliente de ese jugador pinta ahí el marcador de destino, como el juego.
    /// </summary>
    public struct DestinoRed : IComponentData
    {
        [GhostField] public byte Activo;
        [GhostField(Quantization = 100)] public float3 Punto;
    }

    /// <summary>Prefabs fantasma creados por código, idénticos en servidor y cliente.</summary>
    public struct PrefabsRed : IComponentData
    {
        public Entity Heroe;
        public Entity Soldado;
        /// <summary>Soldado o héroe de la batalla de verdad: un <see cref="SoldadoRed"/> con su modelo y su animación.</summary>
        public Entity Espejo;
        /// <summary>El espejo de un héroe: lo mismo más su aspecto (<see cref="AparienciaRed"/>).</summary>
        public Entity EspejoHeroe;
        /// <summary>La entidad de mando de un jugador de la batalla de verdad: su <see cref="EntradaJugador"/> y dónde está su héroe, predicho.</summary>
        public Entity Mando;
    }

    /// <summary>RPC del cliente: ya está listo para recibir la batalla, con el token de su héroe si viene de una batalla del motor.</summary>
    public struct EntrarEnBatalla : IRpcCommand
    {
        public FixedString128Bytes Token;
    }

    /// <summary>Servidor: a qué bando pertenece algo que combate o que guía a una escuadra (0 atacante, 1 defensor).</summary>
    public struct BandoServidor : IComponentData
    {
        public byte Bando;
    }

    /// <summary>Servidor: vida de un héroe o soldado de una batalla de ticket.</summary>
    public struct VidaServidor : IComponentData
    {
        public float Vida;
        public float DanoPorSegundo;
    }

    /// <summary>Servidor: el enemigo vivo más cercano, que recalcula el combate cada tick.</summary>
    public struct ObjetivoCombate : IComponentData
    {
        public Entity Enemigo;
        public float3 Posicion;
        public float Distancia;
    }

    /// <summary>Servidor: dónde se apunta en el ticket lo que le pase (índices de las tablas de <see cref="BatallaDeTicket"/>; -1 = nada).</summary>
    public struct RegistroEnTicket : IComponentData
    {
        public int Escuadra;
        public int Heroe;
    }
}
