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

    #region Core Positioning Logic

    /// <summary>
    /// Contiene toda la lógica de posicionamiento inteligente.
    /// </summary>
    private class SmartPositioningContext
    {
        public Vector2 CanvasPosition { get; set; }
        public Vector2 CanvasSize { get; set; }
        public Vector2 TooltipSize { get; set; }
        public bool HasEnoughSpaceRight { get; set; }
        public bool HasEnoughSpaceLeft { get; set; }
        public bool ShouldPlaceRight { get; set; }
        public Vector2 Pivot { get; set; }
        public Vector2 FinalPosition { get; set; }
    }

    /// <summary>
    /// Calcula el contexto de posicionamiento inteligente.
    /// </summary>
    private SmartPositioningContext CalculateSmartPositioningContext(Vector3 screenPosition, Vector2 tooltipSize)
    {
        var context = new SmartPositioningContext();

        // Conversión común de coordenadas
        context.CanvasPosition = ConvertScreenToCanvasPosition(screenPosition);
        context.CanvasSize = GetCanvasSize();
        context.TooltipSize = tooltipSize;

        // Cálculo de espacio disponible
        float rightLimit = context.CanvasSize.x / 2f;
        float leftLimit = -context.CanvasSize.x / 2f;

        context.HasEnoughSpaceRight = (rightLimit - context.CanvasPosition.x) >= tooltipSize.x;
        context.HasEnoughSpaceLeft = (context.CanvasPosition.x - leftLimit) >= tooltipSize.x;

        // Lógica de decisión de dirección
        context.ShouldPlaceRight = DeterminePlacementDirection(context);

        return context;
    }

    /// <summary>
    /// Determina la dirección óptima de colocación.
    /// </summary>
    private bool DeterminePlacementDirection(SmartPositioningContext context)
    {
        if (context.HasEnoughSpaceRight) return true;
        if (context.HasEnoughSpaceLeft) return false;
        return true; // Default: forzar derecha si no cabe en ninguno
    }

    /// <summary>
    /// Aplica el posicionamiento inteligente a un tooltip.
    /// </summary>
    private void ApplySmartPositioning(SmartPositioningContext context, RectTransform tooltipRect, float offsetY = 0)
    {
        // Aplicar pivot
        context.Pivot = context.ShouldPlaceRight ? new Vector2(0, 1) : new Vector2(1, 1);
        SetTooltipPivot(tooltipRect, context.Pivot);

        // Calcular posición Y
        float posY = context.CanvasPosition.y - Mathf.Abs(offsetY);

        // Calcular posición X según pivot
        float posX = context.ShouldPlaceRight
            ? context.CanvasPosition.x + Mathf.Abs(_controller.TooltipOffset.x)
            : context.CanvasPosition.x - Mathf.Abs(_controller.TooltipOffset.x);

        context.FinalPosition = new Vector2(posX, posY);

        // Aplicar ajuste final
        context.FinalPosition = AdjustPositionForScreenEdges(context.FinalPosition, tooltipRect);

        tooltipRect.anchoredPosition = context.FinalPosition;
    }

    #endregion

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

        // Calcular contexto para ambos tooltips juntos
        Vector2 combinedSize = new Vector2(
            primarySize.x + secondarySize.x + separation,
            Mathf.Max(primarySize.y, secondarySize.y)
        );

        var context = CalculateSmartPositioningContext(screenPosition, combinedSize);

        // Aplicar posicionamiento dual
        ApplyDualPositioning(context, primaryTooltipRect, secondaryTooltipRect, isPrimary, separation);
    }

    /// <summary>
    /// Aplica el posicionamiento específico para tooltips duales.
    /// </summary>
    private void ApplyDualPositioning(SmartPositioningContext context, RectTransform primaryRect,
                                     RectTransform secondaryRect, bool isPrimary, float separation)
    {
        // Calcular posición Y (alineada para ambos tooltips)
        float offsetY = Mathf.Abs(_controller.TooltipOffset.y);
        float posY = context.CanvasPosition.y - offsetY;

        // Validar límites Y
        var limits = GetCanvasLimits();
        float minY = limits.bottom + Mathf.Max(primaryRect.sizeDelta.y, secondaryRect.sizeDelta.y);
        float maxY = limits.top;
        posY = Mathf.Clamp(posY, minY, maxY);

        Vector2 primaryPos, secondaryPos;
        if (context.ShouldPlaceRight)
        {
            SetTooltipPivot(_tooltipPanel.GetComponent<RectTransform>(), new Vector2(0, 1));
            // Primario a la derecha del cursor
            primaryPos = new Vector2(context.CanvasPosition.x + Mathf.Abs(_controller.TooltipOffset.x), posY);
            // Secundario a la derecha del primario
            secondaryPos = new Vector2(primaryPos.x + primaryRect.sizeDelta.x + separation, posY);
        }
        else
        {
            SetTooltipPivot(_tooltipPanel.GetComponent<RectTransform>(), new Vector2(1, 1));
            // Secundario a la izquierda del cursor
            secondaryPos = new Vector2(context.CanvasPosition.x - Mathf.Abs(_controller.TooltipOffset.x), posY);
            // Primario a la izquierda del secundario
            primaryPos = new Vector2(secondaryPos.x - primaryRect.sizeDelta.x - separation, posY);
        }

        // Aplicar posiciones con ajuste de bordes
        if (isPrimary)
            primaryRect.anchoredPosition = AdjustPositionForScreenEdges(primaryPos, primaryRect);
        else
            secondaryRect.anchoredPosition = AdjustPositionForScreenEdges(secondaryPos, secondaryRect);
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
        }
        else
        {
            UpdateSingleTooltipSmartPosition(screenPosition);
        }
    }

    /// <summary>
    /// Posicionamiento inteligente para tooltip único.
    /// </summary>
    public void UpdateSingleTooltipSmartPosition(Vector3 screenPosition)
    {
        RectTransform tooltipRect = _tooltipPanel.GetComponent<RectTransform>();
        if (tooltipRect == null) return;

        var context = CalculateSmartPositioningContext(screenPosition, tooltipRect.sizeDelta);
        ApplySmartPositioning(context, tooltipRect, _controller.TooltipOffset.y);
    }

    public void UpdatePositionSimple(Vector3 screenPosition)
    {
        // Mantener compatibilidad hacia atrás - redirigir a la nueva lógica inteligente
        UpdateSingleTooltipSmartPosition(screenPosition);
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
    /// Obtiene el tamaño del canvas de forma centralizada.
    /// </summary>
    private Vector2 GetCanvasSize()
    {
        RectTransform canvasRect = _tooltipCanvas.GetComponent<RectTransform>();
        if (_tooltipCanvas.renderMode == RenderMode.ScreenSpaceOverlay || canvasRect == null)
        {
            return new Vector2(Screen.width, Screen.height);
        }
        return canvasRect.sizeDelta;
    }

    /// <summary>
    /// Obtiene los límites del canvas.
    /// </summary>
    private (float left, float right, float bottom, float top) GetCanvasLimits()
    {
        Vector2 canvasSize = GetCanvasSize();
        return (
            -canvasSize.x / 2f,
            canvasSize.x / 2f,
            -canvasSize.y / 2f,
            canvasSize.y / 2f
        );
    }

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
