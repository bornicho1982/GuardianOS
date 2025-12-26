using Avalonia.Controls;
using Avalonia.Input;
using Traveler.Core.Models;

namespace Traveler.Desktop.Controls;

public partial class ItemControl : UserControl
{
    public ItemControl()
    {
        InitializeComponent();
    }

    protected override async void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (DataContext is InventoryItem item)
        {
            var dragData = new DataObject();
            dragData.Set("InventoryItem", item);

            // Initiate Drag
            await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move);
        }
    }
}
