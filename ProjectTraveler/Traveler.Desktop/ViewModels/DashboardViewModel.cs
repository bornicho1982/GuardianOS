using ReactiveUI;

namespace Traveler.Desktop.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private ViewModelBase _currentView;

    public ViewModelBase CurrentView
    {
        get => _currentView;
        private set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }
    
    public InventoryViewModel InventoryVm { get; }

    public DashboardViewModel(InventoryViewModel inventoryVm)
    {
        InventoryVm = inventoryVm;
        CurrentView = InventoryVm;
    }
}
