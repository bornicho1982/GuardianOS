using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;

namespace Traveler.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Dashboard "home" view with summary widgets.
/// </summary>
public class DashboardHomeViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;
    
    private int _maxPowerLevel;
    private int _artifactBonus;
    private int _postmasterCount;
    private int _vaultCount;
    private int _exoticCount;
    private bool _isLoading;
    private string _welcomeMessage = "Welcome, Guardian";

    public string Title => "Dashboard";

    public int MaxPowerLevel { get => _maxPowerLevel; set => this.RaiseAndSetIfChanged(ref _maxPowerLevel, value); }
    public int ArtifactBonus { get => _artifactBonus; set => this.RaiseAndSetIfChanged(ref _artifactBonus, value); }
    public int TotalPower => MaxPowerLevel + ArtifactBonus;
    
    public int PostmasterCount { get => _postmasterCount; set => this.RaiseAndSetIfChanged(ref _postmasterCount, value); }
    public bool PostmasterWarning => PostmasterCount >= 18; // 21 is max, warn at 18
    
    public int VaultCount { get => _vaultCount; set => this.RaiseAndSetIfChanged(ref _vaultCount, value); }
    public int ExoticCount { get => _exoticCount; set => this.RaiseAndSetIfChanged(ref _exoticCount, value); }
    
    public bool IsLoading { get => _isLoading; set => this.RaiseAndSetIfChanged(ref _isLoading, value); }
    public string WelcomeMessage { get => _welcomeMessage; set => this.RaiseAndSetIfChanged(ref _welcomeMessage, value); }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    // Design-time constructor
    public DashboardHomeViewModel()
    {
        _inventoryService = null!;
        RefreshCommand = null!;
        
        // Design-time mock data
        MaxPowerLevel = 1810;
        ArtifactBonus = 15;
        PostmasterCount = 19;
        VaultCount = 423;
        ExoticCount = 87;
    }

    public DashboardHomeViewModel(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        
        // Auto-refresh on construction
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsLoading = true;

        try
        {
            await _inventoryService.RefreshInventoryAsync();
            CalculateStats();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CalculateStats()
    {
        var items = _inventoryService.AllItems;

        // Calculate max power level (highest power by slot)
        var slotPowers = new Dictionary<string, int>
        {
            { "Kinetic", 0 }, { "Energy", 0 }, { "Power", 0 },
            { "Helmet", 0 }, { "Gauntlets", 0 }, { "Chest", 0 }, { "Legs", 0 }, { "Class", 0 }
        };

        foreach (var item in items)
        {
            var slot = InferSlot(item.ItemType);
            if (slot != null && item.PowerLevel > slotPowers.GetValueOrDefault(slot, 0))
            {
                slotPowers[slot] = item.PowerLevel;
            }
        }

        // Average of 8 highest power items
        var topPowers = slotPowers.Values.Where(p => p > 0).OrderByDescending(p => p).Take(8).ToList();
        MaxPowerLevel = topPowers.Count > 0 ? (int)topPowers.Average() : 0;

        // Artifact bonus would come from API - using placeholder
        ArtifactBonus = 15;

        // Counts
        VaultCount = items.Count(i => i.Location == "vault");
        ExoticCount = items.Count(i => i.IsExotic);
        
        // Postmaster count (would need specific bucket filtering in real implementation)
        PostmasterCount = 0; // Placeholder - requires postmaster bucket detection

        this.RaisePropertyChanged(nameof(TotalPower));
        this.RaisePropertyChanged(nameof(PostmasterWarning));
    }

    private string? InferSlot(string itemType)
    {
        if (itemType.Contains("Cannon", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Rifle", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Bow", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Scout", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Pulse", StringComparison.OrdinalIgnoreCase))
            return "Kinetic";

        if (itemType.Contains("Submachine", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Fusion", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Sidearm", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Shotgun", StringComparison.OrdinalIgnoreCase))
            return "Energy";

        if (itemType.Contains("Launcher", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Sword", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Machine Gun", StringComparison.OrdinalIgnoreCase) ||
            itemType.Contains("Linear", StringComparison.OrdinalIgnoreCase))
            return "Power";

        if (itemType.Contains("Helmet", StringComparison.OrdinalIgnoreCase)) return "Helmet";
        if (itemType.Contains("Gauntlets", StringComparison.OrdinalIgnoreCase)) return "Gauntlets";
        if (itemType.Contains("Chest", StringComparison.OrdinalIgnoreCase)) return "Chest";
        if (itemType.Contains("Leg", StringComparison.OrdinalIgnoreCase)) return "Legs";
        if (itemType.Contains("Class", StringComparison.OrdinalIgnoreCase)) return "Class";

        return null;
    }
}
