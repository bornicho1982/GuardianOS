using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using ReactiveUI;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;
using Traveler.Core.Services;

namespace Traveler.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Organizer view with advanced filtering (DIM-style).
/// </summary>
public class OrganizerViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;
    private readonly FilterParser _filterParser;
    
    private string _filterText = "";
    private bool _showBaseStats;
    private InventoryItem? _selectedItem;

    public string Title => "Organizer";

    /// <summary>
    /// Full inventory (unfiltered source).
    /// </summary>
    public ObservableCollection<InventoryItem> AllItems { get; } = new();

    /// <summary>
    /// Filtered items based on current query.
    /// </summary>
    public ObservableCollection<InventoryItem> FilteredItems { get; } = new();

    public string FilterText
    {
        get => _filterText;
        set
        {
            this.RaiseAndSetIfChanged(ref _filterText, value);
            ApplyFilter();
        }
    }

    public bool ShowBaseStats
    {
        get => _showBaseStats;
        set => this.RaiseAndSetIfChanged(ref _showBaseStats, value);
    }

    public InventoryItem? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFilterCommand { get; }

    // Design-time constructor
    public OrganizerViewModel()
    {
        _inventoryService = null!;
        _filterParser = new FilterParser();
        RefreshCommand = null!;
        ClearFilterCommand = null!;
    }

    public OrganizerViewModel(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        _filterParser = new FilterParser();

        RefreshCommand = ReactiveCommand.Create(LoadItems);
        ClearFilterCommand = ReactiveCommand.Create(() => { FilterText = ""; });

        LoadItems();
    }

    private void LoadItems()
    {
        AllItems.Clear();
        FilteredItems.Clear();

        foreach (var item in _inventoryService.AllItems)
        {
            AllItems.Add(item);
            FilteredItems.Add(item);
        }
    }

    private void ApplyFilter()
    {
        FilteredItems.Clear();

        if (string.IsNullOrWhiteSpace(FilterText))
        {
            foreach (var item in AllItems)
                FilteredItems.Add(item);
            return;
        }

        try
        {
            var predicate = _filterParser.Parse(FilterText);
            foreach (var item in AllItems.Where(predicate))
            {
                FilteredItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Filter parse error: {ex.Message}");
            // On error, show all items
            foreach (var item in AllItems)
                FilteredItems.Add(item);
        }
    }
}
