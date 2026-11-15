// Los edificios de una plaza tal como los enseña BronzeAgeClient (`paletas.ts`: `EDIFICIO_NOMBRE` y `EDIFICIO_COLOR`): el nombre en español y el
// color del plano. El alto de la caja en el interior 3D es de Conquest (el prototipo dibuja modelos; aquí, cajas hasta que lleguen los de verdad).
// Presentación pura: las cifras del juego salen de `/v1/datos`.
using System.Collections.Generic;

namespace ConquestTactics.Mundo
{
    public static class Edificios
    {
        static readonly Dictionary<string, string> Nombres = new Dictionary<string, string>
        {
            ["centroUrbano"] = "Centro Urbano", ["vivienda"] = "Vivienda", ["granja"] = "Granja", ["cantera"] = "Cantera", ["lenera"] = "Leñera",
            ["almacen"] = "Almacén", ["granero"] = "Granero", ["mina"] = "Mina de oro", ["minaCobre"] = "Mina de cobre", ["minaEstano"] = "Mina de estaño",
            ["minaHierro"] = "Mina de hierro", ["corral"] = "Corral", ["fundicion"] = "Fundición", ["granFundicion"] = "Gran Fundición",
            ["curtiduria"] = "Curtiduría", ["armeria"] = "Armería", ["carpinteria"] = "Carpintería", ["tallerCarpinteria"] = "Taller de carpintería",
            ["barracon"] = "Barracón", ["galeriaDeTiro"] = "Galería de Tiro", ["caballerizas"] = "Caballerizas", ["salaConsejo"] = "Sala del Consejo",
            ["palacio"] = "Palacio", ["mercado"] = "Mercado", ["taberna"] = "Taberna", ["puestoMercado"] = "Puesto de mercado",
            ["albergueCaravanas"] = "Albergue de caravanas", ["maravilla"] = "Maravilla", ["muralla"] = "Muralla", ["plaza"] = "Plaza",
            ["plazaDeArmas"] = "Plaza de Armas", ["patioDeGremios"] = "Patio de Gremios", ["pozo"] = "Pozo", ["parque"] = "Parque",
            ["molino"] = "Molino", ["horno"] = "Horno", ["quesera"] = "Quesera", ["bodega"] = "Bodega", ["prensa"] = "Prensa", ["estatua"] = "Estatua",
        };

        static readonly Dictionary<string, string> Colores = new Dictionary<string, string>
        {
            ["centroUrbano"] = "#9b59b6", ["vivienda"] = "#e8e2d0", ["granja"] = "#d4b106", ["cantera"] = "#8d8d8d", ["lenera"] = "#3f7d3a",
            ["almacen"] = "#7a5c3a", ["granero"] = "#b8933f", ["mina"] = "#f1c40f", ["minaCobre"] = "#c0703c", ["minaEstano"] = "#2f6fd1",
            ["fundicion"] = "#b33a3a", ["granFundicion"] = "#7a1f1f", ["corral"] = "#b5658a", ["curtiduria"] = "#8a5a3c", ["armeria"] = "#a83232",
            ["carpinteria"] = "#6b4226", ["barracon"] = "#8b3a3a", ["galeriaDeTiro"] = "#4a7a4a", ["palacio"] = "#c9a227", ["mercado"] = "#2d9c8f",
            ["taberna"] = "#a8743c", ["puestoMercado"] = "#7fc9bf", ["albergueCaravanas"] = "#4f8a6e", ["maravilla"] = "#ffd700", ["plaza"] = "#f0e8c8",
            ["plazaDeArmas"] = "#c97a7a", ["patioDeGremios"] = "#c99a6b", ["tallerCarpinteria"] = "#a87850", ["pozo"] = "#d8e4e8", ["parque"] = "#c8e0b8",
            ["molino"] = "#c9b98a", ["horno"] = "#b5651d", ["quesera"] = "#e3bf45", ["bodega"] = "#7a1f3a", ["prensa"] = "#7a8a3a", ["estatua"] = "#d8d0c0",
        };

        /// <summary>El alto de la caja de cada tipo en el interior (m). 0: plano (lo que en el prototipo es suelo: plazas, parque, los campos).</summary>
        static readonly Dictionary<string, float> Altos = new Dictionary<string, float>
        {
            ["centroUrbano"] = 7f, ["palacio"] = 8f, ["salaConsejo"] = 6f, ["maravilla"] = 10f, ["vivienda"] = 3f, ["granja"] = 0f, ["corral"] = 1f,
            ["plaza"] = 0f, ["plazaDeArmas"] = 0f, ["parque"] = 0f, ["pozo"] = 1f, ["estatua"] = 4f, ["puestoMercado"] = 2.2f, ["cantera"] = 2f,
            ["lenera"] = 2.5f, ["almacen"] = 5f, ["granero"] = 5f,
        };

        public static string Nombre(string tipo) => tipo != null && Nombres.TryGetValue(tipo, out var n) ? n : tipo;
        public static string Color(string tipo) => tipo != null && Colores.TryGetValue(tipo, out var c) ? c : "#a89a80";
        public static float Alto(string tipo) => tipo != null && Altos.TryGetValue(tipo, out var a) ? a : 4.5f;
    }
}
