using UnityEngine;

/// <summary>
/// Interface para paneles de pantalla completa que pueden ser gestionados
/// por el FullscreenPanelManager de forma coordinada.
/// </summary>
public interface IFullscreenPanel
{
    /// <summary>
    /// Indica si el panel está actualmente abierto.
    /// </summary>
    bool IsPanelOpen { get; }
    
    /// <summary>
    /// Abre el panel sin pasar datos específicos.
    /// </summary>
    void OpenPanel();
    
    /// <summary>
    /// Cierra el panel.
    /// </summary>
    void ClosePanel();
    
    /// <summary>
    /// Alterna el estado del panel (abierto/cerrado).
    /// </summary>
    void TogglePanel();
}
