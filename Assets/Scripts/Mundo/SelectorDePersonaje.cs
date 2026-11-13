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

        /// <summary>El héroe de la cuenta para el selector de personaje de Conquest (`CharacterselecctionScene`), que con él sustituye a la pantalla de
        /// partidas: se ve en 3D con su aspecto y la armadura que lleva, su nombre, nivel, clase y Facción, y se entra al mundo o se cierra sesión.</summary>
        public sealed class HeroeParaVer
        {
            public string Nombre, Faccion;
            public uint Nivel;
            /// <summary>Su clase de `/v1/datos` (null si el motor trae una que no está).</summary>
            public ClaseDeHeroe Clase;
            /// <summary>El tipo de la armadura que lleva en el torso (ligera, media o pesada); sin ella, la inicial de su clase.</summary>
            public string Armadura;
            public bool Femenino;
            public string Cabeza, Pelo, Barba, Cejas;
            public Action Entrar, CerrarSesion;
        }

        /// <summary>El héroe que enseña el selector; null fuera del flujo del motor.</summary>
        public static HeroeParaVer Heroe;
    }
}
