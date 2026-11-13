// El puente entre «Nace tu héroe» (MundoScene, UI Toolkit) y el creador de personaje de Conquest (la escena AvatarCreator, UGUI): lo que el
// creador necesita para crear el héroe en el motor sin saber nada de la sesión. Con `Actual` en null, el creador hace lo de siempre (las clases
// de relleno y `player_save.json`). C# puro.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConquestTactics.Mundo
{
    public static class SelectorDePersonaje
    {
        /// <summary>Lo que se elige en el creador: la clase del motor, el género (masculino | femenino) y las piezas de `AvatarPartDatabase`.</summary>
        public sealed class Eleccion
        {
            public string ClaseId, Genero, Cabeza, Pelo, Barba, Cejas;
        }

        public sealed class Encargo
        {
            /// <summary>El nombre escrito (puede ir vacío: el motor lo llama «Héroe N») y el lugar donde nace, para enseñarlos.</summary>
            public string Nombre, Lugar;
            /// <summary>Las clases de `/v1/datos`.</summary>
            public List<ClaseDeHeroe> Clases;
            /// <summary>Manda `crearHeroe`: null si se aceptó; si no, la frase del rechazo.</summary>
            public Func<Eleccion, Task<string>> Confirmar;
            /// <summary>Vuelve a «Nace tu héroe» sin crear nada (con el lugar y el nombre que había).</summary>
            public Action Volver;
        }

        /// <summary>El encargo en curso; null fuera del flujo del motor.</summary>
        public static Encargo Actual;
    }
}
