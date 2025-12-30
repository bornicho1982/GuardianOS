using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;
using Traveler.Data.Auth;
using Traveler.Desktop.Views;

namespace Traveler.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Dashboard "Command Center" view.
/// Shows character overview, connection status, and quick actions.
/// </summary>
public class DashboardHomeViewModel : ViewModelBase
{
    private readonly IInventoryService? _inventoryService;
    private readonly BungieAuthService? _authService;
    
    // Connection state
    private bool _isConnected;
    private bool _isConnecting;
    private string _connectionStatus = "Not Connected";
    
    // Character data
    private CharacterInfo? _selectedCharacter;
    private ObservableCollection<CharacterInfo> _characters = new();
    
    // Stats
    private int _maxPowerLevel;
    private int _artifactBonus;
    private int _vaultCount;
    private int _postmasterCount;
    private int _exoticCount;
    private bool _isLoading;

    public string Title => "Command Center";

    // Connection properties
    public bool IsConnected { get => _isConnected; set => this.RaiseAndSetIfChanged(ref _isConnected, value); }
    public bool IsConnecting { get => _isConnecting; set => this.RaiseAndSetIfChanged(ref _isConnecting, value); }
    public string ConnectionStatus { get => _connectionStatus; set => this.RaiseAndSetIfChanged(ref _connectionStatus, value); }

    // Character properties
    public ObservableCollection<CharacterInfo> Characters { get => _characters; set => this.RaiseAndSetIfChanged(ref _characters, value); }
    public CharacterInfo? SelectedCharacter 
    { 
        get => _selectedCharacter; 
        set 
        {
            this.RaiseAndSetIfChanged(ref _selectedCharacter, value);
            this.RaisePropertyChanged(nameof(HasSelectedCharacter));
            this.RaisePropertyChanged(nameof(SelectedClassName));
        }
    }
    public bool HasSelectedCharacter => SelectedCharacter != null;
    public string SelectedClassName => SelectedCharacter?.ClassName ?? "Guardian";

    // Stats properties
    public int MaxPowerLevel { get => _maxPowerLevel; set => this.RaiseAndSetIfChanged(ref _maxPowerLevel, value); }
    public int ArtifactBonus { get => _artifactBonus; set => this.RaiseAndSetIfChanged(ref _artifactBonus, value); }
    public int TotalPower => MaxPowerLevel + ArtifactBonus;
    public int VaultCount { get => _vaultCount; set => this.RaiseAndSetIfChanged(ref _vaultCount, value); }
    public int PostmasterCount { get => _postmasterCount; set => this.RaiseAndSetIfChanged(ref _postmasterCount, value); }
    public int ExoticCount { get => _exoticCount; set => this.RaiseAndSetIfChanged(ref _exoticCount, value); }
    public bool IsLoading { get => _isLoading; set => this.RaiseAndSetIfChanged(ref _isLoading, value); }
    public bool PostmasterWarning => PostmasterCount >= 18;

    // Commands
    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<CharacterInfo, Unit> SelectCharacterCommand { get; }
    public ReactiveCommand<CharacterInfo, Unit> OpenGuardianDetailCommand { get; }

    // UI Helper Properties
    public string ConnectionButtonText => IsConnected ? "Disconnect" : "Connect";
    public string ConnectionStatusColor => IsConnected ? "#4CAF50" : "#AAA";
    public string VaultItemCount => $"{VaultCount} / 600";
    public ReactiveCommand<Unit, Unit> ToggleConnectionCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshInventoryCommand { get; }

    // Design-time constructor
    public DashboardHomeViewModel()
    {
        _inventoryService = null;
        _authService = null;
        ConnectCommand = ReactiveCommand.Create(() => { });
        DisconnectCommand = ReactiveCommand.Create(() => { });
        RefreshCommand = ReactiveCommand.Create(() => { });
        SelectCharacterCommand = ReactiveCommand.Create<CharacterInfo>(_ => { });
        OpenGuardianDetailCommand = ReactiveCommand.Create<CharacterInfo>(_ => { });
        ToggleConnectionCommand = ReactiveCommand.Create(() => { });
        RefreshInventoryCommand = ReactiveCommand.Create(() => { });
        
        // Design-time mock data
        IsConnected = true;
        ConnectionStatus = "Connected";
        MaxPowerLevel = 1818;
        ArtifactBonus = 15;
        VaultCount = 423;
        PostmasterCount = 5;
        ExoticCount = 87;
        
        Characters = new ObservableCollection<CharacterInfo>
        {
            new() { ClassName = "Titan", RaceName = "Human", LightLevel = 1820, CharacterId = 1 },
            new() { ClassName = "Hunter", RaceName = "Human", LightLevel = 1815, CharacterId = 2 },
            new() { ClassName = "Warlock", RaceName = "Exo", LightLevel = 1810, CharacterId = 3 }
        };
        SelectedCharacter = Characters.FirstOrDefault();
    }

    public DashboardHomeViewModel(IInventoryService inventoryService, BungieAuthService authService)
    {
        _inventoryService = inventoryService;
        _authService = authService;
        
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync);
        DisconnectCommand = ReactiveCommand.Create(Disconnect);
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        SelectCharacterCommand = ReactiveCommand.Create<CharacterInfo>(SelectCharacter);
        OpenGuardianDetailCommand = ReactiveCommand.Create<CharacterInfo>(OpenGuardianDetail);
        ToggleConnectionCommand = ReactiveCommand.CreateFromTask(ToggleConnectionAsync);
        RefreshInventoryCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
        
        // Subscribe to inventory refresh complete event
        _inventoryService.InventoryRefreshed += OnInventoryRefreshed;
        
        // Check initial connection state
        UpdateConnectionState();
        
        // Auto-load if already connected (deferred to avoid startup crash)
        if (_authService.IsAuthenticated)
        {
            Dispatcher.UIThread.Post(async () => 
            {
                try 
                {
                    await RefreshAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Dashboard] Auto-refresh failed: {ex.Message}");
                }
            });
        }
    }

    private void OnInventoryRefreshed()
    {
        // Use Dispatcher to ensure UI updates happen on the UI thread
        Dispatcher.UIThread.Post(() =>
        {
            Console.WriteLine($"[Dashboard] Inventory refresh complete ({_inventoryService?.AllItems.Count ?? 0} items)");
            CalculateStats();
            LoadCharacters();
        });
    }

    private async Task ConnectAsync()
    {
        if (_authService == null) return;
        
        IsConnecting = true;
        ConnectionStatus = "Connecting...";
        
        try
        {
            var success = await _authService.LoginAsync();
            if (success)
            {
                IsConnected = true;
                ConnectionStatus = "Connected";
                Console.WriteLine("[Dashboard] Connected to Bungie");
                await RefreshAsync();
            }
            else
            {
                ConnectionStatus = "Connection Failed";
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private void Disconnect()
    {
        IsConnected = false;
        ConnectionStatus = "Not Connected";
        Characters.Clear();
        SelectedCharacter = null;
        MaxPowerLevel = 0;
        VaultCount = 0;
        ExoticCount = 0;
        Console.WriteLine("[Dashboard] Disconnected");
    }

    private void UpdateConnectionState()
    {
        if (_authService == null) return;
        
        IsConnected = _authService.IsAuthenticated;
        ConnectionStatus = IsConnected ? "Connected" : "Not Connected";
    }

    private async Task RefreshAsync()
    {
        if (_inventoryService == null) return;
        if (_authService == null || !_authService.IsAuthenticated)
        {
            ConnectionStatus = "Not Connected";
            Console.WriteLine("[Dashboard] Cannot refresh - not authenticated");
            return;
        }
        
        IsLoading = true;
        try
        {
            await _inventoryService.RefreshInventoryAsync();
            ConnectionStatus = "Connected";
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"[Dashboard] Auth error: {ex.Message}");
            ConnectionStatus = "Not Connected";
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[Dashboard] API error: {ex.Message}");
            ConnectionStatus = $"API Error";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dashboard] Refresh error: {ex.Message}");
            ConnectionStatus = "Error";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SelectCharacter(CharacterInfo character)
    {
        SelectedCharacter = character;
        Console.WriteLine($"[Dashboard] Selected character: {character.ClassName} ({character.LightLevel})");
        
        // Update equipped items
        UpdateEquippedItems();
    }

    private void OpenGuardianDetail(CharacterInfo character)
    {
        if (_inventoryService == null) return;
        
        Console.WriteLine($"[Dashboard] Opening detail for: {character.ClassName}");
        var viewModel = new GuardianDetailViewModel(character, _inventoryService);
        var window = new GuardianDetailWindow(viewModel);
        window.Show();
    }

    private async Task ToggleConnectionAsync()
    {
        if (IsConnected)
        {
            Disconnect();
        }
        else
        {
            await ConnectAsync();
        }
        this.RaisePropertyChanged(nameof(ConnectionButtonText));
        this.RaisePropertyChanged(nameof(ConnectionStatusColor));
    }
    
    // Equipped items for selected character
    private InventoryItem? _equippedKinetic;
    private InventoryItem? _equippedEnergy;
    private InventoryItem? _equippedPower;
    private InventoryItem? _equippedHelmet;
    private InventoryItem? _equippedGauntlets;
    private InventoryItem? _equippedChest;
    private InventoryItem? _equippedLegs;
    private InventoryItem? _equippedClassItem;
    
    public InventoryItem? EquippedKinetic { get => _equippedKinetic; set => this.RaiseAndSetIfChanged(ref _equippedKinetic, value); }
    public InventoryItem? EquippedEnergy { get => _equippedEnergy; set => this.RaiseAndSetIfChanged(ref _equippedEnergy, value); }
    public InventoryItem? EquippedPower { get => _equippedPower; set => this.RaiseAndSetIfChanged(ref _equippedPower, value); }
    public InventoryItem? EquippedHelmet { get => _equippedHelmet; set => this.RaiseAndSetIfChanged(ref _equippedHelmet, value); }
    public InventoryItem? EquippedGauntlets { get => _equippedGauntlets; set => this.RaiseAndSetIfChanged(ref _equippedGauntlets, value); }
    public InventoryItem? EquippedChest { get => _equippedChest; set => this.RaiseAndSetIfChanged(ref _equippedChest, value); }
    public InventoryItem? EquippedLegs { get => _equippedLegs; set => this.RaiseAndSetIfChanged(ref _equippedLegs, value); }
    public InventoryItem? EquippedClassItem { get => _equippedClassItem; set => this.RaiseAndSetIfChanged(ref _equippedClassItem, value); }
    
    // Subclass
    private InventoryItem? _equippedSubclass;
    public InventoryItem? EquippedSubclass { get => _equippedSubclass; set => this.RaiseAndSetIfChanged(ref _equippedSubclass, value); }
    
    // Extra Items
    private InventoryItem? _equippedArtifact;
    private InventoryItem? _equippedClanBanner;
    public InventoryItem? EquippedArtifact { get => _equippedArtifact; set => this.RaiseAndSetIfChanged(ref _equippedArtifact, value); }
    public InventoryItem? EquippedClanBanner { get => _equippedClanBanner; set => this.RaiseAndSetIfChanged(ref _equippedClanBanner, value); }
    
    // Bucket hashes for equipment slots
    private static class BucketHashes
    {
        public const uint Kinetic = 1498876634;
        public const uint Energy = 2465295065;
        public const uint Power = 953998645;
        public const uint Helmet = 3448274439;
        public const uint Gauntlets = 3551918588;
        public const uint Chest = 14239492;
        public const uint Legs = 20886954;
        public const uint ClassItem = 1585787867;
        public const uint Subclass = 3284755031;
        public const uint SeasonalArtifact = 1506418338;
        public const uint ClanBanner = 375726501;
    }
    
    private void UpdateEquippedItems()
    {
        if (_inventoryService == null || SelectedCharacter == null) return;
        
        var charId = SelectedCharacter.CharacterId.ToString();
        Console.WriteLine($"[Dashboard] Looking for equipped items for character {charId}");
        
        // First check how many equipped items exist
        var allEquipped = _inventoryService.AllItems.Where(i => i.IsEquipped).ToList();
        Console.WriteLine($"[Dashboard] Total equipped items in inventory: {allEquipped.Count}");
        
        var equipped = _inventoryService.AllItems
            .Where(i => i.IsEquipped && i.Location == charId)
            .ToList();
        
        Console.WriteLine($"[Dashboard] Found {equipped.Count} equipped items for this character");
        
        foreach (var item in equipped.Take(5))
        {
            Console.WriteLine($"[Dashboard]   - {item.Name} (Bucket: {item.BucketHash}, Icon: {item.Icon})");
        }
        
        EquippedKinetic = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Kinetic);
        EquippedEnergy = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Energy);
        EquippedPower = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Power);
        EquippedHelmet = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Helmet);
        EquippedGauntlets = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Gauntlets);
        EquippedChest = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Chest);
        EquippedLegs = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Legs);
        EquippedClassItem = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.ClassItem);
        EquippedClassItem = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.ClassItem);
        EquippedSubclass = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.Subclass);
        EquippedArtifact = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.SeasonalArtifact);
        EquippedClanBanner = equipped.FirstOrDefault(i => i.BucketHash == BucketHashes.ClanBanner);
        
        Console.WriteLine($"[Dashboard] DEBUG: Kinetic AmmoIcon: '{EquippedKinetic?.AmmoIconUrl}' AmmoType: {EquippedKinetic?.AmmoType}");
        Console.WriteLine($"[Dashboard] DEBUG: Kinetic Watermark: '{EquippedKinetic?.WatermarkIconUrl}'");
        Console.WriteLine($"[Dashboard] DEBUG: Helmet Power: {EquippedHelmet?.PowerLevel} IsMasterwork: {EquippedHelmet?.IsMasterwork}");
        Console.WriteLine($"[Dashboard] DEBUG: Artifact Icon: '{EquippedArtifact?.IconUrl}'");
        Console.WriteLine($"[Dashboard] DEBUG: Subclass Abilities: {EquippedSubclass?.AbilityIcons?.Count ?? 0}");
        
        Console.WriteLine($"[Dashboard] Equipped items - K:{EquippedKinetic?.Name ?? "?"} Subclass:{EquippedSubclass?.Name ?? "?"}");
    }

    private void LoadCharacters()
    {
        if (_inventoryService == null) return;
        
        // Use real character data from the InventoryService
        Characters.Clear();
        foreach (var character in _inventoryService.Characters)
        {
            Characters.Add(character);
        }
        
        // If no real characters, still not authenticated
        if (!Characters.Any() && _authService?.IsAuthenticated == true)
        {
            Console.WriteLine("[Dashboard] No characters found in InventoryService");
        }
        
        // Select the first character and update equipped items
        if (SelectedCharacter == null && Characters.Any())
        {
            SelectCharacter(Characters.First());
        }
        else if (SelectedCharacter != null)
        {
            // Re-update equipped items for currently selected character
            UpdateEquippedItems();
        }
    }

    private void CalculateStats()
    {
        if (_inventoryService == null) return;
        
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
        PostmasterCount = 0; // Requires postmaster bucket detection

        this.RaisePropertyChanged(nameof(TotalPower));
        this.RaisePropertyChanged(nameof(PostmasterWarning));
        
        Console.WriteLine($"[Dashboard] Stats: Power={MaxPowerLevel}+{ArtifactBonus}, Vault={VaultCount}, Exotics={ExoticCount}");
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
