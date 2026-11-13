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

        /// <summary>Un rectángulo de la planta, en unidades locales del campamento (x hacia el este, y hacia el sur), por su centro.</summary>
        public struct PiezaDePlanta
        {
            public string Tipo;
            /// <summary>La zona a la que da esta pieza (taberna, mercado o militar), o null.</summary>
            public string Zona;
            public double X, Y, Ancho, Alto;
        }

        /// <summary>El interior de un campamento de mercenarios en FeudoScene (paso 4 del bloque 4): el héroe que anda y la planta que manda el
        /// motor (`CampamentoMercenarios.planta`, `trazado`, `empalizada`, `empalizada_puerta`).</summary>
        public sealed class InteriorParaVer
        {
            public HeroeParaVer Heroe;
            public List<PiezaDePlanta> Edificios = new List<PiezaDePlanta>(), Calles = new List<PiezaDePlanta>(),
                Empalizada = new List<PiezaDePlanta>(), Puerta = new List<PiezaDePlanta>();
            /// <summary>Se rellena desde FeudoScene: cuándo hay héroe andando y cuánto tarda un fotograma (para el informe y las pruebas).</summary>
            public bool HeroeDentro;
            /// <summary>La zona donde está el héroe (taberna, mercado, militar, puerta) o null; la pone FeudoScene, la enseña la interfaz.</summary>
            public string ZonaCercana;
            /// <summary>El héroe pulsó la tecla de interactuar en una zona (la abre la interfaz).</summary>
            public Action<string> AlInteractuar;
            /// <summary>Hay un panel abierto: el héroe no anda, la cámara no gira y el ratón queda libre (como un diálogo de Conquest).</summary>
            public bool PanelAbierto;
        }

        /// <summary>El interior que se enseña; null fuera del flujo del motor (FeudoScene hace lo de siempre).</summary>
        public static InteriorParaVer Interior;
    }
}
