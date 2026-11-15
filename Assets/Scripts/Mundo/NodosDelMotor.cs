// El id de cada nodo como lo usa el motor (BronzeAgeClient: `src/mundo/artefacto.ts`, `nodosConIdDelMotor`): `nodo-N`, con N su posición entre
// los nodos del artefacto de los tipos que el motor carga (piedra, ganado y los metales). Es el de `pueblo.nodo_id`, `obra.nodo_id` y
// `edificio.fuente_id`, y el que pide `marcarPueblo`; el del artefacto (`piedra-727`) el motor no lo entiende. Los de otro tipo conservan el suyo.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo
{
    public static class NodosDelMotor
    {
        static readonly HashSet<string> Tipos = new HashSet<string> { "piedra", "livestock", "oro", "cobre", "estano", "hierro" };
        static readonly ConditionalWeakTable<ArtefactoDeMundo, Dictionary<string, string>> Hechos = new ConditionalWeakTable<ArtefactoDeMundo, Dictionary<string, string>>();

        /// <summary>El id del motor de un nodo del artefacto.</summary>
        public static string Id(ArtefactoDeMundo a, NodoRecurso n) => Tabla(a).TryGetValue(n.Id, out var id) ? id : n.Id;

        static Dictionary<string, string> Tabla(ArtefactoDeMundo a) => Hechos.GetValue(a, x =>
        {
            var t = new Dictionary<string, string>();
            int i = 0;
            foreach (var n in x.Nodos) t[n.Id] = Tipos.Contains(n.Tipo) ? $"nodo-{i++}" : n.Id;
            return t;
        });
    }
}
