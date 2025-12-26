using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using ReactiveUI;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;

namespace Traveler.Desktop.ViewModels;

public class InventoryViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ISmartMoveService _smartMoveService;

    public ObservableCollection<InventoryItem> Items => _inventoryService.AllItems;

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RefreshCommand { get; }

    public InventoryViewModel(IInventoryService inventoryService, ISmartMoveService smartMoveService)
    {
        _inventoryService = inventoryService;
        _smartMoveService = smartMoveService;

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshInventory);
        
        // Auto-load checks
        RxApp.MainThreadScheduler.Schedule(async () => await RefreshInventory());
    }

    // Design-time constructor
    public InventoryViewModel() 
    {
    }

    private async Task RefreshInventory()
    {
        await _inventoryService.RefreshInventoryAsync();
    }
}
