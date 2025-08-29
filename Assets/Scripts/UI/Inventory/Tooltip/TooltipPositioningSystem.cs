using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sistema de posicionamiento de tooltips que maneja layout, posición en pantalla y detección de bordes.
/// Componente interno responsable de la colocación correcta del tooltip en la UI.
/// </summary>
public class TooltipPositioningSystem : ITooltipComponent
{
    private InventoryTooltipController _controller;
    private GameObject _tooltipPanel;
    private Canvas _tooltipCanvas;
    private bool _followMouse = true;

    /// <summary>
    /// Posiciona dos tooltips (primario y secundario) juntos, eligiendo dirección según espacio disponible.
    /// </summary>
    public void UpdateDualTooltipPosition(Vector3 screenPosition)
    {

        // Obtener referencias y parámetros
        bool isPrimary = _controller.CurrentTooltipType == TooltipType.Primary;
        RectTransform primaryTooltipRect = _controller._primaryTooltipRect;
        RectTransform secondaryTooltipRect = _controller._secondaryTooltipRect;
        float separation = _controller.Separation;

        RectTransform tooltipRect = _tooltipPanel.GetComponent<RectTransform>();
        if (primaryTooltipRect == null || secondaryTooltipRect == null || _tooltipCanvas == null) return;
        
        // Obtener tamaño de ambos tooltips
        Vector2 primarySize = primaryTooltipRect.sizeDelta;
        Vector2 secondarySize = secondaryTooltipRect.sizeDelta;
        
        // Convertir posición de pantalla a posición de canvas
        Vector2 canvasPosition = ConvertScreenToCanvasPosition(screenPosition);


        // Obtener límites del canvas
        RectTransform canvasRect = _tooltipCanvas.GetComponent<RectTransform>();
        Vector2 canvasSize = new Vector2(Screen.width, Screen.height);
        if (_tooltipCanvas.renderMode != RenderMode.ScreenSpaceOverlay && canvasRect != null)
        {
            canvasSize = canvasRect.sizeDelta;
        }
        float leftLimit = -canvasSize.x / 2f;
        float rightLimit = canvasSize.x / 2f;
        float bottomLimit = -canvasSize.y / 2f;
        float topLimit = canvasSize.y / 2f;

        // Calcular ancho total requerido
        float totalWidth = primarySize.x + secondarySize.x + separation;

        // Espacio disponible a la derecha del cursor
        float spaceRight = rightLimit - canvasPosition.x;
        // Espacio disponible a la izquierda del cursor
        float spaceLeft = canvasPosition.x - leftLimit;

        bool placeRight = spaceRight >= totalWidth;

        // Calcular posición Y (alineada para ambos tooltips)
        float offsetY = Mathf.Abs(_controller.TooltipOffset.y);
        float posY = canvasPosition.y - offsetY;
        // Validar que no se salga por abajo/arriba
        float minY = bottomLimit + Mathf.Max(primarySize.y, secondarySize.y);
        float maxY = topLimit;
        posY = Mathf.Clamp(posY, minY, maxY);

        // Calcular posiciones X
        Vector2 primaryPos, secondaryPos;
        if (placeRight)
        {
            SetTooltipPivot(tooltipRect, new Vector2(0, 1));
            // Primario a la derecha del cursor
            primaryPos = new Vector2(canvasPosition.x + Mathf.Abs(_controller.TooltipOffset.x), posY);
            // Secundario a la derecha del primario
            secondaryPos = new Vector2(primaryPos.x + primarySize.x + separation, posY);
        }
        else
        {
            SetTooltipPivot(tooltipRect, new Vector2(1, 1));
            // Secundario a la izquierda del cursor
            secondaryPos = new Vector2(canvasPosition.x - Mathf.Abs(_controller.TooltipOffset.x), posY);
            // Primario a la izquierda del secundario
            primaryPos = new Vector2(secondaryPos.x - secondarySize.x - separation, posY);
        }

        // Actualizar solo el tooltip correspondiente
        if (isPrimary)
            primaryTooltipRect.anchoredPosition = AdjustPositionForScreenEdges(primaryPos, primaryTooltipRect);
        else
            secondaryTooltipRect.anchoredPosition = AdjustPositionForScreenEdges(secondaryPos, secondaryTooltipRect);
    }

    /// <summary>
    /// Cambia el pivot de un tooltip.
    /// </summary>
    private void SetTooltipPivot(RectTransform tooltipRect, Vector2 pivot)
    {
        if (tooltipRect != null)
            tooltipRect.pivot = pivot;
    }

    #region ITooltipComponent Implementation

    public void Initialize(InventoryTooltipController controller)
    {
        _controller = controller;
        _tooltipPanel = controller.TooltipPanel;
        _tooltipCanvas = controller.TooltipCanvas;
        _followMouse = controller.FollowMouse;
    }

    public void Cleanup()
    {
        _controller = null;
        _tooltipPanel = null;
        _tooltipCanvas = null;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Actualiza la posición del tooltip basada en la posición de la pantalla.
    /// </summary>
    public void UpdatePosition(Vector3 screenPosition)
    {
        if (_tooltipPanel == null || !_tooltipPanel.activeInHierarchy) return;

        if (_controller.isDual)
        {
            UpdateDualTooltipPosition(screenPosition);
            return;
        }
        else
        {
            UpdatePositionSimple(screenPosition);
            return;
        }
    }

    public void UpdatePositionSimple(Vector3 screenPosition)
    {
        RectTransform tooltipRect = _tooltipPanel.GetComponent<RectTransform>();
        if (tooltipRect == null) return;

        // Convertir posición de pantalla a posición de canvas
        Vector2 canvasPosition = ConvertScreenToCanvasPosition(screenPosition);

        // Aplicar detección de bordes
        Vector2 adjustedPosition = AdjustPositionForScreenEdges(canvasPosition, tooltipRect);

        // Aplicar la posición final
        tooltipRect.anchoredPosition = adjustedPosition;
    }

    /// <summary>
    /// Fuerza la reconstrucción del layout del tooltip.
    /// </summary>
    public void ForceLayoutRebuild()
    {
        if (_tooltipPanel == null) return;

        // Forzar rebuild de todos los layout groups
        Canvas.ForceUpdateCanvases();

        RectTransform tooltipRect = _tooltipPanel.GetComponent<RectTransform>();
        if (tooltipRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
        }

        // Rebuild específico de paneles internos
        if (_controller.TitlePanel != null)
        {
            RectTransform titleRect = _controller.TitlePanel.GetComponent<RectTransform>();
            if (titleRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(titleRect);
        }

        if (_controller.ContentPanel != null)
        {
            RectTransform contentRect = _controller.ContentPanel.GetComponent<RectTransform>();
            if (contentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        if (_controller.InteractionPanel != null)
        {
            RectTransform interactionRect = _controller.InteractionPanel.GetComponent<RectTransform>();
            if (interactionRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(interactionRect);
        }
    }

    /// <summary>
    /// Configura si el tooltip debe seguir al mouse.
    /// </summary>
    public void SetFollowMouse(bool follow)
    {
        _followMouse = follow;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Convierte posición de pantalla a posición de canvas.
    /// </summary>
    private Vector2 ConvertScreenToCanvasPosition(Vector3 screenPosition)
    {
        if (_tooltipCanvas == null) return Vector2.zero;

        // Si es un canvas de tipo Screen Space - Overlay
        if (_tooltipCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Convertir de coordenadas de pantalla a coordenadas de canvas
            Vector2 overlayPosition = new Vector2(
                screenPosition.x - Screen.width * 0.5f,
                screenPosition.y - Screen.height * 0.5f
            );
            return overlayPosition;
        }

        // Para otros tipos de canvas, convertir usando la cámara
        Vector2 canvasPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _tooltipCanvas.transform as RectTransform,
            screenPosition,
            _tooltipCanvas.worldCamera,
            out canvasPosition);

        return canvasPosition;
    }

    /// <summary>
    /// Ajusta la posición del tooltip para que no salga de los bordes de la pantalla.
    /// CORREGIDO: Considera pivot (0,1) - tooltip cuelga hacia abajo desde su posición.
    /// </summary>
    private Vector2 AdjustPositionForScreenEdges(Vector2 canvasPosition, RectTransform tooltipRect)
    {
        if (_tooltipCanvas == null) return canvasPosition;
        RectTransform canvasRect = _tooltipCanvas.GetComponent<RectTransform>();
        Vector2 canvasSize = new Vector2(Screen.width, Screen.height);
        if (_tooltipCanvas.renderMode != RenderMode.ScreenSpaceOverlay && canvasRect != null)
            canvasSize = canvasRect.sizeDelta;
        Vector2 tooltipSize = tooltipRect.sizeDelta;
        float leftLimit = -canvasSize.x / 2f;
        float rightLimit = canvasSize.x / 2f;
        float bottomLimit = -canvasSize.y / 2f;
        float topLimit = canvasSize.y / 2f;

        Vector2 adjustedPosition = canvasPosition;

        // Ajuste horizontal según pivot
        if (tooltipRect.pivot.x == 0f) {
            // Tooltip cuelga hacia la derecha
            if (adjustedPosition.x + tooltipSize.x > rightLimit)
                adjustedPosition.x = rightLimit - tooltipSize.x;
            if (adjustedPosition.x < leftLimit)
                adjustedPosition.x = leftLimit;
        } else {
            // Tooltip cuelga hacia la izquierda
            if (adjustedPosition.x - tooltipSize.x < leftLimit)
                adjustedPosition.x = leftLimit + tooltipSize.x;
            if (adjustedPosition.x > rightLimit)
                adjustedPosition.x = rightLimit;
        }

        // Ajuste vertical (si se sale por arriba o abajo)
        if (adjustedPosition.y - tooltipSize.y < bottomLimit)
            adjustedPosition.y = bottomLimit + tooltipSize.y;
        if (adjustedPosition.y > topLimit)
            adjustedPosition.y = topLimit;

        return adjustedPosition;
    }

    #endregion
}
