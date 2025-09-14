using UnityEngine;

/// <summary>
/// ScriptableObject temporal para transferir datos de batalla entre escenas.
/// Se crea dinámicamente y se destruye después de usar.
/// </summary>
public class BattleTransitionData : ScriptableObject
{
    #region Singleton Pattern

    private static BattleTransitionData _instance;

    public static BattleTransitionData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = CreateInstance<BattleTransitionData>();
            }
            return _instance;
        }
    }

    #endregion

    #region Data

    private BattleData _battleData;

    #endregion

    #region Public API

    /// <summary>
    /// Establece los datos de batalla para transferir.
    /// </summary>
    /// <param name="battleData">Datos de la batalla asignada</param>
    public void SetBattleData(BattleData battleData)
    {
        _battleData = battleData;
        Debug.Log($"[BattleTransitionData] Battle data set: {_battleData?.battleID}");
    }

    /// <summary>
    /// Obtiene los datos de batalla y limpia la referencia.
    /// </summary>
    /// <returns>Datos de batalla o null si no hay datos</returns>
    public BattleData GetAndClearBattleData()
    {
        BattleData data = _battleData;
        _battleData = null;

        if (data != null)
        {
            Debug.Log($"[BattleTransitionData] Battle data retrieved and cleared: {data.battleID}");
        }

        return data;
    }

    /// <summary>
    /// Verifica si hay datos de batalla disponibles.
    /// </summary>
    /// <returns>True si hay datos disponibles</returns>
    public bool HasBattleData()
    {
        return _battleData != null;
    }

    /// <summary>
    /// Limpia los datos de batalla.
    /// </summary>
    public void ClearData()
    {
        _battleData = null;
        Debug.Log("[BattleTransitionData] Data cleared");
    }

    #endregion

    #region Unity Lifecycle

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    #endregion
}