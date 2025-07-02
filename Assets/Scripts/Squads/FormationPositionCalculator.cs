using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Calculadora centralizada para posiciones de formación.
/// Unifica toda la lógica de cálculo de posiciones deseadas para las unidades.
/// </summary>
public static class FormationPositionCalculator
{
    /// <summary>
    /// Calcula la posición deseada para una unidad en formación basada en su grid slot.
    /// </summary>
    /// <param name="heroTransform">Transform del héroe (líder del squad)</param>
    /// <param name="gridSlot">Componente de grid slot de la unidad</param>
    /// <param name="useHeroForward">Si true, usa la orientación del héroe para la base de formación</param>
    /// <param name="adjustForTerrain">Si true, ajusta la altura Y al terreno</param>
    /// <returns>Posición deseada en coordenadas del mundo</returns>
    public static float3 CalculateDesiredPosition(
        LocalTransform heroTransform, 
        UnitGridSlotComponent gridSlot, 
        bool useHeroForward = true,
        bool adjustForTerrain = true)
    {
        float3 heroPos = heroTransform.Position;
        float3 formationBase;
        
        if (useHeroForward)
        {
            // Usar orientación del héroe para calcular la base de formación
            float3 heroForward = math.forward(heroTransform.Rotation);
            formationBase = heroPos - heroForward;
        }
        else
        {
            // Usar directamente la posición del héroe como base
            formationBase = heroPos;
        }
        
        // Aplicar el offset de grid
        float3 targetPos = formationBase + gridSlot.worldOffset;
        
        // Ajustar altura del terreno si se solicita
        if (adjustForTerrain && UnityEngine.Terrain.activeTerrain != null)
        {
            float terrainHeight = UnityEngine.Terrain.activeTerrain.SampleHeight(
                new UnityEngine.Vector3(targetPos.x, 0, targetPos.z));
            terrainHeight += UnityEngine.Terrain.activeTerrain.GetPosition().y;
            targetPos.y = terrainHeight;
        }
        
        return targetPos;
    }
    
    /// <summary>
    /// Calcula la posición deseada con base de formación personalizada.
    /// </summary>
    /// <param name="formationBase">Posición base de la formación</param>
    /// <param name="gridSlot">Componente de grid slot de la unidad</param>
    /// <param name="adjustForTerrain">Si true, ajusta la altura Y al terreno</param>
    /// <returns>Posición deseada en coordenadas del mundo</returns>
    public static float3 CalculateDesiredPositionWithBase(
        float3 formationBase,
        UnitGridSlotComponent gridSlot,
        bool adjustForTerrain = true)
    {
        // Aplicar el offset de grid
        float3 targetPos = formationBase + gridSlot.worldOffset;
        
        // Ajustar altura del terreno si se solicita
        if (adjustForTerrain && UnityEngine.Terrain.activeTerrain != null)
        {
            float terrainHeight = UnityEngine.Terrain.activeTerrain.SampleHeight(
                new UnityEngine.Vector3(targetPos.x, 0, targetPos.z));
            terrainHeight += UnityEngine.Terrain.activeTerrain.GetPosition().y;
            targetPos.y = terrainHeight;
        }
        
        return targetPos;
    }
    
    /// <summary>
    /// Calcula la base de formación usando la posición y orientación del héroe.
    /// </summary>
    /// <param name="heroTransform">Transform del héroe</param>
    /// <param name="useHeroForward">Si true, retrocede un paso desde el héroe</param>
    /// <returns>Posición base para la formación</returns>
    public static float3 CalculateFormationBase(LocalTransform heroTransform, bool useHeroForward = true)
    {
        float3 heroPos = heroTransform.Position;
        
        if (useHeroForward)
        {
            float3 heroForward = math.forward(heroTransform.Rotation);
            return heroPos - heroForward;
        }
        else
        {
            return heroPos;
        }
    }
    
    /// <summary>
    /// Verifica si una unidad está en su posición de slot dentro de un threshold.
    /// </summary>
    /// <param name="unitPosition">Posición actual de la unidad</param>
    /// <param name="desiredPosition">Posición deseada</param>
    /// <param name="thresholdSq">Threshold al cuadrado para optimización</param>
    /// <returns>True si la unidad está en posición</returns>
    public static bool IsUnitInSlot(float3 unitPosition, float3 desiredPosition, float thresholdSq = 0.25f)
    {
        float distSq = math.lengthsq(desiredPosition - unitPosition);
        return distSq <= thresholdSq;
    }
}
