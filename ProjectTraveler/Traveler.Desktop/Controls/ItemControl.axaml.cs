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

#pragma warning disable CS0618
            await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move);
#pragma warning restore CS0618
        }
    }
}
