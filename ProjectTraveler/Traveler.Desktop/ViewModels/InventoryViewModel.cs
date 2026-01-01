using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;

using Traveler.Desktop.Stores;

namespace Traveler.Desktop.ViewModels;

public class InventoryViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ISmartMoveService _smartMoveService;
    private readonly IInventoryBucketService _bucketService;
    private readonly IInventoryFilterService _filterService;
    private readonly InventoryStore _inventoryStore;
    
    private string _searchText = "";
    private CharacterInfo? _selectedCharacter;
    private string _selectedBucketFilter = "All";

    public ObservableCollection<InventoryItem> Items => _inventoryStore.AllItems;
    public ObservableCollection<CharacterInfo> Characters => _inventoryService.Characters;
    
    // === SELECTED CHARACTER ===
    public CharacterInfo? SelectedCharacter
    {
        get => _selectedCharacter;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCharacter, value);
            OnCharacterChanged();
        }
    }
    
    // === SELECTED BUCKET FILTER (For sync scroll) ===
    public string SelectedBucketFilter
    {
        get => _selectedBucketFilter;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedBucketFilter, value);
            this.RaisePropertyChanged(nameof(FilteredVaultItems));
            this.RaisePropertyChanged(nameof(VaultItemCount));
            // Notify all bucket filters for active state
            this.RaisePropertyChanged(nameof(IsKineticActive));
            this.RaisePropertyChanged(nameof(IsEnergyActive));
            this.RaisePropertyChanged(nameof(IsPowerActive));
            this.RaisePropertyChanged(nameof(IsHelmetActive));
            this.RaisePropertyChanged(nameof(IsGauntletsActive));
            this.RaisePropertyChanged(nameof(IsChestActive));
            this.RaisePropertyChanged(nameof(IsLegsActive));
            this.RaisePropertyChanged(nameof(IsClassItemActive));
        }
    }
    
    // === BUCKET FILTER ACTIVE STATES ===
    public bool IsKineticActive => SelectedBucketFilter == "Kinetic Weapons";
    public bool IsEnergyActive => SelectedBucketFilter == "Energy Weapons";
    public bool IsPowerActive => SelectedBucketFilter == "Power Weapons";
    public bool IsHelmetActive => SelectedBucketFilter == "Helmet";
    public bool IsGauntletsActive => SelectedBucketFilter == "Gauntlets";
    public bool IsChestActive => SelectedBucketFilter == "Chest Armor";
    public bool IsLegsActive => SelectedBucketFilter == "Leg Armor";
    public bool IsClassItemActive => SelectedBucketFilter == "Class Armor";
    
    // === SEARCH ===
    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            OnInventoryRefreshed();
        }
    }
    
    private IEnumerable<InventoryItem> FilterItems(IEnumerable<InventoryItem> items)
    {
        return _filterService.FilterItems(items, SearchText);
    }
    
    // === EQUIPPED ITEMS (1 per slot) ===
    public InventoryItem? EquippedKinetic => GetEquipped("Kinetic Weapons");
    public InventoryItem? EquippedEnergy => GetEquipped("Energy Weapons");
    public InventoryItem? EquippedPower => GetEquipped("Power Weapons");
    public InventoryItem? EquippedHelmet => GetEquipped("Helmet");
    public InventoryItem? EquippedGauntlets => GetEquipped("Gauntlets");
    public InventoryItem? EquippedChest => GetEquipped("Chest Armor");
    public InventoryItem? EquippedLegs => GetEquipped("Leg Armor");
    public InventoryItem? EquippedClassItem => GetEquipped("Class Armor");
    
    private InventoryItem? GetEquipped(string bucketType)
    {
        if (SelectedCharacter == null) return null;
        return Items.FirstOrDefault(i => 
            i.Location == SelectedCharacter.CharacterId.ToString() &&
            i.BucketType == bucketType &&
            i.IsEquipped);
    }
    
    // ... existing fields ...

    // ... existing properties ...

    // === INVENTORY COLLECTIONS (Redirection to Service Buckets) ===
    // === BUCKET FILTERED ITEMS ===
    private ObservableCollection<InventoryBucket> _buckets = new();
    public ObservableCollection<InventoryBucket> Buckets
    {
        get => _buckets;
        set => this.RaiseAndSetIfChanged(ref _buckets, value);
    }

    // Keep legacy properties for now, but they will be empty if not populated via DistributeItems
    // Ideally the View should bind to Buckets directly
    public ObservableCollection<InventoryItem> KineticWeapons => _bucketService.GetBucket(BucketCategory.Kinetic).Items;
    public ObservableCollection<InventoryItem> EnergyWeapons => _bucketService.GetBucket(BucketCategory.Energy).Items;
    public ObservableCollection<InventoryItem> PowerWeapons => _bucketService.GetBucket(BucketCategory.Power).Items;
    public ObservableCollection<InventoryItem> Helmets => _bucketService.GetBucket(BucketCategory.Helmet).Items;
    public ObservableCollection<InventoryItem> Gauntlets => _bucketService.GetBucket(BucketCategory.Gauntlets).Items;
    public ObservableCollection<InventoryItem> Chests => _bucketService.GetBucket(BucketCategory.Chest).Items;
    public ObservableCollection<InventoryItem> Legs => _bucketService.GetBucket(BucketCategory.Legs).Items;
    public ObservableCollection<InventoryItem> ClassItems => _bucketService.GetBucket(BucketCategory.ClassItem).Items;


    // Legacy Aliases for View Compatibility
    public IEnumerable<InventoryItem> InventoryKinetic => KineticWeapons;
    public IEnumerable<InventoryItem> InventoryEnergy => EnergyWeapons;
    public IEnumerable<InventoryItem> InventoryPower => PowerWeapons;
    public IEnumerable<InventoryItem> InventoryHelmet => Helmets;
    public IEnumerable<InventoryItem> InventoryGauntlets => Gauntlets;
    public IEnumerable<InventoryItem> InventoryChest => Chests;
    public IEnumerable<InventoryItem> InventoryLegs => Legs;
    public IEnumerable<InventoryItem> InventoryClassItem => ClassItems;

    private void SortItems(IEnumerable<InventoryItem> items)
    {
        if (SelectedCharacter == null) return;
        var charId = SelectedCharacter.CharacterId.ToString();

        // 1. Filter for current character and unequipped items
        var charItems = items.Where(i => i.Location == charId && !i.IsEquipped).ToList();

        // 2. Use new Bucketize logic
        var newBuckets = _bucketService.Bucketize(charItems);
        Buckets = new ObservableCollection<InventoryBucket>(newBuckets);
        
        // 3. Also call legacy DistributeItems to keep existing bindings working for now (KineticWeapons, etc.)
        _bucketService.DistributeItems(charItems);
    }
    
    // === FILTERED VAULT ITEMS (based on SelectedBucketFilter) ===
    public IEnumerable<InventoryItem> FilteredVaultItems
    {
        get
        {
            if (SelectedCharacter == null) return Enumerable.Empty<InventoryItem>();
            
            var charClass = SelectedCharacter.ClassName;
            var vaultItems = Items.Where(i => i.Location == "vault");
            
            // Apply bucket filter
            if (SelectedBucketFilter != "All")
            {
                vaultItems = vaultItems.Where(i => i.BucketType == SelectedBucketFilter);
            }
            
            // Filter armor by class (weapons work for all classes)
            // Only filter if class type is specified and doesn't match
            var armorBuckets = new[] { "Helmet", "Gauntlets", "Chest Armor", "Leg Armor", "Class Armor" };
            vaultItems = vaultItems.Where(i => 
            {
                // If not armor, allow all (weapons)
                if (!armorBuckets.Contains(i.BucketType))
                    return true;
                    
                // For armor: if ClassType is empty/Any, allow it
                if (string.IsNullOrEmpty(i.ClassType) || i.ClassType == "Any")
                    return true;
                    
                // Otherwise, must match character class
                return i.ClassType == charClass;
            });
            
            return FilterItems(vaultItems);
        }
    }
    
    // === VAULT ITEM COUNT ===
    public int VaultItemCount => FilteredVaultItems.Count();
    
    // === CLEAN VAULT COLLECTIONS (Strict Bucket Filtering) ===
    // Weapon bucket hashes: Kinetic, Energy, Power
    private static readonly uint[] WeaponBuckets = { 1498876634, 2465295065, 953998645 };
    // Armor bucket hashes: Helmet, Gauntlets, Chest, Legs, Class Item
    private static readonly uint[] ArmorBuckets = { 3448274439, 3551918588, 14239492, 20886954, 1585787867 };
    
    /// <summary>
    /// Vault items filtered to only WEAPONS (Kinetic, Energy, Power).
    /// Excludes ships, sparrows, ghosts, emblems, etc.
    /// </summary>
    public IEnumerable<InventoryItem> VaultWeapons
    {
        get
        {
            var vaultItems = Items.Where(i => i.Location == "vault");
            return FilterItems(vaultItems.Where(i => WeaponBuckets.Contains(i.BucketHash)));
        }
    }
    
    /// <summary>
    /// Vault items filtered to only ARMOR (Helmet, Gauntlets, Chest, Legs, Class Item).
    /// Also filters by current character class.
    /// </summary>
    public IEnumerable<InventoryItem> VaultArmor
    {
        get
        {
            if (SelectedCharacter == null) return Enumerable.Empty<InventoryItem>();
            
            var charClass = SelectedCharacter.ClassName;
            var vaultItems = Items.Where(i => i.Location == "vault");
            
            return FilterItems(vaultItems.Where(i => 
                ArmorBuckets.Contains(i.BucketHash) &&
                (string.IsNullOrEmpty(i.ClassType) || i.ClassType == "Any" || i.ClassType == charClass)
            ));
        }
    }
    
    public int VaultWeaponsCount => VaultWeapons.Count();
    public int VaultArmorCount => VaultArmor.Count();
    
    // === CHARACTER SELECTION HELPER ===
    public bool IsCharacterSelected(CharacterInfo? character)
    {
        if (character == null || SelectedCharacter == null) return false;
        return character.CharacterId == SelectedCharacter.CharacterId;
    }

    // === COMMANDS ===
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RefreshCommand { get; }
    public ReactiveCommand<CharacterInfo, System.Reactive.Unit> SelectCharacterCommand { get; }
    public ReactiveCommand<string, System.Reactive.Unit> SetBucketFilterCommand { get; }
    public ICommand TransferItemCommand { get; }

    public InventoryViewModel(IInventoryService inventoryService, ISmartMoveService smartMoveService, IInventoryBucketService bucketService, IInventoryFilterService filterService)
    {
        _inventoryService = inventoryService;
        _smartMoveService = smartMoveService;
        _bucketService = bucketService;
        _filterService = filterService;
        _inventoryStore = InventoryStore.Instance;

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshInventory);
        SelectCharacterCommand = ReactiveCommand.Create<CharacterInfo>(SelectCharacter);
        SetBucketFilterCommand = ReactiveCommand.Create<string>(SetBucketFilter);
        
        TransferItemCommand = ReactiveCommand.CreateFromTask<Tuple<InventoryItem, bool>>(async (args) =>
        {
            var (item, toVault) = args;
            var targetId = SelectedCharacter?.CharacterId.ToString() ?? "";
            
            if (toVault && item.Location == "vault") return;
            if (!toVault && item.Location == targetId) return;
            
            await _inventoryService.TransferItemAsync(item, targetId, toVault);
            await RefreshInventory();
        });
        
        _inventoryService.InventoryRefreshed += OnInventoryRefreshed;
        _inventoryStore.ItemsChanged += () => 
        {
             // Trigger UI update when Store changes
             OnInventoryRefreshed();
        };
        
        RxApp.MainThreadScheduler.Schedule(async () => await RefreshInventory());
    }
    
    private void SelectCharacter(CharacterInfo character)
    {
        SelectedCharacter = character;
    }
    
    private void SetBucketFilter(string bucketType)
    {
        SelectedBucketFilter = bucketType;
    }
    
    private void OnCharacterChanged()
    {
        // Re-sort items for the new character
        SortItems(Items);

        // Notify all equipped items
        this.RaisePropertyChanged(nameof(EquippedKinetic));
        this.RaisePropertyChanged(nameof(EquippedEnergy));
        this.RaisePropertyChanged(nameof(EquippedPower));
        this.RaisePropertyChanged(nameof(EquippedHelmet));
        this.RaisePropertyChanged(nameof(EquippedGauntlets));
        this.RaisePropertyChanged(nameof(EquippedChest));
        this.RaisePropertyChanged(nameof(EquippedLegs));
        this.RaisePropertyChanged(nameof(EquippedClassItem));
        
        // Notify all inventory items
        this.RaisePropertyChanged(nameof(InventoryKinetic));
        this.RaisePropertyChanged(nameof(InventoryEnergy));
        this.RaisePropertyChanged(nameof(InventoryPower));
        this.RaisePropertyChanged(nameof(InventoryHelmet));
        this.RaisePropertyChanged(nameof(InventoryGauntlets));
        this.RaisePropertyChanged(nameof(InventoryChest));
        this.RaisePropertyChanged(nameof(InventoryLegs));
        this.RaisePropertyChanged(nameof(InventoryClassItem));
        
        // Notify vault
        this.RaisePropertyChanged(nameof(FilteredVaultItems));
        this.RaisePropertyChanged(nameof(VaultWeapons));
        this.RaisePropertyChanged(nameof(VaultArmor));
        this.RaisePropertyChanged(nameof(VaultWeaponsCount));
        this.RaisePropertyChanged(nameof(VaultArmorCount));
    }

    private void OnInventoryRefreshed()
    {
        if (SelectedCharacter == null && Characters.Any())
        {
            SelectedCharacter = Characters.First();
        }
        
        this.RaisePropertyChanged(nameof(Characters));
        SortItems(Items); // Ensure items are sorted on refresh
        OnCharacterChanged();
    }

    public InventoryViewModel() 
    {
        _inventoryService = null!;
        _smartMoveService = null!;
        _bucketService = null!;
        _filterService = null!;
        _inventoryStore = null!;
        RefreshCommand = null!;
        SelectCharacterCommand = null!;
        SetBucketFilterCommand = null!;
        TransferItemCommand = null!;
    }

    private async Task RefreshInventory()
    {
        await _inventoryService.RefreshInventoryAsync();
        // Sync Store with Service data (Decoupling step 1)
        _inventoryStore.UpdateItems(_inventoryService.AllItems);
    }
}
