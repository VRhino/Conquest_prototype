using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace ConquestTactics.Net
{
    /// <summary>
    /// Las tropas que el servidor de batalla sabe jugar: las <see cref="SquadData"/> de Resources/Squads, por su id, que
    /// es el <c>tropaId</c> del motor (CQ-003). Un ticket con una tropa que no está aquí se rechaza.
    /// </summary>
    public static class TropasRed
    {
        static Dictionary<string, SquadData> _porId;

        public static SquadData Buscar(string tropaId)
        {
            if (_porId == null)
            {
                _porId = new Dictionary<string, SquadData>();
                foreach (var tropa in Resources.LoadAll<SquadData>("Squads"))
                    if (!string.IsNullOrEmpty(tropa.id))
                        _porId[tropa.id] = tropa;
            }
            return _porId.TryGetValue(tropaId ?? "", out var encontrada) ? encontrada : null;
        }
    }

    /// <summary>
    /// Dónde va cada soldado al seguir al héroe: la formación de reposo de su tropa (<see cref="SquadData.restFormation"/>),
    /// detrás del héroe, en su marco (+z delante). Las escuadras de un mismo héroe se ponen una detrás de otra.
    /// ponytail: la rejilla dispersa imita a FormationLayout.Scatter sin ser la misma; cuando el juego entre en red, los
    /// huecos los dará su FormationSystem.
    /// </summary>
    public static class FormacionDeReposo
    {
        const float DistanciaAlHeroe = 2.5f;
        const float SeparacionEntreEscuadras = 2f;

        /// <summary>Desfases de <paramref name="soldados"/> soldados empezando en <paramref name="fondo"/> metros detrás del héroe; devuelve el nuevo fondo.</summary>
        public static float Desfases(FormationDefinition reposo, int soldados, uint semilla, float fondo, List<float3> salida)
        {
            var aleatorio = new Unity.Mathematics.Random(semilla == 0 ? 1u : semilla);
            float espaciado = reposo != null ? reposo.EffectiveSpacing : 1.6f;
            float ruido = reposo != null ? reposo.noiseRadius : 0f;
            var locales = new List<float2>(soldados);

            var plantilla = reposo != null && reposo.layout == FormationLayoutKind.Template && reposo.templates.Length > 0
                ? reposo.templates[0].slots : null;
            if (plantilla != null && plantilla.Length >= soldados)
            {
                for (int i = 0; i < soldados; i++)
                    locales.Add(new float2(plantilla[i].position.x, plantilla[i].position.y) * espaciado);
            }
            else
            {
                // Dispersa: celdas al azar de un cuadrado que sobra un poco.
                int lado = (int)math.ceil(math.sqrt(soldados + 1));
                var celdas = new List<int>(lado * lado);
                for (int c = 0; c < lado * lado; c++) celdas.Add(c);
                for (int i = 0; i < soldados; i++)
                {
                    int k = aleatorio.NextInt(i, celdas.Count);
                    (celdas[i], celdas[k]) = (celdas[k], celdas[i]);
                    float2 celda = new float2(celdas[i] % lado, celdas[i] / lado) - (lado - 1) * 0.5f;
                    locales.Add(celda * espaciado);
                }
            }

            float delante = float.MinValue, detras = float.MaxValue;
            foreach (var p in locales) { delante = math.max(delante, p.y); detras = math.min(detras, p.y); }
            float inicio = fondo + DistanciaAlHeroe;
            foreach (var p in locales)
            {
                float2 temblor = ruido > 0f ? aleatorio.NextFloat2Direction() * aleatorio.NextFloat(0f, ruido) : float2.zero;
                salida.Add(new float3(p.x + temblor.x, 0f, -(inicio + (delante - p.y)) + temblor.y));
            }
            return inicio + (delante - detras) + SeparacionEntreEscuadras - DistanciaAlHeroe;
        }
    }
}
