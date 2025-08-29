/// <summary>
/// Clase base basica para celdas de item con implementacion basica.
/// solo usa la baseItemCellController.
/// </summary>
public class ItemCellController : BaseItemCellController
{
    protected override System.Type InteractionType => typeof(ItemCellInteraction);
}
