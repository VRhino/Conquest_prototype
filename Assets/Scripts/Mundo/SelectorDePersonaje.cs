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
            /// <summary>El sigilo de su Facción (null sin Facción o si no ha llegado).</summary>
            public BronzeAge.Contract.V1.Sigilo Sigilo;
            public uint Nivel;
            /// <summary>Su clase de `/v1/datos` (null si el motor trae una que no está).</summary>
            public ClaseDeHeroe Clase;
            /// <summary>El tipo de la armadura que lleva en el torso (ligera, media o pesada); sin ella, la inicial de su clase.</summary>
            public string Armadura;
            public bool Femenino;
            public string Cabeza, Pelo, Barba, Cejas;
            /// <summary>Lo que lleva puesto, por hueco del motor (torso, guantes, pantalones, botas, casco, arma, montura) y el id de su pieza en `OBJETOS`;
            /// null si no se sabe (entonces se viste con las piezas de inicio de su tipo de armadura).</summary>
            public System.Collections.Generic.Dictionary<string, string> Piezas;
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
            /// <summary>En una plaza (paso 7): el id del edificio (`ed-N`) y si está en cola u en obra («cola», «obra»); null, en pie. En la
            /// muralla, «plan» para la celda comprometida que aún no se ha levantado.</summary>
            public string Id, Estado;
            /// <summary>El nivel de la celda de muralla (1 empalizada, 2 adobe, 3 ciclópea) o el del edificio.</summary>
            public int Nivel;
        }

        /// <summary>El interior de un campamento de mercenarios en FeudoScene (paso 4 del bloque 4): el héroe que anda y la planta que manda el
        /// motor (`CampamentoMercenarios.planta`, `trazado`, `empalizada`, `empalizada_puerta`).</summary>
        public sealed class InteriorParaVer
        {
            public HeroeParaVer Heroe;
            public List<PiezaDePlanta> Edificios = new List<PiezaDePlanta>(), Calles = new List<PiezaDePlanta>(),
                Empalizada = new List<PiezaDePlanta>(), Puerta = new List<PiezaDePlanta>();
            /// <summary>La muralla de una plaza, celda a celda (`Recinto.celdas`: Tipo «muro», «torre» o «puerta»).</summary>
            public List<PiezaDePlanta> Muralla = new List<PiezaDePlanta>();
            /// <summary>Lo que manda el motor para vestir la plaza (`Asentamiento.nivel`, `ascenso` y `estilo_epoca`): su nivel, el ascenso en
            /// curso (`NivelObjetivo` 0 si no hay; sus tiempos en milisegundos de mundo) y la Era de su Facción con su grado. Con ello el arte
            /// reparte el estilo y la época edificio a edificio (`EstiloDeCiudad`), como el prototipo.</summary>
            public int NivelDePlaza = 1, NivelObjetivo, Era = 1, Grado = 3;
            public double AscensoDesdeMs, AscensoHastaMs;
            /// <summary>Sube cuando cambia la planta (una obra que acaba, un edificio nuevo en la cola): FeudoScene la vuelve a montar.</summary>
            public int Version;
            /// <summary>Lo pone FeudoScene: dónde cae en pantalla (de 0 a 1, desde arriba a la izquierda) lo alto de una pieza por su id; null si no
            /// se ve. Con él la interfaz pone el cartel de cada obra encima de su edificio.</summary>
            public Func<string, (float x, float y)?> EnPantalla;
            /// <summary>El edificio elegido (su ficha abierta): FeudoScene lo resalta, como el contorno dorado del prototipo.</summary>
            public string Elegido;
            /// <summary>Se rellena desde FeudoScene: cuándo hay héroe andando y cuánto tarda un fotograma (para el informe y las pruebas).</summary>
            public bool HeroeDentro;
            /// <summary>La zona donde está el héroe (taberna, mercado, militar, puerta) o null; la pone FeudoScene, la enseña la interfaz.</summary>
            public string ZonaCercana;
            /// <summary>El héroe pulsó la tecla de interactuar en una zona (la abre la interfaz).</summary>
            public Action<string> AlInteractuar;
            /// <summary>El reloj del mundo, en milisegundos (`Sesion.AhoraMundo`): de él saca FeudoScene la hora del día para la luz de la plaza.</summary>
            public Func<double> AhoraMundo;
            /// <summary>Hay un panel abierto: el héroe no anda, la cámara no gira y el ratón queda libre (como un diálogo de Conquest).</summary>
            public bool PanelAbierto;
        }

        /// <summary>El interior que se enseña; null fuera del flujo del motor (FeudoScene hace lo de siempre).</summary>
        public static InteriorParaVer Interior;
    }
}
