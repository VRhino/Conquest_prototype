// Dónde viven los ficheros de las tropas de la Edad del Bronce (calcado de las escuadras de Conquest, p. ej. Squires):
//   Assets/Resources/Squads/BronzeAge/<id>/   TODO lo de la tropa: <id>.asset, <id>_Melee.asset, Formations/,
//       Animations/ (clips FBX, AC_ y AOC_), Models/ (sus figuras A..D), Materials/ y Textures/ (su atlas),
//       BattleVisuals/ (los prefabs de batalla A..D), <id>_GO_Squeleton.prefab, UnitPrefab_ECS_<id>.prefab,
//       <id>_icon.png y <id>_miniature.png
//   Assets/BronzeAgeArte/Comun/                         lo común: tonos de piel, guiones (Editor, Scripts)
//   Assets/BronzeAgeArte/Demo/                          la escena de demostración, puestos.json, peana y suelo
// Los modelos, clips y materiales se buscan por su nombre (son únicos) en esas carpetas.
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BronzeAge
{
    public static class RutasBronzeAge
    {
        public const string ESCUADRAS = "Assets/Resources/Squads/BronzeAge";
        public const string COMUN = "Assets/BronzeAgeArte/Comun";
        public const string DEMO = "Assets/BronzeAgeArte/Demo";
        static readonly string[] RAICES = { ESCUADRAS, COMUN, DEMO };

        // índice de los .fbx, .mat y .png de las raíces: un solo recorrido del disco, hasta que algo cambie (Olvidar)
        static Dictionary<string, string> indice;
        static string[] todos;
        static readonly string[] EXTENSIONES = { ".fbx", ".mat", ".png" };

        static Dictionary<string, string> Indice()
        {
            if (indice != null) return indice;
            indice = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            var lista = new List<string>();
            foreach (var raiz in RAICES)
            {
                if (!Directory.Exists(raiz)) continue;
                foreach (var f in Directory.GetFiles(raiz, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (System.Array.IndexOf(EXTENSIONES, ext) < 0) continue;
                    var r = f.Replace('\\', '/');
                    lista.Add(r);
                    var clave = Path.GetFileName(r);
                    if (!indice.ContainsKey(clave)) indice[clave] = r;          // la primera raíz gana, como antes
                }
            }
            todos = lista.ToArray();
            return indice;
        }

        /// <summary>Vacía el índice (lo llama el editor cuando entran, se mueven o se borran ficheros).</summary>
        public static void Olvidar() { indice = null; todos = null; }

        /// <summary>Ruta («Assets/...») del fichero de nuestras tropas con ese nombre y extensión (".fbx", ".mat", ".png"),
        /// o null si no está.</summary>
        public static string Buscar(string nombre, string extension)
        {
            if (string.IsNullOrEmpty(nombre)) return null;
            return Indice().TryGetValue(nombre + extension, out var r) ? r : null;
        }

        /// <summary>Los FBX de clips de todas las escuadras (sus carpetas Animations).</summary>
        public static string[] Clips() { Indice(); return todos.Where(EsClip).Where(r => r.StartsWith(ESCUADRAS + "/")).ToArray(); }

        /// <summary>Los FBX de modelos de todas las tropas.</summary>
        public static string[] Modelos() { Indice(); return todos.Where(r => r.EndsWith(".fbx") && r.StartsWith(ESCUADRAS + "/") && r.Contains("/Models/")).ToArray(); }

        public static bool EsModelo(string ruta) => ruta.StartsWith(ESCUADRAS + "/") && ruta.Contains("/Models/") || ruta.StartsWith(DEMO + "/Modelos/");
        public static bool EsClip(string ruta) => ruta.StartsWith(ESCUADRAS + "/") && ruta.Contains("/Animations/") || ruta.StartsWith(DEMO + "/Animaciones/");
    }
}
