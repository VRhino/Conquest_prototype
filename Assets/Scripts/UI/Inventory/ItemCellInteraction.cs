public class ItemCellInteraction : BaseItemCellInteraction
{
    public override void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (_currentItem != null && _currentItemData != null) base.OnPointerClick(eventData);
    }
}
