using ReactiveUI;
using System;
using System.Collections.ObjectModel; // Added
using System.Linq; // Added

namespace Traveler.Desktop.ViewModels;

public class NavigationItem
{
    public string Label { get; }
    public string Icon { get; } // Placeholder for FluentSymbol or path
    public Type ViewModelType { get; }

    public NavigationItem(string label, string icon, Type viewModelType)
    {
        Label = label;
        Icon = icon;
        ViewModelType = viewModelType;
    }
}

public class DashboardViewModel : ViewModelBase
{
    private ViewModelBase _currentView = null!;
    private NavigationItem _selectedItem = null!;

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public NavigationItem SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (value == null) return;
            this.RaiseAndSetIfChanged(ref _selectedItem, value);
            NavigateTo(value);
        }
    }

    public ViewModelBase CurrentView
    {
        get => _currentView;
        private set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }
    
    // Services retained for DI
    private readonly DashboardHomeViewModel _dashboardHomeVm;
    private readonly InventoryViewModel _inventoryVm;
    private readonly LoadoutsViewModel _loadoutsVm;
    private readonly BuildArchitectViewModel _buildVm;
    private readonly VendorsViewModel _vendorsVm;
    private readonly TriumphsViewModel _triumphsVm;
    private readonly OrganizerViewModel _organizerVm;
    private readonly SettingsViewModel _settingsVm;

    public DashboardViewModel(
        DashboardHomeViewModel dashboardHomeVm,
        InventoryViewModel inventoryVm,
        LoadoutsViewModel loadoutsVm,
        BuildArchitectViewModel buildVm,
        VendorsViewModel vendorsVm,
        TriumphsViewModel triumphsVm,
        OrganizerViewModel organizerVm,
        SettingsViewModel settingsVm)
    {
        _dashboardHomeVm = dashboardHomeVm;
        _inventoryVm = inventoryVm;
        _loadoutsVm = loadoutsVm;
        _buildVm = buildVm;
        _vendorsVm = vendorsVm;
        _triumphsVm = triumphsVm;
        _organizerVm = organizerVm;
        _settingsVm = settingsVm;

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Dashboard", "Home", typeof(DashboardHomeViewModel)),
            new("Inventory", "Backpack", typeof(InventoryViewModel)),
            new("Loadouts", "TshirtCrew", typeof(LoadoutsViewModel)),
            new("Build Architect", "BrainCircuit", typeof(BuildArchitectViewModel)),
            new("Vendors", "ShoppingBag", typeof(VendorsViewModel)),
            new("Triumphs", "Trophy", typeof(TriumphsViewModel)),
            new("Organizer", "Broom", typeof(OrganizerViewModel)),
            new("Settings", "Settings", typeof(SettingsViewModel))
        };

        // Default selection - Dashboard
        SelectedItem = NavigationItems.First();
    }

    private void NavigateTo(NavigationItem item)
    {
        if (item.ViewModelType == typeof(DashboardHomeViewModel)) CurrentView = _dashboardHomeVm;
        else if (item.ViewModelType == typeof(InventoryViewModel)) CurrentView = _inventoryVm;
        else if (item.ViewModelType == typeof(LoadoutsViewModel)) CurrentView = _loadoutsVm;
        else if (item.ViewModelType == typeof(BuildArchitectViewModel)) CurrentView = _buildVm;
        else if (item.ViewModelType == typeof(VendorsViewModel)) CurrentView = _vendorsVm;
        else if (item.ViewModelType == typeof(TriumphsViewModel)) CurrentView = _triumphsVm;
        else if (item.ViewModelType == typeof(OrganizerViewModel)) CurrentView = _organizerVm;
        else if (item.ViewModelType == typeof(SettingsViewModel)) CurrentView = _settingsVm;
    }
}

