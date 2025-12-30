using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Traveler.Core.Models;

namespace Traveler.Desktop.Controls;

public partial class DropZoneControl : UserControl
{
    private Border? _dropBorder;
    
    public static readonly StyledProperty<string> TargetIdProperty =
        AvaloniaProperty.Register<DropZoneControl, string>(nameof(TargetId), "vault");
        
    public string TargetId
    {
        get => GetValue(TargetIdProperty);
        set => SetValue(TargetIdProperty, value);
    }
    
    /// <summary>
    /// Event fired when an item is dropped. Parent can handle transfer.
    /// </summary>
    public event Action<InventoryItem, string>? ItemDropped;
    
    public DropZoneControl()
    {
        InitializeComponent();
        
        _dropBorder = this.FindControl<Border>("DropBorder");
        
        // Setup drag & drop events
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }
    
    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        // Highlight drop zone
        if (_dropBorder != null)
        {
            _dropBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(235, 200, 5)); // Gold
            _dropBorder.Background = new SolidColorBrush(Color.FromArgb(40, 235, 200, 5)); // Semi-transparent gold
        }
        e.Handled = true;
    }
    
    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        // Remove highlight
        if (_dropBorder != null)
        {
            _dropBorder.BorderBrush = Brushes.Transparent;
            _dropBorder.Background = Brushes.Transparent;
        }
        e.Handled = true;
    }
    
    private void OnDrop(object? sender, DragEventArgs e)
    {
        // Remove highlight
        if (_dropBorder != null)
        {
            _dropBorder.BorderBrush = Brushes.Transparent;
            _dropBorder.Background = Brushes.Transparent;
        }
        
        try
        {
#pragma warning disable CS0618
            if (e.Data.Contains("InventoryItem"))
            {
                var item = e.Data.Get("InventoryItem") as InventoryItem;
#pragma warning restore CS0618
                if (item != null)
                {
                    Console.WriteLine($"[DropZone] Item dropped: {item.Name} -> Target: {TargetId}");
                    
                    // Fire event for parent to handle transfer
                    ItemDropped?.Invoke(item, TargetId);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DropZone] Error during drop: {ex.Message}");
        }
        
        e.Handled = true;
    }
}
