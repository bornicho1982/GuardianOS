using Avalonia.Controls;
using Avalonia.Input;
using Traveler.Core.Models;
using Traveler.Desktop.Controls;
using Traveler.Desktop.ViewModels;

namespace Traveler.Desktop.Views;

public partial class InventoryView : UserControl
{
    public InventoryView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, Drop);
        AddHandler(DragDrop.DragOverEvent, DragOver);
    }

    private void DragOver(object? sender, DragEventArgs e)
    {
        // Accept movement
        e.DragEffects = DragDropEffects.Move;
    }

    private async void Drop(object? sender, DragEventArgs e)
    {
        var data = e.Data.Get("InventoryItem");
        if (data is InventoryItem item && DataContext is InventoryViewModel vm)
        {
            var point = e.GetPosition(this);
            var halfWidth = Bounds.Width / 2;
            
            // Left Side = Current Character | Right Side = Vault
            bool toVault = point.X > halfWidth;
            
            if (vm.TransferItemCommand.CanExecute((item, toVault)))
            {
                vm.TransferItemCommand.Execute(new Tuple<InventoryItem, bool>(item, toVault));
            }
        }
    }
    
    // NOTE: StartDrag usually happens on the ItemControl itself using PointerPressed/Moved
}
