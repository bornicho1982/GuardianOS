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
            // Logic: Determine Target from Drop location relative to columns?
            // For now, assume "Vault" as generic target or just log it.
            // In a real grid, we'd check which "Bucket" (Panel) the mouse is over.
            
            // Calling the command on ViewModel
            // await vm.MoveItemCommand.Execute((item, "Vault")); 
            // (Need to expose Command that takes params or simple method)
        }
    }
    
    // NOTE: StartDrag usually happens on the ItemControl itself using PointerPressed/Moved
}
