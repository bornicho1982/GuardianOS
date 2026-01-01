using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Traveler.Core.Models;
using ReactiveUI;

namespace Traveler.Desktop.Stores;

public class InventoryStore : ReactiveObject
{
    // Singleton Instance
    private static InventoryStore? _instance;
    public static InventoryStore Instance => _instance ??= new InventoryStore();

    private ObservableCollection<InventoryItem> _allItems = new();
    public ObservableCollection<InventoryItem> AllItems
    {
        get => _allItems;
        set => this.RaiseAndSetIfChanged(ref _allItems, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public event Action? ItemsChanged;

    private InventoryStore()
    {
        // Private constructor for Singleton
    }

    public void UpdateItems(IEnumerable<InventoryItem> newItems)
    {
        AllItems.Clear();
        foreach (var item in newItems)
        {
            AllItems.Add(item);
        }
        ItemsChanged?.Invoke();
    }
}
