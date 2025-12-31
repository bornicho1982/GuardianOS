using ReactiveUI;
using System.Reactive.Linq;
using Traveler.Core.Services;

namespace Traveler.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase _currentPage = null!;
    private int _selectedIndex;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedIndex, value);
            UpdateCurrentPage();
        }
    }

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }
    
    // Child ViewModels
    private readonly DashboardHomeViewModel _dashboardHomeVm;
    private readonly InventoryViewModel _inventoryVm;
    private readonly LoadoutsViewModel _loadoutsVm;
    private readonly BuildArchitectViewModel _buildVm;
    private readonly VendorsViewModel _vendorsVm;
    private readonly TriumphsViewModel _triumphsVm;
    private readonly OrganizerViewModel _organizerVm;
    private readonly SettingsViewModel _settingsVm;

    public MainWindowViewModel(
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

        // Ensure Localization Service is initialized
        var loc = LocalizationService.Instance;

        // Default selection
        SelectedIndex = 0;
        CurrentPage = _dashboardHomeVm;
    }

    private void UpdateCurrentPage()
    {
        CurrentPage = SelectedIndex switch
        {
            0 => _dashboardHomeVm,
            1 => _inventoryVm,
            2 => _loadoutsVm,
            // 3 => _vendorsVm, // If added to UI later
            // 4 => _triumphsVm, // If added to UI later
            // 5 => _settingsVm, // Settings is a button outside listbox in XAML logic, will handle separate command if needed
            _ => _dashboardHomeVm
        };
    }
}
