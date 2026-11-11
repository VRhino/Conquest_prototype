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
        [GhostField] public byte Bando;
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

    /// <summary>Prefabs fantasma creados por código, idénticos en servidor y cliente.</summary>
    public struct PrefabsRed : IComponentData
    {
        public Entity Heroe;
        public Entity Soldado;
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
