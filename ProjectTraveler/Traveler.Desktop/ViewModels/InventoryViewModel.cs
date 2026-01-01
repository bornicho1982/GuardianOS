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

namespace Traveler.Desktop.ViewModels;

public class InventoryViewModel : ViewModelBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ISmartMoveService _smartMoveService;
    
    private string _searchText = "";
    private CharacterInfo? _selectedCharacter;
    private string _selectedBucketFilter = "All";

    public ObservableCollection<InventoryItem> Items => _inventoryService.AllItems;
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
        if (string.IsNullOrWhiteSpace(SearchText))
            return items;
            
        var search = SearchText.ToLower();
        return items.Where(i => 
            i.Name.ToLower().Contains(search) ||
            i.ItemType.ToLower().Contains(search) ||
            i.TierType.ToLower().Contains(search));
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
    
    // === INVENTORY COLLECTIONS (Separated by Bucket) ===
    public ObservableCollection<InventoryItem> KineticWeapons { get; } = new();
    public ObservableCollection<InventoryItem> EnergyWeapons { get; } = new();
    public ObservableCollection<InventoryItem> PowerWeapons { get; } = new();
    public ObservableCollection<InventoryItem> Helmets { get; } = new();
    public ObservableCollection<InventoryItem> Gauntlets { get; } = new();
    public ObservableCollection<InventoryItem> Chests { get; } = new();
    public ObservableCollection<InventoryItem> Legs { get; } = new();
    public ObservableCollection<InventoryItem> ClassItems { get; } = new();

    // Legacy Aliases for View Compatibility (Optional, can be updated in XAML later)
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
        // 1. Clear existing collections
        KineticWeapons.Clear();
        EnergyWeapons.Clear();
        PowerWeapons.Clear();
        Helmets.Clear();
        Gauntlets.Clear();
        Chests.Clear();
        Legs.Clear();
        ClassItems.Clear();

        if (SelectedCharacter == null) return;
        var charId = SelectedCharacter.CharacterId.ToString();

        // 2. Filter for current character and unequipped items
        // (Assuming we only want the inventory grid items here, distinct from Equipped* properties)
        var charItems = items.Where(i => i.Location == charId && !i.IsEquipped);

        // 3. Sort into buckets
        foreach (var item in charItems)
        {
            switch (item.BucketHash)
            {
                case 1498876634: KineticWeapons.Add(item); break;
                case 2465295065: EnergyWeapons.Add(item); break;
                case 953998645:  PowerWeapons.Add(item); break;
                case 3448274439: Helmets.Add(item); break;
                case 3551918588: Gauntlets.Add(item); break;
                case 14239492:   Chests.Add(item); break;
                case 20886954:   Legs.Add(item); break;
                case 1585787867: ClassItems.Add(item); break;
            }
        }
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

    public InventoryViewModel(IInventoryService inventoryService, ISmartMoveService smartMoveService)
    {
        _inventoryService = inventoryService;
        _smartMoveService = smartMoveService;

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
        RefreshCommand = null!;
        SelectCharacterCommand = null!;
        SetBucketFilterCommand = null!;
        TransferItemCommand = null!;
    }

    private async Task RefreshInventory()
    {
        await _inventoryService.RefreshInventoryAsync();
    }
}
