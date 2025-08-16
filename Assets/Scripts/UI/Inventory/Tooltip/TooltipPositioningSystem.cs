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

    /// <summary>
    /// Actualiza la posición aplicando offset de comparación para tooltips secundarios.
    /// </summary>
    public void UpdatePositionWithComparison(Vector3 screenPosition, bool isSecondary = false)
    {
        Vector3 adjustedPosition = screenPosition;
        
        if (isSecondary)
        {
            // Aplicar offset de comparación desde el controller
            adjustedPosition.x += _controller.ComparisonPositionOffset.x;
            adjustedPosition.y += _controller.ComparisonPositionOffset.y;
        }
        
        UpdatePosition(adjustedPosition);
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

        // Obtener dimensiones del canvas
        RectTransform canvasRect = _tooltipCanvas.GetComponent<RectTransform>();
        
        // Usar dimensiones de pantalla para Screen Space Overlay
        Vector2 canvasSize = new Vector2(Screen.width, Screen.height);
        if (_tooltipCanvas.renderMode != RenderMode.ScreenSpaceOverlay && canvasRect != null)
        {
            canvasSize = canvasRect.sizeDelta;
        }
        
        Vector2 tooltipSize = tooltipRect.sizeDelta;
        
        // Usar offset configurable del controller
        float offsetX = Mathf.Abs(_controller.TooltipOffset.x);
        float offsetY = Mathf.Abs(_controller.TooltipOffset.y);

        Vector2 adjustedPosition = canvasPosition;

        // Convertir límites de canvas a coordenadas centradas
        float leftLimit = -canvasSize.x / 2f;
        float rightLimit = canvasSize.x / 2f;
        float bottomLimit = -canvasSize.y / 2f;
        float topLimit = canvasSize.y / 2f;

        // POSICIONAMIENTO HORIZONTAL (sin cambios - ya funciona correctamente)
        // Posición preferida: a la derecha del cursor
        float preferredX = canvasPosition.x + offsetX;
        
        // Verificar si el tooltip cabe a la derecha
        if (preferredX + tooltipSize.x <= rightLimit)
        {
            adjustedPosition.x = preferredX;
        }
        else
        {
            // No cabe a la derecha, intentar a la izquierda
            float leftX = canvasPosition.x - offsetX - tooltipSize.x;
            if (leftX >= leftLimit)
            {
                adjustedPosition.x = leftX;
            }
            else
            {
                // No cabe en ningún lado, forzar dentro de límites
                adjustedPosition.x = Mathf.Clamp(preferredX, leftLimit, rightLimit - tooltipSize.x);
            }
        }

        // POSICIONAMIENTO VERTICAL CORREGIDO PARA PIVOT (0,1)
        // Con pivot (0,1): el tooltip se extiende desde positionY hasta positionY - tooltipSize.y
        
        // Posición preferida: abajo del cursor (tooltip cuelga hacia abajo)
        float preferredY = canvasPosition.y - offsetY;
        
        // Verificar si el tooltip cabe abajo (considerando que crece hacia abajo desde su posición)
        if (preferredY - tooltipSize.y >= bottomLimit)
        {
            // Cabe abajo del cursor - comportamiento normal
            adjustedPosition.y = preferredY;
        }
        else
        {
            // NO cabe abajo, reposicionar ARRIBA del cursor
            float topY = canvasPosition.y + offsetY;
            
            // Verificar si cabe arriba (el tooltip seguirá colgando hacia abajo, pero desde arriba)
            if (topY <= topLimit)
            {
                adjustedPosition.y = topY;
            }
            else
            {
                // No cabe completamente arriba, ajustar para que quepa
                // Posicionar de tal manera que el tooltip no sobrepase el límite superior
                adjustedPosition.y = topLimit;
            }
        }

        // VALIDACIÓN FINAL: Asegurar que el tooltip esté completamente dentro de límites
        // Considerar pivot (0,1): el tooltip se extiende hacia abajo desde su posición
        
        // Limitar X (sin cambios)
        adjustedPosition.x = Mathf.Clamp(
            adjustedPosition.x,
            leftLimit,
            rightLimit - tooltipSize.x
        );

        // Limitar Y para pivot (0,1): la posición Y es la esquina superior, el tooltip cuelga hacia abajo
        adjustedPosition.y = Mathf.Clamp(
            adjustedPosition.y,
            bottomLimit + tooltipSize.y, // Límite inferior + altura (para que no se salga por abajo)
            topLimit                     // Límite superior (esquina superior del tooltip)
        );

        return adjustedPosition;
    }

    #endregion
}
